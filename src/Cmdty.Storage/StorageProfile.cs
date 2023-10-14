#region License
// Copyright (c) 2020 Jake Fowler
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

namespace Cmdty.Storage
{
    public sealed class StorageProfile // TODO replace by record type
    {
        public double Inventory { get; }
        public double InjectWithdrawVolume { get; }
        public double CmdtyConsumed { get; }
        public double InventoryLoss { get; }
        public double NetVolume => -InjectWithdrawVolume - CmdtyConsumed;
        public double PeriodPv { get; set; }

        public StorageProfile(double inventory, double injectWithdrawVolume, double cmdtyConsumed, 
                                double inventoryLoss, double periodPv)
        {
            Inventory = inventory;
            InjectWithdrawVolume = injectWithdrawVolume;
            CmdtyConsumed = cmdtyConsumed;
            InventoryLoss = inventoryLoss;
            PeriodPv = periodPv;
        }

        public override string ToString()
        {
            return $"{nameof(Inventory)}: {Inventory}, {nameof(InjectWithdrawVolume)}: {InjectWithdrawVolume}, " +
                   $"{nameof(CmdtyConsumed)}: {CmdtyConsumed}, {nameof(InventoryLoss)}: {InventoryLoss}, " +
                   $"{nameof(NetVolume)}: {NetVolume}, {nameof(PeriodPv)}: {PeriodPv}";
        }

        public void Deconstruct(out double inventory, out double injectWithdrawVolume, out double cmdtyConsumed, out double inventoryLoss, 
            out double netPosition, out double periodPv)
        {
            inventory = Inventory;
            injectWithdrawVolume = InjectWithdrawVolume;
            cmdtyConsumed = CmdtyConsumed;
            inventoryLoss = InventoryLoss;
            netPosition = NetVolume;
            periodPv = PeriodPv;
        }

    }
}
