using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit;

public class Game1GuessName : MonoBehaviour
{
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI questionText;
    private Transform modelAnchor;
    
    private ArtifactData correctAnswer;
    private GameObject spawnedModel;
    private List<GameObject> spawnedButtons = new List<GameObject>();
    private Material buttonMaterial;

    private void Start()
    {
        // 1. Resolve UI references from the spawned template
        titleText = transform.Find("TitleText")?.GetComponent<TextMeshProUGUI>();
        questionText = transform.Find("ArtistYearText")?.GetComponent<TextMeshProUGUI>();
        var descText = transform.Find("DescriptionText")?.GetComponent<TextMeshProUGUI>();
        modelAnchor = transform.Find("ModelSpawnAnchor");

        if (titleText != null) titleText.text = "TEKA NAMA ARTEFAK";
        if (questionText != null)
        {
            questionText.text = "Apakah nama objek 3D di sebelah kanan?";
            questionText.fontStyle = FontStyles.Normal;
        }
        if (descText != null) descText.text = ""; // Clear description field

        // Find the button material from active memory
        buttonMaterial = FindMaterial("Mat_Button");

        // 2. Select a random artifact
        SelectRandomArtifactAndSetup();
    }

    private void SelectRandomArtifactAndSetup()
    {
        // Clean up previous
        if (spawnedModel != null) Destroy(spawnedModel);
        foreach (var btn in spawnedButtons) Destroy(btn);
        spawnedButtons.Clear();

        if (RoomManager.Instance == null || RoomManager.Instance.rooms.Count == 0)
        {
            if (titleText != null) titleText.text = "No Artifacts Configured!";
            return;
        }

        // Get all unique artifacts
        List<ArtifactData> allArtifacts = new List<ArtifactData>();
        foreach (var room in RoomManager.Instance.rooms)
        {
            foreach (var art in room.artifacts)
            {
                if (art != null && !allArtifacts.Contains(art))
                {
                    allArtifacts.Add(art);
                }
            }
        }

        if (allArtifacts.Count == 0)
        {
            if (titleText != null) titleText.text = "No Artifacts Found!";
            return;
        }

        // Choose correct answer
        correctAnswer = allArtifacts[Random.Range(0, allArtifacts.Count)];

        // Spawn 3D Model
        if (modelAnchor != null && correctAnswer.modelPrefab != null)
        {
            spawnedModel = Instantiate(correctAnswer.modelPrefab, modelAnchor.position, modelAnchor.rotation, modelAnchor);
            spawnedModel.transform.localPosition = Vector3.zero;
            spawnedModel.transform.localRotation = Quaternion.identity;
            
            // Compensate for Canvas scale
            Vector3 canvasScale = transform.localScale;
            Vector3 prefabScale = correctAnswer.modelPrefab.transform.localScale;
            spawnedModel.transform.localScale = new Vector3(
                canvasScale.x != 0 ? prefabScale.x / canvasScale.x : prefabScale.x,
                canvasScale.y != 0 ? prefabScale.y / canvasScale.y : prefabScale.y,
                canvasScale.z != 0 ? prefabScale.z / canvasScale.z : prefabScale.z
            ) * 0.8f; // slightly smaller for game UI

            // Make it spin slowly
            var spinner = spawnedModel.AddComponent<Spinner>();
            spinner.spinSpeed = 30f;
        }

        // Gather options (1 correct, 2 wrong)
        List<string> options = new List<string> { correctAnswer.artifactName };
        List<ArtifactData> wrongPool = new List<ArtifactData>(allArtifacts);
        wrongPool.Remove(correctAnswer);

        // Add 2 wrong options if available
        for (int i = 0; i < 2; i++)
        {
            if (wrongPool.Count > 0)
            {
                int rIndex = Random.Range(0, wrongPool.Count);
                options.Add(wrongPool[rIndex].artifactName);
                wrongPool.RemoveAt(rIndex);
            }
        }

        // Shuffle options
        ShuffleList(options);

        // Spawn buttons (stacked vertically on the left side)
        float startY = 10f;
        float spacingY = -55f;

        for (int i = 0; i < options.Count; i++)
        {
            string optionText = options[i];
            GameObject buttonObj = CreateGameButton(optionText, new Vector2(-60f, startY + (i * spacingY)), () => OnOptionSelected(optionText));
            spawnedButtons.Add(buttonObj);
        }
    }

    private void OnOptionSelected(string chosenName)
    {
        bool isCorrect = (chosenName == correctAnswer.artifactName);
        
        if (isCorrect)
        {
            if (questionText != null) questionText.text = "<color=#00CC88>Betul! Tahniah!</color>";
            // Mark as interacted on checklist
            if (RoomManager.Instance != null)
            {
                RoomManager.Instance.MarkArtifactInteracted(correctAnswer.artifactId);
            }
        }
        else
        {
            if (questionText != null) questionText.text = $"<color=#FF4444>Salah! Jawapan betul: {correctAnswer.artifactName}</color>";
        }

        // Clear buttons and spawn a "Next" button
        foreach (var btn in spawnedButtons) Destroy(btn);
        spawnedButtons.Clear();

        GameObject nextBtn = CreateGameButton("Main Lagi", new Vector2(-60f, -40f), SelectRandomArtifactAndSetup);
        spawnedButtons.Add(nextBtn);

        // Add Close Button
        GameObject closeBtn = CreateGameButton("Tutup", new Vector2(-60f, -100f), () => MiniGameManager.Instance.CloseActiveGame());
        spawnedButtons.Add(closeBtn);
    }

    private GameObject CreateGameButton(string label, Vector2 anchoredPosition, UnityEngine.Events.UnityAction onClickAction)
    {
        GameObject buttonObj = new GameObject("GameButton");
        buttonObj.transform.SetParent(transform, false);

        RectTransform rect = buttonObj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(210f, 40f);
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
        txt.fontSize = 14;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = new Color(0.1f, 0.1f, 0.15f);

        RectTransform txtRect = textObj.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.sizeDelta = Vector2.zero;

        // Interaction
        XRButtonSelection select = buttonObj.AddComponent<XRButtonSelection>();
        select.buttonImage = img;
        select.scaleTarget = buttonObj.transform;

        BoxCollider col = buttonObj.AddComponent<BoxCollider>();
        col.size = new Vector3(210f, 40f, 15f);
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

    private void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int r = Random.Range(0, i + 1);
            T tmp = list[i];
            list[i] = list[r];
            list[r] = tmp;
        }
    }
}

// Helper component to rotate objects
public class Spinner : MonoBehaviour
{
    public float spinSpeed = 20f;
    private void Update()
    {
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
    }
}
