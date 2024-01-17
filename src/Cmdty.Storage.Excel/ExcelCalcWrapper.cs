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

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Cmdty.Storage.Excel
{

    internal enum CalcStatus
    {
        Running,
        Error,
        Success,
        Cancelled,
        Pending
    }

    internal sealed class ExcelCalcWrapper : IDisposable
    {
        public event Action<double> OnProgressUpdate;
        public event Action<CalcStatus> OnStatusUpdate;
        public event Action<AggregateException> OnException; 
        public double Progress { get; private set; }
        private Task<object> _calcTask;
        public object Result => _calcTask.Result;
        public Type ResultType { get; }
        public bool CancellationSupported { get; }
        public CalcStatus Status { get; private set; } // TODO can this be removed and replaced with _calcTask.Status?
        private CancellationTokenSource _cancellationTokenSource;
        private Func<CancellationToken, Action<double>, object> _calculation;
        private bool _isDisposed;

        private ExcelCalcWrapper(Task<object> calcTask, Type resultType, CancellationTokenSource cancellationTokenSource, CalcStatus calcStatus)
        {
            _calcTask = calcTask;
            ResultType = resultType;
            _cancellationTokenSource = cancellationTokenSource;
            CancellationSupported = true;
            Status = calcStatus;
        }
        
        public static ExcelCalcWrapper Create<TResult>(Func<CancellationToken, Action<double>, TResult> calculation, CalcMode calcMode)
            where TResult : class // Necessary for getting resetting to work
        {
            if (calcMode == CalcMode.Blocking)
            {
                try
                {
                    TResult result = calculation(CancellationToken.None, null /*progress*/); // This will block
                    return CreateCompletedSuccessfully(result);
                }
                catch (Exception e)
                {
                    return CreateCompletedError<TResult>(e);
                }
            }

            if (calcMode == CalcMode.Async)
            {
                var cancellationTokenSource = new CancellationTokenSource();
                Type resultType = typeof(TResult);
                var calcTaskWrapper = new ExcelCalcWrapper(null, resultType, cancellationTokenSource, CalcStatus.Pending);
                calcTaskWrapper._calculation = calculation;
                calcTaskWrapper._calcTask = CreateRunTask(calcTaskWrapper);
                return calcTaskWrapper;
            }

            throw new Exception($"CalcMode enum symbol {calcMode} not recognised.");
        }

        private static Task<object> CreateRunTask(ExcelCalcWrapper calcWrapper)
        {
            void OnProgress(double progress) => UpdateProgress(calcWrapper, progress);
            CancellationToken cancellationToken = calcWrapper._cancellationTokenSource.Token;
            Task<object> task =
                new Task<object>(() => calcWrapper._calculation(cancellationToken, OnProgress), cancellationToken);
            task.ContinueWith(calcWrapper.UpdateStatus); // Don't pass cancellation token as want this to run even if cancelled
            task.ContinueWith(t => 
                calcWrapper.OnException?.Invoke(t.Exception), TaskContinuationOptions.OnlyOnFaulted);
            return task;
        }

        private static ExcelCalcWrapper CreateCompletedSuccessfully<TResult>(TResult results)
        {
            Task<object> completedTask = Task.FromResult((object)results);
            return new ExcelCalcWrapper(completedTask, typeof(TResult), new CancellationTokenSource(), CalcStatus.Success);
        }

        private static ExcelCalcWrapper CreateCompletedError<TResult>(Exception exception)
        {
            Task<object> completedTask = Task.FromException<object>(exception);
            return new ExcelCalcWrapper(completedTask, typeof(TResult), new CancellationTokenSource(), CalcStatus.Error);
        }

        private static void UpdateProgress(ExcelCalcWrapper calcWrapper, double progress)
                                    => calcWrapper.UpdateProgress(progress);

        private void UpdateProgress(double progress)
        {
            // TODO some sort of synchonisation needed? Look online.
            Progress = progress;
            OnProgressUpdate?.Invoke(progress);
        }

        public void Cancel()
            => _cancellationTokenSource.Cancel();
        
        private void UpdateStatus(Task task)
        {
            CalcStatus calcStatus;
            switch (task.Status)
            {
                case TaskStatus.RanToCompletion:
                    calcStatus = CalcStatus.Success;
                    break;
                case TaskStatus.Canceled:
                    calcStatus = CalcStatus.Cancelled;
                    break;
                case TaskStatus.Faulted:
                    calcStatus = CalcStatus.Error;
                    break;
                default:
                    throw new ApplicationException($"Task status {task.Status} not supported.");
            }
            UpdateStatus(calcStatus);
        }

        private void UpdateStatus(CalcStatus calcStatus)
        {
            Status = calcStatus;
            OnStatusUpdate?.Invoke(calcStatus);
        }

        public void Start()
        {
            UpdateStatus(CalcStatus.Running);
            _calcTask.Start();
        }

        // Not thread safe
        public bool Reset() // TODO make thread safe?
        {
            if (Status == CalcStatus.Cancelled)
            {
                _cancellationTokenSource = new CancellationTokenSource();
                _calcTask = CreateRunTask(this);
                UpdateStatus(CalcStatus.Pending);
                UpdateProgress(0.0);
                return true;
            }
            return false;
        }

        public void Dispose()
        {
            if (!_isDisposed)
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
                _isDisposed = true;
            }
        }
    }
}