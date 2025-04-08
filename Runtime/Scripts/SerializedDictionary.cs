using System.Collections.Generic;
using UnityEngine;

namespace MrRobinOfficial.SerializedDictionary
{
    /// <summary>
    /// A serializable dictionary that can be used in the inspector
    /// </summary>
    /// <typeparam name="TKey">The type of the key</typeparam>
    /// <typeparam name="TValue">The type of the value</typeparam>
    [System.Serializable]
    public partial class SerializedDictionary<TKey, TValue> : Dictionary<TKey, TValue>, ISerializationCallbackReceiver
    {
        public SerializedDictionary() : base()
        {

        }

        public SerializedDictionary(IDictionary<TKey, TValue> dictionary) : base(dictionary)
        {

        }

        public SerializedDictionary(IEnumerable<KeyValuePair<TKey, TValue>> collection)
            : base(collection)
        {

        }

        public SerializedDictionary(IEqualityComparer<TKey> comparer) : base(comparer)
        {

        }

        public SerializedDictionary(int capacity) : base(capacity)
        {

        }

        public SerializedDictionary(IDictionary<TKey, TValue> dictionary, IEqualityComparer<TKey> comparer)
            : base(dictionary, comparer)
        {

        }

        public SerializedDictionary(IEnumerable<KeyValuePair<TKey, TValue>> collection, IEqualityComparer<TKey> comparer)
            : base(collection, comparer)
        {

        }

        public SerializedDictionary(int capacity, IEqualityComparer<TKey> comparer)
            : base(capacity, comparer)
        {

        }

        /// <summary>
        /// The list of serialized key value pairs
        /// </summary>
        [SerializeField]
        internal List<SerializedKeyValuePair<TKey, TValue>> _serializedList = new List<SerializedKeyValuePair<TKey, TValue>>();

#if UNITY_EDITOR
        internal IKeyable LookupTable
        {
            get
            {
                if (_lookupTable == null)
                    _lookupTable = new DictionaryLookupTable<TKey, TValue>(this);

                return _lookupTable;
            }
        }

        /// <summary>
        /// The lookup table for the dictionary
        /// </summary>
        private DictionaryLookupTable<TKey, TValue> _lookupTable;
#endif

        public void OnAfterDeserialize()
        {
            Clear();

            foreach (var kvp in _serializedList)
            {
#if UNITY_EDITOR
                if (!ContainsKey(kvp.Key))
                    Add(kvp.Key, kvp.Value);
#else
                Add(kvp.Key, kvp.Value);
#endif
            }

#if UNITY_EDITOR
            LookupTable.RecalculateOccurences();
#else
            _serializedList.Clear();
#endif
        }

        public void OnBeforeSerialize()
        {
#if UNITY_EDITOR
            if (UnityEditor.BuildPipeline.isBuildingPlayer)
                LookupTable.RemoveDuplicates();
#endif
        }
    }
}
