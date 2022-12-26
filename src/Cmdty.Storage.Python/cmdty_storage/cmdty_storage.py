# Copyright(c) 2019 Jake Fowler
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
import System.Collections.Generic as dotnet_cols_gen
from pathlib import Path

clr.AddReference(str(Path('cmdty_storage/lib/Cmdty.Storage')))
import Cmdty.Storage as net_cs

from typing import Union, Callable, Iterable, Tuple, NamedTuple, Optional
from datetime import datetime, date
import pandas as pd
from enum import Enum
from cmdty_storage import utils
import logging

logger: logging.Logger = logging.getLogger('cmdty.storage')


class InjectWithdrawRange(NamedTuple):
    min_inject_withdraw_rate: float
    max_inject_withdraw_rate: float


class RatchetInterp(Enum):
    LINEAR = 1
    STEP = 2


RatchetsType = Optional[Union[Iterable[Tuple[str, Iterable[Tuple[float, float, float]]]],
                     Iterable[Tuple[date, Iterable[Tuple[float, float, float]]]],
                     Iterable[Tuple[datetime, Iterable[Tuple[float, float, float]]]],
                     Iterable[Tuple[pd.Period, Iterable[Tuple[float, float, float]]]]]]


class CmdtyStorage:

    def __init__(self,
                 freq: str,
                 storage_start: utils.TimePeriodSpecType,
                 storage_end: utils.TimePeriodSpecType,
                 injection_cost: Union[float, pd.Series],
                 withdrawal_cost: Union[float, pd.Series],
                 ratchets: RatchetsType = None,
                 ratchet_interp: Optional[RatchetInterp] = None,
                 min_inventory: Union[None, float, int, pd.Series] = None,
                 max_inventory: Union[None, float, int, pd.Series] = None,
                 max_injection_rate: Union[None, float, int, pd.Series] = None,
                 max_withdrawal_rate: Union[None, float, int, pd.Series] = None,
                 cmdty_consumed_inject: Union[None, float, int, pd.Series] = None,
                 cmdty_consumed_withdraw: Union[None, float, int, pd.Series] = None,
                 terminal_storage_npv: Union[None, Callable[[float, float], float]] = None,
                 inventory_loss: Union[None, float, int, pd.Series] = None,
                 inventory_cost: Union[None, float, int, pd.Series] = None):

        if freq not in utils.FREQ_TO_PERIOD_TYPE:
            raise ValueError("freq parameter value of '{}' not supported. The allowable values can be found in the keys of the dict curves.FREQ_TO_PERIOD_TYPE.".format(freq))
        time_period_type = utils.FREQ_TO_PERIOD_TYPE[freq]
        start_period = utils.from_datetime_like(storage_start, time_period_type)
        end_period = utils.from_datetime_like(storage_end, time_period_type)
        builder = net_cs.IBuilder[time_period_type](net_cs.CmdtyStorage[time_period_type].Builder)
        builder = builder.WithActiveTimePeriod(start_period, end_period)
        net_constraints = dotnet_cols_gen.List[net_cs.InjectWithdrawRangeByInventoryAndPeriod[time_period_type]]()

        if ratchets is not None:
            utils.raise_if_not_none(min_inventory, "min_inventory parameter should not be provided if ratchets parameter is provided.")
            utils.raise_if_not_none(max_inventory, "max_inventory parameter should not be provided if ratchets parameter is provided.")
            utils.raise_if_not_none(max_injection_rate, "max_injection_rate parameter should not be provided if ratchets parameter is provided.")
            utils.raise_if_not_none(max_withdrawal_rate, "max_withdrawal_rate parameter should not be provided if ratchets parameter is provided.")
            utils.raise_if_none(ratchet_interp, "ratchet_interp parameter should be provided if ratchets parameter is provided.")
            for period, rates_by_inventory in ratchets:
                net_period = utils.from_datetime_like(period, time_period_type)
                net_rates_by_inventory = dotnet_cols_gen.List[net_cs.InjectWithdrawRangeByInventory]()
                for inventory, min_rate, max_rate in rates_by_inventory:
                    net_rates_by_inventory.Add(net_cs.InjectWithdrawRangeByInventory(inventory, net_cs.InjectWithdrawRange(min_rate, max_rate)))
                net_constraints.Add(net_cs.InjectWithdrawRangeByInventoryAndPeriod[time_period_type](net_period, net_rates_by_inventory))
            builder = net_cs.IAddInjectWithdrawConstraints[time_period_type](builder)
            if ratchet_interp == RatchetInterp.LINEAR:
                net_cs.CmdtyStorageBuilderExtensions.WithTimeAndInventoryVaryingInjectWithdrawRatesPiecewiseLinear[time_period_type](builder, net_constraints)
            elif ratchet_interp == RatchetInterp.STEP:
                if terminal_storage_npv is None:
                    raise ValueError('When ratchet_interp is RatchetInterp.STEP terminal_storage_npv should be '
                                     'specified')
                net_cs.CmdtyStorageBuilderExtensions.WithStepRatchets[time_period_type](builder, net_constraints)
        else:
            utils.raise_if_not_none(ratchet_interp, "ratchet_interp should not be provided if ratchets parameter is not provided.")
            utils.raise_if_none(min_inventory, "min_inventory parameter should be provided if ratchets parameter is not provided.")
            utils.raise_if_none(max_inventory, "max_inventory parameter should be provided if ratchets parameter is not provided.")
            utils.raise_if_none(max_injection_rate, "max_injection_rate parameter should be provided if ratchets parameter is not provided.")
            utils.raise_if_none(max_withdrawal_rate, "max_withdrawal_rate parameter should be provided if ratchets parameter is not provided.")
            builder = net_cs.IAddInjectWithdrawConstraints[time_period_type](builder)
            max_injection_rate_is_scalar = utils.is_scalar(max_injection_rate)
            max_withdrawal_rate_is_scalar = utils.is_scalar(max_withdrawal_rate)
            if max_injection_rate_is_scalar and max_withdrawal_rate_is_scalar:
                net_cs.CmdtyStorageBuilderExtensions.WithConstantInjectWithdrawRange[time_period_type](builder, -max_withdrawal_rate, max_injection_rate)
            else:
                if max_injection_rate_is_scalar:
                    max_injection_rate = pd.Series(data=[max_injection_rate] * len(max_withdrawal_rate), index=max_withdrawal_rate.index)
                elif max_withdrawal_rate_is_scalar:
                    max_withdrawal_rate = pd.Series(data=[max_withdrawal_rate] * len(max_injection_rate), index=max_injection_rate.index)

                inject_withdraw_series = max_injection_rate.combine(max_withdrawal_rate, lambda inj_rate, with_rate: (-with_rate, inj_rate)).dropna()
                net_inj_with_series = utils.series_to_time_series(inject_withdraw_series, time_period_type, net_cs.InjectWithdrawRange, lambda tup: net_cs.InjectWithdrawRange(tup[0], tup[1]))
                builder.WithInjectWithdrawRangeSeries(net_inj_with_series)
            builder = net_cs.IAddMinInventory[time_period_type](builder)
            if isinstance(min_inventory, pd.Series):
                net_series_min_inventory = utils.series_to_double_time_series(min_inventory, time_period_type)
                builder.WithMinInventoryTimeSeries(net_series_min_inventory)
            else: # Assume min_inventory is a constaint number
                builder.WithConstantMinInventory(min_inventory)

            builder = net_cs.IAddMaxInventory[time_period_type](builder)
            if isinstance(max_inventory, pd.Series):
                net_series_max_inventory = utils.series_to_double_time_series(max_inventory, time_period_type)
                builder.WithMaxInventoryTimeSeries(net_series_max_inventory)
            else: # Assume max_inventory is a constaint number
                builder.WithConstantMaxInventory(max_inventory)

        builder = net_cs.IAddInjectionCost[time_period_type](builder)
        if utils.is_scalar(injection_cost):
            builder.WithPerUnitInjectionCost(injection_cost)
        else:
            net_series_injection_cost = utils.series_to_double_time_series(injection_cost, time_period_type)
            builder.WithPerUnitInjectionCostTimeSeries(net_series_injection_cost)

        builder = net_cs.IAddCmdtyConsumedOnInject[time_period_type](builder)
        if cmdty_consumed_inject is not None:
            if utils.is_scalar(cmdty_consumed_inject):
                builder.WithFixedPercentCmdtyConsumedOnInject(cmdty_consumed_inject)
            else:
                net_series_cmdty_consumed_inject = utils.series_to_double_time_series(cmdty_consumed_inject, time_period_type)
                builder.WithPercentCmdtyConsumedOnInjectTimeSeries(net_series_cmdty_consumed_inject)
        else:
            builder.WithNoCmdtyConsumedOnInject()

        builder = net_cs.IAddWithdrawalCost[time_period_type](builder)
        if utils.is_scalar(withdrawal_cost):
            builder.WithPerUnitWithdrawalCost(withdrawal_cost)
        else:
            net_series_withdrawal_cost = utils.series_to_double_time_series(withdrawal_cost, time_period_type)
            builder.WithPerUnitWithdrawalCostTimeSeries(net_series_withdrawal_cost)

        builder = net_cs.IAddCmdtyConsumedOnWithdraw[time_period_type](builder)
        if cmdty_consumed_withdraw is not None:
            if utils.is_scalar(cmdty_consumed_withdraw):
                builder.WithFixedPercentCmdtyConsumedOnWithdraw(cmdty_consumed_withdraw)
            else:
                net_series_cmdty_consumed_withdraw = utils.series_to_double_time_series(cmdty_consumed_withdraw, time_period_type)
                builder.WithPercentCmdtyConsumedOnWithdrawTimeSeries(net_series_cmdty_consumed_withdraw)
        else:
            builder.WithNoCmdtyConsumedOnWithdraw()

        builder = net_cs.IAddCmdtyInventoryLoss[time_period_type](builder)
        if inventory_loss is not None:
            if utils.is_scalar(inventory_loss):
                builder.WithFixedPercentCmdtyInventoryLoss(inventory_loss)
            else:
                net_series_inventory_loss = utils.series_to_double_time_series(inventory_loss, time_period_type)
                builder.WithCmdtyInventoryLossTimeSeries(net_series_inventory_loss)
        else:
            builder.WithNoCmdtyInventoryLoss()

        builder = net_cs.IAddCmdtyInventoryCost[time_period_type](builder)
        if inventory_cost is not None:
            if utils.is_scalar(inventory_cost):
                builder.WithFixedPerUnitInventoryCost(inventory_cost)
            else:
                net_series_inventory_cost = utils.series_to_double_time_series(inventory_cost, time_period_type)
                builder.WithPerUnitInventoryCostTimeSeries(net_series_inventory_cost)
        else:
            builder.WithNoInventoryCost()

        builder = net_cs.IAddTerminalStorageState[time_period_type](builder)
        if terminal_storage_npv is None:
            builder.MustBeEmptyAtEnd()
        else:
            builder.WithTerminalInventoryNpv(dotnet.Func[dotnet.Double, dotnet.Double, dotnet.Double](terminal_storage_npv))

        self._net_storage = net_cs.IBuildCmdtyStorage[time_period_type](builder).Build()
        self._freq = freq

    def _net_time_period(self, period):
        time_period_type = utils.FREQ_TO_PERIOD_TYPE[self._freq]
        return utils.from_datetime_like(period, time_period_type)

    @property
    def net_storage(self) -> net_cs.CmdtyStorage:
        return self._net_storage

    @property
    def freq(self) -> str:
        return self._freq

    @property
    def empty_at_end(self) -> bool:
        return self._net_storage.MustBeEmptyAtEnd

    @property
    def start(self) -> pd.Period:
        return utils.net_time_period_to_pandas_period(self._net_storage.StartPeriod, self._freq)

    @property
    def end(self) -> pd.Period:
        return utils.net_time_period_to_pandas_period(self._net_storage.EndPeriod, self._freq)

    def inject_withdraw_range(self, period, inventory) -> InjectWithdrawRange:

        net_time_period = self._net_time_period(period)
        net_inject_withdraw = self._net_storage.GetInjectWithdrawRange(net_time_period, inventory)

        return InjectWithdrawRange(net_inject_withdraw.MinInjectWithdrawRate, net_inject_withdraw.MaxInjectWithdrawRate)

    def min_inventory(self, period) -> float:
        net_time_period = self._net_time_period(period)
        return self._net_storage.MinInventory(net_time_period)

    def max_inventory(self, period) -> float:
        net_time_period = self._net_time_period(period)
        return self._net_storage.MaxInventory(net_time_period)

    def injection_cost(self, period, inventory, injected_volume) -> float:
        net_time_period = self._net_time_period(period)
        net_inject_costs = self._net_storage.InjectionCost(net_time_period, inventory, injected_volume)
        if net_inject_costs.Count > 0:
            return net_inject_costs[0].Amount
        return 0.0

    def cmdty_consumed_inject(self, period, inventory, injected_volume) -> float:
        net_time_period = self._net_time_period(period)
        return self._net_storage.CmdtyVolumeConsumedOnInject(net_time_period, inventory, injected_volume)

    def withdrawal_cost(self, period, inventory, withdrawn_volume) -> float:
        net_time_period = self._net_time_period(period)
        net_withdrawal_costs = self._net_storage.WithdrawalCost(net_time_period, inventory, withdrawn_volume)
        if net_withdrawal_costs.Count > 0:
            return net_withdrawal_costs[0].Amount
        return 0.0

    def cmdty_consumed_withdraw(self, period, inventory, withdrawn_volume) -> float:
        net_time_period = self._net_time_period(period)
        return self._net_storage.CmdtyVolumeConsumedOnWithdraw(net_time_period, inventory, withdrawn_volume)

    def terminal_storage_npv(self, cmdty_price, terminal_inventory) -> float:
        return self._net_storage.TerminalStorageNpv(cmdty_price, terminal_inventory)

    def inventory_pcnt_loss(self, period) -> float:
        net_time_period = self._net_time_period(period)
        return self._net_storage.CmdtyInventoryPercentLoss(net_time_period)

    def inventory_cost(self, period, inventory) -> float:
        net_time_period = self._net_time_period(period)
        net_inventory_cost = self._net_storage.CmdtyInventoryCost(net_time_period, inventory)
        if net_inventory_cost.Count > 0:
            return net_inventory_cost[0].Amount
        return 0.0

