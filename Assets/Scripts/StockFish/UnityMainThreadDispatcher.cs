using System.Collections.Concurrent;
using System;
using UnityEngine;

/// <summary>
/// UnityMainThreadDispatcher is a MonoBehaviour that allows you to enqueue actions to be executed on the Unity main thread.
/// This is useful for scenarios where you need to perform actions that must run on the main thread, such as UI updates or game logic that interacts with Unity's API.
/// </summary>
public class UnityMainThreadDispatcher : MonoBehaviour
{
    /// <summary>
    /// A thread-safe queue to hold actions that need to be executed on the main thread.
    /// This queue is used to store actions that are enqueued from other threads and will be processed in the Update method of this MonoBehaviour.
    /// </summary>
    private static readonly ConcurrentQueue<Action> actions = new ConcurrentQueue<Action>();

    /// <summary>
    /// Enqueues an action to be executed on the Unity main thread.
    /// This method allows you to add an action to the queue, which will be processed in the Update method of this MonoBehaviour.
    /// </summary>
    /// <param name="action"></param>
    public static void Enqueue(Action action)
    {
        actions.Enqueue(action);
    }

    /// <summary>
    /// Update is called once per frame.
    /// This method processes all actions in the queue by dequeuing them and invoking each action.
    /// </summary>
    void Update()
    {
        while (actions.TryDequeue(out var action))
        {
            action.Invoke();
        }
    }
}

