using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MrRobinOfficial.SerializedDictionary
{
    /// <summary>
    /// Contains utility methods for serialized collections
    /// </summary>
    public static class SerializedCollectionsUtility
    {
        /// <summary>
        /// Returns true if the object is not null
        /// </summary>
        /// <param name="obj">The object</param>
        /// <returns>A boolean indicating if the object is not null</returns>
        public static bool IsValidKey(object obj)
        {
            // we catch this error if we are not on the main thread and simply return false as we assume the object is null
            try
            {
                return !(obj is Object unityObject && unityObject == null);
            }
            catch
            {
                return false;
            }
        }
    }
}