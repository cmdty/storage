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
using System.Globalization;
using System.Linq;
using Cmdty.Core.Common;
using Cmdty.Core.Simulation;
using Cmdty.Storage.PythonHelpers;
using Cmdty.TimePeriodValueTypes;
using Cmdty.TimeSeries;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using MathNet.Numerics.LinearAlgebra.Factorization;
using MathNet.Numerics.Statistics;
using Microsoft.Extensions.Logging;

namespace Cmdty.Storage
{
    public class LsmcStorageValuation
    {
        private readonly ILogger<LsmcStorageValuation> _logger;

        // This has been very roughly estimated. Probably there is a better way of splitting up progress by estimating the order of the backward and forward components.
        private const double BackwardPcntTime = 0.66;

        public LsmcStorageValuation(ILogger<LsmcStorageValuation> logger = null)
        {
            _logger = logger;
        }

        public static LsmcStorageValuation WithNoLogger => new LsmcStorageValuation();
        
        public LsmcStorageValuationResults<T> Calculate<T>(LsmcValuationParameters<T> lsmcParams)
            where T : ITimePeriod<T>
        {
            // TODO split this very long method up into several called sub-methods
            var stopwatches = new Stopwatches();
            stopwatches.All.Start();

            if (lsmcParams.Inventory < 0)
                throw new ArgumentException("Inventory cannot be negative.", nameof(lsmcParams.Inventory));

            if (lsmcParams.CurrentPeriod.CompareTo(lsmcParams.Storage.EndPeriod) > 0)
            {
                lsmcParams.OnProgressUpdate?.Invoke(1.0);
                return LsmcStorageValuationResults<T>.CreateExpiredResults();
            }

            if (lsmcParams.CurrentPeriod.Equals(lsmcParams.Storage.EndPeriod))
            {
                if (lsmcParams.Storage.MustBeEmptyAtEnd)
                {
                    if (lsmcParams.Inventory > 0)
                        throw new InventoryConstraintsCannotBeFulfilledException("Storage must be empty at end, but inventory is greater than zero.");
                    lsmcParams.OnProgressUpdate?.Invoke(1.0);
                    return LsmcStorageValuationResults<T>.CreateExpiredResults();
                }
                // Potentially P&L at end
                double spotPrice = lsmcParams.ForwardCurve[lsmcParams.CurrentPeriod];
                double npv = lsmcParams.Storage.TerminalStorageNpv(spotPrice, lsmcParams.Inventory);
                lsmcParams.OnProgressUpdate?.Invoke(1.0);
                return LsmcStorageValuationResults<T>.CreateEndPeriodResults(npv);
            }

            var basisFunctionList = lsmcParams.BasisFunctions.ToList();

            TimeSeries<T, InventoryRange> inventorySpace = StorageHelper.CalculateInventorySpace(lsmcParams.Storage, lsmcParams.Inventory, lsmcParams.CurrentPeriod);
            T startActiveStorage = inventorySpace.Start.Offset(-1);

            if (lsmcParams.ForwardCurve.Start.CompareTo(startActiveStorage) > 0)
                throw new ArgumentException($"Forward curve starts too late. Must start on or before the period {startActiveStorage}.", nameof(lsmcParams.ForwardCurve));

            if (lsmcParams.ForwardCurve.End.CompareTo(inventorySpace.End) < 0)
                throw new ArgumentException("Forward curve does not extend until storage end period.", nameof(lsmcParams.ForwardCurve));

            // Perform backward induction
            _logger?.LogInformation("Starting regression spot price simulation.");
            stopwatches.RegressionPriceSimulation.Start();
            ISpotSimResults<T> regressionSpotSims = lsmcParams.RegressionSpotSimsGenerator();
            stopwatches.RegressionPriceSimulation.Stop();
            _logger?.LogInformation("Spot regression price simulation complete.");

            int numPeriods = inventorySpace.Count + 1; // +1 as inventorySpaceGrid doesn't contain first period
            var inventorySpaceGrids = new double[numPeriods][];

            // Calculate NPVs at end period
            (double endMinInventory, double endMaxInventory) = inventorySpace[lsmcParams.Storage.EndPeriod];
            double[] endInventorySpaceGrid = lsmcParams.GridCalc.GetGridPoints(endMinInventory, endMaxInventory)
                                            .ToArray();
            inventorySpaceGrids[numPeriods - 1] = endInventorySpaceGrid;

            var storageActualValuesNextPeriod = new Vector<double>[endInventorySpaceGrid.Length];
            ReadOnlySpan<double> endPeriodSimSpotPrices = regressionSpotSims.SpotPricesForPeriod(lsmcParams.Storage.EndPeriod).Span;

            int numSims = regressionSpotSims.NumSims;
            double numSimsSqrt = Math.Sqrt(numSims);

            for (int i = 0; i < endInventorySpaceGrid.Length; i++)
            {
                double inventory = endInventorySpaceGrid[i];
                var storageValueBySim = new DenseVector(numSims);
                for (int simIndex = 0; simIndex < numSims; simIndex++)
                {
                    double simSpotPrice = endPeriodSimSpotPrices[simIndex];
                    storageValueBySim[simIndex] = lsmcParams.Storage.TerminalStorageNpv(simSpotPrice, inventory);
                }
                storageActualValuesNextPeriod[i] = storageValueBySim;
            }
            
            // Calculate discount factor function
            Day dayToDiscountTo = lsmcParams.CurrentPeriod.First<Day>(); // TODO add valuation date to LsmcValuationParameters?

            // Memoize the discount factor
            var discountFactorCache = new Dictionary<Day, double>(); // TODO do this in more elegant way and share with intrinsic calc
            double DiscountToCurrentDay(Day cashFlowDate)
            {
                if (!discountFactorCache.TryGetValue(cashFlowDate, out double discountFactor))
                {
                    discountFactor = lsmcParams.DiscountFactors(dayToDiscountTo, cashFlowDate);
                    discountFactorCache[cashFlowDate] = discountFactor;
                }
                return discountFactor;
            }

            Matrix<double> designMatrix = Matrix<double>.Build.Dense(numSims, basisFunctionList.Count);
            for (int i = 0; i < numSims; i++)
                designMatrix[i, 0] = 1.0;

            // Reuse heap memory
            Matrix<double> qTranspose = Matrix<double>.Build.Dense(basisFunctionList.Count, numSims);
            Matrix<double> pseudoInverse = Matrix<double>.Build.Dense(basisFunctionList.Count, numSims);

            // Loop back through other periods
            T[] periodsForResultsTimeSeries = startActiveStorage.EnumerateTo(inventorySpace.End).ToArray();

            var regressCoeffsBuilder = new TimeSeries<T, Panel<int, double>>.Builder(periodsForResultsTimeSeries.Length - 1);

            int backCounter = numPeriods - 2;
            Vector<double> numSimsMemoryBuffer = Vector<double>.Build.Dense(numSims); // Heap memory that will be reused
            double progress = 0.0;
            double backStepProgressPcnt = BackwardPcntTime / (periodsForResultsTimeSeries.Length - 1);

            double[] currentPeriodContinuationValues = null;
            _logger?.LogInformation("Starting backward induction.");
            stopwatches.BackwardInduction.Start();
            foreach (T period in periodsForResultsTimeSeries.Reverse().Skip(1))
            {
                double[] nextPeriodInventorySpaceGrid = inventorySpaceGrids[backCounter + 1];
                Vector<double>[] storageRegressValuesNextPeriod = new Vector<double>[nextPeriodInventorySpaceGrid.Length];

                if (period.Equals(lsmcParams.CurrentPeriod))
                {
                    currentPeriodContinuationValues = new double[nextPeriodInventorySpaceGrid.Length];
                    // Current period, for which the price isn't random so expected storage values are just the average of the values for all sims
                    for (int i = 0; i < nextPeriodInventorySpaceGrid.Length; i++)
                    {
                        Vector<double> storageValuesBySimNextPeriod = storageActualValuesNextPeriod[i];
                        double expectedStorageValueNextPeriod = storageValuesBySimNextPeriod.Average();
                        storageRegressValuesNextPeriod[i] = Vector<double>.Build.Dense(numSims, expectedStorageValueNextPeriod); // TODO this is a bit inefficent, review
                        currentPeriodContinuationValues[i] = expectedStorageValueNextPeriod;
                    }
                }
                else
                {
                    // TODO option to use SVD rather than QR for regression. Will be slower, but will function with design matrix collinearity.
                    // TODO normalise mean and standard deviation of regressors for better stability
                    // TODO perform regression by direct call to Intel MKL dgels/dgelss, 
                    PopulateDesignMatrix(designMatrix, period, regressionSpotSims, basisFunctionList);
                    stopwatches.PseudoInverse.Start();
                    QR<double> designMatrixQr = designMatrix.QR(QRMethod.Thin);
                    Matrix<double> rInverse = designMatrixQr.R.Inverse();
                    designMatrixQr.Q.Transpose(qTranspose);
                    rInverse.Multiply(qTranspose, pseudoInverse);
                    stopwatches.PseudoInverse.Stop();

                    var thisPeriodRegressCoeffs = new Panel<int, double>(Enumerable.Range(0, nextPeriodInventorySpaceGrid.Length), basisFunctionList.Count);
                    // TODO doing the regressions for all next inventory could be inefficient as they might not all be needed
                    for (int i = 0; i < nextPeriodInventorySpaceGrid.Length; i++)
                    {
                        Vector<double> storageValuesBySimNextPeriod = storageActualValuesNextPeriod[i];
                        Vector<double> regressResults = pseudoInverse.Multiply(storageValuesBySimNextPeriod);
                        Vector<double> estimatedContinuationValues = designMatrix.Multiply(regressResults);
                        storageRegressValuesNextPeriod[i] = estimatedContinuationValues;
                        // Save regression coeffs for later use
                        Span<double> regressCoeffsSpan = thisPeriodRegressCoeffs[i];
                        for (int j = 0; j < regressCoeffsSpan.Length; j++)
                            regressCoeffsSpan[j] = regressResults[j];
                    }
                    regressCoeffsBuilder.Add(period, thisPeriodRegressCoeffs); // Key for regressCoeffs is period of simulated prices/factors, i.e. the regressor, which is the period before the period of continuation value being approximated
                }
                
                double[] inventorySpaceGrid;
                if (period.Equals(startActiveStorage))
                    inventorySpaceGrid = new[] { lsmcParams.Inventory };
                else
                {
                    (double inventorySpaceMin, double inventorySpaceMax) = inventorySpace[period];
                    inventorySpaceGrid = lsmcParams.GridCalc.GetGridPoints(inventorySpaceMin, inventorySpaceMax)
                                                .ToArray();
                }
                (double nextStepInventorySpaceMin, double nextStepInventorySpaceMax) = inventorySpace[period.Offset(1)];

                var storageActualValuesThisPeriod = new Vector<double>[inventorySpaceGrid.Length]; // TODO change type to DenseVector?

                Day cmdtySettlementDate = lsmcParams.SettleDateRule(period);
                double discountFactorFromCmdtySettlement = DiscountToCurrentDay(cmdtySettlementDate);

                ReadOnlySpan<double> simulatedPrices;
                if (period.Equals(lsmcParams.CurrentPeriod))
                {
                    double spotPrice = lsmcParams.ForwardCurve[period];
                    simulatedPrices = Enumerable.Repeat(spotPrice, numSims).ToArray(); // TODO inefficient - review.
                }                
                else
                    simulatedPrices = regressionSpotSims.SpotPricesForPeriod(period).Span;

                for (int inventoryIndex = 0; inventoryIndex < inventorySpaceGrid.Length; inventoryIndex++)
                {
                    double inventory = inventorySpaceGrid[inventoryIndex];
                    InjectWithdrawRange injectWithdrawRange = lsmcParams.Storage.GetInjectWithdrawRange(period, inventory);
                    double inventoryLoss = lsmcParams.Storage.CmdtyInventoryPercentLoss(period) * inventory;
                    double[] decisionSet = StorageHelper.CalculateBangBangDecisionSet(injectWithdrawRange, inventory, inventoryLoss,
                        nextStepInventorySpaceMin, nextStepInventorySpaceMax, lsmcParams.NumericalTolerance, lsmcParams.ExtraDecisions);
                    IReadOnlyList<DomesticCashFlow> inventoryCostCashFlows = lsmcParams.Storage.CmdtyInventoryCost(period, inventory);
                    double inventoryCostNpv = inventoryCostCashFlows.Sum(cashFlow => cashFlow.Amount * DiscountToCurrentDay(cashFlow.Date));

                    double[] injectWithdrawCostNpvs = new double[decisionSet.Length];
                    double[] cmdtyUsedForInjectWithdrawVolume = new double[decisionSet.Length];
                    
                    var regressionContinuationValueByDecisionSet = new Vector<double>[decisionSet.Length];
                    var actualContinuationValueByDecisionSet = new Vector<double>[decisionSet.Length];
                    for (int decisionIndex = 0; decisionIndex < decisionSet.Length; decisionIndex++)
                    {
                        double decisionVolume = decisionSet[decisionIndex];

                        // Inject/Withdraw cost (same for all price sims)
                        injectWithdrawCostNpvs[decisionIndex] = InjectWithdrawCostNpv(lsmcParams.Storage, decisionVolume, period, inventory, DiscountToCurrentDay);

                        // Cmdty Used For Inject/Withdraw (same for all price sims)
                        cmdtyUsedForInjectWithdrawVolume[decisionIndex] = CmdtyVolumeConsumedOnDecision(lsmcParams.Storage, decisionVolume, period, inventory);

                        // Calculate continuation values
                        double inventoryAfterDecision = inventory + decisionVolume - inventoryLoss;
                        for (int inventoryGridIndex = 0; inventoryGridIndex < nextPeriodInventorySpaceGrid.Length; inventoryGridIndex++) // TODO use binary search?
                        {
                            double nextPeriodInventory = nextPeriodInventorySpaceGrid[inventoryGridIndex];
                            if (Math.Abs(nextPeriodInventory - inventoryAfterDecision) < 1E-8) // TODO get rid of hard coded constant
                            {
                                regressionContinuationValueByDecisionSet[decisionIndex] = storageRegressValuesNextPeriod[inventoryGridIndex];
                                actualContinuationValueByDecisionSet[decisionIndex] = storageActualValuesNextPeriod[inventoryGridIndex];
                                break;
                            }
                            if (nextPeriodInventory > inventoryAfterDecision)
                            {
                                // Linearly interpolate inventory space
                                double lowerInventory = nextPeriodInventorySpaceGrid[inventoryGridIndex - 1];
                                double upperInventory = nextPeriodInventory;
                                double inventoryGridSpace = upperInventory - lowerInventory;
                                double lowerWeight = (upperInventory - inventoryAfterDecision) / inventoryGridSpace;
                                double upperWeight = 1.0 - lowerWeight;
                                
                                // Regression storage values
                                Vector<double> lowerRegressStorageValues = storageRegressValuesNextPeriod[inventoryGridIndex - 1];
                                Vector<double> upperRegressStorageValues = storageRegressValuesNextPeriod[inventoryGridIndex];

                                var interpolatedRegressContinuationValue = 
                                    WeightedAverage<T>(lowerRegressStorageValues, 
                                        lowerWeight, upperRegressStorageValues, upperWeight, numSimsMemoryBuffer);

                                regressionContinuationValueByDecisionSet[decisionIndex] = interpolatedRegressContinuationValue;

                                // Actual (simulated) storage values
                                Vector<double> lowerActualStorageValues = storageActualValuesNextPeriod[inventoryGridIndex - 1];
                                Vector<double> upperActualStorageValues = storageActualValuesNextPeriod[inventoryGridIndex];

                                Vector<double> interpolatedActualContinuationValue =
                                        WeightedAverage<T>(lowerActualStorageValues, lowerWeight, 
                                            upperActualStorageValues, upperWeight, numSimsMemoryBuffer);
                                actualContinuationValueByDecisionSet[decisionIndex] = interpolatedActualContinuationValue;
                                break;
                            }
                        }
                    }

                    var storageValuesBySim = new DenseVector(numSims);
                    var decisionNpvsRegress = new double[decisionSet.Length];
                    for (int simIndex = 0; simIndex < numSims; simIndex++)
                    {
                        double simulatedSpotPrice = simulatedPrices[simIndex];
                        for (var decisionIndex = 0; decisionIndex < decisionSet.Length; decisionIndex++)
                        {
                            double decisionVolume = decisionSet[decisionIndex];

                            double injectWithdrawNpv = -decisionVolume * simulatedSpotPrice * discountFactorFromCmdtySettlement;
                            double cmdtyUsedForInjectWithdrawNpv = -cmdtyUsedForInjectWithdrawVolume[decisionIndex] * simulatedSpotPrice * 
                                                                   discountFactorFromCmdtySettlement;
                            double immediateNpv = injectWithdrawNpv - injectWithdrawCostNpvs[decisionIndex] + cmdtyUsedForInjectWithdrawNpv;

                            double continuationValue = regressionContinuationValueByDecisionSet[decisionIndex][simIndex];

                            double totalNpv = immediateNpv + continuationValue - inventoryCostNpv;
                            decisionNpvsRegress[decisionIndex] = totalNpv;
                        }
                        (double optimalRegressDecisionNpv, int indexOfOptimalDecision) = StorageHelper.MaxValueAndIndex(decisionNpvsRegress);
                        
                        // TODO do this tidier an potentially more efficiently
                        double adjustFromRegressToActualContinuation =  
                                                - regressionContinuationValueByDecisionSet[indexOfOptimalDecision][simIndex]
                                                + actualContinuationValueByDecisionSet[indexOfOptimalDecision][simIndex];
                        double optimalActualDecisionNpv = optimalRegressDecisionNpv + adjustFromRegressToActualContinuation;

                        storageValuesBySim[simIndex] = optimalActualDecisionNpv;
                    }
                    storageActualValuesThisPeriod[inventoryIndex] = storageValuesBySim;
                }

                inventorySpaceGrids[backCounter] = inventorySpaceGrid;
                storageActualValuesNextPeriod = storageActualValuesThisPeriod;
                backCounter--;
                progress += backStepProgressPcnt;
                lsmcParams.OnProgressUpdate?.Invoke(progress);
                lsmcParams.CancellationToken.ThrowIfCancellationRequested();
            }
            stopwatches.BackwardInduction.Stop();
            _logger?.LogInformation("Completed backward induction.");

            _logger?.LogInformation("Starting valuation spot price simulation.");
            stopwatches.ValuationPriceSimulation.Start();
            ISpotSimResults<T> valuationSpotSims = lsmcParams.ValuationSpotSimsGenerator();
            stopwatches.ValuationPriceSimulation.Stop();
            _logger?.LogInformation("Valuation spot price simulation complete.");

            (bool returnSimSpotPriceForRegress, bool returnSimSpotPriceForValuation, bool returnSimFactorsForRegression, bool returnSimFactorsForValuation, 
                    bool returnSimInventory, bool returnSimInjectWithdrawVolume, bool returnSimCmdtyConsumed,
                    bool returnSimInventoryLoss, bool returnSimNetVolume, bool returnSimPv) = ParseSimulationDataReturned(lsmcParams.SimulationDataReturned);

            TimeSeries<T, Panel<int, double>> regressCoeffs = regressCoeffsBuilder.Build();
            var inventoryBySim = returnSimInventory ? new Panel<T, double>(periodsForResultsTimeSeries, numSims) : Panel<T, double>.CreateEmpty();
            var injectWithdrawVolumeBySim = returnSimInjectWithdrawVolume ? new Panel<T, double>(periodsForResultsTimeSeries, numSims) : Panel<T, double>.CreateEmpty();
            var cmdtyConsumedBySim = returnSimCmdtyConsumed ? new Panel<T, double>(periodsForResultsTimeSeries, numSims) : Panel<T, double>.CreateEmpty();
            var inventoryLossBySim = returnSimInventoryLoss ? new Panel<T, double>(periodsForResultsTimeSeries, numSims) : Panel<T, double>.CreateEmpty();
            var netVolumeBySim = returnSimNetVolume ? new Panel<T, double>(periodsForResultsTimeSeries, numSims) : Panel<T, double>.CreateEmpty();
            var pvByPeriodAndSim = returnSimPv ? new Panel<T, double>(periodsForResultsTimeSeries, numSims) : Panel<T, double>.CreateEmpty();
            var storageProfiles = new StorageProfile[periodsForResultsTimeSeries.Length];
            var pvBySim = new double[numSims];

            var deltas = new double[periodsForResultsTimeSeries.Length];
            var deltasStandardErrors = new double[periodsForResultsTimeSeries.Length];

            Span<double> inventoryBuffer1 = returnSimInventory ? Span<double>.Empty : new double[numSims];
            Span<double> inventoryBuffer2 = returnSimInventory ? Span<double>.Empty : new double[numSims];


            Span<double> thisPeriodInventories = returnSimInventory ? inventoryBySim[0] : inventoryBuffer1;
            Span<double> nextPeriodInventories = returnSimInventory ? inventoryBySim[1] : inventoryBuffer2;

            for (int i = 0; i < thisPeriodInventories.Length; i++)
                thisPeriodInventories[i] = lsmcParams.Inventory;

            // Trigger price variables
            int numTriggerPriceVolumes = 10; // TODO move to parameters
            var triggerVolumeProfilesArray = new TriggerPriceVolumeProfiles[periodsForResultsTimeSeries.Length - 1];
            var triggerPricesArray = new TriggerPrices[periodsForResultsTimeSeries.Length - 1];

            double forwardStepProgressPcnt = (1.0 - BackwardPcntTime) / periodsForResultsTimeSeries.Length;
            _logger?.LogInformation("Starting calculations of optimal decisions by simulation forward in time.");
            stopwatches.ForwardSimulation.Start();
            for (int periodIndex = 0; periodIndex < periodsForResultsTimeSeries.Length - 1; periodIndex++) // TODO more clearly handle this -1
            {
                T period = periodsForResultsTimeSeries[periodIndex];
                
                double[] nextPeriodInventorySpaceGrid = inventorySpaceGrids[periodIndex + 1];
                //Vector<double>[] regressContinuationValues = storageRegressValuesByPeriod[periodIndex + 1];
                Vector<double>[] regressContinuationValues = new Vector<double>[nextPeriodInventorySpaceGrid.Length];
                if (period.Equals(lsmcParams.CurrentPeriod))
                {
                    // Current period, for which the price isn't random so expected storage values are just the average of the values for all sims
                    for (int i = 0; i < nextPeriodInventorySpaceGrid.Length; i++)
                    {
                        double expectedStorageValueNextPeriod = currentPeriodContinuationValues[i];
                        regressContinuationValues[i] = Vector<double>.Build.Dense(numSims, expectedStorageValueNextPeriod); // TODO this is a bit inefficent, review
                    }
                }
                else
                {
                    PopulateDesignMatrix(designMatrix, period, valuationSpotSims, basisFunctionList);
                    Panel<int, double> regressCoeffsThisPeriod = regressCoeffs[period];
                    for (int i = 0; i < nextPeriodInventorySpaceGrid.Length; i++)
                    {
                        Span<double> regressCoeffsSpan = regressCoeffsThisPeriod[i];
                        var regressCoeffsVector = Vector<double>.Build.DenseOfArray(regressCoeffsSpan.ToArray());
                        regressContinuationValues[i] = designMatrix * regressCoeffsVector;
                    }
                }

                Day cmdtySettlementDate = lsmcParams.SettleDateRule(period);
                double discountFactorFromCmdtySettlement = DiscountToCurrentDay(cmdtySettlementDate);
                double discountForDeltas = lsmcParams.DiscountDeltas ? discountFactorFromCmdtySettlement : 1.0;
                double sumSpotPriceTimesVolume = 0.0;
                double sumSpotPriceTimesVolumeSquared = 0.0;

                ReadOnlySpan<double> simulatedPrices;
                if (period.Equals(lsmcParams.CurrentPeriod))
                {
                    double spotPrice = lsmcParams.ForwardCurve[period];
                    simulatedPrices = Enumerable.Repeat(spotPrice, numSims).ToArray(); // TODO inefficient - review, and share code with backward induction
                }
                else
                    simulatedPrices = valuationSpotSims.SpotPricesForPeriod(period).Span;
                
                (double nextStepInventorySpaceMin, double nextStepInventorySpaceMax) = inventorySpace[period.Offset(1)];
                thisPeriodInventories = returnSimInventory ? inventoryBySim[periodIndex] 
                                            : (periodIndex == 0 ? thisPeriodInventories : nextPeriodInventories);
                Span<double> thisPeriodInjectWithdrawVolumes = returnSimInjectWithdrawVolume ? injectWithdrawVolumeBySim[periodIndex] : Span<double>.Empty;
                Span<double> thisPeriodCmdtyConsumed = returnSimCmdtyConsumed ? cmdtyConsumedBySim[periodIndex] : Span<double>.Empty;
                Span<double> thisPeriodInventoryLoss = returnSimInventoryLoss ? inventoryLossBySim[periodIndex] : Span<double>.Empty;
                Span<double> thisPeriodNetVolume = returnSimNetVolume ? netVolumeBySim[periodIndex] : Span<double>.Empty;
                Span<double> thisPeriodPv = returnSimPv ? pvByPeriodAndSim[periodIndex] : Span<double>.Empty;
                nextPeriodInventories = returnSimInventory ? inventoryBySim[periodIndex + 1] : 
                    thisPeriodInventories == inventoryBuffer1 ? inventoryBuffer2 : inventoryBuffer1;

                double sumOverSimsInjectWithdrawVolumes, sumOverSimsCmdtyConsumed, sumOverSimsInventoryLoss, sumOverSimsPv;
                sumOverSimsInjectWithdrawVolumes = sumOverSimsCmdtyConsumed = sumOverSimsInventoryLoss = sumOverSimsPv = 0.0;
                for (int simIndex = 0; simIndex < numSims; simIndex++)
                {
                    double simulatedSpotPrice = simulatedPrices[simIndex];
                    double inventory = thisPeriodInventories[simIndex];

                    InjectWithdrawRange injectWithdrawRange = lsmcParams.Storage.GetInjectWithdrawRange(period, inventory);
                    double inventoryLoss = lsmcParams.Storage.CmdtyInventoryPercentLoss(period) * inventory;
                    double[] decisionSet = StorageHelper.CalculateBangBangDecisionSet(injectWithdrawRange, inventory,
                        inventoryLoss, nextStepInventorySpaceMin, nextStepInventorySpaceMax, lsmcParams.NumericalTolerance, lsmcParams.ExtraDecisions);
                    IReadOnlyList<DomesticCashFlow> inventoryCostCashFlows = lsmcParams.Storage.CmdtyInventoryCost(period, inventory);
                    double inventoryCostNpv = inventoryCostCashFlows.Sum(cashFlow => cashFlow.Amount * DiscountToCurrentDay(cashFlow.Date));

                    var decisionNpvsRegress = new double[decisionSet.Length];
                    var cmdtyUsedForInjectWithdrawVolumes = new double[decisionSet.Length];
                    var immediatePv = new double[decisionSet.Length];

                    for (var decisionIndex = 0; decisionIndex < decisionSet.Length; decisionIndex++)
                    {
                        double decisionVolume = decisionSet[decisionIndex];
                        double inventoryAfterDecision = inventory + decisionVolume - inventoryLoss;

                        double cmdtyUsedForInjectWithdrawVolume = CmdtyVolumeConsumedOnDecision(lsmcParams.Storage, decisionVolume, period, inventory);

                        double injectWithdrawNpv = -decisionVolume * simulatedSpotPrice * discountFactorFromCmdtySettlement;
                        double cmdtyUsedForInjectWithdrawNpv = -cmdtyUsedForInjectWithdrawVolume * simulatedSpotPrice * discountFactorFromCmdtySettlement;

                        double injectWithdrawCostNpv = InjectWithdrawCostNpv(lsmcParams.Storage, decisionVolume, period, inventory, DiscountToCurrentDay);

                        double immediateNpv = injectWithdrawNpv - injectWithdrawCostNpv + cmdtyUsedForInjectWithdrawNpv - inventoryCostNpv;

                        double continuationValue =
                            InterpolateContinuationValue(inventoryAfterDecision, nextPeriodInventorySpaceGrid, regressContinuationValues, simIndex, lsmcParams.NumericalTolerance);

                        double totalNpv = immediateNpv + continuationValue; 
                        decisionNpvsRegress[decisionIndex] = totalNpv;
                        cmdtyUsedForInjectWithdrawVolumes[decisionIndex] = cmdtyUsedForInjectWithdrawVolume;
                        immediatePv[decisionIndex] = immediateNpv;
                    }
                    (double _, int indexOfOptimalDecision) = StorageHelper.MaxValueAndIndex(decisionNpvsRegress);
                    double optimalDecisionVolume = decisionSet[indexOfOptimalDecision];
                    double optimalNextStepInventory = inventory + optimalDecisionVolume - inventoryLoss;
                    nextPeriodInventories[simIndex] = optimalNextStepInventory;

                    double optimalCmdtyUsedForInjectWithdrawVolume = cmdtyUsedForInjectWithdrawVolumes[indexOfOptimalDecision];

                    double spotPriceTimesVolume = -(optimalDecisionVolume + optimalCmdtyUsedForInjectWithdrawVolume) * simulatedSpotPrice;
                    sumSpotPriceTimesVolume += spotPriceTimesVolume;
                    sumSpotPriceTimesVolumeSquared += spotPriceTimesVolume * spotPriceTimesVolume;

                    if (returnSimInjectWithdrawVolume)
                        thisPeriodInjectWithdrawVolumes[simIndex] = optimalDecisionVolume;
                    sumOverSimsInjectWithdrawVolumes += optimalDecisionVolume;
                    if (returnSimCmdtyConsumed)
                        thisPeriodCmdtyConsumed[simIndex] = optimalCmdtyUsedForInjectWithdrawVolume;
                    sumOverSimsCmdtyConsumed += optimalCmdtyUsedForInjectWithdrawVolume;
                    if (returnSimInventoryLoss)
                        thisPeriodInventoryLoss[simIndex] = inventoryLoss;
                    sumOverSimsInventoryLoss += inventoryLoss;
                    if (returnSimNetVolume)
                        thisPeriodNetVolume[simIndex] = -optimalDecisionVolume - optimalCmdtyUsedForInjectWithdrawVolume;
                    double optimalImmediatePv = immediatePv[indexOfOptimalDecision];
                    if (returnSimPv)
                        thisPeriodPv[simIndex] = optimalImmediatePv;
                    sumOverSimsPv += optimalImmediatePv;
                    pvBySim[simIndex] += optimalImmediatePv;
                }

                double expectedInventory = Average(thisPeriodInventories);
                storageProfiles[periodIndex] = new StorageProfile(expectedInventory, sumOverSimsInjectWithdrawVolumes/numSims,
                    sumOverSimsCmdtyConsumed/numSims, sumOverSimsInventoryLoss/numSims, sumOverSimsPv/numSims);
                double forwardPrice = lsmcParams.ForwardCurve[period];
                // Pathwise differentiation calculation makes assumption that simulated spot price is calculated as forward prices times some stochastic term.
                // This is fine for the multifactor model in Cmdty.Core, but will not be the case for all models, e.g. a shifted lognormal model to account for 
                // negative prices. TODO figure out best way to handle this, and/or document, or just abandon pathwise differentiation as delta calculation method
                double sumPayoffDerivativeWrtForwardPrice = sumSpotPriceTimesVolume / forwardPrice * discountForDeltas;
                double periodDelta = sumPayoffDerivativeWrtForwardPrice/numSims;
                deltas[periodIndex] = periodDelta;

                double deltaStandardDeviation = Math.Sqrt((sumSpotPriceTimesVolumeSquared - 
                                                sumPayoffDerivativeWrtForwardPrice * sumPayoffDerivativeWrtForwardPrice / numSims)/(numSims-1));

                deltasStandardErrors[periodIndex] = deltaStandardDeviation/numSimsSqrt;
                progress += forwardStepProgressPcnt;
                lsmcParams.OnProgressUpdate?.Invoke(progress);
                lsmcParams.CancellationToken.ThrowIfCancellationRequested();

                #region Trigger Price Calculation

                double expectedInventoryInventoryLoss = lsmcParams.Storage.CmdtyInventoryPercentLoss(period) * expectedInventory;
                InjectWithdrawRange expectedInventoryInjectWithdrawRange = lsmcParams.Storage.GetInjectWithdrawRange(period, expectedInventory);
                double[] triggerPriceDecisionSet = StorageHelper.CalculateBangBangDecisionSet(expectedInventoryInjectWithdrawRange, expectedInventory,
                    expectedInventoryInventoryLoss, nextStepInventorySpaceMin, nextStepInventorySpaceMax, lsmcParams.NumericalTolerance, lsmcParams.ExtraDecisions);
                double[] inventoryGridNexPeriod = inventorySpaceGrids[periodIndex + 1];

                double triggerPriceMaxInjectVolume = triggerPriceDecisionSet.Max();
                var injectTriggerPrices = new List<TriggerPricePoint>();
                var triggerPricesBuilder = new TriggerPrices.Builder();

                if (triggerPriceMaxInjectVolume > 0)
                {
                    double alternativeVolume = triggerPriceDecisionSet
                        .Where(d => d >= 0)
                        .OrderBy(d => d)
                        .First(); // Probably zero, but might not due to forced injection, in which case the lowest injection rate
                    if (triggerPriceMaxInjectVolume > alternativeVolume)
                    {
                        (double alternativeContinuationValue, double alternativeDecisionCost, double alternativeCmdtyConsumed) =
                            CalcAlternatives(lsmcParams.Storage, expectedInventory, alternativeVolume, expectedInventoryInventoryLoss, inventoryGridNexPeriod, 
                                regressContinuationValues, period, DiscountToCurrentDay, lsmcParams.NumericalTolerance);
                        double[] triggerPriceVolumes = CalcInjectTriggerPriceVolumes<T>(triggerPriceMaxInjectVolume, alternativeVolume, numTriggerPriceVolumes);

                        foreach (double triggerVolume in triggerPriceVolumes)
                        {
                            double injectTriggerPrice = CalcTriggerPrice(lsmcParams.Storage, expectedInventory, triggerVolume, expectedInventoryInventoryLoss, inventoryGridNexPeriod,
                                regressContinuationValues, alternativeContinuationValue, alternativeVolume, period, alternativeDecisionCost,
                                alternativeCmdtyConsumed, discountFactorFromCmdtySettlement, DiscountToCurrentDay, lsmcParams.NumericalTolerance);
                            injectTriggerPrices.Add(new TriggerPricePoint(triggerVolume, injectTriggerPrice));
                        }

                        triggerPricesBuilder.MaxInjectTriggerPrice = injectTriggerPrices[injectTriggerPrices.Count - 1].Price;
                        triggerPricesBuilder.MaxInjectVolume = triggerPriceMaxInjectVolume;
                    }
                }
                
                double maxWithdrawVolume = triggerPriceDecisionSet.Min();
                var withdrawTriggerPrices = new List<TriggerPricePoint>();
                if (maxWithdrawVolume < 0)
                {
                    double alternativeVolume = triggerPriceDecisionSet
                        .Where(d => d <= 0)
                        .OrderByDescending(d => d)
                        .First(); // Probably zero, but might not due to forced withdrawal, in which case lowest withdrawal
                    if (maxWithdrawVolume < alternativeVolume)
                    {
                        (double alternativeContinuationValue, double alternativeDecisionCost, double alternativeCmdtyConsumed) =
                            CalcAlternatives(lsmcParams.Storage, expectedInventory, alternativeVolume, expectedInventoryInventoryLoss, inventoryGridNexPeriod, 
                                regressContinuationValues, period, DiscountToCurrentDay, lsmcParams.NumericalTolerance);
                        double[] triggerPriceVolumes = CalcWithdrawTriggerPriceVolumes<T>(maxWithdrawVolume, alternativeVolume, numTriggerPriceVolumes);

                        foreach (double triggerVolume in triggerPriceVolumes.Reverse())
                        {
                            double withdrawTriggerPrice = CalcTriggerPrice(lsmcParams.Storage, expectedInventory, triggerVolume, expectedInventoryInventoryLoss, inventoryGridNexPeriod,
                                regressContinuationValues, alternativeContinuationValue, alternativeVolume, period, alternativeDecisionCost,
                                alternativeCmdtyConsumed, discountFactorFromCmdtySettlement, DiscountToCurrentDay, lsmcParams.NumericalTolerance);
                            withdrawTriggerPrices.Add(new TriggerPricePoint(triggerVolume, withdrawTriggerPrice));
                        }

                        triggerPricesBuilder.MaxWithdrawTriggerPrice = withdrawTriggerPrices[0].Price;
                        triggerPricesBuilder.MaxWithdrawVolume = maxWithdrawVolume;
                    }
                }

                triggerVolumeProfilesArray[periodIndex] = new TriggerPriceVolumeProfiles(injectTriggerPrices, withdrawTriggerPrices);
                triggerPricesArray[periodIndex] = triggerPricesBuilder.Build();

                #endregion Trigger Price Calculation
            }
            // Pv on final period
            double endPeriodPv = 0.0;
            if (!lsmcParams.Storage.MustBeEmptyAtEnd)
            {
                ReadOnlySpan<double> storageEndPeriodSpotPrices = regressionSpotSims.SpotPricesForPeriod(lsmcParams.Storage.EndPeriod).Span;
                Span<double> storageEndInventory = nextPeriodInventories;
                Span<double> storageEndPv = returnSimPv ? pvByPeriodAndSim[periodsForResultsTimeSeries.Length-1] : Array.Empty<double>();
                double terminalPv = 0.0;
                for (int simIndex = 0; simIndex < numSims; simIndex++)
                {
                    double inventory = storageEndInventory[simIndex];
                    double spotPrice = storageEndPeriodSpotPrices[simIndex];
                    terminalPv += lsmcParams.Storage.TerminalStorageNpv(spotPrice, inventory);
                    if (returnSimPv)
                        storageEndPv[simIndex] = terminalPv;
                    pvBySim[simIndex] += terminalPv;
                }
                endPeriodPv = terminalPv/numSims;
            }

            stopwatches.ForwardSimulation.Stop();
            _logger?.LogInformation("Starting calculations of optimal decisions by simulation forward in time.");

            double forwardNpv = pvBySim.Average();
            //double standardError = pvBySim.StandardDeviation() / Math.Sqrt(numSims);
            double standardError = StandardErrorWithAntithetic(pvBySim);
            _logger?.LogInformation("Forward Pv: " + forwardNpv.ToString("N", CultureInfo.InvariantCulture));

            // Calculate NPVs for first active period using current inventory
            // TODO this is unnecessarily introducing floating point error if the val date is during the storage active period and there should not be a Vector of simulated spot prices
            double backwardNpv = storageActualValuesNextPeriod[0].Average();

            _logger?.LogInformation("Backward Pv: " + backwardNpv.ToString("N", CultureInfo.InvariantCulture));

            double expectedFinalInventory = Average(nextPeriodInventories);
            // Profile at storage end when no decisions can happen
            storageProfiles[storageProfiles.Length - 1] = new StorageProfile(expectedFinalInventory, 0.0, 0.0, 0.0, endPeriodPv);

            var deltasSeries = new DoubleTimeSeries<T>(periodsForResultsTimeSeries[0], deltas);
            var deltasStandardErrorSeries = new DoubleTimeSeries<T>(periodsForResultsTimeSeries[0], deltasStandardErrors);
            var storageProfileSeries = new TimeSeries<T, StorageProfile>(periodsForResultsTimeSeries[0], storageProfiles);
            var triggerPriceVolumeProfiles = new TimeSeries<T, TriggerPriceVolumeProfiles>(periodsForResultsTimeSeries.First(), triggerVolumeProfilesArray);
            var triggerPrices = new TimeSeries<T, TriggerPrices>(periodsForResultsTimeSeries.First(), triggerPricesArray);

            Panel<T, double> regressionSpotPricePanel = returnSimSpotPriceForRegress ? ExtractSpotSims(regressionSpotSims) : Panel<T, double>.CreateEmpty();
            Panel<T, double> valuationSpotPricePanel = returnSimSpotPriceForValuation ? ExtractSpotSims(valuationSpotSims) : Panel<T, double>.CreateEmpty();

            // TODO in future refactor ISpotSimResults should make use of Panel type, making this code not necessary
            Panel<T, double>[] regressionMarkovFactors = returnSimFactorsForRegression ? ExtractMarkovFactorsToPanel(regressionSpotSims) 
                : Enumerable.Range(0, regressionSpotSims.NumFactors).Select(i => Panel<T, double>.CreateEmpty()).ToArray();
            Panel<T, double>[] valuationMarkovFactors = returnSimFactorsForValuation ? ExtractMarkovFactorsToPanel(valuationSpotSims)
                : Enumerable.Range(0, valuationSpotSims.NumFactors).Select(i => Panel<T, double>.CreateEmpty()).ToArray();
            lsmcParams.OnProgressUpdate?.Invoke(1.0); // Progress with approximately 1.0 should have occurred already, but might have been a bit off because of floating-point error.

            stopwatches.All.Stop();
            if (_logger != null)
            {
                string profilingReport = stopwatches.GenerateProfileReport();
                _logger.LogInformation("Profiling Report:");
                _logger.LogInformation(Environment.NewLine + profilingReport);
            }

            return new LsmcStorageValuationResults<T>(forwardNpv, standardError, deltasSeries, deltasStandardErrorSeries, 
                storageProfileSeries, regressionSpotPricePanel,
                valuationSpotPricePanel, inventoryBySim, injectWithdrawVolumeBySim, cmdtyConsumedBySim, inventoryLossBySim, netVolumeBySim, 
                triggerPrices, triggerPriceVolumeProfiles, pvByPeriodAndSim, pvBySim, regressionMarkovFactors, valuationMarkovFactors);
        }

