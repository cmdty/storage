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

import unittest
import pandas as pd
from cmdty_storage import MultiFactorModel
import numpy as np
import itertools
from datetime import date


class TestMultiFactorModel(unittest.TestCase):
    _short_plus_long_indices = pd.period_range(start='2020-09-01', periods=25, freq='D') \
        .append(pd.period_range(start='2030-09-01', periods=25, freq='D'))
    _1f_0_mr_model = MultiFactorModel('D', [(0.0, {'2020-09-01': 0.36, '2020-10-01': 0.29, '2020-11-01': 0.23})])
    _1f_pos_mr_model = MultiFactorModel('D', [(2.5, pd.Series(data=np.linspace(0.65, 0.38, num=50),
                                                                 index=_short_plus_long_indices))])
    _2f_canonical_model = MultiFactorModel('D',
                                              factors=[(0.0, pd.Series(data=np.linspace(0.53, 0.487, num=50),
                                                                       index=_short_plus_long_indices)),
                                                       (2.5, pd.Series(data=np.linspace(1.45, 1.065, num=50),
                                                                       index=_short_plus_long_indices))],
                                              factor_corrs=0.87)  # If only 2 factors can supply a float for factor_corrs rather than a matrix

    def test_single_non_mean_reverting_factor_implied_vol_equals_factor_vol(self):
        fwd_contract = '2020-09-01'
        implied_vol = self._1f_0_mr_model.integrated_vol(date(2020, 8, 5), date(2020, 8, 30), '2020-09-01')
        factor_vol = self._1f_0_mr_model._factors[0][1][fwd_contract]
        self.assertEqual(factor_vol, implied_vol)

    def test_single_non_mean_reverting_factor_correlations_equal_one(self):
        self._assert_cross_correlations_all_one(date(2020, 8, 1), date(2020, 9, 1), self._1f_0_mr_model)

    def test_single_mean_reverting_factor_correlations_equal_one(self):
        self._assert_cross_correlations_all_one(date(2020, 5, 1), date(2020, 9, 1), self._1f_pos_mr_model)

    def _assert_cross_correlations_all_one(self, obs_start, obs_end, model: MultiFactorModel):
        fwd_points = model._factors[0][1].keys()
        for fwd_point_1, fwd_point_2 in itertools.product(fwd_points, fwd_points):
            if fwd_point_1 != fwd_point_2:
                corr = model.integrated_corr(obs_start, obs_end, fwd_point_1, fwd_point_2)
                self.assertAlmostEqual(1.0, corr, places=14)

    def test_single_mean_reverting_factor_variance_far_in_future_equals_zero(self):
        variance = self._1f_pos_mr_model.integrated_variance('2020-08-05', '2020-09-01', fwd_contract='2030-09-15')
        self.assertAlmostEqual(0.0, variance, places=14)

    def test_2f_canonical_vol_far_in_future_equal_non_mr_vol(self):
        fwd_contract = '2030-09-15'
        implied_vol = self._2f_canonical_model.integrated_vol('2020-08-05', '2021-08-05', fwd_contract)
        non_mr_factor_vol = self._2f_canonical_model._factors[0][1][fwd_contract]
        self.assertAlmostEqual(non_mr_factor_vol, implied_vol, places=10)

    def test_diff_corr_types_give_same_results(self):
        factors = [(0.0, pd.Series(data=np.linspace(0.53, 0.487, num=50),
                                   index=self._short_plus_long_indices)),
                   (2.5, pd.Series(data=np.linspace(1.45, 1.065, num=50),
                                   index=self._short_plus_long_indices))]
        two_f_model_float_corr = MultiFactorModel('D', factors=factors, factor_corrs=0.0)
        two_f_model_int_corr = MultiFactorModel('D', factors=factors, factor_corrs=0)
        two_f_model_float_array_corr = MultiFactorModel('D', factors=factors, factor_corrs=np.array([[1.0, 0.0],
                                                                                                        [0.0, 1.0]]))
        two_f_model_int_array_corr = MultiFactorModel('D', factors=factors, factor_corrs=np.array([[1, 0],
                                                                                                      [0, 1]]))

        two_f_model_float_corr_covar = two_f_model_float_corr.integrated_covar(date(2020, 8, 5),
                                                                               date(2020, 8, 30), '2020-09-01',
                                                                               '2020-09-20')
        two_f_model_float_array_corr_covar = two_f_model_float_array_corr.integrated_covar(date(2020, 8, 5),
                                                                                           date(2020, 8, 30),
                                                                                           '2020-09-01', '2020-09-20')
        two_f_model_int_corr_covar = two_f_model_int_corr.integrated_covar(date(2020, 8, 5),
                                                                           date(2020, 8, 30), '2020-09-01',
                                                                           '2020-09-20')
        two_f_model_int_array_corr_covar = two_f_model_int_array_corr.integrated_covar(date(2020, 8, 5),
                                                                                       date(2020, 8, 30), '2020-09-01',
                                                                                       '2020-09-20')
        self.assertEqual(two_f_model_float_corr_covar, two_f_model_float_array_corr_covar)
        self.assertEqual(two_f_model_float_corr_covar, two_f_model_int_corr_covar)
        self.assertEqual(two_f_model_float_corr_covar, two_f_model_int_array_corr_covar)
        # TODO test MultiFactorModel.for_3_factor_seasonal


if __name__ == '__main__':
    unittest.main()
