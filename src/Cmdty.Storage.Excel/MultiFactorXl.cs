#region License
// Copyright (c) 2021 Jake Fowler
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
using Cmdty.Core.Simulation.MultiFactor;
using Cmdty.TimePeriodValueTypes;
using ExcelDna.Integration;

namespace Cmdty.Storage.Excel
{
    public static class MultiFactorXl
    {
        [ExcelFunction(Name = AddIn.ExcelFunctionNamePrefix + nameof(CreateStorage),
            Description = "Creates and caches an object representing a storage facility.",
            Category = AddIn.ExcelFunctionCategory, IsThreadSafe = false, IsVolatile = false, IsExceptionSafe = true)] // TODO turn IsThreadSafe to true and use ConcurrentDictionary?
        public static object CreateStorage(
            [ExcelArgument(Name = "Storage_name", Description = "Name of storage object to create.")] string name,
            [ExcelArgument(Name = ExcelArg.StorageStart.Name, Description = ExcelArg.StorageStart.Description)] DateTime storageStart,
            [ExcelArgument(Name = ExcelArg.StorageEnd.Name, Description = ExcelArg.StorageEnd.Description)] DateTime storageEnd,
            [ExcelArgument(Name = ExcelArg.Ratchets.Name, Description = ExcelArg.Ratchets.Description)] object ratchets,
            [ExcelArgument(Name = ExcelArg.RatchetInterpolation.Name, Description = ExcelArg.RatchetInterpolation.Description)] string ratchetInterpolation,
            [ExcelArgument(Name = ExcelArg.InjectionCost.Name, Description = ExcelArg.InjectionCost.Description)] double injectionCostRate,
            [ExcelArgument(Name = ExcelArg.CmdtyConsumedInject.Name, Description = ExcelArg.CmdtyConsumedInject.Description)] double cmdtyConsumedOnInjection,
            [ExcelArgument(Name = ExcelArg.WithdrawalCost.Name, Description = ExcelArg.WithdrawalCost.Description)] double withdrawalCostRate,
            [ExcelArgument(Name = ExcelArg.CmdtyConsumedWithdraw.Name, Description = ExcelArg.CmdtyConsumedWithdraw.Description)] double cmdtyConsumedOnWithdrawal,
            [ExcelArgument(Name = ExcelArg.TerminalInventoryConstraint.Name, Description = ExcelArg.TerminalInventoryConstraint.Description)] string terminalInventory,
            [ExcelArgument(Name = ExcelArg.NumericalTolerance.Name, Description = ExcelArg.NumericalTolerance.Description)] object numericalToleranceIn)
        {
            return StorageExcelHelper.ExecuteExcelFunction(() =>
            {
                return ObjectHandler.Instance.GetHandle(name, new object[] {storageStart, storageEnd, ratchets,
                    ratchetInterpolation, injectionCostRate, cmdtyConsumedOnInjection, withdrawalCostRate,
                    cmdtyConsumedOnWithdrawal, terminalInventory, numericalToleranceIn}, () =>
                    {
                        double numericalTolerance = StorageExcelHelper.DefaultIfExcelEmptyOrMissing(numericalToleranceIn, 1E-10, "Numerical_tolerance");
                        CmdtyStorage<Day> storage = StorageExcelHelper.CreateCmdtyStorageFromExcelInputs<Day>(storageStart,
                            storageEnd, ratchets, ratchetInterpolation, injectionCostRate, cmdtyConsumedOnInjection,
                            withdrawalCostRate, cmdtyConsumedOnWithdrawal, terminalInventory, numericalTolerance);
                        return storage;
                    });
            });
        }

