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

using System.Linq;
using Cmdty.TimePeriodValueTypes;
using Cmdty.TimeSeries;
using Xunit;

// ReSharper disable ParameterOnlyUsedForPreconditionCheck.Local

namespace Cmdty.Storage.Test
{
    public sealed class IntrinsicStorageValuationTest
    {

        private static IntrinsicStorageValuationResults<Day> GenerateValuationResults(double startingInventory, 
                                                                        TimeSeries<Day, double> forwardCurve, Day currentPeriod)
        {
            var storageStart = new Day(2019, 9, 1);
            var storageEnd = new Day(2019, 9, 30);

            TimeSeries<Month, Day> settlementDates = new TimeSeries<Month, Day>.Builder
                {
                    {new Month(2019, 9),  new Day(2019, 10, 5)}
                }.Build();

            CmdtyStorage<Day> storage = CmdtyStorage<Day>.Builder
                .WithActiveTimePeriod(storageStart, storageEnd)
                .WithConstantInjectWithdrawRange(-45.5, 56.6)
                .WithConstantMinInventory(0.0)
                .WithConstantMaxInventory(1000.0)
                .WithPerUnitInjectionCost(0.8, injectionDate => injectionDate)
                .WithNoCmdtyConsumedOnInject()
                .WithPerUnitWithdrawalCost(1.2, withdrawalDate => withdrawalDate)
                .WithNoCmdtyConsumedOnWithdraw()
                .WithNoCmdtyInventoryLoss()
                .WithNoInventoryCost()
                .MustBeEmptyAtEnd()
                .Build();

            IntrinsicStorageValuationResults<Day> valuationResults = IntrinsicStorageValuation<Day>
                .ForStorage(storage)
                .WithStartingInventory(startingInventory)
                .ForCurrentPeriod(currentPeriod)
                .WithForwardCurve(forwardCurve)
                .WithMonthlySettlement(settlementDates)
                .WithDiscountFactorFunc((valuationDate, cashFlowDate) => 1.0) // No discounting
                .WithFixedGridSpacing(10.0)
                .WithLinearInventorySpaceInterpolation()
                .WithNumericalTolerance(1E-10)
                .Calculate();

            return valuationResults;
        }

        private static TimeSeries<Day, double> GenerateBackwardatedCurve(Day storageStart, Day storageEnd)
        {
            var forwardCurveBuilder = new TimeSeries<Day, double>.Builder();
            double forwardPrice = 59.95;
            foreach (Day forwardCurvePoint in storageStart.EnumerateTo(storageEnd))
            {
                forwardCurveBuilder.Add(forwardCurvePoint, forwardPrice);
                forwardPrice -= 0.58;
            }

            return forwardCurveBuilder.Build();
        }

        private void AssertDecisionProfileAllZeros<T>(TimeSeries<T, StorageProfile> storageProfile, T expectedStart, T expectedEnd)
            where T : ITimePeriod<T>
        {
            Assert.Equal(expectedStart, storageProfile.Start);
            Assert.Equal(expectedEnd, storageProfile.End);
            foreach (double d in storageProfile.Data.Select(profile => profile.InjectWithdrawVolume))
            {
                Assert.Equal(0.0, d);
            }
        }

        [Fact]
        public void Calculate_ZeroInventoryCurveBackwardated_ResultWithZeroNetPresentValue()
        {
            var currentPeriod = new Day(2019, 9, 15);

            var forwardCurve = GenerateBackwardatedCurve(new Day(2019, 9, 1), 
                                                        new Day(2019, 9, 30));

            IntrinsicStorageValuationResults<Day> valuationResults = GenerateValuationResults(0.0, forwardCurve, currentPeriod);

            Assert.Equal(0.0, valuationResults.Npv);
        }

        [Fact]
        public void Calculate_ZeroInventoryCurveBackwardated_ResultWithZerosDecisionProfile()
        {
            var currentPeriod = new Day(2019, 9, 15);

            var forwardCurve = GenerateBackwardatedCurve(new Day(2019, 9, 1),
                                    new Day(2019, 9, 30));

            IntrinsicStorageValuationResults<Day> valuationResults = GenerateValuationResults(0.0, forwardCurve, currentPeriod);

            AssertDecisionProfileAllZeros(valuationResults.StorageProfile, 
                                new Day(2019, 9, 15), 
                                new Day(2019, 9, 30));
        }

