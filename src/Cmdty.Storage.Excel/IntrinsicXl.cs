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
            Category = AddIn.ExcelFunctionCategory, IsThreadSafe = false, IsVolatile = false, IsExceptionSafe = true)]
        public static object StorageIntrinsicValue(
            [ExcelArgument(Name = ExcelArg.CachedObjectName.Name, Description = ExcelArg.CachedObjectName.Description)] string name,
            [ExcelArgument(Name = ExcelArg.StorageHandle.Name, Description = ExcelArg.StorageHandle.Description)] string storageHandle,
            [ExcelArgument(Name = ExcelArg.ValDate.Name, Description = ExcelArg.ValDate.Description)] DateTime valuationDate,
            [ExcelArgument(Name = ExcelArg.Inventory.Name, Description = ExcelArg.Inventory.Description)] double currentInventory,
            [ExcelArgument(Name = ExcelArg.ForwardCurve.Name, Description = ExcelArg.ForwardCurve.Description)] object forwardCurveIn,
            [ExcelArgument(Name = ExcelArg.InterestRateCurve.Name, Description = ExcelArg.InterestRateCurve.Description)] object interestRateCurve,
            [ExcelArgument(Name = ExcelArg.SettleDates.Name, Description = ExcelArg.SettleDates.Description)] object settleDatesIn,
            [ExcelArgument(Name = ExcelArg.NumGridPoints.Name, Description = ExcelArg.NumGridPoints.Description)] object numGlobalGridPointsIn,
            [ExcelArgument(Name = ExcelArg.NumericalTolerance.Name, Description = ExcelArg.NumericalTolerance.Description)] object numericalToleranceIn)
        {
            return StorageExcelHelper.ExecuteExcelFunction(() =>
            {
                var args = new object[] { name, storageHandle, valuationDate, currentInventory, forwardCurveIn, interestRateCurve, 
                    settleDatesIn, numGlobalGridPointsIn, numericalToleranceIn };

                return ObjectCache.Instance.CacheObjectAndGetHandle(name, args, () =>
                {
                    double numericalTolerance = StorageExcelHelper.DefaultIfExcelEmptyOrMissing(numericalToleranceIn, 1E-10,
                                                                    "Numerical_tolerance");

                    // TODO provide alternative method for interpolating interest rates
                    Func<Day, double> interpolatedInterestRates =
                        StorageExcelHelper.CreateLinearInterpolatedInterestRateFunc(interestRateCurve, ExcelArg.InterestRateCurve.Name);
                    Func<Day, Day> settleDateRule = StorageExcelHelper.CreateSettlementRule(settleDatesIn, ExcelArg.SettleDates.Name);
                    Func<Day, Day, double> discountFunc = StorageHelper.CreateAct65ContCompDiscounter(interpolatedInterestRates);

                    var storage = ObjectCache.Instance.GetObject<CmdtyStorage<Day>>(storageHandle);

                    TimeSeries<Day, double> forwardCurve = StorageExcelHelper.CreateDoubleTimeSeries<Day>(forwardCurveIn, ExcelArg.ForwardCurve.Name);

                    int numGridPoints =
                        StorageExcelHelper.DefaultIfExcelEmptyOrMissing(numGlobalGridPointsIn, ExcelArg.NumGridPoints.Default, ExcelArg.NumGridPoints.Name);
                    Day valDate = Day.FromDateTime(valuationDate);

                    IntrinsicStorageValuationResults<Day> valuationResults = IntrinsicStorageValuation<Day>
                        .ForStorage(storage)
                        .WithStartingInventory(currentInventory)
                        .ForCurrentPeriod(valDate)
                        .WithForwardCurve(forwardCurve)
                        .WithCmdtySettlementRule(settleDateRule)
                        .WithDiscountFactorFunc(discountFunc)
                        .WithFixedNumberOfPointsOnGlobalInventoryRange(numGridPoints)
                        .WithLinearInventorySpaceInterpolation()
                        .WithNumericalTolerance(numericalTolerance)
                        .Calculate();

                    return valuationResults;
                });
            });
        }
        
    }
}
