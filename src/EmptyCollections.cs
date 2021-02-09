using System;
using System.Collections.Generic;

namespace Ibasa.Ripple
{
    internal sealed class EmptyEnumerator<T> : IEnumerator<T>
    {
        public static readonly EmptyEnumerator<T> Instance = new EmptyEnumerator<T>();

        private EmptyEnumerator() { }

        T IEnumerator<T>.Current => throw new NotImplementedException();

        object System.Collections.IEnumerator.Current => throw new NotImplementedException();

        void IDisposable.Dispose()
        {
        }

        bool System.Collections.IEnumerator.MoveNext()
        {
            return false;
        }

        void System.Collections.IEnumerator.Reset()
        {
        }
    }


    internal sealed class EmptyCollection<T> : ICollection<T>
    {
        public static readonly EmptyCollection<T> Instance = new EmptyCollection<T>();
        
        private EmptyCollection() { }

        int ICollection<T>.Count => 0;

        bool ICollection<T>.IsReadOnly => true;

        void ICollection<T>.Add(T item)
        {
            throw new NotImplementedException();
        }

        void ICollection<T>.Clear()
        {
            throw new NotImplementedException();
        }

        bool ICollection<T>.Contains(T item)
        {
            return false;
        }

        void ICollection<T>.CopyTo(T[] array, int arrayIndex)
        {
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return EmptyEnumerator<T>.Instance;
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return (this as IEnumerable<T>).GetEnumerator();
        }

        bool ICollection<T>.Remove(T item)
        {
            throw new NotImplementedException();
        }
    }

    internal sealed class EmptyDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    {
        public static readonly EmptyDictionary<TKey, TValue> Instance = new EmptyDictionary<TKey, TValue>();

        private EmptyDictionary() { }

        TValue IDictionary<TKey, TValue>.this[TKey key] { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        ICollection<TKey> IDictionary<TKey, TValue>.Keys => EmptyCollection<TKey>.Instance;

        ICollection<TValue> IDictionary<TKey, TValue>.Values => EmptyCollection<TValue>.Instance;

        int ICollection<KeyValuePair<TKey, TValue>>.Count => 0;

        bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly => true;

        void IDictionary<TKey, TValue>.Add(TKey key, TValue value)
        {
            throw new NotImplementedException();
        }

        void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item)
        {
            throw new NotImplementedException();
        }

        void ICollection<KeyValuePair<TKey, TValue>>.Clear()
        {
            throw new NotImplementedException();
        }

        bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> item)
        {
            return false;
        }

        bool IDictionary<TKey, TValue>.ContainsKey(TKey key)
        {
            return false;
        }

        void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
        }

        IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator()
        {
            return EmptyEnumerator<KeyValuePair<TKey, TValue>>.Instance;
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return (this as IEnumerable<KeyValuePair<TKey, TValue>>).GetEnumerator();
        }

        bool IDictionary<TKey, TValue>.Remove(TKey key)
        {
            throw new NotImplementedException();
        }

        bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item)
        {
            throw new NotImplementedException();
        }

        bool IDictionary<TKey, TValue>.TryGetValue(TKey key, out TValue value)
        {
            value = default;
            return false;
        }
    }
}
