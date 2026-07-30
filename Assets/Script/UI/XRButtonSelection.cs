using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.Events;
using UnityEngine.EventSystems;

public class XRButtonSelection : XRSimpleInteractable, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
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

    private bool IsWristWatchButton()
    {
        if (WristWatch.Instance != null && WristWatch.Instance.wristWatchButtonObj != null)
        {
            if (gameObject == WristWatch.Instance.wristWatchButtonObj || transform.IsChildOf(WristWatch.Instance.wristWatchButtonObj.transform))
            {
                return true;
            }
        }
        string n = gameObject.name.ToLower();
        return n.Contains("wristwatch") || n.Contains("watchbutton");
    }

    public override bool IsSelectableBy(IXRSelectInteractor interactor)
    {
        if (IsWristWatchButton() && WristWatchFilterUtility.IsLeftHand(interactor))
        {
            return false;
        }
        return base.IsSelectableBy(interactor);
    }

    public override bool IsHoverableBy(IXRHoverInteractor interactor)
    {
        if (IsWristWatchButton() && WristWatchFilterUtility.IsLeftHand(interactor))
        {
            return false;
        }
        return base.IsHoverableBy(interactor);
    }

    protected override void Awake()
    {
        base.Awake();
        
        if (scaleTarget == null)
        {
            scaleTarget = transform;
        }

        // Guard against bad hover scales.
        hoverScaleMultiplier = Mathf.Clamp(hoverScaleMultiplier, 1f, 1.15f);

        originalScale = scaleTarget.localScale;
        targetScale = originalScale;
        targetColor = normalColor;

        if (buttonImage != null)
        {
            buttonImage.color = normalColor;
        }

        onClick.AddListener(ButtonClickAudio.PlayClickSound);
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
        if (IsWristWatchButton() && WristWatchFilterUtility.IsLeftHand(args.interactorObject)) return;
        base.OnHoverEntered(args);
        
        targetScale = originalScale * hoverScaleMultiplier;
        targetColor = hoverColor;
        
        if (args.interactorObject is XRBaseControllerInteractor controllerInteractor)
        {
            controllerInteractor.xrController.SendHapticImpulse(0.2f, 0.05f);
        }
    }

    protected override void OnHoverExited(HoverExitEventArgs args)
    {
        if (IsWristWatchButton() && WristWatchFilterUtility.IsLeftHand(args.interactorObject)) return;
        base.OnHoverExited(args);
        
        targetScale = originalScale;
        targetColor = normalColor;
    }

    protected override void OnSelectEntered(SelectEnterEventArgs args)
    {
        if (IsWristWatchButton() && WristWatchFilterUtility.IsLeftHand(args.interactorObject)) return;
        base.OnSelectEntered(args);
        
        Debug.Log($"Button Selected/Pressed: {gameObject.name}");
        ButtonClickAudio.PlayClickSound();
        onClick.Invoke();

        if (args.interactorObject is XRBaseControllerInteractor controllerInteractor)
        {
            controllerInteractor.xrController.SendHapticImpulse(0.5f, 0.1f);
        }
    }

    #region UI Pointer Handlers (For Graphic Raycasting / Pinching)
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (IsWristWatchButton() && WristWatchFilterUtility.IsLeftHand(null, eventData)) return;
        targetScale = originalScale * hoverScaleMultiplier;
        targetColor = hoverColor;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (IsWristWatchButton() && WristWatchFilterUtility.IsLeftHand(null, eventData)) return;
        targetScale = originalScale;
        targetColor = normalColor;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (IsWristWatchButton() && WristWatchFilterUtility.IsLeftHand(null, eventData)) return;
        Debug.Log($"Button UI Clicked/Pressed: {gameObject.name}");
        ButtonClickAudio.PlayClickSound();
        onClick.Invoke();
    }
    #endregion
}