        private double StandardErrorWithAntithetic(double[] pvBySim)
        {
            int numSims = pvBySim.Length;
            int numPairs = numSims / 2; // TODO does this round down properly?
            var pairAverages = new double[numPairs];

            for (int i = 0; i < numPairs; i++)
            {
                int startIindex = i * 2;
                pairAverages[i] = (pvBySim[startIindex] + pvBySim[startIindex + 1]) / 2.0;
            }

            return pairAverages.StandardDeviation() / Math.Sqrt(numPairs);
        }

        private static (bool ReturnSimSpotPriceForRegress, bool ReturnSimSpotPriceForValuation, bool ReturnSimFactorsForRegression, bool
            ReturnSimFactorsForValuation, bool ReturnSimInventory, bool ReturnSimInjectWithdrawVolume, bool ReturnSimCmdtyConsumed,
            bool ReturnSimInventoryLoss, bool ReturnSimNetVolume, bool ReturnSimPv)
            ParseSimulationDataReturned(SimulationDataReturned simulationDataReturned)
        {
            return (simulationDataReturned.HasFlag(SimulationDataReturned.SpotPricesForRegression), simulationDataReturned.HasFlag(SimulationDataReturned.SpotPricesForValuation), 
                simulationDataReturned.HasFlag(SimulationDataReturned.FactorsForRegression), simulationDataReturned.HasFlag(SimulationDataReturned.FactorsForValuation),
                simulationDataReturned.HasFlag(SimulationDataReturned.Inventory), simulationDataReturned.HasFlag(SimulationDataReturned.InjectWithdrawVolume),
                simulationDataReturned.HasFlag(SimulationDataReturned.CmdtyConsumed), simulationDataReturned.HasFlag(SimulationDataReturned.InventoryLoss),
                simulationDataReturned.HasFlag(SimulationDataReturned.NetVolume), simulationDataReturned.HasFlag(SimulationDataReturned.Pv));
        }

