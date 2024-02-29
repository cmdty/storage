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
using MathNet.Numerics.Providers.LinearAlgebra;
using MathNet.Numerics.Statistics;

namespace Cmdty.Storage
{
    public static class StorageHelper
    {

        public static TimeSeries<T, InventoryRange> CalculateInventorySpace<T>(ICmdtyStorage<T> storage, double startingInventory, T currentPeriod)
            where T : ITimePeriod<T>
        {
            if (currentPeriod.CompareTo(storage.EndPeriod) > 0) // TODO should condition be >= 0?
                throw new ArgumentException("Storage has expired");// TODO change to return empty TimeSeries?

            T startActiveStorage = storage.StartPeriod.CompareTo(currentPeriod) > 0 ? storage.StartPeriod : currentPeriod;

            int numPeriods = storage.EndPeriod.OffsetFrom(startActiveStorage);

            // Calculate the inventory space range going forward

            var forwardCalcMaxInventory = new double[numPeriods];
            var forwardCalcMinInventory = new double[numPeriods];

            double minInventoryForwardCalc = startingInventory;
            double maxInventoryForwardCalc = startingInventory;

            for (int i = 0; i < numPeriods; i++)
            {
                T periodLoop = startActiveStorage.Offset(i);
                T nextPeriod = periodLoop.Offset(1);
                double inventoryPercentLoss = storage.CmdtyInventoryPercentLoss(periodLoop);

                double injectWithdrawMin = storage.GetInjectWithdrawRange(periodLoop, minInventoryForwardCalc).MinInjectWithdrawRate;
                double inventoryLossAtMin = inventoryPercentLoss * minInventoryForwardCalc;
                double storageMin = storage.MinInventory(nextPeriod);
                minInventoryForwardCalc = Math.Max(minInventoryForwardCalc - inventoryLossAtMin + injectWithdrawMin, storageMin);
                forwardCalcMinInventory[i] = minInventoryForwardCalc;

                double injectWithdrawMax = storage.GetInjectWithdrawRange(periodLoop, maxInventoryForwardCalc).MaxInjectWithdrawRate;
                double inventoryLossAtMax = inventoryPercentLoss * maxInventoryForwardCalc;
                double storageMax = storage.MaxInventory(nextPeriod);
                maxInventoryForwardCalc = Math.Min(maxInventoryForwardCalc - inventoryLossAtMax + injectWithdrawMax, storageMax);
                forwardCalcMaxInventory[i] = maxInventoryForwardCalc;
            }

            // Calculate the inventory space range going backwards
            var backwardCalcMaxInventory = new double[numPeriods];

            var backwardCalcMinInventory = new double[numPeriods];

            T periodBackLoop = storage.EndPeriod;
            backwardCalcMaxInventory[numPeriods - 1] = storage.MustBeEmptyAtEnd ? 0 : storage.MaxInventory(storage.EndPeriod);
            backwardCalcMinInventory[numPeriods - 1] = storage.MustBeEmptyAtEnd ? 0 : storage.MinInventory(storage.EndPeriod);

            for (int i = numPeriods - 2; i >= 0; i--)
            {
                periodBackLoop = periodBackLoop.Offset(-1);
                backwardCalcMaxInventory[i] = storage.InventorySpaceUpperBound(periodBackLoop, backwardCalcMinInventory[i + 1], backwardCalcMaxInventory[i + 1]);
                backwardCalcMinInventory[i] = storage.InventorySpaceLowerBound(periodBackLoop, backwardCalcMinInventory[i + 1], 
                                                                backwardCalcMaxInventory[i + 1]);
            }

            // Calculate overall inventory space and check for consistency

            var inventoryRanges = new InventoryRange[numPeriods];

            for (int i = 0; i < numPeriods; i++)
            {
                double inventorySpaceMax = Math.Min(forwardCalcMaxInventory[i], backwardCalcMaxInventory[i]);
                double inventorySpaceMin = Math.Max(forwardCalcMinInventory[i], backwardCalcMinInventory[i]);
                if (inventorySpaceMin > inventorySpaceMax)
                    throw new InventoryConstraintsCannotBeFulfilledException();
                inventoryRanges[i] = new InventoryRange(inventorySpaceMin, inventorySpaceMax);
            }

            return new TimeSeries<T, InventoryRange>(startActiveStorage.Offset(1), inventoryRanges);
        }

