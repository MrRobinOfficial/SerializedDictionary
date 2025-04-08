using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MrRobinOfficial.SerializedDictionary.KeysGenerators
{
    public abstract class KeyListGenerator : ScriptableObject
    {
        public abstract IEnumerable GetKeys(System.Type type);
    }
}