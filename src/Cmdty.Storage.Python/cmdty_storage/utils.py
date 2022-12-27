# Copyright(c) 2020 Jake Fowler
#
# Permission is hereby granted, free of charge, to any person
# obtaining a copy of this software and associated documentation
# files (the "Software"), to deal in the Software without
# restriction, including without limitation the rights to use, 
# copy, modify, merge, publish, distribute, sublicense, and/or sell
# copies of the Software, and to permit persons to whom the
# Software is furnished to do so, subject to the following
# conditions:
#
# The above copyright notice and this permission notice shall be
# included in all copies or substantial portions of the Software.
#
# THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, 
# EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
# OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
# NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
# HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, 
# WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
# FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
# OTHER DEALINGS IN THE SOFTWARE.

import pandas as pd
import numpy as np
from datetime import datetime
import ctypes
import clr
import System as dotnet
import System.Collections.Generic as dotnet_cols_gen
from pathlib import Path

clr.AddReference(str(Path("cmdty_storage/lib/Cmdty.TimePeriodValueTypes")))
import Cmdty.TimePeriodValueTypes as net_tp

clr.AddReference(str(Path('cmdty_storage/lib/Cmdty.TimeSeries')))
import Cmdty.TimeSeries as ts

clr.AddReference(str(Path('cmdty_storage/lib/Cmdty.Storage')))
import Cmdty.Storage as net_cs

clr.AddReference(str(Path('cmdty_storage/lib/Cmdty.Core.Common')))
import Cmdty.Core.Common as net_cc

from datetime import date, datetime
import dateutil
import typing as tp
import re


def from_datetime_like(datetime_like: tp.Union[datetime, date, str, pd.Period], time_period_type):
    """ Converts either a pandas Period, str, datetime or date to a .NET Time Period"""
    date_time = py_date_like_to_net_datetime(datetime_like)
    return net_tp.TimePeriodFactory.FromDateTime[time_period_type](date_time)


def py_date_like_to_net_datetime(datetime_like: tp.Union[datetime, date, str, pd.Period]):
    """Converts either a pandas Period, str, datetime or date to a .NET DateTime."""
    if isinstance(datetime_like, str):
        datetime_like = dateutil.parser.parse(datetime_like)
    if hasattr(datetime_like, 'hour'):
        time_args = (datetime_like.hour, datetime_like.minute, datetime_like.second)
    else:
        time_args = (0, 0, 0)
    return dotnet.DateTime(datetime_like.year, datetime_like.month, datetime_like.day, *time_args)


def net_datetime_to_py_datetime(net_datetime):
    return datetime(net_datetime.Year, net_datetime.Month, net_datetime.Day, net_datetime.Hour, net_datetime.Minute,
                    net_datetime.Second, net_datetime.Millisecond * 1000)


def net_time_period_to_pandas_period(net_time_period, freq):
    start_datetime = net_datetime_to_py_datetime(net_time_period.Start)
    return pd.Period(start_datetime, freq=freq)


def series_to_double_time_series(series, time_period_type):
    """Converts an instance of pandas Series to a Cmdty.TimeSeries.TimeSeries type with Double data type."""
    return series_to_time_series(series, time_period_type, dotnet.Double, lambda x: x)


def series_to_time_series(series, time_period_type, net_data_type, data_selector):
    """Converts an instance of pandas Series to a Cmdty.TimeSeries.TimeSeries."""
    series_len = len(series)
    net_indices = dotnet.Array.CreateInstance(time_period_type, series_len)
    net_values = dotnet.Array.CreateInstance(net_data_type, series_len)

    for i in range(series_len):
        net_indices[i] = from_datetime_like(series.index[i], time_period_type)
        net_values[i] = data_selector(series.values[i])

    return ts.TimeSeries[time_period_type, net_data_type](net_indices, net_values)


def data_frame_to_net_double_panel(data_frame: pd.DataFrame, time_period_type):
    """Converts an instance of pandas DataFrame to a Cmdty.Core.Common.Panel<T, double>."""
    num_periods = len(data_frame.index)
    num_cols = len(data_frame.columns)
    net_indices = dotnet.Array.CreateInstance(time_period_type, num_periods)
    for i in range(num_periods):
        net_indices[i] = from_datetime_like(data_frame.index[i], time_period_type)
    net_values = as_net_array(data_frame.values.flatten(order='C'))
    return net_cc.Panel[time_period_type, dotnet.Double](net_values, net_indices, num_cols)


