using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Animated gesture demonstration shown next to the tutorial practice objects.
///
/// Visual source, in order of preference:
///  1. An artist-made animated prefab assigned on TutorialManager.customHandGizmoPrefab
///     (if it has an Animator, the clickTriggerName / holdDragTriggerName triggers are
///     fired when the mode changes).
///  2. The REAL XR hand: the scene's "Right/Left Hand Interaction Visual" (the same
///     skinned hand mesh the player sees on their own hands) is cloned, its tracking
///     drivers disabled, and its skeleton animated between an open pose and a pinch
///     pose computed with a small CCD IK solve that brings the thumb and index tips
///     together. This makes the demo hand look exactly like the player's hand.
///  3. Fallback: a procedural translucent sphere-hand (editor testing / no rig found).
///
/// Gestures looped:
///  - PinchClick: fingers close, a ring pulses at the pinch point, fingers open.
///  - PinchHoldDrag: fingers close and STAY closed while the whole hand sweeps side
///    to side (a small steady ring shows the hold), then release.
/// </summary>
public class TutorialGestureGizmo : MonoBehaviour
{
    public enum GestureMode { PinchClick, PinchHoldDrag }

    [Header("Custom Prefab Animator Triggers")]
    public string clickTriggerName = "PinchClick";
    public string holdDragTriggerName = "PinchHoldDrag";

    private static readonly Color GhostColor = new Color(0.75f, 0.93f, 1f, 0.55f);
    private static readonly Color RingColor = new Color(0.55f, 0.95f, 1f, 0.9f);
    private static readonly Vector3 SpherePinchPoint = new Vector3(0.045f, 0.075f, 0f);

    private GestureMode mode = GestureMode.PinchClick;
    private Coroutine animationLoop;
    private Camera cachedCamera;

    // Custom prefab path
    private GameObject customInstance;
    private Animator customAnimator;

    // Shared: parent of whatever hand visual is in use; translated during the drag sweep.
    private Transform handRoot;

    // Real cloned XR hand skeleton
    private bool usingRealHand;
    private readonly List<Transform> animatedJoints = new List<Transform>();
    private Quaternion[] openPose;
    private Quaternion[] closedPose;

    // Procedural sphere-hand fallback
    private Transform palm, indexMid, indexTip, thumbMid, thumbTip;
    private Material ghostMat;
    private Material ringMat;

    private LineRenderer pulseRing;

    /// <summary>Creates the gizmo (initially inactive; TutorialManager positions and enables it per step).</summary>
    public static TutorialGestureGizmo Create(GameObject customPrefab)
    {
        GameObject root = new GameObject("TutorialGestureGizmo");
        TutorialGestureGizmo gizmo = root.AddComponent<TutorialGestureGizmo>();

        if (customPrefab != null)
        {
            gizmo.customInstance = Instantiate(customPrefab, root.transform, false);
            gizmo.customAnimator = gizmo.customInstance.GetComponentInChildren<Animator>();
        }
        else if (!gizmo.TryCloneSceneHand())
        {
            gizmo.BuildProceduralHand();
        }

        root.SetActive(false);
        return gizmo;
    }

    public void SetMode(GestureMode newMode)
    {
        mode = newMode;

        if (customAnimator != null)
        {
            string trigger = mode == GestureMode.PinchClick ? clickTriggerName : holdDragTriggerName;
            customAnimator.SetTrigger(trigger);
            return;
        }

        if (animationLoop != null) StopCoroutine(animationLoop);
        if (isActiveAndEnabled) animationLoop = StartCoroutine(AnimateLoop());
    }

    private void OnEnable()
    {
        if (customAnimator == null && handRoot != null)
        {
            if (animationLoop != null) StopCoroutine(animationLoop);
            animationLoop = StartCoroutine(AnimateLoop());
        }
    }

    private void OnDisable()
    {
        animationLoop = null;
    }

