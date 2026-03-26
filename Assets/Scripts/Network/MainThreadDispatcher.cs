using System;
using System.Collections.Generic;
using UnityEngine;

public class MainThreadDispatcher : MonoBehaviour
{
    private static readonly Queue<Action> queue = new Queue<Action>();
    private static MainThreadDispatcher instance;

    public static void Enqueue(Action action)
    {
        lock (queue) queue.Enqueue(action);
    }

    void Awake()
    {
        if (instance == null) { instance = this; DontDestroyOnLoad(gameObject); }
        else Destroy(gameObject);
    }

    void Update()
    {
        lock (queue)
        {
            while (queue.Count > 0)
                queue.Dequeue()?.Invoke();
        }
    }

    // Αυτόματη δημιουργία αν δεν υπάρχει ήδη στη σκηνή
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (instance != null) return;
        GameObject go = new GameObject("MainThreadDispatcher");
        go.AddComponent<MainThreadDispatcher>();
    }
}
