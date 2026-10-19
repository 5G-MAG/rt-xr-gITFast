/*
* Carries an anchored scene.
* AR Foundation destroys the object of an anchor that ARCore drops (for example after tracking was lost), together with everything
* under it. The scene therefore lives on this holder, which copies the pose of the anchor while the anchor is tracked, and asks for
* a new anchor at its last pose when the old one is gone.
*/

using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
#endif

namespace GLTFast
{
#if UNITY_ANDROID
    public class AnchorHolder : MonoBehaviour
    {
        private ARAnchor m_Anchor;
        private System.Func<bool> m_OnLost;
        private float m_RetryAt;

        /// <summary>Follows <paramref name="anchor"/>; <paramref name="onLost"/> creates a new anchor and returns true when it succeeded.</summary>
        public void Bind(ARAnchor anchor, System.Func<bool> onLost)
        {
            m_Anchor = anchor;
            m_OnLost = onLost;
            m_RetryAt = 0f;
            if (anchor != null)
            {
                transform.SetPositionAndRotation(anchor.transform.position, anchor.transform.rotation);
            }
        }

        void LateUpdate()
        {
            if (m_Anchor == null)
            {
                if (m_OnLost != null && Time.unscaledTime >= m_RetryAt)
                {
                    m_RetryAt = Time.unscaledTime + 1f;
                    m_OnLost();
                }
                return;
            }
            if (m_Anchor.trackingState != TrackingState.None)
            {
                transform.SetPositionAndRotation(m_Anchor.transform.position, m_Anchor.transform.rotation);
            }
        }
    }
#endif
}
