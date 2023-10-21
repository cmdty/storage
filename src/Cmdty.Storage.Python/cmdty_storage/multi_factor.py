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
import System.Collections.Generic as dotnet_cols_gen
import pathlib as pl
clr.AddReference(str(pl.Path('cmdty_storage/lib/Cmdty.Storage')))
import Cmdty.Storage as net_cs
clr.AddReference(str(pl.Path('cmdty_storage/lib/Cmdty.Core.Simulation')))
import Cmdty.Core.Simulation.MultiFactor as net_mf
clr.AddReference(str(pl.Path('cmdty_storage/lib/Cmdty.Core.Common')))
import Cmdty.Core.Common as net_cc

import pandas as pd
from datetime import date
import typing as tp
from cmdty_storage import utils, CmdtyStorage
import cmdty_storage.intrinsic as cs_intrinsic
from cmdty_storage import _multi_factor_common as mfc
import logging
from enum import Flag

logger: logging.Logger = logging.getLogger('cmdty.storage.multi-factor')


class SimulationDataReturned(Flag):
    NONE = 0
    SPOT_REGRESS = 1
    SPOT_VALUATION = 1 << 2
    SPOT_ALL = SPOT_REGRESS | SPOT_VALUATION
    FACTORS_REGRESS = 1 << 3
    FACTORS_VALUATION = 1 << 4
    FACTORS_ALL = FACTORS_REGRESS | FACTORS_VALUATION
    INVENTORY = 1 << 5
    INJECT_WITHDRAW_VOLUME = 1 << 6
    CMDTY_CONSUMED = 1 << 7
    INVENTORY_LOSS = 1 << 8
    NET_VOLUME = 1 << 9
    PV = 1 << 10
    ALL = SPOT_ALL | FACTORS_ALL | INVENTORY | INJECT_WITHDRAW_VOLUME | CMDTY_CONSUMED | INVENTORY_LOSS | NET_VOLUME | PV


class TriggerPricePoint(tp.NamedTuple):
    volume: float
    price: float


class TriggerPriceProfile(tp.NamedTuple):
    inject_triggers: tp.List[TriggerPricePoint]
    withdraw_triggers: tp.List[TriggerPricePoint]


class MultiFactorValuationResults(tp.NamedTuple):
    npv: float
    deltas: pd.Series
    expected_profile: pd.DataFrame
    intrinsic_npv: float
    intrinsic_profile: pd.DataFrame
    sim_spot_regress: pd.DataFrame
    sim_spot_valuation: pd.DataFrame
    sim_factors_regress: tp.Tuple[pd.DataFrame, ...]
    sim_factors_valuation: tp.Tuple[pd.DataFrame, ...]
    sim_inventory: pd.DataFrame
    sim_inject_withdraw: pd.DataFrame
    sim_cmdty_consumed: pd.DataFrame
    sim_inventory_loss: pd.DataFrame
    sim_net_volume: pd.DataFrame
    sim_pv: pd.DataFrame
    trigger_prices: pd.DataFrame
    trigger_profiles: pd.Series

    @property
    def extrinsic_npv(self):
        return self.npv - self.intrinsic_npv


def three_factor_seasonal_value(cmdty_storage: CmdtyStorage,
                                val_date: utils.TimePeriodSpecType,
                                inventory: float,
                                fwd_curve: pd.Series,
                                interest_rates: pd.Series, # TODO change this to function which returns discount factor, i.e. delegate DF calc to caller.
                                settlement_rule: tp.Callable[[pd.Period], date],
                                spot_mean_reversion: float,
                                spot_vol: float,
                                long_term_vol: float,
                                seasonal_vol: float,
                                num_sims: int,
                                basis_funcs: str,
                                discount_deltas: bool,
                                seed: tp.Optional[int] = None,
                                fwd_sim_seed: tp.Optional[int] = None,
                                extra_decisions: tp.Optional[int] = None,
                                num_inventory_grid_points: int = 100,
                                numerical_tolerance: float = 1E-12,
                                on_progress_update: tp.Optional[tp.Callable[[float], None]] = None,
                                sim_data_returned: tp.Optional[SimulationDataReturned] = SimulationDataReturned.ALL # TODO on next major version increment change this to default to NONE
                                ) -> MultiFactorValuationResults:
    time_period_type = utils.FREQ_TO_PERIOD_TYPE[cmdty_storage.freq]
    net_current_period = utils.from_datetime_like(val_date, time_period_type)
    net_multi_factor_params = net_mf.MultiFactorParameters.For3FactorSeasonal[time_period_type](
        spot_mean_reversion, spot_vol, long_term_vol, seasonal_vol, net_current_period,
        cmdty_storage.net_storage.EndPeriod)
    # Transform factors x_st -> x0, x_lt -> x1, x_sw -> x2
    basis_func_transformed = basis_funcs.replace('x_st', 'x0').replace('x_lt', 'x1').replace('x_sw', 'x2')

    def add_multi_factor_sim(net_lsmc_params_builder):
        net_lsmc_params_builder.SimulateWithMultiFactorModelAndMersenneTwister(net_multi_factor_params, num_sims, seed,
                                                                               fwd_sim_seed)

    return _net_multi_factor_calc(cmdty_storage, fwd_curve, interest_rates, inventory, add_multi_factor_sim,
                                  num_inventory_grid_points, numerical_tolerance, on_progress_update,
                                  basis_func_transformed, settlement_rule, time_period_type,
                                  val_date, discount_deltas, extra_decisions, sim_data_returned)


