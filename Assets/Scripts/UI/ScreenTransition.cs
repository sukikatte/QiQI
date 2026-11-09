using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 转场：铺一张 Bg/bg 背景图，琦琦飞吻的 8 帧动画（Resources/Art/kiss/kiss_1 ~ kiss_8）在中间播放，
/// 趁屏幕被盖住的时候切换场景 / 界面，然后爱心散开，背景关掉。
/// 对象不随场景销毁，所以"切场景"之后动画也能接着播完。
/// 用法：ScreenTransition.LoadScene("MainMenu")，或 ScreenTransition.Run(() => { 换界面 })。
/// </summary>
public class ScreenTransition : MonoBehaviour
{
    const int FrameCount = 8;
    const float CoverTime = 0.26f;        // 屏幕暗下来
    const float KissDuration = 1.2f;      // 飞吻动画总时长
    const float RevealTime = 0.32f;       // 屏幕亮回来
    const float KissScale = 1.9f;         // 每帧 629×381，再放大一点

    static ScreenTransition s_Instance;

    GameObject m_Root;
    Image m_Kiss;
    CanvasGroup m_KissGroup;
    Sprite[] m_Frames;
    bool m_Busy;

    /// <summary>转场进行中（切换完、还在亮回来的时候也算）。</summary>
    public static bool Busy => s_Instance != null && s_Instance.m_Busy;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        s_Instance = null;      // 关闭"进入 Play 不重载域"时，保证每次新运行都重新创建
    }

    /// <summary>转场并切换场景。</summary>
    public static void LoadScene(string scene)
    {
        Run(() => SceneManager.LoadScene(scene));
    }

    /// <summary>转场；屏幕完全盖住的时候执行 atCover（换界面 / 换场景）。正在转场时再点会被忽略。</summary>
    public static void Run(Action atCover)
    {
        var instance = Get();
        if (instance.m_Busy)
            return;
        instance.StartCoroutine(instance.Play(atCover));
    }

    static ScreenTransition Get()
    {
        if (s_Instance != null)
            return s_Instance;
        var go = new GameObject("ScreenTransition");
        DontDestroyOnLoad(go);
        s_Instance = go.AddComponent<ScreenTransition>();
        s_Instance.Build();
        return s_Instance;
    }

    void Build()
    {
        m_Root = new GameObject("TransitionCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        m_Root.transform.SetParent(transform, false);
        var canvas = m_Root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;           // 盖在所有界面和弹窗的上面
        var scaler = m_Root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var coverRect = WorkshopUI.NewStretch(m_Root.transform, "Cover");
        var cover = coverRect.gameObject.AddComponent<Image>();
        var bgSprite = Resources.Load<Sprite>("Art/Bg/bg");
        if (bgSprite != null)
        {
            cover.sprite = bgSprite;
            cover.preserveAspect = false;
            cover.color = Color.white;
        }
        else
        {
            cover.color = new Color(0.10f, 0.06f, 0.05f, 1f);
        }
        cover.raycastTarget = true;          // 转场期间挡住鼠标点击

        m_Frames = new Sprite[FrameCount];
        for (int i = 0; i < FrameCount; i++)
            m_Frames[i] = Resources.Load<Sprite>("Art/kiss/kiss_" + (i + 1));

        var kissRect = WorkshopUI.NewRect(m_Root.transform, "Kiss", new Vector2(0f, 10f), new Vector2(629f, 381f) * KissScale);
        m_Kiss = kissRect.gameObject.AddComponent<Image>();
        m_Kiss.preserveAspect = true;
        m_Kiss.raycastTarget = false;
        m_Kiss.sprite = m_Frames[0];
        m_KissGroup = kissRect.gameObject.AddComponent<CanvasGroup>();
        m_KissGroup.alpha = 0f;

        m_Root.SetActive(false);
    }

    IEnumerator Play(Action atCover)
    {
        m_Busy = true;
        m_Root.SetActive(true);
        var kissRect = m_Kiss.rectTransform;
        SetFrame(0);

        // 1. 背景图直接铺上，琦琦从小变大浮现
        for (float t = 0f; t < CoverTime; t += Step())
        {
            float u = Mathf.SmoothStep(0f, 1f, t / CoverTime);
            m_KissGroup.alpha = Mathf.Clamp01(u * 1.6f);
            kissRect.localScale = Vector3.one * Mathf.Lerp(0.8f, 1f, u);
            yield return null;
        }
        m_KissGroup.alpha = 1f;
        kissRect.localScale = Vector3.one;

        // 2. 屏幕完全盖住了：切换界面 / 场景，同时播飞吻动画
        try
        {
            atCover?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }

        for (float t = 0f; t < KissDuration; t += Step())
        {
            SetFrame(Mathf.Min(FrameCount - 1, (int)(t / KissDuration * FrameCount)));
            yield return null;
        }
        SetFrame(FrameCount - 1);

        // 3. 琦琦散开，背景图一直铺着，最后整页关掉
        for (float t = 0f; t < RevealTime; t += Step())
        {
            float u = Mathf.SmoothStep(0f, 1f, t / RevealTime);
            m_KissGroup.alpha = 1f - u;
            kissRect.localScale = Vector3.one * Mathf.Lerp(1f, 1.18f, u);
            yield return null;
        }

        m_Root.SetActive(false);
        m_Busy = false;
    }

    /// <summary>每帧前进的时间；切场景那一帧会卡一下，限制一下免得动画被跳过去。</summary>
    static float Step()
    {
        return Mathf.Min(Time.unscaledDeltaTime, 0.05f);
    }

    void SetFrame(int index)
    {
        if (m_Frames[index] != null)
            m_Kiss.sprite = m_Frames[index];
    }
}
