using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// Authoritative Quest-style UI Cursor for both Optical Hands and Controllers.
///
/// Guarantees:
/// 1. Zero visible ray lines (LineRenderers and XRInteractorLineVisuals are permanently suppressed).
/// 2. A crisp, smooth white circular cursor appears on any Canvas or 3D interactable pointed at.
/// 3. Billboards directly facing the camera, floating slightly in front of the surface to prevent occlusion.
/// 4. Auto-injects TrackedDeviceGraphicRaycaster onto all WorldSpace Canvases so every panel is raycastable.
/// </summary>
[DefaultExecutionOrder(XRInteractionUpdateOrder.k_BeforeRenderLineVisual + 10)]
public class HandRayReticle : MonoBehaviour
{
    private const float BaseDiameterAtOneMeter = 0.022f; // ~2.2cm at 1m, matches Quest home cursor
    private const float SurfaceLiftMeters = 0.005f;      // 5mm lift off canvas surface to eliminate z-fighting

    private XRRayInteractor rayInteractor;
    private LineRenderer cachedLineRenderer;
    private XRInteractorLineVisual cachedLineVisual;

    private GameObject cursorInstance;
    private Camera cachedCamera;
    private GameObject lastHoveredButton;

    private static Material s_CursorMaterial;
    private static Texture2D s_CursorTexture;
    private static Mesh s_DiscMesh;

    private static float s_LastCanvasScanTime = -10f;
    private float lastPeriodicCheckTime = 0f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitAfterSceneLoad()
    {
        SetupAllRayInteractors();
    }

    private void Awake()
    {
        FindComponents();
        CreateCursorObject();
        SuppressLineRenderer();
    }

    private void OnEnable()
    {
        Application.onBeforeRender += OnBeforeRender;
        SuppressLineRenderer();
    }

    private void OnDisable()
    {
        Application.onBeforeRender -= OnBeforeRender;
        if (cursorInstance != null)
            cursorInstance.SetActive(false);
    }

    private void OnDestroy()
    {
        Application.onBeforeRender -= OnBeforeRender;
        if (cursorInstance != null)
            Destroy(cursorInstance);
    }

    private void Start()
    {
        FindComponents();
        SuppressLineRenderer();
    }

    private void Update()
    {
        // Periodic check every 0.5s to ensure any newly enabled rays or canvases are hooked
        if (Time.unscaledTime - lastPeriodicCheckTime > 0.5f)
        {
            lastPeriodicCheckTime = Time.unscaledTime;
            SetupAllRayInteractors();
        }
    }

    private void LateUpdate()
    {
        UpdateCursorAndLine();
    }

    private void OnBeforeRender()
    {
        UpdateCursorAndLine();
    }

    private void FindComponents()
    {
        if (rayInteractor == null)
            rayInteractor = GetComponent<XRRayInteractor>();
        if (rayInteractor == null)
            rayInteractor = GetComponentInParent<XRRayInteractor>();
        if (rayInteractor == null)
            rayInteractor = GetComponentInChildren<XRRayInteractor>(true);

        if (cachedLineRenderer == null)
            cachedLineRenderer = GetComponent<LineRenderer>();
        if (cachedLineRenderer == null)
            cachedLineRenderer = GetComponentInChildren<LineRenderer>(true);

        if (cachedLineVisual == null)
            cachedLineVisual = GetComponent<XRInteractorLineVisual>();
        if (cachedLineVisual == null)
            cachedLineVisual = GetComponentInChildren<XRInteractorLineVisual>(true);
    }

    public void SuppressLineRenderer()
    {
        if (cachedLineVisual != null && cachedLineVisual.enabled)
        {
            cachedLineVisual.enabled = false;
        }

        if (cachedLineRenderer != null)
        {
            if (cachedLineRenderer.enabled)
                cachedLineRenderer.enabled = false;
            cachedLineRenderer.widthMultiplier = 0f;
            cachedLineRenderer.startWidth = 0f;
            cachedLineRenderer.endWidth = 0f;
            if (cachedLineRenderer.positionCount > 0)
                cachedLineRenderer.positionCount = 0;
        }

        // Also check any child line renderers
        foreach (var lr in GetComponentsInChildren<LineRenderer>(true))
        {
            if (lr != null)
            {
                if (lr.enabled) lr.enabled = false;
                lr.widthMultiplier = 0f;
                lr.startWidth = 0f;
                lr.endWidth = 0f;
                if (lr.positionCount > 0) lr.positionCount = 0;
            }
        }
        foreach (var vis in GetComponentsInChildren<XRInteractorLineVisual>(true))
        {
            if (vis != null && vis.enabled) vis.enabled = false;
        }
    }

