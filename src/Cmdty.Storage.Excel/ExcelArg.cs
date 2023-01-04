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

namespace Cmdty.Storage.Excel
{
    internal static class ExcelArg
    {
        internal static class CachedObjectName
        {
            public const string Name = "Name";
            public const string Description = "Name of cached object to create.";
        }

        internal static class ValDate
        {
            public const string Name = "Val_date";
            public const string Description = "Current date assumed for valuation.";
        }

        internal static class StorageStart
        {
            public const string Name = "Storage_start";
            public const string Description = "Date-time on which storage facility first becomes active.";
        }

        internal static class StorageEnd
        {
            public const string Name = "Storage_end";
            public const string Description = "Date-time on which storage facility ceases being active. Injections/withdrawal are only allowed on time periods before the end.";
        }

        internal static class Ratchets
        {
            public const string Name = "Ratchets";
            public const string Description = "Table of time-dependent injection, withdrawal and inventory constraints. Range with 4 columns; date-time, inventory, injection rate and withdrawal rate. Withdrawal rates are expressed as a negative numbers.";
        }

        internal static class RatchetInterpolation
        {
            public const string Name = "Ratchet_interpolation";
            public const string Description = "Text which determines how injection/withdrawal rates are interpolated by inventory. Must be either 'PiecewiseLinear', 'Polynomial', or 'Step'.";
        }

        internal static class InjectionCost
        {
            public const string Name = "Inject_cost";
            public const string Description = "The cost of injecting commodity into the storage, for every unit of quantity injected.";
        }

        internal static class CmdtyConsumedInject
        {
            public const string Name = "Inject_cmdty_consume";
            public const string Description = "The quantity of commodity consumed upon injection, expressed as a percentage of quantity injected.";
        }

        internal static class WithdrawalCost
        {
            public const string Name = "Withdraw_cost";
            public const string Description = "The cost of withdrawing commodity out of the storage, for every unit of quantity withdrawn.";
        }

        internal static class CmdtyConsumedWithdraw
        {
            public const string Name = "Withdraw_cmdty_consume";
            public const string Description = "The quantity of commodity consumed upon withdrawal, expressed as a percentage of quantity withdrawn.";
        }
        
        internal static class Inventory
        {
            public const string Name = "Inventory";
            public const string Description = "The quantity of commodity currently being stored.";
        }

        internal static class ForwardCurve
        {
            public const string Name = "Forward_curve";
            public const string Description = "Forward, swap, or futures curve for the underlying stored commodity. Should consist of a two column range, with date-times in the first column (the delivery date), and numbers in the second column (the forward price).";
        }

        internal static class SpotVolCurve
        {
            public const string Name = "Spot_vol_curve";
            public const string Description = "Time-dependent volatility for one-factor spot price process. Should consist of a two column range, with date-times in the first column (the delivery date), and numbers in the second column (the spot vol).";
        }

        internal static class MeanReversion
        {
            public const string Name = "Mean_reversion";
            public const string Description = "Mean reversion rate of one-factor spot price process.";
        }

        internal static class InterestRateCurve
        {
            public const string Name = "Ir_curve";
            public const string Description = "Interest rate curve used to discount cash flows to present value, following Act/365 day count and continuous compounding. Any gaps in the curve are linearly interpolated.";
        }

        internal static class NumGridPoints
        {
            public const string Name = "[Num_grid_points]";
            public const string Description = "Optional parameter specifying the number of points in the inventory space grid used for backward induction. A higher value gives more accuracy, but a longer running time. Defaults to 100 if omitted.";
            public const int Default = 100;
        }

        internal static class NumericalTolerance
        {
            public const string Name = "[Numerical_tolerance]";
            public const string Description = "Optional parameter specifying the numerical tolerance. This should be small number used as a tolerance in numerical routines when comparing two floating point numbers. Defaults to 1E-10 if omitted.";
        }

        internal static class StorageHandle
        {
            public const string Name = "Storage_handle";
            public const string Description = "Handle to cached storage object.";
        }

        internal static class SpotMeanReversion
        {
            public const string Name = "Spot_mean_reversion";
            public const string Description = "Mean reversion of the spot factor of a three-factor seasonal process."; // TODO change this if decided to automatically multiply by 365.25
        }

        internal static class SpotVol
        {
            public const string Name = "Spot_vol";
            public const string Description = "Volatility of the spot factor of a three-factor seasonal process.";
        }

        internal static class LongTermVol
        {
            public const string Name = "Long_term_vol";
            public const string Description = "Volatility of the long-term of for a three-factor seasonal process.";
        }

        internal static class SeasonalVol
        {
            public const string Name = "Seasonal_vol";
            public const string Description = "Volatility of the seasonal of for a three-factor seasonal process.";
        }

        internal static class NumSims
        {
            public const string Name = "Num_sims";
            public const string Description = "Number of Monte Carlo paths used for the simulation.";
        }

        internal static class BasisFunctions
        {
            public const string Name = "Basis_functions";
            public const string Description = "Text representing basis functions use when calculating continuations values using regression.";
        }

        internal static class DiscountDeltas
        {
            public const string Name = "Discount_deltas";
            public const string Description = "Boolean flag indicating whether the deltas should be discounted or not.";
        }

        internal static class Seed
        {
            public const string Name = "[Seed]";
            public const string Description = "Optional integer argument used as the seed to the Mersenne Twister random number generator. Defaults to a random seed if omitted.";
        }

        internal static class ForwardSimSeed
        {
            public const string Name = "[Fwd_seed]";
            public const string Description = "Optional integer argument used as the seed Mersenne seed for the forward simulation. If omitted the forward simulation will use " 
                                              + "a continuation for the backward simulation stream.";
        }

        internal static class ExtraDecisions
        {
            public const string Name = "[Extra_decisions]";
            public const string Description = "Optional integer argument specifying the number of decision pairs to use on top of the usual bang-bang decision set. Defaults to 0 if omitted.";
        }

        internal static class SettleDates
        {
            public const string Name = "Settle_dates";
            public const string Description = "Dates on which delivered volume is financially settled. Should consist of a two column range, with months in the first column (the delivery month), and dates in the second column (the settlement dates).";
        }

        internal static class TerminalInventoryConstraint
        {
            public const string Name = "Terminal_inventory";
            public const string Description = "Text which determines the constraint on, and value of, inventory on the end date of the storage. Must be either 'Empty', 'ZeroValue', or 'SpotValue'.";
        }

        internal static class StoragePropertyDate
        {
            public const string Name = "Date";
            public const string Description = "The date of injection/withdrawal on which this storage property is evaluated.";
        }

        internal static class StoragePropertyInventory
        {
            public const string Name = "Inventory";
            public const string Description = "The inventory at which this storage property is evaluated.";
        }

    }
}
