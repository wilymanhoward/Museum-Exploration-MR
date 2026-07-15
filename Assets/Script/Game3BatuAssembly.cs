using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using TMPro;

public class Game3BatuAssembly : MonoBehaviour
{
    private struct PuzzlePiece
    {
        public GameObject gameObject;
        public Vector3 targetLocalPosition;
        public string sectionName;
        public bool isSnapped;

        public PuzzlePiece(GameObject obj, Vector3 targetLocal, string name)
        {
            gameObject = obj;
            targetLocalPosition = targetLocal;
            sectionName = name;
            isSnapped = false;
        }
    }

    private List<PuzzlePiece> pieces = new List<PuzzlePiece>();
    private GameObject basePedestal;
    private GameObject finalAssembly;

    private TextMeshProUGUI titleText;
    private TextMeshProUGUI feedbackText;
    private Transform modelAnchor;
    private Material buttonMaterial;
    private List<GameObject> spawnedButtons = new List<GameObject>();

    private void Start()
    {
        titleText = transform.Find("TitleText")?.GetComponent<TextMeshProUGUI>();
        feedbackText = transform.Find("ArtistYearText")?.GetComponent<TextMeshProUGUI>();
        var descText = transform.Find("DescriptionText")?.GetComponent<TextMeshProUGUI>();
        modelAnchor = transform.Find("ModelSpawnAnchor");

        if (titleText != null) titleText.text = "PASANG BATU BERSURAT";
        if (feedbackText != null)
        {
            feedbackText.text = "Cari dan cantumkan 3 serpihan batu bersurat ke atas tapak.";
            feedbackText.fontStyle = FontStyles.Normal;
        }
        if (descText != null) descText.text = "Gunakan tangan anda untuk memegang serpihan dan dekatkan dengan kedudukan yang betul.";

        buttonMaterial = FindMaterial("Mat_Button");

        // Spawn Tutup Button on canvas
        GameObject closeBtn = CreateGameButton("Tutup", new Vector2(-60f, -80f), () => MiniGameManager.Instance.CloseActiveGame());
        spawnedButtons.Add(closeBtn);

        StartAssemblyGame();
    }

    private void StartAssemblyGame()
    {
        // Clean up previous
        CleanUpGame();

        if (modelAnchor == null) return;

        // 1. Create Base Pedestal
        basePedestal = GameObject.CreatePrimitive(PrimitiveType.Cube);
        basePedestal.name = "Batu_Base_Pedestal";
        basePedestal.transform.SetParent(modelAnchor, false);
        basePedestal.transform.localPosition = new Vector3(0, -0.25f, 0);
        basePedestal.transform.localRotation = Quaternion.identity;
        basePedestal.transform.localScale = new Vector3(0.3f, 0.05f, 0.3f);
        
        Renderer pedestalRenderer = basePedestal.GetComponent<Renderer>();
        pedestalRenderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        pedestalRenderer.material.color = new Color(0.2f, 0.2f, 0.22f); // Dark slate stone

        // 2. Create the 3 Slabs representing sections (Top, Middle, Bottom)
        // Correct target offsets relative to the anchor
        Vector3 targetBottom = new Vector3(0, -0.15f, 0);
        Vector3 targetMiddle = new Vector3(0, -0.05f, 0);
        Vector3 targetTop = new Vector3(0, 0.05f, 0);

        // Spawn them scattered around the anchor in front of user
        pieces.Add(CreatePiece(targetBottom, new Vector3(-0.15f, -0.1f, -0.1f), "Bahagian Bawah", new Color(0.5f, 0.45f, 0.4f)));
        pieces.Add(CreatePiece(targetMiddle, new Vector3(0.15f, -0.1f, -0.15f), "Bahagian Tengah", new Color(0.55f, 0.5f, 0.45f)));
        pieces.Add(CreatePiece(targetTop, new Vector3(0.0f, -0.1f, -0.2f), "Bahagian Atas", new Color(0.6f, 0.55f, 0.5f)));
    }

