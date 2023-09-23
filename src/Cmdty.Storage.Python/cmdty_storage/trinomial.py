# Copyright(c) 2020 Jake Fowler
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
from cmdty_storage import utils, CmdtyStorage
from pathlib import Path
import typing as tp
from datetime import date
import pandas as pd

clr.AddReference(str(Path('cmdty_storage/lib/Cmdty.Storage')))
import Cmdty.Storage as net_cs


def trinomial_value(cmdty_storage: CmdtyStorage,
                    val_date: utils.TimePeriodSpecType,
                    inventory: float,
                    forward_curve: pd.Series,
                    spot_volatility: pd.Series,
                    mean_reversion: float,
                    time_step: float,
                    interest_rates: pd.Series,  # TODO change this to function which returns discount factor, i.e. delegate DF calc to caller.
                    settlement_rule: tp.Callable[[pd.Period], date],
                    num_inventory_grid_points: int = 100,
                    numerical_tolerance: float = 1E-12) -> float:
    """
    Calculates the value of commodity storage using a one-factor trinomial tree.

    Args:
        settlement_rule (callable): Mapping function from pandas.Period type to the date on which the cmdty delivered in
            this period is settled. The pandas.Period parameter will have freq equal to the cmdty_storage parameter's freq property.
    """
    if cmdty_storage.freq != forward_curve.index.freqstr:
        raise ValueError("cmdty_storage and forward_curve have different frequencies.")
    if cmdty_storage.freq != spot_volatility.index.freqstr:
        raise ValueError("cmdty_storage and spot_volatility have different frequencies.")
    time_period_type = utils.FREQ_TO_PERIOD_TYPE[cmdty_storage.freq]

    trinomial_calc = net_cs.TreeStorageValuation[time_period_type].ForStorage(cmdty_storage.net_storage)
    net_cs.ITreeAddStartingInventory[time_period_type](trinomial_calc).WithStartingInventory(inventory)

    current_period = utils.from_datetime_like(val_date, time_period_type)
    net_cs.ITreeAddCurrentPeriod[time_period_type](trinomial_calc).ForCurrentPeriod(current_period)

    net_forward_curve = utils.series_to_double_time_series(forward_curve, time_period_type)
    net_cs.ITreeAddForwardCurve[time_period_type](trinomial_calc).WithForwardCurve(net_forward_curve)

    net_spot_volatility = utils.series_to_double_time_series(spot_volatility, time_period_type)
    net_cs.TreeStorageValuationExtensions.WithOneFactorTrinomialTree[time_period_type](
        trinomial_calc, net_spot_volatility, mean_reversion, time_step)

    net_settlement_rule = utils.wrap_settle_for_dotnet(settlement_rule, cmdty_storage.freq)
    net_cs.ITreeAddCmdtySettlementRule[time_period_type](trinomial_calc).WithCmdtySettlementRule(net_settlement_rule)

    interest_rate_time_series = utils.series_to_double_time_series(interest_rates, utils.FREQ_TO_PERIOD_TYPE['D'])
    net_cs.TreeStorageValuationExtensions.WithAct365ContinuouslyCompoundedInterestRateCurve[time_period_type](
        trinomial_calc, interest_rate_time_series)

    net_cs.TreeStorageValuationExtensions.WithFixedNumberOfPointsOnGlobalInventoryRange[time_period_type](
        trinomial_calc, num_inventory_grid_points)
    net_cs.TreeStorageValuationExtensions.WithLinearInventorySpaceInterpolation[time_period_type](trinomial_calc)
    net_cs.ITreeAddNumericalTolerance[time_period_type](trinomial_calc).WithNumericalTolerance(numerical_tolerance)
    npv = net_cs.ITreeCalculate[time_period_type](trinomial_calc).Calculate()
    return npv.NetPresentValue


def trinomial_deltas(cmdty_storage: CmdtyStorage,
                     val_date: utils.TimePeriodSpecType,
                     inventory: float,
                     forward_curve: pd.Series,
                     spot_volatility: pd.Series,
                     mean_reversion: float,
                     time_step: float,
                     interest_rates: pd.Series,
                     settlement_rule: tp.Callable[[pd.Period], date],
                     fwd_contracts: utils.FwdContractsType,
                     num_inventory_grid_points: int = 100,
                     numerical_tolerance: float = 1E-12,
                     delta_shift=0.00001  # TODO Improve this!
                     ) -> tp.List[float]:
    fwd_curve_copy = forward_curve.copy()
    deltas = []
    for fwd_contract in fwd_contracts:
        start, end = utils.to_period_range(cmdty_storage.freq, fwd_contract)
        fwd_curve_copy[start:end] = fwd_curve_copy[start:end] + delta_shift # TODO JF improve this!
        value_up_shift = trinomial_value(cmdty_storage, val_date, inventory, fwd_curve_copy,
                                         spot_volatility, mean_reversion, time_step, interest_rates, settlement_rule,
                                         num_inventory_grid_points, numerical_tolerance)
        fwd_curve_copy[start:end] = forward_curve[start:end] - delta_shift  # TODO JF improve this!
        value_down_shift = trinomial_value(cmdty_storage, val_date, inventory, fwd_curve_copy,
                                           spot_volatility, mean_reversion, time_step, interest_rates, settlement_rule,
                                           num_inventory_grid_points, numerical_tolerance)
        delta = (value_up_shift - value_down_shift) / (2.0 * delta_shift)
        fwd_curve_copy[start:end] = forward_curve[start:end]
        deltas.append(delta)
    # TODO undiscount deltas
    return deltas
