using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class HUDManager : MonoBehaviour
{
    [System.Serializable]
    public struct HUDPanel
    {
        public string panelName;
        public GameObject panelObject;

        public void Reveal()
        {
            if (panelObject != null)
            {
                panelObject.SetActive(true);
            }
        }

        public void Hide()
        {
            if (panelObject != null)
            {
                panelObject.SetActive(false);
            }
        }

        public void Toggle()
        {
            if (panelObject != null)
            {
                panelObject.SetActive(!panelObject.activeSelf);
            }
        }
    }

    [System.Serializable]
    public struct HUDCanvas
    {
        public string canvasName;
        public GameObject canvasObject;
        public HUDPanel[] panels;

        public void Reveal()
        {
            if (canvasObject != null)
            {
                canvasObject.SetActive(true);
            }
        }

        public void Hide()
        {
            if (canvasObject != null)
            {
                canvasObject.SetActive(false);
            }
        }

        public void Toggle()
        {
            if (canvasObject != null)
            {
                canvasObject.SetActive(!canvasObject.activeSelf);
            }
        }

        /// <summary>
        /// Reveals the panel with the specified name and hides all other panels in this canvas.
        /// </summary>
        public void RevealOnlyPanel(string targetPanelName)
        {
            if (panels == null) return;
            foreach (var panel in panels)
            {
                if (string.Equals(panel.panelName, targetPanelName, System.StringComparison.OrdinalIgnoreCase))
                {
                    panel.Reveal();
                }
                else
                {
                    panel.Hide();
                }
            }
        }

        public void RevealPanel(string targetPanelName)
        {
            if (panels == null) return;
            foreach (var panel in panels)
            {
                if (string.Equals(panel.panelName, targetPanelName, System.StringComparison.OrdinalIgnoreCase))
                {
                    panel.Reveal();
                }
            }
        }

        public void HidePanel(string targetPanelName)
        {
            if (panels == null) return;
            foreach (var panel in panels)
            {
                if (string.Equals(panel.panelName, targetPanelName, System.StringComparison.OrdinalIgnoreCase))
                {
                    panel.Hide();
                }
            }
        }

        public void TogglePanel(string targetPanelName)
        {
            if (panels == null) return;
            foreach (var panel in panels)
            {
                if (string.Equals(panel.panelName, targetPanelName, System.StringComparison.OrdinalIgnoreCase))
                {
                    panel.Toggle();
                }
            }
        }
    }

    [Header("HUD Setup")]
    public HUDCanvas[] canvases;

    [Header("Events")]
    public UnityEvent onStartEvent;

    void Start()
    {
        EnableWatchStart();
        onStartEvent?.Invoke();
    }

    #region Watch Interaction
    public void EnableWatchStart()
    {
        // By default, hide all canvases, and only enable the Watch canvas
        HideAllCanvases();
        RevealCanvas("Watch");
    }
    #endregion

    #region Helper Functions for HUDManager

    /// <summary>
    /// Hides all canvases.
    /// </summary>
    public void HideAllCanvases()
    {
        if (canvases == null) return;
        foreach (var canvas in canvases)
        {
            canvas.Hide();
        }
    }

    /// <summary>
    /// Reveals the canvas with the specified name.
    /// </summary>
    public void RevealCanvas(string canvasName)
    {
        if (canvases == null) return;
        foreach (var canvas in canvases)
        {
            if (string.Equals(canvas.canvasName, canvasName, System.StringComparison.OrdinalIgnoreCase))
            {
                canvas.Reveal();
            }
        }
    }

    /// <summary>
    /// Hides the canvas with the specified name.
    /// </summary>
    public void HideCanvas(string canvasName)
    {
        if (canvases == null) return;
        foreach (var canvas in canvases)
        {
            if (string.Equals(canvas.canvasName, canvasName, System.StringComparison.OrdinalIgnoreCase))
            {
                canvas.Hide();
            }
        }
    }

    /// <summary>
    /// Reveals the canvas with the specified name and hides all other canvases.
    /// </summary>
    public void RevealOnlyCanvas(string canvasName)
    {
        if (canvases == null) return;
        foreach (var canvas in canvases)
        {
            if (string.Equals(canvas.canvasName, canvasName, System.StringComparison.OrdinalIgnoreCase))
            {
                canvas.Reveal();
            }
            else
            {
                canvas.Hide();
            }
        }
    }

    /// <summary>
    /// Reveals a specific panel by name across all canvases.
    /// </summary>
    public void RevealPanel(string panelName)
    {
        if (canvases == null) return;
        foreach (var canvas in canvases)
        {
            canvas.RevealPanel(panelName);
        }
    }

    /// <summary>
    /// Hides a specific panel by name across all canvases.
    /// </summary>
    public void HidePanel(string panelName)
    {
        if (canvases == null) return;
        foreach (var canvas in canvases)
        {
            canvas.HidePanel(panelName);
        }
    }

    /// <summary>
    /// Reveals the specified panel and hides all other panels in the canvas that contains it.
    /// </summary>
    public void RevealOnlyPanel(string panelName)
    {
        if (canvases == null) return;
        foreach (var canvas in canvases)
        {
            // Check if this canvas contains the target panel
            bool containsPanel = false;
            if (canvas.panels != null)
            {
                foreach (var panel in canvas.panels)
                {
                    if (string.Equals(panel.panelName, panelName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        containsPanel = true;
                        break;
                    }
                }
            }

            // If it does, run RevealOnlyPanel on it
            if (containsPanel)
            {
                canvas.RevealOnlyPanel(panelName);
            }
        }
    }

    /// <summary>
    /// Toggles the active state of the canvas with the specified name.
    /// </summary>
    public void ToggleCanvas(string canvasName)
    {
        if (canvases == null) return;
        foreach (var canvas in canvases)
        {
            if (string.Equals(canvas.canvasName, canvasName, System.StringComparison.OrdinalIgnoreCase))
            {
                canvas.Toggle();
            }
        }
    }

    /// <summary>
    /// Toggles the active state of the panel with the specified name across all canvases.
    /// </summary>
    public void TogglePanel(string panelName)
    {
        if (canvases == null) return;
        foreach (var canvas in canvases)
        {
            canvas.TogglePanel(panelName);
        }
    }

    #endregion
}