using ExcelDna.Integration;
using System;
using System.Collections.Generic;

namespace Cmdty.Storage.Excel
{
    public class ObjectHandler
    {
        public static ObjectHandler Instance { get; }= new ObjectHandler();

        static ObjectHandler()
        {
        }

        Dictionary<string, object> _objects = new Dictionary<string, object>();
        long _handleIndex = 1;

        // The combination of handleType and parameters will be used to uniquely identify the RTD topic
        // So createObject will only be called when a new handleType/parameters combination is used.
        public object GetHandle(string handleType, object[] parameters, Func<object> createObject)
        {
            return ExcelAsyncUtil.Observe(handleType, parameters, () =>
            {
                object value = createObject();
                string handle = handleType + ":" + _handleIndex++; // TODO use Interlocked.Increment?
                _objects[handle] = value;
                return new HandleObservable(this, handle);
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
    }

    class HandleObservable : IExcelObservable, IDisposable
    {
        ObjectHandler _handler;
        string _handle;
        IExcelObserver _observer;

        public HandleObservable(ObjectHandler handler, string handle)
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