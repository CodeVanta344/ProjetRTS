using System.Collections.Generic;
using UnityEngine;

namespace NapoleonicWars.Core
{
    public class ObjectPool : MonoBehaviour
    {
        public static ObjectPool Instance { get; private set; }

        private Dictionary<string, Queue<GameObject>> pools = new Dictionary<string, Queue<GameObject>>();
        private Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
        private Dictionary<string, Transform> poolParents = new Dictionary<string, Transform>();
        private List<PendingReturn> pendingReturns = new List<PendingReturn>();

        private struct PendingReturn
        {
            public string poolId;
            public GameObject obj;
            public float returnTime;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void RegisterPool(string poolId, GameObject prefab, int initialSize)
        {
            if (pools.ContainsKey(poolId)) return;

            pools[poolId] = new Queue<GameObject>();
            prefabs[poolId] = prefab;

            GameObject parent = new GameObject($"Pool_{poolId}");
            parent.transform.SetParent(transform);
            poolParents[poolId] = parent.transform;

            for (int i = 0; i < initialSize; i++)
            {
                GameObject obj = CreateNewObject(poolId);
                obj.SetActive(false);
                pools[poolId].Enqueue(obj);
            }
        }

        public GameObject Get(string poolId)
        {
            if (!pools.ContainsKey(poolId)) return null;

            GameObject obj;
            if (pools[poolId].Count > 0)
            {
                obj = pools[poolId].Dequeue();
                if (obj == null)
                {
                    obj = CreateNewObject(poolId);
                }
            }
            else
            {
                obj = CreateNewObject(poolId);
            }

            obj.SetActive(true);
            return obj;
        }

        public void Return(string poolId, GameObject obj)
        {
            if (!pools.ContainsKey(poolId))
            {
                Destroy(obj);
                return;
            }

            obj.SetActive(false);
            obj.transform.SetParent(poolParents[poolId]);
            pools[poolId].Enqueue(obj);
        }

        public void ReturnDelayed(string poolId, GameObject obj, float delay)
        {
            if (obj == null) return;
            pendingReturns.Add(new PendingReturn
            {
                poolId = poolId,
                obj = obj,
                returnTime = Time.time + delay
            });
        }

        private void Update()
        {
            // Process pending returns without coroutine allocations
            float now = Time.time;
            for (int i = pendingReturns.Count - 1; i >= 0; i--)
            {
                if (pendingReturns[i].returnTime <= now)
                {
                    var pr = pendingReturns[i];
                    pendingReturns.RemoveAt(i);
                    if (pr.obj != null)
                        Return(pr.poolId, pr.obj);
                }
            }
        }

        private GameObject CreateNewObject(string poolId)
        {
            if (!prefabs.ContainsKey(poolId)) return null;

            GameObject obj = Instantiate(prefabs[poolId]);
            obj.transform.SetParent(poolParents[poolId]);
            return obj;
        }

        public void ClearPool(string poolId)
        {
            if (!pools.ContainsKey(poolId)) return;

            while (pools[poolId].Count > 0)
            {
                GameObject obj = pools[poolId].Dequeue();
                if (obj != null) Destroy(obj);
            }
        }

        public void ClearAllPools()
        {
            foreach (var kvp in pools)
            {
                while (kvp.Value.Count > 0)
                {
                    GameObject obj = kvp.Value.Dequeue();
                    if (obj != null) Destroy(obj);
                }
            }
            pools.Clear();
            prefabs.Clear();
        }
    }
}
