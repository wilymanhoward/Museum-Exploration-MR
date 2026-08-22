using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEngine.EventSystems;

public static class WristWatchFilterUtility
{
    public static bool IsLeftHand(IXRInteractor interactor, BaseEventData eventData = null)
    {
        if (interactor != null)
        {
            Component comp = interactor as Component;
            if (comp != null && IsTransformLeftHand(comp.transform)) return true;
            string interactorName = interactor.ToString().ToLower();
            if (interactorName.Contains("left") && !interactorName.Contains("right")) return true;
        }

        if (eventData != null)
        {
            if (eventData is TrackedDeviceEventData trackedData && trackedData.interactor != null)
            {
                Component comp = trackedData.interactor as Component;
                if (comp != null && IsTransformLeftHand(comp.transform)) return true;
                string interactorName = trackedData.interactor.ToString().ToLower();
                if (interactorName.Contains("left") && !interactorName.Contains("right")) return true;
            }

            PointerEventData ptrData = eventData as PointerEventData;
            if (ptrData != null && ptrData.pressEventCamera != null && IsTransformLeftHand(ptrData.pressEventCamera.transform))
            {
                return true;
            }
        }

        // Distance check fallback: check if interactor position is close to left hand anchor
        if (WristWatch.Instance != null && WristWatch.Instance.leftHandAnchor != null)
        {
            Vector3 interactorPos = Vector3.zero;
            if (interactor is Component c) interactorPos = c.transform.position;
            else if (eventData is PointerEventData pData && pData.pressEventCamera != null) interactorPos = pData.pressEventCamera.transform.position;

            if (interactorPos != Vector3.zero)
            {
                float dist = Vector3.Distance(interactorPos, WristWatch.Instance.leftHandAnchor.position);
                if (dist < 0.18f) return true;
            }
        }

        return false;
    }

    private static bool IsTransformLeftHand(Transform t)
    {
        while (t != null)
        {
            string n = t.name.ToLower();
            if (n.Contains("left") && !n.Contains("right")) return true;
            t = t.parent;
        }
        return false;
    }
}

public class WristWatchButtonGuard : MonoBehaviour, IPointerDownHandler, IPointerClickHandler, IPointerEnterHandler
{
    public void OnPointerDown(PointerEventData eventData)
    {
        if (WristWatch.Instance != null && WristWatch.Instance.IsWatchButtonHidden())
        {
            eventData.pointerPress = null;
            eventData.rawPointerPress = null;
            return;
        }
        if (WristWatchFilterUtility.IsLeftHand(null, eventData))
        {
            eventData.pointerPress = null;
            eventData.rawPointerPress = null;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (WristWatch.Instance != null && WristWatch.Instance.IsWatchButtonHidden())
        {
            eventData.pointerPress = null;
            eventData.rawPointerPress = null;
            return;
        }
        if (WristWatchFilterUtility.IsLeftHand(null, eventData))
        {
            eventData.pointerPress = null;
            eventData.rawPointerPress = null;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (WristWatch.Instance != null && WristWatch.Instance.IsWatchButtonHidden())
        {
            return;
        }
        if (WristWatchFilterUtility.IsLeftHand(null, eventData))
        {
            // Do not highlight on left hand hover
        }
    }
}
