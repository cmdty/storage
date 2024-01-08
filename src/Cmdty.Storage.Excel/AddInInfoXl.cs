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

using System.Reflection;
using ExcelDna.Integration;

namespace Cmdty.Storage.Excel
{
    public static class AddInInfoXl
    {

        [ExcelFunction(Name = AddIn.ExcelFunctionNamePrefix + nameof(StorageAddInVersion),
            Category = AddIn.ExcelFunctionCategory, IsThreadSafe = true, IsVolatile = false, IsExceptionSafe = true)]
        public static object StorageAddInVersion(object trigger)
        {
            return StorageExcelHelper.ExecuteExcelFunction(() =>
            {
                Assembly currentAssembly = Assembly.GetExecutingAssembly();
                AssemblyInformationalVersionAttribute infoVersionAttribute =
                    currentAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                return infoVersionAttribute.InformationalVersion;
            });
        }

        [ExcelFunction(Name = AddIn.ExcelFunctionNamePrefix + nameof(LinearAlgebraProvider),
            Category = AddIn.ExcelFunctionCategory, IsThreadSafe = true, IsVolatile = false, IsExceptionSafe = true)]
        public static object LinearAlgebraProvider(object trigger)
        {
            return StorageExcelHelper.ExecuteExcelFunction(StorageHelper.LinearAlgebraProvider);
        }

        [ExcelFunction(Name = AddIn.ExcelFunctionNamePrefix + nameof(GetCalcMode),
            Category = AddIn.ExcelFunctionCategory, IsThreadSafe = true, IsVolatile = false, IsExceptionSafe = true)] // TODO delete when Ribbon radio button working
        public static object GetCalcMode(object trigger)
        {
            return StorageExcelHelper.ExecuteExcelFunction(() => AddIn.CalcMode.ToString("G"));
        }

    }
}
