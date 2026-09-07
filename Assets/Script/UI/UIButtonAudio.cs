using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class UIButtonAudio : MonoBehaviour, IPointerDownHandler, IPointerClickHandler, ISubmitHandler
{
    private Button button;
    private Selectable selectable;
    private XRButtonSelection xrButton;

    private float lastPointerDownTime = -10f;
    private const float POINTER_DOWN_CLICK_SUPPRESS_WINDOW = 0.35f;

    private void Awake()
    {
        CacheComponents();
    }

    private void CacheComponents()
    {
        if (button == null) button = GetComponent<Button>();
        if (selectable == null) selectable = GetComponent<Selectable>();
        if (xrButton == null) xrButton = GetComponent<XRButtonSelection>();
    }

    private bool IsInteractable()
    {
        if (!isActiveAndEnabled) return false;
        if (button != null && !button.interactable) return false;
        if (selectable != null && !selectable.interactable) return false;
        return true;
    }

    private bool IsWristWatchButton()
    {
        if (WristWatch.Instance != null && WristWatch.Instance.wristWatchButtonObj != null)
        {
            if (gameObject == WristWatch.Instance.wristWatchButtonObj || transform.IsChildOf(WristWatch.Instance.wristWatchButtonObj.transform))
            {
                if (WristWatch.Instance.optionsPanelObj != null && transform.IsChildOf(WristWatch.Instance.optionsPanelObj.transform))
                {
                    return false;
                }
                return true;
            }
        }
        string n = gameObject.name.ToLower();
        return n.Contains("wristwatch") || n.Contains("watchbutton");
    }

    private void OnEnable()
    {
        CacheComponents();
        if (button != null)
        {
            button.onClick.RemoveListener(OnButtonClick);
            button.onClick.AddListener(OnButtonClick);
        }
    }

    private void OnDisable()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(OnButtonClick);
        }
    }

    public void AttachButtonClickListener()
    {
        CacheComponents();
        if (button != null)
        {
            button.onClick.RemoveListener(OnButtonClick);
            button.onClick.AddListener(OnButtonClick);
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!IsInteractable()) return;

        if (IsWristWatchButton())
        {
            if (WristWatch.Instance != null && WristWatch.Instance.IsWatchButtonHidden()) return;
            if (WristWatchFilterUtility.IsLeftHand(null, eventData)) return;
        }

        // If XRButtonSelection is active on this object, it handles pointer down / select audio
        if (xrButton != null && xrButton.isActiveAndEnabled)
        {
            lastPointerDownTime = Time.unscaledTime;
            return;
        }

        lastPointerDownTime = Time.unscaledTime;
        ButtonClickAudio.PlayClickSound();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!IsInteractable()) return;

        if (IsWristWatchButton())
        {
            if (WristWatch.Instance != null && WristWatch.Instance.IsWatchButtonHidden()) return;
            if (WristWatchFilterUtility.IsLeftHand(null, eventData)) return;
        }

        // If sound was already played on PointerDown during this pinch/click, suppress double play on release
        if (Time.unscaledTime - lastPointerDownTime < POINTER_DOWN_CLICK_SUPPRESS_WINDOW)
        {
            return;
        }

        if (xrButton != null && xrButton.isActiveAndEnabled)
        {
            return;
        }

        lastPointerDownTime = Time.unscaledTime;
        ButtonClickAudio.PlayClickSound();
    }

    public void OnSubmit(BaseEventData eventData)
    {
        if (!IsInteractable()) return;

        if (IsWristWatchButton())
        {
            if (WristWatch.Instance != null && WristWatch.Instance.IsWatchButtonHidden()) return;
            if (WristWatchFilterUtility.IsLeftHand(null, eventData)) return;
        }

        lastPointerDownTime = Time.unscaledTime;
        ButtonClickAudio.PlayClickSound();
    }

    private void OnButtonClick()
    {
        if (!IsInteractable()) return;

        if (IsWristWatchButton())
        {
            if (WristWatch.Instance != null && WristWatch.Instance.IsWatchButtonHidden()) return;
            return;
        }

        // Suppress if PointerDown or PointerClick already triggered the sound
        if (Time.unscaledTime - lastPointerDownTime < POINTER_DOWN_CLICK_SUPPRESS_WINDOW)
        {
            return;
        }

        if (xrButton != null && xrButton.isActiveAndEnabled)
        {
            return;
        }

        lastPointerDownTime = Time.unscaledTime;
        ButtonClickAudio.PlayClickSound();
    }
}
