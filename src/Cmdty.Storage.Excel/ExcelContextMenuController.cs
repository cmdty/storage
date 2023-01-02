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
using Microsoft.Office.Interop.Excel;
using Application = Microsoft.Office.Interop.Excel.Application;

namespace Cmdty.Storage.Excel
{
    [ComVisible(true)]
    public class ExcelContextMenuController : ExcelRibbon
    {
        public override string GetCustomUI(string ribbonId)
        {
            return @"
<customUI xmlns='http://schemas.microsoft.com/office/2009/07/customui'>
    <contextMenus>
        <contextMenu idMso=""ContextMenuCell"">
            <menu id=""CmdtyStorageMenu"" label=""Cmdty.Storage"" insertBeforeMso=""Cut"" >
                <button id=""Menu1ButtonCancelStorage"" label=""Cancel Execution"" imageMso=""X"" onAction=""CancelExecution""/>
            </menu>
            <menuSeparator id=""MySeparator"" insertBeforeMso=""Cut"" />
        </contextMenu>
    </contextMenus>
</customUI>";
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

    }
}