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
import numpy as np
from datetime import date
from cmdty_storage import MultiFactorSpotSim


# README: PROPER UNIT TESTS ARE IN THE C# CODE.
# TODO regression with antithetic
class TestSpotPriceSim(unittest.TestCase):
    def test_regression(self):
        factors = [  # Tuples where 1st element is factor mean-reversion, 2nd element is factor vol curve
            (0.0, {date(2020, 8, 1): 0.35,
                   '2021-01-15': 0.29,  # Can use string to specify forward delivery date
                   date(2021, 7, 30): 0.32}),
            # factor vol can also be specified as a pandas Series
            (2.5, pd.Series(data=[0.15, 0.18, 0.21],
                            index=pd.PeriodIndex(data=['2020-08-01', '2021-01-15', '2021-07-30'], freq='D'))),
            (16.2, {date(2020, 8, 1): 0.95,
                    '2021-01-15': 0.92,
                    date(2021, 7, 30): 0.89}),
        ]

        factor_corrs = np.array([
            [1.0, 0.6, 0.3],
            [0.6, 1.0, 0.4],
            [0.3, 0.4, 1.0]
        ])

        # Like with factor vol, the fwd_curve can be a pandas Series object
        fwd_curve = {
            '2020-08-01': 56.85,
            pd.Period('2021-01-15', freq='D'): 59.08,
            date(2021, 7, 30): 62.453
        }
        current_date = date(2020, 7, 27)
        # Demonstrates different ways tp specify spot periods to simulate. Easier in practice to just use
        # keys of fwd_curve
        spot_periods_to_sim = [pd.Period('2020-08-01'), '2021-01-15', date(2021, 7, 30)]

        random_seed = 12
        spot_simulator = MultiFactorSpotSim('D', factors, factor_corrs, current_date, fwd_curve,
                                               spot_periods_to_sim, random_seed)
        num_sims = 4
        sim_spot_prices = spot_simulator.simulate(num_sims)
        self.assertEqual(3, len(sim_spot_prices))

        sim1 = sim_spot_prices[0]
        self.assertEqual(52.59976397688973, sim1['2020-08-01'])
        self.assertEqual(57.559631642935514, sim1['2021-01-15'])
        self.assertEqual(89.40526992772634, sim1['2021-07-30'])

        sim2 = sim_spot_prices[1]
        self.assertEqual(46.1206448628463, sim2['2020-08-01'])
        self.assertEqual(72.0381089486175, sim2['2021-01-15'])
        self.assertEqual(85.18869803117379, sim2['2021-07-30'])

        sim3 = sim_spot_prices[2]
        self.assertEqual(58.15838580682589, sim3['2020-08-01'])
        self.assertEqual(82.49607173562342, sim3['2021-01-15'])
        self.assertEqual(138.68587285875978, sim3['2021-07-30'])

        sim4 = sim_spot_prices[3]
        self.assertEqual(65.500441945042979, sim4['2020-08-01'])
        self.assertEqual(42.812676607997183, sim4['2021-01-15'])
        self.assertEqual(76.586790647813046, sim4['2021-07-30'])


if __name__ == '__main__':
    unittest.main()
