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
using System.Linq;
using Cmdty.TimePeriodValueTypes;
using Cmdty.TimeSeries;
using Xunit;

namespace Cmdty.Storage.Test
{
    public sealed class StorageHelperTest
    {
        private const double NumericalTolerance = 1E-10;

        [Fact]
        [Trait("Category", "Helper.BangBangDecisions")]
        public void CalculateBangBangDecisionSet_InjectWithdrawRangeUnconstrainedNoExtraDecisions_ReturnsMinAndMaxRateWithZero()
        {
            var injectWithdrawRange = new InjectWithdrawRange(-15.5, 65.685);
            const double currentInventory = 1010.0;
            const double inventoryLoss = 10.0;
            const double nextStepMinInventory = 900.0;
            const double nextStepMaxInventory = 1070.0;
            const int numExtraDecisions = 0;

            double[] decisionSet = StorageHelper.CalculateBangBangDecisionSet(injectWithdrawRange, currentInventory, inventoryLoss,
                                nextStepMinInventory, nextStepMaxInventory, NumericalTolerance, numExtraDecisions);
            double[] expectedDecisionSet = new[] { injectWithdrawRange.MinInjectWithdrawRate, 0.0, injectWithdrawRange.MaxInjectWithdrawRate};
            Assert.Equal(expectedDecisionSet, decisionSet);
        }

        [Fact]
        [Trait("Category", "Helper.BangBangDecisions")]
        public void CalculateBangBangDecisionSet_InjectWithdrawRangeUnconstrainedWithExtraDecisions_ReturnsMinMaxRateZeroAndExtraDecisions()
        {
            var injectWithdrawRange = new InjectWithdrawRange(-15.5, 65.685);
            const double currentInventory = 1010.0;
            const double inventoryLoss = 10.0;
            const double nextStepMinInventory = 900.0;
            const double nextStepMaxInventory = 1070.0;
            const int numExtraDecisions = 1;

            double[] decisionSet = StorageHelper.CalculateBangBangDecisionSet(injectWithdrawRange, currentInventory, inventoryLoss,
                nextStepMinInventory, nextStepMaxInventory, NumericalTolerance, numExtraDecisions);
            double[] expectedDecisionSet = new[] { injectWithdrawRange.MinInjectWithdrawRate, injectWithdrawRange.MinInjectWithdrawRate/2.0, 0.0,
                injectWithdrawRange.MaxInjectWithdrawRate/2.0, injectWithdrawRange.MaxInjectWithdrawRate };
            Assert.Equal(expectedDecisionSet, decisionSet);
        }


        [Fact]
        [Trait("Category", "Helper.BangBangDecisions")]
        public void CalculateBangBangDecisionSet_InjectWithdrawRangeBothPositiveUnconstrainedNoExtraDecisions_ReturnsMinAndMaxRate()
        {
            var injectWithdrawRange = new InjectWithdrawRange(15.5, 65.685);
            const double currentInventory = 1010.0;
            const double inventoryLoss = 10.0;
            const double nextStepMinInventory = 900.0;
            const double nextStepMaxInventory = 1070.0;
            const int numExtraDecisions = 0;

            double[] decisionSet = StorageHelper.CalculateBangBangDecisionSet(injectWithdrawRange, currentInventory, inventoryLoss,
                                        nextStepMinInventory, nextStepMaxInventory, NumericalTolerance, numExtraDecisions);
            double[] expectedDecisionSet = new[] { injectWithdrawRange.MinInjectWithdrawRate, injectWithdrawRange.MaxInjectWithdrawRate };
            Assert.Equal(expectedDecisionSet, decisionSet);
        }

        [Fact]
        [Trait("Category", "Helper.BangBangDecisions")]
        public void CalculateBangBangDecisionSet_InjectWithdrawRangeBothPositiveUnconstrainedWithExtraDecisions_ReturnsMinAndMaxRateWithExtraDecisions()
        {
            var injectWithdrawRange = new InjectWithdrawRange(15.5, 65.685);
            const double currentInventory = 1010.0;
            const double inventoryLoss = 10.0;
            const double nextStepMinInventory = 900.0;
            const double nextStepMaxInventory = 1070.0;
            const int numExtraDecisions = 1;
            
            double[] decisionSet = StorageHelper.CalculateBangBangDecisionSet(injectWithdrawRange, currentInventory, inventoryLoss,
                nextStepMinInventory, nextStepMaxInventory, NumericalTolerance, numExtraDecisions);
            double[] expectedDecisionSet = new[] { injectWithdrawRange.MinInjectWithdrawRate, 
                (injectWithdrawRange.MinInjectWithdrawRate + injectWithdrawRange.MaxInjectWithdrawRate)/2.0 , injectWithdrawRange.MaxInjectWithdrawRate };
            Assert.Equal(expectedDecisionSet, decisionSet);
        }

