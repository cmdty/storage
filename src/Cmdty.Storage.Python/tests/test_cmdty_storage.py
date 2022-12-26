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

import unittest
import cmdty_storage as cs
from datetime import datetime
import pandas as pd
from tests import utils


class TestCmdtyStorage(unittest.TestCase):

    _default_freq = 'D'
    _default_ratchets = (
                                    (datetime(2019, 8, 28),
                                     (
                                            (0.0, -150.0, 255.2),
                                            (2000.0, -200.0, 175.0),
                                     )),
                            (datetime(2019, 9, 10),
                                    (
                                         (0.0, -170.5, 235.8),
                                         (700.0, -180.2, 200.77),
                                         (1800.0, -190.5, 174.45),
                                    ))
                            )

    _constant_min_inventory = 2.54
    _constant_max_inventory = 1234.56
    _constant_max_injection_rate = 65.64
    _constant_max_withdrawal_rate = 107.07

    _series_min_inventory = utils.create_piecewise_flat_series([2.4, 1.2, 0.0, 0.0],
                            [datetime(2019, 8, 28), datetime(2019, 9, 1), datetime(2019, 9, 10), datetime(2019, 9, 25)], 'D')
    _series_max_inventory = utils.create_piecewise_flat_series([1250.5, 1358.5, 54.5, 54.5],
                            [datetime(2019, 8, 28), datetime(2019, 9, 1), datetime(2019, 9, 10), datetime(2019, 9, 25)], 'D')
    _series_max_injection_rate = utils.create_piecewise_flat_series([125.5, 100, 120.66, 120.66],
                            [datetime(2019, 8, 28), datetime(2019, 9, 1), datetime(2019, 9, 10), datetime(2019, 9, 25)], 'D')
    _series_max_withdrawal_rate = utils.create_piecewise_flat_series([211.52, 200, 220.66, 220.66],
                            [datetime(2019, 8, 28), datetime(2019, 9, 1), datetime(2019, 9, 10), datetime(2019, 9, 25)], 'D')

    _default_storage_start = datetime(2019, 8, 28)
    _default_storage_end = datetime(2019, 9, 25)
    _default_ratchets_interp = cs.RatchetInterp.LINEAR

    _constant_injection_cost = 0.015
    _constant_cmdty_consumed_inject = 0.0001
    _constant_withdrawal_cost = 0.02
    _constant_cmdty_consumed_withdraw = 0.000088
    _constant_inventory_loss = 0.001
    _constant_inventory_cost = 0.002


    _series_injection_cost = utils.create_piecewise_flat_series([1.41384, 2.284, 0.75, 0.75],
                            [datetime(2019, 8, 28), datetime(2019, 9, 1), datetime(2019, 9, 10), datetime(2019, 9, 25)], 'D')
    _series_cmdty_consumed_inject = utils.create_piecewise_flat_series([0.438, 0.413, 4.434, 4.434],
                            [datetime(2019, 8, 28), datetime(2019, 9, 1), datetime(2019, 9, 10), datetime(2019, 9, 25)], 'D')
    _series_withdrawal_cost = utils.create_piecewise_flat_series([0.143, 0.248, 5, 5],
                            [datetime(2019, 8, 28), datetime(2019, 9, 1), datetime(2019, 9, 10), datetime(2019, 9, 25)], 'D')
    _series_cmdty_consumed_withdraw = utils.create_piecewise_flat_series([0.045, 0.0415, 2, 2],
                            [datetime(2019, 8, 28), datetime(2019, 9, 1), datetime(2019, 9, 10), datetime(2019, 9, 25)], 'D')
    _series_inventory_loss = utils.create_piecewise_flat_series([0.003, 0.0015, 0.0017, 0.0017],
                            [datetime(2019, 8, 28), datetime(2019, 9, 1), datetime(2019, 9, 10), datetime(2019, 9, 25)], 'D')
    _series_inventory_cost = utils.create_piecewise_flat_series([0.04, 0.02, 0.055, 0.055],
                            [datetime(2019, 8, 28), datetime(2019, 9, 1), datetime(2019, 9, 10), datetime(2019, 9, 25)], 'D')

    _default_terminal_npv_calc = lambda price, inventory: price * inventory - 15.4 # Some arbitrary calculation
    
    def _create_storage(cls, freq=_default_freq, storage_start=_default_storage_start, storage_end=_default_storage_end,
                        ratchets=_default_ratchets, ratchet_interp = _default_ratchets_interp,
                        min_inventory=None, max_inventory=None, max_injection_rate=None, max_withdrawal_rate=None,
                        injection_cost=_constant_injection_cost,
                        withdrawal_cost=_constant_withdrawal_cost, cmdty_consumed_inject=_constant_cmdty_consumed_inject,
                        cmdty_consumed_withdraw=_constant_cmdty_consumed_withdraw, terminal_storage_npv=_default_terminal_npv_calc,
                        inventory_loss=_constant_inventory_loss, inventory_cost=_constant_inventory_cost):

        return cs.CmdtyStorage(freq, storage_start, storage_end, injection_cost,
                               withdrawal_cost, ratchets=ratchets, ratchet_interp=ratchet_interp,
                               min_inventory=min_inventory, max_inventory=max_inventory,
                               max_injection_rate=max_injection_rate, max_withdrawal_rate=max_withdrawal_rate,
                               cmdty_consumed_inject=cmdty_consumed_inject,
                               cmdty_consumed_withdraw=cmdty_consumed_withdraw,
                               terminal_storage_npv=terminal_storage_npv, inventory_loss=inventory_loss,
                               inventory_cost=inventory_cost)

    def test_ratchets_step_interp_as_expected(self):
        step_ratchets = (('2019-08-28',
                          (
                                 (0.0, -150.0, 255.2),
                                 (2000.0, -150.0, 255.2),
                             )),
                         ('2019-09-10',
                                 (
                                     (0.0, -170.5, 235.8),
                                     (700.0, -180.2, 200.77),
                                     (1800.0, -180.2, 200.77),
                                 )))
        storage = self._create_storage(ratchets=step_ratchets, ratchet_interp=cs.RatchetInterp.STEP)
        for inventory in [0.0, 1252.5, 1999.0]:
            with_rate, inj_rate = storage.inject_withdraw_range('2019-09-05', inventory)
            self.assertEqual(-150.0, with_rate)
            self.assertEqual(255.2, inj_rate)

    def test_init_ratchets_arg_not_none_other_constraint_args_not_none_raises(self):
        with self.assertRaisesRegex(ValueError, "min_inventory parameter should not be provided if ratchets parameter is provided."):
            storage = self._create_storage(ratchets=self._default_ratchets, min_inventory=self._constant_min_inventory)
        
        with self.assertRaisesRegex(ValueError, "max_inventory parameter should not be provided if ratchets parameter is provided."):
            storage = self._create_storage(ratchets=self._default_ratchets, max_inventory=self._constant_max_inventory)
        
        with self.assertRaisesRegex(ValueError, "max_injection_rate parameter should not be provided if ratchets parameter is provided."):
            storage = self._create_storage(ratchets=self._default_ratchets, max_injection_rate=self._constant_max_injection_rate)
        
        with self.assertRaisesRegex(ValueError, "max_withdrawal_rate parameter should not be provided if ratchets parameter is provided."):
            storage = self._create_storage(ratchets=self._default_ratchets, max_withdrawal_rate=self._constant_max_withdrawal_rate)

    def test_init_ratchets_arg_none_other_constraint_args_none_raises(self):
        with self.assertRaisesRegex(ValueError, "min_inventory parameter should be provided if ratchets parameter is not provided."):
            storage = self._create_storage(ratchets=None, ratchet_interp=None, min_inventory=None,
                        max_inventory=self._constant_max_inventory, max_injection_rate=self._constant_max_injection_rate, 
                        max_withdrawal_rate=self._constant_max_withdrawal_rate)
        
        with self.assertRaisesRegex(ValueError, "max_inventory parameter should be provided if ratchets parameter is not provided."):
            storage = self._create_storage(ratchets=None, ratchet_interp=None, min_inventory=self._constant_min_inventory,
                        max_inventory=None, max_injection_rate=self._constant_max_injection_rate, 
                        max_withdrawal_rate=self._constant_max_withdrawal_rate)
        
        with self.assertRaisesRegex(ValueError, "max_injection_rate parameter should be provided if ratchets parameter is not provided."):
            storage = self._create_storage(ratchets=None, ratchet_interp=None, min_inventory=self._constant_min_inventory,
                        max_inventory=self._constant_max_inventory, max_injection_rate=None, 
                        max_withdrawal_rate=self._constant_max_withdrawal_rate)
        
        with self.assertRaisesRegex(ValueError, "max_withdrawal_rate parameter should be provided if ratchets parameter is not provided."):
            storage = self._create_storage(ratchets=None, ratchet_interp=None, min_inventory=self._constant_min_inventory,
                        max_inventory=self._constant_max_inventory, max_injection_rate=self._constant_max_injection_rate, 
                        max_withdrawal_rate=None)

    def test_init_ratchets_arg_none_ratchets_interp_not_none_raises(self):
        with self.assertRaisesRegex(ValueError, "ratchet_interp should not be provided if ratchets parameter is not provided."):
            storage = self._create_storage(ratchets=None, ratchet_interp=cs.RatchetInterp.LINEAR, min_inventory=self._constant_min_inventory,
                        max_inventory=self._constant_max_inventory, max_injection_rate=self._constant_max_injection_rate,
                        max_withdrawal_rate=self._constant_max_withdrawal_rate)

    def test_init_ratchets_arg_not_none_ratchets_none_raises(self):
        with self.assertRaisesRegex(ValueError, "ratchet_interp parameter should be provided if ratchets parameter is provided."):
            storage = self._create_storage(ratchets=self._default_ratchets, ratchet_interp=None, min_inventory=None,
                        max_inventory=None, max_injection_rate=None, max_withdrawal_rate=None)

    def test_start_property(self):
        storage = self._create_storage()
        self.assertEqual(pd.Period(self._default_storage_start, freq='D'), storage.start)
        
    def test_end_property(self):
        storage = self._create_storage()
        self.assertEqual(pd.Period(self._default_storage_end, freq='D'), storage.end)
        
    def test_freq_property(self):
        storage = self._create_storage()
        self.assertEqual(self._default_freq, storage.freq)
        
    def test_empty_at_end_true_when_terminal_storage_npv_none(self):
        storage = self._create_storage(terminal_storage_npv=None)
        self.assertEqual(True, storage.empty_at_end)
        
    def test_empty_at_end_false_when_terminal_storage_npv_not_none(self):
        storage = self._create_storage()
        self.assertEqual(False, storage.empty_at_end)

    def test_terminal_storage_npv_always_zero_when_terminal_storage_npv_none(self):
        storage = self._create_storage(terminal_storage_npv=None)
        for cmdty_price in [0.0, 23.85, 75.9, 100.22]:
            for terminal_inventory in [0.0, 500.58, 1268.65, 1800.0]:
                self.assertEqual(0.0, storage.terminal_storage_npv(cmdty_price, terminal_inventory))

    def test_terminal_storage_npv_evaluates_to_function_specified(self):
        storage = self._create_storage()
        for cmdty_price in [0.0, 23.85, 75.9, 100.22]:
            for terminal_inventory in [0.0, 500.58, 1268.65, 1800.0]:
                self.assertEqual(TestCmdtyStorage._default_terminal_npv_calc(cmdty_price, terminal_inventory), 
                                 storage.terminal_storage_npv(cmdty_price, terminal_inventory))

    def test_inject_withdraw_range_linearly_interpolated(self):
        storage = self._create_storage()
        # Inventory half way between pillars, so assert against mean of min/max inject/withdraw at the pillars
        min_dec, max_dec = storage.inject_withdraw_range(datetime(2019, 8, 29), 1000.0)
        self.assertEqual(-175.0, min_dec)
        self.assertEqual((255.2 + 175.0)/2.0, max_dec)

    def test_inject_withdraw_range_from_float_init_parameters(self):
        storage = self._create_storage(ratchets=None, ratchet_interp=None, min_inventory=self._constant_min_inventory,
                        max_inventory=self._constant_max_inventory, max_injection_rate=self._constant_max_injection_rate, 
                        max_withdrawal_rate=self._constant_max_withdrawal_rate)
        
        for inventory in [2.54, 500.58, 1234.56]:
            for dt in [datetime(2019, 8, 28), datetime(2019, 9, 1), datetime(2019, 9, 20)]:
                min_dec, max_dec = storage.inject_withdraw_range(dt, inventory)
                self.assertEqual(-self._constant_max_withdrawal_rate, min_dec)
                self.assertEqual(self._constant_max_injection_rate, max_dec)

    def test_inject_withdraw_range_from_int_init_parameters(self):
        int_max_injection_rate = int(self._constant_max_injection_rate)
        int_max_withdrawal_rate = int(self._constant_max_withdrawal_rate)
        storage = self._create_storage(ratchets=None, ratchet_interp=None, min_inventory=self._constant_min_inventory,
                        max_inventory=self._constant_max_inventory, max_injection_rate=int_max_injection_rate, 
                        max_withdrawal_rate=int_max_withdrawal_rate)
        
        for inventory in [2.54, 500.58, 1234.56]:
            for dt in [datetime(2019, 8, 28), datetime(2019, 9, 1), datetime(2019, 9, 20)]:
                min_dec, max_dec = storage.inject_withdraw_range(dt, inventory)
                self.assertEqual(-int_max_withdrawal_rate, min_dec)
                self.assertEqual(int_max_injection_rate, max_dec)

    def test_inject_withdraw_range_from_series_init_parameters(self):
        storage = self._create_storage(ratchets=None, ratchet_interp=None, min_inventory=self._constant_min_inventory,
                        max_inventory=self._constant_max_inventory, max_injection_rate=self._series_max_injection_rate, 
                        max_withdrawal_rate=self._series_max_withdrawal_rate)

        for dt in [datetime(2019, 8, 28), datetime(2019, 9, 1), datetime(2019, 9, 20)]:
            expected_min_dec = -self._series_max_withdrawal_rate[dt]
            expeted_max_dec = self._series_max_injection_rate[dt]
            for inventory in [2.54, 500.58, 1234.56]:
                min_dec, max_dec = storage.inject_withdraw_range(dt, inventory)
                self.assertEqual(expected_min_dec, min_dec)
                self.assertEqual(expeted_max_dec, max_dec)
                
    def test_inject_withdraw_range_from_series_max_injection_rate_init_parameters(self):
        storage = self._create_storage(ratchets=None, ratchet_interp=None, min_inventory=self._constant_min_inventory,
                        max_inventory=self._constant_max_inventory, max_injection_rate=self._series_max_injection_rate, 
                        max_withdrawal_rate=self._constant_max_withdrawal_rate)

        for dt in [datetime(2019, 8, 28), datetime(2019, 9, 1), datetime(2019, 9, 20)]:
            expected_min_dec = -self._constant_max_withdrawal_rate
            expeted_max_dec = self._series_max_injection_rate[dt]
            for inventory in [2.54, 500.58, 1234.56]:
                min_dec, max_dec = storage.inject_withdraw_range(dt, inventory)
                self.assertEqual(expected_min_dec, min_dec)
                self.assertEqual(expeted_max_dec, max_dec)

    def test_inject_withdraw_range_from_series_max_withdrawal_rate_init_parameters(self):
        storage = self._create_storage(ratchets=None, ratchet_interp=None, min_inventory=self._constant_min_inventory,
                        max_inventory=self._constant_max_inventory, max_injection_rate=self._constant_max_injection_rate, 
                        max_withdrawal_rate=self._series_max_withdrawal_rate)

        for dt in [datetime(2019, 8, 28), datetime(2019, 9, 1), datetime(2019, 9, 20)]:
            expected_min_dec = -self._series_max_withdrawal_rate[dt]
            expeted_max_dec = self._constant_max_injection_rate
            for inventory in [2.54, 500.58, 1234.56]:
                min_dec, max_dec = storage.inject_withdraw_range(dt, inventory)
                self.assertEqual(expected_min_dec, min_dec)
                self.assertEqual(expeted_max_dec, max_dec)

    def test_min_inventory_property_from_constraints_table(self):
        storage = self._create_storage()
        self.assertEqual(0.0, storage.min_inventory(datetime(2019, 8, 29)))
        self.assertEqual(0.0, storage.min_inventory(datetime(2019, 9, 11)))

    def test_min_inventory_property_from_float_init_param(self):
        storage = self._create_storage(ratchets=None, ratchet_interp=None, min_inventory=self._constant_min_inventory,
                        max_inventory=self._constant_max_inventory, max_injection_rate=self._constant_max_injection_rate, 
                        max_withdrawal_rate=self._constant_max_withdrawal_rate)
        self.assertEqual(self._constant_min_inventory, storage.min_inventory(datetime(2019, 8, 29)))
        self.assertEqual(self._constant_min_inventory, storage.min_inventory(datetime(2019, 9, 11)))
        
    def test_min_inventory_property_from_series_init_param(self):
        storage = self._create_storage(ratchets=None, ratchet_interp=None, min_inventory=self._series_min_inventory,
                        max_inventory=self._series_max_inventory, max_injection_rate=self._constant_max_injection_rate, 
                        max_withdrawal_rate=self._constant_max_withdrawal_rate)
        self.assertEqual(2.4, storage.min_inventory(datetime(2019, 8, 29)))
        self.assertEqual(1.2, storage.min_inventory(datetime(2019, 9, 1)))
        self.assertEqual(0.0, storage.min_inventory(datetime(2019, 9, 11)))

    def test_max_inventory_property_from_float_init_param(self):
        storage = self._create_storage(ratchets=None, ratchet_interp=None, min_inventory=self._constant_min_inventory,
                        max_inventory=self._constant_max_inventory, max_injection_rate=self._constant_max_injection_rate, 
                        max_withdrawal_rate=self._constant_max_withdrawal_rate)
        self.assertEqual(self._constant_max_inventory, storage.max_inventory(datetime(2019, 8, 29)))
        self.assertEqual(self._constant_max_inventory, storage.max_inventory(datetime(2019, 9, 11)))

    def test_max_inventory_property_from_series_init_param(self):
        storage = self._create_storage(ratchets=None, ratchet_interp=None, min_inventory=self._series_min_inventory,
                        max_inventory=self._series_max_inventory, max_injection_rate=self._constant_max_injection_rate, 
                        max_withdrawal_rate=self._constant_max_withdrawal_rate)
        self.assertEqual(1250.5, storage.max_inventory(datetime(2019, 8, 29)))
        self.assertEqual(1358.5, storage.max_inventory(datetime(2019, 9, 1)))
        self.assertEqual(54.5, storage.max_inventory(datetime(2019, 9, 11)))
        
    def test_max_inventory_property_from_constraints_table(self):
        storage = self._create_storage()
        self.assertEqual(2000.0, storage.max_inventory(datetime(2019, 8, 29)))
        self.assertEqual(1800.0, storage.max_inventory(datetime(2019, 9, 11)))

    def test_injection_cost_scalar_init_parameter(self):
        storage = self._create_storage()
        injected_volume = 58.74
        for dt in [datetime(2019, 8, 28), datetime(2019, 9, 1), datetime(2019, 9, 20)]:
            for inventory in [0, 500.58, 1234.56, 1800]:
                injection_cost = storage.injection_cost(dt, inventory, injected_volume)
                self.assertEqual(injected_volume * self._constant_injection_cost, injection_cost)

    def test_injection_cost_series_init_parameter(self):
        storage = self._create_storage(injection_cost=self._series_injection_cost)
        injected_volume = 58.74
        for dt in [datetime(2019, 8, 28), datetime(2019, 9, 1), datetime(2019, 9, 20)]:
            expected_injection_cost = self._series_injection_cost[dt]*injected_volume
            for inventory in [0, 500.58, 1234.56, 1800]:
                injection_cost = storage.injection_cost(dt, inventory, injected_volume)
                self.assertEqual(expected_injection_cost, injection_cost)

    def test_cmdty_consumed_inject_scalar_init_parameter(self):
        storage = self._create_storage()
        injected_volume = 58.74
        for dt in [datetime(2019, 8, 28), datetime(2019, 9, 1), datetime(2019, 9, 20)]:
            for inventory in [2.54, 500.58, 1234.56]:
                cmdty_consumed_inject = storage.cmdty_consumed_inject(dt, inventory, injected_volume)
                self.assertEqual(injected_volume * self._constant_cmdty_consumed_inject, cmdty_consumed_inject)

    def test_cmdty_consumed_inject_none_init_parameter(self):
        storage = self._create_storage(cmdty_consumed_inject=None)
        injected_volume = 58.74
        for dt in [datetime(2019, 8, 28), datetime(2019, 9, 1), datetime(2019, 9, 20)]:
            for inventory in [2.54, 500.58, 1234.56]:
                cmdty_consumed_inject = storage.cmdty_consumed_inject(dt, inventory, injected_volume)
                self.assertEqual(0, cmdty_consumed_inject)

    def test_cmdty_consumed_inject_series_init_parameter(self):
        storage = self._create_storage(cmdty_consumed_inject=self._series_cmdty_consumed_inject)
        injected_volume = 58.74
        for dt in [datetime(2019, 8, 28), datetime(2019, 9, 1), datetime(2019, 9, 20)]:
            expected_cmdty_consumed_inject = self._series_cmdty_consumed_inject[dt] * injected_volume
            for inventory in [2.54, 500.58, 1234.56]:
                cmdty_consumed_inject = storage.cmdty_consumed_inject(dt, inventory, injected_volume)
                self.assertEqual(expected_cmdty_consumed_inject, cmdty_consumed_inject)

    def test_withdrawal_cost_scalar_init_parameter(self):
        storage = self._create_storage()
        withdrawn_volume = 12.05
        for dt in [datetime(2019, 8, 28), datetime(2019, 9, 1), datetime(2019, 9, 20)]:
            for inventory in [0, 500.58, 1234.56, 1800]:
                withdrawal_cost = storage.withdrawal_cost(dt, inventory, withdrawn_volume)
                self.assertEqual(withdrawn_volume * self._constant_withdrawal_cost, withdrawal_cost)

    def test_withdrawal_cost_series_init_parameter(self):
        storage = self._create_storage(withdrawal_cost=self._series_withdrawal_cost)
        withdrawn_volume = 12.05
        for dt in [datetime(2019, 8, 28), datetime(2019, 9, 1), datetime(2019, 9, 20)]:
            expected_withdrawal_cost = self._series_withdrawal_cost[dt] * withdrawn_volume
            for inventory in [0, 500.58, 1234.56, 1800]:
                withdrawal_cost = storage.withdrawal_cost(dt, inventory, withdrawn_volume)
                self.assertEqual(expected_withdrawal_cost, withdrawal_cost)

    def test_cmdty_consumed_withdraw_scalar_init_parameter(self):
        storage = self._create_storage()
        withdrawn_volume = 12.05
        for dt in [datetime(2019, 8, 28), datetime(2019, 9, 1), datetime(2019, 9, 20)]:
            for inventory in [2.54, 500.58, 1234.56]:
                cmdty_consumed_withdraw = storage.cmdty_consumed_withdraw(dt, inventory, withdrawn_volume)
                self.assertEqual(withdrawn_volume * self._constant_cmdty_consumed_withdraw, cmdty_consumed_withdraw)

    def test_cmdty_consumed_withdraw_none_init_parameter(self):
        storage = self._create_storage(cmdty_consumed_withdraw=None)
        withdrawn_volume = 12.05
        for dt in [datetime(2019, 8, 28), datetime(2019, 9, 1), datetime(2019, 9, 20)]:
            for inventory in [2.54, 500.58, 1234.56]:
                cmdty_consumed_withdraw = storage.cmdty_consumed_withdraw(dt, inventory, withdrawn_volume)
                self.assertEqual(0, cmdty_consumed_withdraw)

    def test_cmdty_consumed_withdraw_series_init_parameter(self):
        storage = self._create_storage(cmdty_consumed_withdraw=self._series_cmdty_consumed_withdraw)
        withdrawn_volume = 12.05
        for dt in [datetime(2019, 8, 28), datetime(2019, 9, 1), datetime(2019, 9, 20)]:
            expected_cmdty_consumed_withdraw = self._series_cmdty_consumed_withdraw[dt] * withdrawn_volume
            for inventory in [2.54, 500.58, 1234.56]:
                cmdty_consumed_withdraw = storage.cmdty_consumed_withdraw(dt, inventory, withdrawn_volume)
                self.assertEqual(expected_cmdty_consumed_withdraw, cmdty_consumed_withdraw)

    def test_inventory_pcnt_loss_scalar_init_parameter(self):
        storage = self._create_storage()
        for dt in [datetime(2019, 8, 28), datetime(2019, 9, 1), datetime(2019, 9, 20)]:
            inventory_loss = storage.inventory_pcnt_loss(dt)
            self.assertEqual(self._constant_inventory_loss, inventory_loss)

    def test_inventory_pcnt_loss_none_init_parameter(self):
        storage = self._create_storage(inventory_loss=None)
        for dt in [datetime(2019, 8, 28), datetime(2019, 9, 1), datetime(2019, 9, 20)]:
            inventory_loss = storage.inventory_pcnt_loss(dt)
            self.assertEqual(0, inventory_loss)

    def test_inventory_pcnt_loss_series_init_parameter(self):
        storage = self._create_storage(inventory_loss=self._series_inventory_loss)
        for dt in [datetime(2019, 8, 28), datetime(2019, 9, 1), datetime(2019, 9, 20)]:
            inventory_loss = storage.inventory_pcnt_loss(dt)
            expected_inventory_loss = self._series_inventory_loss[dt]
            self.assertEqual(expected_inventory_loss, inventory_loss)

    def test_inventory_cost_scalar_init_parameter(self):
        storage = self._create_storage()
        for dt in [datetime(2019, 8, 28), datetime(2019, 9, 1), datetime(2019, 9, 20)]:
            for inventory in [0, 500.58, 1234.56, 1800]:
                inventory_cost = storage.inventory_cost(dt, inventory)
                self.assertEqual(self._constant_inventory_cost * inventory, inventory_cost)

    def test_inventory_cost_none_init_parameter(self):
        storage = self._create_storage(inventory_cost=None)
        for dt in [datetime(2019, 8, 28), datetime(2019, 9, 1), datetime(2019, 9, 20)]:
            for inventory in [0, 500.58, 1234.56, 1800]:
                inventory_cost = storage.inventory_cost(dt, inventory)
                self.assertEqual(0.0, inventory_cost)

    def test_inventory_cost_series_init_parameter(self):
        storage = self._create_storage(inventory_cost=self._series_inventory_cost)
        for dt in [datetime(2019, 8, 28), datetime(2019, 9, 1), datetime(2019, 9, 20)]:
            expected_inventory_cost = self._series_inventory_cost[dt]
            for inventory in [0, 500.58, 1234.56, 1800]:
                inventory_cost = storage.inventory_cost(dt, inventory)
                self.assertEqual(expected_inventory_cost * inventory, inventory_cost)


class TestUtils(unittest.TestCase):
    def test_numerics_provider_mkl(self):
        provider = cs.numerics_provider()
        self.assertEqual(provider, 'Intel MKL (x64; revision 13; ahead revision 12; MKL 2020.0 Update 1)')


if __name__ == '__main__':
    unittest.main()
