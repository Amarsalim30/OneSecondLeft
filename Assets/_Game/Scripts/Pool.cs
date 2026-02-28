using System.Collections.Generic;
using UnityEngine;

public sealed class Pool<T> where T : Component
{
    private readonly T prefab;
    private readonly Transform parent;
    private readonly Stack<T> available;
    private readonly HashSet<T> availableLookup;
    private readonly List<T> allInstances;
    private readonly HashSet<T> allInstancesLookup;
    private bool duplicateReleaseWarningLogged;

    public int Capacity => allInstances.Count;
    public int AvailableCount => available.Count;

    public Pool(T prefab, int capacity, Transform parent)
    {
        this.prefab = prefab;
        this.parent = parent;
        int safeCapacity = Mathf.Max(1, capacity);

        available = new Stack<T>(safeCapacity);
        availableLookup = new HashSet<T>();
        allInstances = new List<T>(safeCapacity);
        allInstancesLookup = new HashSet<T>();

        for (int i = 0; i < safeCapacity; i++)
        {
            T instance = CreateInstance();
            available.Push(instance);
            availableLookup.Add(instance);
        }
    }

    public bool TryGet(out T instance)
    {
        while (available.Count > 0)
        {
            instance = available.Pop();
            if (instance == null)
            {
                continue;
            }

            if (!availableLookup.Remove(instance))
            {
                LogDuplicateReleaseWarning(instance);
                continue;
            }

            instance.gameObject.SetActive(true);
            return true;
        }

        if (availableLookup.Count > 0)
        {
            availableLookup.Clear();
        }

        instance = null;
        return false;
    }

    public void Release(T instance)
    {
        if (instance == null)
        {
            return;
        }

        if (!allInstancesLookup.Contains(instance))
        {
            return;
        }

        if (!availableLookup.Add(instance))
        {
            LogDuplicateReleaseWarning(instance);
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
        allInstancesLookup.Add(instance);
        return instance;
    }

    private void LogDuplicateReleaseWarning(T instance)
    {
        if (duplicateReleaseWarningLogged)
        {
            return;
        }

        duplicateReleaseWarningLogged = true;
        string instanceName = instance != null ? instance.name : "null";
        Debug.LogWarning($"Pool<{typeof(T).Name}> ignored duplicate release for '{instanceName}'.");
    }
}