        public static double[] CalculateBangBangDecisionSet(InjectWithdrawRange injectWithdrawRange, double currentInventory, double inventoryLoss,
                                        double nextStepMinInventory, double nextStepMaxInventory, double numericalTolerance, int numExtraDecisions=0) // TODO remove default value of zero
        {
            if (nextStepMinInventory > nextStepMaxInventory)
                throw new ArgumentException($"Parameter {nameof(nextStepMinInventory)} value cannot be higher than parameter {nameof(nextStepMaxInventory)} value.");
            if (numExtraDecisions < 0)
                throw new ArgumentException($"Parameter {nameof(numExtraDecisions)} must be non-negative.", nameof(numExtraDecisions));

            double inventoryAfterLoss = currentInventory - inventoryLoss;

            double inventoryAfterMaxWithdrawal = injectWithdrawRange.MinInjectWithdrawRate + inventoryAfterLoss;
            double yieldedWithdrawalRate;

            if (inventoryAfterMaxWithdrawal > nextStepMaxInventory) // Max withdrawal still above next step max inventory
            {
                if (inventoryAfterMaxWithdrawal - nextStepMaxInventory < numericalTolerance)
                {
                    // Next period inventory is breached, but only by a small amount, probably due to root finding in PolynomialInjectWithdrawConstraint during inventory space reduction
                    yieldedWithdrawalRate = nextStepMaxInventory - inventoryAfterLoss; // TODO unit test code reaching here
                }
                else
                {
                    throw new ArgumentException("Inventory constraints cannot be fulfilled. This could potentially be fixed by increasing the numerical tolerance.");
                }
            }
            else if (inventoryAfterMaxWithdrawal > nextStepMinInventory)
            {
                yieldedWithdrawalRate = injectWithdrawRange.MinInjectWithdrawRate; // Unconstrained withdrawal
            }
            else
            {
                yieldedWithdrawalRate = nextStepMinInventory - inventoryAfterLoss; //constrained withdrawal (could be made positive to injection)
            }

            double inventoryAfterMaxInjection = injectWithdrawRange.MaxInjectWithdrawRate + inventoryAfterLoss;
            double yieldedInjectionRate;

            if (inventoryAfterMaxInjection < nextStepMinInventory) // Max injection still below next step min inventory constraint
            {
                if (nextStepMinInventory - inventoryAfterMaxInjection < numericalTolerance)
                {
                    // Next period inventory is breached, but only by a small amount, probably due to root finding in PolynomialInjectWithdrawConstraint during inventory space reduction
                    yieldedInjectionRate = nextStepMinInventory - inventoryAfterLoss; // TODO unit test code reaching here
                }
                else
                {
                    throw new ArgumentException("Inventory constraints cannot be fulfilled. This could potentially be fixed by increasing the numerical tolerance.");
                }
            }
            else if (inventoryAfterMaxInjection < nextStepMaxInventory)
            {
                yieldedInjectionRate = injectWithdrawRange.MaxInjectWithdrawRate; // Unconstrained injection
            }
            else
            {
                yieldedInjectionRate = nextStepMaxInventory - inventoryAfterLoss; // Constrained injection (could be made negative to withdrawal)
            }

            double[] decisionSet;
            if (yieldedWithdrawalRate >= 0.0 || yieldedInjectionRate <= 0.0) // No zero decision
            {
                if (numExtraDecisions > 0)
                {
                    decisionSet = new double[numExtraDecisions + 2];
                    decisionSet[0] = yieldedWithdrawalRate;
                    decisionSet[decisionSet.Length - 1] = yieldedInjectionRate;
                    PopulateExtraDecisions(yieldedWithdrawalRate, yieldedInjectionRate, numExtraDecisions, new Span<double>(decisionSet, 1, numExtraDecisions));
                }
                else
                    decisionSet = new double[] {yieldedWithdrawalRate, yieldedInjectionRate};
            }
            else
            {
                if (numExtraDecisions > 0)
                {
                    decisionSet = new double[numExtraDecisions * 2 + 3];
                    decisionSet[0] = yieldedWithdrawalRate;
                    decisionSet[decisionSet.Length - 1] = yieldedInjectionRate;
                    PopulateExtraDecisions(yieldedWithdrawalRate, 0, numExtraDecisions, new Span<double>(decisionSet, 1, numExtraDecisions));
                    PopulateExtraDecisions(0, yieldedInjectionRate, numExtraDecisions, new Span<double>(decisionSet, numExtraDecisions + 2, numExtraDecisions));
                }
                else
                    decisionSet = new double[] { yieldedWithdrawalRate, 0.0, yieldedInjectionRate };
            }

            return decisionSet;

            // TODO case of yieldedWithdrawalRate equals to yieldedInjectionRate?
        }

