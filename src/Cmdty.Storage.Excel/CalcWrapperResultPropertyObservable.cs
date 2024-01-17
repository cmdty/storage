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
using System.Linq;
using System.Reflection;

namespace Cmdty.Storage.Excel
{
    sealed class CalcWrapperResultPropertyObservable : CalcWrapperObservableBase
    {
        private readonly object _returnedWhilstWaiting;
        private static readonly object[] GetterParams = Array.Empty<object>();
        private readonly MethodInfo _propertyGetter;
        
        public CalcWrapperResultPropertyObservable(ExcelCalcWrapper calcWrapper, string resultPropertyName, object returnedWhilstWaiting) : base(calcWrapper)
        {
            PropertyInfo[] properties = calcWrapper.ResultType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetProperty);
            PropertyInfo propertyInfo = properties.FirstOrDefault(info => info.Name.Equals(resultPropertyName, StringComparison.OrdinalIgnoreCase));

            if (propertyInfo == null)
                throw new ArgumentException($"Result type {calcWrapper.ResultType.Name} has not public instance property called {resultPropertyName}."); // TODO test and maybe rework
            _propertyGetter = propertyInfo.GetMethod;

            _returnedWhilstWaiting = returnedWhilstWaiting;
            calcWrapper.OnStatusUpdate += OnStatusUpdate;

        }

        private void OnStatusUpdate(CalcStatus status)
        {
            if (status == CalcStatus.Success)
                PropertyValueUpdate();
        }

        private static object GetPropertyValueToReturn(MethodInfo propertyGetter, object resultObject)
        {
            object propertyValue = propertyGetter.Invoke(resultObject, GetterParams);
            return StorageExcelHelper.TransformForExcelReturn(propertyValue);
        }

        protected override void OnSubscribe()
        {
            if (_calcWrapper.Status == CalcStatus.Success) // Has already completed
                PropertyValueUpdate();
            else
                _observer?.OnNext(_returnedWhilstWaiting);
        }

        private void PropertyValueUpdate()
        {
            object propertyValue = GetPropertyValueToReturn(_propertyGetter, _calcWrapper.CalcTask.Result); 
            _observer?.OnNext(propertyValue);
        }

        protected override void OnDispose()
        {
            _calcWrapper.OnStatusUpdate -= OnStatusUpdate;
            base.OnDispose();
        }

    }
}