        [Fact]
        [Trait("Category", "Helper.BangBangDecisions")]
        public void CalculateBangBangDecisionSet_InjectWithdrawRangeBothNegativeUnconstrainedNoExtraDecisions_ReturnsMinAndMaxRate()
        {
            var injectWithdrawRange = new InjectWithdrawRange(-65.685, -41.5);
            const double currentInventory = 1000.0;
            const double inventoryLoss = 10.0;
            const double nextStepMinInventory = 900.0;
            const double nextStepMaxInventory = 950.0;
            const int numExtraDecisions = 0;
            
            double[] decisionSet = StorageHelper.CalculateBangBangDecisionSet(injectWithdrawRange, currentInventory, inventoryLoss,
                                        nextStepMinInventory, nextStepMaxInventory, NumericalTolerance, numExtraDecisions);
            double[] expectedDecisionSet = new[] { injectWithdrawRange.MinInjectWithdrawRate, injectWithdrawRange.MaxInjectWithdrawRate };
            Assert.Equal(expectedDecisionSet, decisionSet);
        }

        [Fact]
        [Trait("Category", "Helper.BangBangDecisions")]
        public void CalculateBangBangDecisionSet_InjectWithdrawRangeBothNegativeUnconstrainedWithExtraDecisions_ReturnsMinAndMaxRateWithExtraDecisions()
        {
            var injectWithdrawRange = new InjectWithdrawRange(-65.685, -41.5);
            const double currentInventory = 1000.0;
            const double inventoryLoss = 10.0;
            const double nextStepMinInventory = 900.0;
            const double nextStepMaxInventory = 950.0;
            const int numExtraDecisions = 1;

            double[] decisionSet = StorageHelper.CalculateBangBangDecisionSet(injectWithdrawRange, currentInventory, inventoryLoss,
                nextStepMinInventory, nextStepMaxInventory, NumericalTolerance, numExtraDecisions);
            double extraDecision = (injectWithdrawRange.MinInjectWithdrawRate + injectWithdrawRange.MaxInjectWithdrawRate) / 2.0;
            double[] expectedDecisionSet = new[] { injectWithdrawRange.MinInjectWithdrawRate, extraDecision, injectWithdrawRange.MaxInjectWithdrawRate };
            Assert.Equal(expectedDecisionSet, decisionSet);
        }

        [Fact]
        [Trait("Category", "Helper.BangBangDecisions")]
        public void
            CalculateBangBangDecisionSet_NextStepInventoryConstrainsInjectionAndWithdrawalAroundCurrentInventory_ReturnsAdjustedInjectWithdrawRangeAndZero() // TODO rename!
        {
            var injectWithdrawRange = new InjectWithdrawRange(-15.5, 65.685);
            const double currentInventory = 1010.0;
            const double inventoryLoss = 10.0;
            const double nextStepMinInventory = 991.87;
            const double nextStepMaxInventory = 1051.8;
            const int numExtraDecisions = 0;

            double[] decisionSet = StorageHelper.CalculateBangBangDecisionSet(injectWithdrawRange, currentInventory, inventoryLoss,
                                        nextStepMinInventory, nextStepMaxInventory, NumericalTolerance, numExtraDecisions);
            double expectedWithdrawalRate = nextStepMaxInventory - currentInventory + inventoryLoss; 
            double expectedInjectionRate = nextStepMinInventory - currentInventory + inventoryLoss;
            double[] expectedDecisionSet = new[] { expectedInjectionRate, 0.0, expectedWithdrawalRate };
            Assert.Equal(expectedDecisionSet, decisionSet);
        }

