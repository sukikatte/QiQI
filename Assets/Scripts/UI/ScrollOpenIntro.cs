using System.Collections;
using UnityEngine;

/// <summary>
/// 主菜单开场：先显示合着的卷轴（轻微漂浮），点击后旋转并展开成打开的卷轴，按钮随之淡入。
/// 由 MenuFlowBootstrap 在运行时创建并初始化。
/// 打开状态由多个对象组成（卷轴图 + 按钮层，中间夹着小蛇），它们会同步展开、淡入。
/// </summary>
public class ScrollOpenIntro : MonoBehaviour
{
    RectTransform m_Closed;
    CanvasGroup m_ClosedGroup;
    CanvasGroup m_HintGroup;
    RectTransform[] m_OpenRects;
    CanvasGroup[] m_OpenGroups;
    Vector2 m_ClosedBasePos;
    Vector2 m_OpenPos;

    // 这一次运行里主菜单是不是已经出现过：第一次需要手动点开，之后回到主菜单会自动展开
    static bool s_HasShownMenu;
    bool m_AutoOpen;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        s_HasShownMenu = false;     // 关闭"进入 Play 不重载域"时，也保证每次新运行都从第一次开始
    }

    bool m_Opening;
    float m_Time;

    // 动画参数
    const float PressTime = 0.12f;
    const float OpenTime = 0.8f;
    // 合着的卷轴待机时的倾斜角度：负数 = 向右倾斜（顺时针），正数 = 向左；0 = 竖直
    const float IdleTilt = -15f;
    // 开场淡入：等待多久后开始、淡入用多久、从下方多少像素升起
    const float IntroDelay = 1.2f;
    const float IntroFadeTime = 2.4f;
    const float IntroRise = 40f;
    static readonly Vector3 OpenStartScale = new Vector3(0.35f, 0.45f, 1f);

    public void Init(RectTransform closed, CanvasGroup closedGroup, CanvasGroup hintGroup,
                     RectTransform[] openRects, CanvasGroup[] openGroups, Vector2 openPos)
    {
        m_Closed = closed;
        m_ClosedGroup = closedGroup;
        m_HintGroup = hintGroup;
        m_OpenRects = openRects;
        m_OpenGroups = openGroups;
        m_ClosedBasePos = closed.anchoredPosition;
        m_OpenPos = openPos;

        // 一开始就是倾斜的，避免第一帧是竖直的再跳过去
        m_Closed.localRotation = Quaternion.Euler(0f, 0f, IdleTilt);

        // 合着的卷轴一开始完全透明、不可点，由 Update 慢慢淡入
        m_ClosedGroup.alpha = 0f;
        m_ClosedGroup.blocksRaycasts = false;
        if (m_HintGroup != null)
            m_HintGroup.alpha = 0f;

        // 初始：打开的卷轴和按钮不可见、不可点
        for (int i = 0; i < m_OpenGroups.Length; i++)
        {
            m_OpenGroups[i].alpha = 0f;
            m_OpenGroups[i].interactable = false;
            m_OpenGroups[i].blocksRaycasts = false;
            m_OpenRects[i].localScale = OpenStartScale;
        }

        // 不是这次运行第一次进主菜单：卷轴直接显示，稍等一下自动展开，不用手动点
        if (s_HasShownMenu)
        {
            m_AutoOpen = true;
            m_Time = IntroDelay + IntroFadeTime;      // 跳过淡入等待
            m_ClosedGroup.alpha = 1f;
            StartCoroutine(AutoOpenRoutine());
        }
        s_HasShownMenu = true;
    }

    IEnumerator AutoOpenRoutine()
    {
        for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
            yield return null;
        Open();
    }

    void Update()
    {
        if (m_Opening || m_Closed == null)
            return;

        m_Time += Time.unscaledDeltaTime;

        // 开场淡入：先等一会儿（标题正在落下），再慢慢浮现，同时从下方轻轻升起
        float fade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((m_Time - IntroDelay) / IntroFadeTime));
        m_ClosedGroup.alpha = fade;
        if (fade >= 1f && !m_ClosedGroup.blocksRaycasts)
            m_ClosedGroup.blocksRaycasts = true;   // 完全显示后才允许点击，避免淡入到一半被点开

        // 待机：轻微上下漂浮 + 左右摇摆，提示“可以点我”
        float rise = (1f - fade) * -IntroRise;
        m_Closed.anchoredPosition = m_ClosedBasePos + new Vector2(0f, rise + Mathf.Sin(m_Time * 1.8f) * 8f);
        m_Closed.localRotation = Quaternion.Euler(0f, 0f, IdleTilt + Mathf.Sin(m_Time * 1.2f) * 1.5f);

        // 提示文字在卷轴快出现完时才淡入
        if (m_HintGroup != null && !m_AutoOpen)
        {
            float hintFade = Mathf.Clamp01((m_Time - IntroDelay - IntroFadeTime * 0.7f) / 1.0f);
            m_HintGroup.alpha = hintFade * (0.55f + 0.45f * Mathf.Sin(m_Time * 3f));
        }
    }

    public void Open()
    {
        if (m_Opening)
            return;
        m_Opening = true;
        StartCoroutine(OpenRoutine());
    }

    IEnumerator OpenRoutine()
    {
        // 关掉点击反馈脚本，避免它和这里的缩放动画打架
        var feedback = m_Closed.GetComponent<UIButtonFeedback>();
        if (feedback != null)
            feedback.enabled = false;
        m_ClosedGroup.blocksRaycasts = false;

        Vector2 startPos = m_Closed.anchoredPosition;
        float startRot = NormalizeAngle(m_Closed.localEulerAngles.z);
        Vector3 startScale = m_Closed.localScale;

        // 阶段 1：按下去一点
        for (float t = 0f; t < PressTime; t += Time.unscaledDeltaTime)
        {
            float k = t / PressTime;
            m_Closed.localScale = Vector3.Lerp(startScale, startScale * 0.92f, EaseOutQuad(k));
            if (m_HintGroup != null)
                m_HintGroup.alpha = Mathf.Lerp(m_HintGroup.alpha, 0f, k);
            yield return null;
        }
        if (m_HintGroup != null)
            m_HintGroup.alpha = 0f;

        Vector3 pressedScale = m_Closed.localScale;

        // 阶段 2：旋转成横向 + 移到打开位置，同时展开、交叉淡入淡出
        for (float t = 0f; t < OpenTime; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.Clamp01(t / OpenTime);
            float move = EaseInOutCubic(k);

            // 合着的卷轴：转 -90°，移到打开卷轴中心，慢慢淡出
            m_Closed.anchoredPosition = Vector2.Lerp(startPos, m_OpenPos, move);
            m_Closed.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(startRot, -90f, move));
            m_Closed.localScale = Vector3.Lerp(pressedScale, pressedScale * 1.15f, move);
            m_ClosedGroup.alpha = 1f - Mathf.InverseLerp(0.45f, 0.95f, k);

            // 打开的卷轴 + 按钮：从小处横向展开（带一点回弹），慢慢淡入
            float grow = EaseOutBack(Mathf.InverseLerp(0.25f, 1f, k));
            float fade = Mathf.InverseLerp(0.3f, 0.9f, k);
            for (int i = 0; i < m_OpenRects.Length; i++)
            {
                m_OpenRects[i].localScale = Vector3.LerpUnclamped(OpenStartScale, Vector3.one, grow);
                m_OpenGroups[i].alpha = fade;
            }

            yield return null;
        }

        for (int i = 0; i < m_OpenRects.Length; i++)
        {
            m_OpenRects[i].localScale = Vector3.one;
            m_OpenGroups[i].alpha = 1f;
            m_OpenGroups[i].interactable = true;
            m_OpenGroups[i].blocksRaycasts = true;
        }

        // 最后一步：隐藏合着的卷轴
        m_Closed.gameObject.SetActive(false);
    }

    static float NormalizeAngle(float a)
    {
        return a > 180f ? a - 360f : a;
    }

    static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);

    static float EaseInOutCubic(float t)
    {
        return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
    }

    static float EaseOutBack(float t)
    {
        const float c1 = 1.2f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
}
