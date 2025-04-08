using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MrRobinOfficial.SerializedDictionary
{
    /// <summary>
    /// A serializable key value pair
    /// </summary>
    /// <typeparam name="TKey">The type of the key</typeparam>
    /// <typeparam name="TValue">The type of the value</typeparam>
    [System.Serializable]
    public struct SerializedKeyValuePair<TKey, TValue>
    {
        /// <summary>
        /// The key
        /// </summary>
        public TKey Key;

        /// <summary>
        /// The value
        /// </summary>
        public TValue Value;

        public SerializedKeyValuePair(TKey key, TValue value)
        {
            Key = key;
            Value = value;
        }
    }
}
