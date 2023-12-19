#region License
// Copyright (c) 2023 Jake Fowler
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
using Cmdty.Core.Simulation.MultiFactor;
using Cmdty.Storage;
using Cmdty.TimePeriodValueTypes;
using Cmdty.TimeSeries;

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

var storageCapacityStart = new Day(2019, 9, 1);
var storageCapacityEnd = new Day(2019, 10, 1);

CmdtyStorage<Day> storage = CmdtyStorage<Day>.Builder
    .WithActiveTimePeriod(storageCapacityStart, storageCapacityEnd)
    .WithTimeAndInventoryVaryingInjectWithdrawRatesPiecewiseLinear(injectWithdrawConstraints)
    .WithPerUnitInjectionCost(constantInjectionCost, injectionDate => injectionDate)
    .WithNoCmdtyConsumedOnInject()
    .WithPerUnitWithdrawalCost(constantWithdrawalCost, withdrawalDate => withdrawalDate)
    .WithNoCmdtyConsumedOnWithdraw()
    .WithNoCmdtyInventoryLoss()
    .WithNoInventoryCost()
    .MustBeEmptyAtEnd()
    .Build();

const double lowerForwardPrice = 56.6;
const double forwardSpread = 87.81;

double higherForwardPrice = lowerForwardPrice + forwardSpread;

var forwardCurveBuilder = new TimeSeries<Day, double>.Builder();

foreach (var day in storageCapacityStart.EnumerateTo(new Day(2019, 9, 22)))
    forwardCurveBuilder.Add(day, lowerForwardPrice);

foreach (var day in new Day(2019, 9, 23).EnumerateTo(storageCapacityEnd))
    forwardCurveBuilder.Add(day, higherForwardPrice);


const double flatInterestRate = 0.055;

// Trinomial tree model parameters
const double longTermVol = 0.17;
const double seasonalVol = 0.32;
const double spotFactorVol = 0.7;
const double spotFactorMeanReversionRate = 90.6;

MultiFactorParameters<Day> threeFactorParameters = MultiFactorParameters.For3FactorSeasonal(spotFactorMeanReversionRate, spotFactorVol,
    longTermVol, seasonalVol, storage.StartPeriod, storage.EndPeriod);

const double startingInventory = 50.0;

const int regressMaxDegree = 3;
const int numInventorySpacePoints = 50;
const int numSims = 500;
const int randomSeed = 11;

var valuationParameters = new LsmcValuationParameters<Day>.Builder
    {
        BasisFunctions = BasisFunctionsBuilder.Ones +
                         BasisFunctionsBuilder.AllMarkovFactorAllPositiveIntegerPowersUpTo(regressMaxDegree, 1) + Sim.Spot,
        CurrentPeriod = new Day(2019, 8, 29),
        DiscountFactors = StorageHelper.CreateAct65ContCompDiscounter(flatInterestRate),
        ForwardCurve = forwardCurveBuilder.Build(),
        GridCalc = FixedSpacingStateSpaceGridCalc.CreateForFixedNumberOfPointsOnGlobalInventoryRange(storage, numInventorySpacePoints),
        Inventory = startingInventory,
        Storage = storage,
        SettleDateRule = deliveryDate => Month.FromDateTime(deliveryDate.Start).Offset(1).First<Day>() + 19, // Settlement on 20th of following month (business days ignore for simplicity),
        SimulationDataReturned = SimulationDataReturned.AllSpotPrices | SimulationDataReturned.AllFactors
    }
    .SimulateWithMultiFactorModelAndMersenneTwister(threeFactorParameters, numSims, randomSeed)
    .Build();

LsmcStorageValuationResults<Day> results = LsmcStorageValuation.WithNoLogger.Calculate(valuationParameters);

Console.WriteLine("Calculated storage NPV: " + results.Npv.ToString("N2"));
Console.WriteLine();

Console.WriteLine("Press any key to exit");
Console.ReadKey();