def multi_factor_value(cmdty_storage: CmdtyStorage,
                       val_date: utils.TimePeriodSpecType,
                       inventory: float,
                       fwd_curve: pd.Series,
                       interest_rates: pd.Series,  # TODO change this to function which returns discount factor, i.e. delegate DF calc to caller.
                       settlement_rule: tp.Callable[[pd.Period], date],
                       factors: tp.Collection[tp.Tuple[float, utils.CurveType]],
                       factor_corrs: mfc.FactorCorrsType,
                       num_sims: int,
                       basis_funcs: str,
                       discount_deltas: bool,
                       seed: tp.Optional[int] = None,
                       fwd_sim_seed: tp.Optional[int] = None,
                       extra_decisions: tp.Optional[int] = None,
                       num_inventory_grid_points: int = 100,
                       numerical_tolerance: float = 1E-12,
                       on_progress_update: tp.Optional[tp.Callable[[float], None]] = None,
                       sim_data_returned: tp.Optional[SimulationDataReturned] = SimulationDataReturned.ALL # TODO on next major version increment change this to default to NONE
                       ) -> MultiFactorValuationResults:
    factor_corrs = mfc.validate_multi_factor_params(factors, factor_corrs)
    time_period_type = utils.FREQ_TO_PERIOD_TYPE[cmdty_storage.freq]
    net_multi_factor_params = mfc.create_net_multi_factor_params(factor_corrs, factors, time_period_type)

    def add_multi_factor_sim(net_lsmc_params_builder):
        net_lsmc_params_builder.SimulateWithMultiFactorModelAndMersenneTwister(net_multi_factor_params, num_sims,
                                                                               seed, fwd_sim_seed)

    return _net_multi_factor_calc(cmdty_storage, fwd_curve, interest_rates, inventory, add_multi_factor_sim,
                                  num_inventory_grid_points, numerical_tolerance, on_progress_update,
                                  basis_funcs, settlement_rule, time_period_type,
                                  val_date, discount_deltas, extra_decisions, sim_data_returned)


def value_from_sims(cmdty_storage: CmdtyStorage,
                    val_date: utils.TimePeriodSpecType,
                    inventory: float,
                    fwd_curve: pd.Series,
                    interest_rates: pd.Series,  # TODO change this to function which returns discount factor, i.e. delegate DF calc to caller.
                    settlement_rule: tp.Callable[[pd.Period], date],
                    sim_spot_regress: pd.DataFrame,
                    sim_spot_valuation: pd.DataFrame,
                    basis_funcs: str,
                    discount_deltas: bool,
                    sim_factors_regress: tp.Optional[tp.Iterable[pd.DataFrame]] = None,
                    sim_factors_valuation: tp.Optional[tp.Iterable[pd.DataFrame]] = None,
                    extra_decisions: tp.Optional[int] = None,
                    num_inventory_grid_points: int = 100,
                    numerical_tolerance: float = 1E-12,
                    on_progress_update: tp.Optional[tp.Callable[[float], None]] = None,
                    sim_data_returned: tp.Optional[SimulationDataReturned] = SimulationDataReturned.ALL # TODO on next major version increment change this to default to NONE
                    ) -> MultiFactorValuationResults:
    time_period_type = utils.FREQ_TO_PERIOD_TYPE[cmdty_storage.freq]
    net_sim_results_regress = _create_net_spot_sim_results(sim_spot_regress, sim_factors_regress, time_period_type)
    net_sim_results_valuation = _create_net_spot_sim_results(sim_spot_valuation, sim_factors_valuation, time_period_type)

    def add_sim_results(net_lsmc_params_builder):
        net_lsmc_params_builder.UseSpotSimResults(net_sim_results_regress, net_sim_results_valuation)

    return _net_multi_factor_calc(cmdty_storage, fwd_curve, interest_rates, inventory, add_sim_results,
                                  num_inventory_grid_points, numerical_tolerance, on_progress_update,
                                  basis_funcs, settlement_rule, time_period_type,
                                  val_date, discount_deltas, extra_decisions, sim_data_returned)


