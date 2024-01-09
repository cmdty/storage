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

using ExcelDna.Integration;
using ExcelDna.Integration.CustomUI;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Cmdty.Storage.Excel.ExcelUiExtensions;
using Microsoft.Office.Interop.Excel;
using Application = Microsoft.Office.Interop.Excel.Application;

namespace Cmdty.Storage.Excel
{
    [ComVisible(true)]
    public class ExcelContextMenuController : ExcelRibbon
    {
        private IRibbonUI _ribbonUi;

        public override string GetCustomUI(string ribbonId)
        {
            return RibbonResources.Ribbon;
        }

        public void CancelExecution(IRibbonControl ribbonControl)
        {
            Application app = (Application)ExcelDnaUtil.Application;
            Range selectedRange = (Range)app.Selection;

            int numCalcsCancelled = 0;
            foreach (dynamic cell in selectedRange.Cells)
            {
                string cellValue = cell.Value2 as string;
                if (cellValue != null)
                    if (ObjectCache.Instance.TryGetObject(cellValue, out object cachedObject))
                        if (cachedObject is ExcelCalcWrapper excelCalcWrapper)
                            if (excelCalcWrapper.Status == CalcStatus.Running)
                            {
                                excelCalcWrapper.Cancel();
                                numCalcsCancelled++;
                            }
            }
            string pluralS = numCalcsCancelled == 1 ? "" : "s";
            MessageBox.Show(numCalcsCancelled + $" calculation{pluralS} cancelled.");
        }

        public void CalculateAllPending(IRibbonControl ribbonControl)
        {
            foreach (string objectHandle in ObjectCache.Instance.Handles)
            {
                if (ObjectCache.Instance.TryGetObject(objectHandle, out object cachedObject))
                {
                    if (cachedObject is ExcelCalcWrapper calcWrapper)
                        if (calcWrapper.Status == CalcStatus.Pending) // TODO thread synchronisation required, or does this always run on same thread?
                            calcWrapper.Start();
                }
            }
        }

        public void OnRibbonLoad(IRibbonUI ribbonUi)
        {
            _ribbonUi = ribbonUi;
        }

        public void AsyncModePressed(IRibbonControl ribbonControl, bool pressed)
        {
            AddIn.CalcMode = pressed ? CalcMode.Async : CalcMode.Blocking;
            _ribbonUi.InvalidateControl("blockingCalcModelButton"); // Unselect Blocking Mode toggle button
            // Enabled buttons for use in async mode
            _ribbonUi.InvalidateControl("calcPendingButton"); 
            _ribbonUi.InvalidateControl("cancelAll");
        }

        public void BlockingModePressed(IRibbonControl ribbonControl, bool pressed)
        {
            AddIn.CalcMode = pressed ? CalcMode.Blocking : CalcMode.Async;
            _ribbonUi.InvalidateControl("asyncCalcModelButton");  // Unselect Async Mode toggle button
            // Disable buttons for use in async mode
            _ribbonUi.InvalidateControl("calcPendingButton");
            _ribbonUi.InvalidateControl("cancelAll");
        }

        public bool IsAsyncModePressed(IRibbonControl ribbonControl)
        {
            return AddIn.CalcMode == CalcMode.Async;
        }

        public bool IsBlockingModePressed(IRibbonControl ribbonControl)
        {
            return AddIn.CalcMode == CalcMode.Blocking;
        }

    }
}