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
using Cmdty.Storage;
using Cmdty.TimePeriodValueTypes;
using Cmdty.TimeSeries;
d
const double constantInjectionCost = 0.48;
const double constantWithdrawalCost = 0.74;

var injectWithdrawConstraints = new List<InjectWithdrawRangeByInventoryAndPeriod<Day>>
{
    (period: new Day(2019, 9, 1), injectWithdrawRanges: new List<InjectWithdrawRangeByInventory>
    {
        (inventory: 0.0, (minInjectWithdrawRate: -44.85, maxInjectWithdrawRate: 56.8)), // Inventory empty, highest injection rate
        (inventory: 100.0, (minInjectWithdrawRate: -45.01, maxInjectWithdrawRate: 54.5)),
        (inventory: 300.0, (minInjectWithdrawRate: -45.78, maxInjectWithdrawRate: 52.01)),
        (inventory: 600.0, (minInjectWithdrawRate: -46.17, maxInjectWithdrawRate: 51.9)),
        (inventory: 800.0, (minInjectWithdrawRate: -46.99, maxInjectWithdrawRate: 50.8)),
        (inventory: 1000.0, (minInjectWithdrawRate: -47.12, maxInjectWithdrawRate: 50.01)) // Inventory full, highest withdrawal rate
    }),
    (period: new Day(2019, 9, 20), injectWithdrawRanges: new List<InjectWithdrawRangeByInventory>
    {
        (inventory: 0.0, (minInjectWithdrawRate: -31.41, maxInjectWithdrawRate: 48.33)), // Inventory empty, highest injection rate
        (inventory: 100.0, (minInjectWithdrawRate: -31.85, maxInjectWithdrawRate: 43.05)),
        (inventory: 300.0, (minInjectWithdrawRate: -31.68, maxInjectWithdrawRate: 41.22)),
        (inventory: 600.0, (minInjectWithdrawRate: -32.78, maxInjectWithdrawRate: 40.08)),
        (inventory: 800.0, (minInjectWithdrawRate: -33.05, maxInjectWithdrawRate: 39.74)),
        (inventory: 1000.0, (minInjectWithdrawRate: -34.80, maxInjectWithdrawRate: 38.51)) // Inventory full, highest withdrawal rate
    })
};

CmdtyStorage<Day> storage = CmdtyStorage<Day>.Builder
    .WithActiveTimePeriod(new Day(2019, 9, 1), new Day(2019, 10, 1))
    .WithTimeAndInventoryVaryingInjectWithdrawRatesPiecewiseLinear(injectWithdrawConstraints)
    .WithPerUnitInjectionCost(constantInjectionCost, injectionDate => injectionDate)
    .WithNoCmdtyConsumedOnInject()
    .WithPerUnitWithdrawalCost(constantWithdrawalCost, withdrawalDate => withdrawalDate)
    .WithNoCmdtyConsumedOnWithdraw()
    .WithNoCmdtyInventoryLoss()
    .WithNoInventoryCost()
    .MustBeEmptyAtEnd()
    .Build();

var currentPeriod = new Day(2019, 9, 15);

const double lowerForwardPrice = 56.6;
const double forwardSpread = 87.81;

double higherForwardPrice = lowerForwardPrice + forwardSpread;

var forwardCurveBuilder = new TimeSeries<Day, double>.Builder();

foreach (var day in new Day(2019, 9, 15).EnumerateTo(new Day(2019, 9, 22)))
    forwardCurveBuilder.Add(day, lowerForwardPrice);

foreach (var day in new Day(2019, 9, 23).EnumerateTo(new Day(2019, 10, 1)))
    forwardCurveBuilder.Add(day, higherForwardPrice);

TimeSeries<Month, Day> cmdtySettlementDates = new TimeSeries<Month, Day>.Builder
    {
        {new Month(2019, 9), new Day(2019, 10, 20) }
    }.Build();

const double interestRate = 0.025;

// Trinomial tree model parameters
const double spotPriceMeanReversion = 5.5;
const double onePeriodTimeStep = 1.0 / 365.0;

TimeSeries<Day, double> spotVolatility = new TimeSeries<Day, double>.Builder
    {
        {new Day(2019, 9, 15),  0.975},
        {new Day(2019, 9, 16),  0.97},
        {new Day(2019, 9, 17),  0.96},
        {new Day(2019, 9, 18),  0.91},
        {new Day(2019, 9, 19),  0.89},
        {new Day(2019, 9, 20),  0.895},
        {new Day(2019, 9, 21),  0.891},
        {new Day(2019, 9, 22),  0.89},
        {new Day(2019, 9, 23),  0.875},
        {new Day(2019, 9, 24),  0.872},
        {new Day(2019, 9, 25),  0.871},
        {new Day(2019, 9, 26),  0.870},
        {new Day(2019, 9, 27),  0.869},
        {new Day(2019, 9, 28),  0.868},
        {new Day(2019, 9, 29),  0.867},
        {new Day(2019, 9, 30),  0.866},
        {new Day(2019, 10, 1),  0.8655}
    }.Build();

const double startingInventory = 50.0;

TreeStorageValuationResults<Day> valuationResults = TreeStorageValuation<Day>
    .ForStorage(storage)
    .WithStartingInventory(startingInventory)
    .ForCurrentPeriod(currentPeriod)
    .WithForwardCurve(forwardCurveBuilder.Build())
    .WithOneFactorTrinomialTree(spotVolatility, spotPriceMeanReversion, onePeriodTimeStep)
    .WithMonthlySettlement(cmdtySettlementDates)
    .WithAct365ContinuouslyCompoundedInterestRate(settleDate => interestRate)
    .WithFixedGridSpacing(10.0)
    .WithLinearInventorySpaceInterpolation()
    .WithNumericalTolerance(1E-12)
    .Calculate();

Console.WriteLine("Calculated storage NPV: " + valuationResults.NetPresentValue.ToString("N2"));
Console.WriteLine();

Console.WriteLine("Press any key to exit");
Console.ReadKey();
