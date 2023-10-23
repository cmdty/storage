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

import logging
import typing as tp
from scipy import optimize
from cmdty_storage import utils, CmdtyStorage
import pandas as pd
from datetime import date

logger: logging.Logger = logging.getLogger('cmdty.storage.calibration.seasonal-three-factor')


class StorageCalibrationTarget(tp.NamedTuple):
    storage: CmdtyStorage
    target_pv: float
    notional_volume: float
    val_date: utils.TimePeriodSpecType
    inventory: float
    fwd_curve: pd.Series
    interest_rates: pd.Series  # TODO change this to function which returns discount factor, i.e. delegate DF calc to caller.
    long_term_vol: float
    seasonal_vol: float
    penalty_weighting: tp.Optional[float]


def calibrate_seasonal_three_factor(
    target_storage: tp.Iterable[StorageCalibrationTarget],
    bounds: tp.Union[optimize.Bounds, tp.Tuple[tp.Tuple[float, float], tp.Tuple[float, float]]],
    settlement_rule: tp.Callable[[pd.Period], date],
    num_sims: int,
    basis_funcs: str,
    seed: tp.Optional[int] = None,
    fwd_sim_seed: tp.Optional[int] = None,
    num_inventory_grid_points: int = 100,
    numerical_tolerance: float = 1E-12,
    optimize_method: tp.Optional[str] = None,
    optimize_tol: tp.Optional[float] = None,
    optimize_options: tp.Optional[dict] = None,
    logger_override: tp.Optional[logging.Logger] = None
) -> optimize.OptimizeResult:
    this_logger = logger if logger_override is None else logger_override

    def storage_val_sum_squared_error(spot_factor_params):
        spot_factor_vol, spot_factor_mr_rate = spot_factor_params


        pass

    optimize_result = optimize.minimize(storage_val_sum_squared_error, bounds=bounds, tol=optimize_tol,
                                         method=optimize_method, options=optimize_options)

    return optimize_result