        [ExcelFunction(Name = AddIn.ExcelFunctionNamePrefix + nameof(StorageValueThreeFactor),
            Description = "Calculates the NPV, Deltas, Trigger prices and other metadata using a 3-factor seasonal model of price dynamics.",
            Category = AddIn.ExcelFunctionCategory, IsThreadSafe = false, IsVolatile = false, IsExceptionSafe = true)] // TODO turn IsThreadSafe to true and use ConcurrentDictionary?
        public static object StorageValueThreeFactor(
            [ExcelArgument(Name = "Name", Description = "Name of cached object to create.")] string name,
            [ExcelArgument(Name = ExcelArg.StorageHandle.Name, Description = ExcelArg.StorageHandle.Description)] string storageHandle,
            [ExcelArgument(Name = ExcelArg.ValDate.Name, Description = ExcelArg.ValDate.Description)] DateTime valuationDate,
            [ExcelArgument(Name = ExcelArg.Inventory.Name, Description = ExcelArg.Inventory.Description)] double currentInventory,
            [ExcelArgument(Name = ExcelArg.ForwardCurve.Name, Description = ExcelArg.ForwardCurve.Description)] object forwardCurve,
            [ExcelArgument(Name = ExcelArg.InterestRateCurve.Name, Description = ExcelArg.InterestRateCurve.Description)] object interestRateCurve,
            [ExcelArgument(Name = ExcelArg.SpotVol.Name, Description = ExcelArg.SpotVol.Description)] double spotVol,
            [ExcelArgument(Name = ExcelArg.SpotMeanReversion.Name, Description = ExcelArg.SpotMeanReversion.Description)] double spotMeanReversion,
            [ExcelArgument(Name = ExcelArg.LongTermVol.Name, Description = ExcelArg.LongTermVol.Description)] double longTermVol,
            [ExcelArgument(Name = ExcelArg.SeasonalVol.Name, Description = ExcelArg.SeasonalVol.Description)] double seasonalVol,
            [ExcelArgument(Name = ExcelArg.DiscountDeltas.Name, Description = ExcelArg.DiscountDeltas.Description)] bool discountDeltas,
            [ExcelArgument(Name = ExcelArg.SettleDates.Name, Description = ExcelArg.SettleDates.Description)] object settleDatesIn,
            [ExcelArgument(Name = ExcelArg.NumSims.Name, Description = ExcelArg.NumSims.Description)] int numSims,
            [ExcelArgument(Name = ExcelArg.BasisFunctions.Name, Description = ExcelArg.BasisFunctions.Description)] string basisFunctionsIn,
            [ExcelArgument(Name = ExcelArg.Seed.Name, Description = ExcelArg.Seed.Description)] object seedIn,
            [ExcelArgument(Name = ExcelArg.ForwardSimSeed.Name, Description = ExcelArg.ForwardSimSeed.Description)] object fwdSimSeedIn,
            [ExcelArgument(Name = ExcelArg.NumGridPoints.Name, Description = ExcelArg.NumGridPoints.Description)] object numGlobalGridPointsIn,
            [ExcelArgument(Name = ExcelArg.NumericalTolerance.Name, Description = ExcelArg.NumericalTolerance.Description)] object numericalTolerance,
            [ExcelArgument(Name = ExcelArg.ExtraDecisions.Name, Description = ExcelArg.ExtraDecisions.Description)] object extraDecisions)
        {
            return StorageExcelHelper.ExecuteExcelFunction(() =>
            {
                var args = new object[] {storageHandle, valuationDate, currentInventory, forwardCurve,
                                interestRateCurve, spotVol, spotMeanReversion, longTermVol, seasonalVol,
                                discountDeltas, settleDatesIn, numSims, basisFunctionsIn, seedIn, fwdSimSeedIn,
                                numGlobalGridPointsIn, numericalTolerance, extraDecisions };

                return ObjectHandler.Instance.GetHandle(name, args, () =>
                {
                    return ExcelCalcWrapper.CreateCancellable((cancellationToken, onProgress) =>
                    {
                        CmdtyStorage<Day> storage = ObjectHandler.Instance.GetObject<CmdtyStorage<Day>>(storageHandle);
                        
                        // TODO provide alternative method for interpolating interest rates
                        Func<Day, double> interpolatedInterestRates =
                            StorageExcelHelper.CreateLinearInterpolatedInterestRateFunc(interestRateCurve, ExcelArg.InterestRateCurve.Name);

                        Func<Day, Day, double> discountFunc = StorageHelper.CreateAct65ContCompDiscounter(interpolatedInterestRates);
                        Day valDate = Day.FromDateTime(valuationDate);
                        Func<Day, Day> settleDateRule = StorageExcelHelper.CreateSettlementRule(settleDatesIn, ExcelArg.SettleDates.Name);

                        int numGlobalGridPoints = StorageExcelHelper.DefaultIfExcelEmptyOrMissing(numGlobalGridPointsIn, ExcelArg.NumGridPoints.Default,
                                                                        ExcelArg.NumGridPoints.Name);

                        string basisFunctionsText = basisFunctionsIn.Replace("x_st", "x0").Replace("x_lt", "x1").Replace("x_sw", "x2");

                        var lsmcParamsBuilder = new LsmcValuationParameters<Day>.Builder
                        {
                            Storage = storage,
                            CurrentPeriod = valDate,
                            Inventory = currentInventory,
                            ForwardCurve = StorageExcelHelper.CreateDoubleTimeSeries<Day>(forwardCurve, ExcelArg.ForwardCurve.Name),
                            DiscountFactors = discountFunc,
                            DiscountDeltas = discountDeltas,
                            BasisFunctions = BasisFunctionsBuilder.Parse(basisFunctionsText),
                            ExtraDecisions = StorageExcelHelper.DefaultIfExcelEmptyOrMissing(extraDecisions, 0, ExcelArg.ExtraDecisions.Name),
                            CancellationToken = cancellationToken,
                            OnProgressUpdate = onProgress,
                            GridCalc = FixedSpacingStateSpaceGridCalc.CreateForFixedNumberOfPointsOnGlobalInventoryRange(storage, numGlobalGridPoints),
                            NumericalTolerance = StorageExcelHelper.DefaultIfExcelEmptyOrMissing(numericalTolerance, LsmcValuationParameters<Day>.Builder.DefaultNumericalTolerance, ExcelArg.NumericalTolerance.Description),
                            SettleDateRule = settleDateRule
                        };

                        // TODO test that this works with expired storage
                        Day endDate = new[] { valDate, storage.EndPeriod }.Max();
                        var threeFactorParams =
                            MultiFactorParameters.For3FactorSeasonal(spotMeanReversion, spotVol, longTermVol, seasonalVol, valDate, endDate);

                        // TODO better error messages if seedIn and fwdSimSeedIn cannot be cast
                        int? seed = StorageExcelHelper.IsExcelEmptyOrMissing(seedIn) ? (int?)null : (int)(double)seedIn;
                        int? fwdSimSeed = StorageExcelHelper.IsExcelEmptyOrMissing(fwdSimSeedIn) ? (int?)null : (int)(double)fwdSimSeedIn;

                        lsmcParamsBuilder.SimulateWithMultiFactorModelAndMersenneTwister(threeFactorParams, numSims, seed, fwdSimSeed);

                        return LsmcStorageValuation.WithNoLogger.Calculate(lsmcParamsBuilder.Build());
                    });
                });
            });
        }

