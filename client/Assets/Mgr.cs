using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public abstract class Mgr<T> : MonoBehaviour where T : class
{
    public static T Instance;


    protected virtual void Awake()
    {
        Instance = this as T;
    }

    protected virtual void OnDestroy()
    {
        //Instance = null;
    }

    public virtual void OnPostPatch()
    {
    }
}
