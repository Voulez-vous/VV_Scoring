using System;
using System.Collections.Generic;
using AYellowpaper.SerializedCollections;
using Unity.Properties;
using UnityEngine;

namespace VV.Scoring.Data
{
    /// <summary>
    /// Meta data stored into ScoreData. Minimal allocations and explicit deep-copy.
    /// </summary>
    [Serializable]
    public class ScoreMetaData
    {
        // Primary runtime store
        private Dictionary<string, object> data;

        public ScoreMetaData(int capacity = 0)
        {
            data = capacity > 0 ? new Dictionary<string, object>(capacity) : new Dictionary<string, object>();
        }

        public IReadOnlyDictionary<string, object> Data => data;

        public void Add(string key, object value)
        {
            data[key] = value;
#if UNITY_EDITOR
            SyncAddOrUpdate(key, value);
#endif
        }
        
        public void Set(string key, object value)
        {
            if (data.TryGetValue(key, out var existingValue))
            {
                // Update
                data[key] = value;
            }
            else
            {
                // Add
                data.Add(key, value);
            }
#if UNITY_EDITOR
            SyncAddOrUpdate(key, value);
#endif
        }

        public void AddRange(IDictionary<string, object> other)
        {
            if (other == null) return;
            foreach (var kv in other)
            {
                data[kv.Key] = kv.Value;
#if UNITY_EDITOR
                SyncAddOrUpdate(kv.Key, kv.Value);
#endif
            }
        }

        public bool TryGet<T>(string key, out T value)
        {
            if (data.TryGetValue(key, out var obj) && obj is T t)
            {
                value = t;
                return true;
            }

            value = default;
            return false;
        }

        public bool ContainsKey(string key) => data.ContainsKey(key);

        public bool Update(string key, object newValue)
        {
            if (!data.ContainsKey(key)) return false;
            data[key] = newValue;
#if UNITY_EDITOR
            SyncAddOrUpdate(key, newValue);
#endif
            return true;
        }

        public void Remove(string key)
        {
            data.Remove(key);
#if UNITY_EDITOR
            SyncRemove(key);
#endif
        }

        /// <summary>
        /// Create deep copy of metadata (shallow-copy of values; values are expected to be primitives/strings or immutable).
        /// Avoids MemberwiseClone and reduces transient allocations.
        /// </summary>
        public ScoreMetaData Clone()
        {
            var copy = new ScoreMetaData(data.Count);
            foreach (var kv in data)
                copy.data[kv.Key] = kv.Value;
            return copy;
        }

        /// <summary>
        /// Convert to a string dictionary suitable for JSON serialization.
        /// Avoids allocating intermediate LINQ results.
        /// </summary>
        public Dictionary<string, string> ToSerializableStringDict()
        {
            var result = new Dictionary<string, string>(data.Count);
            foreach (var kv in data)
                result[kv.Key] = kv.Value?.ToString() ?? string.Empty;
            return result;
        }
        
#if UNITY_EDITOR
        [SerializeField]
        private SerializedDictionary<string, SerializedValueWrapper> serializedData = new();
        
        [CreateProperty]
        public SerializedDictionary<string, SerializedValueWrapper> SerializedData => serializedData;
        
        private void SyncAddOrUpdate(string key, object value)
        {
            ISerializedValue serializedValue = value switch
            {
                int v => new IntValue { value = v },
                float v => new FloatValue { value = v },
                bool v => new BoolValue { value = v },
                string v => new StringValue { value = v },
                UnityEngine.Object obj => new ObjectValue { value = obj },
                Vector3 v => new Vector3Value { value = v },
                _ => null
            };
            
            SerializedValueWrapper serializedValueWrapper = new SerializedValueWrapper { Value = serializedValue };

            if (serializedValue == null)
            {
                Debug.LogWarning($"Unsupported value type: {value?.GetType()}");
                return;
            }

            serializedData[key] = serializedValueWrapper;
            Debug.Log($"Adding {value} to data[{key}] => {serializedData[key]}");
        }

        private void SyncRemove(string key)
        {
            serializedData.Remove(key);
        }
#endif
    }
}