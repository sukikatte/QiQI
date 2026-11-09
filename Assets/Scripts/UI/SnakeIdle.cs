using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 小蛇待机动画：缓慢的呼吸起伏 + 随机眨眼。
/// 呼吸只会"放大"（缩放 ≥ 1，以脚底为支点），所以背景图里原来的小蛇始终被盖在下面，不会露出重影。
/// 眨眼：一张只含"闭着的眼睛"的小补丁图，短暂显示。
/// </summary>
public class SnakeIdle : MonoBehaviour
{
    const float BreathPeriod = 3.6f;     // 一次呼吸的时长（秒）
    const float BreathX = 0.005f;        // 横向最大放大比例
    const float BreathY = 0.012f;        // 竖向最大放大比例（胸腔起伏更明显）

    const float BlinkMinGap = 2.5f;      // 两次眨眼之间的最短间隔
    const float BlinkMaxGap = 5.5f;
    const float BlinkClosedTime = 0.13f; // 闭眼持续时间
    const float DoubleBlinkChance = 0.25f;

    RectTransform m_Root;
    Image m_Blink;

    float m_NextBlinkAt;
    float m_OpenAgainAt;
    bool m_Closed;
    bool m_PendingSecondBlink;

    public void Init(RectTransform root, Image blink)
    {
        m_Root = root;
        m_Blink = blink;
        if (m_Blink != null)
            m_Blink.enabled = false;
        ScheduleNextBlink();
    }

    void Update()
    {
        if (m_Root == null)
            return;

        float t = Time.unscaledTime;

        // 呼吸：0..1 的平滑起伏
        float s = 0.5f - 0.5f * Mathf.Cos(t * Mathf.PI * 2f / BreathPeriod);
        m_Root.localScale = new Vector3(1f + BreathX * s, 1f + BreathY * s, 1f);

        if (m_Blink == null)
            return;

        if (!m_Closed && t >= m_NextBlinkAt)
        {
            m_Closed = true;
            m_Blink.enabled = true;
            m_OpenAgainAt = t + BlinkClosedTime;
        }
        else if (m_Closed && t >= m_OpenAgainAt)
        {
            m_Closed = false;
            m_Blink.enabled = false;

            if (m_PendingSecondBlink)
            {
                m_PendingSecondBlink = false;
                ScheduleNextBlink();
            }
            else if (Random.value < DoubleBlinkChance)
            {
                m_PendingSecondBlink = true;
                m_NextBlinkAt = t + 0.18f;   // 连眨一下
            }
            else
            {
                ScheduleNextBlink();
            }
        }
    }

    void ScheduleNextBlink()
    {
        m_NextBlinkAt = Time.unscaledTime + Random.Range(BlinkMinGap, BlinkMaxGap);
    }
}
