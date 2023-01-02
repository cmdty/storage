#region License
// Copyright (c) 2023 Jake Fowler
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

using ExcelDna.Integration;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Cmdty.Storage.Excel
{
    public sealed class ObjectCache
    {
        private readonly ConcurrentDictionary<string, object> _objects = new ConcurrentDictionary<string, object>();
        private long _handleIndex = 1;

        static ObjectCache()
        {
        }

        public static ObjectCache Instance { get; } = new ObjectCache();

        public ICollection<string> Handles 
        { 
            get { return _objects.Keys; }
        }

        public object GetHandle(string handleType, object[] parameters, Func<object> createObject)
        {
            return ExcelAsyncUtil.Observe(handleType, parameters, () =>
            {
                object value = createObject();
                Interlocked.Increment(ref _handleIndex);
                string handle = handleType + ":" + _handleIndex;
                _objects[handle] = value;
                return new ObjectHandleObservable(this, handle);
            });
        }

        public bool TryGetObject(string handle, out object value) 
                                => _objects.TryGetValue(handle, out value);

        public T GetObject<T>(string handle)
        {
            object cachedObject;
            if (!_objects.TryGetValue(handle, out cachedObject))
                throw new ArgumentException($"No cached object with handled {handle}.");
            if (!(cachedObject is T))
                throw new ArgumentException($"Cached object with handle {handle} is not of expected " +
                    $"type {typeof(T).Name}.");
            return (T)cachedObject;
        }

        public void Remove(string handle)
        {
            if (_objects.TryRemove(handle, out object value))
            {
                var disposable = value as IDisposable;
                if (disposable != null)
                    disposable.Dispose();
            }
        }

        private sealed class ObjectHandleObservable : IExcelObservable, IDisposable
        {
            ObjectCache _handler;
            string _handle;
            IExcelObserver _observer;

            public ObjectHandleObservable(ObjectCache handler, string handle)
            {
                _handler = handler;
                _handle = handle;
            }

            public IDisposable Subscribe(IExcelObserver observer)
            {
                // We know this will only be called once, so we take some adventurous shortcuts (like returning 'this')
                _observer = observer;
                _observer.OnNext(_handle);
                return this;
            }

            public void Dispose() => _handler.Remove(_handle);

        }
    }
}