        [Fact]
        public void Calculate_ZeroInventoryForwardSpreadLessThanCycleCost_ResultWithZeroNetPresentValue()
        {
            // Cycle cost = 2.0, so create curve with spread of 1.99
            var valuationResults = IntrinsicValuationZeroInventoryForwardCurveWithSpread(1.99);

            Assert.Equal(0.0, valuationResults.Npv);
        }

        [Fact]
        public void Calculate_ZeroInventoryForwardSpreadLessThanCycleCost_ResultWithZerosDecisionProfile()
        {
            // Cycle cost = 2.0, so create curve with spread of 1.99
            var valuationResults = IntrinsicValuationZeroInventoryForwardCurveWithSpread(1.99);

            AssertDecisionProfileAllZeros(valuationResults.StorageProfile,
                                new Day(2019, 9, 15),
                                new Day(2019, 9, 30));
        }

        private static IntrinsicStorageValuationResults<Day> IntrinsicValuationZeroInventoryForwardCurveWithSpread(double forwardSpread)
        {
            var currentPeriod = new Day(2019, 9, 15);
            const double lowerForwardPrice = 56.6;
            double higherForwardPrice = lowerForwardPrice + forwardSpread;

            var forwardCurveBuilder = new TimeSeries<Day, double>.Builder();

            foreach (var day in new Day(2019, 9, 15).EnumerateTo(new Day(2019, 9, 22)))
            {
                forwardCurveBuilder.Add(day, lowerForwardPrice);
            }

            foreach (var day in new Day(2019, 9, 23).EnumerateTo(new Day(2019, 9, 30)))
            {
                forwardCurveBuilder.Add(day, higherForwardPrice);
            }

            IntrinsicStorageValuationResults<Day> valuationResults = GenerateValuationResults(0.0,
                                                                            forwardCurveBuilder.Build(), currentPeriod);
            return valuationResults;
        }

        [Fact]
        public void Calculate_ZeroInventoryForwardSpreadHigherThanCycleCost_ResultWithNetPresentValueSpreadMinusCycleCostTimesVolume()
        {
            // Cycle cost = 2.0, so create curve with spread of 2.01
            var valuationResults = IntrinsicValuationZeroInventoryForwardCurveWithSpread(2.01);

            double totalVolumeInjectable = 7 * 45.5; // 7 with maximum withdrawal
            double expectedNpv = totalVolumeInjectable * 0.01; // Volume * (forward spread - 2.0 cycle cost)

            Assert.Equal(expectedNpv, valuationResults.Npv, 10);
        }

        [Fact]
        public void Calculate_CurrentPeriodAfterStorageEnd_ResultWithZeroNetPresentValue()
        {
            var currentPeriod = new Day(2019, 10, 1);

            IntrinsicStorageValuationResults<Day> valuationResults = GenerateValuationResults(0.0,
                                                                        TimeSeries<Day, double>.Empty, currentPeriod);

            Assert.Equal(0.0, valuationResults.Npv);
        }

        [Fact]
        public void Calculate_CurrentPeriodAfterStorageEnd_ResultWithEmptyDecisionProfile()
        {
            var currentPeriod = new Day(2019, 10, 1);

            IntrinsicStorageValuationResults<Day> valuationResults = GenerateValuationResults(0.0,
                                                                        TimeSeries<Day, double>.Empty, currentPeriod);

            Assert.True(valuationResults.StorageProfile.IsEmpty);
        }

        [Fact]
        public void Calculate_CurrentPeriodEqualToStorageEndStorageMustBeEmptyAtEnd_ResultWithZeroNetPresentValue()
        {
            var currentPeriod = new Day(2019, 9, 30);

            IntrinsicStorageValuationResults<Day> valuationResults = GenerateValuationResults(0.0,
                TimeSeries<Day, double>.Empty, currentPeriod);

            Assert.Equal(0.0, valuationResults.Npv);
        }

        [Fact]
        public void Calculate_CurrentPeriodEqualToStorageEndStorageMustBeEmptyAtEnd_ResultWithEmptyDecisionProfile()
        {
            var currentPeriod = new Day(2019, 9, 30);

            IntrinsicStorageValuationResults<Day> valuationResults = GenerateValuationResults(0.0,
                TimeSeries<Day, double>.Empty, currentPeriod);

            Assert.True(valuationResults.StorageProfile.IsEmpty);
        }

