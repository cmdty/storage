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
using Cmdty.TimePeriodValueTypes;
using Cmdty.TimeSeries;
using ExcelDna.Integration;

namespace Cmdty.Storage.Excel
{
    public static class TrinomialXl
    {
        [ExcelFunction(Name = AddIn.ExcelFunctionNamePrefix + nameof(StorageValueTrinomialTree),
            Description = "Calculates the NPV of a commodity storage facility using backward induction methodology, and a one-factor trinomial " +
                          "tree to model the spot price dynamics.",
            Category = AddIn.ExcelFunctionCategory, IsThreadSafe = true, IsVolatile = false, IsExceptionSafe = true)]
        public static object StorageValueTrinomialTree(
            [ExcelArgument(Name = ExcelArg.ValDate.Name, Description = ExcelArg.ValDate.Description)] DateTime valuationDate,
            [ExcelArgument(Name = ExcelArg.StorageStart.Name, Description = ExcelArg.StorageStart.Description)] DateTime storageStart,
            [ExcelArgument(Name = ExcelArg.StorageEnd.Name, Description = ExcelArg.StorageEnd.Description)] DateTime storageEnd,
            [ExcelArgument(Name = ExcelArg.Ratchets.Name, Description = ExcelArg.Ratchets.Description)] object ratchets,
            [ExcelArgument(Name = ExcelArg.RatchetInterpolation.Name, Description = ExcelArg.RatchetInterpolation.Description)] string injectWithdrawInterpolation,
            [ExcelArgument(Name = ExcelArg.InjectionCost.Name, Description = ExcelArg.InjectionCost.Description)] double injectionCostRate,
            [ExcelArgument(Name = ExcelArg.CmdtyConsumedInject.Name, Description = ExcelArg.CmdtyConsumedInject.Description)] double cmdtyConsumedOnInjection,
            [ExcelArgument(Name = ExcelArg.WithdrawalCost.Name, Description = ExcelArg.WithdrawalCost.Description)] double withdrawalCostRate,
            [ExcelArgument(Name = ExcelArg.CmdtyConsumedWithdraw.Name, Description = ExcelArg.CmdtyConsumedWithdraw.Description)] double cmdtyConsumedOnWithdrawal,
            [ExcelArgument(Name = ExcelArg.Inventory.Name, Description = ExcelArg.Inventory.Description)] double currentInventory,
            [ExcelArgument(Name = ExcelArg.ForwardCurve.Name, Description = ExcelArg.ForwardCurve.Description)] object forwardCurve,
            [ExcelArgument(Name = ExcelArg.SpotVolCurve.Name, Description = ExcelArg.SpotVolCurve.Description)] object spotVolatilityCurve,
            [ExcelArgument(Name = ExcelArg.MeanReversion.Name, Description = ExcelArg.MeanReversion.Description)] double meanReversion,
            [ExcelArgument(Name = ExcelArg.InterestRateCurve.Name, Description = ExcelArg.InterestRateCurve.Description)] object interestRateCurve,
            [ExcelArgument(Name = ExcelArg.NumGridPoints.Name, Description = ExcelArg.NumGridPoints.Description)] object numGlobalGridPoints,
            [ExcelArgument(Name = ExcelArg.NumericalTolerance.Name, Description = ExcelArg.NumericalTolerance.Description)] object numericalTolerance) // TODO add granularity
        {
            return StorageExcelHelper.ExecuteExcelFunction(() =>
                TrinomialStorageValuation<Day>(valuationDate, storageStart, storageEnd, ratchets, injectWithdrawInterpolation,
                    injectionCostRate, cmdtyConsumedOnInjection, withdrawalCostRate,
                    cmdtyConsumedOnWithdrawal, currentInventory, forwardCurve, spotVolatilityCurve, 
                    meanReversion, interestRateCurve, numGlobalGridPoints, numericalTolerance).NetPresentValue);
        }