def _create_net_spot_sim_results(sim_spot, sim_factors, time_period_type):
    net_sim_spot = utils.data_frame_to_net_double_panel(sim_spot, time_period_type)
    net_sim_factors = dotnet_cols_gen.List[net_cc.Panel[time_period_type, dotnet.Double]]()
    for sim_factor in sim_factors:
        net_sim_panel = utils.data_frame_to_net_double_panel(sim_factor, time_period_type)
        net_sim_factors.Add(net_sim_panel)
    return net_cs.PythonHelpers.SpotSimResultsFromPanels[time_period_type](net_sim_spot, net_sim_factors)


def _net_multi_factor_calc(cmdty_storage, fwd_curve, interest_rates, inventory, add_sim_to_val_params,
                           num_inventory_grid_points, numerical_tolerance, on_progress_update,
                           basis_funcs, settlement_rule, time_period_type,
                           val_date, discount_deltas, extra_decisions, sim_data_returned):
    if cmdty_storage.freq != fwd_curve.index.freqstr:
        raise ValueError("cmdty_storage and forward_curve have different frequencies.")
    # Convert inputs to .NET types
    net_forward_curve = utils.series_to_double_time_series(fwd_curve, time_period_type)
    net_current_period = utils.from_datetime_like(val_date, time_period_type)
    net_grid_calc = net_cs.FixedSpacingStateSpaceGridCalc.CreateForFixedNumberOfPointsOnGlobalInventoryRange[
        time_period_type](cmdty_storage.net_storage, num_inventory_grid_points)
    net_settlement_rule = utils.wrap_settle_for_dotnet(settlement_rule, cmdty_storage.freq)
    net_interest_rate_time_series = utils.series_to_double_time_series(interest_rates, utils.FREQ_TO_PERIOD_TYPE['D'])
    net_discount_func = net_cs.StorageHelper.CreateAct65ContCompDiscounterFromSeries(net_interest_rate_time_series)
    net_on_progress = utils.wrap_on_progress_for_dotnet(on_progress_update)

    logger.info('Compiling basis functions. Takes a few seconds on the first run.')
    net_basis_functions = net_cs.BasisFunctionsBuilder.Parse(basis_funcs)
    logger.info('Compilation of basis functions complete.')

    # Intrinsic calc
    logger.info('Calculating intrinsic value.')
    intrinsic_result = cs_intrinsic.net_intrinsic_calc(cmdty_storage, net_current_period, net_interest_rate_time_series,
                                                       inventory, net_forward_curve, net_settlement_rule,
                                                       num_inventory_grid_points,
                                                       numerical_tolerance, time_period_type)
    logger.info('Calculation of intrinsic value complete.')

    # Multi-factor calc
    # TODO: pass sim_data_returned through to .NET API do avoid this simulation data even getting allocated
    logger.info('Calculating LSMC value.')
    net_logger = utils.create_net_log_adapter(logger, net_cs.LsmcStorageValuation)
    lsmc = net_cs.LsmcStorageValuation(net_logger)
    net_lsmc_params_builder = net_cs.PythonHelpers.ObjectFactory.CreateLsmcValuationParamsBuilder[time_period_type]()
    net_lsmc_params_builder.CurrentPeriod = net_current_period
    net_lsmc_params_builder.Inventory = inventory
    net_lsmc_params_builder.ForwardCurve = net_forward_curve
    net_lsmc_params_builder.Storage = cmdty_storage.net_storage
    net_lsmc_params_builder.SettleDateRule = net_settlement_rule
    net_lsmc_params_builder.DiscountFactors = net_discount_func
    net_lsmc_params_builder.GridCalc = net_grid_calc
    net_lsmc_params_builder.NumericalTolerance = numerical_tolerance
    net_lsmc_params_builder.BasisFunctions = net_basis_functions
    net_lsmc_params_builder.SimulationDataReturned = net_cs.SimulationDataReturned(sim_data_returned.value)
    if net_on_progress is not None:
        net_lsmc_params_builder.OnProgressUpdate = net_on_progress
    net_lsmc_params_builder.DiscountDeltas = discount_deltas
    if extra_decisions is not None:
        net_lsmc_params_builder.ExtraDecisions = extra_decisions
    add_sim_to_val_params(net_lsmc_params_builder)

    net_lsmc_params = net_lsmc_params_builder.Build()
    net_val_results = lsmc.Calculate[time_period_type](net_lsmc_params)
    logger.info('Calculation of LSMC value complete.')

    deltas = utils.net_time_series_to_pandas_series(net_val_results.Deltas, cmdty_storage.freq)
    expected_profile = cs_intrinsic.profile_to_data_frame(cmdty_storage.freq, net_val_results.ExpectedStorageProfile)
    trigger_prices = _trigger_prices_to_data_frame(cmdty_storage.freq, net_val_results.TriggerPrices)
    trigger_profiles = _trigger_profiles_to_data_frame(cmdty_storage.freq, net_val_results.TriggerPriceVolumeProfiles)
    sim_spot_regress = utils.net_panel_to_data_frame(net_val_results.RegressionSpotPriceSim, cmdty_storage.freq)
    sim_spot_valuation = utils.net_panel_to_data_frame(net_val_results.ValuationSpotPriceSim, cmdty_storage.freq)
    sim_inventory = utils.net_panel_to_data_frame(net_val_results.InventoryBySim, cmdty_storage.freq)
    sim_inject_withdraw = utils.net_panel_to_data_frame(net_val_results.InjectWithdrawVolumeBySim, cmdty_storage.freq)
    sim_cmdty_consumed = utils.net_panel_to_data_frame(net_val_results.CmdtyConsumedBySim, cmdty_storage.freq)
    sim_inventory_loss = utils.net_panel_to_data_frame(net_val_results.InventoryLossBySim, cmdty_storage.freq)
    sim_net_volume = utils.net_panel_to_data_frame(net_val_results.NetVolumeBySim, cmdty_storage.freq)
    sim_pv = utils.net_panel_to_data_frame(net_val_results.PvByPeriodAndSim, cmdty_storage.freq)
    sim_factors_regress = _net_panel_enumerable_to_data_frame_tuple(net_val_results.RegressionMarkovFactors, cmdty_storage.freq)
    sim_factors_valuation = _net_panel_enumerable_to_data_frame_tuple(net_val_results.ValuationMarkovFactors, cmdty_storage.freq)

    return MultiFactorValuationResults(net_val_results.Npv, deltas, expected_profile,
                                       intrinsic_result.npv, intrinsic_result.profile, sim_spot_regress,
                                       sim_spot_valuation, sim_factors_regress, sim_factors_valuation,
                                       sim_inventory, sim_inject_withdraw,
                                       sim_cmdty_consumed, sim_inventory_loss, sim_net_volume, sim_pv,
                                       trigger_prices, trigger_profiles)