        private static IntrinsicStorageValuationResults<Day> 
            GenerateValuationResults_CurrentPeriodEqualToStorageEndStorageInventoryHasTerminalValue(double startingInventory, 
                                                                                                    double forwardPrice)
        {
            var storageStart = new Day(2019, 9, 1);
            var storageEnd = new Day(2019, 9, 30);

            TimeSeries<Month, Day> settlementDates = new TimeSeries<Month, Day>.Builder
                {
                    {new Month(2019, 9),  new Day(2019, 10, 5)}
                }.Build();

            CmdtyStorage<Day> storage = CmdtyStorage<Day>.Builder
                .WithActiveTimePeriod(storageStart, storageEnd)
                .WithConstantInjectWithdrawRange(-45.5, 56.6)
                .WithConstantMinInventory(0.0)
                .WithConstantMaxInventory(1000.0)
                .WithPerUnitInjectionCost(0.8, injectionDate => injectionDate)
                .WithNoCmdtyConsumedOnInject()
                .WithPerUnitWithdrawalCost(1.2, withdrawalDate => withdrawalDate)
                .WithNoCmdtyConsumedOnWithdraw()
                .WithNoCmdtyInventoryLoss()
                .WithNoInventoryCost()
                .WithTerminalInventoryNpv((cmdtyPrice, terminalInventory) => cmdtyPrice * terminalInventory - 999.0)
                .Build();

            var forwardCurve = new TimeSeries<Day, double>.Builder(1)
                            {
                                {storageEnd, forwardPrice }
                            }.Build();

            IntrinsicStorageValuationResults<Day> valuationResults = IntrinsicStorageValuation<Day>
                .ForStorage(storage)
                .WithStartingInventory(startingInventory)
                .ForCurrentPeriod(storageEnd)
                .WithForwardCurve(forwardCurve)
                .WithMonthlySettlement(settlementDates)
                .WithDiscountFactorFunc((valuationDate, cashFlowDate) => 1.0) // No discounting
                .WithFixedGridSpacing(10.0)
                .WithLinearInventorySpaceInterpolation()
                .WithNumericalTolerance(1E-10)
                .Calculate();

            return valuationResults;
        }
        
        [Fact]
        public void Calculate_CurrentPeriodEqualToStorageEndStorageInventoryHasTerminalValue_ResultWithNetPresentValueEqualToTerminalValue()
        {
            const double terminalInventory = 120.1;
            const double forwardPriceForEndDate = 45.67;

            IntrinsicStorageValuationResults<Day> valuationResults = 
                GenerateValuationResults_CurrentPeriodEqualToStorageEndStorageInventoryHasTerminalValue(terminalInventory, forwardPriceForEndDate);

            double expectedNpv = terminalInventory * forwardPriceForEndDate - 999.0; // Arbitrary terminal function defined for storage

            Assert.Equal(expectedNpv, valuationResults.Npv);
        }

        [Fact]
        public void Calculate_CurrentPeriodEqualToStorageEndStorageInventoryHasTerminalValue_ResultWithEmptyDecisionProfile()
        {
            const double terminalInventory = 120.1;
            const double forwardPriceForEndDate = 45.67;

            IntrinsicStorageValuationResults<Day> valuationResults =
                GenerateValuationResults_CurrentPeriodEqualToStorageEndStorageInventoryHasTerminalValue(terminalInventory, forwardPriceForEndDate);

            Assert.True(valuationResults.StorageProfile.IsEmpty);
        }


        // TODO test cases:
        // Empty + spread more than inject + withdraw cost = value is spread minus costs, profile has inject withdraw
        // Inventory + curve backwardated: value is highest part of curve * volume - withdraw cost, profile is in highest part of curve
        // Inventory + spread less than inject + withdraw cost + terminal value = terminal value and zero profile (discounted?)
        // Single period with high forward price: npv and profile all in this part
        // Decision profile * forward curve discounted = NPV
        // Multiple cycles
        // Inject and withdraw rates equal (or multiples), net profile will be zero OR

    }
}
