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

using System;
using System.Collections.Generic;
using ExcelDna.Integration;
using System.Reflection;
using System.Linq;

namespace Cmdty.Storage.Excel
{
    public static class CachedObjectsXl
    {
        [ExcelFunction(Name = AddIn.ExcelFunctionNamePrefix + nameof(SubscribeProgress),
//            Description = "TODO.", // TODO
            Category = AddIn.ExcelFunctionCategory, IsThreadSafe = false, IsVolatile = false, IsExceptionSafe = true, IsClusterSafe = true)]
        public static object SubscribeProgress(string name)
        {
            return StorageExcelHelper.ExecuteExcelFunction(() =>
            {
                const string functionName = nameof(SubscribeProgress);
                return ExcelAsyncUtil.Observe(functionName, name, () =>
                {
                    ExcelCalcWrapper wrapper = ObjectCache.Instance.GetObject<ExcelCalcWrapper>(name);
                    var excelObserver = new CalcWrapperProgressObservable(wrapper);
                    return excelObserver;
                });
            });
        }

        [ExcelFunction(Name = AddIn.ExcelFunctionNamePrefix + nameof(SubscribeStatus),
//            Description = "TODO.", // TODO
            Category = AddIn.ExcelFunctionCategory, IsThreadSafe = false, IsVolatile = false, IsExceptionSafe = true)]
        public static object SubscribeStatus(string name)
        {
            return StorageExcelHelper.ExecuteExcelFunction(() =>
            {
                const string functionName = nameof(SubscribeStatus);
                return ExcelAsyncUtil.Observe(functionName, name, () =>
                {
                    ExcelCalcWrapper wrapper = ObjectCache.Instance.GetObject<ExcelCalcWrapper>(name);
                    var excelObserver = new CalcWrapperStatusObservable(wrapper);
                    return excelObserver;
                });
            });
        }

        [ExcelFunction(Name = AddIn.ExcelFunctionNamePrefix + nameof(SubscribeError),
    //            Description = "TODO.", // TODO
            Category = AddIn.ExcelFunctionCategory, IsThreadSafe = false, IsVolatile = false, IsExceptionSafe = true)]
        public static object SubscribeError(string name)
        {
            return StorageExcelHelper.ExecuteExcelFunction(() =>
            {
                const string functionName = nameof(SubscribeError);
                return ExcelAsyncUtil.Observe(functionName, name, () =>
                {
                    ExcelCalcWrapper wrapper = ObjectCache.Instance.GetObject<ExcelCalcWrapper>(name);
                    var excelObserver = new CalcWrapperExceptionObservable(wrapper);
                    return excelObserver;
                });
            });
        }

        [ExcelFunction(Name = AddIn.ExcelFunctionNamePrefix + nameof(SubscribeResultProperty),
//            Description = "TODO.",
            Category = AddIn.ExcelFunctionCategory, IsThreadSafe = false, IsVolatile = false, IsExceptionSafe = true)]
        public static object SubscribeResultProperty(string objectHandle, string propertyName, object returnedWhilstWaiting)
        {
            return StorageExcelHelper.ExecuteExcelFunction(() =>
            {
                const string functionName = nameof(SubscribeResultProperty);
                return ExcelAsyncUtil.Observe(functionName, new[] { objectHandle, propertyName, returnedWhilstWaiting }, () =>
                {
                    ExcelCalcWrapper wrapper = ObjectCache.Instance.GetObject<ExcelCalcWrapper>(objectHandle);
                    var excelObservable = new CalcWrapperResultPropertyObservable(wrapper, propertyName, returnedWhilstWaiting);
                    return excelObservable;
                });
            });
        }

        [ExcelFunction(Name = AddIn.ExcelFunctionNamePrefix + nameof(NumberOfRunningCalculations),
            Description = "Returns the number of calculations which are currently running.",
            Category = AddIn.ExcelFunctionCategory, IsThreadSafe = false, IsVolatile = true,
            IsExceptionSafe = true)]
        public static object NumberOfRunningCalculations()
        {
            return StorageExcelHelper.ExecuteExcelFunction(() =>
            {
                int runningCalcCount = 0;
                ICollection<string> objectHandles = ObjectCache.Instance.Handles;
                foreach (string objectHandle in objectHandles)
                {
                    object cachedObject;
                    if (ObjectCache.Instance.TryGetObject(objectHandle, out cachedObject))
                    {
                        ExcelCalcWrapper calcWrapper = cachedObject as ExcelCalcWrapper;
                        if (calcWrapper != null)
                            if (calcWrapper.Status == CalcStatus.Running)
                                runningCalcCount++;
                    }
                }
                return runningCalcCount;
            });
        }

        private static readonly object[] GetterParams = new object[0];

        [ExcelFunction(Name = AddIn.ExcelFunctionNamePrefix + nameof(GetObjectProperty),
//            Description = "TODO.", // TODO
            Category = AddIn.ExcelFunctionCategory, IsThreadSafe = false, IsVolatile = false, IsExceptionSafe = true)]
        public static object GetObjectProperty(string objectHandle, string propertyName)
        {
            return StorageExcelHelper.ExecuteExcelFunction(() =>
            {
                object cachedObject = ObjectCache.Instance.GetObject<object>(objectHandle);
                // TODO share code with CalcWrapperResultPropertyObservable
                Type cachedObjectType = cachedObject.GetType();
                PropertyInfo[] properties = cachedObjectType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetProperty);
                PropertyInfo propertyInfo = properties.FirstOrDefault(info => info.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase));

                if (propertyInfo == null)
                    throw new ArgumentException($"Result type {cachedObjectType.Name} has not public instance property called {propertyName}.");
                MethodInfo propertyGetter = propertyInfo.GetMethod;
                object propertyValue = propertyGetter.Invoke(cachedObject, GetterParams);
                return StorageExcelHelper.TransformForExcelReturn(propertyValue);
            });
        }

    }
}
