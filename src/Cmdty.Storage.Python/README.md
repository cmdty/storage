## Overview
Storage valuation and optimisation model implemented using the Longstaff Schwartz Least Squares 
Monte Carlo technique.
[A multi-factor model](https://github.com/cmdty/core/blob/master/docs/multifactor_price_process/multifactor_price_process.pdf) 
is used to for the commodity price dynamics. This allows for a complex 
volatility and correlations structure between forward rates as well as calibration to option 
implied volatilities.


The models can be used for any commodity, although are most suitable for natural gas 
storage valuation and optimisation.

Calculations take into account many of the complex features of physical storage including:
* Inventory dependent injection and withdrawal rates, otherwise known as ratchets. For physical storage it is often the case that maximum withdrawal rates will increase, and injection rates will decrease as the storage inventory increases. For natural gas, this due to the increased pressure within the storage cavern.
* Time dependent injection and withdrawal rates, including the ability to add outages when no injection or withdrawal is allowed.
* Forced injection/withdrawal, as can be enforced by regulatory or physical constraints.
* Commodity consumed on injection/withdrawal, for example where natural gas is consumed by the motors that power injection into storage.
* Time dependent minimum and maximum inventory, necessary if different notional volumes of a storage facility are leased for different consecutive years.
* Optional time and inventory dependent loss of commodity in storage. For example this assumption is necessary for electricity storage which isn't 100% efficient.
* Ability to constrain the storage to be empty at the end of it's life, or specify a value of commodity inventory left in storage.


## Examples
### Creating the Storage Object
The first step is to create an instance of the class CmdtyStorage which
represents the storage facility. See below for two examples of this. The first example creates
a simple storage object with constant constraints. The second examples creates a storage
object with inventory-varying injection and withdrawal rates, commonly known as "ratchets".

For full details on how to create CmdtyStorage instances see the Jupyter notebook 
[creating_storage_instances.ipynb](https://github.com/cmdty/storage/blob/master/samples/python/creating_storage_instances.ipynb).

```python
from cmdty_storage import CmdtyStorage, RatchetInterp
import pandas as pd
storage_simple = CmdtyStorage(
    freq='D',
    storage_start = '2021-04-01',
    storage_end = '2022-04-01',
    injection_cost = 0.01,
    withdrawal_cost = 0.025,
    min_inventory = 0.0,
    max_inventory = 1500.0,
    max_injection_rate = 25.5,
    max_withdrawal_rate = 30.9
)

storage_with_ratchets = CmdtyStorage(
    freq='D',
    storage_start = '2021-04-01',
    storage_end = '2022-04-01',
    injection_cost = 0.01,
    withdrawal_cost = 0.025,
    ratchets= [
                ('2021-04-01', # For days after 2021-04-01 (inclusive) until 2022-10-01 (exclusive):
                       [
                            (0.0, -150.0, 250.0),    # At min inventory of zero, max withdrawal of 150, max injection 250
                            (2000.0, -200.0, 175.0), # At inventory of 2000, max withdrawal of 200, max injection 175
                            (5000.0, -260.0, 155.0), # At inventory of 5000, max withdrawal of 260, max injection 155
                            (7000.0, -275.0, 132.0), # At max inventory of 7000, max withdrawal of 275, max injection 132
                        ]),
                  ('2022-10-01', # For days after 2022-10-01 (inclusive):
                       [
                            (0.0, -130.0, 260.0),    # At min inventory of zero, max withdrawal of 130, max injection 260
                            (2000.0, -190.0, 190.0), # At inventory of 2000, max withdrawal of 190, max injection 190
                            (5000.0, -230.0, 165.0), # At inventory of 5000, max withdrawal of 230, max injection 165
                            (7000.0, -245.0, 148.0), # At max inventory of 7000, max withdrawal of 245, max injection 148
                        ]),
                 ],
    ratchet_interp = RatchetInterp.LINEAR
)

```

### Storage Optimisation Using LSMC
The following is an example of valuing the storage using LSMC and a [three-factor seasonal model](https://github.com/cmdty/core/blob/master/docs/three_factor_seasonal_model/three_factor_seasonal_model.pdf) of price dynamics.

```python
from cmdty_storage import three_factor_seasonal_value

# Creating the Inputs
monthly_index = pd.period_range(start='2021-04-25', periods=25, freq='M')
monthly_fwd_prices = [16.61, 15.68, 15.42, 15.31, 15.27, 15.13, 15.96, 17.22, 17.32, 17.66, 
                      17.59, 16.81, 15.36, 14.49, 14.28, 14.25, 14.32, 14.33, 15.30, 16.58, 
                      16.64, 16.79, 16.64, 15.90, 14.63]
fwd_curve = pd.Series(data=monthly_fwd_prices, index=monthly_index).resample('D').fillna('pad')

rates = [0.005, 0.006, 0.0072, 0.0087, 0.0101, 0.0115, 0.0126]
rates_pillars = pd.PeriodIndex(freq='D', data=['2021-04-25', '2021-06-01', '2021-08-01', '2021-12-01', '2022-04-01', 
                                              '2022-12-01', '2023-12-01'])
ir_curve = pd.Series(data=rates, index=rates_pillars).resample('D').asfreq('D').interpolate(method='linear')

def settlement_rule(delivery_date):
    return delivery_date.asfreq('M').asfreq('D', 'end') + 20

# Call the three-factor seasonal model
three_factor_results = three_factor_seasonal_value(
    cmdty_storage = storage_with_ratchets,
    val_date = '2021-04-25',
    inventory = 1500.0,
    fwd_curve = fwd_curve,
    interest_rates = ir_curve,
    settlement_rule = settlement_rule,
    num_sims = 2000,
    seed = 12,
    spot_mean_reversion = 91.0,
    spot_vol = 0.85,
    long_term_vol =  0.30,
    seasonal_vol = 0.19,
    basis_funcs = '1 + x_st + x_sw + x_lt + s + x_st**2 + x_sw**2 + x_lt**2 + s**2 + s * x_st',
    discount_deltas = True
)

# Inspect the NPV results
print('Full NPV:\t{0:,.0f}'.format(three_factor_results.npv))
print('Intrinsic NPV: \t{0:,.0f}'.format(three_factor_results.intrinsic_npv))
print('Extrinsic NPV: \t{0:,.0f}'.format(three_factor_results.extrinsic_npv))
```
Prints the following.

```
Full NPV:	    78,175
Intrinsic NPV: 	40,976
Extrinsic NPV: 	37,199
```
For comprehensive documentation of invoking the LSMC model, using both the three-factor price model,
and a more general multi-factor model, see the notebook [multifactor_storage.ipynb](https://github.com/cmdty/storage/blob/master/samples/python/multifactor_storage.ipynb).

### Inspecting Valuation Results
The object returned from the calling `three_factor_seasonal_value` has many properties containing useful information. The code below give examples of a
few of these. See the **Valuation Results** section of [multifactor_storage.ipynb](https://github.com/cmdty/storage/blob/master/samples/python/multifactor_storage.ipynb) for more details.

Plotting the daily Deltas and projected inventory:
```python
%matplotlib inline
ax_deltas = three_factor_results.deltas.plot(title='Daily Deltas vs Projected Inventory', legend=True, label='Delta')
ax_deltas.set_ylabel('Delta')
inventory_projection = three_factor_results.expected_profile['inventory']
ax_inventory = inventory_projection.plot(secondary_y=True, legend=True, ax=ax_deltas, label='Expected Inventory')
h1, l1 = ax_deltas.get_legend_handles_labels()
h2, l2 = ax_inventory.get_legend_handles_labels()
ax_inventory.set_ylabel('Inventory')
ax_deltas.legend(h1+h2, l1+l2, loc=1)
```

![Delta Chart](https://github.com/cmdty/storage/raw/master/assets/delta_inventory_chart.png)

The **trigger_prices** property contains information on "trigger prices" which are approximate spot price levels at which the exercise decision changes.
* The withdraw trigger price is the spot price level, at time of nomination, above which the optimal decision will change to withdraw.
* The inject trigger price is the spot price level, at time of nomination, below which the optimal decision will change to inject.

Plotting the trigger prices versus the forward curve:
```python
%matplotlib inline
ax_triggers = three_factor_results.trigger_prices['inject_trigger_price'].plot(
    title='Trigger Prices vs Forward Curve', legend=True)
three_factor_results.trigger_prices['withdraw_trigger_price'].plot(legend=True)
fwd_curve['2021-04-25' : '2022-04-01'].plot(legend=True)
ax_triggers.legend(['Inject Trigger Price', 'Withdraw Trigger', 'Forward Curve'])
```
![Trigger Prices Chart](https://github.com/cmdty/storage/raw/master/assets/trigger_prices_chart.png)

## Example GUI
An example GUI notebook created using Jupyter Widgets can be found 
[here](https://github.com/cmdty/storage/blob/master/samples/python/multi_factor_gui.ipynb).

![Demo GUI](https://github.com/cmdty/storage/raw/master/assets/gui_demo.gif)
