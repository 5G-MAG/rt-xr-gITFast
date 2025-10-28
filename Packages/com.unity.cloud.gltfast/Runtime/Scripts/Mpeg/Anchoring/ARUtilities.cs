using System;
using UnityEngine;
#if UNITY_2022_3_OR_NEWER
    using Unity.XR.CoreUtils;
#else
    using UnityEngine.XR.ARFoundation;
#endif

/// <summary>
/// Utility class to handle version references
/// and a few handy methods 
/// </summary>
public static class ARUtilities
{
    /// <summary>
    /// Returns the XR Origin's GameObject of the scene
    /// throw an exception if it doesn't exists
    /// </summary>
    /// <returns></returns>
    public static GameObject GetSessionOrigin()
    {
        GameObject go = null;
        XROrigin or = GameObject.FindFirstObjectByType<XROrigin>(FindObjectsInactive.Include); 
        if(or == null)
        {
            throw new Exception("No XR Origin found");
        }
        go = or.gameObject;
        if(go == null)
        {
            throw new Exception("Can't initialize Trackable marker geo, no origin found");
        }

        return go;
    }
}