        private Panel<T, double> ExtractSpotSims<T>(ISpotSimResults<T> spotSimResults)
            where T : ITimePeriod<T>
        {
            // TODO this code is horrific and caused by ISpotSimResults leaky abstraction. Refactor once ISpotSimResults in sorted.
            if (spotSimResults is SpotSimResultsFromPanels<T> spotSimResultsFromPanels)
                return spotSimResultsFromPanels.SpotPriceSims;
            else
                return Panel.UseRawDataArray(spotSimResults.SpotPrices, spotSimResults.SimulatedPeriods, spotSimResults.NumSims);
        }

        private Panel<T, double>[] ExtractMarkovFactorsToPanel<T>(ISpotSimResults<T> spotSims) where T : ITimePeriod<T>
        {
            // TODO see comment above about ISpotSimResults being leaky
            if (spotSims is SpotSimResultsFromPanels<T> spotSimResultsFromPanels)
                return spotSimResultsFromPanels.FactorSims.ToArray();

            var markovFactorPanelArray = new Panel<T, double>[spotSims.NumFactors];
            for (int factorIndex = 0; factorIndex < markovFactorPanelArray.Length; factorIndex++) // Loop through different factors
            {
                var markovFactorSims = new Panel<T, double>(spotSims.SimulatedPeriods, spotSims.NumSims);
                for (int simulatedPeriodIndex = 0; simulatedPeriodIndex < spotSims.SimulatedPeriods.Count; simulatedPeriodIndex++)
                {
                    ReadOnlySpan<double> simulatedMarkovFactors = 
                        spotSims.MarkovFactorsForStepIndex(simulatedPeriodIndex, factorIndex).Span;
                    Span<double> panelRowSpan = markovFactorSims[simulatedPeriodIndex];
                    for (int i = 0; i < panelRowSpan.Length; i++)
                        panelRowSpan[i] = simulatedMarkovFactors[i];
                }
                markovFactorPanelArray[factorIndex] = markovFactorSims;
            }
            return markovFactorPanelArray;
        }

