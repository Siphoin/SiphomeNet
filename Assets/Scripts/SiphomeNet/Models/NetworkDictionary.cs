using System;
using System.Collections;
using System.Collections.Generic;
using SouthPointe.Serialization.MessagePack;
using Unity.Netcode;

namespace SiphomeNet.Network.Models
{
    [Serializable]
    public struct NetworkDictionary<TKey, TValue> : INetworkSerializable, IDictionary<TKey, TValue>, IReadOnlyDictionary<TKey, TValue>
    {
        private Dictionary<TKey, TValue> _dictionary;
        private static readonly MessagePackFormatter _formatter = new();

        public NetworkDictionary(Dictionary<TKey, TValue> dictionary)
        {
            _dictionary = dictionary ?? new Dictionary<TKey, TValue>();
        }

        public TValue this[TKey key]
        {
            get => _dictionary[key];
            set => _dictionary[key] = value;
        }

        public ICollection<TKey> Keys => _dictionary.Keys;
        public ICollection<TValue> Values => _dictionary.Values;
        public int Count => _dictionary.Count;
        public bool IsReadOnly => false;

        IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => Keys;
        IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => Values;

        public void Add(TKey key, TValue value) => _dictionary.Add(key, value);

        public void Add(KeyValuePair<TKey, TValue> item) => _dictionary.Add(item.Key, item.Value);

        public void Clear() => _dictionary.Clear();

        public bool Contains(KeyValuePair<TKey, TValue> item) =>
            TryGetValue(item.Key, out var value) && EqualityComparer<TValue>.Default.Equals(value, item.Value);

        public bool ContainsKey(TKey key) => _dictionary.ContainsKey(key);

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) =>
            ((ICollection<KeyValuePair<TKey, TValue>>)_dictionary).CopyTo(array, arrayIndex);

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => _dictionary.GetEnumerator();

        public bool Remove(TKey key) => _dictionary.Remove(key);

        public bool Remove(KeyValuePair<TKey, TValue> item) => _dictionary.Remove(item.Key);

        public bool TryGetValue(TKey key, out TValue value) => _dictionary.TryGetValue(key, out value);

        IEnumerator IEnumerable.GetEnumerator() => _dictionary.GetEnumerator();

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            if (serializer.IsReader)
            {
                byte[] data = null;
                serializer.SerializeValue(ref data);
                _dictionary = _formatter.Deserialize<Dictionary<TKey, TValue>>(data);
            }
            else
            {
                byte[] data = _formatter.Serialize(_dictionary);
                serializer.SerializeValue(ref data);
            }
        }

        public static implicit operator Dictionary<TKey, TValue>(NetworkDictionary<TKey, TValue> networkDict) =>
            networkDict._dictionary;

        public static implicit operator NetworkDictionary<TKey, TValue>(Dictionary<TKey, TValue> dictionary) =>
            new NetworkDictionary<TKey, TValue>(dictionary);
    }
}