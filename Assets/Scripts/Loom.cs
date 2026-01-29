using UnityEngine;
using System.Collections.Generic;
using System;

public class Loom : MonoBehaviour
{
    private readonly static System.Object mLockObject = new object();
    static bool initialized;
    private static Loom _current;
    public static Loom Current
    {
        get
        {
            Initialize();
            return _current;
        }
    }

    void Awake()
    {
        _current = this;
        initialized = true;
    }

    public static void Initialize()
    {
        if (!initialized)
        {
            if (!Application.isPlaying)
                return;
            initialized = true;
            var g = new GameObject("Loom");
            g.hideFlags = HideFlags.NotEditable;
            DontDestroyOnLoad(g);
            _current = g.AddComponent<Loom>();
        }
    }

    private List<QueueCallItem> mLitThreadAction = new List<QueueCallItem>(30);
    private List<QueueCallItem> mLitCurrentAction = new List<QueueCallItem>(30);
    
    public struct QueueCallItem
    {
        public Action<object> Action;
        public object Parameter;
        public string mStrNotifyID;
    }

    public static void ClearQueueOnMainThread()
    {
        if(initialized && Current!=null)
            Current.mLitThreadAction.Clear();
    }

    public static void QueueOnMainThread(Action<object> action, object parameter)
    {
        if ((System.Object)Current == null)
            return;
        lock (mLockObject)
        {
            Current.mLitThreadAction.Add(new QueueCallItem()
            {
                Action = action,
                Parameter = parameter,
            });
        }
    }

    void OnDisable()
    {
        if (_current == this)
        {
            _current = null;
            initialized = false;
        }
    }


    private long startTime;
    private float useTime;
    // Update is called once per frame
    void Update()
    {
        lock (mLockObject)
        {
            mLitCurrentAction.Clear();
            List<QueueCallItem> litTemp = mLitThreadAction;
            mLitThreadAction = mLitCurrentAction;
            mLitCurrentAction = litTemp;
        }
        for (int i = 0, iLength = mLitCurrentAction.Count; i < iLength; i++)
        {
            QueueCallItem a = mLitCurrentAction[i];
            try
            {
                startTime = DateTime.Now.Ticks;
                a.Action(a.Parameter);
                useTime = (DateTime.Now.Ticks - startTime) / 1000000f;
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }
    }

}
