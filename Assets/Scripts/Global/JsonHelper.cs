using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cosmicrafts
{
    /// <summary>
    /// Helper class that adds functionality to Unity's JsonUtility to handle features similar to Newtonsoft.Json
    /// </summary>
    public static class JsonHelper
    {
        // Serializes an object to a JSON string
        public static string SerializeObject(object obj)
        {
            // Handle common built-in types
            if (obj == null) return "null";
            if (obj is string str) return "\"" + str.Replace("\"", "\\\"") + "\"";
            if (obj is int || obj is float || obj is double || obj is bool) return obj.ToString().ToLower();
            
            // For Dictionary and non-serializable objects, we need to handle specially
            if (obj is Dictionary<string, object> dict)
            {
                return SerializeDictionary(dict);
            }

            // For arrays and lists
            if (obj is System.Collections.IList list)
            {
                return SerializeList(list);
            }

            // Default to JsonUtility for serializable objects
            try
            {
                return JsonUtility.ToJson(obj);
            }
            catch
            {
                Debug.LogWarning($"Could not serialize object of type {obj.GetType()}");
                return "{}";
            }
        }

        // Helper for serializing a list to JSON
        public static string ToJson<T>(List<T> list)
        {
            Wrapper<T> wrapper = new Wrapper<T>();
            wrapper.Items = list;
            return JsonUtility.ToJson(wrapper);
        }

        // Helper for deserializing a JSON string to a list
        public static List<T> FromJson<T>(string json)
        {
            Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>(json);
            return wrapper.Items;
        }

        // Deserializes a JSON string to the specified type
        public static T DeserializeObject<T>(string json)
        {
            // Special handling for lists since JsonUtility doesn't handle them directly
            if (typeof(T).IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(List<>))
            {
                // We need to wrap the array in an object
                string wrappedJson = "{\"Items\":" + json + "}";
                
                // Get the list element type
                Type elementType = typeof(T).GetGenericArguments()[0];
                
                // Create the generic wrapper type
                Type wrapperType = typeof(Wrapper<>).MakeGenericType(elementType);
                
                // Deserialize to the wrapper
                object wrapper = JsonUtility.FromJson(wrappedJson, wrapperType);
                
                // Get the Items field value
                var itemsField = wrapperType.GetField("Items");
                return (T)itemsField.GetValue(wrapper);
            }
            
            // For normal objects, use JsonUtility directly
            try
            {
                return JsonUtility.FromJson<T>(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error deserializing JSON to {typeof(T)}: {ex.Message}");
                return default(T);
            }
        }

        // Helper to serialize dictionaries
        private static string SerializeDictionary(Dictionary<string, object> dict)
        {
            List<string> entries = new List<string>();
            foreach (var kvp in dict)
            {
                entries.Add($"\"{kvp.Key}\":{SerializeObject(kvp.Value)}");
            }
            return "{" + string.Join(",", entries) + "}";
        }

        // Helper to serialize lists
        private static string SerializeList(System.Collections.IList list)
        {
            List<string> items = new List<string>();
            foreach (var item in list)
            {
                items.Add(SerializeObject(item));
            }
            return "[" + string.Join(",", items) + "]";
        }

        // Wrapper class for serializing/deserializing lists
        [Serializable]
        private class Wrapper<T>
        {
            public List<T> Items;
        }
    }
} 