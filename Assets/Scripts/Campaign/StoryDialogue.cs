using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 剧情对话框：琦琦的立绘站在一侧，下面是一块羊皮纸对话框，文字一个字一个字地冒出来。
/// 点击屏幕 / 空格 / 回车：先把当前这句显示完，再点一下就下一句。右上角可以"跳过"。
/// 琦琦说话时立绘会轻轻晃动，"糟糕！"这类句子会跳一下或者抖一下；玩家（你）的句子，立绘会暗下去。
/// </summary>
public class StoryDialogue : MonoBehaviour
{
    public enum Fx { None, Hop, Shake }

    public struct Line
    {
        public bool player;          // true = 玩家说的话
        public string text;
        public Fx fx;                // 琦琦这一句说话时的小动作
        public Action onShow;        // 这一句出现的时候执行（比如让棋盘掉下来）
    }

    public static Line Qiqi(string text, Fx fx = Fx.None, Action onShow = null)
    {
        return new Line { player = false, text = text, fx = fx, onShow = onShow };
    }

    public static Line Me(string text)
    {
        return new Line { player = true, text = text };
    }

    // ---------------- 外观 ----------------
    const float TypeSpeed = 44f;                                 // 每秒冒出几个字
    static readonly Vector2 BoxSize = new Vector2(1500f, 290f);
    static readonly Vector2 BoxPos = new Vector2(0f, -345f);
    static readonly Vector2 PortraitSize = new Vector2(418f, 400f);
    static readonly Vector2 PortraitPos = new Vector2(-650f, -55f);
    static readonly Vector2 TextPos = new Vector2(110f, -352f);
    static readonly Vector2 TextSize = new Vector2(1020f, 176f);
    static readonly Color PlayerInk = new Color(0.10f, 0.28f, 0.36f, 1f);
    static readonly Color PlayerPortraitTint = new Color(0.55f, 0.55f, 0.62f, 1f);

    // 立绘：从 qiqi_snake 大图里裁出小蛇那一块（原图像素，原点在左上角）
    static readonly Rect PortraitRaw = new Rect(500f, 345f, 510f, 490f);
    static Sprite s_Portrait;

    static Sprite Portrait()
    {
        if (s_Portrait == null)
            s_Portrait = WorkshopUI.CropSprite(Resources.Load<Sprite>("Art/Qiqi/qiqi_snake"), PortraitRaw);
        return s_Portrait;
    }

    Line[] m_Lines;
    int m_Index;
    Action m_OnDone;

    CanvasGroup m_Group;
    RectTransform m_BoxRect, m_PortraitRect, m_TagRect, m_ArrowRect;
    Image m_PortraitImage;
    TextMeshProUGUI m_Text, m_Name;
    GameObject m_Hint;

    bool m_Typing, m_Closing, m_Ready;
    float m_TypeTime;
    float m_LineStart;
    float m_Intro;                 // 0 → 1：入场动画
    Fx m_Fx;

    public bool IsOpen => !m_Closing;

    /// <summary>
    /// 开始一段对话。parent 一般是内容舞台；dim = 背后压多暗；onDone 在对话框关掉（说完或者跳过）以后调用。
    /// </summary>
    public static StoryDialogue Play(Transform parent, Line[] lines, Action onDone, bool allowSkip = true, float dim = 0.4f)
    {
        var rect = WorkshopUI.NewStretch(parent, "StoryDialogue");
        rect.SetAsLastSibling();
        var dialog = rect.gameObject.AddComponent<StoryDialogue>();
        dialog.m_Lines = lines;
        dialog.m_OnDone = onDone;
        dialog.Build(rect, allowSkip, dim);
        dialog.ShowLine();
        return dialog;
    }

    void Build(RectTransform root, bool allowSkip, float dim)
    {
        m_Group = root.gameObject.AddComponent<CanvasGroup>();
        m_Group.alpha = 0f;

        // 背后压暗，同时挡住下面的鼠标；点哪里都是"下一句"
        var dimmer = WorkshopUI.NewStretch(root, "Dim");
        var dimImage = dimmer.gameObject.AddComponent<Image>();
        dimImage.color = new Color(0.05f, 0.03f, 0.02f, dim);
        dimImage.raycastTarget = true;
        var dimButton = dimmer.gameObject.AddComponent<Button>();
        dimButton.transition = Selectable.Transition.None;
        dimButton.onClick.AddListener(Advance);

        // 对话框
        var box = WorkshopUI.NewImage(root, "Box", WorkshopUI.LoadSprite("UI/panel_parchment"), BoxPos, BoxSize, true, false);
        m_BoxRect = box.rectTransform;

        m_Text = WorkshopUI.NewLabel(box.transform, "Text", "", 38f, WorkshopUI.Ink, TextPos - BoxPos, TextSize,
            TextAlignmentOptions.TopLeft, true);
        m_Text.lineSpacing = 8f;
        m_Text.enableAutoSizing = true;
        m_Text.fontSizeMax = 38f;
        m_Text.fontSizeMin = 28f;

        // 右下角的提示：说完一句以后出现，▼ 轻轻上下晃
        m_Hint = WorkshopUI.NewRect(box.transform, "Hint", Vector2.zero, Vector2.zero).gameObject;
        WorkshopUI.NewLabel(m_Hint.transform, "Tip", "点击或按空格继续", 22f, WorkshopUI.InkSoft,
            new Vector2(540f, -106f), new Vector2(300f, 32f), TextAlignmentOptions.MidlineRight);
        var arrow = WorkshopUI.NewLabel(m_Hint.transform, "Arrow", "▼", 30f, new Color(0.78f, 0.5f, 0.12f),
            new Vector2(712f, -104f), new Vector2(40f, 40f));
        m_ArrowRect = arrow.rectTransform;
        m_Hint.SetActive(false);

        // 名牌：压在对话框的上沿
        var tag = WorkshopUI.NewImage(box.transform, "NameTag", WorkshopUI.LoadSprite("UI/btn_wood"), new Vector2(-270f, 150f),
            new Vector2(230f, 66f), true, false);
        m_TagRect = tag.rectTransform;
        m_Name = WorkshopUI.NewLabel(tag.transform, "Name", "琦琦", 34f, WorkshopUI.Cream, Vector2.zero, new Vector2(210f, 56f));

        // 立绘（画在对话框上面，下半身压住对话框的左边）
        var portrait = WorkshopUI.NewImage(root, "Portrait", Portrait(), PortraitPos, PortraitSize);
        m_PortraitRect = portrait.rectTransform;
        m_PortraitImage = portrait;

        if (allowSkip)
        {
            var skip = PlaqueButton.Create(root, "Skip", "跳过", new Vector2(810f, 470f), new Vector2(150f, 58f), 28, Finish);
            skip.transform.SetAsLastSibling();
        }

        StartCoroutine(IntroRoutine());
    }

