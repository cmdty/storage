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
markets where a liquid CSO market exists. As such this approach has not been
investigated. If this sounds of interest please open a GitHub issue or 
[get in touch](mailto:jake@cmdty.co.uk?subject=Cmdty%20Storage%20CSO%20Calibration). 

### Calibration to European Option Implied Volatilities
An obvious calibration approach would be find the model parameters which
match the implied volatility of European options. Reasonably liquid 
European option markets exist for many natural gas hubs and often
represent the only standardised traded contracts which have extrinsic
value.

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
two underlying delivery months. A historical backtesting experiment
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
options ineffective.

### Storage Capacity Auction Results


### Calendar Spread Variance

