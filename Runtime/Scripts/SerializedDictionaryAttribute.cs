using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace MrRobinOfficial.SerializedDictionary
{
    /// <summary>
    /// Attribute to customize key and value names in the inspector for a serialized dictionary
    /// </summary>
    [Conditional("UNITY_EDITOR")]
    public class SerializedDictionaryAttribute : Attribute
    {
        /// <summary>
        /// The name of the key
        /// </summary>
        public readonly string KeyName;

        /// <summary>
        /// The name of the value
        /// </summary>
        public readonly string ValueName;

        public SerializedDictionaryAttribute(string keyName = null, string valueName = null)
        {
            KeyName = keyName;
            ValueName = valueName;
        }
    }
}