    private void Update()
    {
        // Billboard toward the player (yaw only) so the demonstration always reads clearly.
        if (cachedCamera == null || !cachedCamera.isActiveAndEnabled)
        {
            cachedCamera = Camera.main != null ? Camera.main : FindObjectOfType<Camera>();
        }
        if (cachedCamera == null) return;

        Vector3 toCam = cachedCamera.transform.position - transform.position;
        toCam.y = 0f;
        if (toCam.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(-toCam, Vector3.up);
        }
    }

    private void OnDestroy()
    {
        if (ghostMat != null) Destroy(ghostMat);
        if (ringMat != null) Destroy(ringMat);
    }

    #region Real XR hand cloning

    /// <summary>
    /// Clones the scene's hand interaction visual (the player's actual hand mesh),
    /// freezes its tracking, poses it upright facing the player, and precomputes an
    /// open pose and a CCD-IK pinch pose for the skeleton. Returns false if the rig
    /// can't be found so the caller can fall back to the procedural hand.
    /// </summary>
    private bool TryCloneSceneHand()
    {
        bool isLeft = false;
        GameObject source = FindSceneObjectByName("Right Hand Interaction Visual");
        if (source == null)
        {
            source = FindSceneObjectByName("Left Hand Interaction Visual");
            isLeft = source != null;
        }
        if (source == null) return false;

        handRoot = new GameObject("HandRoot").transform;
        handRoot.SetParent(transform, false);

        GameObject clone = Instantiate(source, handRoot, false);
        clone.name = "GhostHand";
        clone.transform.localPosition = Vector3.zero;
        clone.transform.localRotation = Quaternion.identity;
        clone.SetActive(true);

        // Freeze the clone: no tracking driver may keep puppeting this skeleton.
        foreach (Behaviour b in clone.GetComponentsInChildren<Behaviour>(true))
        {
            if (b != null) b.enabled = false;
        }
        foreach (Collider c in clone.GetComponentsInChildren<Collider>(true))
        {
            Destroy(c);
        }
        foreach (LineRenderer lr in clone.GetComponentsInChildren<LineRenderer>(true))
        {
            lr.enabled = false; // stray ray visuals under the hand hierarchy
        }
        foreach (SkinnedMeshRenderer smr in clone.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            // The mesh controller may have hidden the mesh on tracking loss; force it visible.
            smr.enabled = true;
            smr.updateWhenOffscreen = true;
            smr.gameObject.SetActive(true);
        }

        // Locate the joints (Hands Interaction Demo naming: L_/R_ + joint, e.g. R_IndexProximal).
        Transform[] all = clone.GetComponentsInChildren<Transform>(true);
        Transform wrist = FindJoint(all, "_Wrist");
        Transform realIndexTip = FindJoint(all, "IndexTip");
        Transform realThumbTip = FindJoint(all, "ThumbTip");
        Transform indexProx = FindJoint(all, "IndexProximal");
        Transform indexInter = FindJoint(all, "IndexIntermediate");
        Transform indexDist = FindJoint(all, "IndexDistal");
        Transform thumbMeta = FindJoint(all, "ThumbMetacarpal");
        Transform thumbProx = FindJoint(all, "ThumbProximal");
        Transform thumbDist = FindJoint(all, "ThumbDistal");
        Transform middleProx = FindJoint(all, "MiddleProximal");
        Transform littleProx = FindJoint(all, "LittleProximal");

        if (wrist == null || realIndexTip == null || realThumbTip == null || indexProx == null ||
            indexDist == null || thumbProx == null || thumbDist == null || middleProx == null || littleProx == null)
        {
            Debug.LogWarning("[Tutorial] Hand visual found but joints missing - using procedural gizmo hand.");
            Destroy(handRoot.gameObject);
            handRoot = null;
            return false;
        }

        // Orient the hand: fingers up, palm facing away from the viewer (like seeing the
        // back of your own raised hand), slightly angled for readability. Convention-free:
        // the basis is computed from the joint positions themselves.
        Vector3 fingerDir = (middleProx.position - wrist.position).normalized;
        Vector3 across = (littleProx.position - indexProx.position).normalized;
        Vector3 palmNormal = isLeft ? Vector3.Cross(fingerDir, across) : Vector3.Cross(across, fingerDir);
        if (fingerDir.sqrMagnitude < 0.5f || palmNormal.sqrMagnitude < 0.001f)
        {
            Debug.LogWarning("[Tutorial] Hand joints degenerate - using procedural gizmo hand.");
            Destroy(handRoot.gameObject);
            handRoot = null;
            return false;
        }
        palmNormal.Normalize();

        Quaternion currentBasis = Quaternion.LookRotation(palmNormal, fingerDir);
        Quaternion desiredBasis = handRoot.rotation * Quaternion.Euler(-12f, isLeft ? 18f : -18f, 0f);
        clone.transform.rotation = desiredBasis * Quaternion.Inverse(currentBasis) * clone.transform.rotation;

        // Center the open-pose pinch point at the gizmo origin.
        Vector3 pinchMid = (realIndexTip.position + realThumbTip.position) * 0.5f;
        clone.transform.position += handRoot.position - pinchMid;

        // Joints that animate between open and pinched.
        animatedJoints.Clear();
        AddJoint(indexProx); AddJoint(indexInter); AddJoint(indexDist);
        AddJoint(thumbMeta); AddJoint(thumbProx); AddJoint(thumbDist);
        foreach (string finger in new[] { "Middle", "Ring", "Little" })
        {
            AddJoint(FindJoint(all, finger + "Proximal"));
            AddJoint(FindJoint(all, finger + "Intermediate"));
        }

        openPose = CapturePose();

        // Build the pinch pose: gently curl the helper fingers toward the palm, then
        // CCD the index and thumb chains until the tips meet.
        Vector3 palmarDir = handRoot.forward;
        Vector3 palmCenter = Vector3.Lerp(wrist.position, middleProx.position, 0.55f) + palmarDir * 0.025f;
        foreach (string finger in new[] { "Middle", "Ring", "Little" })
        {
            Transform prox = FindJoint(all, finger + "Proximal");
            Transform inter = FindJoint(all, finger + "Intermediate");
            Transform tip = FindJoint(all, finger + "Tip");
            if (tip == null) continue;
            RotateTowardTarget(prox, tip, palmCenter, 22f);
            RotateTowardTarget(inter, tip, palmCenter, 16f);
        }

        for (int iter = 0; iter < 20; iter++)
        {
            if ((realIndexTip.position - realThumbTip.position).magnitude < 0.007f) break;
            Vector3 mid = (realIndexTip.position + realThumbTip.position) * 0.5f;
            RotateTowardTarget(indexProx, realIndexTip, mid, 4f);
            RotateTowardTarget(indexInter, realIndexTip, mid, 5f);
            RotateTowardTarget(indexDist, realIndexTip, mid, 5f);
            RotateTowardTarget(thumbMeta, realThumbTip, mid, 3f);
            RotateTowardTarget(thumbProx, realThumbTip, mid, 5f);
            RotateTowardTarget(thumbDist, realThumbTip, mid, 5f);
        }

        closedPose = CapturePose();
        Vector3 closedPinchLocal = handRoot.InverseTransformPoint(
            (realIndexTip.position + realThumbTip.position) * 0.5f);

        // Back to the open pose; animation slerps between the two.
        ApplyPose(openPose);

        usingRealHand = true;
        BuildPulseRing(closedPinchLocal);
        Debug.Log($"[Tutorial] Gesture gizmo is using the real XR hand mesh ({source.name}).");
        return true;
    }

