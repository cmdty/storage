﻿#region License
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

using ExcelDna.Integration;
using ExcelDna.Logging;

namespace Cmdty.Storage.Excel
{
    public static class ExcelCommands
    {
        [ExcelCommand(ShortCut = "^A")]
        public static void StartAllPendingCalculations() =>
            AsyncCalcHelper.CalculateAllPending();

        [ExcelCommand(ShortCut = "^S")]
        public static void StartSelectedPendingCalculations() =>
            AsyncCalcHelper.CalculateSelectedPending();
        
        [ExcelCommand(ShortCut = "^X")]
        public static void CancelAllCalculations() =>
            AsyncCalcHelper.CancelAllRunning(true);

        [ExcelCommand(ShortCut = "^C")]
        public static void CancelSelectedCalculations() =>
            AsyncCalcHelper.CancelSelectedRunning(true);

        [ExcelCommand(ShortCut = "^P")]
        public static void ResetAllCancelled() =>
            AsyncCalcHelper.ResetAllCancelled();

        [ExcelCommand(ShortCut = "^R")]
        public static void ResetSelectedCancelled() =>
            AsyncCalcHelper.ResetSelectedCancelled();

        [ExcelCommand(ShortCut = "^L" /* Ctrl+Shift+L */)]
        public static void DisplayExcelDnaLogScreen() =>
            LogDisplay.Show();

    }
}
