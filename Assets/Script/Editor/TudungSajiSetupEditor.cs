using System.IO;
using UnityEngine;
using UnityEditor;

public static class TudungSajiSetupEditor
{

    [MenuItem("Tools/Museum/Setup Tudung Saji Model")]
    public static void ManualSetup()
    {
        DoSetup(true);
    }

    private static void CheckAndSetup()
    {
        DoSetup(false);
    }

    private static void DoSetup(bool force)
    {
        string dataPath = "Assets/Resources/MuseumData/Artifacts/artifact_artifact_room_4_3_Tudung_Saji.asset";
        ArtifactData data = AssetDatabase.LoadAssetAtPath<ArtifactData>(dataPath);
        string prefabPath = "Assets/Resources/Models/model_artifact_tudung_saji.prefab";
        GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

        if (!force && data != null && data.modelPrefab != null && existingPrefab != null)
        {
            // Already set up
            return;
        }

        Debug.Log("[TudungSajiSetup] Beginning automated setup for Tudung Saji 3D model...");

        // Ensure directories exist
        EnsureDirectory("Assets/Prefabs");
        EnsureDirectory("Assets/Resources/Models");
        EnsureDirectory("Assets/Artifact/Tudung Saji/Textures");

        // 1. Refresh AssetDatabase to import OBJ and textures
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        // 2. Configure Normal Map
        string normalPath = "Assets/Artifact/Tudung Saji/Textures/TudungSaji_Normal.png";
        TextureImporter normalImporter = AssetImporter.GetAtPath(normalPath) as TextureImporter;
        if (normalImporter != null)
        {
            bool dirty = false;
            if (normalImporter.textureType != TextureImporterType.NormalMap)
            {
                normalImporter.textureType = TextureImporterType.NormalMap;
                dirty = true;
            }
            if (normalImporter.sRGBTexture)
            {
                normalImporter.sRGBTexture = false;
                dirty = true;
            }
            if (dirty)
            {
                normalImporter.SaveAndReimport();
                Debug.Log("[TudungSajiSetup] Reimported TudungSaji_Normal.png as NormalMap.");
            }
        }

        // 3. Configure OBJ importer
        string objPath = "Assets/Artifact/Tudung Saji/TudungSaji.obj";
        ModelImporter modelImporter = AssetImporter.GetAtPath(objPath) as ModelImporter;
        if (modelImporter != null)
        {
            if (modelImporter.materialImportMode != ModelImporterMaterialImportMode.None)
            {
                modelImporter.materialImportMode = ModelImporterMaterialImportMode.None;
                modelImporter.SaveAndReimport();
                Debug.Log("[TudungSajiSetup] Configured TudungSaji.obj ModelImporter.");
            }
        }

        // 4. Load textures
        Texture2D baseTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Artifact/Tudung Saji/Textures/TudungSaji_BaseColor.png");
        Texture2D normTex = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);

        // 5. Create URP Lit Material
        Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
        if (litShader == null) litShader = Shader.Find("Standard");

        string matPath = "Assets/Prefabs/Mat_artifact_tudung_saji.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        bool isNewMat = (mat == null);
        if (isNewMat)
        {
            mat = new Material(litShader);
        }
        else
        {
            mat.shader = litShader;
        }

        if (baseTex != null)
        {
            mat.SetTexture("_BaseMap", baseTex);
            mat.SetTexture("_MainTex", baseTex);
        }
        if (normTex != null)
        {
            mat.SetTexture("_BumpMap", normTex);
            mat.EnableKeyword("_NORMALMAP");
        }
        mat.SetFloat("_Smoothness", 0.35f);
        mat.SetFloat("_Metallic", 0.05f);
        mat.color = Color.white;
        mat.SetColor("_BaseColor", Color.white);

        if (isNewMat)
        {
            AssetDatabase.CreateAsset(mat, matPath);
        }
        else
        {
            EditorUtility.SetDirty(mat);
        }

        // Also duplicate to Assets/Artifact/Tudung Saji/Mat_TudungSaji.mat
        string matPath2 = "Assets/Artifact/Tudung Saji/Mat_TudungSaji.mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(matPath2) == null)
        {
            AssetDatabase.CopyAsset(matPath, matPath2);
        }

        // 6. Instantiate OBJ and build Prefab
        GameObject objAsset = AssetDatabase.LoadAssetAtPath<GameObject>(objPath);
        if (objAsset != null)
        {
            GameObject instance = Object.Instantiate(objAsset);
            instance.name = "model_artifact_tudung_saji";

            // Assign material to all renderers
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in renderers)
            {
                r.sharedMaterial = mat;
            }

            // Configure BoxCollider
            BoxCollider col = instance.GetComponent<BoxCollider>();
            if (col == null) col = instance.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.165f, 0f);
            col.size = new Vector3(0.72f, 0.35f, 0.72f);

            // Save Prefabs
            string p1 = "Assets/Prefabs/model_artifact_tudung_saji.prefab";
            string p2 = "Assets/Resources/Models/model_artifact_tudung_saji.prefab";
            string p3 = "Assets/Resources/Models/model_artifact_room_4_3.prefab";

            GameObject prefab1 = PrefabUtility.SaveAsPrefabAsset(instance, p1);
            GameObject prefab2 = PrefabUtility.SaveAsPrefabAsset(instance, p2);
            PrefabUtility.SaveAsPrefabAsset(instance, p3);

            Object.DestroyImmediate(instance);

            // 7. Update ArtifactData ScriptableObject
            if (data != null)
            {
                data.modelPrefab = prefab2 != null ? prefab2 : prefab1;
                EditorUtility.SetDirty(data);
                Debug.Log($"[TudungSajiSetup] Assigned modelPrefab to {dataPath}");
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[TudungSajiSetup] SUCCESS! Tudung Saji 3D model, material, prefabs, and metadata fully configured.");
        }
        else
        {
            Debug.LogError($"[TudungSajiSetup] Failed to find OBJ model at {objPath}");
        }
    }

    private static void EnsureDirectory(string relativePath)
    {
        if (!Directory.Exists(relativePath))
        {
            Directory.CreateDirectory(relativePath);
        }
    }
}
