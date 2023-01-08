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
using System.Linq;
using System.Threading.Tasks;
using Cmdty.Core.Common;
using Cmdty.TimePeriodValueTypes;
using Cmdty.TimeSeries;
using ExcelDna.Integration;
using MathNet.Numerics.Interpolation;

namespace Cmdty.Storage.Excel
{
    public static class StorageExcelHelper
    {
        private static readonly HashSet<Type> ReturnableAsObject;

        static StorageExcelHelper()
        {
            ReturnableAsObject = new HashSet<Type>
            {
                typeof(double),
                typeof(string),
                typeof(DateTime),
                typeof(bool),
                typeof(double[]),
                typeof(double[,]),
                typeof(object[]),
                typeof(object[,]),
                typeof(ExcelError),
                typeof(ExcelMissing),
                typeof(ExcelEmpty),
                typeof(ExcelReference), // TODO check what happens if this is returned
                typeof(int),
                typeof(short),
                typeof(ushort),
                typeof(decimal),
                typeof(long)
            };
        }

        public static object ExecuteExcelFunction(Func<object> functionBody)
        {
            if (ExcelDnaUtil.IsInFunctionWizard())
                return "Currently in Function Wizard.";

            try
            {
                return functionBody();
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }

        public static async Task<object> ExecuteExcelFunctionAsync(Func<Task<object>> functionBody)
        {
            if (ExcelDnaUtil.IsInFunctionWizard())
                return "Currently in Function Wizard.";

            try
            {
                return await functionBody();
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }

        public static T DefaultIfExcelEmptyOrMissing<T>(object excelArgument, T defaultValue, string argumentName)
        {
            if (IsExcelEmptyOrMissing(excelArgument))
                return defaultValue;

            try
            {
                return (T) Convert.ChangeType(excelArgument, typeof(T));
            }
            catch (Exception)
            {
                string typeName = typeof(T).Name;
                throw new ArgumentException($"Excel argument '{argumentName}' is not of type {typeName}, and cannot be converted to this type.");
            }
        }

        public static bool IsExcelEmptyOrMissing(object excelArgument) 
            => excelArgument is ExcelMissing || excelArgument is ExcelEmpty;

        public static CmdtyStorage<T> CreateCmdtyStorageFromExcelInputs<T>(DateTime storageStartDateTime,
            DateTime storageEndDateTime,
            object ratchets,
            string ratchetInterpolationIn,
            double injectionCostRate,
            double cmdtyConsumedOnInjection,
            double withdrawalCostRate,
            double cmdtyConsumedOnWithdrawal, 
            string terminalInventory,
            double numericalTolerance)
            where T : ITimePeriod<T>
        {
            if (ratchetInterpolationIn == "Step" && terminalInventory == "Empty")
                throw new ArgumentException($"If argument {ExcelArg.RatchetInterpolation.Name} equals 'Step' then " +
                    $"argument {ExcelArg.TerminalInventoryConstraint.Name} cannot equal 'Empty'.");

            T storageStart = TimePeriodFactory.FromDateTime<T>(storageStartDateTime);
            T storageEnd = TimePeriodFactory.FromDateTime<T>(storageEndDateTime);

            if (ratchets is ExcelMissing || ratchets is ExcelEmpty)
                throw new ArgumentException("Inject/withdraw constraints haven't been specified.");

            if (!(ratchets is object[,] injectWithdrawArray))
                throw new ArgumentException("Inject/withdraw constraints have been incorrectly entered. Argument value should be of a range with 4 columns, the first containing dates, the rest containing numbers.");

            if (injectWithdrawArray.GetLength(1) != 4)
                throw new ArgumentException("Inject/withdraw constraints have been incorrectly entered. Argument value should be a range 4 columns.");
            
            var injectWithdrawGrouped = TakeWhileNotEmptyOrError(injectWithdrawArray).Select((row, i) => new
            {
                period = ObjectToDateTime(row[0], $"Row {i + 1} of inject/withdraw/inventory constrains contains invalid date time in 1st column."),
                inventory = ObjectToDouble(row[1], $"Row {i + 1} of inject/withdraw/inventory constraints contains invalid inventory in 2nd column as is not a number."),
                injectRate = ObjectToDouble(row[2], $"Row {i + 1} of inject/withdraw/inventory constraints contains invalid injection rate in 3rd column as is not a number."),
                withdrawRate = ObjectToDouble(row[3], $"Row {i + 1} of inject/withdraw/inventory constraints contains invalid withdrawal in 4th column as is not a number.")
            }).GroupBy(arg => arg.period);

            var injectWithdrawConstraints = new List<InjectWithdrawRangeByInventoryAndPeriod<T>>();
            foreach (var injectWithdrawGroup in injectWithdrawGrouped)
            {
                T period = TimePeriodFactory.FromDateTime<T>(injectWithdrawGroup.Key);
                IEnumerable<InjectWithdrawRangeByInventory> injectWithdrawByInventory = injectWithdrawGroup.Select(arg =>
                        new InjectWithdrawRangeByInventory(arg.inventory, new InjectWithdrawRange(-arg.withdrawRate, arg.injectRate)));
                injectWithdrawConstraints.Add(new InjectWithdrawRangeByInventoryAndPeriod<T>(period, injectWithdrawByInventory));
            }

            InterpolationType interpolationType;
            if (ratchetInterpolationIn == "PiecewiseLinear")
            {
                interpolationType = InterpolationType.PiecewiseLinear;
            }
            else if (ratchetInterpolationIn == "Polynomial")
            {
                interpolationType = InterpolationType.PolynomialWithParams(numericalTolerance);
            }
            else if (ratchetInterpolationIn == "Step")
            {
                interpolationType = InterpolationType.Step;
            }
            else
            {
                throw new ArgumentException($"Value of Inject_withdraw_interpolation '{ratchetInterpolationIn}' not recognised. Must be either 'PiecewiseLinear', 'Polynomial' or 'Step'.");
            }

            IAddTerminalStorageState<T> addTerminalStorageState = CmdtyStorage<T>.Builder
                    .WithActiveTimePeriod(storageStart, storageEnd)
                    .WithTimeAndInventoryVaryingInjectWithdrawRates(injectWithdrawConstraints, interpolationType)
                    .WithPerUnitInjectionCost(injectionCostRate)
                    .WithFixedPercentCmdtyConsumedOnInject(cmdtyConsumedOnInjection)
                    .WithPerUnitWithdrawalCost(withdrawalCostRate)
                    .WithFixedPercentCmdtyConsumedOnWithdraw(cmdtyConsumedOnWithdrawal)
                    .WithNoCmdtyInventoryLoss()
                    .WithNoInventoryCost();

            IBuildCmdtyStorage<T> buildCmdtyStorage;
            switch (terminalInventory)
            {
                case "Empty":
                    buildCmdtyStorage = addTerminalStorageState.MustBeEmptyAtEnd();
                    break;
                case "ZeroValue":
                    buildCmdtyStorage = addTerminalStorageState.WithTerminalInventoryNpv((cmdtyPrice, finalInventory) => 0.0);
                    break;
                case "SpotValue":
                    buildCmdtyStorage = addTerminalStorageState.WithTerminalInventoryNpv(
                                            (cmdtyPrice, finalInventory) => cmdtyPrice * finalInventory);
                    break;
                default:
                    throw new ArgumentException($"{ExcelArg.TerminalInventoryConstraint.Name} value of '{terminalInventory}' not valid." +
                        $" Must be either 'Empty', 'ZeroValue', or 'SpotValue'.");
            }

            CmdtyStorage<T> storage = buildCmdtyStorage.Build();
            return storage;
        }

        private static IEnumerable<object[]> TakeWhileNotEmptyOrError(object[,] excelInput)
        {
            int numColumns = excelInput.GetLength(1);
            for (int i = 0; i < excelInput.GetLength(0); i++)
            {
                if (excelInput[i, 0] is ExcelEmpty || excelInput[i, 0] is ExcelError)
                    yield break;

                var slice = new object[numColumns];
                for (int j = 0; j < numColumns; j++)
                {
                    slice[j] = excelInput[i, j];
                }

                yield return slice;
            }
        }


        public static TimeSeries<TIndex, double> CreateDoubleTimeSeries<TIndex>(object excelValues, string excelArgumentName)
            where TIndex : ITimePeriod<TIndex>
        {
            bool TryCreateDouble(object excelInput, out double inputAsDouble)
            {
                if (!(excelInput is double d))
                {
                    inputAsDouble = 0.0;
                    return false;
                }
                inputAsDouble = d;
                return true;
            }
            return CreateTimeSeries<TIndex, double>(excelValues, excelArgumentName, TryCreateDouble);
        }

        public delegate bool TryCreateData<TData>(object excelInput, out TData createdData);

        public static Func<Day, Day> CreateSettlementRule(object excelValues, string excelArgumentName)
        {
            TimeSeries<Month, Day> settlementSeries = CreateSettlementSeries(excelValues, excelArgumentName);
            Day GetSettlementDate(Day deliveryDate)
            {
                Month deliveryMonth = Month.FromDateTime(deliveryDate.Start);
                if (!settlementSeries.TryGetValue(deliveryMonth, out Day settlementDate))
                    throw new ApplicationException($"No settlement date for delivery month {deliveryMonth}.");
                return settlementDate;
            }
            return GetSettlementDate;
        }

        public static TimeSeries<Month, Day> CreateSettlementSeries(object excelValues, string excelArgumentName)
        {
            bool TryCreateDay(object excelInput, out Day inputAsDay)
            {
                if (!(excelInput is double d))
                {
                    inputAsDay = new Day();
                    return false;
                }
                try
                {
                    DateTime inputAsDateTime = DateTime.FromOADate(d);
                    inputAsDay = Day.FromDateTime(inputAsDateTime);
                    return true;
                }
                catch
                {
                    inputAsDay = new Day();
                    return false;
                }
            }
            return CreateTimeSeries<Month, Day>(excelValues, excelArgumentName, TryCreateDay);
        }

        public static TimeSeries<TIndex, TData> CreateTimeSeries<TIndex, TData>(object excelValues, string excelArgumentName,
            TryCreateData<TData> tryCreateData)
            where TIndex : ITimePeriod<TIndex>
        {
            if (excelValues is ExcelMissing || excelValues is ExcelEmpty)
                throw new ArgumentException(excelArgumentName + " hasn't been specified.");

            if (!(excelValues is object[,] excelValuesArray))
                throw new ArgumentException(excelArgumentName + " has been incorrectly entered. Argument value should be of a range with 2 columns, the first containing dates, the second containing numbers.");

            if (excelValuesArray.GetLength(1) != 2)
                throw new ArgumentException(excelArgumentName + " has been incorrectly entered. Argument value should be a range with 2 columns.");

            var builder = new TimeSeries<TIndex, TData>.Builder();

            for (int i = 0; i < excelValuesArray.GetLength(0); i++)
            {
                if (excelValuesArray[i, 0] is ExcelEmpty || excelValuesArray[i, 0] is ExcelError)
                    break;

                object dataValueIn = excelValuesArray[i, 1];
                if (!tryCreateData(dataValueIn, out TData dataValue))
                    throw new ArgumentException($"Value in the second column of row {i} for argument {excelArgumentName} cannot be converted to type {typeof(TData).Name}.");

                DateTime curvePointDateTime = ObjectToDateTime(excelValuesArray[i, 0], $"Cannot create DateTime from value in first row of argument {excelArgumentName}.");
                TIndex curvePointPeriod = TimePeriodFactory.FromDateTime<TIndex>(curvePointDateTime);

                builder.Add(curvePointPeriod, dataValue);
            }

            return builder.Build();
        }

        public static Func<Day, double> CreateLinearInterpolatedInterestRateFunc(object excelValues, string excelArgumentName)
        {
            if (excelValues is ExcelMissing || excelValues is ExcelEmpty)
                throw new ArgumentException(excelArgumentName + " hasn't been specified.");

            if (!(excelValues is object[,] excelValuesArray))
                throw new ArgumentException(excelArgumentName + " has been incorrectly entered. Argument value should be of a range with 2 columns, the first containing dates, the second containing numbers.");

            if (excelValuesArray.GetLength(1) != 2)
                throw new ArgumentException(excelArgumentName + " has been incorrectly entered. Argument value should be a range with 2 columns.");

            var interestRatePoints = new List<(Day Date, double InterestRate)>();

            for (int i = 0; i < excelValuesArray.GetLength(0); i++)
            {
                if (excelValuesArray[i, 0] is ExcelEmpty || excelValuesArray[i, 0] is ExcelError)
                    break;

                if (!(excelValuesArray[i, 1] is double doubleValue))
                    throw new ArgumentException($"Value in the second column of row {i} for argument {excelArgumentName} is not a number.");

                DateTime curvePointDateTime = ObjectToDateTime(excelValuesArray[i, 0], $"Cannot create DateTime from value in first row of argument {excelArgumentName}.");

                interestRatePoints.Add((Date: Day.FromDateTime(curvePointDateTime), InterestRate: doubleValue));
            }

            if (interestRatePoints.Count < 2)
                throw new ArgumentException(excelArgumentName + " must contain at least two points.");

            Day firstDate = interestRatePoints.Min(curvePoint => curvePoint.Date);
            IEnumerable<double> maturitiesToInterpolate =
                interestRatePoints.Select(curvePoint => curvePoint.Date.OffsetFrom(firstDate) / 365.0);

            LinearSpline linearSpline = LinearSpline.Interpolate(maturitiesToInterpolate, interestRatePoints.Select(curvePoint => curvePoint.InterestRate));

            Day lastDate = interestRatePoints.Max(curvePoint => curvePoint.Date);

            double InterpolatedCurve(Day cashFlowDate)
            {
                if (cashFlowDate < firstDate || cashFlowDate > lastDate)
                    throw new ArgumentException($"Interest rate for future cash flow {cashFlowDate} cannot be interpolated, as this date is outside of the bounds of the input rates curve.");
                double maturity = (cashFlowDate - firstDate) / 365.0;
                return linearSpline.Interpolate(maturity);
            }
            
            return InterpolatedCurve;
        }
        
        private static double ObjectToDouble(object excelNumber, string messageOneFail)
        {
            if (!(excelNumber is double doubleNumber))
                throw new ArgumentException(messageOneFail);
            return doubleNumber;
        }

        private static DateTime ObjectToDateTime(object excelDateTime, string errorMessage)
        {
            if (!(excelDateTime is double doubleValue))
                throw new ArgumentException(errorMessage);

            try
            {
                DateTime dateTime = DateTime.FromOADate(doubleValue);
                return dateTime;
            }
            catch (Exception)
            {
                throw new ArgumentException(errorMessage);
            }

        }

        public static object[,] TimeSeriesToExcelReturnValues<TIndex>(TimeSeries<TIndex, double> timeSeries, bool indicesAsText)
            where TIndex : ITimePeriod<TIndex>
        {
            var resultArray = new object[timeSeries.Count, 2];
            for (int i = 0; i < timeSeries.Count; i++)
            {
                resultArray[i, 0] = indicesAsText ? (object)timeSeries.Indices[i].ToString() : timeSeries.Indices[i].Start;
                resultArray[i, 1] = timeSeries[i];
            }
            return resultArray;
        }

        public static object[,] TimeSeriesToExcelReturnValues<TIndex, TData>(TimeSeries<TIndex, TData> timeSeries, Action<TData, int, object[,]> populateData, 
                        int numDataFields, bool indicesAsText)
            where TIndex : ITimePeriod<TIndex>
        {
            var resultArray = new object[timeSeries.Count, numDataFields + 1];
            for (int i = 0; i < timeSeries.Count; i++)
            {
                TIndex index = timeSeries.Indices[i];
                resultArray[i, 0] = indicesAsText ? (object)index.ToString() : index.Start;
                populateData(timeSeries[index], i, resultArray);
            }
            return resultArray;
        }

        public static object[,] StorageProfileToExcelReturnValues<TIndex>(TimeSeries<TIndex, StorageProfile> storageProfiles, bool indicesAsText)
            where TIndex : ITimePeriod<TIndex>
        {
            return TimeSeriesToExcelReturnValues(storageProfiles, (storageProfile, rowIndex, resultArray) =>
            {
                resultArray[rowIndex, 1] = storageProfile.Inventory;
                resultArray[rowIndex, 2] = storageProfile.InjectWithdrawVolume;
                resultArray[rowIndex, 3] = storageProfile.CmdtyConsumed;
                resultArray[rowIndex, 4] = storageProfile.InventoryLoss;
                resultArray[rowIndex, 5] = storageProfile.NetVolume;
                resultArray[rowIndex, 6] = storageProfile.PeriodPv;

            }, 6, indicesAsText);
        }

        public static object[,] TriggerPricesToExcelReturnValues<TIndex>(TimeSeries<TIndex, TriggerPrices> triggerPriceSeries, bool indicesAsText)
            where TIndex : ITimePeriod<TIndex>
        {
            return TimeSeriesToExcelReturnValues(triggerPriceSeries, (triggerPrices, rowIndex, resultArray) =>
            {
                resultArray[rowIndex, 1] = TransformNullableForExcel(triggerPrices.MaxInjectVolume);
                resultArray[rowIndex, 2] = TransformNullableForExcel(triggerPrices.MaxInjectTriggerPrice);
                resultArray[rowIndex, 3] = TransformNullableForExcel(triggerPrices.MaxWithdrawVolume);
                resultArray[rowIndex, 4] = TransformNullableForExcel(triggerPrices.MaxWithdrawTriggerPrice);
            }, 4, indicesAsText);
        }

        public static object TransformNullableForExcel<T>(T? nullable)
            where T : struct => nullable == null ? ExcelError.ExcelErrorNA : (object)nullable.Value;

        public static object[,] PaneToExcelReturnValues<TIndex>(Panel<TIndex, double> panel, bool indicesAsText)
            where TIndex : ITimePeriod<TIndex>
        {
            var resultArray = new object[panel.NumRows, panel.NumCols + 1];
            int i = 0;
            foreach (TIndex timePeriod in panel.RowKeys)
            {
                resultArray[i, 0] = indicesAsText ? (object)timePeriod.ToString() : timePeriod.Start;
                Span<double> row = panel[timePeriod];
                for (int j = 0; j < row.Length; j++)
                    resultArray[i, j + 1] = row[j];
                i++;
            }
            return resultArray;
        }
        
        public static object TransformForExcelReturn(object obj)
        {
            if (ReferenceEquals(obj, null))
                return ExcelError.ExcelErrorNull;

            if (obj is TimeSeries<Day, double> dailyTimeSeries)
                return TimeSeriesToExcelReturnValues(dailyTimeSeries, false);

            if (obj is Panel<Day, double> dailyPanel)
                return PaneToExcelReturnValues(dailyPanel, false);

            if (obj is TimeSeries<Day, StorageProfile> dailyProfile)
                return StorageProfileToExcelReturnValues(dailyProfile, false);

            if (obj is TimeSeries<Day, TriggerPrices> dailyTriggerPrices)
                return TriggerPricesToExcelReturnValues(dailyTriggerPrices, false);

            Type objType = obj.GetType();
            if (ReturnableAsObject.Contains(objType))
                return obj;

            // TODO handle enums

            throw new NotImplementedException($"Returning objects of type {objType.Name} not supported yet.");
        }

    }
}
