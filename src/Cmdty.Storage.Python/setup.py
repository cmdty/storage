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

import setuptools
import os

with open('README.md', 'r') as fh:
    long_description = fh.read()

version = {}
here = os.path.abspath(os.path.dirname(__file__))
with open(os.path.join(here, 'cmdty_storage', '__version__.py')) as fp:
    exec(fp.read(), version)

setuptools.setup(
    name='cmdty-storage',
    version=version['__version__'],
    author='Jake Fowler',
    author_email='jake@cmdty.co.uk',
    description='Multi-Factor Least Squares Monte Carlo energy storage valuation model.',
    long_description=long_description,
    long_description_content_type='text/markdown',
    url='https://github.com/cmdty/storage',
    packages=setuptools.find_packages(),
    keywords = 'commodities trading storage oil gas power quantitative finance',
    classifiers=[
        'Development Status :: 5 - Production/Stable',
        'Programming Language :: C#',
        'Intended Audience :: Developers',
        'Intended Audience :: Financial and Insurance Industry',
        'Programming Language :: Python :: 3',
        'Programming Language :: Python :: 3.7',
        'Programming Language :: Python :: 3.8',
        'Programming Language :: Python :: 3.9',
        'Programming Language :: Python :: 3.10',
        'Programming Language :: Python :: 3.11',
        'License :: OSI Approved :: MIT License',
        'Operating System :: Microsoft :: Windows',
        'Operating System :: POSIX :: Linux',
        'Operating System :: MacOS :: MacOS X',
        'Topic :: Office/Business :: Financial',
        'Topic :: Office/Business :: Financial :: Investment',
        'Topic :: Scientific/Engineering :: Mathematics',
        'Topic :: Software Development :: Libraries :: Python Modules',
    ],
    install_requires=[
        'pythonnet==3.0.1',
        'pandas>=1.0.0'
        ],
    python_requires='>=3.7, <3.12',
    package_data={'cmdty_storage' : [
                        'lib/*.dll',
                        'lib/*.pdb',
                        'lib/x86/*.dll',
                        'lib/x64/*.dll'
                    ]},
    include_package_data=True
)