        [Fact]
        [Trait("Category", "Helper.BangBangDecisions")]
        public void
            CalculateBangBangDecisionSet_NextStepInventoryConstrainsInjectionAndWithdrawalAroundCurrentInventoryExtraDecisions_ReturnsAdjustedInjectWithdrawRangeZeroAndExtraDecisions() // TODO rename!
        {
            var injectWithdrawRange = new InjectWithdrawRange(-15.5, 65.685);
            const double currentInventory = 1010.0;
            const double inventoryLoss = 10.0;
            const double nextStepMinInventory = 991.87;
            const double nextStepMaxInventory = 1051.8;
            const int numExtraDecisions = 1;

            double[] decisionSet = StorageHelper.CalculateBangBangDecisionSet(injectWithdrawRange, currentInventory, inventoryLoss,
                nextStepMinInventory, nextStepMaxInventory, NumericalTolerance, numExtraDecisions);
            double expectedWithdrawalRate = nextStepMaxInventory - currentInventory + inventoryLoss;
            double expectedInjectionRate = nextStepMinInventory - currentInventory + inventoryLoss;
            double[] expectedDecisionSet = new[] { expectedInjectionRate, expectedInjectionRate/2.0, 0.0, expectedWithdrawalRate/2.0, expectedWithdrawalRate };
            Assert.Equal(expectedDecisionSet, decisionSet);
        }

        [Fact]
        [Trait("Category", "Helper.BangBangDecisions")]
        public void CalculateBangBangDecisionSet_NextStepInventoryConstrainsInjectionLowerThanCurrentNoExtraDecisions_ReturnsArrayWithTwoValuesNoneZero()
        {
            var injectWithdrawRange = new InjectWithdrawRange(-15.5, 65.685);
            const double currentInventory = 1010.0;
            const double inventoryLoss = 10.0;
            const double nextStepMinInventory = 900.00;
            const double nextStepMaxInventory = 995.8;
            const int numExtraDecisions = 0;

            double[] decisionSet = StorageHelper.CalculateBangBangDecisionSet(injectWithdrawRange, currentInventory, inventoryLoss,
                                        nextStepMinInventory, nextStepMaxInventory, NumericalTolerance, numExtraDecisions);
            double expectedWithdrawalRate = injectWithdrawRange.MinInjectWithdrawRate;
            double expectedInjectionRate = nextStepMaxInventory - currentInventory + inventoryLoss;     // Negative injection, i.e. withdrawal
            double[] expectedDecisionSet = new[] { expectedWithdrawalRate, expectedInjectionRate };
            Assert.Equal(expectedDecisionSet, decisionSet);
        }

        [Fact]
        [Trait("Category", "Helper.BangBangDecisions")]
        public void CalculateBangBangDecisionSet_NextStepInventoryConstrainsInjectionLowerThanCurrentWithExtraDecisions_ReturnsArrayWithThreeValuesNoneZero()
        {
            var injectWithdrawRange = new InjectWithdrawRange(-15.5, 65.685);
            const double currentInventory = 1010.0;
            const double inventoryLoss = 10.0;
            const double nextStepMinInventory = 900.00;
            const double nextStepMaxInventory = 995.8;
            const int numExtraDecisions = 1;

            double[] decisionSet = StorageHelper.CalculateBangBangDecisionSet(injectWithdrawRange, currentInventory, inventoryLoss,
                nextStepMinInventory, nextStepMaxInventory, NumericalTolerance, numExtraDecisions);
            double expectedWithdrawalRate = injectWithdrawRange.MinInjectWithdrawRate;
            double expectedInjectionRate = nextStepMaxInventory - currentInventory + inventoryLoss;     // Negative injection, i.e. withdrawal
            double extraDecision = (expectedWithdrawalRate + expectedInjectionRate) / 2.0;
            double[] expectedDecisionSet = new[] { expectedWithdrawalRate, extraDecision, expectedInjectionRate };
            Assert.Equal(expectedDecisionSet, decisionSet);
        }

        [Fact]
        [Trait("Category", "Helper.BangBangDecisions")]
        public void CalculateBangBangDecisionSet_NextStepInventoryConstrainsWithdrawalHigherThanCurrentNoExtraDecisions_ReturnsArrayWithTwoValuesNoneZero()
        {
            var injectWithdrawRange = new InjectWithdrawRange(-15.5, 65.685);
            const double currentInventory = 1010.0;
            const double inventoryLoss = 10.0;
            const double nextStepMinInventory = 1001.8;
            const double nextStepMaxInventory= 1009.51;
            const int numExtraDecisions = 0;

            double[] decisionSet = StorageHelper.CalculateBangBangDecisionSet(injectWithdrawRange, currentInventory, inventoryLoss,
                                    nextStepMinInventory, nextStepMaxInventory, NumericalTolerance, numExtraDecisions);
            double expectedWithdrawalRate = nextStepMaxInventory - currentInventory + inventoryLoss;
            double expectedInjectionRate = nextStepMinInventory - currentInventory + inventoryLoss;     // Negative injection, i.e. withdrawal
            double[] expectedDecisionSet = new[] { expectedInjectionRate, expectedWithdrawalRate };
            Assert.Equal(expectedDecisionSet, decisionSet);
        }

