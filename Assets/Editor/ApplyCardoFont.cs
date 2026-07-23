using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// One-click tool to make the whole app use Cardo-Regular.
/// Generates the TMP SDF font asset from the .ttf (if not already present),
/// then reassigns every TextMeshPro and legacy UI Text to it across prefabs
/// and all currently-open scenes.
///
/// Run from the menu: Tools > Fonts > Apply Cardo To Everything
/// </summary>
public static class ApplyCardoFont
{
    const string TtfPath = "Assets/Fonts/Cardo-Regular.ttf";
    const string FontAssetPath = "Assets/Fonts/Cardo-Regular SDF.asset";

    [MenuItem("Tools/Fonts/Apply Cardo To Everything")]
    public static void Apply()
    {
        TMP_FontAsset cardo = GetOrCreateCardo();
        if (cardo == null) return;

        Font cardoTtf = AssetDatabase.LoadAssetAtPath<Font>(TtfPath);

        int tmpCount = 0, legacyCount = 0, prefabCount = 0;

        // --- 1. Prefabs (skip third-party demo folders) ---
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.StartsWith("Assets/Samples/") || path.StartsWith("Assets/Layer Lab/"))
                continue;

            GameObject root;
            try { root = PrefabUtility.LoadPrefabContents(path); }
            catch { continue; }

            bool changed = false;
            foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
                if (t.font != cardo) { t.font = cardo; changed = true; tmpCount++; }
            foreach (var t in root.GetComponentsInChildren<Text>(true))
                if (cardoTtf != null && t.font != cardoTtf) { t.font = cardoTtf; changed = true; legacyCount++; }

            if (changed) { PrefabUtility.SaveAsPrefabAsset(root, path); prefabCount++; }
            PrefabUtility.UnloadPrefabContents(root);
        }

        // --- 2. All currently-open scenes ---
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;

            foreach (GameObject rootGo in scene.GetRootGameObjects())
            {
                foreach (var t in rootGo.GetComponentsInChildren<TMP_Text>(true))
                    if (t.font != cardo) { t.font = cardo; EditorUtility.SetDirty(t); tmpCount++; }
                foreach (var t in rootGo.GetComponentsInChildren<Text>(true))
                    if (cardoTtf != null && t.font != cardoTtf) { t.font = cardoTtf; EditorUtility.SetDirty(t); legacyCount++; }
            }
            EditorSceneManager.MarkSceneDirty(scene);
        }
        EditorSceneManager.SaveOpenScenes();

        // --- 3. Make Cardo the TMP default for any new text ---
        SetTmpDefault(cardo);

        AssetDatabase.SaveAssets();
        Debug.Log($"[Cardo] Done. TMP texts: {tmpCount}, legacy UI texts: {legacyCount}, prefabs updated: {prefabCount}. " +
                  "Open any other scene (e.g. SampleScene) and run again to cover it too.");
    }

    static TMP_FontAsset GetOrCreateCardo()
    {
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (existing != null) return existing;

        Font src = AssetDatabase.LoadAssetAtPath<Font>(TtfPath);
        if (src == null)
        {
            Debug.LogError("[Cardo] Source font not found at " + TtfPath);
            return null;
        }

        // Dynamic SDF font asset: glyphs rasterize on demand at runtime (works on device).
        TMP_FontAsset fa = TMP_FontAsset.CreateFontAsset(src);
        if (fa == null)
        {
            Debug.LogError("[Cardo] TMP_FontAsset.CreateFontAsset failed.");
            return null;
        }

        AssetDatabase.CreateAsset(fa, FontAssetPath);
        if (fa.material != null)
        {
            fa.material.name = "Cardo-Regular SDF Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
        }
        if (fa.atlasTexture != null)
        {
            fa.atlasTexture.name = "Cardo-Regular SDF Atlas";
            AssetDatabase.AddObjectToAsset(fa.atlasTexture, fa);
        }
        EditorUtility.SetDirty(fa);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(FontAssetPath);
        Debug.Log("[Cardo] Created font asset at " + FontAssetPath);
        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
    }

    static void SetTmpDefault(TMP_FontAsset cardo)
    {
        var settings = TMP_Settings.instance;
        if (settings == null) return;
        var so = new SerializedObject(settings);
        var prop = so.FindProperty("m_defaultFontAsset");
        if (prop != null)
        {
            prop.objectReferenceValue = cardo;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(settings);
        }
    }
}
