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

import clr
import System as dotnet

import typing as tp
import pandas as pd
from datetime import datetime, date
from cmdty_storage import utils
import System.Collections.Generic as dotnet_cols_gen
import pathlib as pl
from cmdty_storage import _multi_factor_common as mfc

clr.AddReference(str(pl.Path('cmdty_storage/lib/Cmdty.Core.Simulation')))
import Cmdty.Core.Simulation as net_sim


class MultiFactorSpotSim:

    def __init__(self,
                 freq: str,
                 factors: tp.Collection[tp.Tuple[float, utils.CurveType]],
                 factor_corrs: mfc.FactorCorrsType,
                 current_date: tp.Union[datetime, date, str, pd.Period],
                 fwd_curve: utils.CurveType,
                 sim_periods: tp.Iterable[tp.Union[pd.Period, datetime, date, str]],
                 seed: tp.Optional[int] = None,
                 antithetic: bool = False,
                 # time_func: Callable[[Union[datetime, date], Union[datetime, date]], float] TODO add this back in
                 ):
        factor_corrs = mfc.validate_multi_factor_params(factors, factor_corrs)
        if freq not in utils.FREQ_TO_PERIOD_TYPE:
            raise ValueError("freq parameter value of '{}' not supported. The allowable values can be found in the "
                             "keys of the dict curves.FREQ_TO_PERIOD_TYPE.".format(freq))

        time_period_type = utils.FREQ_TO_PERIOD_TYPE[freq]

        net_multi_factor_params = mfc.create_net_multi_factor_params(factor_corrs, factors, time_period_type)
        net_forward_curve = utils.curve_to_net_dict(fwd_curve, time_period_type)
        net_current_date = utils.py_date_like_to_net_datetime(current_date)
        net_time_func = dotnet.Func[dotnet.DateTime, dotnet.DateTime, dotnet.Double](net_sim.TimeFunctions.Act365)
        net_sim_periods = dotnet_cols_gen.List[time_period_type]()
        [net_sim_periods.Add(utils.from_datetime_like(p, time_period_type)) for p in sim_periods]

        if seed is None:
            mt_rand = net_sim.MersenneTwisterGenerator(antithetic)
        else:
            mt_rand = net_sim.MersenneTwisterGenerator(seed, antithetic)
        mt_rand = net_sim.IStandardNormalGeneratorWithSeed(mt_rand)

        self._net_simulator = net_sim.MultiFactor.MultiFactorSpotPriceSimulator[time_period_type](
            net_multi_factor_params, net_current_date, net_forward_curve, net_sim_periods, net_time_func, mt_rand)
        self._sim_periods = [_to_pd_period(freq, p) for p in sim_periods]
        self._freq = freq

    def simulate(self, num_sims: int) -> pd.DataFrame:
        net_sim_results = self._net_simulator.Simulate(num_sims)
        spot_sim_array = utils.as_numpy_array(net_sim_results.SpotPrices)
        spot_sim_array.resize((net_sim_results.NumSteps, net_sim_results.NumSims))
        period_index = pd.PeriodIndex(data=self._sim_periods, freq=self._freq)
        return pd.DataFrame(data=spot_sim_array, index=period_index)


def _to_pd_period(freq: str, date_like: tp.Union[pd.Period, datetime, date, str]) -> pd.Period:
    if isinstance(date_like, pd.Period):
        return date_like
    return pd.Period(date_like, freq=freq)