        private static double CalcTriggerPrice<T>(ICmdtyStorage<T> storage, double expectedInventory, double triggerVolume, double inventoryLoss,
                double[] inventoryGridNexPeriod, Vector<double>[] regressContinuationValues, double alternativeContinuationValue, double alternativeVolume, T period,
                double alternativeDecisionCost, double alternativeCmdtyConsumed, double discountFactorFromCmdtySettlement, Func<Day, double> discountToCurrentDay,
                double numericalTolerance) 
            where T : ITimePeriod<T>
        {
            double inventoryAfterTriggerVolume = expectedInventory + triggerVolume - inventoryLoss;
            double triggerVolumeContinuationValue = AverageContinuationValue(inventoryAfterTriggerVolume, inventoryGridNexPeriod, regressContinuationValues, numericalTolerance);
            double triggerVolumeContinuationValueChange = triggerVolumeContinuationValue - alternativeContinuationValue;

            double triggerVolumeExcessVolume = triggerVolume - alternativeVolume;
            double triggerVolumeInjectWithdrawCostChange =
                InjectWithdrawCostNpv(storage, triggerVolume, period, expectedInventory, discountToCurrentDay) // This will be positive value
                - alternativeDecisionCost;
            double cmdtyConsumedCostChange = CmdtyVolumeConsumedOnDecision(storage, triggerVolume, period, expectedInventory) - alternativeCmdtyConsumed;

            double triggerPrice = (triggerVolumeContinuationValueChange - triggerVolumeInjectWithdrawCostChange) /
                                        (discountFactorFromCmdtySettlement * (triggerVolumeExcessVolume + cmdtyConsumedCostChange));
            return triggerPrice;
        }

