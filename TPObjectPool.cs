/**
*   Authored by Tomasz Piowczyk
*   MIT LICENSE
*   Copyright 2018 You're allowed to make changes in functionality and use for commercial or personal.
*   You're not allowed to claim ownership of this script.
*   https://github.com/Prastiwar/TPObjectPool/
*/
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TP.Utilities
{
    /// <summary>  
    ///  This class allows you to manage Objects in Pools.
    /// </summary>  
    public class TPObjectPool
    {
        public delegate void ActivationEventHandler(GameObject _gameObject, bool _value);
        static ActivationEventHandler _onBeforeActivation;
        static ActivationEventHandler _onAfterActivation;

        static Dictionary<string, List<GameObject>> Pool = new Dictionary<string, List<GameObject>>();

        /// <summary>  
        ///  This ActivationEventHandler delegate is called just before object from pool is active/deactive.
        /// </summary>  
        public static ActivationEventHandler OnBeforeActivation
        {
            get
            {
                if (_onBeforeActivation == null)
                    _onBeforeActivation = OnNull;
                return _onBeforeActivation;
            }

            set { _onBeforeActivation = value; }
        }

        /// <summary>  
        ///  This ActivationEventHandler delegate is called just after object from pool is active/deactive.
        /// </summary>  
        public static ActivationEventHandler OnAfterActivation
        {
            get
            {
                if (_onAfterActivation == null)
                    _onAfterActivation = OnNull;
                return _onAfterActivation;
            }

            set { _onAfterActivation = value; }
        }

        static void OnNull(GameObject _gameObject, bool _value) { }

        /// <summary>  
        ///  Activates GameObject.
        /// </summary>  
        /// <param name="_gameObject">GameObject to active.</param>
        public static void ActiveObj(GameObject _gameObject)
        {
            if (_gameObject == null)
            {
                return;
            }

            OnBeforeActivation(_gameObject, true);

            if (!_gameObject.activeSelf)
                _gameObject.SetActive(true);

            OnAfterActivation(_gameObject, true);
        }

        /// <summary>  
        ///  Activates gameobject with given position.
        /// </summary> 
        /// <param name="_gameObject">GameObject to active.</param>
        public static void ActiveObj(GameObject _gameObject, Vector3 _position)
        {
            _gameObject.transform.position = _position;
            ActiveObj(_gameObject);
        }

        /// <summary>  
        ///  Activates gameobject with given position and rotation.
        /// </summary> 
        /// <param name="_gameObject">GameObject to active.</param>
        public static void ActiveObj(GameObject _gameObject, Vector3 _position, Quaternion _rotation)
        {
            Transform trans = _gameObject.transform;
            trans.position = _position;
            trans.rotation = _rotation;
            ActiveObj(_gameObject);
        }

#if NET_2_0 || NET_2_0_SUBSET
        /// <summary>  
        ///  Coroutine that activates gameobject after delay.
        /// </summary> 
        /// <param name="_gameObject">GameObject to active.</param>
        public static IEnumerator ActiveObj(GameObject _gameObject, float _delay)
        {
            yield return new WaitForSeconds(_delay);
            ActiveObj(_gameObject);
        }
#else

        /// <summary>  
        ///  Activates gameobject after delay.
        /// </summary> 
        /// <param name="_gameObject">GameObject to active.</param>
        public static async void ActiveObj(GameObject _gameObject, float _delay)
        {
            await System.Threading.Tasks.Task.Delay(System.TimeSpan.FromSeconds(_delay));
            ActiveObj(_gameObject);
        }
#endif

        /// <summary>  
        ///  Deactivates gameobject.
        /// </summary> 
        /// <param name="_gameObject">GameObject to deactive.</param>
        public static void DeactiveObj(GameObject _gameObject)
        {
            if (_gameObject == null)
                return;

            OnBeforeActivation(_gameObject, false);

            if (_gameObject.activeSelf)
                _gameObject.SetActive(false);

            OnAfterActivation(_gameObject, false);
        }

#if NET_2_0 || NET_2_0_SUBSET
        /// <summary>  
        ///  Coroutine that deactivates gameobject after delay.
        /// </summary> 
        /// <param name="_gameObject">GameObject to deactive.</param>
        public static IEnumerator DeactiveObj(GameObject _gameObject, float _delay)
        {
            yield return new WaitForSeconds(_delay);
            DeactiveObj(_gameObject);
        }
#else

        /// <summary>  
        ///  Deactivates gameobject after delay.
        /// </summary> 
        /// <param name="_gameObject">GameObject to deactive.</param>
        public static async void DeactiveObj(GameObject _gameObject, float _delay)
        {
            await System.Threading.Tasks.Task.Delay(System.TimeSpan.FromSeconds(_delay));
            DeactiveObj(_gameObject);
        }
#endif

        /// <summary>  
        ///  Clears whole pool.
        /// </summary> 
        /// <param name="_poolName">Unique Key of pool.</param>
        public static void ClearPool(string _poolName)
        {
            if (Pool.ContainsKey(_poolName))
            {
                Pool[_poolName].Clear();
            }
        }

        /// <summary>  
        ///  Removes GameObjects from pool and Destroys them.
        /// </summary> 
        /// <param name="_poolName">Unique Key of pool.</param>
        /// <param name="_gameObjects">All GameObjects which should be removed from pool.</param>
        public static void RemoveFromPool(string _poolName, params GameObject[] _gameObjects)
        {
            if (Pool.ContainsKey(_poolName))
            {
                foreach (var obj in _gameObjects)
                {
                    int index = Pool[_poolName].IndexOf(obj);
                    GameObject.Destroy(Pool[_poolName][index]);
                    Pool[_poolName].Remove(obj);
                }
            }
        }

        /// <summary>  
        ///  Removes GameObjects from their pool and Destroys them.
        /// </summary> 
        /// <param name="_gameObjects">All GameObjects which should be removed from pool.</param>
        public static void RemoveFromPool(params GameObject[] _gameObjects)
        {
            int length = _gameObjects.Length;
            foreach (var item in Pool)
            {
                for (int i = 0; i < length; i++)
                {
                    if (item.Value.Contains(_gameObjects[i]))
                    {
                        RemoveFromPool(item.Key, _gameObjects);
                        return;
                    }
                }
            }
        }

        /// <summary>  
        ///  Creates pool with its unique key with GameObjects.
        /// </summary> 
        /// <param name="_poolName">Unique Key of pool.</param>
        /// <param name="_gameObjects">All GameObjects which should be in one pool.</param>
        public static void AddToPool(string _poolName, params GameObject[] _gameObjects)
        {
            int length = _gameObjects.Length;

            if (Pool.ContainsKey(_poolName))
            {
                for (int i = 0; i < length; i++)
                {
                    GameObject obj = GameObject.Instantiate(_gameObjects[i]);
                    obj.SetActive(false);
                    Pool[_poolName].Add(obj);
                }
            }
            else
            {
                List<GameObject> list = new List<GameObject>();

                for (int i = 0; i < length; i++)
                {
                    GameObject obj = GameObject.Instantiate(_gameObjects[i]);
                    obj.SetActive(false);
                    list.Add(obj);
                }
                Pool.Add(_poolName, list);
            }
        }

        /// <summary>  
        ///  Creates pool of length with GameObject.
        /// </summary> 
        /// <param name="_gameObject">GameObject which should multiplied in pool.</param>
        /// <param name="_length">Length of pool - how many copies of GameObject should be in pool.</param>
        public static void AddToPool(GameObject _gameObject, int _length)
        {
            string TKey = _gameObject.GetInstanceID().ToString();
            for (int i = 0; i < _length; i++)
            {
                AddToPool(TKey, _gameObject);
            }
        }

        /// <summary>  
        ///  Creates pool of unique Key of length with GameObject.
        /// </summary> 
        /// <param name="_poolName">Unique Key of pool.</param>
        /// <param name="_gameObject">GameObject which should multiplied in pool.</param>
        /// <param name="_length">Length of pool - how many copies of GameObject should be in pool.</param>
        public static void AddToPool(string _poolName, GameObject _gameObject, int _length)
        {
            for (int i = 0; i < _length; i++)
            {
                AddToPool(_poolName, _gameObject);
            }
        }

        /// <summary>  
        ///  Returns deactivated object from pool or creates new object(from first object in pool) if there is no free objects.
        /// </summary> 
        /// <param name="_poolName">Unique Key of pool given on adding.</param>
        /// <param name="_createNew">if true, it'll create new object when pool is empty of free objects.</param>
        public static GameObject GetFreeObjOf(string _poolName, bool _createNew)
        {
            if (Pool.ContainsKey(_poolName))
            {
                foreach (var obj in Pool[_poolName])
                {
                    if (!obj.activeSelf)
                        return obj;
                }
                if (_createNew)
                {
                    GameObject newObj = GameObject.Instantiate(Pool[_poolName][0]);
                    Pool[_poolName].Add(newObj);
                    return newObj;
                }
            }
            return null;
        }

        /// <summary>  
        ///  Returns deactivated object from pool or creates new object if there is no free objects.
        /// </summary> 
        /// <param name="_poolName">Unique Key of pool given on adding.</param>
        /// <param name="_index">Index of object in pool.</param>
        /// <param name="_createNew">if true, it'll create new object when pool is empty of free objects.</param>
        public static GameObject GetFreeObjOf(string _poolName, int _index, bool _createNew)
        {
            if (Pool.ContainsKey(_poolName) && _index < Pool.Count)
            {
                if (!Pool[_poolName][_index].activeSelf)
                {
                    return Pool[_poolName][_index];
                }
                else
                {
                    if (_createNew)
                    {
                        GameObject newObj = GameObject.Instantiate(Pool[_poolName][_index]);
                        Pool[_poolName].Add(newObj);
                        return newObj;
                    }
                }
            }
            return null;
        }

        /// <summary>  
        ///  Returns deactivated object from pool or creates new object if there is no free objects.
        /// </summary> 
        /// <param name="_gameObject">Object which has its pool.</param>
        /// <param name="_createNew">if true, it'll create new object when pool is empty of free objects.</param>
        public static GameObject GetFreeObjOf(GameObject _gameObject, bool _createNew)
        {
            string TKey = _gameObject.GetInstanceID().ToString();
            if (Pool.ContainsKey(TKey))
            {
                foreach (var obj in Pool[TKey])
                {
                    if (!obj.activeSelf)
                        return obj;
                }
                if (_createNew)
                {
                    GameObject newObj = GameObject.Instantiate(_gameObject);
                    Pool[TKey].Add(newObj);
                    return newObj;
                }
            }
            return null;
        }

        /// <summary>  
        ///  Returns first activated object from pool.
        /// </summary> 
        /// <param name="_poolName">Unique Key of pool given on adding.</param>
        public static GameObject GetBusyObjOf(string _poolName)
        {
            if (Pool.ContainsKey(_poolName))
            {
                foreach (var obj in Pool[_poolName])
                {
                    if (!obj.activeSelf)
                        return obj;
                }
            }
            return null;
        }

        /// <summary>  
        ///  Returns activated object from pool.
        /// </summary> 
        /// <param name="_poolName">Unique Key of pool given on adding.</param>
        /// <param name="_index">Index of object in pool.</param>
        public static GameObject GetBusyObjOf(string _poolName, int _index)
        {
            if (Pool.ContainsKey(_poolName) && _index < Pool.Count)
            {
                if (Pool[_poolName][_index].activeSelf)
                {
                    return Pool[_poolName][_index];
                }
            }
            return null;
        }

        /// <summary>  
        ///  Returns first activated object from pool.
        /// </summary> 
        /// <param name="_gameObject">Object which has its pool.</param>
        public static GameObject GetBusyObjOf(GameObject _gameObject)
        {
            string TKey = _gameObject.GetInstanceID().ToString();
            if (Pool.ContainsKey(TKey))
            {
                foreach (var obj in Pool[TKey])
                {
                    if (obj.activeSelf)
                        return obj;
                }
            }
            return null;
        }
        
        /// <summary>  
        ///  Returns List of all objects from pool.
        /// </summary> 
        /// <param name="_gameObject">Object which has its pool.</param>
        public static List<GameObject> GetAllObjects(GameObject _gameObject)
        {
            return GetAllObjects(_gameObject.GetInstanceID().ToString());
        }

        /// <summary>  
        ///  Returns List of all objects from pool.
        /// </summary> 
        /// <param name="_poolName">Unique Key of pool given on adding.</param>
        public static List<GameObject> GetAllObjects(string _poolName)
        {
            List<GameObject> objects = new List<GameObject>();
            if (Pool.ContainsKey(_poolName))
            {
                foreach (var obj in Pool[_poolName])
                {
                    freeObjects.Add(obj);
                }
            }
            return freeObjects;
        }

        /// <summary>  
        ///  Returns List of deactivated object from pool.
        /// </summary> 
        /// <param name="_gameObject">Object which has its pool.</param>
        public static List<GameObject> GetAllFreeObjects(GameObject _gameObject)
        {
            return GetAllFreeObjects(_gameObject.GetInstanceID().ToString());
        }

        /// <summary>  
        ///  Returns List of deactivated object from pool.
        /// </summary> 
        /// <param name="_poolName">Unique Key of pool given on adding.</param>
        public static List<GameObject> GetAllFreeObjects(string _poolName)
        {
            List<GameObject> freeObjects = new List<GameObject>();
            if (Pool.ContainsKey(_poolName))
            {
                foreach (var obj in Pool[_poolName])
                {
                    if (!obj.activeSelf)
                        freeObjects.Add(obj);
                }
            }
            return freeObjects;
        }

        /// <summary>  
        ///  Returns List of activated object from pool.
        /// </summary> 
        /// <param name="_gameObject">Object which has its pool.</param>
        public static List<GameObject> GetAllBusyObjects(GameObject _gameObject)
        {
            return GetAllBusyObjects(_gameObject.GetInstanceID().ToString());
        }

        /// <summary>  
        ///  Returns List of activated object from pool.
        /// </summary> 
        /// <param name="_poolName">Unique Key of pool given on adding.</param>
        public static List<GameObject> GetAllBusyObjects(string _poolName)
        {
            List<GameObject> busyObjects = new List<GameObject>();
            if (Pool.ContainsKey(_poolName))
            {
                foreach (var obj in Pool[_poolName])
                {
                    if (obj.activeSelf)
                        busyObjects.Add(obj);
                }
            }
            return busyObjects;
        }

        /// <summary>  
        ///  Returns length of deactivated objects from its pool.
        /// </summary> 
        /// <param name="_gameObject">Object which has its pool.</param>
        public static int FreObjLengthOf(GameObject _gameObject)
        {
            return FreObjLengthOf(_gameObject.GetInstanceID().ToString());
        }

        /// <summary>  
        ///  Returns length of deactivated objects from its pool.
        /// </summary> 
        /// <param name="_poolName">Unique Key of pool given on adding.</param>
        public static int FreObjLengthOf(string _poolName)
        {
            int length = 0;
            foreach (var obj in Pool[_poolName])
            {
                if (!obj.activeSelf)
                    length++;
            }
            return length;
        }

        /// <summary>  
        ///  Returns length of activated objects from its pool.
        /// </summary> 
        /// <param name="_gameObject">Object which has its pool.</param>
        public static int BusyObjLengthOf(GameObject _gameObject)
        {
            return BusyObjLengthOf(_gameObject.GetInstanceID().ToString());
        }

        /// <summary>  
        ///  Returns length of activated objects from its pool.
        /// </summary> 
        /// <param name="_poolName">Unique Key of pool given on adding.</param>
        public static int BusyObjLengthOf(string _poolName)
        {
            int length = 0;
            foreach (var obj in Pool[_poolName])
            {
                if (obj.activeSelf)
                    length++;
            }
            return length;
        }

        /// <summary>  
        ///  Returns length of all objects from its pool.
        /// </summary> 
        /// <param name="_gameObject">Object which has its pool.</param>
        public static int ObjLengthOf(GameObject _gameObject)
        {
            return ObjLengthOf(_gameObject.GetInstanceID().ToString());
        }

        /// <summary>  
        ///  Returns length of all objects from its pool.
        /// </summary> 
        /// <param name="_poolName">Unique Key of pool given on adding.</param>
        public static int ObjLengthOf(string _poolName)
        {
            int length = 0;
            foreach (var obj in Pool[_poolName])
            {
                length++;
            }
            return length;
        }

        /// <summary>  
        ///  Clears all pools.
        /// </summary>  
        public static void Dispose()
        {
            Pool.Clear();
        }
    }
}