def _net_panel_enumerable_to_data_frame_tuple(net_panel_enumerable, freq) -> tp.Tuple[pd.DataFrame, ...]:
    return tuple(utils.net_panel_to_data_frame(net_panel, freq) for net_panel in net_panel_enumerable)


def _trigger_prices_to_data_frame(freq, net_trigger_prices) -> pd.DataFrame:
    index = _create_period_index(freq, net_trigger_prices)
    inject_volume = _create_empty_list(net_trigger_prices.Count)
    inject_trigger_price = _create_empty_list(net_trigger_prices.Count)
    withdraw_volume = _create_empty_list(net_trigger_prices.Count)
    withdraw_trigger_price = _create_empty_list(net_trigger_prices.Count)
    for i, trig in enumerate(net_trigger_prices.Data):
        if trig.HasInjectPrice:
            inject_volume[i] = trig.MaxInjectVolume
            inject_trigger_price[i] = trig.MaxInjectTriggerPrice
        if trig.HasWithdrawPrice:
            withdraw_volume[i] = trig.MaxWithdrawVolume
            withdraw_trigger_price[i] = trig.MaxWithdrawTriggerPrice
    data_frame_data = {'inject_volume': inject_volume, 'inject_trigger_price': inject_trigger_price,
                       'withdraw_volume': withdraw_volume, 'withdraw_trigger_price': withdraw_trigger_price}
    data_frame = pd.DataFrame(data=data_frame_data, index=index)
    return data_frame


def _create_period_index(freq, net_time_series):
    if net_time_series.Count == 0:
        return pd.PeriodIndex(data=[], freq=freq)
    else:
        profile_start = utils.net_datetime_to_py_datetime(net_time_series.Indices[0].Start)
        return pd.period_range(start=profile_start, freq=freq, periods=net_time_series.Count)


def _create_empty_list(count: int) -> tp.List:
    return [None] * count


def _trigger_profiles_to_data_frame(freq, net_trigger_profiles) -> pd.Series:
    index = _create_period_index(freq, net_trigger_profiles)
    profiles_list = _create_empty_list(net_trigger_profiles.Count)
    for i, prof in enumerate(net_trigger_profiles.Data):
        inject_triggers = [TriggerPricePoint(x.Volume, x.Price) for x in prof.InjectTriggerPrices]
        withdraw_triggers = [TriggerPricePoint(x.Volume, x.Price) for x in prof.WithdrawTriggerPrices]
        profiles_list[i] = TriggerPriceProfile(inject_triggers, withdraw_triggers)
    return pd.Series(data=profiles_list, index=index)
