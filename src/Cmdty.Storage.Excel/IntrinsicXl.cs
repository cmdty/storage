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
    public static class IntrinsicXl
    {

        [ExcelFunction(Name = AddIn.ExcelFunctionNamePrefix + nameof(StorageIntrinsicValue), 
            Category = AddIn.ExcelFunctionCategory, IsThreadSafe = true, IsVolatile = false, IsExceptionSafe = true)]
        public static object StorageIntrinsicValue(
                        DateTime valuationDate,
                        DateTime storageStart,
                        DateTime storageEnd,
                        object injectWithdrawConstraints,
                        [ExcelArgument(Name = ExcelArg.RatchetInterpolation.Name, Description = ExcelArg.RatchetInterpolation.Description)] string injectWithdrawInterpolation,
                        double injectionCostRate,
                        double cmdtyConsumedOnInjection,
                        double withdrawalCostRate,
                        double cmdtyConsumedOnWithdrawal,
                        double currentInventory,
                        object forwardCurve,
                        object interestRateCurve,
                        object numGlobalGridPoints, // TODO excel argument says default is 100
                        object numericalTolerance,
                        [ExcelArgument(Name = "Granularity")] object granularity)
        {
            return StorageExcelHelper.ExecuteExcelFunction(() =>
                IntrinsicStorageVal<Day>(valuationDate, storageStart, storageEnd, injectWithdrawConstraints, injectWithdrawInterpolation,
                    injectionCostRate, cmdtyConsumedOnInjection, withdrawalCostRate,
                    cmdtyConsumedOnWithdrawal,
                    currentInventory, forwardCurve, interestRateCurve, numGlobalGridPoints, numericalTolerance).NetPresentValue);
        }

        [ExcelFunction(Name = AddIn.ExcelFunctionNamePrefix + nameof(StorageIntrinsicDecisionProfile), 
            Category = AddIn.ExcelFunctionCategory, IsThreadSafe = true, IsVolatile = false, IsExceptionSafe = true)]
        public static object StorageIntrinsicDecisionProfile(
            DateTime valuationDate,
            DateTime storageStart,
            DateTime storageEnd,
            object injectWithdrawConstraints,
            [ExcelArgument(Name = ExcelArg.RatchetInterpolation.Name, Description = ExcelArg.RatchetInterpolation.Description)] string injectWithdrawInterpolation,
            double injectionCostRate,
            double cmdtyConsumedOnInjection,
            double withdrawalCostRate,
            double cmdtyConsumedOnWithdrawal,
            double currentInventory,
            object forwardCurve,
            object interestRateCurve,
            object numGlobalGridPoints, // TODO excel argument says default is 100
            object numericalTolerance,
            [ExcelArgument(Name = "Granularity")] object granularity)
        {
            return StorageExcelHelper.ExecuteExcelFunction(() =>
            {
                IntrinsicStorageValuationResults<Day> valuationResults = IntrinsicStorageVal<Day>(valuationDate, storageStart, storageEnd,
                    injectWithdrawConstraints, injectWithdrawInterpolation,
                    injectionCostRate, cmdtyConsumedOnInjection, withdrawalCostRate,
                    cmdtyConsumedOnWithdrawal,
                    currentInventory, forwardCurve, interestRateCurve, numGlobalGridPoints, numericalTolerance);

                var resultArray = new object[valuationResults.StorageProfile.Count, 3];

                for (int i = 0; i < resultArray.GetLength(0); i++)
                {
                    resultArray[i, 0] = valuationResults.StorageProfile.Indices[i].Start;
                    resultArray[i, 1] = valuationResults.StorageProfile[i].InjectWithdrawVolume;
                    resultArray[i, 2] = valuationResults.StorageProfile[i].CmdtyConsumed;
                }

                return resultArray;
            });
        }

        private static IntrinsicStorageValuationResults<T> IntrinsicStorageVal<T>(
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
                        withdrawalCostRate, cmdtyConsumedOnWithdrawal, "Empty", numericalTolerance);

            T currentPeriod = TimePeriodFactory.FromDateTime<T>(valuationDateTime);

            TimeSeries<T, double> forwardCurve = StorageExcelHelper.CreateDoubleTimeSeries<T>(forwardCurveIn, "Forward_curve");
            
            // TODO input settlement dates and use interest rates
            int numGridPoints =
                StorageExcelHelper.DefaultIfExcelEmptyOrMissing<int>(numGlobalGridPointsIn, 100, "Num_global_grid_points");

            IntrinsicStorageValuationResults<T> valuationResults = IntrinsicStorageValuation<T>
                .ForStorage(storage)
                .WithStartingInventory(currentInventory)
                .ForCurrentPeriod(currentPeriod)
                .WithForwardCurve(forwardCurve)
                .WithCmdtySettlementRule(period => period.First<Day>()) // TODO get rid if this
                .WithDiscountFactorFunc((currentDate, cashFlowDate) => 1.0) // TODO add proper discounting
                .WithFixedNumberOfPointsOnGlobalInventoryRange(numGridPoints)
                .WithLinearInventorySpaceInterpolation()
                .WithNumericalTolerance(numericalTolerance)
                .Calculate();

            return valuationResults;
        }
        
    }
}
