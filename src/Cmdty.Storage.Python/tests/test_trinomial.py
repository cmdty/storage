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

import unittest
import pandas as pd
import cmdty_storage as cs
from datetime import date, timedelta
from tests import utils


class TestIntrinsicValue(unittest.TestCase):

    def test_trinomial_value_runs(self):
        constraints = [
            (date(2019, 8, 28),
             [
                 (0.0, -150.0, 255.2),
                 (2000.0, -200.0, 175.0),
             ]),
            (date(2019, 9, 10),
             [
                 (0.0, -170.5, 235.8),
                 (700.0, -180.2, 200.77),
                 (1800.0, -190.5, 174.45),
             ])
        ]

        storage_start = date(2019, 8, 28)
        storage_end = date(2019, 9, 25)
        constant_injection_cost = 0.015
        constant_pcnt_consumed_inject = 0.0001
        constant_withdrawal_cost = 0.02
        constant_pcnt_consumed_withdraw = 0.000088
        constant_pcnt_inventory_loss = 0.001;
        constant_pcnt_inventory_cost = 0.002;

        def terminal_npv_calc(price, inventory):
            return price * inventory - 15.4  # Some arbitrary calculation

        cmdty_storage = cs.CmdtyStorage('D', storage_start, storage_end, constant_injection_cost,
                                        constant_withdrawal_cost, constraints, ratchet_interp=cs.RatchetInterp.LINEAR,
                                        cmdty_consumed_inject=constant_pcnt_consumed_inject,
                                        cmdty_consumed_withdraw=constant_pcnt_consumed_withdraw,
                                        terminal_storage_npv=terminal_npv_calc,
                                        inventory_loss=constant_pcnt_inventory_loss,
                                        inventory_cost=constant_pcnt_inventory_cost)

        inventory = 650.0
        val_date = date(2019, 9, 2)

        forward_curve = utils.create_piecewise_flat_series([58.89, 61.41, 59.89, 59.89],
                                                           [val_date, date(2019, 9, 12), date(2019, 9, 18),
                                                            storage_end], freq='D')

        # TODO test with proper interest rate curve
        flat_interest_rate = 0.03
        interest_rate_curve = pd.Series(index=pd.period_range(val_date, storage_end + timedelta(days=60), freq='D'),
                                        dtype='float64')
        interest_rate_curve[:] = flat_interest_rate

        # Trinomial Tree parameters
        mean_reversion = 14.5
        spot_volatility = utils.create_piecewise_flat_series([1.35, 1.13, 1.24, 1.24],
                                                             [val_date, date(2019, 9, 12), date(2019, 9, 18),
                                                              storage_end], freq='D')
        time_step = 1.0 / 365.0

        twentieth_of_next_month = lambda period: period.asfreq('M').asfreq('D', 'end') + 20
        trinomial_value = cs.trinomial_value(cmdty_storage, val_date, inventory, forward_curve,
                                             spot_volatility, mean_reversion, time_step,
                                             settlement_rule=twentieth_of_next_month,
                                             interest_rates=interest_rate_curve, num_inventory_grid_points=100)
        self.assertTrue(isinstance(trinomial_value, float))

    def test_trinomial_deltas_runs(self):
        constraints = [
            (date(2019, 8, 28),
             [
                 (0.0, -150.0, 255.2),
                 (2000.0, -200.0, 175.0),
             ]),
            (date(2019, 9, 10),
             [
                 (0.0, -170.5, 235.8),
                 (700.0, -180.2, 200.77),
                 (1800.0, -190.5, 174.45),
             ])
        ]

        storage_start = date(2019, 8, 28)
        storage_end = date(2019, 9, 25)
        constant_injection_cost = 0.015
        constant_pcnt_consumed_inject = 0.0001
        constant_withdrawal_cost = 0.02
        constant_pcnt_consumed_withdraw = 0.000088
        constant_pcnt_inventory_loss = 0.001
        constant_pcnt_inventory_cost = 0.002

        def terminal_npv_calc(price, inventory):
            return price * inventory - 15.4  # Some arbitrary calculation

        cmdty_storage = cs.CmdtyStorage('D', storage_start, storage_end, constant_injection_cost,
                                        constant_withdrawal_cost, constraints, ratchet_interp=cs.RatchetInterp.LINEAR,
                                        cmdty_consumed_inject=constant_pcnt_consumed_inject,
                                        cmdty_consumed_withdraw=constant_pcnt_consumed_withdraw,
                                        terminal_storage_npv=terminal_npv_calc,
                                        inventory_loss=constant_pcnt_inventory_loss,
                                        inventory_cost=constant_pcnt_inventory_cost)
        inventory = 650.0
        val_date = date(2019, 9, 2)
        forward_curve = utils.create_piecewise_flat_series([58.89, 61.41, 59.89, 59.89],
                                                           [val_date, date(2019, 9, 12), date(2019, 9, 18),
                                                            storage_end], freq='D')
        # TODO test with proper interest rate curve
        flat_interest_rate = 0.03
        interest_rate_curve = pd.Series(index=pd.period_range(val_date, storage_end + timedelta(days=60), freq='D'),
                                        dtype='float64')
        interest_rate_curve[:] = flat_interest_rate
        # Trinomial Tree parameters
        mean_reversion = 14.5
        spot_volatility = utils.create_piecewise_flat_series([1.35, 1.13, 1.24, 1.24],
                                                             [val_date, date(2019, 9, 12), date(2019, 9, 18),
                                                              storage_end], freq='D')
        time_step = 1.0 / 365.0
        twentieth_of_next_month = lambda period: period.asfreq('M').asfreq('D', 'end') + 20
        trinomial_deltas = cs.trinomial_deltas(cmdty_storage, val_date, inventory, forward_curve,
                                               spot_volatility, mean_reversion, time_step,
                                               interest_rates=interest_rate_curve,
                                               settlement_rule=twentieth_of_next_month,
                                               fwd_contracts=['2018-08-28', '2018-08-29'],
                                               num_inventory_grid_points=100)
        self.assertTrue(isinstance(trinomial_deltas, list))

    def test_trinomial_delta_deep_itm_equals_intrinsic_delta(self):
        storage_start = '2019-12-01'
        storage_end = '2020-04-01'
        constant_injection_rate = 700.0
        constant_withdrawal_rate = 700.0
        constant_injection_cost = 1.23
        constant_withdrawal_cost = 0.98
        min_inventory = 0.0
        max_inventory = 100000.0

        cmdty_storage = cs.CmdtyStorage('D', storage_start, storage_end, constant_injection_cost,
                            constant_withdrawal_cost, min_inventory=min_inventory, max_inventory=max_inventory,
                            max_injection_rate=constant_injection_rate, max_withdrawal_rate=constant_withdrawal_rate)
        inventory = 0.0
        val_date = '2019-08-29'
        low_price = 23.87
        high_price = 150.32
        num_days_at_high_price = 20
        date_switch_high_price = '2020-03-12' # TODO calculate this from num_days_at_high_price
        forward_curve = utils.create_piecewise_flat_series([low_price, high_price, high_price],
                                                           [val_date, date_switch_high_price,
                                                            storage_end], freq='D')

        flat_interest_rate = 0.00
        interest_rate_curve = pd.Series(index=pd.period_range(val_date, '2020-06-01', freq='D'), dtype='float64')
        interest_rate_curve[:] = flat_interest_rate
        # Trinomial Tree parameters
        mean_reversion = 14.5
        spot_volatility = pd.Series(index=pd.period_range(val_date, '2020-06-01', freq='D'), dtype='float64')
        spot_volatility[:] = 1.15
        time_step = 1.0 / 365.0
        twentieth_of_next_month = lambda period: period.asfreq('M').asfreq('D', 'end') + 20

        delta_fwd_contracts = [(storage_start, '2020-03-11'),
                                (date_switch_high_price, storage_end)]
        trinomial_deltas = cs.trinomial_deltas(cmdty_storage, val_date, inventory, forward_curve,
                                               spot_volatility, mean_reversion, time_step,
                                               interest_rates=interest_rate_curve,
                                               settlement_rule=twentieth_of_next_month,
                                               fwd_contracts=delta_fwd_contracts,
                                               num_inventory_grid_points=500)
        withdraw_delta = trinomial_deltas[1]
        expected_withdraw_delta = constant_withdrawal_rate * num_days_at_high_price
        pcnt_error = (withdraw_delta - expected_withdraw_delta) / expected_withdraw_delta
        self.assertAlmostEqual(pcnt_error, 0.0, 3)
        self.assertTrue(isinstance(trinomial_deltas, list))