        private static double[] CalcInjectTriggerPriceVolumes<T>(double maxInjectVolume, double alternativeVolume, int numTriggerPriceVolumes)
            where T : ITimePeriod<T>
        {
            double triggerVolumeIncrement = (maxInjectVolume - alternativeVolume) / numTriggerPriceVolumes;
            var triggerPriceVolumes = new double[numTriggerPriceVolumes];
            triggerPriceVolumes[numTriggerPriceVolumes - 1] = maxInjectVolume; // Use exact volume directly to avoid floating point error
            for (int i = 1; i < numTriggerPriceVolumes; i++)
                triggerPriceVolumes[i - 1] = alternativeVolume + i * triggerVolumeIncrement;
            return triggerPriceVolumes;
        }

        private static double[] CalcWithdrawTriggerPriceVolumes<T>(double maxWithdrawVolume, double alternativeVolume, int numTriggerPriceVolumes)
            where T : ITimePeriod<T>
        {
            double triggerVolumeIncrement = (alternativeVolume - maxWithdrawVolume) / numTriggerPriceVolumes;
            var triggerPriceVolumes = new double[numTriggerPriceVolumes];
            for (int i = 0; i < numTriggerPriceVolumes; i++)
                triggerPriceVolumes[i] = maxWithdrawVolume + i * triggerVolumeIncrement;
            return triggerPriceVolumes;
        }