        [ExcelFunction(Name = AddIn.ExcelFunctionNamePrefix + nameof(SubscribeProgress),
            Description = "TODO.", // TODO
            Category = AddIn.ExcelFunctionCategory, IsThreadSafe = false, IsVolatile = false, IsExceptionSafe = true, IsClusterSafe =true)] // TODO turn IsThreadSafe to true and use ConcurrentDictionary?
        public static object SubscribeProgress(string name)
        {
            return StorageExcelHelper.ExecuteExcelFunction(() =>
            {
                const string functionName = nameof(SubscribeProgress);
                return ExcelAsyncUtil.Observe(functionName, name, () =>
                {
                    ExcelCalcWrapper wrapper = ObjectHandler.Instance.GetObject<ExcelCalcWrapper>(name);
                    var excelObserver = new CalcWrapperProgressObservable(wrapper);
                    return excelObserver;
                });
            });
        }

        [ExcelFunction(Name = AddIn.ExcelFunctionNamePrefix + nameof(SubscribeStatus),
            Description = "TODO.", // TODO
            Category = AddIn.ExcelFunctionCategory, IsThreadSafe = false, IsVolatile = false, IsExceptionSafe = true)] // TODO turn IsThreadSafe to true and use ConcurrentDictionary?
        public static object SubscribeStatus(string name)
        {
            return StorageExcelHelper.ExecuteExcelFunction(() =>
            {
                const string functionName = nameof(SubscribeStatus);
                return ExcelAsyncUtil.Observe(functionName, name, () =>
                {
                    ExcelCalcWrapper wrapper = ObjectHandler.Instance.GetObject<ExcelCalcWrapper>(name);
                    var excelObserver = new CalcWrapperStatusObservable(wrapper);
                    return excelObserver;
                });
            });
        }

