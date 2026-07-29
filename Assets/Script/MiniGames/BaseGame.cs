using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Base class for all mini-game panels (Game1Panel, Game2Panel, Game3Panel, …).
/// Provides a Close button that returns the player to MiniGameMenuPanel.
///
/// Usage:
///   - Attach a subclass (e.g. Game1GuessName : BaseGame) to the game panel GameObject.
///   - Assign the close button in the Inspector, OR name it "CloseButton" / "ButtonClose"
///     and it will be found automatically.
///   - Override OnGameStart() and OnGameEnd() to initialise / clean up your game state.
/// </summary>
public class BaseGame : MonoBehaviour
{
    [Header("Close Button (auto-found by name if left empty)")]
    [Tooltip("Button that returns the player to the mini-game menu.")]
    public Button closeButton;

    // ─────────────────────────────────────────────────────────────────────────
    // Unity
    // ─────────────────────────────────────────────────────────────────────────

    protected virtual void Awake()
    {
        ResolveCloseButton();
        WireCloseButton();
    }

    protected virtual void OnEnable()
    {
        OnGameStart();
    }

    protected virtual void OnDisable()
    {
        OnGameEnd();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Virtual game lifecycle – override in subclasses
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Called when this game panel is activated (OnEnable).
    /// Override to reset and initialise your game state.
    /// </summary>
    public virtual void OnGameStart()
    {
        Debug.Log($"{GetType().Name}: OnGameStart");
    }

    /// <summary>
    /// Called when this game panel is deactivated (OnDisable).
    /// Override to clean up timers, spawned objects, etc.
    /// </summary>
    public virtual void OnGameEnd()
    {
        Debug.Log($"{GetType().Name}: OnGameEnd");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Close / Back
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Deactivates this game panel and returns to the mini-game menu.
    /// Override for custom close behaviour (e.g. stop coroutines before hiding).
    /// </summary>
    public virtual void OnClose()
    {
        Debug.Log($"{GetType().Name}: Closed – returning to menu.");
        gameObject.SetActive(false);           // triggers OnDisable → OnGameEnd
        MiniGames.Instance?.ShowMenuPanel();   // re-activates MiniGameMenuPanel
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Private helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void ResolveCloseButton()
    {
        if (closeButton != null) return;

        // Search by common names
        string[] candidateNames = { "CloseButton", "ButtonClose", "BackButton", "ButtonBack", "CloseBtn" };
        foreach (string n in candidateNames)
        {
            Transform tf = FindDeepChild(transform, n);
            if (tf != null)
            {
                closeButton = tf.GetComponent<Button>();
                if (closeButton != null) return;
            }
        }
    }

    private void WireCloseButton()
    {
        if (closeButton == null) return;

        closeButton.onClick.RemoveAllListeners();
        closeButton.onClick.AddListener(OnClose);

        // Also wire XRButtonSelection so hand-ray / poke works
        XRButtonSelection xr = closeButton.GetComponent<XRButtonSelection>();
        if (xr != null)
        {
            xr.onClick.RemoveAllListeners();
            xr.onClick.AddListener(OnClose);
        }
    }

    private static Transform FindDeepChild(Transform root, string childName)
    {
        if (root == null) return null;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t != null && t.name == childName) return t;
        return null;
    }
}