        [ExcelFunction(Name = AddIn.ExcelFunctionNamePrefix + nameof(StorageValueIntrinsic),
            Description = "Calculated the intrinsic NPV of a commodity storage facility using backward induction methodology.",
            Category = AddIn.ExcelFunctionCategory, IsThreadSafe = true, IsVolatile = false, IsExceptionSafe = true)]
        public static object StorageValueIntrinsic(
                [ExcelArgument(Name = ExcelArg.ValDate.Name, Description = ExcelArg.ValDate.Description)] DateTime valuationDate,
                [ExcelArgument(Name = ExcelArg.StorageStart.Name, Description = ExcelArg.StorageStart.Description)] DateTime storageStart,
                [ExcelArgument(Name = ExcelArg.StorageEnd.Name, Description = ExcelArg.StorageEnd.Description)] DateTime storageEnd,
                [ExcelArgument(Name = ExcelArg.Ratchets.Name, Description = ExcelArg.Ratchets.Description)] object storageConstraints,
                [ExcelArgument(Name = ExcelArg.RatchetInterpolation.Name, Description = ExcelArg.RatchetInterpolation.Description)] string injectWithdrawInterpolation,
                [ExcelArgument(Name = ExcelArg.InjectionCost.Name, Description = ExcelArg.InjectionCost.Description)] double injectionCostRate,
                [ExcelArgument(Name = ExcelArg.CmdtyConsumedInject.Name, Description = ExcelArg.CmdtyConsumedInject.Description)] double cmdtyConsumedOnInjection,
                [ExcelArgument(Name = ExcelArg.WithdrawalCost.Name, Description = ExcelArg.WithdrawalCost.Description)] double withdrawalCostRate,
                [ExcelArgument(Name = ExcelArg.CmdtyConsumedWithdraw.Name, Description = ExcelArg.CmdtyConsumedWithdraw.Description)] double cmdtyConsumedOnWithdrawal,
                [ExcelArgument(Name = ExcelArg.Inventory.Name, Description = ExcelArg.Inventory.Description)] double currentInventory,
                [ExcelArgument(Name = ExcelArg.ForwardCurve.Name, Description = ExcelArg.ForwardCurve.Description)] object forwardCurve,
                [ExcelArgument(Name = ExcelArg.InterestRateCurve.Name, Description = ExcelArg.InterestRateCurve.Description)] object interestRateCurve,
                [ExcelArgument(Name = ExcelArg.NumGridPoints.Name, Description = ExcelArg.NumGridPoints.Description)] object numGlobalGridPoints,
                [ExcelArgument(Name = ExcelArg.NumericalTolerance.Name, Description = ExcelArg.NumericalTolerance.Description)] object numericalTolerance) // TODO add granularity
        {
            return StorageExcelHelper.ExecuteExcelFunction(() =>
                TrinomialStorageValuationIntrinsic<Day>(valuationDate, storageStart, storageEnd, storageConstraints, injectWithdrawInterpolation,
                    injectionCostRate, cmdtyConsumedOnInjection, withdrawalCostRate,
                    cmdtyConsumedOnWithdrawal, currentInventory, forwardCurve, interestRateCurve, numGlobalGridPoints, 
                    numericalTolerance).NetPresentValue);
        }
        
