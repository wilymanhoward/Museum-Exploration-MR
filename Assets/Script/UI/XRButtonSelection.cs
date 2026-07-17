using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class XRButtonSelection : UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
{
    [Header("Visual Configurations")]
    [Tooltip("Target UI Image to color-transition on hover.")]
    public Image buttonImage;

    [Tooltip("Scale target transform (e.g., the button container). Defaults to this transform.")]
    public Transform scaleTarget;

    [Header("Colors & Animation")]
    public Color normalColor = new Color(0.9f, 0.9f, 0.93f, 0.8f); // Translucent light gray/white
    public Color hoverColor = new Color(0.8f, 0.85f, 0.96f, 0.95f); // Soft lavender-blue highlight
    public float hoverScaleMultiplier = 1.08f;
    public float transitionSpeed = 8f;

    [Header("Button Events")]
    [Tooltip("UnityEvent invoked when the button is tapped/selected.")]
    public UnityEvent onClick = new UnityEvent();

    private Vector3 originalScale;
    private Vector3 targetScale;
    private Color targetColor;

    protected override void Awake()
    {
        base.Awake();
        
        if (scaleTarget == null)
        {
            scaleTarget = transform;
        }
        
        originalScale = scaleTarget.localScale;
        targetScale = originalScale;
        targetColor = normalColor;

        if (buttonImage != null)
        {
            buttonImage.color = normalColor;
        }
    }

    private void Update()
    {
        // Smoothly interpolate scale and color transitions
        if (scaleTarget != null)
        {
            scaleTarget.localScale = Vector3.Lerp(scaleTarget.localScale, targetScale, Time.deltaTime * transitionSpeed);
        }

        if (buttonImage != null)
        {
            buttonImage.color = Color.Lerp(buttonImage.color, targetColor, Time.deltaTime * transitionSpeed);
        }
    }

    protected override void OnHoverEntered(HoverEnterEventArgs args)
    {
        base.OnHoverEntered(args);
        
        // Hand hover entered: Highlight state
        targetScale = originalScale * hoverScaleMultiplier;
        targetColor = hoverColor;
        
        // Optional: Play a tiny haptic click in controllers if they are used
        if (args.interactorObject is UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInputInteractor controllerInteractor)
        {
            controllerInteractor.xrController.SendHapticImpulse(0.2f, 0.05f);
        }
    }

    protected override void OnHoverExited(HoverExitEventArgs args)
    {
        base.OnHoverExited(args);
        
        // Hand hover exited: Return to normal state
        targetScale = originalScale;
        targetColor = normalColor;
    }

    protected override void OnSelectEntered(SelectEnterEventArgs args)
    {
        base.OnSelectEntered(args);
        
        // Finger tap / Pinch gesture: Trigger click
        Debug.Log($"Button Selected/Pressed: {gameObject.name}");
        onClick.Invoke();

        // Send a stronger haptic impulse on press
        if (args.interactorObject is UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInputInteractor controllerInteractor)
        {
            controllerInteractor.xrController.SendHapticImpulse(0.5f, 0.1f);
        }
    }

    #region UI Pointer Handlers (For Graphic Raycasting / Pinching)
    public void OnPointerEnter(PointerEventData eventData)
    {
        targetScale = originalScale * hoverScaleMultiplier;
        targetColor = hoverColor;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        targetScale = originalScale;
        targetColor = normalColor;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        Debug.Log($"Button UI Clicked/Pressed: {gameObject.name}");
        onClick.Invoke();
    }
    #endregion
}