        [Fact]
        [Trait("Category", "Helper.BangBangDecisions")]
        public void CalculateBangBangDecisionSet_NextStepInventoryConstrainsWithdrawalHigherThanCurrentWihtExtraDecisions_ReturnsArrayWithThreeValuesNoneZero()
        {
            var injectWithdrawRange = new InjectWithdrawRange(-15.5, 65.685);
            const double currentInventory = 1010.0;
            const double inventoryLoss = 10.0;
            const double nextStepMinInventory = 1001.8;
            const double nextStepMaxInventory = 1009.51;
            const int numExtraDecisions = 1;

            double[] decisionSet = StorageHelper.CalculateBangBangDecisionSet(injectWithdrawRange, currentInventory, inventoryLoss,
                nextStepMinInventory, nextStepMaxInventory, NumericalTolerance, numExtraDecisions);
            double expectedWithdrawalRate = nextStepMaxInventory - currentInventory + inventoryLoss;
            double expectedInjectionRate = nextStepMinInventory - currentInventory + inventoryLoss;     // Negative injection, i.e. withdrawal
            double extraDecision = (expectedWithdrawalRate + expectedInjectionRate) / 2.0;
            double[] expectedDecisionSet = new[] { expectedInjectionRate, extraDecision, expectedWithdrawalRate };
            Assert.Equal(expectedDecisionSet, decisionSet);
        }
        // TODO throws exception if constraints cannot be met

        [Fact]
        [Trait("Category", "Helper.MaxValueAndIndex")]
        public void MaxValueAndIndex_ReturnsMaxValueAndIndex()
        {
            double[] array = {4.5, -1.2, 6.8, 3.2};
            (double maxValue, int indexOfMax) = StorageHelper.MaxValueAndIndex(array);

            Assert.Equal(6.8, maxValue);
            Assert.Equal(2, indexOfMax);
        }

        [Fact]
        [Trait("Category", "Helper.MaxValueAndIndex")]
        public void MaxValueAndIndex_ArrayOfZeroLength_ThrowsIndexOutOfRangeException()
        {
            Assert.Throws<IndexOutOfRangeException>(() => StorageHelper.MaxValueAndIndex(new double[0]));
        }