    private void AddJoint(Transform joint)
    {
        if (joint != null && !animatedJoints.Contains(joint)) animatedJoints.Add(joint);
    }

    private Quaternion[] CapturePose()
    {
        Quaternion[] pose = new Quaternion[animatedJoints.Count];
        for (int i = 0; i < animatedJoints.Count; i++) pose[i] = animatedJoints[i].localRotation;
        return pose;
    }

    private void ApplyPose(Quaternion[] pose)
    {
        for (int i = 0; i < animatedJoints.Count && i < pose.Length; i++)
        {
            animatedJoints[i].localRotation = pose[i];
        }
    }

    private static Transform FindJoint(Transform[] all, string nameSuffix)
    {
        foreach (Transform t in all)
        {
            if (t != null && t.name.EndsWith(nameSuffix)) return t;
        }
        return null;
    }

    /// <summary>
    /// One clamped CCD step: rotates 'joint' so 'endEffector' moves toward 'target'.
    /// Works purely in world space, so it is independent of the rig's axis conventions.
    /// </summary>
    private static void RotateTowardTarget(Transform joint, Transform endEffector, Vector3 target, float maxDegrees)
    {
        if (joint == null || endEffector == null) return;

        Vector3 from = endEffector.position - joint.position;
        Vector3 to = target - joint.position;
        if (from.sqrMagnitude < 1e-8f || to.sqrMagnitude < 1e-8f) return;

        Quaternion delta = Quaternion.FromToRotation(from, to);
        delta.ToAngleAxis(out float angle, out Vector3 axis);
        if (angle > 180f) angle -= 360f;
        if (Mathf.Abs(angle) < 0.01f || float.IsNaN(axis.x)) return;

        angle = Mathf.Clamp(angle, -maxDegrees, maxDegrees);
        joint.rotation = Quaternion.AngleAxis(angle, axis) * joint.rotation;
    }

