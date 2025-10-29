using SouthPointe.Serialization.MessagePack;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace SiphomeNet.Network.Models
{
    [Serializable]
    public struct NetworkRoom : INetworkSerializable, IEquatable<NetworkRoom>
    {
        public Guid Guid;
        public ulong ClientId;
        public bool IsHidden;
        public FixedString32Bytes Name;

        public FixedString512Bytes SerializedData;

        private static readonly MessagePackFormatter _formatter = new();
        public bool IsEmpty => Name.IsEmpty || Guid == Guid.Empty;

        public NetworkRoom(ulong clientId, FixedString32Bytes name)
        {
            Guid = Guid.NewGuid();
            ClientId = clientId;
            Name = name;
            SerializedData = default;
            IsHidden = false;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            if (serializer.IsReader)
            {
                byte[] bytes = default;
                serializer.SerializeValue(ref bytes);

                if (bytes != null && bytes.Length > 0)
                {
                    var deserialized = _formatter.Deserialize<NetworkRoom>(bytes);
                    this = deserialized;
                }
            }
            else
            {
                byte[] bytes = _formatter.Serialize(this);
                serializer.SerializeValue(ref bytes);
            }
        }

        public bool Equals(NetworkRoom other)
        {
            return base.Equals(other);
        }

        public override bool Equals(object obj)
        {
            return obj is NetworkRoom other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Guid.GetHashCode();
        }

        private Dictionary<FixedString32Bytes, FixedString512Bytes> ReadDataDict()
        {
            var dict = new Dictionary<FixedString32Bytes, FixedString512Bytes>();
            if (SerializedData.IsEmpty) return dict;

            string text = SerializedData.ToString();
            if (string.IsNullOrEmpty(text)) return dict;

            foreach (var line in text.Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split(' ', 2);
                if (parts.Length == 2)
                {
                    dict[(FixedString32Bytes)parts[0]] = (FixedString512Bytes)parts[1];
                }
            }

            return dict;
        }

        private void WriteDataDict(Dictionary<FixedString32Bytes, FixedString512Bytes> dict)
        {
            if (dict == null || dict.Count == 0)
            {
                SerializedData = default;
                return;
            }

            string text = string.Join("\n", dict.Select(kv => $"{kv.Key} {kv.Value}"));
            if (text.Length > 510) text = text.Substring(0, 510);

            SerializedData = (FixedString512Bytes)text;
        }

        // === Add / Remove / Update Methods ===

        public void AddData(string key, object value)
        {
            AddData((FixedString32Bytes)key, value);
        }

        public void AddData<T>(string key, T value)
        {
            AddData((FixedString32Bytes)key, value);
        }

        public void AddData(FixedString32Bytes key, object value)
        {
            var dict = ReadDataDict();

            if (value == null)
            {
                dict[key] = (FixedString512Bytes)"null";
            }
            else
            {
                byte[] bytes = _formatter.Serialize(value);
                string base64 = Convert.ToBase64String(bytes);

                if (base64.Length > 510)
                    throw new InvalidOperationException($"Serialized data too large for FixedString512Bytes. Length: {base64.Length}");

                dict[key] = (FixedString512Bytes)base64;
            }

            WriteDataDict(dict);
            Debug.Log($"[NetworkRoom] Added key '{key}'. SerializedData size: {SerializedData.Length} / 512 bytes");
        }

        public void AddData<T>(FixedString32Bytes key, T value)
        {
            AddData((FixedString32Bytes)key, (object)value);
        }

        public bool TryGetData<T>(string key, out T value)
        {
            return TryGetData((FixedString32Bytes)key, out value);
        }

        public bool TryGetData<T>(FixedString32Bytes key, out T value)
        {
            value = default;
            var dict = ReadDataDict();
            if (!dict.TryGetValue(key, out var fixedVal)) return false;

            string stringValue = fixedVal.ToString();
            if (stringValue == "null") return false;

            try
            {
                byte[] bytes = Convert.FromBase64String(stringValue);
                value = _formatter.Deserialize<T>(bytes);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public T GetData<T>(string key, T defaultValue = default)
        {
            return GetData((FixedString32Bytes)key, defaultValue);
        }

        public T GetData<T>(FixedString32Bytes key, T defaultValue = default)
        {
            var dict = ReadDataDict();
            if (!dict.TryGetValue(key, out var fixedVal)) return defaultValue;

            string value = fixedVal.ToString();
            if (value == "null") return defaultValue;

            try
            {
                byte[] bytes = Convert.FromBase64String(value);
                return _formatter.Deserialize<T>(bytes);
            }
            catch
            {
                return defaultValue;
            }
        }

        public void RemoveData(string key)
        {
            RemoveData((FixedString32Bytes)key);
        }

        public void RemoveData(FixedString32Bytes key)
        {
            var dict = ReadDataDict();
            if (dict.Remove(key))
            {
                WriteDataDict(dict);
                Debug.Log($"[NetworkRoom] Removed key '{key}'. SerializedData size: {SerializedData.Length} / 512 bytes");
            }
        }

        public bool ContainsData(string key) => ContainsData((FixedString32Bytes)key);

        public bool ContainsData(FixedString32Bytes key)
        {
            var dict = ReadDataDict();
            return dict.ContainsKey(key);
        }

        public void ClearData()
        {
            SerializedData = default;
            Debug.Log($"[NetworkRoom] Cleared all data. SerializedData size: {SerializedData.Length} / 512 bytes");
        }

        // === String / Int / Bool Helpers ===

        public void AddStringData(string key, string value) => AddStringData((FixedString32Bytes)key, value);

        public void AddStringData(FixedString32Bytes key, string value)
        {
            var dict = ReadDataDict();
            if (value == null)
                dict[key] = (FixedString512Bytes)"null";
            else
            {
                if (value.Length > 510) value = value.Substring(0, 510);
                dict[key] = (FixedString512Bytes)value;
            }

            WriteDataDict(dict);
            Debug.Log($"[NetworkRoom] Added string key '{key}'. SerializedData size: {SerializedData.Length} / 512 bytes");
        }

        public string GetStringData(string key, string defaultValue = "") => GetStringData((FixedString32Bytes)key, defaultValue);

        public string GetStringData(FixedString32Bytes key, string defaultValue = "")
        {
            var dict = ReadDataDict();
            if (!dict.TryGetValue(key, out var fixedVal)) return defaultValue;

            string val = fixedVal.ToString();
            return val == "null" ? defaultValue : val;
        }

        public void AddIntData(string key, int value) => AddStringData(key, value.ToString());

        public int GetIntData(string key, int defaultValue = 0)
        {
            string s = GetStringData(key, null);
            return int.TryParse(s, out var r) ? r : defaultValue;
        }

        public void AddBoolData(string key, bool value) => AddStringData(key, value.ToString());

        public bool GetBoolData(string key, bool defaultValue = false)
        {
            string s = GetStringData(key, null);
            return bool.TryParse(s, out var r) ? r : defaultValue;
        }

    }
}
