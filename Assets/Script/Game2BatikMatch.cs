using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using TMPro;

public class Game2BatikMatch : MonoBehaviour
{
    public class BatikBlock : MonoBehaviour
    {
        public int correctStepIndex; // 0, 1, 2, 3
        public int currentSlotIndex;
        public Vector3 targetLocalPosition;
        public bool isGrabbed;

        private XRGrabInteractable grabInteractable;
        private Game2BatikMatch controller;

        public void Setup(Game2BatikMatch gameController, int stepIndex, int startSlot, Mesh blockMesh, Material blockMat)
        {
            controller = gameController;
            correctStepIndex = stepIndex;
            currentSlotIndex = startSlot;

            // 1. Setup Mesh Filter and Renderer
            var filter = gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = blockMesh;

            var renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = blockMat;

            // 2. Add Collider matching mesh bounds (in Canvas Units)
            var boxCol = gameObject.AddComponent<BoxCollider>();
            boxCol.size = new Vector3(180f, 32f, 10f);

            // 3. Add Rigidbody for XR Interaction
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = true;

            // 4. Setup XR Grab Interactable
            grabInteractable = gameObject.AddComponent<XRGrabInteractable>();
            grabInteractable.movementType = XRBaseInteractable.MovementType.Instantaneous;
            grabInteractable.trackPosition = true;
            grabInteractable.trackRotation = false; // Keep it upright so text is readable
            grabInteractable.useDynamicAttach = true;

            grabInteractable.selectEntered.AddListener(OnGrabbed);
            grabInteractable.selectExited.AddListener(OnReleased);
        }

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            isGrabbed = true;
            controller.OnBlockGrabbed(this);
        }

        private void OnReleased(SelectExitEventArgs args)
        {
            isGrabbed = false;
            controller.OnBlockReleased(this);
        }

        private void Update()
        {
            if (!isGrabbed)
            {
                // Smoothly slide block into its assigned slot position (in Canvas Units)
                transform.localPosition = Vector3.Lerp(transform.localPosition, targetLocalPosition, Time.deltaTime * 10f);
                transform.localRotation = Quaternion.Slerp(transform.localRotation, Quaternion.identity, Time.deltaTime * 10f);
            }
        }