        private static (double alternativeContinuationValue, double alternativeDecisionCost, double alternativeCmdtyConsumed) CalcAlternatives<T>(
            ICmdtyStorage<T> storage, double expectedInventory, double alternativeVolume, double inventoryLoss, double[] inventoryGridNexPeriod,
            Vector<double>[] regressContinuationValues, T period, Func<Day, double> discountToPresent, double numericalTolerance) where T : ITimePeriod<T>
        {
            double inventoryAfterAlternative = expectedInventory + alternativeVolume - inventoryLoss;
            double alternativeContinuationValue = AverageContinuationValue(inventoryAfterAlternative, inventoryGridNexPeriod, regressContinuationValues, numericalTolerance);
            double alternativeDecisionCost = InjectWithdrawCostNpv(storage, alternativeVolume, period, expectedInventory, discountToPresent);
            double alternativeCmdtyConsumed = CmdtyVolumeConsumedOnDecision(storage, alternativeVolume, period, expectedInventory);
            return (alternativeContinuationValue, alternativeDecisionCost, alternativeCmdtyConsumed);
        }

        private static double CmdtyVolumeConsumedOnDecision<T>(ICmdtyStorage<T> storage, double decisionVolume, T period, double inventory) 
            where T : ITimePeriod<T>
        {
            return decisionVolume > 0.0
                ? storage.CmdtyVolumeConsumedOnInject(period, inventory, decisionVolume)
                : storage.CmdtyVolumeConsumedOnWithdraw(period, inventory, -decisionVolume);
        }

