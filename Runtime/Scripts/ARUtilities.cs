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
    /// Returns the XR Origin or the XROrigin in the scene
    /// throw an exception if it doesn't exists
    /// </summary>
    /// <returns></returns>
    public static GameObject GetSessionOrigin()
    {
// XROrigin is deprecated in version 2022.3 or above
        GameObject go = null;
#if UNITY_2022_3_OR_NEWER
        XROrigin or = GameObject.FindObjectOfType<XROrigin>();
        if(or == null)
        {
            throw new Exception("No XR Origin found");
        }
        go = or.gameObject;
#else
        XROrigin arSess = GameObject.FindObjectOfType<XROrigin>();
        if(arSess == null)
        {
            throw new Exception("No AR Session origin found");
        }
        go = arSess.gameObject;
#endif
        if(go == null)
        {
            throw new Exception("Can't initialize Trackable marker geo, no origin found");
        }

        return go;
    }
}