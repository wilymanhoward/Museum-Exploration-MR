using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Button))]
public class UIButtonAudio : MonoBehaviour, IPointerClickHandler, ISubmitHandler
{
    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
    }

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

    private void OnEnable()
    {
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

    public void OnPointerClick(PointerEventData eventData)
    {
        if (IsWristWatchButton())
        {
            if (WristWatch.Instance != null && WristWatch.Instance.IsWatchButtonHidden()) return;
            if (WristWatchFilterUtility.IsLeftHand(null, eventData)) return;
        }
        if (button != null && button.interactable)
        {
            ButtonClickAudio.PlayClickSound();
        }
    }

    public void OnSubmit(BaseEventData eventData)
    {
        if (IsWristWatchButton())
        {
            if (WristWatch.Instance != null && WristWatch.Instance.IsWatchButtonHidden()) return;
            if (WristWatchFilterUtility.IsLeftHand(null, eventData)) return;
        }
        if (button != null && button.interactable)
        {
            ButtonClickAudio.PlayClickSound();
        }
    }

    private void OnButtonClick()
    {
        if (IsWristWatchButton())
        {
            if (WristWatch.Instance != null && WristWatch.Instance.IsWatchButtonHidden()) return;
            return;
        }
        ButtonClickAudio.PlayClickSound();
    }
}
