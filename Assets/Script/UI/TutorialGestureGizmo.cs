using System.Collections;
using UnityEngine;

/// <summary>
/// Animated gesture demonstration shown next to the tutorial practice objects.
///
/// Default: a procedural "ghost hand" built from translucent spheres (palm,
/// index finger, thumb) that loops the current gesture:
///   - PinchClick: fingers close, a ring pulses at the pinch point, fingers open.
///   - PinchHoldDrag: fingers close and STAY closed while the whole hand sweeps
///     side to side, then release.
///
/// Optional: assign an artist-made animated hand prefab on
/// TutorialManager.customHandGizmoPrefab instead. It is instantiated in place of
/// the ghost hand; if it has an Animator, the triggers named by
/// clickTriggerName / holdDragTriggerName are fired when the mode changes.
/// </summary>
public class TutorialGestureGizmo : MonoBehaviour
{
    public enum GestureMode { PinchClick, PinchHoldDrag }

    [Header("Custom Prefab Animator Triggers")]
    public string clickTriggerName = "PinchClick";
    public string holdDragTriggerName = "PinchHoldDrag";

    private static readonly Color GhostColor = new Color(0.75f, 0.93f, 1f, 0.55f);
    private static readonly Color RingColor = new Color(0.55f, 0.95f, 1f, 0.9f);

    private GestureMode mode = GestureMode.PinchClick;
    private Coroutine animationLoop;
    private Camera cachedCamera;

    // Custom prefab path
    private GameObject customInstance;
    private Animator customAnimator;

    // Procedural ghost hand
    private Transform handRoot;
    private Transform palm, indexMid, indexTip, thumbMid, thumbTip;
    private LineRenderer pulseRing;
    private Material ghostMat;
    private Material ringMat;

    private static readonly Vector3 PinchPoint = new Vector3(0.045f, 0.075f, 0f);

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
        else
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

    #region Procedural hand construction

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
        BuildPulseRing();
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

    private void BuildPulseRing()
    {
        GameObject ringGo = new GameObject("PulseRing");
        ringGo.transform.SetParent(handRoot, false);
        ringGo.transform.localPosition = PinchPoint;

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

    /// <summary>0 = hand open, 1 = thumb and index touching (pinched).</summary>
    private void SetPinchAmount(float t)
    {
        if (handRoot == null) return;
        t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));

        palm.localPosition = Vector3.zero;
        indexMid.localPosition = Vector3.Lerp(new Vector3(0.012f, 0.055f, 0f), new Vector3(0.022f, 0.05f, 0f), t);
        indexTip.localPosition = Vector3.Lerp(new Vector3(0.02f, 0.09f, 0f), PinchPoint + new Vector3(-0.004f, 0.005f, 0f), t);
        thumbMid.localPosition = Vector3.Lerp(new Vector3(0.045f, 0.008f, 0f), new Vector3(0.052f, 0.022f, 0f), t);
        thumbTip.localPosition = Vector3.Lerp(new Vector3(0.075f, 0.035f, 0f), PinchPoint + new Vector3(0.004f, -0.005f, 0f), t);
    }

    #endregion

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
