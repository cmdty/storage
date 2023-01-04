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

using Cmdty.TimePeriodValueTypes;
using ExcelDna.Integration;
using System;

namespace Cmdty.Storage.Excel
{
    public static class CmdtyStorageXl
    {
        [ExcelFunction(Name = AddIn.ExcelFunctionNamePrefix + nameof(CreateStorage),
            Description = "Creates and caches an object representing a storage facility.",
            Category = AddIn.ExcelFunctionCategory, IsThreadSafe = false, IsVolatile = false, IsExceptionSafe = true)]
        public static object CreateStorage(
            [ExcelArgument(Name = "Storage_name", Description = "Name of storage object to create.")] string name,
            [ExcelArgument(Name = ExcelArg.StorageStart.Name, Description = ExcelArg.StorageStart.Description)] DateTime storageStart,
            [ExcelArgument(Name = ExcelArg.StorageEnd.Name, Description = ExcelArg.StorageEnd.Description)] DateTime storageEnd,
            [ExcelArgument(Name = ExcelArg.Ratchets.Name, Description = ExcelArg.Ratchets.Description)] object ratchets,
            [ExcelArgument(Name = ExcelArg.RatchetInterpolation.Name, Description = ExcelArg.RatchetInterpolation.Description)] string ratchetInterpolation,
            [ExcelArgument(Name = ExcelArg.InjectionCost.Name, Description = ExcelArg.InjectionCost.Description)] double injectionCostRate,
            [ExcelArgument(Name = ExcelArg.CmdtyConsumedInject.Name, Description = ExcelArg.CmdtyConsumedInject.Description)] double cmdtyConsumedOnInjection,
            [ExcelArgument(Name = ExcelArg.WithdrawalCost.Name, Description = ExcelArg.WithdrawalCost.Description)] double withdrawalCostRate,
            [ExcelArgument(Name = ExcelArg.CmdtyConsumedWithdraw.Name, Description = ExcelArg.CmdtyConsumedWithdraw.Description)] double cmdtyConsumedOnWithdrawal,
            [ExcelArgument(Name = ExcelArg.TerminalInventoryConstraint.Name, Description = ExcelArg.TerminalInventoryConstraint.Description)] string terminalInventory,
            [ExcelArgument(Name = ExcelArg.NumericalTolerance.Name, Description = ExcelArg.NumericalTolerance.Description)] object numericalToleranceIn)
        {
            return StorageExcelHelper.ExecuteExcelFunction(() =>
            {
                return ObjectCache.Instance.CacheObjectAndGetHandle(name, new object[] {storageStart, storageEnd, ratchets,
                    ratchetInterpolation, injectionCostRate, cmdtyConsumedOnInjection, withdrawalCostRate,
                    cmdtyConsumedOnWithdrawal, terminalInventory, numericalToleranceIn}, () =>
                    {
                        double numericalTolerance = StorageExcelHelper.DefaultIfExcelEmptyOrMissing(numericalToleranceIn, 1E-10, "Numerical_tolerance");
                        CmdtyStorage<Day> storage = StorageExcelHelper.CreateCmdtyStorageFromExcelInputs<Day>(storageStart,
                            storageEnd, ratchets, ratchetInterpolation, injectionCostRate, cmdtyConsumedOnInjection,
                            withdrawalCostRate, cmdtyConsumedOnWithdrawal, terminalInventory, numericalTolerance);
                        return storage;
                    });
            });
        }