        [Fact]
        [Trait("Category", "Helper.CalculateInventorySpace")]
        public void CalculateInventorySpace_CurrentPeriodAfterStorageStartPeriod_AsExpected()
        {
            const double injectionRate = 5.0;
            const double withdrawalRate = 6.0;
            const double startingInventory = 8.0;

            const double inventoryPercentLoss = 0.03;
            const double minInventory = 0.0;
            const double maxInventory = 23.5;

            var storageStart = new Day(2019, 8, 1);
            var storageEnd = new Day(2019, 8, 28);
            var currentPeriod = new Day(2019, 8,  20);

            var storage = CmdtyStorage<Day>.Builder
                        .WithActiveTimePeriod(storageStart, storageEnd)
                        .WithConstantInjectWithdrawRange(-withdrawalRate, injectionRate)
                        .WithConstantMinInventory(minInventory)
                        .WithConstantMaxInventory(maxInventory)
                        .WithPerUnitInjectionCost(1.5)
                        .WithNoCmdtyConsumedOnInject()
                        .WithPerUnitWithdrawalCost(0.8)
                        .WithNoCmdtyConsumedOnWithdraw()
                        .WithFixedPercentCmdtyInventoryLoss(inventoryPercentLoss)
                        .WithNoInventoryCost()
                        .WithTerminalInventoryNpv((cmdtyPrice, inventory) => 0.0)
                        .Build();

            TimeSeries<Day, InventoryRange> inventorySpace =
                        StorageHelper.CalculateInventorySpace(storage, startingInventory, currentPeriod);

            int expectedInventorySpaceCount = storageEnd.OffsetFrom(currentPeriod);
            Assert.Equal(expectedInventorySpaceCount, inventorySpace.Count);

            double expectedInventoryLower = startingInventory * (1 - inventoryPercentLoss) - withdrawalRate;
            double expectedInventoryUpper = startingInventory * (1 - inventoryPercentLoss) + injectionRate;
            AssertInventoryRangeEqualsExpected(inventorySpace[new Day(2019, 8, 21)], expectedInventoryLower, expectedInventoryUpper);
            
            expectedInventoryLower = Math.Max(expectedInventoryLower * (1 - inventoryPercentLoss) - withdrawalRate, minInventory);
            expectedInventoryUpper = Math.Min(expectedInventoryUpper * (1 - inventoryPercentLoss) + injectionRate, maxInventory);
            AssertInventoryRangeEqualsExpected(inventorySpace[new Day(2019, 8, 22)], expectedInventoryLower, expectedInventoryUpper);

            expectedInventoryLower = Math.Max(expectedInventoryLower * (1 - inventoryPercentLoss) - withdrawalRate, minInventory);
            expectedInventoryUpper = Math.Min(expectedInventoryUpper * (1 - inventoryPercentLoss) + injectionRate, maxInventory);
            AssertInventoryRangeEqualsExpected(inventorySpace[new Day(2019, 8, 23)], expectedInventoryLower, expectedInventoryUpper);

            expectedInventoryLower = Math.Max(expectedInventoryLower * (1 - inventoryPercentLoss) - withdrawalRate, minInventory);
            expectedInventoryUpper = Math.Min(expectedInventoryUpper * (1 - inventoryPercentLoss) + injectionRate, maxInventory);
            AssertInventoryRangeEqualsExpected(inventorySpace[new Day(2019, 8, 24)], expectedInventoryLower, expectedInventoryUpper);

            expectedInventoryLower = Math.Max(expectedInventoryLower * (1 - inventoryPercentLoss) - withdrawalRate, minInventory);
            expectedInventoryUpper = Math.Min(expectedInventoryUpper * (1 - inventoryPercentLoss) + injectionRate, maxInventory);
            AssertInventoryRangeEqualsExpected(inventorySpace[new Day(2019, 8, 25)], expectedInventoryLower, expectedInventoryUpper);

            expectedInventoryLower = Math.Max(expectedInventoryLower * (1 - inventoryPercentLoss) - withdrawalRate, minInventory);
            expectedInventoryUpper = Math.Min(expectedInventoryUpper * (1 - inventoryPercentLoss) + injectionRate, maxInventory);
            AssertInventoryRangeEqualsExpected(inventorySpace[new Day(2019, 8, 26)], expectedInventoryLower, expectedInventoryUpper);

            expectedInventoryLower = Math.Max(expectedInventoryLower * (1 - inventoryPercentLoss) - withdrawalRate, minInventory);
            expectedInventoryUpper = Math.Min(expectedInventoryUpper * (1 - inventoryPercentLoss) + injectionRate, maxInventory);
            AssertInventoryRangeEqualsExpected(inventorySpace[new Day(2019, 8, 27)], expectedInventoryLower, expectedInventoryUpper);

            expectedInventoryLower = Math.Max(expectedInventoryLower * (1 - inventoryPercentLoss) - withdrawalRate, minInventory);
            expectedInventoryUpper = Math.Min(expectedInventoryUpper * (1 - inventoryPercentLoss) + injectionRate, maxInventory);
            AssertInventoryRangeEqualsExpected(inventorySpace[new Day(2019, 8, 28)], expectedInventoryLower, expectedInventoryUpper);
            
        }
        