        private static TreeStorageValuationResults<T> TrinomialStorageValuation<T>(
                            DateTime valuationDateTime,
                            DateTime storageStartDateTime,
                            DateTime storageEndDateTime,
                            object ratchets,
                            string injectWithdrawInterpolation,
                            double injectionCostRate,
                            double cmdtyConsumedOnInjection,
                            double withdrawalCostRate,
                            double cmdtyConsumedOnWithdrawal,
                            double currentInventory,
                            object forwardCurveIn,
                            object spotVolatilityCurveIn,
                            double meanReversion,
                            object interestRateCurve,
                            object numGlobalGridPointsIn,
                            object numericalToleranceIn)
            where T : ITimePeriod<T>
        {
            double numericalTolerance = StorageExcelHelper.DefaultIfExcelEmptyOrMissing(numericalToleranceIn, 1E-10,
                            "Numerical_tolerance");

            var storage = StorageExcelHelper.CreateCmdtyStorageFromExcelInputs<T>(storageStartDateTime,
                storageEndDateTime, ratchets, injectWithdrawInterpolation, injectionCostRate, cmdtyConsumedOnInjection,
                withdrawalCostRate, cmdtyConsumedOnWithdrawal, "Empty", ExcelMissing.Value, numericalTolerance);

            T currentPeriod = TimePeriodFactory.FromDateTime<T>(valuationDateTime);

            TimeSeries<T, double> forwardCurve = StorageExcelHelper.CreateDoubleTimeSeries<T>(forwardCurveIn, "Forward_curve");
            TimeSeries<T, double> spotVolatilityCurve = StorageExcelHelper.CreateDoubleTimeSeries<T>(spotVolatilityCurveIn, "Spot_volatility_curve");

            // TODO input settlement dates
            int numGridPoints =
                StorageExcelHelper.DefaultIfExcelEmptyOrMissing<int>(numGlobalGridPointsIn, 100, "Num_global_grid_points");

            double timeDelta = 1.0 / 365.0; // TODO remove this hard coding

            Day valDate = currentPeriod.First<Day>();
            Func<Day, Day, double> discountFunc =
                StorageExcelHelper.CreateLogLinearInterpolatedDiscountFactors(interestRateCurve, ExcelArg.InterestRateCurve.Name, valDate);

            TreeStorageValuationResults<T> valuationResults = TreeStorageValuation<T>
                        .ForStorage(storage)
                        .WithStartingInventory(currentInventory)
                        .ForCurrentPeriod(currentPeriod)
                        .WithForwardCurve(forwardCurve)
                        .WithOneFactorTrinomialTree(spotVolatilityCurve, meanReversion, timeDelta)
                        .WithCmdtySettlementRule(period => period.First<Day>()) // TODO get rid if this
                        .WithDiscountFactorFunc(discountFunc)
                        .WithFixedNumberOfPointsOnGlobalInventoryRange(numGridPoints)
                        .WithLinearInventorySpaceInterpolation()
                        .WithNumericalTolerance(numericalTolerance)
                        .Calculate();

            return valuationResults;
        }

        private static TreeStorageValuationResults<T> TrinomialStorageValuationIntrinsic<T>(
                    DateTime valuationDateTime,
                    DateTime storageStartDateTime,
                    DateTime storageEndDateTime,
                    object injectWithdrawConstraints,
                    string injectWithdrawInterpolation,
                    double injectionCostRate,
                    double cmdtyConsumedOnInjection,
                    double withdrawalCostRate,
                    double cmdtyConsumedOnWithdrawal,
                    double currentInventory,
                    object forwardCurveIn,
                    object interestRateCurve,
                    object numGlobalGridPointsIn,
                    object numericalToleranceIn)
            where T : ITimePeriod<T>
        {
            double numericalTolerance = StorageExcelHelper.DefaultIfExcelEmptyOrMissing(numericalToleranceIn, 1E-10,
                            "Numerical_tolerance");

            var storage = StorageExcelHelper.CreateCmdtyStorageFromExcelInputs<T>(storageStartDateTime,
                storageEndDateTime, injectWithdrawConstraints, injectWithdrawInterpolation, injectionCostRate, cmdtyConsumedOnInjection,
                withdrawalCostRate, cmdtyConsumedOnWithdrawal, "Empty", ExcelMissing.Value, numericalTolerance);

            T currentPeriod = TimePeriodFactory.FromDateTime<T>(valuationDateTime);

            TimeSeries<T, double> forwardCurve = StorageExcelHelper.CreateDoubleTimeSeries<T>(forwardCurveIn, "Forward_curve");

            // TODO input settlement dates
            int numGridPoints =
                StorageExcelHelper.DefaultIfExcelEmptyOrMissing<int>(numGlobalGridPointsIn, 100, "Num_global_grid_points");

            Day valDate = currentPeriod.First<Day>();
            Func<Day, Day, double> discountFunc =
                StorageExcelHelper.CreateLogLinearInterpolatedDiscountFactors(interestRateCurve, ExcelArg.InterestRateCurve.Name, valDate);

            TreeStorageValuationResults<T> valuationResults = TreeStorageValuation<T>
                        .ForStorage(storage)
                        .WithStartingInventory(currentInventory)
                        .ForCurrentPeriod(currentPeriod)
                        .WithForwardCurve(forwardCurve)
                        .WithIntrinsicTree()
                        .WithCmdtySettlementRule(period => period.First<Day>()) // TODO get rid if this
                        .WithDiscountFactorFunc(discountFunc)
                        .WithFixedNumberOfPointsOnGlobalInventoryRange(numGridPoints)
                        .WithLinearInventorySpaceInterpolation()
                        .WithNumericalTolerance(numericalTolerance)
                        .Calculate();

            return valuationResults;
        }


    }
}