    private PuzzlePiece CreatePiece(Vector3 targetLocalPos, Vector3 spawnLocalPos, string sectionName, Color color)
    {
        GameObject pieceObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pieceObj.name = $"Batu_Piece_{sectionName.Replace(" ", "_")}";
        pieceObj.transform.SetParent(modelAnchor, false);
        pieceObj.transform.localPosition = spawnLocalPos;
        pieceObj.transform.localScale = new Vector3(0.2f, 0.08f, 0.15f);

        // Set visual styling to match historic rock
        Renderer r = pieceObj.GetComponent<Renderer>();
        r.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        r.material.color = color;
        r.material.SetFloat("_Smoothness", 0.1f);

        // Rigidbody for physics/grab
        Rigidbody rb = pieceObj.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;

        // XR Grab setup
        var grab = pieceObj.AddComponent<XRGrabInteractable>();
        grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
        grab.trackPosition = true; // Let them move it!
        grab.trackRotation = true;
        grab.useDynamicAttach = true;

        // Add our custom rotation driver too so they have smooth turning
        var rotationDriver = pieceObj.AddComponent<ArtifactRotationDriver>();
        rotationDriver.rotationSensitivity = 250f;

        return new PuzzlePiece(pieceObj, targetLocalPos, sectionName);
    }

    private void Update()
    {
        if (pieces.Count == 0) return;

        bool allSnapped = true;

        for (int i = 0; i < pieces.Count; i++)
        {
            var p = pieces[i];
            if (p.isSnapped) continue;

            allSnapped = false;

            // Distance to its target position
            float dist = Vector3.Distance(p.gameObject.transform.localPosition, p.targetLocalPosition);

            // Snap Threshold (6cm)
            if (dist < 0.06f)
            {
                SnapPiece(i);
            }
        }

        if (allSnapped && finalAssembly == null)
        {
            OnPuzzleCompleted();
        }
    }

    private void SnapPiece(int index)
    {
        var p = pieces[index];
        p.isSnapped = true;
        
        // Remove grabbing so it locks in place
        var grab = p.gameObject.GetComponent<XRGrabInteractable>();
        if (grab != null)
        {
            Destroy(grab);
        }
        var rotationDriver = p.gameObject.GetComponent<ArtifactRotationDriver>();
        if (rotationDriver != null)
        {
            Destroy(rotationDriver);
        }
        var rb = p.gameObject.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Destroy(rb);
        }

        // Lock exactly into target offset
        p.gameObject.transform.localPosition = p.targetLocalPosition;
        p.gameObject.transform.localRotation = Quaternion.identity;

        // Set visual indicator (turns slightly green/gold highlight before full assembly)
        Renderer r = p.gameObject.GetComponent<Renderer>();
        if (r != null)
        {
            r.material.color = new Color(0.4f, 0.8f, 0.5f); // Soft green success highlight
        }

        if (feedbackText != null)
        {
            feedbackText.text = $"<color=#00CC88>Cantum! {p.sectionName} berjaya dipasang.</color>";
        }

