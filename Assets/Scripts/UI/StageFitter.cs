using UnityEngine;

/// <summary>
/// 16:9 舞台：固定 1920×1080 的容器，按屏幕大小等比缩放、居中，所以画面永远不会被拉伸变形。
/// 舞台内部的坐标永远是 1920×1080，不会随屏幕变化。
///   cover = false（内容舞台）：完整放进屏幕，按钮、文字、棋盘永远不会被裁掉；
///   cover = true （背景舞台）：铺满屏幕，窗口不是 16:9 时裁掉多出来的边缘，所以不会出现黑边。
/// </summary>
public class StageFitter : MonoBehaviour
{
    public const float StageWidth = 1920f;
    public const float StageHeight = 1080f;

    RectTransform m_Rect;
    RectTransform m_Parent;
    bool m_Cover;

    void Awake()
    {
        m_Rect = (RectTransform)transform;
        m_Parent = (RectTransform)transform.parent;

        m_Rect.anchorMin = m_Rect.anchorMax = new Vector2(0.5f, 0.5f);
        m_Rect.pivot = new Vector2(0.5f, 0.5f);
        m_Rect.anchoredPosition = Vector2.zero;
        m_Rect.sizeDelta = new Vector2(StageWidth, StageHeight);
        Fit();
    }

    public void Configure(bool cover)
    {
        m_Cover = cover;
        if (m_Rect != null)
            Fit();
    }

    void Update()
    {
        Fit();   // 窗口被拖动改大小时也能实时跟上，开销很小
    }

    void Fit()
    {
        var parentRect = m_Parent.rect;
        if (parentRect.width <= 0f || parentRect.height <= 0f)
            return;

        float sx = parentRect.width / StageWidth;
        float sy = parentRect.height / StageHeight;
        float scale = m_Cover ? Mathf.Max(sx, sy) : Mathf.Min(sx, sy);
        m_Rect.localScale = new Vector3(scale, scale, 1f);
    }
}
