#region License
// Copyright (c) 2024 Jake Fowler
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
using Cmdty.Curves;
using Cmdty.TimeSeries;
using Cmdty.TimePeriodValueTypes;
using ExcelDna.Integration;

namespace Cmdty.Storage.Excel
{
    public static class CurvesXl
    {
        [ExcelFunction(Name = AddIn.ExcelFunctionNamePrefix + nameof(InterpolateCurveToDaily),
            Description = "Interpolates forward market data to daily using either spline or piecewise flat interpolation.",
            Category = AddIn.ExcelFunctionCategory, IsThreadSafe = false, IsVolatile = false, IsExceptionSafe = true)]
        public static object InterpolateCurveToDaily(
            [ExcelArgument(Name = ExcelArg.ForwardPrices.Name, Description = ExcelArg.ForwardPrices.Description)] object[,] forwardPrices,
            [ExcelArgument(Name = ExcelArg.FwdInterpolationType.Name, Description = ExcelArg.FwdInterpolationType.Description)] string interpolationType,
            [ExcelArgument(Name = ExcelArg.DailyFwdShapingFactors.Name, Description = ExcelArg.DailyFwdShapingFactors.Description)] object dailyShapingFactors,
            [ExcelArgument(Name = ExcelArg.Tension.Name, Description = ExcelArg.Tension.Description)] object tension)
        {
            return StorageExcelHelper.ExecuteExcelFunction(() =>
            {
                ((Day day, double fwdPrice)[] inputForwardData, Day endDay) = ReadInputForwardData(forwardPrices);
                TimeSeries<Day, double> dailyCurve;
                if (interpolationType == "Flat")
                    dailyCurve = PiecewiseFlatInterpolateToDaily(inputForwardData, endDay);
                else if (interpolationType == "Spline")
                    dailyCurve = SplineInterpolateToDaily(inputForwardData, endDay, dailyShapingFactors, tension);
                else
                    throw new ArgumentException($"Interpolation_type '{interpolationType}' not recognised. Should be either 'Flat' or 'Spline'.");

                return StorageExcelHelper.TimeSeriesToExcelReturnValues(dailyCurve, false);
            });
        }

        private static TimeSeries<Day, double> SplineInterpolateToDaily((Day day, double fwdPrice)[] inputForwardData, Day endDay, object dailyShapingFactorsIn, object tensionIn)
        {
            Day[] boundaryDays = inputForwardData.Skip(1).Select(p => p.day).Concat(new[] { endDay }).ToArray();
            ISplineAddOptionalParameters<Day> curveBuilder = new MaxSmoothnessSplineCurveBuilder<Day>();
            for (int i = 0; i < inputForwardData.Length; i++)
            {
                (Day contractStart, double contractPrice) = inputForwardData[i];
                Day contractEnd = boundaryDays[i] - 1;
                if (contractStart.First<Month>() != contractEnd.First<Month>())
                    throw new ArgumentException("When interpolating with spline, input contracts cannot straddle more than one month.");
                curveBuilder.AddContract(contractStart, contractEnd, contractPrice);
            }
            
            if (!StorageExcelHelper.IsExcelEmptyOrMissing(dailyShapingFactorsIn))
            {
                Dictionary<DayOfWeek, double> dailyShapingFactors = ExtractDailyShapingFactors(dailyShapingFactorsIn);
                curveBuilder.WithMultiplySeasonalAdjustment(day => dailyShapingFactors[day.DayOfWeek]);
            }

            if (!StorageExcelHelper.IsExcelEmptyOrMissing(tensionIn))
                if (StorageExcelHelper.TryCreateDouble(tensionIn, out double tension))
                    curveBuilder.WithTensionParameter(tension);
                else
                    throw new ArgumentException($"Provided {ExcelArg.Tension.Name} parameter value must be numeric value.");
            
            return curveBuilder.BuildCurve().Curve;
        }

        private const string DailyForwardShapingFactorErrorText =
            "Argument " + ExcelArg.DailyFwdShapingFactors.Name + " has been incorrectly entered. Should be single column with 7 rows.";

