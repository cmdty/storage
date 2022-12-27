#region License
// Copyright (c) 2020 Jake Fowler
//
// Permission is hereby granted, free of charge, to any person 
// obtaining a copy of this software and associated documentation 
// files (the "Software"), to deal in the Software without 
// restriction, including without limitation the rights to use, 
// copy, modify, merge, publish, distribute, sublicense, and/or sell 
// copies of the Software, and to permit persons to whom the 
// Software is furnished to do so, subject to the following 
// conditions:
//
// The above copyright notice and this permission notice shall be 
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, 
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES 
// OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND 
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT 
// HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, 
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING 
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR 
// OTHER DEALINGS IN THE SOFTWARE.
#endregion

using Cmdty.Core.Common;
using Cmdty.Core.Simulation;
using Cmdty.TimePeriodValueTypes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Cmdty.Storage.PythonHelpers
{
    // TODO move out of PythonHelpers namespace
    internal class SpotSimResultsFromPanels<T> : ISpotSimResults<T>
        where T : ITimePeriod<T>
    {
        public Panel<T, double> SpotPriceSims { get; }
        private readonly Panel<T, double>[] _factorSims;

        public IReadOnlyList<Panel<T, double>> FactorSims => _factorSims;

        public SpotSimResultsFromPanels(Panel<T, double> spotPriceSims, 
                IEnumerable<Panel<T, double>> factorSims)
        {
            if (spotPriceSims is null)
                throw new ArgumentNullException(nameof(spotPriceSims));
            if (factorSims is null)
                throw new ArgumentNullException(nameof(factorSims));
            if (spotPriceSims.NumCols == 0)
                throw new ArgumentException($"{nameof(spotPriceSims)}.{nameof(spotPriceSims.NumCols)} (number of simulations), " +
                    $"cannot equal zero.");
            _factorSims = factorSims.ToArray();

            for (int i = 0; i < _factorSims.Length; i++)
            {
                Panel<T, double> factorSim = _factorSims[i];
                // Check number of simulation is consistent
                if (spotPriceSims.NumCols != factorSim.NumCols)
                    throw new ArgumentException($"Inconsistent number of columns (simulations) between " +
                        $"{nameof(spotPriceSims)} and item {i} of {nameof(factorSims)}.");
                // Check simulated periods is consistent
                if (spotPriceSims.NumRows != factorSim.NumRows)
                    throw new ArgumentException($"Inconsistent number of rows (time periods) between " +
                        $"{nameof(spotPriceSims)} and item {i} of {nameof(factorSims)}.");
                foreach ((T SpotKey, T FactorKey) in spotPriceSims.RowKeys.Zip(factorSim.RowKeys,
                                (spotKey, factorKey) => (spotKey, factorKey)))
                    if (!SpotKey.Equals(FactorKey))
                        throw new ArgumentException($"Inconsistent time period row keys between " +
                            $"{nameof(spotPriceSims)} and item {i} of {nameof(factorSims)}.");
            }
            SpotPriceSims = spotPriceSims;
        }

        // TODO not NotImplementedException in below 2 methods shows this is a leaky abstraction. Look to refactor
        public double[] SpotPrices => throw new NotImplementedException();

        public double[] MarkovFactors => throw new NotImplementedException();

        public int NumSteps => SpotPriceSims.NumRows;

        public int NumSims => SpotPriceSims.NumCols;

        public int NumFactors => _factorSims.Length;

        public IReadOnlyList<T> SimulatedPeriods => SpotPriceSims.RowKeys.ToArray();

        public ReadOnlyMemory<double> MarkovFactorsForPeriod(T period, int factorIndex)
        {
            CheckFactorIndex(factorIndex);
            return _factorSims[factorIndex].GetRowMemory(period);
        }

        public ReadOnlyMemory<double> MarkovFactorsForStepIndex(int stepIndex, int factorIndex)
        {
            CheckFactorIndex(factorIndex);
            return _factorSims[factorIndex].GetRowMemory(stepIndex);
        }

        public ReadOnlyMemory<double> SpotPricesForPeriod(T period)
        {
            return SpotPriceSims.GetRowMemory(period);
        }

        public ReadOnlyMemory<double> SpotPricesForStepIndex(int stepIndex)
        {
            return SpotPriceSims.GetRowMemory(stepIndex);
        }

        private void CheckFactorIndex(int factorIndex)
        {
            if (factorIndex < 0 || factorIndex >= NumFactors)
                throw new IndexOutOfRangeException($"Factor index should be in interval [0, " + 
                    (NumFactors - 1) + "].");
        }
    }
}
