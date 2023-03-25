# Copyright(c) 2023 Jake Fowler
#
# Permission is hereby granted, free of charge, to any person
# obtaining a copy of this software and associated documentation
# files (the "Software"), to deal in the Software without
# restriction, including without limitation the rights to use,
# copy, modify, merge, publish, distribute, sublicense, and/or sell
# copies of the Software, and to permit persons to whom the
# Software is furnished to do so, subject to the following
# conditions:
#
# The above copyright notice and this permission notice shall be
# included in all copies or substantial portions of the Software.
#
# THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
# EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
# OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
# NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
# HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
# WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
# FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
# OTHER DEALINGS IN THE SOFTWARE.

import typing as tp
import numpy as np
import pandas as pd
from cmdty_storage import time_func as tf
import math
from cmdty_storage import utils


# TODO convert to common key types for vol curve and fwd contracts
class MultiFactorModel:
    _corr_tolerance = 1E-10  # TODO more scientific way of finding this.
    _factors: tp.List[tp.Tuple[float, utils.CurveType]]
    _factor_corrs: utils.FactorCorrsType
    _time_func: utils.TimeFunctionType

    def __init__(self,
                 freq: str,
                 factors: tp.Collection[tp.Tuple[float, utils.CurveType]],
                 factor_corrs: utils.FactorCorrsType = None,
                 time_func: tp.Optional[utils.TimeFunctionType] = None):
        self._factor_corrs = utils.validate_multi_factor_params(factors, factor_corrs)
        self._factors = list(factors)
        self._time_func = tf.act_365 if time_func is None else time_func

    def integrated_covar(self,
                         obs_start: utils.TimePeriodSpecType,
                         obs_end: utils.TimePeriodSpecType,
                         fwd_contract_1: utils.ForwardPointType,
                         fwd_contract_2: utils.ForwardPointType) -> float:
        obs_start_t = 0.0
        obs_end_t = self._time_func(obs_start, obs_end)
        if obs_end_t < 0.0:
            raise ValueError("obs_end cannot be before obs_start.")
        fwd_1_t = self._time_func(obs_start, fwd_contract_1)
        fwd_2_t = self._time_func(obs_start, fwd_contract_2)

        cov = 0.0
        for (i, j), corr in np.ndenumerate(self._factor_corrs):
            mr_i, vol_curve_i = self._factors[i]
            vol_i = self._get_factor_vol(i, fwd_contract_1,
                                         vol_curve_i)  # TODO if converted to nested loop vol_i could be looked up less
            mr_j, vol_curve_j = self._factors[j]
            vol_j = self._get_factor_vol(j, fwd_contract_2, vol_curve_j)
            cov += vol_i * vol_j * self._factor_corrs[i, j] * math.exp(-mr_i * fwd_1_t - mr_j * fwd_2_t) * \
                   self._cont_ext(-obs_start_t, -obs_end_t, mr_i + mr_j)
        return cov

    def integrated_variance(self,
                            obs_start: utils.TimePeriodSpecType,
                            obs_end: utils.TimePeriodSpecType,
                            fwd_contract: utils.ForwardPointType) -> float:
        return self.integrated_covar(obs_start, obs_end, fwd_contract, fwd_contract)

    def integrated_stan_dev(self,
                            obs_start: utils.TimePeriodSpecType,
                            obs_end: utils.TimePeriodSpecType,
                            fwd_contract: utils.ForwardPointType) -> float:
        return math.sqrt(self.integrated_covar(obs_start, obs_end, fwd_contract, fwd_contract))

    def integrated_vol(self,
                       val_date: utils.TimePeriodSpecType,
                       expiry: utils.TimePeriodSpecType,
                       fwd_contract: utils.ForwardPointType) -> float:
        time_to_expiry = self._time_func(val_date, expiry)
        if time_to_expiry <= 0:
            raise ValueError("val_date must be before expiry.")
        return math.sqrt(self.integrated_covar(val_date, expiry, fwd_contract, fwd_contract) / time_to_expiry)

    def integrated_corr(self,
                        obs_start: utils.TimePeriodSpecType,
                        obs_end: utils.TimePeriodSpecType,
                        fwd_contract_1: utils.ForwardPointType,
                        fwd_contract_2: utils.ForwardPointType) -> float:
        covariance = self.integrated_covar(obs_start, obs_end, fwd_contract_1, fwd_contract_2)
        variance_1 = self.integrated_variance(obs_start, obs_end, fwd_contract_1)
        variance_2 = self.integrated_variance(obs_start, obs_end, fwd_contract_2)
        corr = covariance / math.sqrt(variance_1 * variance_2)
        if 1.0 < corr < (1.0 + self._corr_tolerance):
            return 1.0
        if (-1.0 - self._corr_tolerance) < corr < -1:
            return -1.0
        return corr

    @staticmethod
    def _cont_ext(c1, c2, x) -> float:
        if x == 0.0:
            return c1 - c2
        return (math.exp(-x * c2) - math.exp(-x * c1)) / x

    @staticmethod
    def _get_factor_vol(factor_num, fwd_contract, vol_curve) -> float:
        vol = vol_curve.get(fwd_contract, None)
        if vol is None:
            raise ValueError(
                "No point in vol curve of factor {factor_num} for fwd_contract_1 value of {fwd}.".format(
                    factor_num=factor_num, fwd=fwd_contract))
        return vol

    @staticmethod
    def for_3_factor_seasonal(freq: str,
                              spot_mean_reversion: float,
                              spot_vol: float,
                              long_term_vol: float,
                              seasonal_vol: float,
                              start: utils.ForwardPointType,
                              end: utils.ForwardPointType,
                              time_func: tp.Optional[utils.TimeFunctionType] = None) -> 'MultiFactorModel':
        factors, factor_corrs = _create_3_factor_season_params(freq, spot_mean_reversion, spot_vol, long_term_vol,
                                                              seasonal_vol, start, end)
        return MultiFactorModel(freq, factors, factor_corrs, time_func)