        private static double InjectWithdrawCostNpv<T>(ICmdtyStorage<T> storage, double decisionVolume, T period, double inventory,
                                            Func<Day, double> discountToPresent) 
            where T : ITimePeriod<T>
        {
            IReadOnlyList<DomesticCashFlow> injectWithdrawCostCostCashFlows = decisionVolume > 0.0
                ? storage.InjectionCost(period, inventory, decisionVolume)
                : storage.WithdrawalCost(period, inventory, -decisionVolume);
            double injectWithdrawCostNpv = injectWithdrawCostCostCashFlows.Sum(cashFlow => cashFlow.Amount * discountToPresent(cashFlow.Date));
            return injectWithdrawCostNpv;
        }

        private static double Average(Span<double> span)
        {
            double sum = 0.0;
            // ReSharper disable once ForCanBeConvertedToForeach
            for (int i = 0; i < span.Length; i++)
                sum += span[i];
            return sum/span.Length;
        }

        private static double AverageContinuationValue(double inventoryAfterDecision, double[] inventoryGrid,
                Vector<double>[] storageRegressValuesNextPeriod, double numericalTolerance)
        {
            (int lowerInventoryIndex, int upperInventoryIndex) = StorageHelper.BisectInventorySpace(inventoryGrid, inventoryAfterDecision, numericalTolerance);

            if (lowerInventoryIndex == upperInventoryIndex)
                return storageRegressValuesNextPeriod[lowerInventoryIndex].Average();

            double lowerInventory = inventoryGrid[lowerInventoryIndex];
            double upperInventory = inventoryGrid[upperInventoryIndex];
            double inventoryGridSpace = upperInventory - lowerInventory;
            double lowerWeight = (upperInventory - inventoryAfterDecision) / inventoryGridSpace;
            double upperWeight = 1.0 - lowerWeight;

            Vector<double> lowerStorageRegressValues = storageRegressValuesNextPeriod[lowerInventoryIndex];
            Vector<double> upperStorageRegressValues = storageRegressValuesNextPeriod[upperInventoryIndex];
            Vector<double> weightedAverageStorageRegressValues =
                lowerStorageRegressValues * lowerWeight + upperStorageRegressValues * upperWeight;

            return weightedAverageStorageRegressValues.Average();
        }

