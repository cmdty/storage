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

using ExcelDna.Integration;
using System;
using System.Threading.Tasks;

namespace Cmdty.Storage.Excel
{
    sealed class CalcWrapperExceptionObservable : CalcWrapperObservableBase
    {
        private AggregateException _taskException;

        public CalcWrapperExceptionObservable(ExcelCalcWrapper calcWrapper) : base(calcWrapper)
            => calcWrapper.CalcTask.ContinueWith(task => ProcessTaskForExceptions(task), 
                TaskContinuationOptions.OnlyOnFaulted);

        private void ProcessTaskForExceptions(Task task)
        {
            _taskException = task.Exception;
            // For some reason _observer.OnError doesn't work as expected
            _observer?.OnNext(task.Exception.InnerExceptions[0].Message);
        }

        protected override void OnSubscribe()
        {
            if (_taskException != null)
                _observer.OnNext(_taskException.InnerExceptions[0].Message);
            else
                _observer.OnNext(ExcelError.ExcelErrorNA);
        }
    }
}
