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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cmdty.Core.Simulation;
using Cmdty.Core.Simulation.MultiFactor;
using Cmdty.TimePeriodValueTypes;
using Cmdty.TimeSeries;
using JetBrains.Annotations;

namespace Cmdty.Storage
{
    public sealed class LsmcValuationParameters<T> 
        where T : ITimePeriod<T>
    {

        public T CurrentPeriod { get; }
        public double Inventory { get; }
        public TimeSeries<T, double> ForwardCurve { get; }
        public ICmdtyStorage<T> Storage { get; }
        public Func<T, Day> SettleDateRule { get; }
        public Func<Day, Day, double> DiscountFactors { get; }
        public IDoubleStateSpaceGridCalc GridCalc { get; }
        public double NumericalTolerance { get; }
        public Func<ISpotSimResults<T>> RegressionSpotSimsGenerator { get; }
        public Func<ISpotSimResults<T>> ValuationSpotSimsGenerator { get; }
        public IEnumerable<BasisFunction> BasisFunctions { get; }
        public CancellationToken CancellationToken { get; }
        public Action<double> OnProgressUpdate { get; }
        public bool DiscountDeltas { get; }
        public int ExtraDecisions { get; }

        private LsmcValuationParameters(T currentPeriod, double inventory, TimeSeries<T, double> forwardCurve, 
            ICmdtyStorage<T> storage, Func<T, Day> settleDateRule, Func<Day, Day, double> discountFactors, IDoubleStateSpaceGridCalc gridCalc, 
            double numericalTolerance, SimulateSpotPrice regressionSpotSims, SimulateSpotPrice valuationSpotSims, IEnumerable<BasisFunction> basisFunctions, 
            CancellationToken cancellationToken, bool discountDeltas, int extraDecisions, Action<double> onProgressUpdate = null)
        {
            CurrentPeriod = currentPeriod;
            Inventory = inventory;
            ForwardCurve = forwardCurve;
            Storage = storage;
            SettleDateRule = settleDateRule;
            DiscountFactors = discountFactors;
            GridCalc = gridCalc;
            NumericalTolerance = numericalTolerance;
            RegressionSpotSimsGenerator = () => regressionSpotSims(CurrentPeriod, storage.StartPeriod, storage.EndPeriod, forwardCurve);
            ValuationSpotSimsGenerator = () => valuationSpotSims(CurrentPeriod, storage.StartPeriod, storage.EndPeriod, forwardCurve);
            BasisFunctions = basisFunctions.ToArray();
            CancellationToken = cancellationToken;
            DiscountDeltas = discountDeltas;
            ExtraDecisions = extraDecisions;
            OnProgressUpdate = onProgressUpdate;
        }

        public delegate ISpotSimResults<T> SimulateSpotPrice(T currentPeriod, T storageStart, T storageEnd, 
            TimeSeries<T, double> forwardCurve);

        public sealed class Builder
        {
            // ReSharper disable once StaticMemberInGenericType
            public static double DefaultNumericalTolerance { get; } = 1E-10;
            public double? Inventory { get; set; }
            public TimeSeries<T, double> ForwardCurve { get; set; }
            public ICmdtyStorage<T> Storage { get; set; }
            public Func<T, Day> SettleDateRule { get; set; }
            public Func<Day, Day, double> DiscountFactors { get; set; }
            public IDoubleStateSpaceGridCalc GridCalc { get; set; }
            public double NumericalTolerance { get; set; }
            public SimulateSpotPrice RegressionSpotSimsGenerator { get; set; }
            public SimulateSpotPrice ValuationSpotSimsGenerator { get; set; }
            public IEnumerable<BasisFunction> BasisFunctions { get; set; }
            public CancellationToken CancellationToken { get; set; }
            public Action<double> OnProgressUpdate { get; set; }
            public int ExtraDecisions { get; set; }

            public bool DiscountDeltas { get; set; }
            private T _currentPeriod;
            private bool _currentPeriodSet;

            public T CurrentPeriod
            {
                get => _currentPeriod;
                set
                {
                    _currentPeriodSet = true;
                    _currentPeriod = value;
                }
            }
            
            public Builder()
            {
                CancellationToken = CancellationToken.None; // TODO see if this can be removed
                NumericalTolerance = DefaultNumericalTolerance;
            }

            public LsmcValuationParameters<T> Build()
            {
                if (!_currentPeriodSet)
                    throw new InvalidOperationException("CurrentPeriod has not been set.");
                ThrowIfNotSet(Inventory, nameof(Inventory));
                ThrowIfNotSet(ForwardCurve, nameof(ForwardCurve));
                ThrowIfNotSet(Storage, nameof(Storage));
                ThrowIfNotSet(SettleDateRule, nameof(SettleDateRule));
                ThrowIfNotSet(DiscountFactors, nameof(DiscountFactors));
                ThrowIfNotSet(GridCalc, nameof(GridCalc));
                ThrowIfNotSet(RegressionSpotSimsGenerator, nameof(RegressionSpotSimsGenerator));
                ThrowIfNotSet(ValuationSpotSimsGenerator, nameof(ValuationSpotSimsGenerator));
                ThrowIfNotSet(BasisFunctions, nameof(BasisFunctions));
                if (ExtraDecisions < 0)
                    throw new InvalidOperationException(nameof(ExtraDecisions) + " must be non-negative.");

                // ReSharper disable once PossibleInvalidOperationException
                return new LsmcValuationParameters<T>(CurrentPeriod, Inventory.Value, ForwardCurve, Storage, SettleDateRule, 
                    DiscountFactors, GridCalc, NumericalTolerance, RegressionSpotSimsGenerator, ValuationSpotSimsGenerator, 
                    BasisFunctions, CancellationToken, DiscountDeltas, ExtraDecisions, OnProgressUpdate);
            }

            // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
            private static void ThrowIfNotSet<TField>(TField field, string fieldName)
            {
                if (field is null)
                    throw new InvalidOperationException(fieldName + " has not been set.");
            }

            public Builder SimulateWithMultiFactorModel(
                IStandardNormalGenerator regressionSimNormalGenerator, IStandardNormalGenerator valuationSimNormalGenerator, 
                [NotNull] MultiFactorParameters<T> modelParameters, int numSims)
            {
                if (modelParameters == null) throw new ArgumentNullException(nameof(modelParameters));
                RegressionSpotSimsGenerator = CreateSimulationSpotPrice(regressionSimNormalGenerator, modelParameters, numSims);
                ValuationSpotSimsGenerator = CreateSimulationSpotPrice(valuationSimNormalGenerator, modelParameters, numSims);
                return this;
            }

            private static SimulateSpotPrice CreateSimulationSpotPrice(IStandardNormalGenerator randomNumberGenerator, 
                [NotNull] MultiFactorParameters<T> modelParameters, int numSims)
            {
                return (currentPeriod, storageStart, storageEnd, forwardCurve) =>
                {
                    if (currentPeriod.Equals(storageEnd))
                    {
                        // TODO think of more elegant way of doing this
                        return new MultiFactorSpotSimResults<T>(new double[0],
                            new double[0], new T[0], 0, numSims, modelParameters.NumFactors);
                    }

                    DateTime currentDate = currentPeriod.Start; // TODO IMPORTANT, this needs to change;
                    T simStart = new[] { currentPeriod.Offset(1), storageStart }.Max();
                    var simulatedPeriods = simStart.EnumerateTo(storageEnd);
                    var simulator = new MultiFactorSpotPriceSimulator<T>(modelParameters, currentDate,
                        forwardCurve, simulatedPeriods, TimeFunctions.Act365, randomNumberGenerator);
                    return simulator.Simulate(numSims);
                };
            }

            public Builder SimulateWithMultiFactorModelAndMersenneTwister(
                                        MultiFactorParameters<T> modelParameters, int numSims, int? simSeed = null, 
                                        int? valuationSimSeed = null)
            {
                MersenneTwisterGenerator regressionSimNormalGenerator = simSeed == null ? new MersenneTwisterGenerator(true) :
                            new MersenneTwisterGenerator(simSeed.Value, true);
                // If valuationSimSeed is null then use the same random number generator as regression, which will continue the sequence
                MersenneTwisterGenerator valuationSimNormalGenerator = valuationSimSeed == null ? regressionSimNormalGenerator : 
                    new MersenneTwisterGenerator(valuationSimSeed.Value, true);

                return SimulateWithMultiFactorModel(regressionSimNormalGenerator, valuationSimNormalGenerator, modelParameters, numSims);
            }

            public Builder UseSpotSimResults(ISpotSimResults<T> regressionSpotSim, ISpotSimResults<T> valuationSpotSim)
            {
                if (regressionSpotSim is null)
                    throw new ArgumentNullException(nameof(regressionSpotSim));
                if (valuationSpotSim is null)
                    throw new ArgumentNullException(nameof(valuationSpotSim));

                RegressionSpotSimsGenerator = (T currentPeriod, T storageStart, T storageEnd, TimeSeries<T, double> forwardCurve) =>
                {
                    CheckSpotSim(regressionSpotSim, currentPeriod, storageStart, storageEnd, nameof(regressionSpotSim));
                    return regressionSpotSim;
                };
                ValuationSpotSimsGenerator = (T currentPeriod, T storageStart, T storageEnd, TimeSeries<T, double> forwardCurve) =>
                {
                    CheckSpotSim(valuationSpotSim, currentPeriod, storageStart, storageEnd, nameof(valuationSpotSim));
                    return valuationSpotSim;
                };
                return this;
            }

            // This can all be removed when ISpotSimResults<T> is refactored or removed
            private void CheckSpotSim(ISpotSimResults<T> spotSim, T currentPeriod, T storageStart, 
                T storageEnd, string spotSimArgument)
            {
                if (currentPeriod.CompareTo(storageEnd) >= 0)
                    return; // Don't need to check as simulations not needed

                var simulatedPeriods = new HashSet<T>(spotSim.SimulatedPeriods);
                T simStart = new[] { currentPeriod.Offset(1), storageStart }.Max();

                foreach (T simulatedPeriod in simStart.EnumerateTo(storageEnd))
                    if (!simulatedPeriods.Contains(simulatedPeriod))
                        throw new ArgumentException(spotSimArgument + $" does not contain simulations for period {simulatedPeriod}.");
            }

            public Builder Clone()
            {
                return new Builder
                {
                    _currentPeriod = this._currentPeriod,
                    _currentPeriodSet = this._currentPeriodSet,
                    BasisFunctions = this.BasisFunctions.ToList(),
                    CancellationToken = this.CancellationToken,
                    DiscountFactors = this.DiscountFactors,
                    ForwardCurve = this.ForwardCurve,
                    GridCalc = this.GridCalc,
                    NumericalTolerance = this.NumericalTolerance,
                    OnProgressUpdate = this.OnProgressUpdate,
                    Inventory = this.Inventory,
                    SettleDateRule = this.SettleDateRule,
                    RegressionSpotSimsGenerator = this.RegressionSpotSimsGenerator,
                    ValuationSpotSimsGenerator = this.ValuationSpotSimsGenerator,
                    Storage = this.Storage,
                    ExtraDecisions = this.ExtraDecisions
                };
            }

        }

    }
}