        pieces[index] = p; // Save state
    }

    private void OnPuzzleCompleted()
    {
        // 1. Destroy the scattered blocks
        foreach (var p in pieces)
        {
            Destroy(p.gameObject);
        }
        pieces.Clear();

        if (basePedestal != null) Destroy(basePedestal);

        // 2. Spawn the final real Batu Bersurat prefab
        GameObject targetPrefab = null;
        if (MuseumManager.Instance != null)
        {
            // Find "artifact_batu" in current configurations to get the real model
            foreach (var room in MuseumManager.Instance.rooms)
            {
                var match = room.artifacts.Find(a => a.artifactId == "artifact_batu");
                if (match != null)
                {
                    targetPrefab = match.modelPrefab;
                    break;
                }
            }
        }

        if (targetPrefab != null && modelAnchor != null)
        {
            finalAssembly = Instantiate(targetPrefab, modelAnchor.position, modelAnchor.rotation, modelAnchor);
            finalAssembly.transform.localPosition = Vector3.zero;
            finalAssembly.transform.localRotation = Quaternion.identity;

            // Scale to look premium
            Vector3 canvasScale = transform.localScale;
            Vector3 prefabScale = targetPrefab.transform.localScale;
            finalAssembly.transform.localScale = new Vector3(
                canvasScale.x != 0 ? prefabScale.x / canvasScale.x : prefabScale.x,
                canvasScale.y != 0 ? prefabScale.y / canvasScale.y : prefabScale.y,
                canvasScale.z != 0 ? prefabScale.z / canvasScale.z : prefabScale.z
            ) * 1.1f;

            // Make it spin slowly as a victory highlight
            var spinner = finalAssembly.AddComponent<Spinner>();
            spinner.spinSpeed = 25f;

            // Add gold outline or glowing effect if shader allows
            Renderer[] renderers = finalAssembly.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                r.material.EnableKeyword("_EMISSION");
                r.material.SetColor("_EmissionColor", new Color(0.1f, 0.08f, 0.02f)); // Soft golden glow
            }
        }

        // 3. Update Text
        if (feedbackText != null)
        {
            feedbackText.text = "<color=#E8B000>★ Lengkap! Batu Bersurat Berjaya Dicantumkan! ★</color>";
        }
        var descText = transform.Find("DescriptionText")?.GetComponent<TextMeshProUGUI>();
        if (descText != null)
        {
            descText.text = "Tahniah! Anda telah berjaya menyusun semula Batu Bersurat Terengganu (1303 Masihi) yang menjadi khazanah sejarah penting negara.";
        }

        // Mark on checklist
        if (MuseumManager.Instance != null)
        {
            MuseumManager.Instance.MarkArtifactInteracted("artifact_batu");
        }

        // Add Replay Button
        foreach (var btn in spawnedButtons) Destroy(btn);
        spawnedButtons.Clear();

        GameObject replayBtn = CreateGameButton("Main Lagi", new Vector2(-60f, -80f), () => {
            if (finalAssembly != null) Destroy(finalAssembly);
            StartAssemblyGame();
            if (feedbackText != null) feedbackText.text = "Cari dan cantumkan 3 serpihan batu bersurat ke atas tapak.";
            if (descText != null) descText.text = "Gunakan tangan anda untuk memegang serpihan dan dekatkan dengan kedudukan yang betul.";
        });
        spawnedButtons.Add(replayBtn);

        GameObject closeBtn = CreateGameButton("Tutup", new Vector2(100f, -80f), () => MiniGameManager.Instance.CloseActiveGame());
        spawnedButtons.Add(closeBtn);
    }

    private GameObject CreateGameButton(string label, Vector2 anchoredPosition, UnityEngine.Events.UnityAction onClickAction)
    {
        GameObject buttonObj = new GameObject("GameButton");
        buttonObj.transform.SetParent(transform, false);

        RectTransform rect = buttonObj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(140f, 36f);
        rect.anchoredPosition = anchoredPosition;

        Image img = buttonObj.AddComponent<Image>();
        if (buttonMaterial != null)
        {
            img.material = buttonMaterial;
        }
        img.color = new Color(0.9f, 0.9f, 0.93f, 0.8f);

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(buttonObj.transform, false);
        TextMeshProUGUI txt = textObj.AddComponent<TextMeshProUGUI>();
        txt.text = label;
        txt.fontSize = 12;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = new Color(0.1f, 0.1f, 0.15f);

        RectTransform txtRect = textObj.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.sizeDelta = Vector2.zero;

        XRButtonSelection select = buttonObj.AddComponent<XRButtonSelection>();
        select.buttonImage = img;
        select.scaleTarget = buttonObj.transform;

        BoxCollider col = buttonObj.AddComponent<BoxCollider>();
        col.size = new Vector3(140f, 36f, 15f);
        col.isTrigger = true;

        select.onClick.AddListener(onClickAction);
        return buttonObj;
    }

    private Material FindMaterial(string matName)
    {
        Material[] mats = Resources.FindObjectsOfTypeAll<Material>();
        foreach (Material m in mats)
        {
            if (m.name == matName) return m;
        }
        return null;
    }

    private void CleanUpGame()
    {
        foreach (var p in pieces)
        {
            if (p.gameObject != null) Destroy(p.gameObject);
        }
        pieces.Clear();

        if (basePedestal != null) Destroy(basePedestal);
        if (finalAssembly != null) Destroy(finalAssembly);
    }

    private void OnDestroy()
    {
        CleanUpGame();
    }
}