        private void OnDestroy()
        {
            if (grabInteractable != null)
            {
                grabInteractable.selectEntered.RemoveListener(OnGrabbed);
                grabInteractable.selectExited.RemoveListener(OnReleased);
            }
        }
    }

    private struct BatikStep
    {
        public int stepIndex;
        public string stepName;
        public string stepDesc;

        public BatikStep(int index, string name, string desc)
        {
            stepIndex = index;
            stepName = name;
            stepDesc = desc;
        }
    }

    private List<BatikStep> stepsData = new List<BatikStep>();
    private BatikBlock[] slots = new BatikBlock[4];
    private GameObject[] socketVisuals = new GameObject[4];
    private List<GameObject> spawnedButtons = new List<GameObject>();

    private TextMeshProUGUI titleText;
    private TextMeshProUGUI feedbackText;
    private Material buttonMaterial;
    
    // Countdown state (30 seconds limit)
    private const float TotalGameTime = 30.0f;
    private float remainingTime = TotalGameTime;
    private bool isGameOver = false;

    // Donut UI References
    private GameObject donutContainer;
    private Image donutImage;
    private TextMeshProUGUI donutText;
    private Texture2D generatedRingTexture;

    // Board parameters (defined in Canvas Units: Canvas is 240x320)
    private Vector3[] slotLocalPositions = new Vector3[]
    {
        new Vector3(0f, 65f, -6f),
        new Vector3(0f, 20f, -6f),
        new Vector3(0f, -25f, -6f),
        new Vector3(0f, -70f, -6f)
    };

    private void Start()
    {
        // 1. Setup Header Texts on Canvas
        titleText = transform.Find("TitleText")?.GetComponent<TextMeshProUGUI>();
        feedbackText = transform.Find("ArtistYearText")?.GetComponent<TextMeshProUGUI>();
        var descText = transform.Find("DescriptionText")?.GetComponent<TextMeshProUGUI>();

        if (titleText != null) titleText.text = "PROSES BATIK";
        if (feedbackText != null)
        {
            feedbackText.text = "Heret bongkah mengikut susunan.";
            feedbackText.fontStyle = FontStyles.Normal;
        }
        if (descText != null) descText.text = ""; // clear description text area

        buttonMaterial = FindMaterial("Mat_Button");

        // 2. Initialize Batik Steps Data
        stepsData.Clear();
        stepsData.Add(new BatikStep(0, "1. Canting (Lilin)", "Melukis corak kain menggunakan lilin cair."));
        stepsData.Add(new BatikStep(1, "2. Mewarna (Dyeing)", "Menyapu warna pada kain tanpa lilin."));
        stepsData.Add(new BatikStep(2, "3. Merebus (Boiling)", "Merebus kain untuk melarutkan lilin."));
        stepsData.Add(new BatikStep(3, "4. Kering (Drying)", "Membasuh bersih dan menjemur kain."));

        // 3. Setup Donut Timer UI
        CreateDonutTimerUI();

        // 4. Initialize 3D Play Area
        InitializeGameArea();
    }

    private void CreateDonutTimerUI()
    {
        // 1. Container for Donut Timer (Top Right of Panel)
        donutContainer = new GameObject("DonutTimer");
        donutContainer.transform.SetParent(transform, false);
        
        RectTransform donutRect = donutContainer.AddComponent<RectTransform>();
        donutRect.sizeDelta = new Vector2(36f, 36f);
        donutRect.anchoredPosition = new Vector2(88f, 125f); // Top Right corner

        // 2. Background Circle (subtle dark backing)
        GameObject bgCircle = new GameObject("BackgroundCircle");
        bgCircle.transform.SetParent(donutContainer.transform, false);
        RectTransform bgRect = bgCircle.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

        Image bgImage = bgCircle.AddComponent<Image>();
        generatedRingTexture = CreateRingTexture(128, 16);
        Sprite ringSprite = Sprite.Create(generatedRingTexture, new Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f));
        bgImage.sprite = ringSprite;
        bgImage.color = new Color(0.1f, 0.1f, 0.12f, 0.4f); // Muted dark background ring

        // 3. Foreground Fill Circle (the one that drains)
        GameObject fillCircle = new GameObject("FillCircle");
        fillCircle.transform.SetParent(donutContainer.transform, false);
        RectTransform fillRect = fillCircle.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.sizeDelta = Vector2.zero;

        donutImage = fillCircle.AddComponent<Image>();
        donutImage.sprite = ringSprite;
        donutImage.color = new Color(0f, 0.8f, 0.5f, 1f); // Vibrant emerald green initially
        donutImage.type = Image.Type.Filled;
        donutImage.fillMethod = Image.FillMethod.Radial360;
        donutImage.fillOrigin = (int)Image.Origin360.Top;
        donutImage.fillClockwise = false; // anti-clockwise drain

        // 4. Countdown Text in center of Donut
        GameObject textObj = new GameObject("CountdownText");
        textObj.transform.SetParent(donutContainer.transform, false);
        
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;

        donutText = textObj.AddComponent<TextMeshProUGUI>();
        donutText.text = "30";
        donutText.fontSize = 11;
        donutText.fontStyle = FontStyles.Bold;
        donutText.alignment = TextAlignmentOptions.Center;
        donutText.color = new Color(0.1f, 0.1f, 0.15f);
    }

    private void Update()
    {
        if (isGameOver) return;

        remainingTime -= Time.deltaTime;
        
        if (remainingTime <= 0f)
        {
            remainingTime = 0f;
            TriggerLoss();
        }

        // Update Donut fill and text
        if (donutImage != null)
        {
            donutImage.fillAmount = remainingTime / TotalGameTime;

            // Shift colors depending on time left
            if (remainingTime > 15f)
            {
                donutImage.color = new Color(0f, 0.8f, 0.5f, 1f); // Green
            }
            else if (remainingTime > 6f)
            {
                donutImage.color = new Color(0.9f, 0.6f, 0f, 1f); // Orange/Yellow
            }
            else
            {
                donutImage.color = new Color(0.9f, 0.2f, 0.2f, 1f); // Red (Urgent)
            }
        }

        if (donutText != null)
        {
            donutText.text = Mathf.CeilToInt(remainingTime).ToString();
        }
    }

    private void InitializeGameArea()
    {
        // Clear old buttons
        foreach (var btn in spawnedButtons) Destroy(btn);
        spawnedButtons.Clear();

        // Recreate close button
        GameObject closeBtn = CreateGameButton("Tutup Game", new Vector2(0f, -125f), () => MiniGameManager.Instance.CloseActiveGame());
        spawnedButtons.Add(closeBtn);

        // Generate Meshes with corrected dimensions for all 6 faces to prevent weird stretching!
        Mesh blockMesh = CreateRoundedBox(180f, 32f, 10f, 5f, 8);
        Mesh socketMesh = CreateRoundedBox(184f, 36f, 2f, 5f, 4);

        // Create Materials
        Material socketMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        socketMat.color = new Color(0.12f, 0.12f, 0.15f, 0.9f); // Slate dark backplate
        socketMat.SetFloat("_Smoothness", 0.2f);

        Material blockMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        blockMat.color = new Color(0.95f, 0.95f, 0.97f, 1f); // Sleek white glass block
        blockMat.SetFloat("_Smoothness", 0.6f);

        // Create Sockets (Static slot visual guides)
        for (int i = 0; i < 4; i++)
        {
            GameObject socketObj = new GameObject($"Batik_Socket_{i}");
            socketObj.transform.SetParent(transform, false);
            socketObj.transform.localPosition = slotLocalPositions[i] + new Vector3(0, 0, 4f); // place slightly behind blocks
            socketObj.transform.localRotation = Quaternion.identity;

            socketObj.AddComponent<MeshFilter>().sharedMesh = socketMesh;
            socketObj.AddComponent<MeshRenderer>().sharedMaterial = socketMat;

            socketVisuals[i] = socketObj;
        }

        // Shuffle steps starting order
        List<int> stepIndexes = new List<int> { 0, 1, 2, 3 };
        while (IsIndicesInCorrectOrder(stepIndexes))
        {
            ShuffleList(stepIndexes);
        }

        // Spawn Interactive Blocks
        for (int i = 0; i < 4; i++)
        {
            int stepIndex = stepIndexes[i];
            BatikStep step = stepsData[stepIndex];

            GameObject blockObj = new GameObject($"Batik_Block_{stepIndex}");
            blockObj.transform.SetParent(transform, false);
            blockObj.transform.localPosition = slotLocalPositions[i];
            blockObj.transform.localRotation = Quaternion.identity;

            BatikBlock blockComponent = blockObj.AddComponent<BatikBlock>();
            blockComponent.Setup(this, stepIndex, i, blockMesh, blockMat);
            blockComponent.targetLocalPosition = slotLocalPositions[i];

            // Add UI Text Canvas directly on the block's front face (size matches block in Canvas Units)
            GameObject blockCanvasObj = new GameObject("BlockCanvas");
            blockCanvasObj.transform.SetParent(blockObj.transform, false);
            blockCanvasObj.transform.localPosition = new Vector3(0f, 0f, -5.1f); // offset forward to avoid z-fighting
            blockCanvasObj.transform.localRotation = Quaternion.identity;

            Canvas canvas = blockCanvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            
            RectTransform canvasRect = blockCanvasObj.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(180, 32);
            blockCanvasObj.transform.localScale = Vector3.one; // scale is 1:1 because it uses Canvas Units!

            // Title Text on Block
            GameObject textObj = new GameObject("TitleText");
            textObj.transform.SetParent(blockCanvasObj.transform, false);
            TextMeshProUGUI textComp = textObj.AddComponent<TextMeshProUGUI>();
            textComp.text = $"<b>{step.stepName}</b>";
            textComp.fontSize = 11;
            textComp.color = new Color(0.1f, 0.1f, 0.15f);
            textComp.alignment = TextAlignmentOptions.Center;

            RectTransform titleRect = textObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 0.45f);
            titleRect.anchorMax = new Vector2(1f, 0.95f);
            titleRect.sizeDelta = Vector2.zero;

            // Description Text on Block
            GameObject descObj = new GameObject("DescText");
            descObj.transform.SetParent(blockCanvasObj.transform, false);
            TextMeshProUGUI descComp = descObj.AddComponent<TextMeshProUGUI>();
            descComp.text = step.stepDesc;
            descComp.fontSize = 7.5f;
            descComp.color = new Color(0.3f, 0.3f, 0.35f);
            descComp.alignment = TextAlignmentOptions.Center;
            descComp.enableWordWrapping = true;

            RectTransform descRect = descObj.GetComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0f, 0.05f);
            descRect.anchorMax = new Vector2(1f, 0.45f);
            descRect.sizeDelta = Vector2.zero;

            slots[i] = blockComponent;
        }

        UpdateSocketGlows();
    }

    public void OnBlockGrabbed(BatikBlock block)
    {
    }

    public void OnBlockReleased(BatikBlock block)
    {
        if (isGameOver) return;

        // 1. Find the closest slot based on current local position (in Canvas Units)
        int closestSlotIndex = 0;
        float minDist = float.MaxValue;

        for (int i = 0; i < 4; i++)
        {
            float dist = Vector3.Distance(block.transform.localPosition, slotLocalPositions[i]);
            if (dist < minDist)
            {
                minDist = dist;
                closestSlotIndex = i;
            }
        }

        int previousSlotIndex = block.currentSlotIndex;

        // 2. Perform Swap if the slot is different
        if (closestSlotIndex != previousSlotIndex)
        {
            BatikBlock blockToSwap = slots[closestSlotIndex];

            // Reassign slots
            slots[closestSlotIndex] = block;
            block.currentSlotIndex = closestSlotIndex;
            block.targetLocalPosition = slotLocalPositions[closestSlotIndex];

            slots[previousSlotIndex] = blockToSwap;
            blockToSwap.currentSlotIndex = previousSlotIndex;
            blockToSwap.targetLocalPosition = slotLocalPositions[previousSlotIndex];

            Debug.Log($"Swapped Block {block.correctStepIndex} (to Slot {closestSlotIndex}) with Block {blockToSwap.correctStepIndex} (to Slot {previousSlotIndex})");
        }
        else
        {
            // Reset to its correct slot target position
            block.targetLocalPosition = slotLocalPositions[previousSlotIndex];
        }

        // 3. Update slot glow indicators
        UpdateSocketGlows();

        // 4. Check Win State
        CheckWinCondition();
    }

    private void UpdateSocketGlows()
    {
        for (int i = 0; i < 4; i++)
        {
            if (slots[i] == null || socketVisuals[i] == null) continue;

            Renderer socketRenderer = socketVisuals[i].GetComponent<Renderer>();
            if (socketRenderer != null)
            {
                if (slots[i].correctStepIndex == i)
                {
                    // Correct slot -> Glow emerald green
                    socketRenderer.material.color = new Color(0.1f, 0.4f, 0.25f, 0.9f);
                }
                else
                {
                    // Incorrect slot -> Normal dark slate
                    socketRenderer.material.color = new Color(0.12f, 0.12f, 0.15f, 0.9f);
                }
            }
        }
    }

    private void CheckWinCondition()
    {
        bool correct = true;
        for (int i = 0; i < 4; i++)
        {
            if (slots[i].correctStepIndex != i)
            {
                correct = false;
                break;
            }
        }

        if (correct)
        {
            isGameOver = true;
            float solveTime = TotalGameTime - remainingTime;
            
            if (feedbackText != null)
            {
                feedbackText.text = $"<color=#00CC88>★ Selesai! Masa: {solveTime:F1}s! ★</color>";
            }

            // Lock all blocks (remove grab interactability so they can't be dragged anymore)
            for (int i = 0; i < 4; i++)
            {
                var grab = slots[i].gameObject.GetComponent<XRGrabInteractable>();
                if (grab != null) Destroy(grab);

                // Change block color to golden/glowing visual
                Renderer r = slots[i].gameObject.GetComponent<Renderer>();
                if (r != null)
                {
                    r.material.color = new Color(0.95f, 0.85f, 0.4f, 1f); // Shiny gold
                }
            }

            // Celebration visual effects and audio
            SpawnConfetti();
            PlayCelebrationSound();

            // Mark Batik Fabric completed on checklist
            if (MuseumManager.Instance != null)
            {
                MuseumManager.Instance.MarkArtifactInteracted("artifact_batik");
            }

            ShowEndGameOptions(true);
        }
    }

    private void TriggerLoss()
    {
        isGameOver = true;
        
        if (feedbackText != null)
        {
            feedbackText.text = "<color=#FF4444>Masa Tamat! Cuba lagi.</color>";
        }

        // Lock blocks (remove grab interactability)
        for (int i = 0; i < 4; i++)
        {
            var grab = slots[i].gameObject.GetComponent<XRGrabInteractable>();
            if (grab != null) Destroy(grab);

            Renderer r = slots[i].gameObject.GetComponent<Renderer>();
            if (r != null)
            {
                r.material.color = new Color(0.8f, 0.3f, 0.3f, 1f); // Dull red
            }
        }

        // Play a low-pitch synth buzz to indicate failure
        PlayLossSound();

        ShowEndGameOptions(false);
    }

    private void ShowEndGameOptions(bool won)
    {
        // Clear current buttons
        foreach (var btn in spawnedButtons) Destroy(btn);
        spawnedButtons.Clear();

        // Add Retray (Main Lagi) and Close (Tutup) buttons side-by-side
        GameObject replayBtn = CreateGameButton("Main Lagi", new Vector2(-55f, -125f), ResetAndRestartGame);
        spawnedButtons.Add(replayBtn);

        GameObject closeBtn = CreateGameButton("Tutup", new Vector2(55f, -125f), () => MiniGameManager.Instance.CloseActiveGame());
        spawnedButtons.Add(closeBtn);
    }

    private void ResetAndRestartGame()
    {
        // Clean up previous blocks and sockets
        CleanUpGame();

        // Reset timer
        remainingTime = TotalGameTime;
        isGameOver = false;

        // Reset text
        if (feedbackText != null) feedbackText.text = "Heret bongkah mengikut susunan.";

        // Restart area
        InitializeGameArea();
    }

    private Texture2D CreateRingTexture(int size, int thickness)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = size * 0.5f;
        float outerRad = size * 0.5f;
        float innerRad = outerRad - thickness;
        
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                
                if (dist >= innerRad && dist <= outerRad)
                {
                    // Basic antialiasing on edges
                    float alpha = 1.0f;
                    if (dist > outerRad - 1.5f) alpha = (outerRad - dist) / 1.5f;
                    else if (dist < innerRad + 1.5f) alpha = (dist - innerRad) / 1.5f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
                else
                {
                    tex.SetPixel(x, y, Color.clear);
                }
            }
        }
        tex.Apply();
        return tex;
    }

    private void SpawnConfetti()
    {
        GameObject confettiObj = new GameObject("Confetti");
        confettiObj.transform.position = transform.position + new Vector3(0f, 0.15f, -0.05f); // Spawn slightly above and in front of the card
        
        ParticleSystem ps = confettiObj.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 3.0f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.0f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.03f); // small squares
        main.gravityModifier = 0.5f; // fall down slowly
        main.loop = false;
        main.playOnAwake = false;

        // Particle shape: Cone pointing upwards/forwards
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 35f;
        shape.radius = 0.1f;
        shape.rotation = new Vector3(-60f, 0f, 0f); // point forward/up

        // Emission: Burst of particles
        var emission = ps.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, 80) });

        // Color over lifetime (colorful confetti!)
        var colorLifecycle = ps.colorOverLifetime;
        colorLifecycle.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(Color.red, 0.0f),
                new GradientColorKey(Color.yellow, 0.25f),
                new GradientColorKey(Color.green, 0.5f),
                new GradientColorKey(Color.cyan, 0.75f),
                new GradientColorKey(Color.magenta, 1.0f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1.0f, 0.0f),
                new GradientAlphaKey(1.0f, 0.7f),
                new GradientAlphaKey(0.0f, 1.0f) // fade out at the end
            }
        );
        colorLifecycle.color = grad;

        // Noise to make it flutter!
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.3f;
        noise.frequency = 1.5f;

        // Force Burst
        ps.Play();
        
        // Auto-destroy after 3 seconds
        Destroy(confettiObj, 4.0f);
    }

    private void PlayCelebrationSound()
    {
        GameObject audioObj = new GameObject("CelebrationSound");
        AudioSource source = audioObj.AddComponent<AudioSource>();
        source.spatialBlend = 0.0f; // 2D sound for clarity
        source.volume = 0.6f;

        // Generate a beautiful C-Major chord chime (C5-E5-G5-C6) procedurally
        int sampleRate = 44100;
        float duration = 1.0f;
        int numSamples = (int)(sampleRate * duration);
        float[] samples = new float[numSamples];

        float[] freqs = new float[] { 523.25f, 659.25f, 783.99f, 1046.50f }; // C5, E5, G5, C6

        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = Mathf.Clamp01(1.0f - t / duration);
            
            float val = 0f;
            foreach (float f in freqs)
            {
                val += Mathf.Sin(2f * Mathf.PI * f * t);
            }
            samples[i] = (val / freqs.Length) * envelope;
        }

        AudioClip clip = AudioClip.Create("Chime", numSamples, 1, sampleRate, false);
        clip.SetData(samples, 0);

        source.PlayOneShot(clip);
        Destroy(audioObj, 2.0f); // clean up
    }

    private void PlayLossSound()
    {
        GameObject audioObj = new GameObject("LossSound");
        AudioSource source = audioObj.AddComponent<AudioSource>();
        source.spatialBlend = 0.0f;
        source.volume = 0.5f;

        // Generate a low-pitch failing buzz sound (sawtooth-like decay)
        int sampleRate = 44100;
        float duration = 0.6f;
        int numSamples = (int)(sampleRate * duration);
        float[] samples = new float[numSamples];

        float freq = 120.0f; // low buzz frequency (B2/C3 range)

        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = Mathf.Clamp01(1.0f - t / duration);
            
            // Sawtooth wave approximation using basic math
            float val = 2.0f * (t * freq - Mathf.Floor(t * freq + 0.5f));
            samples[i] = val * envelope;
        }

        AudioClip clip = AudioClip.Create("Buzz", numSamples, 1, sampleRate, false);
        clip.SetData(samples, 0);

        source.PlayOneShot(clip);
        Destroy(audioObj, 1.5f);
    }

    private Mesh CreateRoundedBox(float w, float h, float d, float r, int subdivisions)
    {
        Mesh mesh = new Mesh();
        mesh.name = "RoundedBox";

        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();

        // Generate 6 faces of the rounded box with corrected orientation math
        AddFace(vertices, normals, uvs, triangles, Vector3.forward, Vector3.up, Vector3.right, w, h, d, r, subdivisions);
        AddFace(vertices, normals, uvs, triangles, Vector3.back, Vector3.up, Vector3.left, w, h, d, r, subdivisions);
        AddFace(vertices, normals, uvs, triangles, Vector3.up, Vector3.forward, Vector3.right, w, d, h, r, subdivisions);
        AddFace(vertices, normals, uvs, triangles, Vector3.down, Vector3.back, Vector3.right, w, d, h, r, subdivisions);
        AddFace(vertices, normals, uvs, triangles, Vector3.right, Vector3.up, Vector3.back, d, h, w, r, subdivisions);
        AddFace(vertices, normals, uvs, triangles, Vector3.left, Vector3.up, Vector3.forward, d, h, w, r, subdivisions);

        mesh.vertices = vertices.ToArray();
        mesh.normals = normals.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.triangles = triangles.ToArray();

        return mesh;
    }

    private void AddFace(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs, List<int> triangles,
                         Vector3 normal, Vector3 up, Vector3 right, 
                         float width, float height, float depth, float r, int sub)
    {
        int startVertIndex = vertices.Count;
        Vector3 faceCenter = normal * (depth * 0.5f);

        for (int y = 0; y <= sub; y++)
        {
            float vFactor = (float)y / sub - 0.5f;
            Vector3 vOffset = up * (vFactor * height);

            for (int x = 0; x <= sub; x++)
            {
                float uFactor = (float)x / sub - 0.5f;
                Vector3 uOffset = right * (uFactor * width);

                Vector3 originalPos = faceCenter + uOffset + vOffset;

                // Perform rounding mathematical projection
                Vector3 inner = new Vector3(
                    Mathf.Max(0, width * 0.5f - r),
                    Mathf.Max(0, height * 0.5f - r),
                    Mathf.Max(0, depth * 0.5f - r)
                );

                Vector3 clamped = new Vector3(
                    Mathf.Clamp(originalPos.x, -inner.x, inner.x),
                    Mathf.Clamp(originalPos.y, -inner.y, inner.y),
                    Mathf.Clamp(originalPos.z, -inner.z, inner.z)
                );

                Vector3 diff = originalPos - clamped;
                Vector3 finalPos;
                Vector3 finalNormal;

                if (diff.sqrMagnitude > 0.00001f)
                {
                    Vector3 dir = diff.normalized;
                    finalPos = clamped + dir * r;
                    finalNormal = dir;
                }
                else
                {
                    finalPos = originalPos;
                    finalNormal = normal;
                }

                vertices.Add(finalPos);
                normals.Add(finalNormal);
                uvs.Add(new Vector2((float)x / sub, (float)y / sub));
            }
        }

        int rowSize = sub + 1;
        for (int y = 0; y < sub; y++)
        {
            for (int x = 0; x < sub; x++)
            {
                int i0 = startVertIndex + y * rowSize + x;
                int i1 = i0 + 1;
                int i2 = i0 + rowSize;
                int i3 = i2 + 1;

                triangles.Add(i0);
                triangles.Add(i2);
                triangles.Add(i1);

                triangles.Add(i1);
                triangles.Add(i2);
                triangles.Add(i3);
            }
        }
    }

    private GameObject CreateGameButton(string label, Vector2 anchoredPosition, UnityEngine.Events.UnityAction onClickAction)
    {
        GameObject buttonObj = new GameObject("GameButton");
        buttonObj.transform.SetParent(transform, false);

        RectTransform rect = buttonObj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(100f, 36f); // Slightly narrower to fit two buttons side-by-side
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
        txt.fontSize = 11;
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
        col.size = new Vector3(100f, 36f, 15f);
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

    private bool IsIndicesInCorrectOrder(List<int> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != i) return false;
        }
        return true;
    }

    private void CleanUpGame()
    {
        foreach (var b in slots)
        {
            if (b != null) Destroy(b.gameObject);
        }
        foreach (var sv in socketVisuals)
        {
            if (sv != null) Destroy(sv);
        }
    }

    private void OnDestroy()
    {
        CleanUpGame();
        if (generatedRingTexture != null)
        {
            Destroy(generatedRingTexture);
        }
    }
}
