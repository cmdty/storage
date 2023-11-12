This document contains some thoughts on the calibration of the stochastic process parameters for the purpose of gas storage valuation.

### Calibration to European Option Implied Volatilities


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

### Storage Capacity Auction Results


### Calendar Spread Variance

