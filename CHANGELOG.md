
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

### 1.3.0 (not yet released)
* .NET binaries included in Python package target .NET Standard, not .NET Framework, hence compatible with
more .NET types.
* For non-Windows OS default to trying .NET (Core), rather than Mono as default runtime.

---
## Excel Add-In Releases

### 0.1.0
* First cut of asynchronous object handle based LSMC valuation.
* Object handle based intrinsic valuation.
