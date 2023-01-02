using ExcelDna.Integration;
using System;
using System.Collections.Generic;

namespace Cmdty.Storage.Excel
{
    public sealed class ObjectCache
    {
        public static ObjectCache Instance { get; }= new ObjectCache();

        static ObjectCache()
        {
        }

        public IReadOnlyDictionary<string, object> Objects { get { return _objects; } }

        Dictionary<string, object> _objects = new Dictionary<string, object>();
        long _handleIndex = 1;

        public object GetHandle(string handleType, object[] parameters, Func<object> createObject)
        {
            return ExcelAsyncUtil.Observe(handleType, parameters, () =>
            {
                object value = createObject();
                string handle = handleType + ":" + _handleIndex++; // TODO use Interlocked.Increment?
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
            object value;
            if (TryGetObject(handle, out value))
            {
                _objects.Remove(handle);
                var disp = value as IDisposable;
                if (disp != null)
                {
                    disp.Dispose();
                }
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