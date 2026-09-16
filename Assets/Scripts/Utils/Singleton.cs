using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    protected Singleton()
    {
    }

    protected static T _instance;

    // Set once OnApplicationQuit fires (which Unity broadcasts to every active object before it
    // starts tearing down the scene - including when Stopping the Player in the Editor). Guards
    // GetInstance() below so a listener whose OnDisable/OnDestroy runs after this singleton's own
    // OnDestroy (teardown order across objects isn't guaranteed) can't resurrect it as a fresh,
    // half-initialized "[singleton] " GameObject that then spams "not assigned" errors.
    private static bool _isQuitting;
    protected static bool IsQuitting => _isQuitting;

    public static T Instance
    {
        get
        {
            if (_instance == null && !_isQuitting)
            {
                _instance = GetInstance();
            }

            return _instance;
        }
    }

    public static T GetInstance()
    {
        if (_isQuitting)
        {
            return null;
        }

        _instance = FindAnyObjectByType<T>();
        if (_instance == null)
        {
            GameObject singleton = new GameObject();
            _instance = singleton.AddComponent<T>();
            singleton.name = "[singleton] " + typeof(T).ToString();
        }

        return _instance;
    }

    protected virtual void Awake()
    {
        if (_instance != null && _instance.gameObject != gameObject)
        {
            Destroy(gameObject);
        }
        else
        {
            _instance = (T)(object)this;
        }

        Initialize();
    }

    protected virtual void OnApplicationQuit()
    {
        _isQuitting = true;
    }

    protected virtual void OnDestroy()
    {
        if (_instance == (T)(object)this)
        {
            _instance = null;
        }
    }

    /// <summary>
    /// Initialization override
    /// </summary>
    protected virtual void Initialize()
    {
    }
}