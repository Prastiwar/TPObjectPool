/**
*   Authored by Tomasz Piowczyk
*   MIT LICENSE (https://github.com/Prastiwar/TPObjectPool/blob/master/LICENSE)
*/
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace TP
{
    public enum TPObjectState
    {
        Auto = 0, // Deactive or Active
        Deactive = 2,
        Active = 4
    }

    // ------------------------------------------- Monobehaviour with more effective coroutines ---------------------------------------------------------------- //

    /// <summary> Class that allows to effectively create garbage free coroutines </summary>
    internal class TPObjectPooler : MonoBehaviour
    {
        /// <summary> Coroutine object hold data needed for ToggleActive </summary>
        private struct PoolCoroutine
        {
            public float Delay;
            public int PoolKey;
            public TPObjectState State;
            public bool ToggleAll;
            public bool CreateNew;
            public Vector3? Position;
            public Quaternion? Rotation;
        }

        private Queue<PoolCoroutine> coroutines = new Queue<PoolCoroutine>();
        private int length;

        internal void Add(bool toggleAll, int poolKey, float delay, TPObjectState state, bool createNew, Vector3? position, Quaternion? rotation)
        {
            PoolCoroutine newUp = new PoolCoroutine {
                PoolKey = poolKey,
                Delay = delay,
                State = state,
                CreateNew = createNew,
                Position = position,
                Rotation = rotation,
                ToggleAll = toggleAll
            };
            coroutines.Enqueue(newUp);
            length++;
        }

        private void Update()
        {
            float delta = Time.deltaTime;
            for (int i = 0; i < length; i++)
            {
                var coroutine = coroutines.Dequeue();
                coroutine.Delay -= delta;
                if (coroutine.Delay <= 0.0f)
                {
                    TPObjectPool.ToggleActive(coroutine.PoolKey, coroutine.State, coroutine.CreateNew, coroutine.Position, coroutine.Rotation);
                    TPObjectPool.ToggleActiveAll(coroutine.PoolKey, coroutine.State, coroutine.Position, coroutine.Rotation);
                    length--;
                }
                else
                {
                    coroutines.Enqueue(coroutine);
                }
            }
        }
    }



    // ---------------------------------------------- Collection of pooled objects ------------------------------------------------------------------------- //

    /// <summary> "Collection" of pooled objects </summary>
    public class TPPoolContainer
    {
        private Stack<GameObject> deactiveObjects;
        private Stack<GameObject> activeObjects;

        public int ObjectsLength;
        public int ActiveLength;
        public int DeactiveLength;

        public TPPoolContainer()
        {
            deactiveObjects = new Stack<GameObject>();
            activeObjects = new Stack<GameObject>();
            ObjectsLength = 0;
            ActiveLength = 0;
            DeactiveLength = 0;
        }

        public TPPoolContainer(params GameObject[] gameObjects)
        {
            int length = gameObjects.Length;
            deactiveObjects = new Stack<GameObject>(gameObjects);
            activeObjects = new Stack<GameObject>();
            ObjectsLength = length;
            ActiveLength = 0;
            DeactiveLength = length;
        }

        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public void Push(GameObject poolObject)
        {
            var state = poolObject.GetState();
            ObjectsLength++;
            if (state == TPObjectState.Deactive)
            {
                deactiveObjects.Push(poolObject);
                DeactiveLength++;
            }
            else
            {
                activeObjects.Push(poolObject);
                ActiveLength++;
            }
        }

        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public void ToggleState(TPObjectState state = TPObjectState.Auto)
        {
            var popObject = Pop(state);
            if (popObject != null)
            {
                popObject.SetActive(!popObject.activeSelf);
                Push(popObject);
            }
        }

        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public GameObject Pop(TPObjectState state = TPObjectState.Auto)
        {
            if (ObjectsLength > 0)
            {
                if (state == TPObjectState.Auto)
                {
                    var obj = Pop(TPObjectState.Deactive);
                    return obj ?? Pop(TPObjectState.Active);
                }
                else if (state == TPObjectState.Deactive)
                {
                    return PopDeactive();
                }
                else
                {
                    return PopActive();
                }
            }
            return null;
        }

        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public GameObject Peek(TPObjectState state = TPObjectState.Auto)
        {
            switch (state)
            {
                case TPObjectState.Deactive:
                    return DeactiveLength > 0 ? deactiveObjects.Peek() : null;
                case TPObjectState.Active:
                    return ActiveLength > 0 ? activeObjects.Peek() : null;
                case TPObjectState.Auto:
                    var freeObj = Peek(TPObjectState.Deactive);
                    return freeObj ?? Peek(TPObjectState.Active);
            }
            return null;
        }

        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public void Dispose(bool destroyObjects = true)
        {
            if (destroyObjects)
            {
                while (ActiveLength > 0)
                    UnityEngine.Object.Destroy(PopActive());
            }
            activeObjects.Clear();
            RemoveUnused(destroyObjects);
            ActiveLength = 0;
            ObjectsLength = 0;
        }

        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public void RemoveUnused(bool destroyObjects = true)
        {
            if (destroyObjects)
            {
                while (DeactiveLength > 0)
                    UnityEngine.Object.Destroy(PopDeactive());
            }
            DeactiveLength = 0;
            deactiveObjects.Clear();
        }

        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        private GameObject PopActive()
        {
            if (ActiveLength > 0)
            {
                ObjectsLength--;
                ActiveLength--;
                return activeObjects.Pop();
            }
            return null;
        }

        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        private GameObject PopDeactive()
        {
            if (DeactiveLength > 0)
            {
                ObjectsLength--;
                DeactiveLength--;
                return deactiveObjects.Pop();
            }
            return null;
        }
    }



    // ---------------------------------------------- Static Class Object Pool Manager ---------------------------------------------------------------------------//

    /// <summary> This class allows you to manage TPPoolContainer collection with its pooled objects. </summary>  
    public static class TPObjectPool
    {
        public delegate void ActivationEventHandler(GameObject poolObject, bool active);

        /// <summary> This ActivationEventHandler delegate is called just before object from pool is set active/deactive </summary>  
        public static ActivationEventHandler OnBeforeActivation { get; set; }

        /// <summary> This ActivationEventHandler delegate is called just after object from pool is set active/deactive </summary>  
        public static ActivationEventHandler OnAfterActivation { get; set; }

        /// <summary> Lookup holds pooled object collections </summary>
        private static Dictionary<int, TPPoolContainer> pool = new Dictionary<int, TPPoolContainer>();

        /// <summary> Reference to coroutine manager </summary>
        private static TPObjectPooler monoCoroutineManager;

        /// <summary> Max array length before rebuilding it </summary>
        private static int reusableArraysLength = 32;

        /// <summary> Persistant allocated array to prevent creating GC runtime </summary>
        private static GameObject[] reusableArray = new GameObject[reusableArraysLength];

        /// <summary> Returns reusableArray or rebuilds it if needed </summary>
        private static GameObject[] ReusableArray(int length)
        {
            if (reusableArraysLength < length)
            {
                reusableArray = new GameObject[length];
                reusableArraysLength = length;
            }
            return reusableArray;
        }

        /// <summary> Reference to coroutine manager </summary>
        private static TPObjectPooler MonoCoroutineManager {
            get {
                if (monoCoroutineManager == null)
                    monoCoroutineManager = new GameObject("TPPoolCoroutineManager").AddComponent<TPObjectPooler>();
                return monoCoroutineManager;
            }
        }

        /// <summary> Toggles active(state) of first found object of given state </summary>
        /// <param name="poolKey"> Unique Key of pool </param>
        /// <param name="state"> State of object </param>
        /// <param name="createNew"> Should create new object if none found? </param>
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static void ToggleActive(int poolKey, TPObjectState state = TPObjectState.Auto, bool createNew = false, Vector3? position = null, Quaternion? rotation = null)
        {
            if (SafeKey(poolKey))
            {
                GameObject poolObject = Peek(poolKey, state);
                if (poolObject != null)
                {
                    var active = !poolObject.GetState().ActiveSelf();

                    if (position.HasValue)
                        poolObject.transform.position = position.Value;
                    if (rotation.HasValue)
                        poolObject.transform.rotation = rotation.Value;

                    SafeInvoke(OnBeforeActivation, poolObject, active);
                    pool[poolKey].ToggleState(state);
                    SafeInvoke(OnAfterActivation, poolObject, active);
                }
                else if (createNew)
                {
                    var newObj = Peek(poolKey);
                    if (newObj != null)
                    {
                        AddToPool(poolKey, newObj);
                        ToggleActive(poolKey, state, createNew, position, rotation);
                    }
                }
            }
        }

        /// <summary> Toggles active(state) of all found objects of given state </summary>
        /// <param name="poolKey"> Unique Key of pool </param>
        /// <param name="state"> State of object </param>
        /// <param name="createNew"> Should create new object if none found? </param>
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static void ToggleActiveAll(int poolKey, TPObjectState state = TPObjectState.Auto, Vector3? position = null, Quaternion? rotation = null)
        {
            if (SafeKey(poolKey))
            {
                int length = Length(poolKey, state);
                var poolObjects = PopObjects(poolKey, state);
                for (int i = 0; i < length; i++)
                {
                    var poolObject = poolObjects[i];
                    var active = !poolObject.GetState().ActiveSelf();

                    if (position.HasValue)
                        poolObject.transform.position = position.Value;
                    if (rotation.HasValue)
                        poolObject.transform.rotation = rotation.Value;

                    SafeInvoke(OnBeforeActivation, poolObject, active);
                    poolObject.SetActive(active);
                    SafeInvoke(OnAfterActivation, poolObject, active);
                    pool[poolKey].Push(poolObject);
                }
            }
        }

#if NET_2_0 || NET_2_0_SUBSET
        /// <summary> Toggles active(state) of all found objects of given state </summary> 
        /// <param name="poolKey"> Unique Key of pool </param>
        /// <param name="state"> State of object </param>
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static void ToggleActiveAll(int poolKey, float delay, TPObjectState state = TPObjectState.Auto, Vector3? position = null, Quaternion? rotation = null)
        {
            MonoCoroutineManager.Add(true, poolKey, delay, state, false, position, rotation);
        }

        /// <summary> Toggles active(state) of first found object of given state </summary> 
        /// <param name="poolKey"> Unique Key of pool </param>
        /// <param name="state"> State of object </param>
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static void ToggleActive(int poolKey, float delay, TPObjectState state = TPObjectState.Auto, bool createNew = false, Vector3? position = null, Quaternion? rotation = null)
        {
            MonoCoroutineManager.Add(false, poolKey, delay, state, createNew, position, rotation);
        }
#else

        /// <summary> Toggles active(state) of first found object of given state after delay </summary> 
        /// <param name="poolKey"> Unique Key of pool </param>
        /// <param name="state"> State of object </param>
        /// <param name="createNew"> Should create new object if none found? </param>
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static async void ToggleActive(int poolKey, float delay, TPObjectState state = TPObjectState.Auto, bool createNew = false, Vector3? position = null, Quaternion? rotation = null)
        {
            await System.Threading.Tasks.Task.Delay(System.TimeSpan.FromSeconds(delay));
            ToggleActive(poolKey, state, createNew, position, rotation);
        }

        /// <summary> Toggles active(state) of all found objects of given state after delay </summary> 
        /// <param name="poolKey"> Unique Key of pool </param>
        /// <param name="state"> State of object </param>
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static async void ToggleActiveAll(int poolKey, float delay, TPObjectState state = TPObjectState.Auto, Vector3? position = null, Quaternion? rotation = null)
        {
            await System.Threading.Tasks.Task.Delay(System.TimeSpan.FromSeconds(delay));
            ToggleActiveAll(poolKey, state, position, rotation);
        }
#endif

        /// <summary> Creates or adds to existing pool with its unique key with pool objects </summary> 
        /// <param name="poolKey"> Unique Key of pool </param>
        /// <param name="poolObjects"> All pool objects which should be in one pool </param>
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static void AddToPool(int poolKey, params GameObject[] poolObjects)
        {
            int length = poolObjects.Length;
            GameObject[] spawned = new GameObject[length];

            for (int i = 0; i < length; i++)
            {
                spawned[i] = UnityEngine.Object.Instantiate(poolObjects[i]);
                spawned[i].SetActive(false);
            }

            if (HasKey(poolKey))
            {
                for (int i = 0; i < length; i++)
                    pool[poolKey].Push(spawned[i]);
            }
            else
            {
                pool[poolKey] = new TPPoolContainer(spawned);
            }
        }

        /// <summary> Creates pool of unique Key of length with GameObject </summary> 
        /// <param name="poolKey"> Unique key of pool </param>
        /// <param name="poolObject"> GameObject which should multiplied in pool </param>
        /// <param name="length"> Length of pool - how many copies of GameObject should be in pool </param>
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static void AddToPool(int poolKey, GameObject poolObject, int length)
        {
            GameObject[] copies = new GameObject[length];
            for (int i = 0; i < length; i++)
                copies[i] = poolObject;
            AddToPool(poolKey, copies);
        }

        /// <summary> Put (doesn't instantiate) object to existing pool </summary> 
        /// <param name="poolKey"> Unique key of pool </param>
        /// <param name="poolObject"> Object which should be added </param>
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static void PushObject(int poolKey, GameObject poolObject)
        {
            if (SafeKey(poolKey))
                pool[poolKey].Push(poolObject);
        }

        /// <summary> Takes out (removes and returns obj from pool) first object in state from pool </summary> 
        /// <param name="poolKey"> Unique key of pool </param>
        /// <param name="state"> State of object </param>
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static GameObject PopObject(int poolKey, TPObjectState state = TPObjectState.Auto)
        {
            if (SafeKey(poolKey))
                return pool[poolKey].Pop(state);
            return null;
        }

        /// <summary> Takes out (removes and returns objs from pool) array of objects in state from pool </summary> 
        /// <param name="poolKey"> Unique key of pool </param>
        /// <param name="state"> State of object </param>
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static GameObject[] PopObjects(int poolKey, TPObjectState state = TPObjectState.Auto)
        {
            int length = Length(poolKey, state);
            var array = ReusableArray(length);
            for (int i = 0; i < length; i++)
                array[i] = PopObject(poolKey, state);
            return array;
        }

        /// <summary> Returns (without removing from pool) object in state from pool </summary> 
        /// <param name="poolKey"> Unique key of pool </param>
        /// <param name="state"> State of object </param>
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static GameObject Peek(int poolKey, TPObjectState state = TPObjectState.Auto)
        {
            return SafeKey(poolKey) ? pool[poolKey].Peek(state) : null;
        }

        /// <summary> Returns length of all objects in state from its pool </summary> 
        /// <param name="poolKey"> Unique key of pool </param>
        /// <param name="state"> State of object </param>
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static int Length(int poolKey, TPObjectState state = TPObjectState.Auto)
        {
            if (SafeKey(poolKey))
            {
                switch (state)
                {
                    case TPObjectState.Deactive:
                        return pool[poolKey].DeactiveLength;
                    case TPObjectState.Active:
                        return pool[poolKey].ActiveLength;
                    case TPObjectState.Auto:
                        return pool[poolKey].ObjectsLength;
                }
            }
            return 0;
        }

        /// <summary> Checks if there is any object in state in pool </summary>
        /// <param name="poolKey"> Unique Key of pool </param>
        /// <param name="state"> State of object </param>
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static bool HasAnyObject(int poolKey, TPObjectState state = TPObjectState.Auto)
        {
            return Length(poolKey, state) > 0;
        }

        /// <summary> Clears pool from deactive (unused) objects </summary> 
        /// <param name="poolKey"> Unique key of pool </param>
        /// <param name="destroyObjects"> Should destroy objects? </param>
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static void RemoveUnused(int poolKey, bool destroyObjects = true)
        {
            if (SafeKey(poolKey))
                pool[poolKey].RemoveUnused(destroyObjects);
        }

        /// <summary> Clears whole pool. </summary> 
        /// <param name="poolKey"> Unique key of pool </param>
        /// <param name="destroyObjects"> Should destroy objects? </param>
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static void Dispose(int poolKey, bool destroyObjects = true)
        {
            if (SafeKey(poolKey))
            {
                pool[poolKey].Dispose(destroyObjects);
                pool.Remove(poolKey);
            }
        }

        /// <summary> Clears all pools </summary>  
        /// <param name="destroyObjects"> Should destroy objects? </param>
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static void Dispose(bool destroyObjects = true)
        {
            foreach (var pair in pool)
                Dispose(pair.Key, destroyObjects);
            pool.Clear();
        }

        /// <summary> Checks if given key(pool) exists </summary> 
        /// <param name="poolKey"> Unique key of pool </param>
        public static bool HasKey(int poolKey)
        {
            return pool.ContainsKey(poolKey);
        }

        /// <summary> Converts GameObject activeSelf to state </summary>
        public static TPObjectState GetState(this GameObject poolObject)
        {
            return poolObject.activeSelf ? TPObjectState.Active : TPObjectState.Deactive;
        }

        /// <summary> Converts State to GameObject activeSelf </summary>
        public static bool ActiveSelf(this TPObjectState state)
        {
            return state == TPObjectState.Deactive ? false : true;
        }

        /// <summary> Returns true if poolObject has given state </summary>
        public static bool HasState(this GameObject poolObject, TPObjectState state)
        {
            return state == GetState(poolObject) || state == TPObjectState.Auto;
        }

        /// <summary> Sets active all objects in array </summary>
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static void SetActive(this GameObject[] poolObjects, bool active)
        {
            int length = poolObjects.Length;
            for (int i = 0; i < length; i++)
                poolObjects[i].SetActive(active);
        }

        /// <summary> This checks for null before ActivationEventHandler is called </summary>  
        private static void SafeInvoke(ActivationEventHandler onActivation, GameObject poolObject, bool value)
        {
            if (onActivation != null)
                onActivation(poolObject, value);
        }

        /// <summary> This checks for existing key. Returns true if is safe </summary>  
        private static bool SafeKey(int poolKey)
        {
            if (!HasKey(poolKey))
            {
                Debug.LogError("Pool with this key doesn't exist: " + poolKey);
                return false;
            }
            return true;
        }

    }
}