def net_time_series_to_pandas_series(net_time_series, freq):
    """Converts an instance of class Cmdty.TimeSeries.TimeSeries to a pandas Series"""
    if net_time_series.IsEmpty:
        return pd.Series()
    curve_start = net_time_series.Indices[0].Start
    curve_start_datetime = net_datetime_to_py_datetime(curve_start)
    index = pd.period_range(start=curve_start_datetime, freq=freq, periods=net_time_series.Count)
    prices = [net_time_series.Data[idx] for idx in range(0, net_time_series.Count)]
    return pd.Series(prices, index)


def is_scalar(arg):
    return isinstance(arg, int) or isinstance(arg, float)


def raise_if_none(arg, error_message):
    if arg is None:
        raise ValueError(error_message)


def raise_if_not_none(arg, error_message):
    if arg is not None:
        raise ValueError(error_message)


FREQ_TO_PERIOD_TYPE = {
    "15min": net_tp.QuarterHour,
    "30min": net_tp.HalfHour,
    "H": net_tp.Hour,
    "D": net_tp.Day,
    "M": net_tp.Month,
    "Q": net_tp.Quarter
}
""" dict of str: .NET time period type.
Each item describes an allowable granularity of curves constructed, as specified by the 
freq parameter in the curves public methods.

The keys represent the pandas Offset Alias which describe the granularity, and will generally be used
    as the freq of the pandas Series objects returned by the curve construction methods.
The values are the associated .NET time period types used in behind-the-scenes calculations.
"""


def wrap_settle_for_dotnet(py_settle_func, freq):
    def wrapper_settle_function(py_function, net_time_period, freq):
        pandas_period = net_time_period_to_pandas_period(net_time_period, freq)
        py_function_result = py_function(pandas_period)
        net_settle_day = from_datetime_like(py_function_result, net_tp.Day)
        return net_settle_day

    def wrapped_function(net_time_period):
        return wrapper_settle_function(py_settle_func, net_time_period, freq)

    time_period_type = FREQ_TO_PERIOD_TYPE[freq]
    return dotnet.Func[time_period_type, net_tp.Day](wrapped_function)


def wrap_on_progress_for_dotnet(py_on_progress):
    if py_on_progress is None:
        return None
    return dotnet.Action[dotnet.Double](py_on_progress)


# TODO get rid of TimePeriodSpecType or ForwardPointType?
# TODO check that each type definition is correct still
TimePeriodSpecType = tp.Union[str, datetime, date, pd.Period]
ForwardPointType = tp.Union[str, date, datetime, pd.Period]
CurveType = tp.Union[pd.Series, tp.Dict[ForwardPointType, float]]
TimeFunctionType = tp.Callable[[tp.Union[date, datetime], tp.Union[date, datetime]], float]
FwdContractType = tp.Union[date, datetime, pd.Period, float,
                           tp.Tuple[date, date], tp.Tuple[datetime, datetime],
                           tp.Tuple[pd.Period, pd.Period]]
FwdContractsType = tp.Iterable[FwdContractType]


def curve_to_net_dict(curve: CurveType, time_period_type):
    """Creates a .NET Dictionary<T, Double> instance from a Python curve, with type defined by CurveType."""
    ret = dotnet_cols_gen.Dictionary[time_period_type, dotnet.Double]()
    for key in curve.keys():
        net_key = from_datetime_like(key, time_period_type)
        ret.Add(net_key, curve[key])
    return ret


_MAP_NP_NET = {
    np.dtype('float32'): dotnet.Single,
    np.dtype('float64'): dotnet.Double,
    np.dtype('int8'): dotnet.SByte,
    np.dtype('int16'): dotnet.Int16,
    np.dtype('int32'): dotnet.Int32,
    np.dtype('int64'): dotnet.Int64,
    np.dtype('uint8'): dotnet.Byte,
    np.dtype('uint16'): dotnet.UInt16,
    np.dtype('uint32'): dotnet.UInt32,
    np.dtype('uint64'): dotnet.UInt64,
    np.dtype('bool'): dotnet.Boolean,
}


