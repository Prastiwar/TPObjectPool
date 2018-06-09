/**
*   Authored by Tomasz Piowczyk
*   MIT LICENSE (https://github.com/Prastiwar/TPObjectPool/blob/master/LICENSE)
*/
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;

namespace TP
{
    /// <summary> This class allows you to manage Objects in Pools. </summary>  
    public static class TPObjectPool
    {
        private static Dictionary<string, List<GameObject>> pool = new Dictionary<string, List<GameObject>>();

        public delegate void ActivationEventHandler(GameObject gameObject, bool value);

        /// <summary> This ActivationEventHandler delegate is called just before object from pool is set active/deactive. </summary>  
        public static ActivationEventHandler OnBeforeActivation { get; set; }

        /// <summary> This ActivationEventHandler delegate is called just after object from pool is set active/deactive. </summary>  
        public static ActivationEventHandler OnAfterActivation { get; set; }

        /// <summary> Set activation of GameObject. </summary>  
        /// <param name="gameObject">GameObject to active.</param>
        public static void SetActive(GameObject gameObject, bool value, Vector3? position = default(Vector3?), Quaternion? rotation = default(Quaternion?))
        {
            if (gameObject == null)
            {
                Debug.LogError("You're trying to set activation of Null object");
                return;
            }

            if (position.HasValue)
                gameObject.transform.position = position.Value;
            if (rotation.HasValue)
                gameObject.transform.rotation = rotation.Value;

            SafeInvoke(OnBeforeActivation, gameObject, value);
            gameObject.SetActive(value);
            SafeInvoke(OnAfterActivation, gameObject, value);
        }

        /// <summary> Set activation of All GameObjects in pool. </summary>  
        /// <param name="poolName">Unique Key of pool.</param>
        public static void SetActive(string poolName, bool value, Vector3? position = default(Vector3?), Quaternion? rotation = default(Quaternion?))
        {
            if (SafeCheck(poolName))
                pool[poolName].ForEach(obj => SetActive(obj, value, position, rotation));
        }

#if NET_2_0 || NET_2_0_SUBSET
        /// <summary> API to easier call StartCoroutine(SetActive(parameters)). </summary> 
        /// <param name="mono">To call coroutine we need monobehaviour.</param>
        /// <param name="gameObject">GameObject to active.</param>
        public static void SetActive(MonoBehaviour mono, GameObject gameObject, float delay, bool value, Vector3? position = default(Vector3?), Quaternion? rotation = default(Quaternion?))
        {
            mono.StartCoroutine(SetActive(gameObject, delay, value, position, rotation));
        }

        /// <summary> API to easier call StartCoroutine(SetActive(parameters)). </summary> 
        /// <param name="mono">To call coroutine we need monobehaviour.</param>
        /// <param name="gameObject">GameObject to active.</param>
        public static void SetActive(MonoBehaviour mono, string poolName, float delay, bool value, Vector3? position = default(Vector3?), Quaternion? rotation = default(Quaternion?))
        {
            mono.StartCoroutine(SetActive(poolName, delay, value, position, rotation));
        }

        /// <summary> Coroutine that sets activation of GameObject after delay. REMEMBER TO CALL IT BY StartCoroutine() </summary> 
        /// <param name="gameObject">GameObject to active.</param>
        public static IEnumerator SetActive(GameObject gameObject, float delay, bool value, Vector3? position = default(Vector3?), Quaternion? rotation = default(Quaternion?))
        {
            yield return new WaitForSeconds(delay);
            SetActive(gameObject, value, position, rotation);
        }

        /// <summary> Coroutine that sets activation of All GameObjects in pool after delay. REMEMBER TO CALL IT BY StartCoroutine() </summary> 
        /// <param name="gameObject">GameObject to active.</param>
        public static IEnumerator SetActive(string poolName, float delay, bool value, Vector3? position = default(Vector3?), Quaternion? rotation = default(Quaternion?))
        {
            yield return new WaitForSeconds(delay);
            if (SafeCheck(poolName))
                pool[poolName].ForEach(obj => SetActive(obj, value, position, rotation));
        }
#else
        /// <summary> Sets activation of GameObject after delay. </summary> 
        /// <param name="gameObject">GameObject to active.</param>
        public static async void SetActive(GameObject gameObject, float delay, bool value, Vector3? position = default(Vector3?), Quaternion? rotation = default(Quaternion?))
        {
            await System.Threading.Tasks.Task.Delay(System.TimeSpan.FromSeconds(delay));
            SetActive(gameObject, value, position, rotation);
        }

