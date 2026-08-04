using UnityEngine;

/// <summary>
/// Quest-style cursor dot shown where the hand ray hits a panel, button or 3D object -
/// a solid white circle with a soft edge and a faint dark rim so it stays visible on
/// both light and dark surfaces, like the Meta Quest home-menu cursor.
///
/// The circle is a generated disc MESH (not a textured quad), so it is geometrically
/// round no matter which shader the runtime material fallback picks.
///
/// An instance is assigned to each XRInteractorLineVisual's reticle slot (see
/// TutorialManager.ConfigureHandRayVisuals). The line visual owns its behaviour: it
/// places the dot at the exact hit point, turns it to face along the surface normal,
/// and shows/hides it depending on whether the ray hits anything. This component only
/// keeps the dot's apparent size constant with distance.
///
/// Built entirely at runtime via Create() - no prefab, mesh or texture asset needed.
/// </summary>
public class HandRayReticle : MonoBehaviour
{
    /// <summary>Dot diameter in meters at 1m distance (~1.1 degrees apparent size, a touch larger than the Quest cursor for readability).</summary>
    private const float BaseDiameterAtOneMeter = 0.02f;

    private Camera cachedCamera;

    /// <summary>Creates a ready-to-use reticle instance (initially inactive; the line visual toggles it).</summary>
    public static GameObject Create()
    {
        GameObject root = new GameObject("HandRayReticle");
        root.AddComponent<HandRayReticle>();

        GameObject dot = new GameObject("Dot");
        dot.transform.SetParent(root.transform, false);
        // The line visual points root.forward INTO the surface, so the disc looks back
        // at the player. Lift it 2mm off the surface toward the viewer so it never
        // z-fights the panel it sits on.
        dot.transform.localPosition = new Vector3(0f, 0f, -0.002f);
        dot.transform.localScale = Vector3.one * BaseDiameterAtOneMeter;

        dot.AddComponent<MeshFilter>().mesh = GenerateDiscMesh();
        MeshRenderer renderer = dot.AddComponent<MeshRenderer>();
        renderer.material = TutorialGestureGizmo.MakeRuntimeMaterial(Color.white, true);
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        return root;
    }

    private void Update()
    {
        // Constant angular size: scale linearly with distance from the eye, so the dot
        // looks the same whether the panel is at arm's length or across the room.
        if (cachedCamera == null || !cachedCamera.isActiveAndEnabled)
        {
            cachedCamera = Camera.main != null ? Camera.main : FindObjectOfType<Camera>();
        }
        if (cachedCamera == null) return;

        float distance = Vector3.Distance(cachedCamera.transform.position, transform.position);
        transform.localScale = Vector3.one * Mathf.Clamp(distance, 0.4f, 4f);
    }

    /// <summary>
    /// Flat disc of unit diameter in the XY plane, colored via vertex colors:
    /// solid white core -> faint dark rim (contrast on white panels) -> transparent
    /// feathered outer edge. Shaders that ignore vertex color still show a round
    /// white disc, so the reticle can never degrade into a square.
    /// </summary>
    private static Mesh GenerateDiscMesh()
    {
        const int segments = 48;
        const float coreRadius = 0.36f;   // solid white center
        const float rimRadius = 0.44f;    // dark contrast ring
        const float outerRadius = 0.5f;   // fully transparent feathered edge

        Color coreColor = Color.white;
        Color rimColor = new Color(0.12f, 0.14f, 0.18f, 0.55f);
        Color edgeColor = new Color(0.12f, 0.14f, 0.18f, 0f);

        // 1 center vertex + 3 rings.
        Vector3[] vertices = new Vector3[1 + segments * 3];
        Color[] colors = new Color[vertices.Length];
        vertices[0] = Vector3.zero;
        colors[0] = coreColor;

        for (int i = 0; i < segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            Vector3 dir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);

            vertices[1 + i] = dir * coreRadius;
            colors[1 + i] = coreColor;

            vertices[1 + segments + i] = dir * rimRadius;
            colors[1 + segments + i] = rimColor;

            vertices[1 + segments * 2 + i] = dir * outerRadius;
            colors[1 + segments * 2 + i] = edgeColor;
        }

        // Fan (center -> core ring) + two quad strips (core -> rim -> edge).
        int[] triangles = new int[segments * 3 + segments * 6 * 2];
        int t = 0;
        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;

            triangles[t++] = 0;
            triangles[t++] = 1 + i;
            triangles[t++] = 1 + next;

            AddQuad(triangles, ref t, 1 + i, 1 + next, 1 + segments + i, 1 + segments + next);
            AddQuad(triangles, ref t, 1 + segments + i, 1 + segments + next, 1 + segments * 2 + i, 1 + segments * 2 + next);
        }

        Mesh mesh = new Mesh { name = "HandRayReticleDisc" };
        mesh.vertices = vertices;
        mesh.colors = colors;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void AddQuad(int[] triangles, ref int t, int innerA, int innerB, int outerA, int outerB)
    {
        triangles[t++] = innerA;
        triangles[t++] = outerA;
        triangles[t++] = outerB;

        triangles[t++] = innerA;
        triangles[t++] = outerB;
        triangles[t++] = innerB;
    }
}
