namespace CosmicraftsSP
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    
    /// <summary>
    /// Helper class to replace Newtonsoft.Json functionality with Unity's built-in JSON utilities
    /// </summary>
    public static class JsonHelper
    {
        /// <summary>
        /// Serialize an object to JSON string
        /// </summary>
        public static string SerializeObject(object obj)
        {
            if (obj == null)
                return "null";
                
            // Handle simple types
            if (obj is string str)
                return $"\"{str}\"";
            if (obj is int || obj is float || obj is double || obj is bool)
                return obj.ToString().ToLower();
            
            // Handle DateTime specifically
            if (obj is DateTime dateTime)
                return $"\"{dateTime:yyyy-MM-ddTHH:mm:ss}\"";
                
            // Handle List<T>
            if (obj is System.Collections.IList list)
            {
                var items = new List<string>();
                foreach (var item in list)
                {
                    items.Add(SerializeObject(item));
                }
                return $"[{string.Join(",", items)}]";
            }
            
            // Handle Dictionary<TKey, TValue>
            if (obj is System.Collections.IDictionary dict)
            {
                var items = new List<string>();
                foreach (System.Collections.DictionaryEntry entry in dict)
                {
                    string key = entry.Key.ToString();
                    items.Add($"\"{key}\":{SerializeObject(entry.Value)}");
                }
                return $"{{{string.Join(",", items)}}}";
            }
            
            // For other objects, use Unity's JsonUtility
            try
            {
                return JsonUtility.ToJson(obj);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error serializing object of type {obj.GetType().Name}: {e.Message}");
                return "{}";
            }
        }
        
        /// <summary>
        /// Deserialize a JSON string to an object of type T
        /// </summary>
        public static T DeserializeObject<T>(string json)
        {
            if (string.IsNullOrEmpty(json) || json == "null")
                return default;
                
            try
            {
                // Use Unity's JsonUtility
                return JsonUtility.FromJson<T>(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error deserializing JSON to type {typeof(T).Name}: {e.Message}");
                return default;
            }
        }
        
        /// <summary>
        /// Wrapper for JsonUtility.FromJsonOverwrite
        /// </summary>
        public static void PopulateObject(string json, object obj)
        {
            if (string.IsNullOrEmpty(json) || json == "null" || obj == null)
                return;
                
            try
            {
                JsonUtility.FromJsonOverwrite(json, obj);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error populating object of type {obj.GetType().Name}: {e.Message}");
            }
        }
    }
    
    // Helper class to wrap lists for JSON serialization
    [Serializable]
    public class JsonListWrapper<T>
    {
        public List<T> Items;
        
        public JsonListWrapper(List<T> items)
        {
            Items = items;
        }
    }
    
    // Helper class to serialize/deserialize a simple dictionary
    [Serializable]
    public class JsonDictWrapper
    {
        [Serializable]
        public class KeyValuePair
        {
            public string Key;
            public string Value;
        }
        
        public List<KeyValuePair> Entries = new List<KeyValuePair>();
        
        public Dictionary<string, string> ToDictionary()
        {
            var dict = new Dictionary<string, string>();
            foreach (var entry in Entries)
            {
                dict[entry.Key] = entry.Value;
            }
            return dict;
        }
        
        public static JsonDictWrapper FromDictionary(Dictionary<string, string> dict)
        {
            var wrapper = new JsonDictWrapper();
            foreach (var kvp in dict)
            {
                wrapper.Entries.Add(new KeyValuePair { Key = kvp.Key, Value = kvp.Value });
            }
            return wrapper;
        }
    }
} 