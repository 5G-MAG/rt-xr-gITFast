// SPDX-FileCopyrightText: 2024 Unity Technologies and the glTFast authors
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace GLTFast.Export
{

    /// <summary>
    /// Wraps a managed TIn[] in a NativeArray&lt;TOut&gt;without copying memory.
    /// </summary>
    /// <typeparam name="TIn">Type of items in input array.</typeparam>
    /// <typeparam name="TOut">Type of items in output NativeArray (might differ from input type TIn).</typeparam>
    public class ManagedNativeArray<TIn, TOut> : IDisposable
        where TIn : unmanaged
        where TOut : unmanaged
    {

        NativeArray<TOut> m_NativeArray;
        GCHandle m_BufferHandle;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        AtomicSafetyHandle m_SafetyHandle;
#endif
        bool m_Pinned;

        /// <summary>
        /// Wraps a managed T[] in a NativeArray&lt;T&gt;without copying memory.
        /// </summary>
        /// <param name="original">The original T[] to convert into a NativeArray&lt;T&gt;</param>
        public unsafe ManagedNativeArray(TIn[] original)
        {
            if (original != null)
            {
                m_BufferHandle = GCHandle.Alloc(original, GCHandleType.Pinned);
                fixed (void* bufferAddress = &original[0])
                {
                    m_NativeArray = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<TOut>(bufferAddress, original.Length, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    m_SafetyHandle = AtomicSafetyHandle.Create();
                    NativeArrayUnsafeUtility.SetAtomicSafetyHandle(array: ref m_NativeArray, m_SafetyHandle);
#endif
                }

                m_Pinned = true;
            }
            else
            {
                m_NativeArray = new NativeArray<TOut>();
            }
        }

        /// <summary>
        /// Points to the managed NativeArray&lt;T&gt;.
        /// </summary>
        public NativeArray<TOut> nativeArray => m_NativeArray;


        /// <summary>
        /// Disposes the managed NativeArray&lt;TT&gt;.
        /// </summary>
        public void Dispose()
        {
            if (m_Pinned)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.Release(m_SafetyHandle);
#endif
                m_BufferHandle.Free();
            }
        }
    }
}