        [Fact]
        [Trait("Category", "Helper.CalculateInventorySpace")]
        public void CalculateInventorySpace_CurrentPeriodBeforeStorageStartPeriod_AsExpected()
        {
            const double injectionRate = 5.0;
            const double withdrawalRate = 6.0;
            const double startingInventory = 11.0;

            const double inventoryPercentLoss = 0.03;
            const double minInventory = 0.0;
            const double maxInventory = 23.5;

            var storageStart = new Day(2019, 8, 19);
            var storageEnd = new Day(2019, 8, 28);
            var currentPeriod = new Day(2019, 8, 10);

            var storage = CmdtyStorage<Day>.Builder
                        .WithActiveTimePeriod(storageStart, storageEnd)
                        .WithConstantInjectWithdrawRange(-withdrawalRate, injectionRate)
                        .WithConstantMinInventory(minInventory)
                        .WithConstantMaxInventory(maxInventory)
                        .WithPerUnitInjectionCost(1.5)
                        .WithNoCmdtyConsumedOnInject()
                        .WithPerUnitWithdrawalCost(0.8)
                        .WithNoCmdtyConsumedOnWithdraw()
                        .WithFixedPercentCmdtyInventoryLoss(inventoryPercentLoss)
                        .WithNoInventoryCost()
                        .MustBeEmptyAtEnd()
                        .Build();

            TimeSeries<Day, InventoryRange> inventorySpace =
                        StorageHelper.CalculateInventorySpace(storage, startingInventory, currentPeriod);

            int expectedInventorySpaceCount = storageEnd.OffsetFrom(storageStart);
            Assert.Equal(expectedInventorySpaceCount, inventorySpace.Count);
            
            double expectedInventoryLower = startingInventory * (1 - inventoryPercentLoss) - withdrawalRate;
            double expectedInventoryUpper = startingInventory * (1 - inventoryPercentLoss) + injectionRate;
            AssertInventoryRangeEqualsExpected(inventorySpace[new Day(2019, 8, 20)], expectedInventoryLower, expectedInventoryUpper);

            expectedInventoryLower = Math.Max(expectedInventoryLower * (1 - inventoryPercentLoss) - withdrawalRate, minInventory);
            expectedInventoryUpper = Math.Min(expectedInventoryUpper * (1 - inventoryPercentLoss) + injectionRate, maxInventory);
            AssertInventoryRangeEqualsExpected(inventorySpace[new Day(2019, 8, 21)], expectedInventoryLower, expectedInventoryUpper);

            expectedInventoryLower = Math.Max(expectedInventoryLower * (1 - inventoryPercentLoss) - withdrawalRate, minInventory);
            expectedInventoryUpper = Math.Min(expectedInventoryUpper * (1 - inventoryPercentLoss) + injectionRate, maxInventory);
            AssertInventoryRangeEqualsExpected(inventorySpace[new Day(2019, 8, 22)], expectedInventoryLower, expectedInventoryUpper);

            expectedInventoryLower = Math.Max(expectedInventoryLower * (1 - inventoryPercentLoss) - withdrawalRate, minInventory);
            expectedInventoryUpper = Math.Min(expectedInventoryUpper * (1 - inventoryPercentLoss) + injectionRate, maxInventory);
            AssertInventoryRangeEqualsExpected(inventorySpace[new Day(2019, 8, 23)], expectedInventoryLower, expectedInventoryUpper);

            expectedInventoryLower = Math.Max(expectedInventoryLower * (1 - inventoryPercentLoss) - withdrawalRate, minInventory);
            expectedInventoryUpper = Math.Min(expectedInventoryUpper * (1 - inventoryPercentLoss) + injectionRate, maxInventory);
            AssertInventoryRangeEqualsExpected(inventorySpace[new Day(2019, 8, 24)], expectedInventoryLower, expectedInventoryUpper);

            // At this point the backwardly derived reduced inventory space kicks in so we need to start going backwards in time
            expectedInventoryLower = 0.0;
            expectedInventoryUpper = 0.0;
            AssertInventoryRangeEqualsExpected(inventorySpace[new Day(2019, 8, 28)], expectedInventoryLower, expectedInventoryUpper);

            expectedInventoryUpper = Math.Min((expectedInventoryUpper + withdrawalRate) / (1 - inventoryPercentLoss), maxInventory);
            AssertInventoryRangeEqualsExpected(inventorySpace[new Day(2019, 8, 27)], expectedInventoryLower, expectedInventoryUpper);

            expectedInventoryUpper = Math.Min((expectedInventoryUpper + withdrawalRate) / (1 - inventoryPercentLoss), maxInventory);
            AssertInventoryRangeEqualsExpected(inventorySpace[new Day(2019, 8, 26)], expectedInventoryLower, expectedInventoryUpper);

            expectedInventoryUpper = Math.Min((expectedInventoryUpper + withdrawalRate) / (1 - inventoryPercentLoss), maxInventory);
            AssertInventoryRangeEqualsExpected(inventorySpace[new Day(2019, 8, 25)], expectedInventoryLower, expectedInventoryUpper);

        }
        
        private void AssertInventoryRangeEqualsExpected(InventoryRange inventoryRange, 
                            double expectedInventoryLower, double expectedInventoryUpper)
        {
            Assert.Equal(expectedInventoryLower, inventoryRange.MinInventory);
            Assert.Equal(expectedInventoryUpper, inventoryRange.MaxInventory);
        }

        [Fact]
        [Trait("Category", "Helper.BisectInventorySpace")]
        public void BisectInventorySpace_InventoryEqualsHighestGridValue_ReturnsTopIndexMinusOneAndTopIndex()
        {
            var inventoryGrid = new[] { 0.0, 5.3, 9.5, 15.63, 25.8};
            int topIndex = inventoryGrid.Length - 1;
            double inventory = inventoryGrid[topIndex];

            (int lowerIndex, int upperIndex) = StorageHelper.BisectInventorySpace(inventoryGrid, inventory, NumericalTolerance);

            Assert.Equal(topIndex-1, lowerIndex);
            Assert.Equal(topIndex, upperIndex);
        }

