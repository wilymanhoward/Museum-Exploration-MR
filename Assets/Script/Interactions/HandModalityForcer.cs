using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs;

/// <summary>
/// Forces hands-only interaction and, crucially, makes itself the SINGLE authority
/// over the hand/controller interactor GameObjects.
///
/// XRI's built-in <see cref="XRInputModalityManager"/> toggles these same objects
/// on/off based on which devices are tracked. If it stays enabled while this script
/// also drives them, the two fight over SetActive every frame, which rapidly
/// enables/disables the ray interactor and makes the hand ray glitch/twitch fast.
/// So we disable that manager up front and keep hands on / controllers off ourselves.
/// </summary>
public class HandModalityForcer : MonoBehaviour
{
    [Header("Controllers")]
    public GameObject leftController;
    public GameObject rightController;

    [Header("Hands")]
    public GameObject leftHand;
    public GameObject rightHand;

    private void Awake()
    {
        // Stop the built-in modality manager from contending for the same objects.
        // It normally lives on the XR Origin (the object this component is added to);
        // fall back to parents/children so this works regardless of exact placement.
        var modalityManager = GetComponent<XRInputModalityManager>();
        if (modalityManager == null)
            modalityManager = GetComponentInParent<XRInputModalityManager>();
        if (modalityManager == null)
            modalityManager = GetComponentInChildren<XRInputModalityManager>(true);
        if (modalityManager != null)
            modalityManager.enabled = false;

        ApplyHandsOnly();
    }

    private void Update()
    {
        // Safety net only. With the modality manager disabled nothing else contends,
        // so these are no-ops after the first frame (no per-frame SetActive churn).
        ApplyHandsOnly();
    }

    private void ApplyHandsOnly()
    {
        if (leftController != null && leftController.activeSelf)
            leftController.SetActive(false);
        if (rightController != null && rightController.activeSelf)
            rightController.SetActive(false);
        if (leftHand != null && !leftHand.activeSelf)
            leftHand.SetActive(true);
        if (rightHand != null && !rightHand.activeSelf)
            rightHand.SetActive(true);

        // Disable any extra controller gameobjects in rig hierarchy
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            if (t != null && t.name.Contains("Controller") && !t.name.Contains("Hand"))
            {
                if (t.gameObject.activeSelf) t.gameObject.SetActive(false);
            }
        }

        // Deduplicate rays: ensure ONLY 1 ray line is active per hand
        DeduplicateHandRays(leftHand);
        DeduplicateHandRays(rightHand);
    }

    /// <summary>
    /// Ensures only 1 primary hand ray line visual is active under the hand hierarchy,
    /// turning off any duplicate or secondary ray lines.
    /// </summary>
    private void DeduplicateHandRays(GameObject handObj)
    {
        if (handObj == null) return;

        var lineVisuals = handObj.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.XRInteractorLineVisual>(true);
        if (lineVisuals != null && lineVisuals.Length > 1)
        {
            // Keep the first primary ray visual active, disable all duplicate secondary ray lines!
            for (int i = 1; i < lineVisuals.Length; i++)
            {
                if (lineVisuals[i] != null)
                {
                    lineVisuals[i].enabled = false;
                    var lr = lineVisuals[i].GetComponent<LineRenderer>();
                    if (lr != null) lr.enabled = false;
                }
            }
        }

        var lineRenderers = handObj.GetComponentsInChildren<LineRenderer>(true);
        if (lineRenderers != null && lineRenderers.Length > 1)
        {
            LineRenderer primary = (lineVisuals != null && lineVisuals.Length > 0) ? lineVisuals[0].GetComponent<LineRenderer>() : lineRenderers[0];
            for (int i = 0; i < lineRenderers.Length; i++)
            {
                if (lineRenderers[i] != null && lineRenderers[i] != primary)
                {
                    lineRenderers[i].enabled = false;
                }
            }
        }
    }
}
