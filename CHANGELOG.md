# TPObjectPool ChangeLog

## 04/07/2018

- Changed AddToPool to CreatePool - now it only creates pool
- Changed a lot of signatures
- Removed activation delegates
- Added more toggle helpers
- More safety

## 02/07/2018

- Completely rewritten TPObjectPool. It uses stacks:
- More performant
- GC free
- Better architecture
- Safety
- Added Pool Collection - TPPoolContainer - it's based on two stacks (active objects and deactive objects)
- Added non public TPObjectPooler - MonoBehaviour - it's replacement for Unity's coroutines to deal with ToggleActive with delay without creating GC
### API changes:
- Pool key is now Integer instead of String
- Every method changed to define state e.g FreeLength/BusyLength is just Length(TPObjectState.Active/TPObjectState.Deactive)
- ToggleActive() instead of SetActive() - now you set activation by toggling given state of object
- ToggleActiveAll() instead of SetActiveAll()
- PushObject() will push object directly to TPPoolContainer without instantiating new object
- PopObject() will return object AND remove it from pool
- PopObjects() will return all objects AND remove them from pool
- Peek() will return object without removing it from pool
- RemoveUnused() will remove all deactivated objects from pool
- Dispose() has parameter allows you to control if objects should be destroyed
- Extenstion: GameObject.GetState() will return state of object
- Extenstion: GameObject.HasState(state) will check if object is of given state
- Extenstion: TPObjectState.ActiveSelf() converts from State to Bool (GameObject.activeSelf)
- Extenstion: GameObject[].SetActive() will set activation to all objects in array