        /// <summary> Sets activation of All GameObjects in pool after delay. </summary> 
        /// <param name="gameObject">GameObject to active.</param>
        public static async void SetActive(string poolName, float delay, bool value, Vector3? position = default(Vector3?), Quaternion? rotation = default(Quaternion?))
        {
            await System.Threading.Tasks.Task.Delay(System.TimeSpan.FromSeconds(delay));
            if (SafeCheck(poolName))
                pool[poolName].ForEach(obj => SetActive(obj, value, position, rotation));
        }
#endif

        /// <summary> Creates or adds to existing pool with its unique key with GameObjects. </summary> 
        /// <param name="poolName">Unique Key of pool.</param>
        /// <param name="gameObjects">All GameObjects which should be in one pool.</param>
        public static void AddToPool(string poolName, params GameObject[] gameObjects)
        {
            int length = gameObjects.Length;
            if (HasKey(poolName))
            {
                for (int i = 0; i < length; i++)
                {
                    gameObjects[i].SetActive(false);
                    pool[poolName].Add(UnityEngine.Object.Instantiate(gameObjects[i]));
                }
            }
            else
            {
                List<GameObject> newList = new List<GameObject>();
                for (int i = 0; i < length; i++)
                {
                    gameObjects[i].SetActive(false);
                    newList.Add(UnityEngine.Object.Instantiate(gameObjects[i]));
                }
                pool[poolName] = newList;
            }
        }

        /// <summary> Creates pool of unique Key of length with GameObject. </summary> 
        /// <param name="poolName">Unique Key of pool.</param>
        /// <param name="gameObject">GameObject which should multiplied in pool.</param>
        /// <param name="length">Length of pool - how many copies of GameObject should be in pool.</param>
        public static void AddToPool(string poolName, GameObject gameObject, int length)
        {
            GameObject[] copies = new GameObject[length];
            for (int i = 0; i < length; i++)
                copies[i] = gameObject;
            AddToPool(poolName, copies);
        }

        /// <summary> Removes GameObjects from pool and destroys them. </summary> 
        /// <param name="poolName">Unique Key of pool.</param>
        /// <param name="gameObjects">All GameObjects which should be removed from pool.</param>
        public static void RemoveFromPool(string poolName, params GameObject[] gameObjects)
        {
            if (SafeCheck(poolName))
            {
                foreach (var obj in gameObjects)
                {
                    int index = pool[poolName].IndexOf(obj);
                    UnityEngine.Object.Destroy(pool[poolName][index]);
                    pool[poolName].Remove(obj);
                }
            }
        }

        /// <summary> Returns deactivated object from pool or creates new object(from first object in pool) if there is no free objects. </summary> 
        /// <param name="poolName">Unique Key of pool given on adding.</param>
        /// <param name="createNew">if true, it'll create new object when there is no free objects.</param>
        public static GameObject GetFreeObject(string poolName, bool createNew)
        {
            if (SafeCheck(poolName))
            {
                if (pool[poolName].Any(FreeObject))
                    return pool[poolName].First(FreeObject);

                if (createNew && pool[poolName].Count > 0)
                {
                    GameObject newObj = UnityEngine.Object.Instantiate(pool[poolName][0]);
                    pool[poolName].Add(newObj);
                    return newObj;
                }
            }
            return null;
        }

        /// <summary> Returns first activated object from pool. </summary> 
        /// <param name="poolName">Unique Key of pool given on adding.</param>
        public static GameObject GetBusyObject(string poolName)
        {
            return HasAnyBusyObject(poolName) ? pool[poolName].First(BusyObject) : null;
        }

