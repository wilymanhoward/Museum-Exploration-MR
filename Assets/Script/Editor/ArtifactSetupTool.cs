using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class ArtifactSetupTool : Editor
{
    [MenuItem("Tools/Museum MR/Setup New Artifacts")]
    public static void SetupArtifacts()
    {
        Debug.Log("Starting Indonesian Artifacts Setup...");

        // Ensure directories exist
        string museumDataDir = "Assets/MuseumData";
        string prefabDir = "Assets/Prefabs";
        if (!Directory.Exists(museumDataDir)) Directory.CreateDirectory(museumDataDir);
        if (!Directory.Exists(prefabDir)) Directory.CreateDirectory(prefabDir);

        // Load starting room (Renaissance Gallery)
        string roomPath = "Assets/MuseumData/room_1_RenaissanceGallery.asset";
        RoomData startingRoom = AssetDatabase.LoadAssetAtPath<RoomData>(roomPath);
        if (startingRoom == null)
        {
            Debug.LogError($"Could not find starting room asset at: {roomPath}");
            return;
        }

        // Define our 5 new artifacts with metadata and texture mappings
        var newArtifactsConfig = new List<ArtifactConfig>()
        {
            new ArtifactConfig()
            {
                id = "artifact_batu",
                name = "Ancient Batu",
                artist = "Megalithic Artisan",
                year = "c. 2500 BCE",
                description = "A historical stone artifact from ancient Indonesia. Carved from solid river rock, it represents early megalithic culture and ceremonial craftsmanship.",
                modelPath = "Assets/Artifact/Batu/Batu.fbx",
                baseTexPath = "Assets/Artifact/Batu/RockTexture/RockArtifact_Base_color_1001.png",
                normalTexPath = "Assets/Artifact/Batu/RockTexture/RockArtifact_Normal_1001.png",
                targetSize = 1.6f
            },
            new ArtifactConfig()
            {
                id = "artifact_gamelan",
                name = "Gamelan Xylophone",
                artist = "Traditional Javanese Smith",
                year = "c. 1200 CE",
                description = "A traditional Javanese musical instrument. Part of the gamelan ensemble, this bronze xylophone-style instrument produces resonant, metallic tones.",
                modelPath = "Assets/Artifact/Gamelan/Gamelen.fbx",
                baseTexPath = "Assets/Artifact/Gamelan/Textures/Xylophone_Base_color_1001.png",
                normalTexPath = "Assets/Artifact/Gamelan/Textures/Xylophone_Normal_1001.png",
                targetSize = 0.55f,
                submeshTextures = new Dictionary<string, string>()
                {
                    { "Xylophone", "Assets/Artifact/Gamelan/Textures/Xylophone_Base_color_1001.png" },
                    { "Xylophone2", "Assets/Artifact/Gamelan/Textures/Xylophone2_Base_color_1001.png" }
                },
                submeshNormals = new Dictionary<string, string>()
                {
                    { "Xylophone", "Assets/Artifact/Gamelan/Textures/Xylophone_Normal_1001.png" },
                    { "Xylophone2", "Assets/Artifact/Gamelan/Textures/Xylophone2_Normal_1001.png" }
                }
            },
            new ArtifactConfig()
            {
                id = "artifact_keris",
                name = "Traditional Indonesian Keris",
                artist = "Empu Bladesmith",
                year = "c. 1400 CE",
                description = "An asymmetrical dagger from Indonesia. Both a functional weapon and a spiritual object, the keris is famous for its distinctive wavy blade and decorated hilt.",
                modelPath = "Assets/Artifact/Keris/Keris.fbx",
                baseTexPath = "Assets/Artifact/Keris/Textures/Material_Base_color_1001.png",
                normalTexPath = "Assets/Artifact/Keris/Textures/Material_Normal_1001.png",
                targetSize = 0.45f
            },
            new ArtifactConfig()
            {
                id = "artifact_songket",
                name = "Sumatran Songket Fabric",
                artist = "Minangkabau Weaver",
                year = "c. 1880 CE",
                description = "A hand-woven fabric featuring gold and silver threads. Originating from Sumatra, it is traditionally worn during ceremonial occasions and signifies wealth and prestige.",
                modelPath = "Assets/Artifact/Songket/Songket.fbx",
                baseTexPath = "Assets/Artifact/Songket/Textures/DSC_3387.JPG",
                targetSize = 1.9f
            },
            new ArtifactConfig()
            {
                id = "artifact_wayang",
                name = "Wayang Kulit Puppet",
                artist = "Dalang Puppet Maker",
                year = "c. 1100 CE",
                description = "A traditional puppet used in shadow play (Wayang Kulit) theatrical performances. Made of finely carved and painted water buffalo leather.",
                modelPath = "Assets/Artifact/Wayang/Wayang.fbx",
                baseTexPath = "Assets/Artifact/Wayang/Texture/Wayang Texture.png",
                targetSize = 0.5f
            }
        };

        foreach (var config in newArtifactsConfig)
        {
            Debug.Log($"Setting up: {config.name}");

            // 1. Process Normal Maps (Set texture type to NormalMap in AssetImporter)
            ConfigureNormalMap(config.normalTexPath);
            if (config.submeshNormals != null)
            {
                foreach (var normalPath in config.submeshNormals.Values)
                {
                    ConfigureNormalMap(normalPath);
                }
            }

            // 2. Create Material(s)
            Material mainMat = CreateMaterial(config.id, config.baseTexPath, config.normalTexPath);
            Dictionary<string, Material> submeshMaterials = new Dictionary<string, Material>();
            if (config.submeshTextures != null)
            {
                foreach (var pair in config.submeshTextures)
                {
                    string normalPath = (config.submeshNormals != null && config.submeshNormals.ContainsKey(pair.Key)) ? config.submeshNormals[pair.Key] : null;
                    submeshMaterials[pair.Key] = CreateMaterial($"{config.id}_{pair.Key}", pair.Value, normalPath);
                }
            }

            // 3. Load FBX Model & Instantiate it in the scene
            GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(config.modelPath);
            if (modelPrefab == null)
            {
                Debug.LogError($"Could not find FBX model at: {config.modelPath}");
                continue;
            }

            GameObject modelInstance = PrefabUtility.InstantiatePrefab(modelPrefab) as GameObject;
            modelInstance.name = config.name + "_ModelInstance";

            // 4. Assign Material(s) to Renderers
            Renderer[] renderers = modelInstance.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                Material[] sharedMats = renderer.sharedMaterials;
                for (int m = 0; m < sharedMats.Length; m++)
                {
                    // Match by material slot name
                    string slotName = sharedMats[m] != null ? sharedMats[m].name : "";
                    bool matchedSubmesh = false;
                    foreach (var pair in submeshMaterials)
                    {
                        if (slotName.Contains(pair.Key))
                        {
                            sharedMats[m] = pair.Value;
                            matchedSubmesh = true;
                            break;
                        }
                    }

                    if (!matchedSubmesh)
                    {
                        sharedMats[m] = mainMat;
                    }
                }
                renderer.sharedMaterials = sharedMats;
            }

            // 5. Add BoxCollider (design-time generation, so it is saved in the prefab itself)
            BoxCollider boxCol = modelInstance.GetComponent<BoxCollider>();
            if (boxCol == null)
            {
                boxCol = modelInstance.AddComponent<BoxCollider>();
            }
            Bounds localBounds = new Bounds(Vector3.zero, Vector3.zero);
            bool hasBounds = false;
            MeshFilter[] filters = modelInstance.GetComponentsInChildren<MeshFilter>(true);
            foreach (var filter in filters)
            {
                if (filter.sharedMesh != null)
                {
                    Bounds meshBounds = filter.sharedMesh.bounds;
                    
                    // Correctly transform the 8 corners of the mesh bounds to the root's local space
                    // This handles nested parent scales, rotations, and offsets accurately.
                    Vector3 center = meshBounds.center;
                    Vector3 extents = meshBounds.extents;
                    Vector3[] corners = new Vector3[8]
                    {
                        center + new Vector3(+extents.x, +extents.y, +extents.z),
                        center + new Vector3(+extents.x, +extents.y, -extents.z),
                        center + new Vector3(+extents.x, -extents.y, +extents.z),
                        center + new Vector3(+extents.x, -extents.y, -extents.z),
                        center + new Vector3(-extents.x, +extents.y, +extents.z),
                        center + new Vector3(-extents.x, +extents.y, -extents.z),
                        center + new Vector3(-extents.x, -extents.y, +extents.z),
                        center + new Vector3(-extents.x, -extents.y, -extents.z)
                    };

                    Bounds transformedBounds = new Bounds(modelInstance.transform.InverseTransformPoint(filter.transform.TransformPoint(corners[0])), Vector3.zero);
                    for (int c = 1; c < 8; c++)
                    {
                        transformedBounds.Encapsulate(modelInstance.transform.InverseTransformPoint(filter.transform.TransformPoint(corners[c])));
                    }
                    
                    if (!hasBounds)
                    {
                        localBounds = transformedBounds;
                        hasBounds = true;
                    }
                    else
                    {
                        localBounds.Encapsulate(transformedBounds);
                    }
                }
            }
            if (hasBounds)
            {
                boxCol.center = localBounds.center;
                boxCol.size = localBounds.size;
            }

            // 6. Automatically Normalize Scale (so it fits nicely in its target size for VR inspection)
            float maxDim = Mathf.Max(localBounds.size.x, Mathf.Max(localBounds.size.y, localBounds.size.z));
            if (maxDim > 0)
            {
                float scaleFactor = config.targetSize / maxDim;
                modelInstance.transform.localScale = Vector3.one * scaleFactor;
                
                // Adjust collider center/size to match the normalized root scale
                if (hasBounds)
                {
                    boxCol.center = localBounds.center;
                    boxCol.size = localBounds.size;
                }
            }
            else
            {
                modelInstance.transform.localScale = Vector3.one;
            }

            // Reset rotation and position
            modelInstance.transform.localPosition = Vector3.zero;
            modelInstance.transform.localRotation = Quaternion.identity;

            // 7. Save as Prefab
            string savedPrefabPath = $"{prefabDir}/model_{config.id}.prefab";
            GameObject finalPrefab = PrefabUtility.SaveAsPrefabAsset(modelInstance, savedPrefabPath);
            DestroyImmediate(modelInstance);

            // 7. Create/Configure ArtifactData ScriptableObject
            string dataPath = $"{museumDataDir}/artifact_{config.id}.asset";
            ArtifactData dataAsset = AssetDatabase.LoadAssetAtPath<ArtifactData>(dataPath);
            bool isNewAsset = (dataAsset == null);
            if (isNewAsset)
            {
                dataAsset = ScriptableObject.CreateInstance<ArtifactData>();
            }

            dataAsset.artifactId = config.id;
            dataAsset.artifactName = config.name;
            dataAsset.artist = config.artist;
            dataAsset.year = config.year;
            dataAsset.description = config.description;
            dataAsset.modelPrefab = finalPrefab;

            if (isNewAsset)
            {
                AssetDatabase.CreateAsset(dataAsset, dataPath);
            }
            else
            {
                EditorUtility.SetDirty(dataAsset);
            }

            // 8. Add to room's artifacts list if not already present
            if (!startingRoom.artifacts.Contains(dataAsset))
            {
                startingRoom.artifacts.Add(dataAsset);
                EditorUtility.SetDirty(startingRoom);
            }
        }

        // Save everything
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Finished setting up artifacts. Initiating QR code generation...");

        // Trigger batch QR Code Generation using the project's existing tool!
        // We open the window, which initiates downloads or we can directly invoke the batch logic.
        QRCodeGeneratorEditor.ShowWindow();
        
        Debug.Log("Artifacts setup successfully complete! Click 'Scan Project & Generate All QR Codes' in the QR Generator window to update QR assets.");
    }

    private static void ConfigureNormalMap(string texturePath)
    {
        if (string.IsNullOrEmpty(texturePath)) return;

        TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        if (importer != null && importer.textureType != TextureImporterType.NormalMap)
        {
            importer.textureType = TextureImporterType.NormalMap;
            importer.SaveAndReimport();
            Debug.Log($"Reimported texture as Normal Map: {texturePath}");
        }
    }

    private static Material CreateMaterial(string id, string baseTexPath, string normalTexPath)
    {
        string materialPath = $"Assets/Prefabs/Mat_{id}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        bool isNewMat = (mat == null);

        if (isNewMat)
        {
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null) litShader = Shader.Find("Standard");
            mat = new Material(litShader);
        }

        // Set base color map
        if (!string.IsNullOrEmpty(baseTexPath))
        {
            Texture2D baseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(baseTexPath);
            if (baseTex != null)
            {
                mat.SetTexture("_BaseMap", baseTex);
                mat.SetTexture("_MainTex", baseTex); // standard shader compatibility
            }
        }

        // Set normal map
        if (!string.IsNullOrEmpty(normalTexPath))
        {
            Texture2D normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(normalTexPath);
            if (normalTex != null)
            {
                mat.SetTexture("_BumpMap", normalTex);
                mat.EnableKeyword("_NORMALMAP");
            }
        }

        // Set URP roughness/smoothness
        mat.SetFloat("_Smoothness", 0.3f);
        mat.SetFloat("_Metallic", 0.1f);

        if (isNewMat)
        {
            AssetDatabase.CreateAsset(mat, materialPath);
        }
        else
        {
            EditorUtility.SetDirty(mat);
        }

        return mat;
    }

    private class ArtifactConfig
    {
        public string id;
        public string name;
        public string artist;
        public string year;
        public string description;
        public string modelPath;
        public string baseTexPath;
        public string normalTexPath;
        public float targetSize = 0.35f;
        public Dictionary<string, string> submeshTextures;
        public Dictionary<string, string> submeshNormals;
    }
}
