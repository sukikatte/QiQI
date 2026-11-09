using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 木牌按钮：九宫格木头底 + 奶油色文字，可带小图标，可切换成“选中”的发亮外观。
/// 悬浮/按下的缩放反馈复用 UIButtonFeedback。
/// </summary>
public class PlaqueButton : MonoBehaviour
{
    public enum Style { Normal, Selected }

    Image m_Image;
    Button m_Button;
    TextMeshProUGUI m_Label;
    RectTransform m_LabelRect;
    Image m_Icon;

    public static PlaqueButton Create(Transform parent, string name, string label, Vector2 pos, Vector2 size,
                                      float fontSize, Action onClick, Sprite icon = null)
    {
        var rect = WorkshopUI.NewRect(parent, name, pos, size);
        var go = rect.gameObject;

        var image = go.AddComponent<Image>();
        image.sprite = WorkshopUI.LoadSprite("UI/btn_wood");
        image.type = Image.Type.Sliced;
        image.raycastTarget = true;

        var button = go.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;    // 悬浮/按下效果交给 UIButtonFeedback

        var plaque = go.AddComponent<PlaqueButton>();
        plaque.m_Image = image;
        plaque.m_Button = button;

        float leftPad = 18f;
        if (icon != null)
        {
            float iconSize = size.y * 0.72f;
            var iconImg = WorkshopUI.NewImage(rect, "Icon", icon,
                new Vector2(-size.x / 2f + iconSize * 0.5f + 18f, 0f), new Vector2(iconSize, iconSize));
            plaque.m_Icon = iconImg;
            leftPad = iconSize + 28f;
        }

        var text = WorkshopUI.NewLabel(rect, "Text", label, fontSize, WorkshopUI.Cream, Vector2.zero, Vector2.zero);
        WorkshopUI.Stretch(text.rectTransform);
        text.rectTransform.offsetMin = new Vector2(leftPad, 6f);
        text.rectTransform.offsetMax = new Vector2(-16f, -6f);
        text.enableAutoSizing = true;                 // 文字太长放不下时自动缩小一点
        text.fontSizeMax = fontSize;
        text.fontSizeMin = fontSize * 0.65f;
        plaque.m_Label = text;
        plaque.m_LabelRect = text.rectTransform;

        plaque.SetOnClick(onClick);

        // 最后再挂反馈脚本：它在 Awake 里记录初始缩放和颜色
        go.AddComponent<UIButtonFeedback>();
        return plaque;
    }

    public void SetLabel(string label)
    {
        m_Label.text = label;
    }

    public void SetIcon(Sprite icon)
    {
        if (m_Icon != null)
            m_Icon.sprite = icon;
    }

    public void SetOnClick(Action onClick)
    {
        m_Button.onClick.RemoveAllListeners();
        if (onClick != null)
            m_Button.onClick.AddListener(() => onClick());
    }

    public void SetPosSize(Vector2 pos, Vector2 size)
    {
        var rect = (RectTransform)transform;
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
        Relayout(size);
    }

    public void SetFontSize(float fontSize)
    {
        if (m_Label == null)
            return;
        m_Label.fontSize = fontSize;
        m_Label.fontSizeMax = fontSize;
        m_Label.fontSizeMin = fontSize * 0.65f;
    }

    void Relayout(Vector2 size)
    {
        float leftPad = 18f;
        if (m_Icon != null)
        {
            float iconSize = size.y * 0.72f;
            m_Icon.rectTransform.sizeDelta = new Vector2(iconSize, iconSize);
            m_Icon.rectTransform.anchoredPosition = new Vector2(-size.x / 2f + iconSize * 0.5f + 18f, 0f);
            leftPad = iconSize + 28f;
        }
        if (m_LabelRect != null)
        {
            m_LabelRect.offsetMin = new Vector2(leftPad, 6f);
            m_LabelRect.offsetMax = new Vector2(-16f, -6f);
        }
    }

    public void SetStyle(Style style)
    {
        m_Image.sprite = WorkshopUI.LoadSprite(style == Style.Selected ? "UI/btn_gold" : "UI/btn_wood");
        // 选中时木牌是亮金色，深褐字更清楚；普通时是深木头，奶油字更清楚
        m_Label.color = style == Style.Selected ? WorkshopUI.Ink : WorkshopUI.Cream;
    }
}
