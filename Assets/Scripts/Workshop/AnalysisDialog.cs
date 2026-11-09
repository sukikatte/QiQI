using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 关卡分析弹窗：先显示"正在计算…"，算完把结果填进来
/// （最少步数 + 最短路线 / 最优解法数 / 局面总数 / 无解局面占比 / 暗坑 / 难度），再给几句设计建议。
/// </summary>
public class AnalysisDialog : MonoBehaviour
{
    static readonly Color[] DifficultyColors =
    {
        new Color(0.45f, 0.72f, 0.32f), new Color(0.62f, 0.76f, 0.28f), new Color(0.93f, 0.72f, 0.22f),
        new Color(0.92f, 0.50f, 0.20f), new Color(0.82f, 0.28f, 0.22f),
    };

    static readonly Color ValueInk = new Color(0.05f, 0.04f, 0.03f);

    GameObject m_Overlay;
    GameObject m_Stats;
    TextMeshProUGUI m_Waiting;
    TextMeshProUGUI m_Tip;
    TextMeshProUGUI m_RouteCaption;
    readonly TextMeshProUGUI[] m_Values = new TextMeshProUGUI[6];
    const int DifficultyIndex = 5;
    readonly Image[] m_Bars = new Image[5];
    Action m_OnClosed;
    float m_Time;
    bool m_Computing = true;

    public bool IsOpen => m_Overlay != null;

    public static AnalysisDialog Show(Transform canvas, string subtitle, Action onClosed)
    {
        var overlay = WorkshopUI.NewDimmer(canvas);
        var dialog = overlay.gameObject.AddComponent<AnalysisDialog>();
        dialog.m_Overlay = overlay.gameObject;
        dialog.m_OnClosed = onClosed;
        dialog.Build(overlay, subtitle);
        return dialog;
    }

    // 背景：eva 图（带藤蔓和宝石角的羊皮纸），裁掉四周的透明边
    static readonly Rect PanelRaw = new Rect(50f, 45f, 1430f, 920f);
    static readonly Vector2 PanelSize = new Vector2(1400f, 900f);
    static Sprite s_Panel;

    static Sprite PanelSprite()
    {
        if (s_Panel == null)
            s_Panel = WorkshopUI.CropSprite(Resources.Load<Sprite>("Art/UI/eva"), PanelRaw);
        return s_Panel;
    }

    void Build(RectTransform overlay, string subtitle)
    {
        var panel = WorkshopUI.NewImage(overlay, "Panel", PanelSprite(), Vector2.zero, PanelSize, false, true);
        var p = panel.transform;

        WorkshopUI.NewLabel(p, "Title", "关卡分析", 58f, WorkshopUI.Ink,
            new Vector2(0f, 352f), new Vector2(900f, 80f));
        WorkshopUI.NewLabel(p, "Subtitle", subtitle, 28f, WorkshopUI.InkSoft,
            new Vector2(0f, 296f), new Vector2(900f, 44f));

        m_Waiting = WorkshopUI.NewLabel(p, "Waiting", "正在计算", 50f, WorkshopUI.InkSoft,
            new Vector2(0f, 20f), new Vector2(800f, 90f));

        m_Stats = WorkshopUI.NewRect(p, "Stats", Vector2.zero, Vector2.zero).gameObject;
        string[] labels = { "最少步数", "最优解法", "局面总数", "无解局面", "其中暗坑", "难度" };
        Vector2[] cardPos =
        {
            new Vector2(-265f, 71f), new Vector2(-265f, 153f), new Vector2(265f, 235f),
            new Vector2(265f, 153f), new Vector2(265f, 71f), new Vector2(-265f, 235f),
        };
        var cardSize = new Vector2(480f, 68f);
        for (int i = 0; i < m_Values.Length; i++)
        {
            var card = WorkshopUI.NewImage(m_Stats.transform, "Card_" + i, null, cardPos[i], cardSize);
            card.color = new Color(0.45f, 0.28f, 0.12f, 0.12f);
            WorkshopUI.NewLabel(card.transform, "Label", labels[i], 28f, WorkshopUI.InkSoft, new Vector2(-120f, 0f),
                new Vector2(200f, 50f), TextAlignmentOptions.MidlineLeft);
            m_Values[i] = WorkshopUI.NewLabel(card.transform, "Value", "", 34f, WorkshopUI.Ink, new Vector2(80f, 0f),
                new Vector2(250f, 56f), TextAlignmentOptions.MidlineRight);
        }

        // 难度条：五小格，挂在"难度"那张卡的左边
        var difficultyCard = m_Values[DifficultyIndex].transform.parent;
        for (int i = 0; i < 5; i++)
        {
            m_Bars[i] = WorkshopUI.NewImage(difficultyCard, "Bar_" + i, null, new Vector2(-52f + i * 29f, -9f), new Vector2(25f, 16f));
            m_Bars[i].color = new Color(0.45f, 0.28f, 0.12f, 0.25f);
        }

        const float routeFont = 26f;
        m_RouteCaption = WorkshopUI.NewLabel(m_Stats.transform, "RouteCaption", "", routeFont, WorkshopUI.Ink,
            new Vector2(0f, -38f), new Vector2(940f, 64f), TextAlignmentOptions.TopLeft, true);
        m_RouteCaption.enableAutoSizing = true;
        m_RouteCaption.fontSizeMax = routeFont;
        m_RouteCaption.fontSizeMin = 14f;
        m_Stats.SetActive(false);

        const float tipFont = 26f;
        m_Tip = WorkshopUI.NewLabel(p, "Tip", "", tipFont, WorkshopUI.Ink,
            new Vector2(0f, -222f), new Vector2(940f, 190f),
            TextAlignmentOptions.TopLeft, true);
        m_Tip.lineSpacing = 4f;
        m_Tip.enableAutoSizing = true;
        m_Tip.fontSizeMax = tipFont;
        m_Tip.fontSizeMin = 18f;

        PlaqueButton.Create(p, "Close", "关闭",
            new Vector2(0f, -378f), new Vector2(240f, 72f), 34f, Close);
    }