    IEnumerator IntroRoutine()
    {
        const float duration = 0.3f;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            m_Intro = Mathf.SmoothStep(0f, 1f, t / duration);
            m_Group.alpha = m_Intro;
            yield return null;
        }
        m_Intro = 1f;
        m_Group.alpha = 1f;
        m_Ready = true;
    }

    // ---------------- 一句话 ----------------
    void ShowLine()
    {
        var line = m_Lines[m_Index];
        m_Name.text = line.player ? "你" : "琦琦";
        m_TagRect.anchoredPosition = new Vector2(line.player ? 560f : -270f, 150f);
        m_Text.color = line.player ? PlayerInk : WorkshopUI.Ink;
        m_Text.text = line.text;
        m_Text.maxVisibleCharacters = 0;
        m_PortraitImage.color = line.player ? PlayerPortraitTint : Color.white;
        m_Hint.SetActive(false);

        m_Typing = true;
        m_TypeTime = 0f;
        m_LineStart = Time.unscaledTime;
        m_Fx = line.player ? Fx.None : line.fx;

        line.onShow?.Invoke();
    }

    void Advance()
    {
        if (m_Closing || !m_Ready)
            return;

        if (m_Typing)
        {
            // 这一句还没冒完：先全部显示出来
            m_Typing = false;
            m_Text.maxVisibleCharacters = int.MaxValue;
            m_Hint.SetActive(true);
            return;
        }

        UiSfx.PlayClick();
        if (m_Index >= m_Lines.Length - 1)
        {
            Finish();
            return;
        }
        m_Index++;
        ShowLine();
    }

    void Finish()
    {
        if (m_Closing)
            return;
        m_Closing = true;
        StopAllCoroutines();
        StartCoroutine(CloseRoutine());
    }

    IEnumerator CloseRoutine()
    {
        const float duration = 0.25f;
        float startAlpha = m_Group.alpha;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            m_Group.alpha = startAlpha * (1f - Mathf.SmoothStep(0f, 1f, t / duration));
            yield return null;
        }
        var onDone = m_OnDone;
        Destroy(gameObject);
        onDone?.Invoke();
    }

    // ---------------- 每帧：打字、立绘小动作、键盘 ----------------
    void Update()
    {
        if (m_Closing)
            return;

        if (m_Ready && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
            Advance();
        if (m_Closing || m_Index < 0 || m_Index >= m_Lines.Length)
            return;

        float now = Time.unscaledTime;
        var line = m_Lines[m_Index];

        if (m_Typing)
        {
            m_TypeTime += Time.unscaledDeltaTime;
            int visible = (int)(m_TypeTime * TypeSpeed);
            m_Text.maxVisibleCharacters = visible;
            if (visible >= line.text.Length)
            {
                m_Typing = false;
                m_Text.maxVisibleCharacters = int.MaxValue;
                m_Hint.SetActive(true);
            }
        }

        if (m_Hint.activeSelf)
            m_ArrowRect.anchoredPosition = new Vector2(712f, -104f + Mathf.Sin(now * 5f) * 5f);

        // 立绘：入场时从左边滑进来；说话时轻轻点头；有特殊动作的句子再加一下
        var pos = PortraitPos + new Vector2(-140f * (1f - m_Intro), 0f);
        float scale = 1f;
        if (!line.player)
        {
            pos.y += Mathf.Sin(now * 1.8f) * 4f;
            if (m_Typing)
                pos.y += Mathf.Abs(Mathf.Sin(now * 13f)) * 7f;
        }
        else
        {
            scale = 0.97f;
        }

        float since = now - m_LineStart;
        if (m_Fx == Fx.Hop && since < 0.8f)
        {
            // 开心地跳两下
            float u = since / 0.8f;
            pos.y += Mathf.Abs(Mathf.Sin(u * Mathf.PI * 2f)) * 46f * (1f - u * 0.5f);
        }
        else if (m_Fx == Fx.Shake && since < 0.9f)
        {
            // 吓了一跳：左右抖，越抖越小
            float u = since / 0.9f;
            pos.x += Mathf.Sin(since * 48f) * 22f * (1f - u);
            pos.y += Mathf.Abs(Mathf.Sin(u * Mathf.PI * 3f)) * 20f * (1f - u);
            m_PortraitRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(since * 40f) * 7f * (1f - u));
        }
        else
        {
            m_PortraitRect.localRotation = Quaternion.identity;
        }

        m_PortraitRect.anchoredPosition = pos;
        m_PortraitRect.localScale = Vector3.one * scale;
        m_BoxRect.anchoredPosition = BoxPos + new Vector2(0f, -70f * (1f - m_Intro));
    }
}