        private static Dictionary<DayOfWeek, double> ExtractDailyShapingFactors(object dailyShapingFactorsIn)
        {
            if (!(dailyShapingFactorsIn is object[,] dailyShapingFactorsExcel))
                throw new ArgumentException(DailyForwardShapingFactorErrorText);
            if (dailyShapingFactorsExcel.GetLength(0) != 7)
                throw new ArgumentException(DailyForwardShapingFactorErrorText);
            if (dailyShapingFactorsExcel.GetLength(1) != 1)
                throw new ArgumentException(DailyForwardShapingFactorErrorText);

            for (int i = 0; i < 7; i++)
                if (!(dailyShapingFactorsExcel[i, 0] is double))
                    throw new ArgumentException($"Element {i} of argument {ExcelArg.DailyFwdShapingFactors.Name} is not a numeric value.");

            var shapingDictionary = new Dictionary<DayOfWeek, double>()
            {
                { DayOfWeek.Monday, (double)dailyShapingFactorsExcel[0, 0]},
                { DayOfWeek.Tuesday, (double)dailyShapingFactorsExcel[1, 0]},
                { DayOfWeek.Wednesday, (double)dailyShapingFactorsExcel[2, 0]},
                { DayOfWeek.Thursday, (double)dailyShapingFactorsExcel[3, 0]},
                { DayOfWeek.Friday, (double)dailyShapingFactorsExcel[4, 0]},
                { DayOfWeek.Saturday, (double)dailyShapingFactorsExcel[5, 0]},
                { DayOfWeek.Sunday, (double)dailyShapingFactorsExcel[6, 0]},
            };

            return shapingDictionary;
        }

        private static TimeSeries<Day, double> PiecewiseFlatInterpolateToDaily((Day day, double fwdPrice)[] inputForwardData, Day endDay)
        {
            Day startDay = inputForwardData[0].day;
            int numDays = endDay - startDay;
            var dailyPrices = new double[numDays];
            var days = new Day[numDays];
            int inputDataIndex = 0;

            Day[] boundaryDays = inputForwardData.Skip(1).Select(p => p.day).Concat(new []{endDay}).ToArray();

            for (int i = 0; i < numDays; i++)
            {
                Day day = startDay + i;
                if (day == boundaryDays[inputDataIndex])
                    inputDataIndex++;
                dailyPrices[i] = inputForwardData[inputDataIndex].fwdPrice;
                days[i] = day;
            }
            return new TimeSeries<Day,double>(days, dailyPrices);
        }

        private static ((Day day, double fwdPrice)[], Day endDay) ReadInputForwardData(object[,] forwardPriceInputs)
        {
            if (forwardPriceInputs.GetLength(1) != 2)
                throw new ArgumentException(ExcelArg.ForwardPrices.Name + " has been incorrectly entered. Argument value should be a range with 2 columns.");

            object[][] inputRows = StorageExcelHelper.TakeWhileNotEmptyOrError(forwardPriceInputs).ToArray();
            if (inputRows.Length < 2)
                throw new ArgumentException($"Argument {ExcelArg.ForwardPrices.Name} must have at least 2 rows of data.");

            var forwardPoints = new (Day day, double fwdPrice)[inputRows.Length-1];

            for (int i = 0; i < inputRows.Length-1; i++)
            {
                object[] inputRow = inputRows[i];
                Day curvePointDay = StorageExcelHelper.ObjectToDay(inputRow[0], 
                    $"Cannot create DateTime from value in first row of argument {ExcelArg.ForwardPrices.Name}.");
                double forwardPrice = StorageExcelHelper.ObjectToDouble(inputRow[1],
                    $"Second row of argument {ExcelArg.ForwardPrices.Name} contains non-numerical values of {inputRow[1]}.");
                forwardPoints[i] = (curvePointDay, forwardPrice);
            }

            for (int i = 0; i < forwardPoints.Length-1; i++)
                if (forwardPoints[i].day >= forwardPoints[i + 1].day)
                    throw new ArgumentException(ExcelArg.ForwardPrices.Name + " dates are not in strictly ascending order.");
            
            Day finalDay = StorageExcelHelper.ObjectToDay(inputRows[inputRows.Length - 1][0], 
                $"Second row of argument {ExcelArg.ForwardPrices.Name} contains non-numerical values of {inputRows[inputRows.Length - 1][0]}.");

            if (forwardPoints[forwardPoints.Length-1].day >= finalDay)
                throw new ArgumentException(ExcelArg.ForwardPrices.Name + " dates are not in strictly ascending order.");

            return (forwardPoints, finalDay);
        }
    }
}