        [Fact]
        [Trait("Category", "Helper.BisectInventorySpace")]
        public void BisectInventorySpace_InventoryEqualsLowestGridValue_ReturnsZeroAndZero()
        {
            var inventoryGrid = new[] { 0.0, 5.3, 9.5, 15.63, 25.8 };
            double inventory = inventoryGrid[0];

            (int lowerIndex, int upperIndex) = StorageHelper.BisectInventorySpace(inventoryGrid, inventory, NumericalTolerance);

            Assert.Equal(0, lowerIndex);
            Assert.Equal(0, upperIndex);
        }

        [Fact]
        [Trait("Category", "Helper.BisectInventorySpace")]
        public void BisectInventorySpace_InventoryBetweenTopTwoValues_ReturnsTopIndexMinusOneAndTopIndex()
        {
            var inventoryGrid = new[] { 0.0, 5.3, 9.5, 15.63, 25.8 };
            int topIndex = inventoryGrid.Length - 1;
            double inventory = 20.01;

            (int lowerIndex, int upperIndex) = StorageHelper.BisectInventorySpace(inventoryGrid, inventory, NumericalTolerance);

            Assert.Equal(topIndex - 1, lowerIndex);
            Assert.Equal(topIndex, upperIndex);
        }

        [Fact]
        [Trait("Category", "Helper.BisectInventorySpace")]
        public void BisectInventorySpace_InventoryBetweenBottomTwoValues_ReturnsBottomIndexAndBottomIndexPlusOne()
        {
            var inventoryGrid = new[] { 0.0, 5.3, 9.5, 15.63, 25.8 };
            const int bottomIndex = 0;
            double inventory = 1.23;

            (int lowerIndex, int upperIndex) = StorageHelper.BisectInventorySpace(inventoryGrid, inventory, NumericalTolerance);

            Assert.Equal(bottomIndex, lowerIndex);
            Assert.Equal(bottomIndex + 1, upperIndex);
        }

        [Fact]
        [Trait("Category", "Helper.BisectInventorySpace")]
        public void BisectInventorySpace_InventoryEqualsSecondLowestValue_ReturnsOneAndOne()
        {
            var inventoryGrid = new[] { 0.0, 5.3, 9.5, 15.63, 25.8 };
            double inventory = 5.3;

            (int lowerIndex, int upperIndex) = StorageHelper.BisectInventorySpace(inventoryGrid, inventory, NumericalTolerance);

            Assert.Equal(1, lowerIndex);
            Assert.Equal(1, upperIndex);
        }

        [Fact]
        [Trait("Category", "Helper.BisectInventorySpace")]
        public void BisectInventorySpace_InventoryWithinGrid_AsExpected()
        {
            var inventoryGrid = new[] { 0.0, 5.3, 9.5, 15.63, 25.8 };
            double inventory = 10.89;

            (int lowerIndex, int upperIndex) = StorageHelper.BisectInventorySpace(inventoryGrid, inventory, NumericalTolerance);

            Assert.Equal(2, lowerIndex);
            Assert.Equal(3, upperIndex);
        }

        [Fact]
        [Trait("Category", "Helper.BisectInventorySpace")]
        public void BisectInventorySpace_GridWithTwoPointsInventoryWithin_ReturnsZeroAndOne()
        {
            var inventoryGrid = new[] { 1.3, 2.8 };
            double inventory = 1.4;

            (int lowerIndex, int upperIndex) = StorageHelper.BisectInventorySpace(inventoryGrid, inventory, NumericalTolerance);

            Assert.Equal(0, lowerIndex);
            Assert.Equal(1, upperIndex);
        }

        [Fact]
        [Trait("Category", "Helper.BisectInventorySpace")]
        public void BisectInventorySpace_GridWithTwoPointsInventoryEqualsLowestPoint_ReturnsZeroAndZero()
        {
            var inventoryGrid = new[] { 1.3, 2.8 };
            double inventory = 1.3;

            (int lowerIndex, int upperIndex) = StorageHelper.BisectInventorySpace(inventoryGrid, inventory, NumericalTolerance);

            Assert.Equal(0, lowerIndex);
            Assert.Equal(0, upperIndex);
        }

