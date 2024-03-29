This document contains some thoughts on the calibration of the stochastic process parameters for the purpose of gas storage valuation.

### Market Calendar Spread Option Values
A lower bound for storage PV can be derived from a basket of forwards and
CSOs (Calendar Spread Options). This approach is known as the "basket-of-options" methodology. Should a liquid market exist for CSOs, then it
makes sense to calibrate the model parameters to match the PV (or more likely some transformation of PV like implied spread vol, or implied correlation) of the CSOs which constitute this lower bound portfolio.
This calibration will make it unlikely that the full storage PV breaches this lower bound, hence avoiding an arbitragable PV.
A precursor to this calibration would be to perform a basket-of-options
storage valuation in order to determine which CSO contracts constitute the
lower bound portfolio, and so should be calibrated to.

Under such a calibration approach, after the full storage PV has been 
calculated, it would then be of considerable interest to analyse the 
difference between the basket-of-options lower bound PV and the full 
storage PV. As this difference is purely model derived, it cannot be locked 
in, but rather is monetised through the rebalancing of CSO and forward 
hedges. When performing a valuation the uncertain nature of this part fo the PV
could be reflected by reducing (by way of a PV reserve) a proportion of
this premium of full PV over the lower bound.

Unfortunately we do not have experience of storage valuation in natural gas
markets where a liquid CSO market exists. As such, this approach has not been
investigated. If this sounds of interest please open a GitHub issue or 
[get in touch](mailto:jake@cmdty.co.uk?subject=Cmdty%20Storage%20CSO%20Calibration). 

### Calibration to European Option Implied Volatilities
An obvious calibration approach would be find the model parameters which
match the implied volatility of European options. Reasonably liquid 
European option markets exist for many natural gas hubs and often
represent the only standardised traded contracts which have extrinsic
value. Because model calibration to vanilla options is a common approach in financial
markets when pricing exotic options this might seem like the correct thing to do here.

It is our opinion that calibration to European options is not a good 
approach for a model to be used for storage valuation. A lower bound for
actual storage deals cannot be derived from European options, and in our
opinion European options are not useful for monetising the extrinsic value of storage.
This latter point could be contested using the logic that both storage
and European options have Vega and Gamma, hence the exposure of storage
PV from these Greeks can be offset with European option trades. However,
prior historical backtesting of the hedging of storage with European
options has been performed. It was found that neither Vega nor Gamma
hedging the storage with European options was effective.

Why was this not effective? As described above, storage extrinsic 
value is largely derived from calendar spread optionality. Such CSOs
can be priced using the implied volality of European options on the
two underlying delivery months. Assuming that the CSO expiry date is
very close to the expiry date of the European option on the front leg
the implied vol for this European option can directly be used. However,
as the expiry date of the back leg will be after the CSO expiry date, an
adjusted "off term" volatility of the back leg will need to be used in
CSO pricing. This off term volatility should be derivable from the standard
European option implied volatility, hence you can still theoretically see
the CSO having Vega with respect to the back leg European option implied vol.
A historical backtesting experiment
was also performed on the hedging of CSOs with European options. This
showed similar poor performance to that of hedging storage. Both
European and calendar spread options were priced with lognormal models
of the underlying. One explaination of the poor performance of this 
hedging experiment is that although a lognormal model does not capture
all aspects of a European option value (for example volatility smile)
it is a good enough model to manage the risk of such contracts.
However, the assumption of the two legs being correlated lognormal 
could be a sufficiently poor model to explain the variance of the 
calendar spread to render Vega and Gamma hedging of CSOs with European
options ineffective. Another reasons is that European options 
cannot hedge exposure from cross partial derivative terms like the
cross-gamma.

