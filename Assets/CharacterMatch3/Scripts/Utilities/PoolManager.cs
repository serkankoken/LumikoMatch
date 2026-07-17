using System.Collections.Generic;
using UnityEngine;

namespace CharacterMatch3.Utilities
{
    public sealed class PoolManager : MonoBehaviour
    {
        private readonly Dictionary<GameObject, Queue<GameObject>> pools = new Dictionary<GameObject, Queue<GameObject>>();

        public GameObject Get(GameObject prefab, Transform parent)
        {
            if (prefab == null)
            {
                return null;
            }

            if (!pools.TryGetValue(prefab, out var queue))
            {
                queue = new Queue<GameObject>();
                pools[prefab] = queue;
            }

            var instance = queue.Count > 0 ? queue.Dequeue() : Instantiate(prefab);
            instance.transform.SetParent(parent, false);
            instance.SetActive(true);
            return instance;
        }

        public void Release(GameObject prefab, GameObject instance)
        {
            if (prefab == null || instance == null)
            {
                return;
            }

            if (!pools.TryGetValue(prefab, out var queue))
            {
                queue = new Queue<GameObject>();
                pools[prefab] = queue;
            }

            instance.SetActive(false);
            instance.transform.SetParent(transform, false);
            queue.Enqueue(instance);
        }
    }
}