    private void CreateCursorObject()
    {
        if (cursorInstance != null) return;

        EnsureSharedAssets();

        cursorInstance = new GameObject("WhiteCircleCursor");
        cursorInstance.transform.SetParent(null, false);

        var filter = cursorInstance.AddComponent<MeshFilter>();
        filter.sharedMesh = s_DiscMesh;

        var renderer = cursorInstance.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = s_CursorMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

        cursorInstance.SetActive(false);
    }

    private static void EnsureSharedAssets()
    {
        if (s_CursorMaterial == null)
        {
            s_CursorMaterial = CreateCursorMaterial();
        }

        if (s_DiscMesh == null)
        {
            s_DiscMesh = CreateCircleMesh(48);
        }
    }

    private static Texture2D GenerateCircleTexture(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "RayCursorTexture",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float radius = size * 0.44f;
        float feather = 1.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                float alpha = Mathf.Clamp01((radius + feather - dist) / (feather * 2f));

                // Always use pure white (1, 1, 1) for RGB so bilinear filtering never bleeds dark colors!
                // Pixels outside the circle radius have alpha = 0 (100% transparent).
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply(false, true);
        return tex;
    }

    private static Material CreateCursorMaterial()
    {
        Shader shader = Shader.Find("UI/RayCursor");
        if (shader == null) shader = Shader.Find("VR/RayCursor");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("UI/Default");
        if (shader == null) shader = Shader.Find("Unlit/Transparent");

        Material mat = new Material(shader)
        {
            name = "RayCursorMaterial"
        };

        mat.color = Color.white;
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
        if (mat.HasProperty("_Radius")) mat.SetFloat("_Radius", 0.46f);
        if (mat.HasProperty("_Feather")) mat.SetFloat("_Feather", 0.015f);

        // Explicitly enforce transparency and turn off depth writing on all properties
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f); // Transparent
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f); // Alpha
        if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", 0);
        if (mat.HasProperty("_Cull")) mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 500;

        // If using fallback shaders that sample textures (like Sprites/Default or UI/Default),
        // provide a clean generated circle texture where outside pixels are strictly 100% transparent:
        if (mat.HasProperty("_MainTex") || mat.HasProperty("_BaseMap"))
        {
            if (s_CursorTexture == null)
                s_CursorTexture = GenerateCircleTexture(128);

            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", s_CursorTexture);
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", s_CursorTexture);
        }

        return mat;
    }

    private static Mesh CreateCircleMesh(int segments = 48)
    {
        Mesh mesh = new Mesh { name = "RayCursorDisc" };
        Vector3[] vertices = new Vector3[segments + 1];
        Vector2[] uvs = new Vector2[segments + 1];
        int[] triangles = new int[segments * 6];

        vertices[0] = Vector3.zero;
        uvs[0] = new Vector2(0.5f, 0.5f);

        float angleStep = 360f / segments * Mathf.Deg2Rad;
        float radius = 0.5f;

        for (int i = 0; i < segments; i++)
        {
            float angle = i * angleStep;
            float x = Mathf.Cos(angle) * radius;
            float y = Mathf.Sin(angle) * radius;

            vertices[i + 1] = new Vector3(x, y, 0f);
            uvs[i + 1] = new Vector2(0.5f + x, 0.5f + y);
        }

        int triIdx = 0;
        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;

            // Front face
            triangles[triIdx++] = 0;
            triangles[triIdx++] = i + 1;
            triangles[triIdx++] = next + 1;

            // Back face
            triangles[triIdx++] = 0;
            triangles[triIdx++] = next + 1;
            triangles[triIdx++] = i + 1;
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private void UpdateCursorAndLine()
    {
        SuppressLineRenderer();

        if (rayInteractor == null)
            FindComponents();

        if (rayInteractor == null || !rayInteractor.isActiveAndEnabled || !gameObject.activeInHierarchy)
        {
            if (cursorInstance != null && cursorInstance.activeSelf)
                cursorInstance.SetActive(false);
            return;
        }

        bool hasHit = false;
        Vector3 hitPoint = Vector3.zero;

        // 1. Check UI Raycast Result (buttons, labels, images, panel backgrounds)
        bool hasUIHit = rayInteractor.TryGetCurrentUIRaycastResult(out RaycastResult uiResult) && uiResult.isValid;

        // Button hover detection for immediate controller buzz
        if (hasUIHit && uiResult.gameObject != null)
        {
            Button btn = uiResult.gameObject.GetComponentInParent<Button>();
            XRButtonSelection xrBtn = uiResult.gameObject.GetComponentInParent<XRButtonSelection>();
            GameObject targetBtn = btn != null ? btn.gameObject : (xrBtn != null ? xrBtn.gameObject : null);

            if (targetBtn != null && targetBtn != lastHoveredButton)
            {
                lastHoveredButton = targetBtn;
                XRButtonHaptics.TriggerHover(rayInteractor, targetBtn);
            }
            else if (targetBtn == null)
            {
                lastHoveredButton = null;
            }
        }
        else
        {
            lastHoveredButton = null;
        }

        // 2. Check 3D Raycast Hit (artifacts, 3D buttons, colliders)
        bool has3DHit = rayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit hit3D);

        if (hasUIHit && has3DHit)
        {
            // Pick whichever valid surface is closer to the interactor
            if (uiResult.distance <= hit3D.distance)
            {
                hitPoint = uiResult.worldPosition;
                hasHit = true;
            }
            else
            {
                hitPoint = hit3D.point;
                hasHit = true;
            }
        }
        else if (hasUIHit)
        {
            hitPoint = uiResult.worldPosition;
            hasHit = true;
        }
        else if (has3DHit)
        {
            hitPoint = hit3D.point;
            hasHit = true;
        }

        // Update cursor visual
        if (hasHit)
        {
            if (cachedCamera == null || !cachedCamera.isActiveAndEnabled)
            {
                cachedCamera = Camera.main != null ? Camera.main : FindObjectOfType<Camera>();
            }

            if (cursorInstance == null)
            {
                CreateCursorObject();
            }

            if (cachedCamera != null && cursorInstance != null)
            {
                Vector3 toCamera = cachedCamera.transform.position - hitPoint;
                float dist = toCamera.magnitude;
                if (dist > 0.001f) toCamera /= dist;
                else toCamera = Vector3.up;

                // Lift 5mm toward camera to guarantee no clipping or z-fighting
                cursorInstance.transform.position = hitPoint + toCamera * SurfaceLiftMeters;
                cursorInstance.transform.rotation = Quaternion.LookRotation(-toCamera, cachedCamera.transform.up);

                // Constant apparent angular size in headset
                float scale = Mathf.Clamp(dist * BaseDiameterAtOneMeter, 0.008f, 0.09f);
                cursorInstance.transform.localScale = new Vector3(scale, scale, scale);

                if (!cursorInstance.activeSelf)
                    cursorInstance.SetActive(true);
            }
        }
        else
        {
            if (cursorInstance != null && cursorInstance.activeSelf)
                cursorInstance.SetActive(false);
        }
    }

    private static void UpdateActiveCanvasesList()
    {
        if (Time.unscaledTime - s_LastCanvasScanTime < 1.0f)
            return;

        s_LastCanvasScanTime = Time.unscaledTime;

        Canvas[] allCanvases = FindObjectsOfType<Canvas>(true);
        for (int i = 0; i < allCanvases.Length; i++)
        {
            if (allCanvases[i] != null && allCanvases[i].renderMode == RenderMode.WorldSpace)
            {
                if (allCanvases[i].GetComponent<TrackedDeviceGraphicRaycaster>() == null)
                {
                    allCanvases[i].gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
                }
            }
        }
    }

    /// <summary>
    /// App-wide initialization: finds all ray interactors, removes visible lines, and attaches this cursor.
    /// </summary>
    public static void SetupAllRayInteractors()
    {
        UpdateActiveCanvasesList();

        XRRayInteractor[] rays = FindObjectsOfType<XRRayInteractor>(true);
        for (int i = 0; i < rays.Length; i++)
        {
            var ray = rays[i];
            if (ray == null) continue;
            if (ray.name.Contains("Teleport") || ray.name.Contains("Gaze")) continue;

            ray.lineType = XRRayInteractor.LineType.StraightLine;
            ray.enableUIInteraction = true;

            var lineVisual = ray.GetComponent<XRInteractorLineVisual>();
            if (lineVisual != null)
            {
                lineVisual.enabled = false;
            }

            var lr = ray.GetComponent<LineRenderer>();
            if (lr != null)
            {
                lr.enabled = false;
                lr.widthMultiplier = 0f;
                lr.startWidth = 0f;
                lr.endWidth = 0f;
                if (lr.positionCount > 0) lr.positionCount = 0;
            }

            var reticle = ray.GetComponent<HandRayReticle>();
            if (reticle == null)
            {
                reticle = ray.gameObject.AddComponent<HandRayReticle>();
            }
            reticle.SuppressLineRenderer();
        }
    }

    /// <summary>
    /// Legacy factory method for compatibility with any existing caller.
    /// </summary>
    public static GameObject Create()
    {
        EnsureSharedAssets();
        GameObject root = new GameObject("HandRayReticle");
        var filter = root.AddComponent<MeshFilter>();
        filter.sharedMesh = s_DiscMesh;
        var renderer = root.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = s_CursorMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return root;
    }
}
