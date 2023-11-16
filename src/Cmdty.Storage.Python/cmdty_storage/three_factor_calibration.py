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
import numpy as np
from scipy import optimize
import utils
from cmdty_storage import CmdtyStorage
from multi_factor import three_factor_seasonal_value, SimulationDataReturned
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


def calibrate_seasonal_three_factor(
    storage_targets: tp.Collection[StorageCalibrationTarget],
    bounds: tp.Union[optimize.Bounds, tp.Tuple[tp.Tuple[float, float], tp.Tuple[float, float]]],
    settlement_rule: tp.Callable[[pd.Period], date],
    num_sims: int,
    basis_funcs: str,
    seed: int,
    fwd_sim_seed: tp.Optional[int] = None,
    num_inventory_grid_points: tp.Optional[int] = 100,
    numerical_tolerance: tp.Optional[float] = 1E-12,
    penalty_weights: tp.Optional[tp.Collection[float]] = None,
    optimize_method: tp.Optional[str] = None,
    optimize_tol: tp.Optional[float] = None,
    optimize_options: tp.Optional[dict] = None,
    logger_override: tp.Optional[logging.Logger] = None) -> optimize.OptimizeResult:

    this_logger = logger if logger_override is None else logger_override
    num_storage_targets = len(storage_targets)
    if penalty_weights is not None:
        if len(penalty_weights) != num_storage_targets:
            raise ValueError(f'penalty_weights and storage_targets argument should have same length. However, penalty_weights '
                             f'has length {len(penalty_weights)}, whereas storage_targets as length {num_storage_targets}.')
        penalty_weights_vector = np.fromiter(penalty_weights, dtype=np.float64, count=len(penalty_weights))
        penalty_weights_vector = penalty_weights_vector/np.sum(penalty_weights_vector)
        this_logger.info(f'penalty_weights vector normalised to {np.array_str(penalty_weights_vector)}.')
    else:  # Default to equal weights
        penalty_weights_vector = np.repeat(1.0/num_storage_targets, num_storage_targets)

    def storage_val_sum_squared_error(spot_factor_params):
        spot_factor_vol, spot_factor_mr_rate = spot_factor_params
        this_logger.debug(f'Calling objective function with spot_factor_vol {spot_factor_vol} and spot_factor_mr_rate {spot_factor_mr_rate}.')
        penalty = 0.0
        for i, storage_target in enumerate(storage_targets):
            logger.debug(f'Performing valuation of storage {i} of {num_storage_targets} targets.')
            storage_pv = three_factor_seasonal_value(cmdty_storage=storage_target.storage, val_date=storage_target.val_date, inventory=storage_target.inventory,
                                                 fwd_curve=storage_target.fwd_curve, interest_rates=storage_target.interest_rates, settlement_rule=settlement_rule,
                                                 long_term_vol=storage_target.long_term_vol, seasonal_vol=storage_target.seasonal_vol, num_sims=num_sims, basis_funcs=basis_funcs,
                                                 discount_deltas=False, seed=seed, fwd_sim_seed=fwd_sim_seed, num_inventory_grid_points=num_inventory_grid_points,
                                                 numerical_tolerance=numerical_tolerance, sim_data_returned=SimulationDataReturned.NONE).npv
            pv_diff_to_target = storage_pv - storage_target.target_pv
            normalised_pv_diff_to_target = pv_diff_to_target/storage_target.notional_volume
            normalised_pv_diff_to_target_squared = normalised_pv_diff_to_target**2
            logger.debug(f'PV of storage {i} calculated as {storage_pv} which has diff to target of {pv_diff_to_target}, per unit of '
                 f'notional diff to target {normalised_pv_diff_to_target}, the square of which is {normalised_pv_diff_to_target_squared}.')
            penalty += normalised_pv_diff_to_target_squared * penalty_weights_vector[i]
        this_logger.debug(f'Objective function value of {penalty} returned for spot_factor_vol {spot_factor_vol} and '
                          f'spot_factor_mr_rate {spot_factor_mr_rate}.')
        return penalty

    optimize_result = optimize.minimize(storage_val_sum_squared_error, bounds=bounds, tol=optimize_tol,
                                         method=optimize_method, options=optimize_options)
    # TODO warning on non-success
    return optimize_result
