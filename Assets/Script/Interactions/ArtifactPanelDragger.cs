using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Enables an Artifact Detail Panel to be repositioned in world space by pinching and holding for a duration (default 1.5s - 3s).
/// Quick pinch taps continue to function normally for UI buttons without obstruction.
/// </summary>
public class ArtifactPanelDragger : XRSimpleInteractable
{
    [Header("Pinch and Hold Settings")]
    [Tooltip("Duration in seconds the player must pinch and hold on the panel to begin moving it around.")]
    public float holdToMoveDuration = 1.5f;

    private bool isPinching = false;
    private bool isMoving = false;
    private Coroutine holdCoroutine;

    protected override void Awake()
    {
        base.Awake();

        // Ensure Rigidbody is present and kinematic for 3D raycast interaction
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        // Ensure BoxCollider covers panel bounds for raycast targeting
        BoxCollider boxCol = GetComponent<BoxCollider>();
        if (boxCol == null) boxCol = gameObject.AddComponent<BoxCollider>();

        RectTransform rect = GetComponent<RectTransform>();
        if (rect != null)
        {
            Vector2 size = rect.rect.size;
            if (size.x <= 1f || size.y <= 1f) size = new Vector2(640f, 480f);
            boxCol.size = new Vector3(size.x, size.y, 10f);
            boxCol.center = new Vector3(rect.rect.center.x, rect.rect.center.y, 0f);
        }
        else
        {
            boxCol.size = new Vector3(0.64f, 0.48f, 0.02f);
            boxCol.center = Vector3.zero;
        }
    }

    protected override void OnSelectEntered(SelectEnterEventArgs args)
    {
        base.OnSelectEntered(args);

        isPinching = true;
        if (holdCoroutine != null) StopCoroutine(holdCoroutine);
        holdCoroutine = StartCoroutine(ProcessPinchHold(args.interactorObject));
    }

    protected override void OnSelectExited(SelectExitEventArgs args)
    {
        base.OnSelectExited(args);

        isPinching = false;
        if (holdCoroutine != null)
        {
            StopCoroutine(holdCoroutine);
            holdCoroutine = null;
        }
        isMoving = false;
    }

    private IEnumerator ProcessPinchHold(IXRSelectInteractor interactor)
    {
        float elapsed = 0f;
        Transform interactorTransform = (interactor != null && interactor.transform != null) ? interactor.transform : null;

        // Phase 1: Wait for pinch-and-hold duration (allows instant UI button taps to complete unaffected)
        while (isPinching && elapsed < holdToMoveDuration)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Phase 2: If still pinching after hold duration, activate Move Mode!
        if (isPinching && interactorTransform != null)
        {
            isMoving = true;
            Debug.Log($"[ArtifactPanelDragger] Pinch hold threshold ({holdToMoveDuration}s) reached! Moving panel with hand.");

            // Calculate initial local offset relative to the interactor ray
            Vector3 initialLocalPos = interactorTransform.InverseTransformPoint(transform.position);
            Quaternion initialLocalRot = Quaternion.Inverse(interactorTransform.rotation) * transform.rotation;

            while (isPinching && interactorTransform != null)
            {
                Vector3 targetPos = interactorTransform.TransformPoint(initialLocalPos);
                Quaternion targetRot = interactorTransform.rotation * initialLocalRot;

                transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * 20f);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 20f);

                yield return null;
            }

            isMoving = false;
            Debug.Log("[ArtifactPanelDragger] Pinch released. Detail panel fixed at new world location.");
        }
    }
}