def as_net_array(np_array: np.ndarray):
    """
    Given a `numpy.ndarray` returns a CLR `System.Array`.  See _MAP_NP_NET for
    the mapping of Numpy dtypes to CLR types.
    """
    dims = np_array.shape
    dtype = np_array.dtype
    if dtype not in _MAP_NP_NET:
        raise NotImplementedError("as_net_array does not yet support dtype {}".format(dtype))
    net_dims = dotnet.Array.CreateInstance(dotnet.Int32, np_array.ndim)
    for idx in range(np_array.ndim):
        net_dims[idx] = dotnet.Int32(dims[idx])

    if not np_array.flags.c_contiguous:
        np_array = np_array.copy(order='C')
    assert np_array.flags.c_contiguous

    net_array = dotnet.Array.CreateInstance(_MAP_NP_NET[dtype], *net_dims)

    try:  # Memmove
        dest_handle = dotnet.Runtime.InteropServices.GCHandle.Alloc(net_array,
                                                                    dotnet.Runtime.InteropServices.GCHandleType.Pinned)
        source_ptr = np_array.__array_interface__['data'][0]
        dest_ptr = dest_handle.AddrOfPinnedObject().ToInt64()
        ctypes.memmove(dest_ptr, source_ptr, np_array.nbytes)
    finally:
        if dest_handle.IsAllocated:
            dest_handle.Free()
    return net_array


_MAP_NET_NP = {
    'Single': np.dtype('float32'),
    'Double': np.dtype('float64'),
    'SByte': np.dtype('int8'),
    'Int16': np.dtype('int16'),
    'Int32': np.dtype('int32'),
    'Int64': np.dtype('int64'),
    'Byte': np.dtype('uint8'),
    'UInt16': np.dtype('uint16'),
    'UInt32': np.dtype('uint32'),
    'UInt64': np.dtype('uint64'),
    'Boolean': np.dtype('bool'),
}


def as_numpy_array(net_array) -> np.ndarray:
    """
    Given a CLR `System.Array` returns a `numpy.ndarray`.  See _MAP_NET_NP for
    the mapping of CLR types to Numpy dtypes.
    """
    dims = np.empty(net_array.Rank, dtype=int)
    for idx in range(net_array.Rank):
        dims[idx] = net_array.GetLength(idx)
    net_type = net_array.GetType().GetElementType().Name

    try:
        np_array = np.empty(dims, order='C', dtype=_MAP_NET_NP[net_type])
    except KeyError:
        raise NotImplementedError("asNumpyArray does not yet support System type {}".format(net_type))

    try:  # Memmove
        source_handle = dotnet.Runtime.InteropServices.GCHandle.Alloc(
            net_array, dotnet.Runtime.InteropServices.GCHandleType.Pinned)
        source_ptr = source_handle.AddrOfPinnedObject().ToInt64()
        dest_ptr = np_array.__array_interface__['data'][0]
        ctypes.memmove(dest_ptr, source_ptr, np_array.nbytes)
    finally:
        if source_handle.IsAllocated:
            source_handle.Free()
    return np_array


def to_period_range(freq: str,
                    fwd_contract: FwdContractType) -> tp.Tuple[pd.Period, pd.Period]:
    if isinstance(fwd_contract, pd.Period):
        return fwd_contract.asfreq(freq, 's'), _last_period(fwd_contract, freq)
    if isinstance(fwd_contract, tuple):
        start = fwd_contract[0]
        end = fwd_contract[1]
    else:
        start = fwd_contract
        end = fwd_contract
    if isinstance(start, pd.Period):
        start_period = start.asfreq(freq, 's')
    else:
        start_period = pd.Period(start, freq=freq)
    if isinstance(end, pd.Period):
        end_period = _last_period(end, freq)
    else:
        end_period = pd.Period(end, freq=freq)
    return start_period, end_period


def _last_period(period: pd.Period, freq: str) -> pd.Period:
    """Find the last pandas Period instance of a specific frequency within a Period instance"""
    if not freq[0].isdigit():
        return period.asfreq(freq, 'e')
    m = re.match("(\d+)(\w+)", freq)
    num = int(m.group(1))
    sub_freq = m.group(2)
    return (period.asfreq(sub_freq, 'e') - num + 1).asfreq(freq)


def numerics_provider() -> str:
    return net_cs.StorageHelper.LinearAlgebraProvider()


def net_panel_to_data_frame(net_panel, freq: str) -> pd.DataFrame:
    np_array = as_numpy_array(net_panel.RawData)
    np_array.resize((net_panel.NumRows, net_panel.NumCols))
    sim_periods = [net_time_period_to_pandas_period(p, freq) for p in net_panel.RowKeys]
    period_index = pd.PeriodIndex(data=sim_periods, freq=freq)
    return pd.DataFrame(data=np_array, index=period_index)


def create_net_log_adapter(logger, net_logger_type):
    def log(level, msg):
        logger.log(level, msg)

    is_enabled = dotnet.Func[dotnet.Int32, dotnet.Boolean](lambda level: logger.isEnabledFor(level))
    py_log = dotnet.Action[dotnet.Int32, dotnet.String](log)
    return net_cs.PythonHelpers.PythonLoggerAdapter[net_logger_type](is_enabled, py_log)