### Models Which Account for Smile
The above is written in the context of calibrating a lognormal model of forward prices to
European options, without taking into account volatility smile, hence would use implied 
volatilities for just a single strike per expiry. A model incorporating local and/or stochastic 
volatility could be used
to account for the smile. The cost of such models is a significant overhead in terms of the 
complexity and performance of both calibration and price simulation. As such it should be 
considered whether the product being priced has significant value sensitivity to the smile 
shape and hence parameters for the local/stochastic volatility model.

As already stated, the 
extrinsic value of storage is derived from the variance of calendar spreads. 
The sensitivity of calendar spread variance to volatility smile has not been investigated yet. 
But it’s our feeling that although there will be some sensitivity, it will be small and 
insignificant compared to the sensitivity to the correlation between the (log of) the forward 
prices of the two legs of the calendar spread under a purely lognormal model. Any error in PV
due to correlation estimate (which is itself hard to get right) will probably be much larger
than PV impact of incorporating smile.

Even if it were found that there is sensitivity of storage value to the smile we need to
consider whether it is practical to manage such risk using European options. If this isn’t
deemed practical then it makes the case for implementing a stochastic/local volatility even
less compelling. Our guess is that the transactions costs would make managing smile risk
impractical.

The conclusion is that it is not worth implementing a stochastic/local volatility model to price 
storage capacity. It is better to use a simpler model.

A shifted lognormal of forward prices can be used to model Black-76 smile (in a limited capacity) but in 
the context of natural gas it makes more sense to use this model to account for negative prices,
setting the shift parameter based on market prices and fundamentals. It is unlikely that a
shifted lognormal model will be able to fit market volatility smile.

### Calendar Spread Variance
Even in the absense of a CSO market, the contingency of storage extrinsic
value on calendar spread variance could still be of used as a calibration strategy. The approach would look something like:

* Calculate a matrix of CSO theoretical PVs. This could
be done using a historical analysis of calendar spread variances.
* Deduce which calendar spreads upon which the storage extrinsic value is mostly contingent using the basket-of-options approach.
* Solve for the model parameters which most closely give the calendar
spread variances estimated in the previous step.

The idea is that a preliminary basket-of-options valuation is used to 
determine which calendar sprear variances are most import to the storage
extrinsic value, and hence should be targetted in calibration.
One complication is that a stochastic model is itelf required for the first
step, albeit a more simplistic one than to value storage capacity.
Also, the structure of the stochastic model would need to be trusted in order
to not assign more extrinsic value to different calendar spread variances than those 
which were calibrated to.
This approach is currently just a rough idea and has not been investigated
in detail yet.

### Preferred Method: Storage Capacity Auction Results
The preferred approach is to use the transaction value of actual
storage deals as the calibration targets. Although a liquid market for
standardised storage capactity does not exist in markets we are familiar with, 
the value of storage 
transactions are available from the results of auctions. Energy market
participants could also have other sources, for example virtual
storage deals transacted with counterparts.

Specifically for the three-factor seasonal model, the following approach
has been applied with some success:
* Decide the long-term and seasonal-factor volatility parameters from
a historically analysis of forward price data time series, potentially augmented
with a view of how future fundamentals will affect price dynamics.
* Find the spot-factor mean reversion rate and volatility parameters
which minimise the sum of squared differences between the 
model-calculated storage PV and the observed transaction prices.

A practical example of this approach will be provided at a later date.

One advantage of calibrating the model to the PV at which storage actually
trades, is that these market prices should factor in real-world aspects 
which aren't included in the model assumption. Examples
of such assumptions are transaction costs and unhedgeable risk. Of course
a certain leap of faith is required to trust that the storage model then
accurately prices capacity which differs from the calibration targets,
something which can't be guaranteed. In a way, under this school of thought
the storage model is used to "interpolate" between observed market PVs. 

Clearly this approach is not suitable for users with the view that the
capacity auction results do not accurately reflect the actual value of
storage capacity, and are wishing to use a storage model to "beat" the
auction by calculating a more accurate value in order to win auctions
which are undervaluing capacity.