    private static GameObject FindSceneObjectByName(string name)
    {
        GameObject fallback = null;
        foreach (Transform t in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (t == null || t.name != name || !t.gameObject.scene.IsValid()) continue;
            if (t.gameObject.activeInHierarchy) return t.gameObject;
            if (fallback == null) fallback = t.gameObject;
        }
        return fallback;
    }

    #endregion

    #region Procedural hand construction (fallback)

    private void BuildProceduralHand()
    {
        ghostMat = MakeRuntimeMaterial(GhostColor, true);

        handRoot = new GameObject("HandRoot").transform;
        handRoot.SetParent(transform, false);

        palm = MakePart("Palm", new Vector3(0.055f, 0.068f, 0.03f));
        indexMid = MakePart("IndexMid", Vector3.one * 0.022f);
        indexTip = MakePart("IndexTip", Vector3.one * 0.02f);
        thumbMid = MakePart("ThumbMid", Vector3.one * 0.024f);
        thumbTip = MakePart("ThumbTip", Vector3.one * 0.02f);

        SetPinchAmount(0f);
        BuildPulseRing(SpherePinchPoint);
    }

    private Transform MakePart(string name, Vector3 scale)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        part.name = name;
        Destroy(part.GetComponent<Collider>());
        part.transform.SetParent(handRoot, false);
        part.transform.localScale = scale;
        part.GetComponent<Renderer>().material = ghostMat;
        return part.transform;
    }

    #endregion

    #region Pulse ring

    private void BuildPulseRing(Vector3 localPosition)
    {
        GameObject ringGo = new GameObject("PulseRing");
        ringGo.transform.SetParent(handRoot, false);
        ringGo.transform.localPosition = localPosition;

        pulseRing = ringGo.AddComponent<LineRenderer>();
        pulseRing.useWorldSpace = false;
        pulseRing.loop = true;
        pulseRing.positionCount = 32;
        pulseRing.widthMultiplier = 0.004f;
        ringMat = MakeRuntimeMaterial(RingColor, true);
        pulseRing.material = ringMat;
        pulseRing.startColor = RingColor;
        pulseRing.endColor = RingColor;
        pulseRing.enabled = false;
    }

    private void SetRingRadius(float radius, float alpha)
    {
        if (pulseRing == null) return;
        for (int i = 0; i < pulseRing.positionCount; i++)
        {
            float angle = (float)i / pulseRing.positionCount * Mathf.PI * 2f;
            pulseRing.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
        }
        Color c = RingColor;
        c.a = alpha;
        pulseRing.startColor = c;
        pulseRing.endColor = c;
    }

    #endregion

    /// <summary>0 = hand open, 1 = thumb and index touching (pinched).</summary>
    private void SetPinchAmount(float t)
    {
        if (handRoot == null) return;
        t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));

        if (usingRealHand)
        {
            for (int i = 0; i < animatedJoints.Count; i++)
            {
                animatedJoints[i].localRotation = Quaternion.Slerp(openPose[i], closedPose[i], t);
            }
            return;
        }

        palm.localPosition = Vector3.zero;
        indexMid.localPosition = Vector3.Lerp(new Vector3(0.012f, 0.055f, 0f), new Vector3(0.022f, 0.05f, 0f), t);
        indexTip.localPosition = Vector3.Lerp(new Vector3(0.02f, 0.09f, 0f), SpherePinchPoint + new Vector3(-0.004f, 0.005f, 0f), t);
        thumbMid.localPosition = Vector3.Lerp(new Vector3(0.045f, 0.008f, 0f), new Vector3(0.052f, 0.022f, 0f), t);
        thumbTip.localPosition = Vector3.Lerp(new Vector3(0.075f, 0.035f, 0f), SpherePinchPoint + new Vector3(0.004f, -0.005f, 0f), t);
    }

    #region Animation loops

    private IEnumerator AnimateLoop()
    {
        while (true)
        {
            if (mode == GestureMode.PinchClick)
            {
                yield return AnimatePinch(0f, 1f, 0.3f);
                yield return AnimateRingPulse(0.35f);
                yield return AnimatePinch(1f, 0f, 0.3f);
                yield return new WaitForSeconds(0.7f);
            }
            else
            {
                yield return AnimatePinch(0f, 1f, 0.3f);

                // Hold the pinch and sweep the whole hand side to side: two full sweeps.
                float duration = 2.6f;
                float elapsed = 0f;
                SetRingRadius(0.018f, 0.5f); // small steady ring = "still holding"
                if (pulseRing != null) pulseRing.enabled = true;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float x = Mathf.Sin(elapsed / duration * Mathf.PI * 4f) * 0.07f;
                    handRoot.localPosition = new Vector3(x, 0f, 0f);
                    yield return null;
                }
                if (pulseRing != null) pulseRing.enabled = false;

                yield return AnimatePinch(1f, 0f, 0.3f);

                // Glide back to center before the next loop.
                Vector3 from = handRoot.localPosition;
                float t = 0f;
                while (t < 0.3f)
                {
                    t += Time.deltaTime;
                    handRoot.localPosition = Vector3.Lerp(from, Vector3.zero, t / 0.3f);
                    yield return null;
                }
                yield return new WaitForSeconds(0.6f);
            }
        }
    }

    private IEnumerator AnimatePinch(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            SetPinchAmount(Mathf.Lerp(from, to, elapsed / duration));
            yield return null;
        }
        SetPinchAmount(to);
    }

    private IEnumerator AnimateRingPulse(float duration)
    {
        if (pulseRing == null) yield break;
        pulseRing.enabled = true;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetRingRadius(Mathf.Lerp(0.015f, 0.07f, t), 1f - t);
            yield return null;
        }
        pulseRing.enabled = false;
    }

    #endregion

    /// <summary>
    /// Shared helper for all runtime-built tutorial visuals.
    /// Transparent materials use UI/Sprite shaders (unlit, URP-compatible, alpha-blended);
    /// opaque ones prefer URP Lit so practice objects sit naturally in passthrough lighting.
    /// </summary>
    public static Material MakeRuntimeMaterial(Color color, bool transparent)
    {
        Shader shader = null;
        if (transparent)
        {
            shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("UI/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        }
        else
        {
            shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
        }
        if (shader == null) shader = Shader.Find("Unlit/Color");

        Material mat = new Material(shader);
        mat.color = color;
        return mat;
    }
}