        private static void PopulateExtraDecisions(double min, double max, int extraDecisions, Span<double> results)
        {
            double increment = (max - min) / (extraDecisions + 1);
            for (int i = 0; i < extraDecisions; i++)
                results[i] = (i + 1) * increment + min;
        }

        public static (double Max, int IndexOfMax) MaxValueAndIndex(double[] array)
        {
            double max = array[0];
            int indexOfMax = 0;

            for (int i = 1; i < array.Length; i++)
            {
                double val = array[i];
                if (val > max)
                {
                    max = val;
                    indexOfMax = i;
                }
            }
            return (max, indexOfMax);
        }

        // TODO use this in IntrinsicStorageValuation
        public static (double ImmediateNpv, double CmdtyConsumed) 
            StorageImmediateNpvForDecision<T>(ICmdtyStorage<T> storage, T period, double inventory,
                double injectWithdrawVolume, double cmdtyPrice, double discountFactorFromCmdtySettlement, Func<Day, double> discountFactors)
            where T : ITimePeriod<T>
        {

            double injectWithdrawNpv = -injectWithdrawVolume * cmdtyPrice * discountFactorFromCmdtySettlement;

            IReadOnlyList<DomesticCashFlow> storageCostCashFlows = injectWithdrawVolume > 0.0
                    ? storage.InjectionCost(period, inventory, injectWithdrawVolume)
                    : storage.WithdrawalCost(period, inventory, -injectWithdrawVolume);

            double storageCostNpv = storageCostCashFlows.Sum(cashFlow => cashFlow.Amount * discountFactors(cashFlow.Date));

            double cmdtyUsedForInjectWithdrawVolume = injectWithdrawVolume > 0.0
                ? storage.CmdtyVolumeConsumedOnInject(period, inventory, injectWithdrawVolume)
                : storage.CmdtyVolumeConsumedOnWithdraw(period, inventory, -injectWithdrawVolume);

            // Note that calculations assume that decision volumes do NOT include volumes consumed, and that these volumes are purchased in the market
            double cmdtyUsedForInjectWithdrawNpv = -cmdtyUsedForInjectWithdrawVolume * cmdtyPrice * discountFactorFromCmdtySettlement;

            double immediateNpv = injectWithdrawNpv - storageCostNpv + cmdtyUsedForInjectWithdrawNpv;

            return (ImmediateNpv: immediateNpv, CmdtyConsumed: cmdtyUsedForInjectWithdrawVolume);
        }

        // Long name because Pythonnet doesn't like overloads
        public static Func<Day, Day, double> CreateAct65ContCompDiscounterFromSeries([NotNull] TimeSeries<Day, double> interestRateCurve)
        {
            double InterestRate(Day cashFlowDate)
            {
                if (!interestRateCurve.TryGetValue(cashFlowDate, out double interestRate))
                    throw new ArgumentException($"No interest rate provided for {cashFlowDate.ToString()}.");
                return interestRate;
            }
            return CreateAct65ContCompDiscounter(InterestRate);
        }

