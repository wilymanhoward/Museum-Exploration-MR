using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// Centralized Haptics Manager for UI button interactions in VR.
/// Provides distinct buzz intensities for hover and click events.
/// </summary>
public static class XRButtonHaptics
{
    // Hover: Subtle, snappy, gentle tick
    public const float HoverAmplitude = 0.22f;
    public const float HoverDuration = 0.035f;

    // Click: Firm, punchy, distinct mechanical click
    public const float ClickAmplitude = 0.65f;
    public const float ClickDuration = 0.08f;

    private static float s_LastHoverTime = -10f;
    private static GameObject s_LastHoveredObject;
    private const float HoverDebounceSeconds = 0.04f;

    private static float s_LastClickTime = -10f;
    private const float ClickDebounceSeconds = 0.10f;

    /// <summary>
    /// Trigger hover buzz when the ray/cursor enters a button.
    /// </summary>
    public static void TriggerHover(object source = null, GameObject target = null)
    {
        if (target != null && target == s_LastHoveredObject && Time.unscaledTime - s_LastHoverTime < 0.2f)
            return;

        if (Time.unscaledTime - s_LastHoverTime < HoverDebounceSeconds)
            return;

        s_LastHoverTime = Time.unscaledTime;
        s_LastHoveredObject = target;

        SendHaptics(source, target, HoverAmplitude, HoverDuration);
    }

    /// <summary>
    /// Trigger click buzz when a button is clicked or pressed.
    /// </summary>
    public static void TriggerClick(object source = null, GameObject target = null)
    {
        if (Time.unscaledTime - s_LastClickTime < ClickDebounceSeconds)
            return;

        s_LastClickTime = Time.unscaledTime;

        SendHaptics(source, target, ClickAmplitude, ClickDuration);
    }

    private static void SendHaptics(object source, GameObject target, float amplitude, float duration)
    {
        // 1. Try to extract controller from TrackedDeviceEventData
        if (source is TrackedDeviceEventData trackedData && trackedData.interactor != null)
        {
            if (SendToInteractor(trackedData.interactor, amplitude, duration))
                return;
        }

        // 2. Try to extract controller from IXRInteractor / XRBaseControllerInteractor
        if (source is IXRInteractor interactor)
        {
            if (SendToInteractor(interactor, amplitude, duration))
                return;
        }

        // 3. Try to find active XRRayInteractor currently pointing at the target
        if (target != null)
        {
            XRRayInteractor[] rays = Object.FindObjectsOfType<XRRayInteractor>();
            for (int i = 0; i < rays.Length; i++)
            {
                var ray = rays[i];
                if (ray == null || !ray.isActiveAndEnabled) continue;

                if (ray.TryGetCurrentUIRaycastResult(out RaycastResult uiRes) && uiRes.isValid)
                {
                    if (uiRes.gameObject == target || 
                        uiRes.gameObject.transform.IsChildOf(target.transform) || 
                        target.transform.IsChildOf(uiRes.gameObject.transform))
                    {
                        if (SendToInteractor(ray, amplitude, duration))
                            return;
                    }
                }

                if (ray.TryGetCurrent3DRaycastHit(out RaycastHit hit3D))
                {
                    if (hit3D.collider != null && (hit3D.collider.gameObject == target || hit3D.collider.transform.IsChildOf(target.transform)))
                    {
                        if (SendToInteractor(ray, amplitude, duration))
                            return;
                    }
                }
            }
        }

        // 4. Fallback: send directly to the active XR controller devices via InputDevices
        SendToActiveXRDevices(amplitude, duration);
    }

    private static bool SendToInteractor(object interactorObj, float amplitude, float duration)
    {
        if (interactorObj is XRBaseControllerInteractor ctrlInteractor)
        {
            if (ctrlInteractor.xrController != null)
            {
                ctrlInteractor.xrController.SendHapticImpulse(amplitude, duration);
                return true;
            }
            ctrlInteractor.SendHapticImpulse(amplitude, duration);
            return true;
        }
        else if (interactorObj is Component comp)
        {
            var ctrlInt = comp.GetComponentInParent<XRBaseControllerInteractor>() ?? comp.GetComponentInChildren<XRBaseControllerInteractor>();
            if (ctrlInt != null)
            {
                if (ctrlInt.xrController != null)
                {
                    ctrlInt.xrController.SendHapticImpulse(amplitude, duration);
                    return true;
                }
                ctrlInt.SendHapticImpulse(amplitude, duration);
                return true;
            }
        }
        return false;
    }

    private static void SendToActiveXRDevices(float amplitude, float duration)
    {
        bool sent = false;

        var rightHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (rightHand.isValid && rightHand.TryGetHapticCapabilities(out var capsR) && capsR.supportsImpulse)
        {
            rightHand.SendHapticImpulse(0, amplitude, duration);
            sent = true;
        }

        var leftHand = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        if (leftHand.isValid && leftHand.TryGetHapticCapabilities(out var capsL) && capsL.supportsImpulse)
        {
            leftHand.SendHapticImpulse(0, amplitude, duration);
            sent = true;
        }

        if (!sent)
        {
            var deviceList = new List<InputDevice>();
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller, deviceList);
            for (int i = 0; i < deviceList.Count; i++)
            {
                if (deviceList[i].isValid && deviceList[i].TryGetHapticCapabilities(out var caps) && caps.supportsImpulse)
                {
                    deviceList[i].SendHapticImpulse(0, amplitude, duration);
                }
            }
        }
    }
}
