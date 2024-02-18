
## Python PIP Package Releases
### 1.0.0
* Update pythonnet dependency from 2.5.1 to 2.5.2.

### 1.1.0
* Update pythonnet dependency from 2.5.1 to 3.0.1 to allow compatibility with Python up to version 3.11.

### 1.2.0
* Addition of `value_from sims` function which allows users to provide their
own price simulation data.
* Markovian factors included in returned type for both C# and Python.
* Error raised if CmdtyStorage instance created with step ratchets, but 
terminal_storage_npv not specified, as this virtually always results in an 
error during the valuation.

### 1.3.0
* .NET binaries included in Python package target .NET Standard, not .NET Framework, hence compatible with
more .NET types.
* For non-Windows OS default to trying .NET (Core), rather than Mono as default runtime.

### 1.4.0
* sim_data_returned added to Monte Carlo valuation functions to allow the caller to control which simulation-level
data is returned.

### 1.5.0
* Update pythonnet dependency to <3.1.0. This allows reference to the latest version (3.0.3) which is compatible with Python up to version 3.12.
* Add standard error to results.

---
## Excel Add-In Releases

### 0.1.0
* First cut of asynchronous object handle based LSMC valuation.
* Object handle based intrinsic valuation.

### 0.2.0
* Change to allowable values Terminal_inventory argument of cmdty.CreateStorage function and addition of
associated Terminal_val_param argument.

### 0.3.0
* Async and blocking modes for calculation.
* Log-linear interpolation of discount factors.
* Forward curve interpolation.
* Upgrade Excel-DNA to version 1.7.0.

### 0.3.1 (not yet released)
* Binaries not packed into the add-in xll file, due to xll getting flagged as malicious.
* Standard error included in results and sample spreadsheet.

---
## NuGet Package Releases
### 1.1.0
* SimulationDataReturned enum defined and property of this type added to LsmcValuationParameters to allow the caller to control which simulation-level
data is populated in the returned LsmcStorageValuationResults instance.