        [ExcelFunction(Name = AddIn.ExcelFunctionNamePrefix + nameof(StorageInjectionRate),
            Description = "Returns the maximum injection rate of a storage facility for a specific inventory and date.",
            Category = AddIn.ExcelFunctionCategory, IsThreadSafe = false, IsVolatile = false, IsExceptionSafe = true)]
        public static object StorageInjectionRate(
            [ExcelArgument(Name = ExcelArg.StorageHandle.Name, Description = ExcelArg.StorageHandle.Description)] string storageHandle,
            [ExcelArgument(Name = ExcelArg.StoragePropertyDate.Name, Description = ExcelArg.StoragePropertyDate.Description)] DateTime date,
            [ExcelArgument(Name = ExcelArg.StoragePropertyInventory.Name, Description = ExcelArg.StoragePropertyInventory.Description)] double inventory)
        {
            return StorageExcelHelper.ExecuteExcelFunction(() =>
            {
                CmdtyStorage<Day> storage = ObjectCache.Instance.GetObject<CmdtyStorage<Day>>(storageHandle);
                return storage.GetInjectWithdrawRange(Day.FromDateTime(date), inventory).MaxInjectWithdrawRate;
            });
        }

        [ExcelFunction(Name = AddIn.ExcelFunctionNamePrefix + nameof(StorageWithdrawalRate),
            Description = "Returns the maximum withdrawal rate of a storage facility for a specific inventory and date.",
            Category = AddIn.ExcelFunctionCategory, IsThreadSafe = false, IsVolatile = false, IsExceptionSafe = true)]
        public static object StorageWithdrawalRate(
            [ExcelArgument(Name = ExcelArg.StorageHandle.Name, Description = ExcelArg.StorageHandle.Description)] string storageHandle,
            [ExcelArgument(Name = ExcelArg.StoragePropertyDate.Name, Description = ExcelArg.StoragePropertyDate.Description)] DateTime date,
            [ExcelArgument(Name = ExcelArg.StoragePropertyInventory.Name, Description = ExcelArg.StoragePropertyInventory.Description)] double inventory)
        {
            return StorageExcelHelper.ExecuteExcelFunction(() =>
            {
                CmdtyStorage<Day> storage = ObjectCache.Instance.GetObject<CmdtyStorage<Day>>(storageHandle);
                return -storage.GetInjectWithdrawRange(Day.FromDateTime(date), inventory).MinInjectWithdrawRate;
            });
        }

        [ExcelFunction(Name = AddIn.ExcelFunctionNamePrefix + nameof(StorageMinInventory),
            Description = "Returns the minimum allowed inventory of a storage facility on a specific date.",
            Category = AddIn.ExcelFunctionCategory, IsThreadSafe = false, IsVolatile = false, IsExceptionSafe = true)]
        public static object StorageMinInventory(
            [ExcelArgument(Name = ExcelArg.StorageHandle.Name, Description = ExcelArg.StorageHandle.Description)] string storageHandle,
            [ExcelArgument(Name = ExcelArg.StoragePropertyDate.Name, Description = ExcelArg.StoragePropertyDate.Description)] DateTime date)
        {
            return StorageExcelHelper.ExecuteExcelFunction(() =>
            {
                CmdtyStorage<Day> storage = ObjectCache.Instance.GetObject<CmdtyStorage<Day>>(storageHandle);
                return storage.MinInventory(Day.FromDateTime(date));
            });
        }

        [ExcelFunction(Name = AddIn.ExcelFunctionNamePrefix + nameof(StorageMaxInventory),
            Description = "Returns the maximum allowed inventory of a storage facility on a specific date.",
            Category = AddIn.ExcelFunctionCategory, IsThreadSafe = false, IsVolatile = false, IsExceptionSafe = true)]
        public static object StorageMaxInventory(
            [ExcelArgument(Name = ExcelArg.StorageHandle.Name, Description = ExcelArg.StorageHandle.Description)] string storageHandle,
            [ExcelArgument(Name = ExcelArg.StoragePropertyDate.Name, Description = ExcelArg.StoragePropertyDate.Description)] DateTime date)
        {
            return StorageExcelHelper.ExecuteExcelFunction(() =>
            {
                CmdtyStorage<Day> storage = ObjectCache.Instance.GetObject<CmdtyStorage<Day>>(storageHandle);
                return storage.MaxInventory(Day.FromDateTime(date));
            });
        }

    }
}
