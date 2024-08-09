using System;
using UnityEngine;
#if ARCORE_USE_ARF_5
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
// XROrigin is deprecated in ARFoundation 5.x or above
// ARFoundation 5.x is required when version 2022.3 or
        GameObject go = null;
#if ARCORE_USE_ARF_5
        XROrigin or = GameObject.FindObjectOfType<XROrigin>();
        if(or == null)
        {
            throw new Exception("No XR Origin found");
        }
        go = or.gameObject;
#else
        ARSessionOrigin arSess = GameObject.FindObjectOfType<ARSessionOrigin>();
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