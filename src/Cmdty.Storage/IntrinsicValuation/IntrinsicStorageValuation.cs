#region License
// Copyright (c) 2019 Jake Fowler
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
using Cmdty.TimePeriodValueTypes;
using Cmdty.TimeSeries;
using JetBrains.Annotations;

namespace Cmdty.Storage
{
    public sealed class IntrinsicStorageValuation<T> : IIntrinsicAddStartingInventory<T>, IIntrinsicAddCurrentPeriod<T>, IIntrinsicAddForwardCurve<T>, 
            IIntrinsicAddCmdtySettlementRule<T>, IIntrinsicAddDiscountFactorFunc<T>, IIntrinsicAddInventoryGridCalculation<T>, IIntrinsicAddNumericalTolerance<T>, IIntrinsicAddInterpolator<T>, IIntrinsicCalculate<T>
        where T : ITimePeriod<T>
    {
        private readonly ICmdtyStorage<T> _storage;
        private double _startingInventory;
        private T _currentPeriod;
        private TimeSeries<T, double> _forwardCurve;
        private Func<T, Day> _settleDateRule;
        private Func<Day, Day, double> _discountFactors;
        private Func<ICmdtyStorage<T>, IDoubleStateSpaceGridCalc> _gridCalcFactory;
        private IInterpolatorFactory _interpolatorFactory;
        private double _numericalTolerance;

        private IntrinsicStorageValuation([NotNull] ICmdtyStorage<T> storage)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        public static IIntrinsicAddStartingInventory<T> ForStorage([NotNull] ICmdtyStorage<T> storage)
        {
            return new IntrinsicStorageValuation<T>(storage);
        }

        IIntrinsicAddCurrentPeriod<T> IIntrinsicAddStartingInventory<T>.WithStartingInventory(double inventory)
        {
            if (inventory < 0)
                throw new ArgumentException("Inventory cannot be negative", nameof(inventory));
            _startingInventory = inventory;
            return this;
        }

        IIntrinsicAddForwardCurve<T> IIntrinsicAddCurrentPeriod<T>.ForCurrentPeriod([NotNull] T currentPeriod)
        {
            if (currentPeriod == null)
                throw new ArgumentNullException(nameof(currentPeriod));
            _currentPeriod = currentPeriod;
            return this;
        }

        IIntrinsicAddCmdtySettlementRule<T> IIntrinsicAddForwardCurve<T>.WithForwardCurve([NotNull] TimeSeries<T, double> forwardCurve)
        {
            _forwardCurve = forwardCurve ?? throw new ArgumentNullException(nameof(forwardCurve));
            return this;
        }

        public IIntrinsicAddDiscountFactorFunc<T> WithCmdtySettlementRule([NotNull] Func<T, Day> settleDateRule)
        {
            _settleDateRule = settleDateRule ?? throw new ArgumentNullException(nameof(settleDateRule));
            return this;
        }

        IIntrinsicAddInventoryGridCalculation<T> IIntrinsicAddDiscountFactorFunc<T>.WithDiscountFactorFunc([NotNull] Func<Day, Day, double> discountFactors)
        {
            _discountFactors = discountFactors ?? throw new ArgumentNullException(nameof(discountFactors));
            return this;
        }

        IIntrinsicAddInterpolator<T> IIntrinsicAddInventoryGridCalculation<T>
                    .WithStateSpaceGridCalculation([NotNull] Func<ICmdtyStorage<T>, IDoubleStateSpaceGridCalc> gridCalcFactory)
        {
            _gridCalcFactory = gridCalcFactory ?? throw new ArgumentNullException(nameof(gridCalcFactory));
            return this;
        }

        IIntrinsicAddNumericalTolerance<T> IIntrinsicAddInterpolator<T>.WithInterpolatorFactory([NotNull] IInterpolatorFactory interpolatorFactory)
        {
            _interpolatorFactory = interpolatorFactory ?? throw new ArgumentNullException(nameof(interpolatorFactory));
            return this;
        }

        IIntrinsicCalculate<T> IIntrinsicAddNumericalTolerance<T>.WithNumericalTolerance(double numericalTolerance)
        {
            if (numericalTolerance <= 0)
                throw new ArgumentException("Numerical tolerance must be positive.", nameof(numericalTolerance));
            _numericalTolerance = numericalTolerance;
            return this;
        }

        IntrinsicStorageValuationResults<T> IIntrinsicCalculate<T>.Calculate()
        {
            return Calculate(_currentPeriod, _startingInventory, _forwardCurve, _storage, _settleDateRule, _discountFactors,
                    _gridCalcFactory, _interpolatorFactory, _numericalTolerance);
        }

        private static IntrinsicStorageValuationResults<T> Calculate(T currentPeriod, double startingInventory,
                TimeSeries<T, double> forwardCurve, ICmdtyStorage<T> storage, Func<T, Day> settleDateRule,
                Func<Day, Day, double> discountFactors, Func<ICmdtyStorage<T>, IDoubleStateSpaceGridCalc> gridCalcFactory,
                IInterpolatorFactory interpolatorFactory, double numericalTolerance)
        {
            if (startingInventory < 0)
                throw new ArgumentException("Inventory cannot be negative.", nameof(startingInventory));

            if (currentPeriod.CompareTo(storage.EndPeriod) > 0)
                return new IntrinsicStorageValuationResults<T>(0.0, TimeSeries<T, StorageProfile>.Empty);

            if (currentPeriod.Equals(storage.EndPeriod))
            {
                if (storage.MustBeEmptyAtEnd)
                {
                    if (startingInventory > 0) // TODO allow some tolerance for floating point numerical error?
                        throw new InventoryConstraintsCannotBeFulfilledException("Storage must be empty at end, but inventory is greater than zero.");
                    return new IntrinsicStorageValuationResults<T>(0.0, TimeSeries<T, StorageProfile>.Empty);
                }

                double terminalMinInventory = storage.MinInventory(storage.EndPeriod);
                double terminalMaxInventory = storage.MaxInventory(storage.EndPeriod);

                if (startingInventory < terminalMinInventory)
                    throw new InventoryConstraintsCannotBeFulfilledException("Current inventory is lower than the minimum allowed in the end period.");

                if (startingInventory > terminalMaxInventory)
                    throw new InventoryConstraintsCannotBeFulfilledException("Current inventory is greater than the maximum allowed in the end period.");

                double cmdtyPrice = forwardCurve[storage.EndPeriod];
                double npv = storage.TerminalStorageNpv(cmdtyPrice, startingInventory);
                return new IntrinsicStorageValuationResults<T>(npv, TimeSeries<T, StorageProfile>.Empty);
            }

            TimeSeries<T, InventoryRange> inventorySpace = StorageHelper.CalculateInventorySpace(storage, startingInventory, currentPeriod);

            // TODO think of method to put in TimeSeries class to perform the validation check below in one line
            if (forwardCurve.IsEmpty)
                throw new ArgumentException("Forward curve cannot be empty.", nameof(forwardCurve));

            if (forwardCurve.Start.CompareTo(inventorySpace.Start) > 0)
                throw new ArgumentException("Forward curve starts too late.", nameof(forwardCurve));

            if (forwardCurve.End.CompareTo(inventorySpace.End) < 0)
                throw new ArgumentException("Forward curve does not extend until storage end period.", nameof(forwardCurve));

            // Calculate discount factor function
            Day dayToDiscountTo = currentPeriod.First<Day>(); // TODO IMPORTANT, this needs to change
            
            // Memoize the discount factor
            var discountFactorCache = new Dictionary<Day, double>(); // TODO do this in more elegant way and share with Tree calc
            double DiscountToCurrentDay(Day cashFlowDate)
            {
                if (!discountFactorCache.TryGetValue(cashFlowDate, out double discountFactor))
                {
                    discountFactor = discountFactors(dayToDiscountTo, cashFlowDate);
                    discountFactorCache[cashFlowDate] = discountFactor;
                }
                return discountFactor;
            }

            // Perform backward induction
            var storageValueByInventory = new Func<double, double>[inventorySpace.Count];

            double cmdtyPriceAtEnd = forwardCurve[storage.EndPeriod];
            storageValueByInventory[inventorySpace.Count - 1] = 
                finalInventory => storage.TerminalStorageNpv(cmdtyPriceAtEnd, finalInventory) ;

            int backCounter = inventorySpace.Count - 2;
            IDoubleStateSpaceGridCalc gridCalc = gridCalcFactory(storage);

            foreach (T periodLoop in inventorySpace.Indices.Reverse().Skip(1))
            {
                (double inventorySpaceMin, double inventorySpaceMax) = inventorySpace[periodLoop];
                double[] inventorySpaceGrid = gridCalc.GetGridPoints(inventorySpaceMin, inventorySpaceMax)
                                                        .ToArray();
                var storageValuesGrid = new double[inventorySpaceGrid.Length];

                double cmdtyPrice = forwardCurve[periodLoop];
                Func<double, double> continuationValueByInventory = storageValueByInventory[backCounter + 1];

                Day cmdtySettlementDate = settleDateRule(periodLoop);
                double discountFactorFromCmdtySettlement = DiscountToCurrentDay(cmdtySettlementDate);

                (double nextStepInventorySpaceMin, double nextStepInventorySpaceMax) = inventorySpace[periodLoop.Offset(1)];
                for (int i = 0; i < inventorySpaceGrid.Length; i++)
                {
                    double inventory = inventorySpaceGrid[i];
                    storageValuesGrid[i] = OptimalDecisionAndValue(storage, periodLoop, inventory, nextStepInventorySpaceMin, 
                                                nextStepInventorySpaceMax, cmdtyPrice, continuationValueByInventory,
                                                discountFactorFromCmdtySettlement, DiscountToCurrentDay, numericalTolerance).StorageNpv;
                }

                storageValueByInventory[backCounter] =
                    interpolatorFactory.CreateInterpolator(inventorySpaceGrid, storageValuesGrid);
                backCounter--;
            }

            // Loop forward from start inventory choosing optimal decisions
            int numStorageProfiles = inventorySpace.Count + 1;
            var storageProfiles = new StorageProfile[numStorageProfiles];
            var periods = new T[numStorageProfiles];

            double inventoryLoop = startingInventory;
            T startActiveStorage = inventorySpace.Start.Offset(-1);
            for (int i = 0; i < numStorageProfiles; i++)
            {
                T periodLoop = startActiveStorage.Offset(i);
                double spotPrice = forwardCurve[periodLoop];
                StorageProfile storageProfile;
                if (periodLoop.Equals(storage.EndPeriod))
                {
                    double endPeriodNpv = storage.MustBeEmptyAtEnd ? 0.0 : storage.TerminalStorageNpv(spotPrice, inventoryLoop);
                    storageProfile = new StorageProfile(inventoryLoop, 0.0, 0.0, 0.0, endPeriodNpv);
                }
                else
                {
                    Day cmdtySettlementDate = settleDateRule(periodLoop);
                    double discountFactorFromCmdtySettlement = DiscountToCurrentDay(cmdtySettlementDate);

                    Func<double, double> continuationValueByInventory = storageValueByInventory[i];
                    (double nextStepInventorySpaceMin, double nextStepInventorySpaceMax) = inventorySpace[periodLoop.Offset(1)];
                    (double _, double optimalInjectWithdraw, double cmdtyConsumedOnAction, double inventoryLoss, double optimalPeriodPv) =
                        OptimalDecisionAndValue(storage, periodLoop, inventoryLoop, nextStepInventorySpaceMin,
                            nextStepInventorySpaceMax, spotPrice, continuationValueByInventory, discountFactorFromCmdtySettlement,
                            DiscountToCurrentDay, numericalTolerance);

                    inventoryLoop += optimalInjectWithdraw - inventoryLoss;

                    double netVolume = -optimalInjectWithdraw - cmdtyConsumedOnAction;
                    storageProfile = new StorageProfile(inventoryLoop, optimalInjectWithdraw, cmdtyConsumedOnAction, inventoryLoss, optimalPeriodPv);

                }
                storageProfiles[i] = storageProfile;
                periods[i] = periodLoop;
            }

            double storageNpv = storageProfiles.Sum(profile => profile.PeriodPv);

            return new IntrinsicStorageValuationResults<T>(storageNpv, new TimeSeries<T, StorageProfile>(periods, storageProfiles));
        }

        private static (double StorageNpv, double OptimalInjectWithdraw, double CmdtyConsumedOnAction, double InventoryLoss, double PeriodPv) 
            OptimalDecisionAndValue(ICmdtyStorage<T> storage, T period, double inventory,
            double nextStepInventorySpaceMin, double nextStepInventorySpaceMax, double cmdtyPrice,
            Func<double, double> continuationValueByInventory, double discountFactorFromCmdtySettlement, 
            Func<Day, double> discountFactors, double numericalTolerance)
        {
            InjectWithdrawRange injectWithdrawRange = storage.GetInjectWithdrawRange(period, inventory);
            double inventoryLoss = storage.CmdtyInventoryPercentLoss(period) * inventory;
            double[] decisionSet = StorageHelper.CalculateBangBangDecisionSet(injectWithdrawRange, inventory, inventoryLoss,
                                                    nextStepInventorySpaceMin, nextStepInventorySpaceMax, numericalTolerance);
            var valuesForDecision = new double[decisionSet.Length];
            var cmdtyConsumedForDecision = new double[decisionSet.Length];
            var periodPvForDecision = new double[decisionSet.Length];

            for (var j = 0; j < decisionSet.Length; j++)
            {
                double decisionInjectWithdraw = decisionSet[j];
                (valuesForDecision[j], cmdtyConsumedForDecision[j], periodPvForDecision[j]) = StorageValueForDecision(storage, period, inventory, inventoryLoss,
                    decisionInjectWithdraw, cmdtyPrice, continuationValueByInventory, discountFactorFromCmdtySettlement, discountFactors);
            }

            (double storageNpv, int indexOfOptimalDecision) = StorageHelper.MaxValueAndIndex(valuesForDecision);

            return (StorageNpv: storageNpv, OptimalInjectWithdraw: decisionSet[indexOfOptimalDecision], 
                    CmdtyConsumedOnAction: cmdtyConsumedForDecision[indexOfOptimalDecision], 
                    InventoryLoss: inventoryLoss, PeriodPv: periodPvForDecision[indexOfOptimalDecision]);
        }


        private static (double StorageNpv, double CmdtyConsumed, double PeriodPv) StorageValueForDecision(
                        ICmdtyStorage<T> storage, T period, double inventory, double inventoryLoss,
                        double injectWithdrawVolume, double cmdtyPrice, Func<double, double> continuationValueInterpolated, 
                        double discountFactorFromCmdtySettlement, Func<Day, double> discountFactors)
        {
            double inventoryAfterDecision = inventory + injectWithdrawVolume - inventoryLoss;
            double continuationFutureNpv = continuationValueInterpolated(inventoryAfterDecision);
            // TODO use StorageHelper.StorageImmediateNpvForDecision

            double injectWithdrawNpv = -injectWithdrawVolume * cmdtyPrice * discountFactorFromCmdtySettlement;

            IReadOnlyList<DomesticCashFlow> inventoryCostCashFlows = storage.CmdtyInventoryCost(period, inventory);
            double inventoryCostNpv = inventoryCostCashFlows.Sum(cashFlow => cashFlow.Amount * discountFactors(cashFlow.Date));

            IReadOnlyList<DomesticCashFlow> decisionCostCashFlows = injectWithdrawVolume > 0.0
                    ? storage.InjectionCost(period, inventory, injectWithdrawVolume)
                    : storage.WithdrawalCost(period, inventory, -injectWithdrawVolume);

            double decisionCostNpv = decisionCostCashFlows.Sum(cashFlow => cashFlow.Amount * discountFactors(cashFlow.Date));

            double cmdtyUsedForInjectWithdrawVolume = injectWithdrawVolume > 0.0
                ? storage.CmdtyVolumeConsumedOnInject(period, inventory, injectWithdrawVolume)
                : storage.CmdtyVolumeConsumedOnWithdraw(period, inventory, Math.Abs(injectWithdrawVolume));

            // Note that calculations assume that decision volumes do NOT include volumes consumed, and that these volumes are purchased in the market
            double cmdtyUsedForInjectWithdrawNpv = -cmdtyUsedForInjectWithdrawVolume * cmdtyPrice * discountFactorFromCmdtySettlement;

            double periodPv = injectWithdrawNpv - decisionCostNpv + cmdtyUsedForInjectWithdrawNpv - inventoryCostNpv;
            double storageNpv = continuationFutureNpv + periodPv;

            return (StorageNpv: storageNpv, CmdtyConsumed: cmdtyUsedForInjectWithdrawVolume, PeriodPv: periodPv);
        }
    }
}