        /// <summary> Returns List of all objects from pool. </summary> 
        /// <param name="poolName">Unique Key of pool given on adding.</param>
        public static List<GameObject> GetObjects(string poolName)
        {
            return SafeCheck(poolName) ? pool[poolName] : null;
        }

        /// <summary> Returns List of deactivated object from pool. </summary> 
        /// <param name="poolName">Unique Key of pool given on adding.</param>
        public static List<GameObject> GetFreeObjects(string poolName)
        {
            return HasAnyFreeObject(poolName) ? pool[poolName].Where(FreeObject).ToList() : null;
        }

        /// <summary> Returns List of activated object from pool.  </summary> 
        /// <param name="poolName">Unique Key of pool given on adding.</param>
        public static List<GameObject> GetBusyObjects(string poolName)
        {
            return HasAnyBusyObject(poolName) ? pool[poolName].Where(BusyObject).ToList() : null;
        }

        /// <summary> Returns length of deactivated objects from its pool. </summary> 
        /// <param name="poolName">Unique Key of pool given on adding.</param>
        public static int FreeObjectsLength(string poolName)
        {
            return HasAnyFreeObject(poolName) ? pool[poolName].Where(FreeObject).Count() : 0;
        }

        /// <summary> Returns length of activated objects from its pool. </summary> 
        /// <param name="poolName">Unique Key of pool given on adding.</param>
        public static int BusyObjectsLength(string poolName)
        {
            return HasAnyBusyObject(poolName) ? pool[poolName].Where(BusyObject).Count() : 0;
        }

        /// <summary> Returns length of all objects from its pool. </summary> 
        /// <param name="poolName">Unique Key of pool given on adding.</param>
        public static int ObjectsLength(string poolName)
        {
            return SafeCheck(poolName) ? pool[poolName].Count : 0;
        }

        /// <summary> Checks if given key(pool) exists. </summary> 
        /// <param name="poolName">Unique Key of pool.</param>
        public static bool HasKey(string poolName)
        {
            return pool.ContainsKey(poolName);
        }

        /// <summary> Checks if there is any free object in pool. </summary> 
        /// <param name="poolName">Unique Key of pool.</param>
        public static bool HasAnyObject(string poolName)
        {
            return SafeCheck(poolName) ? pool[poolName].Count > 0 : false;
        }

        /// <summary> Checks if there is any free object in pool. </summary> 
        /// <param name="poolName">Unique Key of pool.</param>
        public static bool HasAnyFreeObject(string poolName)
        {
            return SafeCheck(poolName) ? pool[poolName].Any(FreeObject) : false;
        }

        /// <summary> Checks if there is any activated object in pool. </summary> 
        /// <param name="poolName">Unique Key of pool.</param>
        public static bool HasAnyBusyObject(string poolName)
        {
            return SafeCheck(poolName) ? pool[poolName].Any(BusyObject) : false;
        }

        /// <summary> Clears whole pool. </summary> 
        /// <param name="poolName">Unique Key of pool.</param>
        public static void ClearPool(string poolName)
        {
            if (SafeCheck(poolName))
                RemoveFromPool(poolName, GetObjects(poolName).ToArray());
        }

        /// <summary> Clears all pools. </summary>  
        public static void Dispose()
        {
            foreach (var pair in pool)
                ClearPool(pair.Key);
        }

        /// <summary> This checks for null before ActivationEventHandler is called. </summary>  
        private static void SafeInvoke(ActivationEventHandler onActivation, GameObject gameObject, bool value)
        {
            if (onActivation != null)
                onActivation(gameObject, value);
        }

        /// <summary> This checks for existing key and can throw log error. </summary>  
        private static bool SafeCheck(string poolName)
        {
            if (HasKey(poolName))
                return true;
            Debug.LogError("Given key doesn't exist");
            return false;
        }

        /// <summary> Returns func, where GameObject is activated </summary>
        private static Func<GameObject, bool> BusyObject {
            get { return obj => obj.activeSelf; }
        }

        /// <summary> Returns func, where GameObject is deactivated </summary>
        private static Func<GameObject, bool> FreeObject {
            get { return obj => !obj.activeSelf; }
        }

    }
}