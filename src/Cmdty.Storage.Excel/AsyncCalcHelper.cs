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

using System.Windows.Forms;

namespace Cmdty.Storage.Excel
{
    public static class AsyncCalcHelper
    {

        public static void CalculateAllPending()
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

        public static void CancelAllRunning(bool showMsgBox)
        {
            int numCalcsCancelled = 0;
            foreach (string objectHandle in ObjectCache.Instance.Handles)
            {
                if (ObjectCache.Instance.TryGetObject(objectHandle, out object cachedObject))
                {
                    if (cachedObject is ExcelCalcWrapper calcWrapper)
                        if (calcWrapper.Status == CalcStatus.Running)
                        {
                            calcWrapper.Cancel();
                            numCalcsCancelled++;
                        }
                }
            }
            if (showMsgBox)
            {
                string message = numCalcsCancelled == 1 ? "1 calculation has been cancelled."
                    : numCalcsCancelled + " calculations have been cancelled.";
                MessageBox.Show(message, "Cmdty.Storage", MessageBoxButtons.OK);
            }
        }

    }
}