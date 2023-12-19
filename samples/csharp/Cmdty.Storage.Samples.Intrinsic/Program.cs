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
using Cmdty.Storage;
using Cmdty.TimePeriodValueTypes;
using Cmdty.TimeSeries;

const double constantMaxInjectRate = 5.26;
const double constantMaxWithdrawRate = 14.74;
const double constantMaxInventory = 1100.74;
const double constantMinInventory = 0.0;
const double constantInjectionCost = 0.48;
const double constantWithdrawalCost = 0.74;

CmdtyStorage<Day> storage = CmdtyStorage<Day>.Builder
    .WithActiveTimePeriod(new Day(2019, 9, 1), new Day(2019, 10, 1))
    .WithConstantInjectWithdrawRange(-constantMaxWithdrawRate, constantMaxInjectRate)
    .WithConstantMinInventory(constantMinInventory)
    .WithConstantMaxInventory(constantMaxInventory)
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

const double startingInventory = 50.0;

IntrinsicStorageValuationResults<Day> valuationResults = IntrinsicStorageValuation<Day>
    .ForStorage(storage)
    .WithStartingInventory(startingInventory)
    .ForCurrentPeriod(currentPeriod)
    .WithForwardCurve(forwardCurveBuilder.Build())
    .WithCmdtySettlementRule(day => day.First<Month>().Offset(1).First<Day>().Offset(5)) // Commodity is settled on the 5th day of the next month
    .WithDiscountFactorFunc((valDate, cfDate) => 1.0) // Assumes no discounting (don't do this in practice)
    .WithFixedGridSpacing(10.0)
    .WithLinearInventorySpaceInterpolation()
    .WithNumericalTolerance(1E-12)
    .Calculate();

Console.WriteLine("Calculated intrinsic storage NPV: " + valuationResults.Npv.ToString("N2"));
Console.WriteLine();

Console.WriteLine("Press any key to exit");
Console.ReadKey();