        public static Func<Day, Day, double> CreateAct65ContCompDiscounter([NotNull] Func<Day, double> settleDateToInterestRate)
        {
            if (settleDateToInterestRate == null) throw new ArgumentNullException(nameof(settleDateToInterestRate));

            return (Day presentDay, Day cashFlowDay) =>
            {
                if (cashFlowDay <= presentDay)
                    return 1.0;
                double interestRate = settleDateToInterestRate(cashFlowDay);
                return Math.Exp(-cashFlowDay.OffsetFrom(presentDay) / 365.0 * interestRate);
            };
        }

        public static Func<Day, Day, double> CreateAct65ContCompDiscounter(double interestRate) =>
            CreateAct65ContCompDiscounter(date=> interestRate);

        public static string LinearAlgebraProvider() => LinearAlgebraControl.Provider.ToString();

        public static (int LowerIndex, int UpperIndex) BisectInventorySpace(double[] inventoryGrid, double inventory, double numericalTolerance)
        {
            if (inventoryGrid.Length == 1 && EqualsWithinTol(inventory, inventoryGrid[0], numericalTolerance))
                return (LowerIndex: 0, UpperIndex: 0);

            int lowerIndex = 0;
            int upperIndex = inventoryGrid.Length - 1;
            int topIndex = upperIndex;
            while (upperIndex > lowerIndex)
            {
                int midIndex = (lowerIndex + upperIndex) / 2;
                double inventoryMid = inventoryGrid[midIndex];

                if (EqualsWithinTol(inventory, inventoryMid, numericalTolerance))
                    return (LowerIndex: midIndex, UpperIndex: midIndex);

                if (inventoryMid > inventory)
                {
                    upperIndex = midIndex; // Search lower half
                }
                else // inventory >= inventoryMid
                {
                    int midIndexPlusOne = midIndex + 1;
                    double inventoryMidPlusOne = inventoryGrid[midIndexPlusOne];
                    if (inventory <= inventoryMidPlusOne)
                        return (LowerIndex: midIndex, UpperIndex: midIndexPlusOne);
                    if (EqualsWithinTol(inventory, inventoryMidPlusOne, numericalTolerance))
                        return (LowerIndex: midIndexPlusOne, UpperIndex: midIndexPlusOne);
                    if (midIndexPlusOne == topIndex)
                        throw new ArgumentException("Inventory is outside of inventoryGrid bounds.");
                    lowerIndex = midIndex; // Search upper half
                }
            }
            throw new ArgumentException("Inventory is outside of inventoryGrid bounds.");
        }

        public static bool EqualsWithinTol(double a, double b, double tol) => Math.Abs(a - b) <= tol;

        /// <summary>
        /// Derives a linear equation from a pair of points (x1, y1) and (x2, y2) and then solves for x, for a known y
        /// </summary>
        public static double InterpolateLinearAndSolve(double x1, double y1, double x2, double y2, double y)
        {
            // Calculate m (gradient) and c (constant) coefficients of linear equation y = mx + c
            double gradient = (y2 - y1) / (x2 - x1);
            double constant = y1 - gradient * x1;

            // Find x for known y
            double x = (y - constant) / gradient;
            return x;
        }

        public static double StandardError(double[] values, bool antithetic)
        {
            return antithetic ? 
                AntitheticStandardError(values) :
                values.StandardDeviation() / Math.Sqrt(values.Length);
        }

        private static double AntitheticStandardError(double[] values)
        {
            int divNumSimsBy2 = Math.DivRem(values.Length, 2, out int remainder);
            int numIndependent = divNumSimsBy2 + remainder;
            return BatchPairs(values, divNumSimsBy2, remainder).StandardDeviation() / Math.Sqrt(numIndependent);
        }

        // TODO: unit test
        private static IEnumerable<double> BatchPairs(double[] values, int numPairs, int remainder)
        {
            for (int i = 0; i < numPairs; i++)
            {
                int startIndex = i * 2;
                yield return (values[startIndex] + values[startIndex + 1]) / 2.0;
            }
            if (remainder > 0)
                yield return values[values.Length - 1];
        }
    }
}
