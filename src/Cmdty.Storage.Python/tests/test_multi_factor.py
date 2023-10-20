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
from cmdty_storage import CmdtyStorage, three_factor_seasonal_value, \
    multi_factor_value, value_from_sims
from tests import utils
from os import path

# README: PROPER UNIT TESTS ARE IN THE C# CODE.
class TestMultiFactorValue(unittest.TestCase):
    def test_multi_factor_value_regression(self):
        storage_start = '2019-12-01'
        storage_end = '2020-04-01'
        constant_injection_rate = 700.0
        constant_withdrawal_rate = 700.0
        constant_injection_cost = 1.23
        constant_withdrawal_cost = 0.98
        min_inventory = 0.0
        max_inventory = 100000.0

        cmdty_storage = CmdtyStorage('D', storage_start, storage_end, constant_injection_cost,
                                     constant_withdrawal_cost, min_inventory=min_inventory,
                                     max_inventory=max_inventory,
                                     max_injection_rate=constant_injection_rate,
                                     max_withdrawal_rate=constant_withdrawal_rate)
        inventory = 0.0
        val_date = '2019-08-29'
        low_price = 23.87
        high_price = 150.32
        date_switch_high_price = '2020-03-12'
        forward_curve = utils.create_piecewise_flat_series([low_price, high_price, high_price],
                                                           [val_date, date_switch_high_price,
                                                            storage_end], freq='D')

        flat_interest_rate = 0.03
        interest_rate_curve = pd.Series(index=pd.period_range(val_date, '2020-06-01', freq='D'),
                                        dtype='float64')
        interest_rate_curve[:] = flat_interest_rate

        # Multi-Factor parameters
        mean_reversion = 16.2
        spot_volatility = pd.Series(index=pd.period_range(val_date, '2020-06-01', freq='D'), dtype='float64')
        spot_volatility[:] = 1.15

        def twentieth_of_next_month(period):
            return period.asfreq('M').asfreq('D', 'end') + 20

        long_term_vol = pd.Series(index=pd.period_range(val_date, '2020-06-01', freq='D'), dtype='float64')
        long_term_vol[:] = 0.14

        factors = [(0.0, long_term_vol),
                   (mean_reversion, spot_volatility)]
        factor_corrs = 0.64
        progresses = []

        def on_progress(progress):
            progresses.append(progress)

        # Simulation parameter
        num_sims = 500
        seed = 11
        fwd_sim_seed = seed  # Temporarily set to pass regression tests
        basis_funcs = '1 + x0 + x0**2 + x1 + x1*x1'
        discount_deltas = False

        multi_factor_val = multi_factor_value(cmdty_storage, val_date, inventory, forward_curve,
                                              interest_rate_curve, twentieth_of_next_month,
                                              factors, factor_corrs, num_sims,
                                              basis_funcs, discount_deltas,
                                              seed=seed,
                                              fwd_sim_seed=fwd_sim_seed,
                                              on_progress_update=on_progress)
        self.assertAlmostEqual(multi_factor_val.npv, 1780380.7581833513, places=6)
        self.assertEqual(progresses[-1], 1.0)
        self.assertEqual(245, len(progresses))
        self.assertEqual(1703773.0757192627, multi_factor_val.intrinsic_npv)
        self.assertEqual((123, num_sims), multi_factor_val.sim_spot_regress.shape)
        self.assertEqual((123, num_sims), multi_factor_val.sim_spot_valuation.shape)
        self.assertEqual((123, num_sims), multi_factor_val.sim_inventory.shape)
        self.assertEqual((123, num_sims), multi_factor_val.sim_inject_withdraw.shape)
        self.assertEqual((123, num_sims), multi_factor_val.sim_cmdty_consumed.shape)
        self.assertEqual((123, num_sims), multi_factor_val.sim_inventory_loss.shape)
        self.assertEqual((123, num_sims), multi_factor_val.sim_net_volume.shape)
        # Test factors
        self.assertEqual(len(factors), len(multi_factor_val.sim_factors_regress))
        for sim_factor_regress in multi_factor_val.sim_factors_regress:
            self.assertEqual((123, num_sims), sim_factor_regress.shape)
        self.assertEqual(len(factors), len(multi_factor_val.sim_factors_valuation))
        for sim_factor_valuation in multi_factor_val.sim_factors_valuation:
            self.assertEqual((123, num_sims), sim_factor_valuation.shape)

        regress_deltas, regress_expected_profile, regress_intrinsic_profile, regress_trigger_prices = \
            self._load_valuation_results_csvs(path.join('.', 'regression_test_data', 'multi_factor_test-1'))
        pd.testing.assert_series_equal(multi_factor_val.deltas, regress_deltas, check_names=False)
        pd.testing.assert_frame_equal(multi_factor_val.expected_profile, regress_expected_profile)
        pd.testing.assert_frame_equal(multi_factor_val.intrinsic_profile, regress_intrinsic_profile)
        pd.testing.assert_frame_equal(multi_factor_val.trigger_prices, regress_trigger_prices)

    def test_value_from_sims_using_sims_from_multi_factor_value(self):
        """Test which first runs multi_factor_value, then passes the simulated spot
        prices and factors from the result into the value_from_sims function.
        Tests that the result from value_from_sims is identical to that from
        multi_factor_value."""
        storage_start = '2019-12-01'
        storage_end = '2020-04-01'
        constant_injection_rate = 700.0
        constant_withdrawal_rate = 700.0
        constant_injection_cost = 1.23
        constant_withdrawal_cost = 0.98
        min_inventory = 0.0
        max_inventory = 100000.0

        cmdty_storage = CmdtyStorage('D', storage_start, storage_end, constant_injection_cost,
                                     constant_withdrawal_cost, min_inventory=min_inventory,
                                     max_inventory=max_inventory,
                                     max_injection_rate=constant_injection_rate,
                                     max_withdrawal_rate=constant_withdrawal_rate)
        inventory = 0.0
        val_date = '2019-08-29'
        low_price = 23.87
        high_price = 150.32
        date_switch_high_price = '2020-03-12'
        forward_curve = utils.create_piecewise_flat_series([low_price, high_price, high_price],
                                                           [val_date, date_switch_high_price,
                                                            storage_end], freq='D')

        flat_interest_rate = 0.03
        interest_rate_curve = pd.Series(index=pd.period_range(val_date, '2020-06-01', freq='D'),
                                        dtype='float64')
        interest_rate_curve[:] = flat_interest_rate

        # Multi-Factor parameters
        mean_reversion = 16.2
        spot_volatility = pd.Series(index=pd.period_range(val_date, '2020-06-01', freq='D'), dtype='float64')
        spot_volatility[:] = 1.15

        def twentieth_of_next_month(period): return period.asfreq('M').asfreq('D', 'end') + 20

        long_term_vol = pd.Series(index=pd.period_range(val_date, '2020-06-01', freq='D'), dtype='float64')
        long_term_vol[:] = 0.14

        factors = [(0.0, long_term_vol),
                   (mean_reversion, spot_volatility)]
        factor_corrs = 0.64
        # Simulation parameter
        num_sims = 500
        seed = 11
        fwd_sim_seed = seed  # Temporarily set to pass regression tests
        basis_funcs = '1 + x0 + x0**2 + x1 + x1*x1'
        discount_deltas = False

        multi_factor_val = multi_factor_value(cmdty_storage, val_date, inventory, forward_curve,
                                              interest_rate_curve, twentieth_of_next_month,
                                              factors, factor_corrs, num_sims,
                                              basis_funcs, discount_deltas,
                                              seed=seed,
                                              fwd_sim_seed=fwd_sim_seed)
        value_from_sims_result = value_from_sims(cmdty_storage, val_date, inventory, forward_curve,
                                                 interest_rate_curve, twentieth_of_next_month,
                                                 multi_factor_val.sim_spot_regress,
                                                 multi_factor_val.sim_spot_valuation,
                                                 basis_funcs, discount_deltas,
                                                 multi_factor_val.sim_factors_regress,
                                                 multi_factor_val.sim_factors_valuation, )
        self.assertEqual(multi_factor_val.npv, value_from_sims_result.npv)
        self.assertTrue(multi_factor_val.deltas.equals(value_from_sims_result.deltas))
        self.assertTrue(multi_factor_val.expected_profile.equals(value_from_sims_result.expected_profile))
        self.assertEqual(multi_factor_val.intrinsic_npv, value_from_sims_result.intrinsic_npv)

    def test_three_factor_seasonal_regression(self):
        storage_start = '2019-12-01'
        storage_end = '2020-04-01'
        constant_injection_rate = 700.0
        constant_withdrawal_rate = 700.0
        constant_injection_cost = 1.23
        constant_withdrawal_cost = 0.98
        min_inventory = 0.0
        max_inventory = 100000.0

        cmdty_storage = CmdtyStorage('D', storage_start, storage_end, constant_injection_cost,
                                     constant_withdrawal_cost, min_inventory=min_inventory,
                                     max_inventory=max_inventory,
                                     max_injection_rate=constant_injection_rate,
                                     max_withdrawal_rate=constant_withdrawal_rate)
        inventory = 0.0
        val_date = '2019-08-29'
        low_price = 23.87
        high_price = 150.32
        date_switch_high_price = '2020-03-12'
        forward_curve = utils.create_piecewise_flat_series([low_price, high_price, high_price],
                                                           [val_date, date_switch_high_price,
                                                            storage_end], freq='D')

        flat_interest_rate = 0.03
        interest_rate_curve = pd.Series(index=pd.period_range(val_date, '2020-06-01', freq='D'), dtype='float64')
        interest_rate_curve[:] = flat_interest_rate

        # Multi-Factor parameters
        spot_mean_reversion = 16.2
        spot_volatility = 1.15
        seasonal_volatility = 0.18
        long_term_vol = 0.14

        def twentieth_of_next_month(period):
            return period.asfreq('M').asfreq('D', 'end') + 20

        progresses = []

        def on_progress(progress):
            progresses.append(progress)

        # Simulation parameter
        num_sims = 500
        seed = 11
        fwd_sim_seed = seed  # Temporarily set to pass regression tests
        basis_funcs = '1 + x_st + x_sw + x_lt + x_st**2 + x_sw**2 + x_lt**2'
        discount_deltas = False

        multi_factor_val = three_factor_seasonal_value(cmdty_storage, val_date, inventory, forward_curve,
                                                       interest_rate_curve, twentieth_of_next_month,
                                                       spot_mean_reversion, spot_volatility, long_term_vol,
                                                       seasonal_volatility,
                                                       num_sims,
                                                       basis_funcs,
                                                       discount_deltas,
                                                       seed=seed,
                                                       fwd_sim_seed=fwd_sim_seed,
                                                       on_progress_update=on_progress)
        self.assertAlmostEqual(multi_factor_val.npv, 1766460.137569665, places=6)
        self.assertEqual(progresses[-1], 1.0)
        self.assertEqual(245, len(progresses))
        self.assertEqual(1703773.0757192627, multi_factor_val.intrinsic_npv)
        self.assertEqual((123, num_sims), multi_factor_val.sim_spot_regress.shape)
        self.assertEqual((123, num_sims), multi_factor_val.sim_spot_valuation.shape)
        self.assertEqual((123, num_sims), multi_factor_val.sim_inventory.shape)
        self.assertEqual((123, num_sims), multi_factor_val.sim_inject_withdraw.shape)
        self.assertEqual((123, num_sims), multi_factor_val.sim_cmdty_consumed.shape)
        self.assertEqual((123, num_sims), multi_factor_val.sim_inventory_loss.shape)
        self.assertEqual((123, num_sims), multi_factor_val.sim_net_volume.shape)
        # Test factors
        self.assertEqual(3, len(multi_factor_val.sim_factors_regress))
        for sim_factor_regress in multi_factor_val.sim_factors_regress:
            self.assertEqual((123, num_sims), sim_factor_regress.shape)
        self.assertEqual(3, len(multi_factor_val.sim_factors_valuation))
        for sim_factor_valuation in multi_factor_val.sim_factors_valuation:
            self.assertEqual((123, num_sims), sim_factor_valuation.shape)

        regress_deltas, regress_expected_profile, regress_intrinsic_profile, regress_trigger_prices = \
            self._load_valuation_results_csvs(path.join('.', 'regression_test_data', 'three_factor_test-1'))
        pd.testing.assert_series_equal(multi_factor_val.deltas, regress_deltas, check_names=False)
        pd.testing.assert_frame_equal(multi_factor_val.expected_profile, regress_expected_profile)
        pd.testing.assert_frame_equal(multi_factor_val.intrinsic_profile, regress_intrinsic_profile)
        pd.testing.assert_frame_equal(multi_factor_val.trigger_prices, regress_trigger_prices)

    @staticmethod
    def _save_valuation_results_csvs(val_results, root_path: str):
        val_results.deltas.to_csv(path.join(root_path, 'deltas.csv'), header=False)
        val_results.expected_profile.to_csv(path.join(root_path, 'expected_profile.csv'))
        val_results.intrinsic_profile.to_csv(path.join(root_path, 'intrinsic_profile.csv'))
        val_results.trigger_prices.to_csv(path.join(root_path, 'trigger_prices.csv'))

    @staticmethod
    def _load_valuation_results_csvs(root_path: str):
        deltas = pd.read_csv(path.join(root_path, 'deltas.csv'), header=None, index_col=0, parse_dates=True).iloc[:,0]
        deltas.index = deltas.index.to_period('D')
        expected_profile = TestMultiFactorValue._load_data_frame_from_csv(path.join(root_path, 'expected_profile.csv'))
        intrinsic_profile = TestMultiFactorValue._load_data_frame_from_csv(path.join(root_path, 'intrinsic_profile.csv'))
        trigger_prices = TestMultiFactorValue._load_data_frame_from_csv(path.join(root_path, 'trigger_prices.csv'))
        return deltas, expected_profile, intrinsic_profile, trigger_prices

    @staticmethod
    def _load_data_frame_from_csv(file_path):
        df = pd.read_csv(file_path, index_col=0, parse_dates=True)
        df.index = df.index.to_period('D')
        return df

if __name__ == '__main__':
    unittest.main()
