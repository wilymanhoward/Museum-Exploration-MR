#if XR_HANDS_1_2_OR_NEWER
using Unity.XR.CoreUtils;
using Unity.XR.CoreUtils.Bindings;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit.Utilities.Tweenables.Primitives;
#endif

namespace UnityEngine.XR.Interaction.Toolkit.Samples.Hands
{
    /// <summary>
    /// A class that follows the pinch point between the thumb and index finger using XR Hand Tracking. 
    /// It updates its position to the midpoint between the thumb and index tip while optionally adjusting its rotation 
    /// to look at a specified target. The rotation towards the target can also be smoothly interpolated over time.
    /// </summary>
    public class PinchPointFollow : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The XR Hand Tracking Events component that will be used to subscribe to hand tracking events.")]
#if XR_HANDS_1_2_OR_NEWER
        XRHandTrackingEvents m_XRHandTrackingEvents;
#else
        Object m_XRHandTrackingEvents;
#endif

        [SerializeField]
        [Tooltip("The transform to match the rotation of.")]
        Transform m_TargetRotation;

        [SerializeField]
        [Tooltip("The transform will use the XRRayInteractor endpoint position to calculate the transform rotation.")]
        XRRayInteractor m_RayInteractor;

        [SerializeField]
        [Tooltip("How fast to match rotation (0 means no rotation smoothing.)")]
        [Range(0f, 32f)]
        float m_RotationSmoothingSpeed = 12f;

#if XR_HANDS_1_2_OR_NEWER
        bool m_HasRayInteractor;
        bool m_HasTargetRotationTransform;

        OneEuroFilterVector3 m_OneEuroFilterVector3;
#endif

        /// <summary>
        /// See <see cref="MonoBehaviour"/>.
        /// </summary>
        void OnEnable()
        {
#if XR_HANDS_1_2_OR_NEWER
            if (m_XRHandTrackingEvents != null)
                m_XRHandTrackingEvents.jointsUpdated.AddListener(OnJointsUpdated);

            m_OneEuroFilterVector3 = new OneEuroFilterVector3(transform.localPosition);
            m_HasRayInteractor = m_RayInteractor != null;
            m_HasTargetRotationTransform = m_TargetRotation != null;
#else
            Debug.LogWarning("PinchPointFollow requires XR Hands (com.unity.xr.hands) 1.2.0 or newer. Disabling component.", this);
            enabled = false;
#endif
        }

        /// <summary>
        /// See <see cref="MonoBehaviour"/>.
        /// </summary>
        void OnDisable()
        {
#if XR_HANDS_1_2_OR_NEWER
            if (m_XRHandTrackingEvents != null)
                m_XRHandTrackingEvents.jointsUpdated.RemoveListener(OnJointsUpdated);
#endif
        }

#if XR_HANDS_1_2_OR_NEWER
        void OnJointsUpdated(XRHandJointsUpdatedEventArgs args)
        {
            // Left empty to prevent mid-frame transform competition with native XRI / Meta OpenXR Hand Rays.
            // Native XRI Hand Tracking drives ray origin positions and rotations smoothly without frame jitter.
        }
#endif
    }
}
