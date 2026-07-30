using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ButtonClickAudio : MonoBehaviour
{
    private static ButtonClickAudio instance;
    public static ButtonClickAudio Instance => instance;

    private AudioSource audioSource;
    private AudioClip clickClip;
    private float lastPlayTime = -1f;
    private const float DEBOUNCE_INTERVAL = 0.03f; // 30ms threshold

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        if (instance == null)
        {
            GameObject go = new GameObject("[ButtonClickAudio]");
            instance = go.AddComponent<ButtonClickAudio>();
            DontDestroyOnLoad(go);
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f; // 2D UI Audio
        audioSource.volume = 1f;
        audioSource.ignoreListenerPause = true;

        LoadAudioClip();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        AttachListenersToAllButtons();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        AttachListenersToAllButtons();
    }

    private float lastScanTime = 0f;
    private void Update()
    {
        // Periodically scan for newly instantiated UI buttons (e.g. dynamically spawned room/game list items)
        if (Time.unscaledTime - lastScanTime > 0.3f)
        {
            lastScanTime = Time.unscaledTime;
            AttachListenersToAllButtons();
        }
    }

    private void LoadAudioClip()
    {
        clickClip = Resources.Load<AudioClip>("Audio/Click");
        if (clickClip == null)
        {
            clickClip = Resources.Load<AudioClip>("Click");
        }

        if (clickClip == null)
        {
            Debug.LogWarning("ButtonClickAudio: Could not load 'Click' audio clip from Resources/Audio/Click or Resources/Click.");
        }
    }

    public static void PlayClickSound()
    {
        if (instance != null)
        {
            instance.PlaySoundInternal();
        }
        else
        {
            AutoInitialize();
            if (instance != null)
            {
                instance.PlaySoundInternal();
            }
        }
    }

    private void PlaySoundInternal()
    {
        if (clickClip == null)
        {
            LoadAudioClip();
        }

        if (clickClip == null) return;

        // Prevent double playing if multiple events fire on the same tap/click
        if (Time.unscaledTime - lastPlayTime < DEBOUNCE_INTERVAL)
        {
            return;
        }

        lastPlayTime = Time.unscaledTime;
        if (audioSource != null)
        {
            audioSource.PlayOneShot(clickClip);
        }
    }

    public void AttachListenersToAllButtons()
    {
        Button[] buttons = Object.FindObjectsOfType<Button>(true);
        foreach (Button b in buttons)
        {
            if (b != null && b.gameObject != null)
            {
                if (b.gameObject.GetComponent<UIButtonAudio>() == null)
                {
                    b.gameObject.AddComponent<UIButtonAudio>();
                }
            }
        }
    }
}