        [ExcelFunction(Name = AddIn.ExcelFunctionNamePrefix + nameof(SubscribeResultProperty),
            Description = "TODO.", // TODO
            Category = AddIn.ExcelFunctionCategory, IsThreadSafe = false, IsVolatile = false, IsExceptionSafe = true)] // TODO turn IsThreadSafe to true and use ConcurrentDictionary?
        public static object SubscribeResultProperty(string objectHandle, string propertyName, object returnedWhilstWaiting)
        {
            return StorageExcelHelper.ExecuteExcelFunction(() =>
            {
                const string functionName = nameof(SubscribeResultProperty);
                return ExcelAsyncUtil.Observe(functionName, new [] { objectHandle, propertyName, returnedWhilstWaiting}, () =>
                {
                    ExcelCalcWrapper wrapper = ObjectHandler.Instance.GetObject<ExcelCalcWrapper>(objectHandle);
                    var excelObservable = new CalcWrapperResultPropertyObservable(wrapper, propertyName, returnedWhilstWaiting);
                    return excelObservable;
                });
            });
        }

        [ExcelFunction(Name = AddIn.ExcelFunctionNamePrefix + nameof(NumberOfRunningCalculations),
            Description = "Returns the number of calculations which are currently running.",
            Category = AddIn.ExcelFunctionCategory, IsThreadSafe = false, IsVolatile = true, 
            IsExceptionSafe = true)] // TODO turn IsThreadSafe to true and use ConcurrentDictionary?
        public static object NumberOfRunningCalculations()
        {
            return StorageExcelHelper.ExecuteExcelFunction(() =>
            {
                int runningCalcCount = 0;
                string[] objectHandles = ObjectHandler.Instance.ObjectCache.Keys.ToArray();
                foreach (string objectHandle in objectHandles)
                {
                    object cachedObject;
                    if (ObjectHandler.Instance.TryGetObject(objectHandle, out cachedObject))
                    {
                        ExcelCalcWrapper calcWrapper = cachedObject as ExcelCalcWrapper;
                        if (calcWrapper != null)
                            if (calcWrapper.Status == CalcStatus.Running)
                                runningCalcCount++;
                    }
                }
                return runningCalcCount;
            });
        }

        //[ExcelFunction(Name = AddIn.ExcelFunctionNamePrefix + nameof(GetError),
        //    Description = "TODO.", // TODO
        //    Category = AddIn.ExcelFunctionCategory, IsThreadSafe = false, IsVolatile = false, IsExceptionSafe = true)]
        //public static object GetError()
        //{
        //    return ExcelError.ExcelErrorGettingData;
        //}

    }
}
