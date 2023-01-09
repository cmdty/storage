# Copyright(c) 2019 Jake Fowler
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

from pythonnet import load


load("coreclr")


from cmdty_storage.__version__ import __version__
from cmdty_storage.cmdty_storage import CmdtyStorage, RatchetInterp
from cmdty_storage.intrinsic import intrinsic_value
from cmdty_storage.trinomial import trinomial_value, trinomial_deltas
from cmdty_storage.multi_factor import MultiFactorSpotSim, MultiFactorModel, three_factor_seasonal_value, \
    multi_factor_value, value_from_sims
from cmdty_storage.utils import FREQ_TO_PERIOD_TYPE, numerics_provider
import logging

logger: logging.Logger = logging.getLogger('cmdty.storage')
logger.addHandler(logging.NullHandler())
