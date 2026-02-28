using System.Collections.Generic;
using UnityEngine;

public sealed class Pool<T> where T : Component
{
    private readonly T prefab;
    private readonly Transform parent;
    private readonly Stack<T> available;
    private readonly List<T> allInstances;

    public int Capacity => allInstances.Count;
    public int AvailableCount => available.Count;

    public Pool(T prefab, int capacity, Transform parent)
    {
        this.prefab = prefab;
        this.parent = parent;
        int safeCapacity = Mathf.Max(1, capacity);

        available = new Stack<T>(safeCapacity);
        allInstances = new List<T>(safeCapacity);

        for (int i = 0; i < safeCapacity; i++)
        {
            T instance = CreateInstance();
            available.Push(instance);
        }
    }

    public bool TryGet(out T instance)
    {
        if (available.Count == 0)
        {
            instance = null;
            return false;
        }

        instance = available.Pop();
        instance.gameObject.SetActive(true);
        return true;
    }

    public void Release(T instance)
    {
        if (instance == null)
        {
            return;
        }

        instance.transform.SetParent(parent, false);
        instance.gameObject.SetActive(false);
        available.Push(instance);
    }

    public void ReleaseAll(T[] activeItems, ref int activeCount)
    {
        for (int i = 0; i < activeCount; i++)
        {
            Release(activeItems[i]);
            activeItems[i] = null;
        }

        activeCount = 0;
    }

    private T CreateInstance()
    {
        T instance = Object.Instantiate(prefab, parent);
        instance.gameObject.SetActive(false);
        allInstances.Add(instance);
        return instance;
    }
}
