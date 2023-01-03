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

import pandas as pd
import clr
import System as dotnet
from cmdty_storage import utils, CmdtyStorage
from typing import NamedTuple, Union, Callable
from datetime import date
from pathlib import Path
clr.AddReference(str(Path('cmdty_storage/lib/Cmdty.Storage')))
import Cmdty.Storage as net_cs
clr.AddReference(str(Path("cmdty_storage/lib/Cmdty.TimePeriodValueTypes")))
import Cmdty.TimePeriodValueTypes as tp


class IntrinsicValuationResults(NamedTuple):
    npv: float
    profile: pd.DataFrame


def intrinsic_value(cmdty_storage: CmdtyStorage,
                    val_date: utils.TimePeriodSpecType,
                    inventory: Union[float, int],
                    forward_curve: pd.Series,
                    interest_rates: pd.Series,
                    settlement_rule: Callable[[pd.Period], date],
                    num_inventory_grid_points: int = 100,
                    numerical_tolerance: float = 1E-12) -> IntrinsicValuationResults:
    """
    Calculates the intrinsic value of commodity storage.

    Args:
        settlement_rule (callable): Mapping function from pandas.Period type to the date on which the cmdty delivered in
            this period is settled. The pandas.Period parameter will have freq equal to the cmdty_storage parameter's freq property.
    """
    if cmdty_storage.freq != forward_curve.index.freqstr:
        raise ValueError("cmdty_storage and forward_curve have different frequencies.")
    time_period_type = utils.FREQ_TO_PERIOD_TYPE[cmdty_storage.freq]
    current_period = utils.from_datetime_like(val_date, time_period_type)
    net_forward_curve = utils.series_to_double_time_series(forward_curve, time_period_type)
    net_settlement_rule = utils.wrap_settle_for_dotnet(settlement_rule, cmdty_storage.freq)
    interest_rate_time_series = utils.series_to_double_time_series(interest_rates, utils.FREQ_TO_PERIOD_TYPE['D'])
    return net_intrinsic_calc(cmdty_storage, current_period, interest_rate_time_series, inventory, net_forward_curve,
                                 net_settlement_rule, num_inventory_grid_points, numerical_tolerance, time_period_type)


def net_intrinsic_calc(cmdty_storage, current_period, interest_rate_time_series, inventory, net_forward_curve,
                       net_settlement_rule, num_inventory_grid_points, numerical_tolerance, time_period_type):
    intrinsic_calc = net_cs.IntrinsicStorageValuation[time_period_type].ForStorage(cmdty_storage.net_storage)
    net_cs.IIntrinsicAddStartingInventory[time_period_type](intrinsic_calc).WithStartingInventory(inventory)
    net_cs.IIntrinsicAddCurrentPeriod[time_period_type](intrinsic_calc).ForCurrentPeriod(current_period)
    net_cs.IIntrinsicAddForwardCurve[time_period_type](intrinsic_calc).WithForwardCurve(net_forward_curve)
    net_cs.IIntrinsicAddCmdtySettlementRule[time_period_type](intrinsic_calc).WithCmdtySettlementRule(
        net_settlement_rule)
    net_cs.IntrinsicStorageValuationExtensions.WithAct365ContinuouslyCompoundedInterestRateCurve[time_period_type](
        intrinsic_calc, interest_rate_time_series)
    net_cs.IntrinsicStorageValuationExtensions.WithFixedNumberOfPointsOnGlobalInventoryRange[time_period_type](
        intrinsic_calc, num_inventory_grid_points)
    net_cs.IntrinsicStorageValuationExtensions.WithLinearInventorySpaceInterpolation[time_period_type](intrinsic_calc)
    net_cs.IIntrinsicAddNumericalTolerance[time_period_type](intrinsic_calc).WithNumericalTolerance(numerical_tolerance)
    net_val_results = net_cs.IIntrinsicCalculate[time_period_type](intrinsic_calc).Calculate()
    data_frame = profile_to_data_frame(cmdty_storage.freq, net_val_results.StorageProfile)
    results = IntrinsicValuationResults(net_val_results.Npv, data_frame)
    return results


def profile_to_data_frame(freq, net_profile):
    if net_profile.Count == 0:
        index = pd.PeriodIndex(data=[], freq=freq)
    else:
        profile_start = utils.net_datetime_to_py_datetime(net_profile.Indices[0].Start)
        index = pd.period_range(start=profile_start, freq=freq, periods=net_profile.Count)
    inventories = [None] * net_profile.Count
    inject_withdraw_volumes = [None] * net_profile.Count
    cmdty_consumed = [None] * net_profile.Count
    inventory_loss = [None] * net_profile.Count
    net_volume = [None] * net_profile.Count
    period_pv = [None] * net_profile.Count
    for i, profile_data in enumerate(net_profile.Data):
        inventories[i] = profile_data.Inventory
        inject_withdraw_volumes[i] = profile_data.InjectWithdrawVolume
        cmdty_consumed[i] = profile_data.CmdtyConsumed
        inventory_loss[i] = profile_data.InventoryLoss
        net_volume[i] = profile_data.NetVolume
        period_pv[i] = profile_data.PeriodPv
    data_frame_data = {'inventory': inventories, 'inject_withdraw_volume': inject_withdraw_volumes,
                       'cmdty_consumed': cmdty_consumed, 'inventory_loss': inventory_loss, 'net_volume': net_volume,
                       'period_pv': period_pv}
    data_frame = pd.DataFrame(data=data_frame_data, index=index)
    return data_frame