    void Update()
    {
        if (!m_Computing || m_Waiting == null)
            return;
        m_Time += Time.unscaledDeltaTime;
        int dots = 1 + (int)(m_Time * 2.5f) % 3;
        m_Waiting.text = "正在计算" + new string('.', dots);
    }

    public void Close()
    {
        if (m_Overlay == null)
            return;
        var overlay = m_Overlay;
        m_Overlay = null;
        Destroy(overlay);
        m_OnClosed?.Invoke();
    }

    // ====================================================================
    //  填结果
    // ====================================================================
    public void ShowResult(LevelAnalysis a)
    {
        if (m_Overlay == null)
            return;
        m_Computing = false;
        m_Waiting.gameObject.SetActive(false);

        if (a.truncated)
        {
            m_Tip.text = SafeFormat("这张地图太开阔了，局面多到算不完（已经搜过 {0} 种）。\n试试多放几块水晶，或者少放几个蛋，再来算一次。",
                a.states.ToString("N0"));
            return;
        }

        m_Stats.SetActive(true);
        if (!a.solvable)
        {
            for (int i = 0; i < m_Values.Length; i++)
                m_Values[i].text = "—";
            m_Values[2].text = a.states.ToString("N0");
            m_Values[DifficultyIndex].text = "无解";
            m_Values[DifficultyIndex].color = ValueInk;
            m_RouteCaption.text = "";
            m_Tip.text = BuildTip(a);
            return;
        }

        m_Values[0].text = a.minSteps + " 步";
        m_Values[1].text = FormatRoutes(a.optimalRoutes);
        m_Values[2].text = a.states.ToString("N0");
        m_Values[3].text = Mathf.RoundToInt(a.LoseRatio * 100f) + "%";
        m_Values[4].text = a.hiddenTrapStates.ToString("N0");
        m_Values[DifficultyIndex].text = a.difficultyName;
        m_Values[DifficultyIndex].color = ValueInk;          // 字是黑的，只有下面的小格子带颜色
        m_RouteCaption.text = "最短路线（共" + a.minSteps + "步）：" + FormatRoute(a.route);
        for (int i = 0; i < m_Bars.Length; i++)
            m_Bars[i].color = i < a.difficulty
                ? DifficultyColors[Mathf.Clamp(a.difficulty - 1, 0, 4)]
                : new Color(0.45f, 0.28f, 0.12f, 0.25f);
        m_Tip.text = BuildTip(a);
    }

    public void ShowError(string message)
    {
        if (m_Overlay == null)
            return;
        m_Computing = false;
        m_Waiting.gameObject.SetActive(true);
        m_Waiting.fontSize = 34f;
        m_Waiting.text = "分析失败了：" + message;
    }

    static string FormatRoute(string route)
    {
        if (string.IsNullOrEmpty(route))
            return "";
        const int MaxShown = 240;
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < route.Length && i < MaxShown; i++)
        {
            if (i > 0 && i % 5 == 0)
                sb.Append(' ');
            sb.Append(route[i]);
        }
        if (route.Length > MaxShown)
            sb.Append(" …");
        return sb.ToString();
    }

    static string FormatRoutes(double n)
    {
        if (n >= 1e6)
            return "100 万种以上";
        return ((long)n).ToString("N0") + " 种";
    }

    static string SafeFormat(string tmpl, params object[] args)
    {
        if (string.IsNullOrEmpty(tmpl))
            return "";
        try { return string.Format(tmpl, args); }
        catch (FormatException) { return tmpl; }
    }

    static string BuildTip(LevelAnalysis a)
    {
        if (!a.solvable)
        {
            if (a.boxCount > a.goalCount)
            {
                return SafeFormat("这一关现在没法通关：有 {0} 个蛋，却只有 {1} 个展示盒。\n蛋不能比展示盒多，补几个展示盒，或者擦掉几个蛋吧。",
                    a.boxCount, a.goalCount);
            }
            return "这一关现在没法通关。常见的原因：\n· 蛋一开始就在角落，或者被水晶 / 其他蛋堵死了；\n· 蛋贴着棋盘边，而那条边上没有展示盒；\n· 水晶把琦琦或者展示盒围住了。";
        }

        var lines = new System.Collections.Generic.List<string>();
        if (a.optimalRoutes <= 1.5)
            lines.Add("· 最优解法只有 1 种：谜题很「唯一」，通关会很有成就感，不过也别太刁钻哦。");
        else if (a.optimalRoutes >= 20)
            lines.Add("· 有很多种过关的路线，比较宽松。想变难，可以放水晶堵住捷径。");
        else
            lines.Add("· 最优解法有 " + (long)a.optimalRoutes + " 种，既有选择，也要动动脑。");

        if (a.LoseRatio >= 0.5f)
            lines.Add("· 超过一半的局面都已经没救，玩家很容易走错。");
        else if (a.LoseRatio <= 0.1f)
            lines.Add("· 几乎怎么走都能通关，很温柔。想更有挑战，就让蛋更容易被卡住。");

        if (a.hiddenTrapStates > 0 && a.states > 0 && a.hiddenTrapStates * 5 > a.states)
            lines.Add("· 有不少「暗坑」：局面已经没救，游戏却还没弹失败，玩家会白忙一阵，才发现要重来。");

        if (a.minSteps <= 6)
            lines.Add("· 几步就能通关，适合当入门关。");
        return string.Join("\n", lines);
    }
}