        [Fact]
        [Trait("Category", "Helper.BisectInventorySpace")]
        public void BisectInventorySpace_GridWithTwoPointsInventoryEqualsUpperPoint_ReturnsZeroAndOne()
        {
            var inventoryGrid = new[] { 1.3, 2.8 };
            double inventory = 2.8;

            (int lowerIndex, int upperIndex) = StorageHelper.BisectInventorySpace(inventoryGrid, inventory, NumericalTolerance);

            Assert.Equal(0, lowerIndex);
            Assert.Equal(1, upperIndex);
        }

        [Fact]
        [Trait("Category", "Helper.BisectInventorySpace")]
        public void BisectInventorySpace_SingleItemGridEqualsInventory_ReturnsTwoZeros()
        {
            var inventoryGrid = new[] { 1.3 };
            double inventory = 1.3;

            (int lowerIndex, int upperIndex) = StorageHelper.BisectInventorySpace(inventoryGrid, inventory, NumericalTolerance);

            Assert.Equal(0, lowerIndex);
            Assert.Equal(0, upperIndex);
        }

        [Fact]
        [Trait("Category", "Helper.BisectInventorySpace")]
        public void BisectInventorySpace_InventoryHigherThanRange_Throws()
        {
            var inventoryGrid = new[] { 0.0, 5.3, 9.5, 15.63, 25.8 };
            double inventory = 26.8;

            Assert.Throws<ArgumentException>(() => StorageHelper.BisectInventorySpace(inventoryGrid, inventory, NumericalTolerance));
        }

        [Fact]
        [Trait("Category", "Helper.BisectInventorySpace")]
        public void BisectInventorySpace_InventoryLowerThanRange_Throws()
        {
            var inventoryGrid = new[] { 0.0, 5.3, 9.5, 15.63, 25.8 };
            double inventory = -1.2;

            Assert.Throws<ArgumentException>(() => StorageHelper.BisectInventorySpace(inventoryGrid, inventory, NumericalTolerance));
        }

        [Fact]
        [Trait("Category", "Helper.BisectInventorySpace")]
        public void BisectInventorySpace_InventoryLowerButWithinToleranceOfInventoryGridMin_ReturnsZeroAndZero()
        {
            var inventoryGrid = new[] { 0.0, 5.3, 9.5, 15.63, 25.8 };
            double inventory = inventoryGrid[0] - NumericalTolerance / 2.0;

            (int lowerIndex, int upperIndex) = StorageHelper.BisectInventorySpace(inventoryGrid, inventory, NumericalTolerance);

            Assert.Equal(0, lowerIndex);
            Assert.Equal(0, upperIndex);
        }

        [Fact]
        [Trait("Category", "Helper.BisectInventorySpace")]
        public void BisectInventorySpace_InventoryHigherButWithinToleranceOfInventoryGridMax_ReturnsMaxIndexAndMaxIndex()
        {
            var inventoryGrid = new[] { 0.0, 5.3, 9.5, 15.63, 25.8 };
            int maxIndex = inventoryGrid.Length - 1;
            double inventory = inventoryGrid[maxIndex] + NumericalTolerance / 2.0;

            (int lowerIndex, int upperIndex) = StorageHelper.BisectInventorySpace(inventoryGrid, inventory, NumericalTolerance);

            Assert.Equal(maxIndex, lowerIndex);
            Assert.Equal(maxIndex, upperIndex);
        }

        [Fact]
        [Trait("Category", "Helper.BatchPairs")]
        public void BatchPairs_FourElements_ReturnsTwoItemsWithCorrectAverages()
        {
            // Arrange
            var values = new [] { 1.0, 2.0, 3.0, 4.5 };
            
            // Act
            double[] batched = StorageHelper.BatchPairs(values, 2, 0).ToArray();

            // Assert
            Assert.Equal(2, batched.Length);
            Assert.Equal((values[0] + values[1])/2.0, batched[0]);
            Assert.Equal((values[2] + values[3])/2.0, batched[1]);
        }

        [Fact]
        [Trait("Category", "Helper.BatchPairs")]
        public void BatchPairs_FiveElements_ReturnsThreeItemsWithCorrectAverages()
        {
            // Arrange
            var values = new[] { 1.0, 2.0, 3.0, 4.5, 7.7 };

            // Act
            double[] batched = StorageHelper.BatchPairs(values, 2, 1).ToArray();

            // Assert
            Assert.Equal(3, batched.Length);
            Assert.Equal((values[0] + values[1]) / 2.0, batched[0]);
            Assert.Equal((values[2] + values[3]) / 2.0, batched[1]);
            Assert.Equal(values[4], batched[2]);
        }

    }
}
