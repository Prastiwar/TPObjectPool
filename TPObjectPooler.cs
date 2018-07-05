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
        [System.Serializable]
        private struct PoolCoroutine
        {
            public int PoolKey;
            public float Delay;
            public TPObjectState State;
            public GameObject PoolObject;
            public Vector3 Position;
            public Quaternion Rotation;
            public bool CreateNew;
            public bool PushObject;
            public bool ToggleAll;
        }

        private Queue<PoolCoroutine> coroutines = new Queue<PoolCoroutine>();
        private int length;

        private void Awake()
        {
            coroutines = new Queue<PoolCoroutine>(3000);
        }

        internal void AddCoroutine(int poolKey, float delay, TPObjectState state, Vector3 position, Quaternion rotation, bool createNew = false, bool toggleAll = false)
        {
            AddNew(new PoolCoroutine {
                PoolKey = poolKey,
                Delay = delay,
                State = state,
                PoolObject = null,
                Position = position,
                Rotation = rotation,
                PushObject = true,
                CreateNew = createNew,
                ToggleAll = toggleAll,
            });
        }

        internal void AddCoroutine(int poolKey, float delay, GameObject poolObject, Vector3 position, Quaternion rotation, bool pushObject = false)
        {
            AddNew(new PoolCoroutine {
                PoolKey = poolKey,
                Delay = delay,
                State = poolObject.GetState(),
                CreateNew = false,
                Position = position,
                Rotation = rotation,
                ToggleAll = false,
                PoolObject = poolObject,
                PushObject = pushObject
            });
        }

        private void AddNew(PoolCoroutine coroutine)
        {
            coroutines.Enqueue(coroutine);
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
                    if (!coroutine.ToggleAll)
                    {
                        if (coroutine.PoolObject != null)
                            TPObjectPool.ToggleActive(coroutine.PoolKey, coroutine.PoolObject, coroutine.Position, coroutine.Rotation, coroutine.PushObject);
                        else
                            TPObjectPool.ToggleActive(coroutine.PoolKey, coroutine.State, coroutine.Position, coroutine.Rotation, coroutine.CreateNew);
                    }
                    else
                    {
                        TPObjectPool.ToggleActiveAll(coroutine.PoolKey, coroutine.State, coroutine.Position, coroutine.Rotation);
                    }
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
        public int Capacity;

        public TPPoolContainer(int capacity = 10)
        {
            Capacity = capacity;
            deactiveObjects = new Stack<GameObject>(Capacity);
            activeObjects = new Stack<GameObject>(Capacity);
            ObjectsLength = 0;
            ActiveLength = 0;
            DeactiveLength = 0;
        }

        public TPPoolContainer(int capacity = 10, params GameObject[] gameObjects)
        {
            int length = gameObjects.Length;
            Capacity = capacity + length;
            deactiveObjects = new Stack<GameObject>(Capacity);
            activeObjects = new Stack<GameObject>(Capacity);

            for (int i = 0; i < length; i++)
                Push(gameObjects[i]);
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

        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        private void TrimExcess()
        {
            deactiveObjects.TrimExcess();
            activeObjects.TrimExcess();
        }
    }



    // ---------------------------------------------- Static Class Object Pool Manager ---------------------------------------------------------------------------//

    /// <summary> This class allows you to manage TPPoolContainer collection with its pooled objects. </summary>  
    public static class TPObjectPool
    {
        /// <summary> Lookup holds pooled object collections </summary>
        private static Dictionary<int, TPPoolContainer> pool = new Dictionary<int, TPPoolContainer>();

        private static Quaternion defaultQuaternion = Quaternion.identity;
        private static Vector3 defaultPosition = Vector3.zero;

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

        /// <summary> Creates pool with its unique key with pool objects </summary>
        /// <param name="capacity"> Additional capacity to stack (final is capacity + objects length) </param>
        /// <param name="poolObjects"> All pool objects which should be in one pool </param>
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static void CreatePool(int poolKey, int capacity = 10, params GameObject[] poolObjects)
        {
            if (!HasKey(poolKey))
            {
                int length = poolObjects.Length;
                GameObject[] spawned = new GameObject[length];

                for (int i = 0; i < length; i++)
                {
                    spawned[i] = UnityEngine.Object.Instantiate(poolObjects[i]);
                    spawned[i].SetActive(false);
                }
                pool[poolKey] = new TPPoolContainer(capacity + length, spawned);
            }
            else
            {
                Debug.LogError("Pool with this key already exists " + poolKey);
            }
        }

        /// <summary> Creates pool with its unique key with pool objects </summary>
        /// <param name="poolObject"> Pool object which should multiplied in pool </param>
        /// <param name="length"> Length of pool - how many copies of GameObject should be in pool </param>
        /// <param name="capacity"> Additional capacity to stack (final is capacity + length) </param>
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static void CreatePool(int poolKey, GameObject poolObject, int length, int capacity = 10)
        {
            GameObject[] copies = new GameObject[length];
            for (int i = 0; i < length; i++)
                copies[i] = poolObject;
            CreatePool(poolKey, capacity + length, copies);
        }

        /// <summary> Put (doesn't instantiate) object to existing pool </summary>
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static void PushObject(int poolKey, GameObject poolObject)
        {
            if (SafeKey(poolKey))
                pool[poolKey].Push(poolObject);
        }

        /// <summary> Put (doesn't instantiate) object to existing pool </summary>
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static void PushObjects(int poolKey, params GameObject[] poolObjects)
        {
            if (SafeKey(poolKey))
            {
                int length = poolObjects.Length;
                for (int i = 0; i < length; i++)
                    pool[poolKey].Push(poolObjects[i]);
            }
        }

        /// <summary> Takes out (removes and returns obj from pool) first object in state from pool </summary>
        /// <param name="createNew"> Should create new if don't find? </param>
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static GameObject PopObject(int poolKey, TPObjectState state = TPObjectState.Auto, bool createNew = false)
        {
            if (SafeKey(poolKey))
            {
                var obj = pool[poolKey].Pop(state);
                if (obj == null && createNew)
                {
                    obj = pool[poolKey].Pop();
                    if (obj != null)
                    {
                        obj = UnityEngine.Object.Instantiate(obj);
                        obj.SetActive(state.ActiveSelf());
                    }
                    else
                    {
                        Debug.LogError("You can't create new object from empty pool! Pool key: " + poolKey);
                    }
                }
                return obj;
            }
            return null;
        }

        /// <summary> Takes out (removes and returns objs from pool) array of objects in state from pool </summary>
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
        /// <param name="createNew"> Should create new if don't find? </param>
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static GameObject Peek(int poolKey, TPObjectState state = TPObjectState.Auto, bool createNew = false)
        {
            if (SafeKey(poolKey))
            {
                var obj = pool[poolKey].Peek(state);
                if (obj == null && createNew)
                {
                    obj = pool[poolKey].Peek();
                    if (obj != null)
                    {
                        obj = UnityEngine.Object.Instantiate(obj);
                        obj.SetActive(state.ActiveSelf());
                        pool[poolKey].Push(obj);
                    }
                    else
                    {
                        Debug.LogError("You can't create new object from empty pool! Pool key: " + poolKey);
                    }
                }
                return obj;
            }
            return null;
        }

        /// <param name="pushObject"> Should push object to pool after toggle activation? </param>
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static void ToggleActive(int poolKey, GameObject poolObject, bool pushObject = false)
        {
            if (SafeKey(poolKey))
            {
                if (SafeObject(poolObject))
                {
                    poolObject.SetActive(!poolObject.activeSelf);
                    if (pushObject)
                        PushObject(poolKey, poolObject);
                }
            }
        }

        /// <param name="pushObject"> Should push object to pool after toggle activation? </param>
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static void ToggleActive(int poolKey, GameObject poolObject, Vector3 position, Quaternion rotation, bool pushObject = false)
        {
            if (SafeKey(poolKey))
            {
                if (SafeObject(poolObject))
                {
                    poolObject.transform.SetPositionAndRotation(position, rotation);
                    poolObject.SetActive(!poolObject.activeSelf);
                    if (pushObject)
                        PushObject(poolKey, poolObject);
                }
            }
        }

        /// <param name="pushObject"> Should push object to pool after toggle activation? </param>
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static void ToggleActive(int poolKey, GameObject poolObject, Vector3 position, bool pushObject = false)
        {
            ToggleActive(poolKey, poolObject, position, poolObject.transform.rotation, pushObject);
        }

        /// <param name="pushObject"> Should push object to pool after toggle activation? </param>
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static void ToggleActive(int poolKey, GameObject poolObject, Quaternion rotation, bool pushObject = false)
        {
            ToggleActive(poolKey, poolObject, poolObject.transform.position, rotation, pushObject);
        }

        /// <summary> Toggles active of first found object with given state </summary>
        /// <param name="state"> State of searched object to be toggled </param>
        /// <param name="createNew"> Should create new object if none found? </param>
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static void ToggleActive(int poolKey, TPObjectState state, bool createNew = false)
        {
            if (SafeKey(poolKey))
            {
                GameObject poolObject = PopObject(poolKey, state, createNew);
                if (SafeObject(poolKey, state, poolObject))
                    ToggleActive(poolKey, poolObject, true);
            }
        }

        /// <summary> Toggles active of first found object with given state </summary>
        /// <param name="state"> State of searched object to be toggled </param>
        /// <param name="createNew"> Should create new object if none found? </param>
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static void ToggleActive(int poolKey, TPObjectState state, Vector3 position, Quaternion rotation, bool createNew = false)
        {
            if (SafeKey(poolKey))
            {
                GameObject poolObject = PopObject(poolKey, state, createNew);
                if (SafeObject(poolKey, state, poolObject))
                    ToggleActive(poolKey, poolObject, position, rotation, true);
            }
        }

        /// <summary> Toggles active of first found object with given state </summary>
        /// <param name="state"> State of searched object to be toggled </param>
        /// <param name="createNew"> Should create new object if none found? </param>
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static void ToggleActive(int poolKey, TPObjectState state, Vector3 position, bool createNew = false)
        {
            if (SafeKey(poolKey))
            {
                GameObject poolObject = PopObject(poolKey, state, createNew);
                if (SafeObject(poolKey, state, poolObject))
                    ToggleActive(poolKey, poolObject, position, true);
            }
        }

        /// <summary> Toggles active of first found object with given state </summary>
        /// <param name="state"> State of searched object to be toggled </param>
        /// <param name="createNew"> Should create new object if none found? </param>
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static void ToggleActive(int poolKey, TPObjectState state, Quaternion rotation, bool createNew = false)
        {
            if (SafeKey(poolKey))
            {
                GameObject poolObject = PopObject(poolKey, state, createNew);
                if (SafeObject(poolKey, state, poolObject))
                    ToggleActive(poolKey, poolObject, rotation, true);
            }
        }

        /// <summary> Toggles active all found objects of given state </summary>
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static void ToggleActiveAll(int poolKey, TPObjectState state)
        {
            if (SafeKey(poolKey))
            {
                int length = Length(poolKey, state);
                var poolObjects = PopObjects(poolKey, state);
                for (int i = 0; i < length; i++)
                    ToggleActive(poolKey, poolObjects[i], true);
            }
        }

        /// <summary> Toggles active all found objects of given state </summary>
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static void ToggleActiveAll(int poolKey, TPObjectState state, Vector3 position, Quaternion rotation)
        {
            if (SafeKey(poolKey))
            {
                int length = Length(poolKey, state);
                var poolObjects = PopObjects(poolKey, state);
                for (int i = 0; i < length; i++)
                    ToggleActive(poolKey, poolObjects[i], position, rotation, true);
            }
        }

        /// <summary> Toggles active all found objects of given state </summary>
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static void ToggleActiveAll(int poolKey, TPObjectState state, Vector3 position)
        {
            if (SafeKey(poolKey))
            {
                int length = Length(poolKey, state);
                var poolObjects = PopObjects(poolKey, state);
                for (int i = 0; i < length; i++)
                    ToggleActive(poolKey, poolObjects[i], position, true);
            }
        }

        /// <summary> Toggles active all found objects of given state </summary>
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static void ToggleActiveAll(int poolKey, TPObjectState state, Quaternion rotation)
        {
            if (SafeKey(poolKey))
            {
                int length = Length(poolKey, state);
                var poolObjects = PopObjects(poolKey, state);
                for (int i = 0; i < length; i++)
                    ToggleActive(poolKey, poolObjects[i], rotation, true);
            }
        }

#if NET_2_0 || NET_2_0_SUBSET
        /// <param name="pushObject"> Should push object after toggling it? </param>
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static void ToggleActive(int poolKey, float delay, GameObject poolObject, bool pushObject = false)
        {
            MonoCoroutineManager.AddCoroutine(poolKey, delay, poolObject, poolObject.transform.position, poolObject.transform.rotation, pushObject);
        }

        /// <param name="pushObject"> Should push object after toggling it? </param>
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static void ToggleActive(int poolKey, float delay, GameObject poolObject, Vector3 position, Quaternion rotation, bool pushObject = false)
        {
            MonoCoroutineManager.AddCoroutine(poolKey, delay, poolObject, position, rotation, pushObject);
        }

        /// <param name="pushObject"> Should push object after toggling it? </param>
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static void ToggleActive(int poolKey, float delay, GameObject poolObject, Vector3 position, bool pushObject = false)
        {
            ToggleActive(poolKey, delay, poolObject, position, poolObject.transform.rotation, pushObject);
        }

        /// <param name="pushObject"> Should push object after toggling it? </param>
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static void ToggleActive(int poolKey, float delay, GameObject poolObject, Quaternion rotation, bool pushObject = false)
        {
            ToggleActive(poolKey, delay, poolObject, poolObject.transform.position, rotation, pushObject);
        }

        /// <summary> Toggles active of first found object with given state </summary>
        /// <param name="createNew"> Should create new object if none found? </param>
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static void ToggleActive(int poolKey, float delay, TPObjectState state, bool createNew = false)
        {
            MonoCoroutineManager.AddCoroutine(poolKey, delay, state, defaultPosition, defaultQuaternion, createNew);
        }

        /// <summary> Toggles active of first found object with given state </summary>
        /// <param name="createNew"> Should create new object if none found? </param>
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static void ToggleActive(int poolKey, float delay, TPObjectState state, Vector3 position, Quaternion rotation, bool createNew = false)
        {
            MonoCoroutineManager.AddCoroutine(poolKey, delay, state, position, rotation, createNew);
        }

        /// <summary> Toggles active of first found object with given state </summary>
        /// <param name="createNew"> Should create new object if none found? </param>
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static void ToggleActive(int poolKey, float delay, TPObjectState state, Vector3 position, bool createNew = false)
        {
            ToggleActive(poolKey, delay, state, position, defaultQuaternion, createNew);
        }

        /// <summary> Toggles active of first found object with given state </summary>
        /// <param name="createNew"> Should create new object if none found? </param>
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static void ToggleActive(int poolKey, float delay, TPObjectState state, Quaternion rotation, bool createNew = false)
        {
            ToggleActive(poolKey, delay, state, defaultPosition, rotation, createNew);
        }

        /// <summary> Toggles active all found objects of given state </summary>
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static void ToggleActiveAll(int poolKey, float delay, TPObjectState state, Vector3 position, Quaternion rotation)
        {
            MonoCoroutineManager.AddCoroutine(poolKey, delay, state, position, rotation, false, true);
        }

        /// <summary> Toggles active all found objects of given state </summary>
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static void ToggleActiveAll(int poolKey, float delay, TPObjectState state, Vector3 position)
        {
            ToggleActiveAll(poolKey, delay, state, position, defaultQuaternion);
        }

        /// <summary> Toggles active all found objects of given state </summary>
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static void ToggleActiveAll(int poolKey, float delay, TPObjectState state, Quaternion rotation)
        {
            ToggleActiveAll(poolKey, delay, state, defaultPosition, rotation);
        }
#else

        /// <param name="createNew"> Should create new object if none found? </param>
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static async void ToggleActive(int poolKey, float delay, GameObject poolObject, bool pushObject = false)
        {
            await System.Threading.Tasks.Task.Delay(System.TimeSpan.FromSeconds(delay));
            ToggleActive(poolKey, poolObject, pushObject);
        }

        /// <param name="createNew"> Should create new object if none found? </param>
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static async void ToggleActive(int poolKey, float delay, GameObject poolObject, Vector3 position, Quaternion rotation, bool pushObject = false)
        {
            await System.Threading.Tasks.Task.Delay(System.TimeSpan.FromSeconds(delay));
            ToggleActive(poolKey, poolObject, position, rotation, pushObject);
        }

        /// <param name="createNew"> Should create new object if none found? </param>
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static async void ToggleActive(int poolKey, float delay, GameObject poolObject, Vector3 position, bool pushObject = false)
        {
            await System.Threading.Tasks.Task.Delay(System.TimeSpan.FromSeconds(delay));
            ToggleActive(poolKey, poolObject, position, pushObject);
        }

        /// <param name="createNew"> Should create new object if none found? </param>
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static async void ToggleActive(int poolKey, float delay, GameObject poolObject, Quaternion rotation, bool pushObject = false)
        {
            await System.Threading.Tasks.Task.Delay(System.TimeSpan.FromSeconds(delay));
            ToggleActive(poolKey, poolObject, rotation, pushObject);
        }

        /// <summary> Toggles active of first found object with given state </summary>
        /// <param name="createNew"> Should create new object if none found? </param>
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static async void ToggleActive(int poolKey, float delay, TPObjectState state, bool createNew = false)
        {
            await System.Threading.Tasks.Task.Delay(System.TimeSpan.FromSeconds(delay));
            ToggleActive(poolKey, state, createNew);
        }

        /// <summary> Toggles active of first found object with given state </summary>
        /// <param name="createNew"> Should create new object if none found? </param>
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static async void ToggleActive(int poolKey, float delay, TPObjectState state, Vector3 position, Quaternion rotation, bool createNew = false)
        {
            await System.Threading.Tasks.Task.Delay(System.TimeSpan.FromSeconds(delay));
            ToggleActive(poolKey, state, position, rotation, createNew);
        }

        /// <summary> Toggles active of first found object with given state </summary>
        /// <param name="createNew"> Should create new object if none found? </param>
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static async void ToggleActive(int poolKey, float delay, TPObjectState state, Vector3 position, bool createNew = false)
        {
            await System.Threading.Tasks.Task.Delay(System.TimeSpan.FromSeconds(delay));
            ToggleActive(poolKey, state, position, createNew);
        }

        /// <summary> Toggles active of first found object with given state </summary>
        /// <param name="createNew"> Should create new object if none found? </param>
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static async void ToggleActive(int poolKey, float delay, TPObjectState state, Quaternion rotation, bool createNew = false)
        {
            await System.Threading.Tasks.Task.Delay(System.TimeSpan.FromSeconds(delay));
            ToggleActive(poolKey, state, rotation, createNew);
        }

        /// <summary> Toggles active all found objects of given state </summary>
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static async void ToggleActiveAll(int poolKey, float delay, TPObjectState state, Vector3 position, Quaternion rotation)
        {
            await System.Threading.Tasks.Task.Delay(System.TimeSpan.FromSeconds(delay));
            ToggleActiveAll(poolKey, state, position, rotation);
        }

        /// <summary> Toggles active all found objects of given state </summary>
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static async void ToggleActiveAll(int poolKey, float delay, TPObjectState state, Vector3 position)
        {
            await System.Threading.Tasks.Task.Delay(System.TimeSpan.FromSeconds(delay));
            ToggleActiveAll(poolKey, state, position);
        }

        /// <summary> Toggles active all found objects of given state </summary>
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static async void ToggleActiveAll(int poolKey, float delay, TPObjectState state, Quaternion rotation)
        {
            await System.Threading.Tasks.Task.Delay(System.TimeSpan.FromSeconds(delay));
            ToggleActiveAll(poolKey, state, rotation);
        }
#endif

        /// <summary> Returns length of all objects in state from its pool </summary>
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
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static bool HasAnyObject(int poolKey, TPObjectState state = TPObjectState.Auto)
        {
            return Length(poolKey, state) > 0;
        }

        /// <summary> Clears pool from deactive (unused) objects </summary>
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static void RemoveUnused(int poolKey, bool destroyObjects = true)
        {
            if (SafeKey(poolKey))
                pool[poolKey].RemoveUnused(destroyObjects);
        }

        /// <summary> Clears whole pool. </summary>
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
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static void Dispose(bool destroyObjects = true)
        {
            foreach (var pair in pool)
                Dispose(pair.Key, destroyObjects);
            pool.Clear();
        }

        /// <summary> Checks if given key(pool) exists </summary>
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
            return state == TPObjectState.Active ? true : false;
        }

        /// <summary> Returns true if poolObject has given state </summary>
        public static bool HasState(this GameObject poolObject, TPObjectState state)
        {
            return state == GetState(poolObject) || state == TPObjectState.Auto;
        }

        /// <summary> Sets active all objects in array </summary>
        [MethodImpl((MethodImplOptions)0x100)] // agressive inline
        public static void SetAllActive(this GameObject[] poolObjects, bool active)
        {
            int length = poolObjects.Length;
            for (int i = 0; i < length; i++)
                poolObjects[i].SetActive(active);
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

        /// <summary> This checks for existing key. Returns true if is safe </summary>  
        private static bool SafeObject(GameObject poolObject)
        {
            if (poolObject == null)
            {
                Debug.LogError("You're trying toggle active Null object!");
                return false;
            }
            return true;
        }

        /// <summary> This checks for existing key. Returns true if is safe </summary>  
        private static bool SafeObject(int poolKey, TPObjectState state, GameObject poolObject)
        {
            if (poolObject == null)
            {
                Debug.LogWarning("No objects with state " + state + " found. Pool key: " + poolKey);
                return false;
            }
            return true;
        }

    }
}