        private static double InterpolateContinuationValue(double inventoryAfterDecision, double[] inventoryGrid, 
                            Vector<double>[] storageRegressValuesNextPeriod, int simIndex, double numericalTolerance)
        {
            // TODO look into the efficiency of memory access in this method and think about reordering dimension of arrays
            (int lowerInventoryIndex, int upperInventoryIndex) = StorageHelper.BisectInventorySpace(inventoryGrid, inventoryAfterDecision, numericalTolerance);

            if (lowerInventoryIndex == upperInventoryIndex)
                return storageRegressValuesNextPeriod[lowerInventoryIndex][simIndex];

            double lowerInventory = inventoryGrid[lowerInventoryIndex];
            double upperInventory = inventoryGrid[upperInventoryIndex];
            double inventoryGridSpace = upperInventory - lowerInventory;
            double lowerWeight = (upperInventory - inventoryAfterDecision) / inventoryGridSpace;
            double upperWeight = 1.0 - lowerWeight;

            double lowerStorageRegressValue = storageRegressValuesNextPeriod[lowerInventoryIndex][simIndex];
            double upperStorageRegressValue = storageRegressValuesNextPeriod[upperInventoryIndex][simIndex];

            return lowerStorageRegressValue * lowerWeight + upperStorageRegressValue * upperWeight;
        }

        private static Vector<double> WeightedAverage<T>(Vector<double> vector1,
            double weight1, Vector<double> vector2, double weight2, Vector<double> upperWeightedBuffer) where T : ITimePeriod<T>
        {
            Vector<double> interpolatedRegressContinuationValue = Vector<double>.Build.Dense(vector1.Count);
            vector1.Multiply(weight1, interpolatedRegressContinuationValue);
            vector2.Multiply(weight2, upperWeightedBuffer);
            upperWeightedBuffer.Add(interpolatedRegressContinuationValue, interpolatedRegressContinuationValue);
            return interpolatedRegressContinuationValue;
        }

        public static void PopulateDesignMatrix<T>(Matrix<double> designMatrix, T period, ISpotSimResults<T> spotSims,
            IReadOnlyList<BasisFunction> basisFunctions)
            where T : ITimePeriod<T>
        {
            ReadOnlySpan<double> spotPrices = spotSims.SpotPricesForPeriod(period).Span;
            int numSims = spotSims.NumSims;
            int numFactors = spotSims.NumFactors;
            ReadOnlyMemory<double>[] markovFactors = new ReadOnlyMemory<double>[numFactors];
            for (int i = 0; i < numFactors; i++)
                markovFactors[i] = spotSims.MarkovFactorsForPeriod(period, i);

            for (int basisIndex = 0; basisIndex < basisFunctions.Count; basisIndex++)
            {
                Span<double> designMatrixColumn = new Span<double>(designMatrix.AsColumnMajorArray(), basisIndex * numSims, numSims);
                BasisFunction basisFunction = basisFunctions[basisIndex];
                basisFunction(markovFactors, spotPrices, designMatrixColumn);
            }
        }
        
    }
}
