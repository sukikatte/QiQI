using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 按钮悬浮放大、按下缩小的简单反馈。
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class UIButtonFeedback : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    [SerializeField] float hoverScale = 1.08f;
    [SerializeField] float pressedScale = 0.94f;
    [SerializeField] float lerpSpeed = 14f;
    /// <summary>悬浮 / 按下时图片的颜色（没有图的透明按钮可以改成淡淡的白底）。</summary>
    public Color hoverTint = new Color(1f, 0.98f, 0.9f, 1f);
    public Color pressedTint = new Color(0.9f, 0.9f, 0.9f, 1f);

    /// <summary>点击时是否播放 click 音效（有的按钮自己处理声音，会关掉它）。</summary>
    public bool playClickSound = true;

    RectTransform m_Rect;
    Image m_Image;
    Vector3 m_BaseScale;
    Color m_BaseColor;
    Vector3 m_TargetScale;
    Color m_TargetColor;
    bool m_Hovered;
    bool m_Pressed;

    void Awake()
    {
        m_Rect = transform as RectTransform;
        m_Image = GetComponent<Image>();
        m_BaseScale = m_Rect.localScale;
        m_BaseColor = m_Image != null ? m_Image.color : Color.white;
        m_TargetScale = m_BaseScale;
        m_TargetColor = m_BaseColor;
    }

    void Update()
    {
        m_Rect.localScale = Vector3.Lerp(m_Rect.localScale, m_TargetScale, Time.unscaledDeltaTime * lerpSpeed);
        if (m_Image != null)
            m_Image.color = Color.Lerp(m_Image.color, m_TargetColor, Time.unscaledDeltaTime * lerpSpeed);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        m_Hovered = true;
        ApplyTarget();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        m_Hovered = false;
        m_Pressed = false;
        ApplyTarget();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        m_Pressed = true;
        ApplyTarget();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        m_Pressed = false;
        ApplyTarget();
    }

    /// <summary>点击（按下并在按钮上松开）时播放 click 音效；按钮被禁用时不响。</summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (!playClickSound || eventData.button != PointerEventData.InputButton.Left)
            return;
        var button = GetComponent<Button>();
        if (button != null && !button.IsInteractable())
            return;
        UiSfx.PlayClick();
    }

    void ApplyTarget()
    {
        if (m_Pressed)
        {
            m_TargetScale = m_BaseScale * pressedScale;
            m_TargetColor = pressedTint;
        }
        else if (m_Hovered)
        {
            m_TargetScale = m_BaseScale * hoverScale;
            m_TargetColor = hoverTint;
        }
        else
        {
            m_TargetScale = m_BaseScale;
            m_TargetColor = m_BaseColor;
        }
    }
}
