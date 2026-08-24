using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Meta.XR.MRUtilityKit;

/// <summary>
/// Enables an Artifact Detail Panel to be repositioned in world space by pinching and holding for a duration (default 1.0s).
/// Quick pinch taps continue to function normally for UI buttons without obstruction.
///
/// While dragging, the panel also magnetically snaps flush onto nearby real-world walls
/// using Meta's MRUK room scan (the same system QRCodeScanner already relies on) - see
/// TryGetWallSnap. No wall data (no room scan yet, or running in the Editor without a
/// live XR session) simply means dragging behaves as before: free placement, no snap.
/// </summary>
public class ArtifactPanelDragger : XRSimpleInteractable, IPointerDownHandler, IPointerUpHandler, IDragHandler, IPointerExitHandler
{
    [Header("Pinch and Hold Settings")]
    [Tooltip("Duration in seconds the player must pinch and hold on the panel to begin moving it around.")]
    public float holdToMoveDuration = 1.0f;

    [Header("Wall Snapping (Meta MRUK)")]
    [Tooltip("While dragging, magnetically snap the panel flush onto a nearby real-world wall (requires a completed Meta room scan). If no scan/wall data is available, dragging just behaves as free placement.")]
    public bool snapToWalls = true;
    [Tooltip("Distance from a wall, in meters, within which the panel starts snapping to it.")]
    public float wallSnapRadius = 0.35f;
    [Tooltip("Gap kept between the panel and the wall surface so it doesn't clip into the wall mesh, in meters.")]
    public float wallSnapOffset = 0.02f;

    private static readonly LabelFilter WallOnlyFilter = new LabelFilter(MRUKAnchor.SceneLabels.WALL_FACE);

    private bool isPinching = false;
    private bool isMoving = false;
    private bool isUserMoved = false;
    private bool isSnappedToWall = false;
    private Coroutine holdCoroutine;

    public bool IsMoving => isMoving;
    public bool IsUserMoved => isUserMoved;
    public bool IsSnappedToWall => isSnappedToWall;

    public void ResetUserMoved()
    {
        isUserMoved = false;
        isMoving = false;
        isPinching = false;
        isSnappedToWall = false;
        if (holdCoroutine != null)
        {
            StopCoroutine(holdCoroutine);
            holdCoroutine = null;
        }
    }

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

