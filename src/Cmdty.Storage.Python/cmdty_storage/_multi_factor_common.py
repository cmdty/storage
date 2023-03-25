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
import System.Collections.Generic as dotnet_cols_gen
import pathlib as pl
clr.AddReference(str(pl.Path('cmdty_storage/lib/Cmdty.Core.Simulation')))
import Cmdty.Core.Simulation as net_sim
import typing as tp
import numpy as np
from cmdty_storage import utils


FactorCorrsType = tp.Optional[tp.Union[float, np.ndarray]]


def create_net_multi_factor_params(factor_corrs, factors, time_period_type):
    net_factors = dotnet_cols_gen.List[net_sim.MultiFactor.Factor[time_period_type]]()
    for mean_reversion, vol_curve in factors:
        net_vol_curve = utils.curve_to_net_dict(vol_curve, time_period_type)
        net_factors.Add(net_sim.MultiFactor.Factor[time_period_type](mean_reversion, net_vol_curve))
    net_factor_corrs = utils.as_net_array(factor_corrs)
    net_multi_factor_params = net_sim.MultiFactor.MultiFactorParameters[time_period_type](net_factor_corrs,
                                                                                          *net_factors)
    return net_multi_factor_params


def validate_multi_factor_params(  # TODO unit test validation fails
        factors: tp.Collection[tp.Tuple[float, utils.CurveType]],
        factor_corrs: FactorCorrsType) -> np.ndarray:
    factors_len = len(factors)
    if factors_len == 0:
        raise ValueError("factors cannot be empty.")
    if factors_len == 1 and factor_corrs is None:
        factor_corrs = np.array([[1.0]])
    if factors_len == 2 and (isinstance(factor_corrs, float) or isinstance(factor_corrs, int)):
        factor_corrs = np.array([[1.0, float(factor_corrs)],
                                 [float(factor_corrs), 1.0]])
    if factor_corrs.ndim != 2:
        raise ValueError("Factor correlation matrix is not 2-dimensional.")
    corr_shape = factor_corrs.shape
    if corr_shape[0] != corr_shape[1]:
        raise ValueError("Factor correlation matrix is not square.")
    if factor_corrs.dtype != np.float64:
        factor_corrs = factor_corrs.astype(np.float64)
    for (i, j), corr in np.ndenumerate(factor_corrs):
        if i == j:
            if not np.isclose([corr], [1.0]):
                raise ValueError("Factor correlation on diagonal position ({i}, {j}) value of {corr} not valid as not "
                                 "equal to 1.".format(i=i, j=j, corr=corr))
        else:
            if not -1 <= corr <= 1:
                raise ValueError("Factor correlation in position ({i}, {j}) value of {corr} not valid as not in the "
                                 "interval [-1, 1]".format(i=i, j=j, corr=corr))
    num_factors = corr_shape[0]
    if factors_len != num_factors:
        raise ValueError("factors and factor_corrs are of inconsistent sizes.")
    for idx, (mr, vol) in enumerate(factors):
        if mr < 0.0:
            raise ValueError("Mean reversion value of {mr} for factor at index {idx} not valid as is negative.".format(
                mr=mr, idx=idx))
    return factor_corrs
