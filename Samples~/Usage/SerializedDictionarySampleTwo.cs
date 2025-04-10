using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MrRobinOfficial.SerializedDictionary
{
    public class SerializedDictionarySampleTwo : MonoBehaviour
    {
        [SerializedDictionary("ID", "Person")]
        public SerializedDictionary<int, Person> People;

        [System.Serializable]
        public class Person
        {
            public string FirstName;
            public string LastName;
        }
    }
}