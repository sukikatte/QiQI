using UnityEngine;

/// <summary>
/// 窗外天空：一条 3 倍宽的天空图（原图 / 镜像 / 原图）在窗口遮罩里匀速平移，
/// 起点和终点画面完全相同，所以循环时看不出接缝。
/// 挂在带 Mask 的窗口对象上。
/// </summary>
public class WindowSkyScroll : MonoBehaviour
{
    RectTransform m_Window;
    RectTransform m_Strip;
    float m_WidthsPerSecond;

    /// <param name="widthsPerSecond">每秒移动多少个窗口宽度；0.0143 约等于 140 秒循环一圈。</param>
    public void Init(RectTransform strip, float widthsPerSecond)
    {
        m_Window = (RectTransform)transform;
        m_Strip = strip;
        m_WidthsPerSecond = widthsPerSecond;
    }

    void LateUpdate()
    {
        if (m_Strip == null)
            return;

        float w = m_Window.rect.width;
        if (w <= 0f)
            return;

        // 天空条：左对齐、竖向铺满、宽度 3 倍
        m_Strip.sizeDelta = new Vector2(w * 3f, 0f);
        float offset = Mathf.Repeat(Time.unscaledTime * m_WidthsPerSecond * w, w * 2f);
        m_Strip.anchoredPosition = new Vector2(-offset, 0f);
    }
}