_days_per_year = 365.25
_seconds_per_year = 60 * 60 * 24 * _days_per_year


def _create_3_factor_season_params(
        freq: str,
        spot_mean_reversion: float,
        spot_vol: float,
        long_term_vol: float,
        seasonal_vol: float,
        start: utils.ForwardPointType,
        end: utils.ForwardPointType) -> tp.Tuple[tp.Iterable[tp.Tuple[float, utils.CurveType]], np.ndarray]:
    factor_corrs = np.array([
        [1.0, 0.0, 0.0],
        [0.0, 1.0, 0.0],
        [0.0, 0.0, 1.0]])
    start_period = start if isinstance(start, pd.Period) else pd.Period(start, freq=freq)
    end_period = end if isinstance(end, pd.Period) else pd.Period(end, freq=freq)
    index = pd.period_range(start=start_period, end=end_period, freq=freq)
    long_term_vol_curve = pd.Series(index=index, data=[long_term_vol] * len(index))
    spot_vol_curve = pd.Series(index=index.copy(), data=[spot_vol] * len(index))
    peak_period = pd.Period(year=start_period.year, month=2, day=1, freq=freq)
    phase = np.pi / 2.0
    amplitude = seasonal_vol / 2.0
    seasonal_vol_array = np.empty((len(index)))
    for i, p in enumerate(index):
        t_from_peak = (p.start_time - peak_period.start_time).total_seconds() / _seconds_per_year
        seasonal_vol_array[i] = 2.0 * np.pi * t_from_peak + phase
    seasonal_vol_array = np.sin(seasonal_vol_array) * amplitude
    seasonal_vol_curve = pd.Series(index=index.copy(), data=seasonal_vol_array)
    factors = [
        (spot_mean_reversion, spot_vol_curve),
        (0.0, long_term_vol_curve),
        (0.0, seasonal_vol_curve)
    ]
    return factors, factor_corrs