        // Ensure Canvas background image receives UI raycasts
        UnityEngine.UI.Image img = GetComponent<UnityEngine.UI.Image>();
        if (img != null)
        {
            img.raycastTarget = true;
        }
    }

    protected override void OnSelectEntered(SelectEnterEventArgs args)
    {
        base.OnSelectEntered(args);
        Transform interactorT = args.interactorObject != null ? args.interactorObject.transform : null;

        // This selection path comes from the 3D collider (XRSimpleInteractable), which has no
        // PointerEventData of its own - ask the ray interactor what it's CURRENTLY over in UI
        // space too, so a pinch that started on a scrollable area (e.g. the description text)
        // is recognized here exactly like it is via OnPointerDown below.
        GameObject uiHitObj = null;
        if (args.interactorObject is XRRayInteractor rayInteractor && rayInteractor.TryGetCurrentUIRaycastResult(out RaycastResult uiResult))
        {
            uiHitObj = uiResult.gameObject;
        }

        StartPinchHold(interactorT, uiHitObj);
    }

    protected override void OnSelectExited(SelectExitEventArgs args)
    {
        base.OnSelectExited(args);
        StopPinchHold();
    }

    #region UI Pointer Handlers (For Graphic Raycasting via TrackedDeviceGraphicRaycaster)
    public void OnPointerDown(PointerEventData eventData)
    {
        Transform interactorT = ExtractInteractorTransform(eventData);
        GameObject hitObj = eventData != null ? eventData.pointerCurrentRaycast.gameObject : null;
        StartPinchHold(interactorT, hitObj);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        StopPinchHold();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // Only stop if we are still waiting in Phase 1 (not currently moving)
        if (!isMoving)
        {
            StopPinchHold();
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        // Interface required so Unity EventSystem routes hold drag events to this panel
    }
    #endregion

    private bool IsTargeting3DArtifactModel(GameObject hitObj)
    {
        if (hitObj != null)
        {
            RotateArtifact rotator = hitObj.GetComponent<RotateArtifact>() ?? hitObj.GetComponentInParent<RotateArtifact>();
            if (rotator != null && rotator.IsBeingRotated)
            {
                return true;
            }

            // Only suppress if directly pinching a 3D mesh object spawned inside ObjectSpawner
            if (hitObj.GetComponent<MeshRenderer>() != null || hitObj.GetComponent<MeshFilter>() != null)
            {
                if (hitObj.transform.parent != null && hitObj.transform.parent.name == "ObjectSpawner")
                {
                    return true;
                }
            }
        }

        RotateArtifact childRotator = GetComponentInChildren<RotateArtifact>();
        if (childRotator != null && childRotator.IsBeingRotated)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// True if the pinch landed inside ANY ScrollRect's hierarchy (e.g. the History panel's
    /// scrollable description text) - pinch-hold-drag there must scroll the content, not move
    /// the whole panel. Generic on purpose: this component is shared by the Artifact and
    /// History detail panels, so any current or future scrollable area is excluded automatically
    /// without this dragger needing to know about specific fields on either panel.
    /// </summary>
    private static bool IsTargetingScrollableContent(GameObject hitObj)
    {
        return hitObj != null && hitObj.GetComponentInParent<ScrollRect>() != null;
    }

    private Transform ExtractInteractorTransform(PointerEventData eventData)
    {
        if (eventData is TrackedDeviceEventData trackedData && trackedData.interactor != null)
        {
            Transform interactorTransform = (trackedData.interactor as Component)?.transform;
            if (interactorTransform != null) return interactorTransform;
        }
        if (eventData != null && eventData.pressEventCamera != null)
        {
            return eventData.pressEventCamera.transform;
        }
        return FindActiveInteractorTransform();
    }

    private Transform FindActiveInteractorTransform()
    {
        XRBaseInteractor[] interactors = FindObjectsOfType<XRBaseInteractor>();
        foreach (var interactor in interactors)
        {
            if (interactor != null && interactor.gameObject.activeInHierarchy)
            {
                if (interactor.hasSelection || interactor.hasHover)
                {
                    return interactor.transform;
                }
            }
        }
        foreach (var interactor in interactors)
        {
            if (interactor != null && interactor.gameObject.activeInHierarchy)
            {
                return interactor.transform;
            }
        }
        if (Camera.main != null) return Camera.main.transform;
        return null;
    }

    private void StartPinchHold(Transform interactorTransform, GameObject hitObj = null)
    {
        if (IsTargeting3DArtifactModel(hitObj))
        {
            Debug.Log("[ArtifactPanelDragger] Pinch target is the 3D artifact model or model rotator! Suppressing panel drag.");
            return;
        }

        if (IsTargetingScrollableContent(hitObj))
        {
            Debug.Log("[ArtifactPanelDragger] Pinch target is inside a scrollable area (e.g. description text). Suppressing panel drag so it can scroll instead.");
            return;
        }

        isPinching = true;
        if (holdCoroutine != null) StopCoroutine(holdCoroutine);
        holdCoroutine = StartCoroutine(ProcessPinchHold(interactorTransform));
    }

    private void StopPinchHold()
    {
        isPinching = false;
        if (holdCoroutine != null)
        {
            StopCoroutine(holdCoroutine);
            holdCoroutine = null;
        }
        isMoving = false;
    }

    private Transform GetMoveTargetTransform()
    {
        if (transform.parent != null && transform.parent.GetComponent<Canvas>() != null && transform.parent.parent == null)
        {
            return transform.parent;
        }
        return transform;
    }

    private IEnumerator ProcessPinchHold(Transform interactorTransform)
    {
        float elapsed = 0f;

        // Phase 1: Wait for pinch-and-hold duration (allows instant UI button taps to complete unaffected)
        while (isPinching && elapsed < holdToMoveDuration)
        {
            RotateArtifact rotator = GetComponentInChildren<RotateArtifact>();
            if (rotator != null && rotator.IsBeingRotated)
            {
                Debug.Log("[ArtifactPanelDragger] 3D artifact model rotation active during hold! Aborting panel drag.");
                isPinching = false;
                yield break;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Phase 2: If still pinching after hold duration, activate Move Mode!
        if (isPinching)
        {
            isMoving = true;
            isUserMoved = true;
            isSnappedToWall = false;
            Debug.Log($"[ArtifactPanelDragger] Pinch hold threshold ({holdToMoveDuration}s) reached! Moving panel with hand/ray.");

            if (interactorTransform == null)
            {
                interactorTransform = FindActiveInteractorTransform();
            }

            Transform targetT = GetMoveTargetTransform();

            if (interactorTransform != null)
            {
                // Calculate initial local offset relative to the interactor ray
                Vector3 initialLocalPos = interactorTransform.InverseTransformPoint(targetT.position);
                Quaternion initialLocalRot = Quaternion.Inverse(interactorTransform.rotation) * targetT.rotation;

                while (isPinching)
                {
                    if (interactorTransform == null)
                    {
                        interactorTransform = FindActiveInteractorTransform();
                        if (interactorTransform == null) break;
                    }

                    Vector3 rawTargetPos = interactorTransform.TransformPoint(initialLocalPos);
                    Quaternion rawTargetRot = interactorTransform.rotation * initialLocalRot;

                    // Lock pitch (X) and roll (Z) to 0 so panel is perfectly level without any tilt
                    Quaternion levelRot = Quaternion.Euler(0f, rawTargetRot.eulerAngles.y, 0f);

                    Vector3 targetPos = rawTargetPos;
                    Quaternion targetRot = levelRot;

                    bool nowSnapped = TryGetWallSnap(rawTargetPos, out Vector3 snapPos, out Quaternion snapRot);
                    if (nowSnapped)
                    {
                        targetPos = snapPos;
                        targetRot = snapRot;
                    }
                    if (nowSnapped && !isSnappedToWall)
                    {
                        // Just engaged - a light haptic tap confirms the "click" onto the wall
                        // (no-op on hand tracking, which has no haptic actuator).
                        TryPulseHaptics(interactorTransform, 0.35f, 0.08f);
                    }
                    isSnappedToWall = nowSnapped;

                    targetT.position = Vector3.Lerp(targetT.position, targetPos, Time.deltaTime * 25f);
                    targetT.rotation = Quaternion.Slerp(targetT.rotation, targetRot, Time.deltaTime * 25f);

                    yield return null;
                }
            }

            // Explicitly force exactly 0-degree tilt on placement release (a wall-snapped
            // rotation is already pitch/roll-free, so this is a no-op in that case).
            targetT.rotation = Quaternion.Euler(0f, targetT.rotation.eulerAngles.y, 0f);

            isMoving = false;
            Debug.Log(isSnappedToWall
                ? "[ArtifactPanelDragger] Pinch released. Detail panel snapped flush onto the wall."
                : "[ArtifactPanelDragger] Pinch released. Detail panel fixed at level 0-degree tilt world location.");
        }
    }

    /// <summary>
    /// Looks for the nearest real-world WALL_FACE surface (via Meta MRUK's room scan) to
    /// worldPos. Returns true and outputs a flush, room-facing pose when one is within
    /// range. Uses a wider "stay attached" radius once already snapped so hovering right
    /// at the boundary doesn't flicker in and out.
    /// </summary>
    private bool TryGetWallSnap(Vector3 worldPos, out Vector3 snappedPos, out Quaternion snappedRot)
    {
        snappedPos = Vector3.zero;
        snappedRot = Quaternion.identity;

        if (!snapToWalls) return false;

        MRUKRoom room = MRUK.Instance != null ? MRUK.Instance.GetCurrentRoom() : null;
        if (room == null) return false;

        float distance = room.TryGetClosestSurfacePosition(worldPos, out Vector3 surfacePos, out MRUKAnchor anchor, out Vector3 normal, WallOnlyFilter);
        if (anchor == null) return false;

        float effectiveRadius = isSnappedToWall ? wallSnapRadius * 1.6f : wallSnapRadius;
        if (distance > effectiveRadius) return false;

        // Push out from the surface along its normal (which points into the room) so the
        // panel sits just proud of the wall instead of clipping into it, and face the same
        // way - flush-mounted, readable by anyone standing in the room.
        snappedPos = surfacePos + normal * wallSnapOffset;
        snappedRot = Quaternion.LookRotation(-normal, Vector3.up);
        return true;
    }

    private static void TryPulseHaptics(Transform interactorTransform, float amplitude, float duration)
    {
        if (interactorTransform == null) return;
        XRBaseControllerInteractor controllerInteractor = interactorTransform.GetComponent<XRBaseControllerInteractor>();
        if (controllerInteractor != null && controllerInteractor.xrController != null)
        {
            controllerInteractor.xrController.SendHapticImpulse(amplitude, duration);
        }
    }
}
