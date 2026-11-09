using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 创意工坊：关卡列表 → 编辑 → 测试 / 正式游玩。
/// 四种模式共用同一块棋盘和同一个侧栏，只是按钮和可做的事不同：
///   List  关卡列表（游玩 / 编辑 / 删除 / 创建新关卡）
///   Edit  编辑（五种工具 + 外观选择 + 测试 / 保存 / 返回）
///   Test  测试（暂停 / 重新测试 / 继续摆关；暂停时可以改布局）
///   Play  正式游玩（退出 / 重新开始；通关弹窗）
/// UI 全部在运行时生成，风格复用背景图里的木头 + 羊皮纸 + 金色。
/// </summary>
public class WorkshopController : MonoBehaviour
{
    enum Mode { List, Edit, Test, Play }
    enum Tool { Box, Wall, Snake, Goal, Erase }

    // ---------------- 可调参数 ----------------
    const int DefaultBoardSize = 8;
    const int MinBoardSize = 3;
    const int MaxBoardSize = 9;
    const float BoardPixels = 960f;
    static readonly Vector2 BoardCenter = new Vector2(0f, -240f);
    static readonly Vector2 NotebookCenter = new Vector2(655f, -255f);
    static readonly Vector2 BarPos = new Vector2(0f, 340f);
    const float BarW = 1200f, BarH = 380f;
    static readonly Vector2 NamePos = new Vector2(-200f, 58f);
    static readonly Vector2 NameSize = new Vector2(520f, 56f);
    const float NameFont = 60f;
    static readonly Vector2 StatusPos = new Vector2(180f, 58f);
    static readonly Vector2 StatusSize = new Vector2(520f, 56f);
    const float StatusFont = 42f;
    const float ActionY = -52f, ActionX = 0f, ActionW = 220f, ActionH = 100f, ActionGap = 18f, ActionFont = 36f;
    static readonly Vector2 LeftPos = new Vector2(-772f, -80f);
    static readonly Vector2 ToolEggPos = new Vector2(-110f, 230f);
    static readonly Vector2 ToolCrystalPos = new Vector2(110f, 230f);
    static readonly Vector2 ToolSnakePos = new Vector2(-110f, 148f);
    static readonly Vector2 ToolGoalPos = new Vector2(110f, 148f);
    static readonly Vector2 ToolErasePos = new Vector2(0f, 66f);
    static readonly Vector2 ToolHalf = new Vector2(204f, 72f);
    static readonly Vector2 ToolEraseSize = new Vector2(424f, 72f);
    const float ToolFont = 32f;
    const int MaxUndo = 80;
    const int MaxNameChars = 18;           // 列表里关卡名最多显示多少字，多了用 … 代替
    const string DefaultLevelName = "未命名关卡";

    // ---------------- 引用 ----------------
    Transform m_Canvas;

    GameObject m_ListRoot;
    ScrollRect m_ListScroll;
    RectTransform m_ListContent;
    TextMeshProUGUI m_EmptyLabel;

    GameObject m_BoardRoot;
    GameObject m_PanelRoot;
    WorkshopBoardView m_Board;

    TextMeshProUGUI m_NameLabel, m_StatusLabel;
    GameObject m_ToolsGroup;
    PlaqueButton[] m_ToolButtons;
    SkinPicker m_Skins;
    RectTransform m_Notebook;              // 编辑页右侧的“设计手记”
    PlaqueButton[] m_ActionButtons;

    RectTransform m_ToastRect;
    CanvasGroup m_ToastGroup;
    TextMeshProUGUI m_ToastLabel;
    Coroutine m_ToastRoutine;
    Coroutine m_RefreshListRoutine;

    // ---------------- 状态 ----------------
    Mode m_Mode = Mode.List;
    Tool m_Tool = Tool.Box;
    SokobanGame m_World;              // 当前棋盘上的世界（编辑 / 测试 / 游玩共用）
    string m_LevelId;                 // 正在编辑的关卡 id（null = 还没保存过）
    string m_LevelName = DefaultLevelName;
    bool m_Dirty;                     // 编辑后还没保存
    LevelData m_PlayLevel;            // 正式游玩的那一关（用来“重新开始”）
    SokobanGame m_TestSnapshot;       // 本轮测试开始时的布局
    bool m_TestPaused;
    bool m_Modal;                     // 有弹窗时：停掉棋盘输入和移动

    readonly MoveHold m_Hold = new MoveHold();

    readonly List<SokobanGame> m_Undo = new List<SokobanGame>();
    SokobanGame m_StrokeBefore;       // 这一笔开始前的棋盘，拖着画只记一次撤回
    bool m_FailOpen;

    bool IsEditable => m_Mode == Mode.Edit || (m_Mode == Mode.Test && m_TestPaused);
    bool CanUndo => m_Mode == Mode.Edit || m_Mode == Mode.Test || m_Mode == Mode.Play;

    // ====================================================================
    //  初始化
    // ====================================================================
    public void Init(Transform canvas, TMP_FontAsset font)
    {
        m_Canvas = canvas;
        WorkshopUI.Font = font;

        BuildList();
        BuildBoardAndPanel();
        BuildToast();
        ShowList();
    }

    // ---------------- 关卡列表界面 ----------------
    void BuildList()
    {
        var root = WorkshopUI.NewStretch(m_Canvas, "ListScreen");
        m_ListRoot = root.gameObject;

        // 打开的卷轴做底
        var scrollSprite = Resources.Load<Sprite>("Art/UI/scroll");
        WorkshopUI.NewImage(root, "Scroll", scrollSprite, new Vector2(0f, 60f), new Vector2(1700f, 956f));
        // 标题：用你做的图片（1400×600，保持比例），像一块招牌挂在卷轴上沿
        var titleSprite = Resources.Load<Sprite>("Art/UI/workshop_title");
        if (titleSprite != null)
            WorkshopUI.NewImage(root, "Title", titleSprite, new Vector2(0f, 392f), new Vector2(640f, 274f));
        else
            WorkshopUI.NewLabel(root, "Title", "创意工坊", 64, WorkshopUI.Ink, new Vector2(0f, 285f), new Vector2(800f, 90f));

        // 可滚动的关卡列表：大约露出 3 条，第 4 条冒一点头提示还能往下滚
        var scrollRect = WorkshopUI.NewRect(root, "LevelScroll", new Vector2(0f, 12f), new Vector2(1160f, 338f));
        var sr = scrollRect.gameObject.AddComponent<ScrollRect>();
        m_ListScroll = sr;

        var viewport = WorkshopUI.NewStretch(scrollRect, "Viewport");
        var vpImage = viewport.gameObject.AddComponent<Image>();
        vpImage.color = new Color(0f, 0f, 0f, 0f);     // 透明，但能拖、能滚轮
        vpImage.raycastTarget = true;
        viewport.gameObject.AddComponent<RectMask2D>();

        var contentGo = new GameObject("Content", typeof(RectTransform));
        m_ListContent = (RectTransform)contentGo.transform;
        m_ListContent.SetParent(viewport, false);
        m_ListContent.anchorMin = new Vector2(0f, 1f);
        m_ListContent.anchorMax = new Vector2(1f, 1f);
        m_ListContent.pivot = new Vector2(0.5f, 1f);
        m_ListContent.anchoredPosition = Vector2.zero;
        m_ListContent.sizeDelta = Vector2.zero;

        var vlg = contentGo.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 10f;
        vlg.padding = new RectOffset(0, 0, 4, 4);
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        contentGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        sr.viewport = viewport;
        sr.content = m_ListContent;
        sr.horizontal = false;
        sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.inertia = true;
        sr.scrollSensitivity = 48f;

        m_EmptyLabel = WorkshopUI.NewLabel(root, "Empty",
            "这里还没有关卡\n点下面的「创建新的关卡」，布置你的第一间收藏室吧", 38, WorkshopUI.InkSoft,
            new Vector2(0f, 12f), new Vector2(960f, 180f), TextAlignmentOptions.Center, true);

        // 底部按钮（放在卷轴下方，压在地板上）
        PlaqueButton.Create(root, "CreateButton", "创建新的关卡", new Vector2(-210f, -335f), new Vector2(380f, 92f), 38,
            ChooseSizeThenEdit);
        PlaqueButton.Create(root, "BackButton", "返回主页", new Vector2(210f, -335f), new Vector2(380f, 92f), 38,
            () => ScreenTransition.LoadScene("MainMenu"));

        root.Find("Title")?.SetAsLastSibling();
        root.Find("CreateButton")?.SetAsLastSibling();
        root.Find("BackButton")?.SetAsLastSibling();
    }

    void RefreshList()
    {
        // 删除按钮还在这一行上：不能立刻 DestroyImmediate，等点击回调结束再拆
        if (m_RefreshListRoutine != null)
            StopCoroutine(m_RefreshListRoutine);
        m_RefreshListRoutine = StartCoroutine(RefreshListNextFrame());
    }

    IEnumerator RefreshListNextFrame()
    {
        yield return null;
        RebuildListNow();
        m_RefreshListRoutine = null;
    }

    void RebuildListNow()
    {
        if (m_ListContent == null)
            return;

        for (int i = m_ListContent.childCount - 1; i >= 0; i--)
            DestroyImmediate(m_ListContent.GetChild(i).gameObject);

        var levels = LevelStorage.LoadAll();
        if (m_EmptyLabel != null)
            m_EmptyLabel.gameObject.SetActive(levels.Count == 0);
        foreach (var level in levels)
            AddListRow(level);

        LayoutRebuilder.ForceRebuildLayoutImmediate(m_ListContent);
        SnapListToTop();
        StartCoroutine(SnapListToTopNextFrame());
    }

    void SnapListToTop()
    {
        if (m_ListScroll == null || m_ListContent == null)
            return;
        m_ListScroll.StopMovement();
        Canvas.ForceUpdateCanvases();
        m_ListContent.anchoredPosition = Vector2.zero;
    }

    IEnumerator SnapListToTopNextFrame()
    {
        yield return null;
        SnapListToTop();
        yield return new WaitForEndOfFrame();
        SnapListToTop();
    }

    void AddListRow(LevelData level)
    {
        var rowGo = new GameObject("Row", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        rowGo.transform.SetParent(m_ListContent, false);
        rowGo.GetComponent<Image>().color = new Color(0.45f, 0.28f, 0.12f, 0.14f);
        rowGo.GetComponent<LayoutElement>().preferredHeight = 92f;
        var row = (RectTransform)rowGo.transform;

        string sub = level.boxes.Count + " 个蛋 · " + level.goals.Count + " 个展示盒 · "
                     + level.walls.Count + " 块水晶 · " + level.boardSize + "×" + level.boardSize;
        string text = Truncate(level.name) + "\n<size=24><color=#8A6A44>" + sub + "</color></size>";
        var name = WorkshopUI.NewLabel(row, "Name", text, 34, WorkshopUI.Ink, Vector2.zero, new Vector2(640f, 84f),
            TextAlignmentOptions.MidlineLeft);
        WorkshopUI.AnchorTo(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(28f, 0f));

        var size = new Vector2(132f, 66f);
        string id = level.id;
        var play = PlaqueButton.Create(row, "Play", "游玩", Vector2.zero, size, 32, () => EnterPlay(level));
        var edit = PlaqueButton.Create(row, "Edit", "编辑", Vector2.zero, size, 32, () => EnterEdit(level));
        var del = PlaqueButton.Create(row, "Delete", "删除", Vector2.zero, size, 32, () =>
        {
            LevelStorage.Delete(id);
            rowGo.SetActive(false);
            RefreshList();
        });
        var right = new Vector2(1f, 0.5f);
        WorkshopUI.AnchorTo((RectTransform)del.transform, right, right, new Vector2(-18f, 0f));
        WorkshopUI.AnchorTo((RectTransform)edit.transform, right, right, new Vector2(-18f - 144f, 0f));
        WorkshopUI.AnchorTo((RectTransform)play.transform, right, right, new Vector2(-18f - 288f, 0f));
    }

    static string Truncate(string name)
    {
        if (string.IsNullOrEmpty(name))
            return DefaultLevelName;
        return name.Length <= MaxNameChars ? name : name.Substring(0, MaxNameChars) + "…";
    }

    void RefreshNameLabel()
    {
        if (m_NameLabel == null)
            return;
        string name = Truncate(m_LevelName);
        if (m_World != null)
            name += "  " + m_World.Size + "×" + m_World.Size;
        m_NameLabel.text = name;
    }

    // ---------------- 棋盘 + 侧栏 ----------------
    void BuildBoardAndPanel()
    {
        var boardHolder = WorkshopUI.NewRect(m_Canvas, "BoardHolder", BoardCenter, Vector2.zero);
        m_BoardRoot = boardHolder.gameObject;

        var boardRect = WorkshopUI.NewRect(boardHolder, "Board", Vector2.zero, new Vector2(BoardPixels, BoardPixels));
        var catcher = boardRect.gameObject.AddComponent<Image>();
        catcher.color = new Color(0f, 0f, 0f, 0f);
        catcher.raycastTarget = true;
        m_Board = boardRect.gameObject.AddComponent<WorkshopBoardView>();
        m_Board.Build(DefaultBoardSize, BoardPixels);
        m_Board.CellPressed += OnCellPressed;

        var panel = WorkshopUI.NewImage(m_Canvas, "InfoBar", WorkshopUI.DesignBarSprite(), BarPos,
            new Vector2(BarW, BarH), false, true);
        panel.preserveAspect = false;
        m_PanelRoot = panel.gameObject;
        var p = panel.transform;

        m_NameLabel = WorkshopUI.NewLabel(p, "Name", "", NameFont, WorkshopUI.Cream, NamePos,
            NameSize, TextAlignmentOptions.MidlineLeft);
        m_StatusLabel = WorkshopUI.NewLabel(p, "Status", "", StatusFont, WorkshopUI.Cream, StatusPos,
            StatusSize, TextAlignmentOptions.MidlineRight);

        m_Skins = SkinPicker.Create(m_Canvas, LeftPos, new Vector2(SkinPicker.PanelWidth, SkinPicker.PanelHeight));
        m_Skins.Picked += isEgg =>
        {
            SelectTool(isEgg ? Tool.Box : Tool.Wall);
            RefreshToolIcons();
        };

        m_ToolsGroup = WorkshopUI.NewRect(m_Skins.transform, "Tools", Vector2.zero, Vector2.zero).gameObject;
        string[] toolNames = { "放蛋", "放水晶", "琦琦起点", "放展示盒", "擦除" };
        Sprite[] icons =
        {
            SkinLibrary.Egg(0), SkinLibrary.Crystal(0), WorkshopUI.LoadSprite("Board/snake_token"), SkinLibrary.DisplayPad(), null
        };
        m_ToolButtons = new PlaqueButton[5];
        for (int i = 0; i < 5; i++)
        {
            var tool = (Tool)i;
            m_ToolButtons[i] = PlaqueButton.Create(m_ToolsGroup.transform, "Tool_" + i, toolNames[i],
                Vector2.zero, new Vector2(204f, 72f), 30, () => SelectTool(tool), icons[i]);
        }
        LayoutTools();
        RefreshToolIcons();

        m_Notebook = DesignNotebook.CreateIcon(m_Canvas, NotebookCenter, OpenNotebook);

        m_ActionButtons = new PlaqueButton[4];
        for (int i = 0; i < m_ActionButtons.Length; i++)
            m_ActionButtons[i] = PlaqueButton.Create(p, "Action_" + i, "", new Vector2(0f, ActionY),
                new Vector2(ActionW, ActionH), ActionFont, null);
    }

    void LayoutTools()
    {
        if (m_ToolButtons == null || m_ToolButtons.Length < 5)
            return;
        PlaceTool(m_ToolButtons[0], ToolEggPos, ToolHalf, ToolFont);
        PlaceTool(m_ToolButtons[1], ToolCrystalPos, ToolHalf, ToolFont);
        PlaceTool(m_ToolButtons[2], ToolSnakePos, ToolHalf, ToolFont);
        PlaceTool(m_ToolButtons[3], ToolGoalPos, ToolHalf, ToolFont);
        PlaceTool(m_ToolButtons[4], ToolErasePos, ToolEraseSize, ToolFont);
    }

    static void PlaceTool(PlaqueButton button, Vector2 pos, Vector2 size, float font)
    {
        if (button == null)
            return;
        button.SetPosSize(pos, size);
        button.SetFontSize(font);
    }

    void BuildToast()
    {
        m_ToastRect = WorkshopUI.NewRect(m_Canvas, "Toast", new Vector2(0f, 492f), new Vector2(760f, 76f));
        var img = m_ToastRect.gameObject.AddComponent<Image>();
        img.sprite = WorkshopUI.LoadSprite("UI/btn_wood");
        img.type = Image.Type.Sliced;
        img.raycastTarget = false;
        m_ToastGroup = m_ToastRect.gameObject.AddComponent<CanvasGroup>();
        m_ToastGroup.alpha = 0f;
        m_ToastGroup.blocksRaycasts = false;
        m_ToastLabel = WorkshopUI.NewLabel(m_ToastRect, "Text", "", 32, WorkshopUI.Cream, Vector2.zero, new Vector2(720f, 60f));
    }

    void ShowToast(string message)
    {
        m_ToastLabel.text = message;
        m_ToastRect.anchoredPosition = m_Mode == Mode.List
            ? new Vector2(0f, 450f)
            : BarPos + new Vector2(0f, -BarH * 0.5f - 40f);
        m_ToastRect.SetAsLastSibling();
        if (m_ToastRoutine != null)
            StopCoroutine(m_ToastRoutine);
        m_ToastRoutine = StartCoroutine(ToastRoutine());
    }

    IEnumerator ToastRoutine()
    {
        m_ToastGroup.alpha = 1f;
        yield return new WaitForSecondsRealtime(2.2f);
        for (float t = 0f; t < 0.4f; t += Time.unscaledDeltaTime)
        {
            m_ToastGroup.alpha = 1f - t / 0.4f;
            yield return null;
        }
        m_ToastGroup.alpha = 0f;
        m_ToastRoutine = null;
    }

    // ====================================================================
    //  模式切换
    // ====================================================================
    void ShowList()
    {
        m_Mode = Mode.List;
        m_World = null;
        m_TestPaused = false;
        ClearUndo();
        ApplyMode();
        RefreshList();
    }

    /// <summary>新建关卡：先选地图大小（3×3 ~ 9×9），再进入编辑。</summary>
    void ChooseSizeThenEdit()
    {
        WorkshopUI.ShowSizeDialog(m_Canvas, MinBoardSize, MaxBoardSize, size => EnterEdit(null, size), null);
    }

    void EnterEdit(LevelData data, int newSize = DefaultBoardSize)
    {
        if (data == null)
        {
            // 新关卡：空棋盘 + 琦琦站在左下角附近
            m_World = new SokobanGame(newSize, new GridPos(Mathf.Min(1, newSize - 1), Mathf.Min(1, newSize - 1)));
            m_LevelId = null;
            m_LevelName = DefaultLevelName;
        }
        else
        {
            m_World = new SokobanGame(data);
            m_LevelId = data.id;
            m_LevelName = string.IsNullOrEmpty(data.name) ? DefaultLevelName : data.name;
        }
        m_Dirty = false;
        m_TestPaused = false;
        m_Mode = Mode.Edit;
        ClearUndo();
        ApplyMode();
    }

    void EnterPlay(LevelData data)
    {
        m_PlayLevel = data;
        m_LevelName = string.IsNullOrEmpty(data.name) ? DefaultLevelName : data.name;
        m_World = new SokobanGame(data);
        m_Mode = Mode.Play;
        ClearUndo();
        ApplyMode();
    }

    void StartTest()
    {
        if (!ValidateLevel())
            return;

        m_TestSnapshot = m_World.Clone();   // 记下“测试开始前”的布局，重新测试 / 测试成功后都要回到它
        m_World = m_World.Clone();
        m_TestPaused = false;
        m_Mode = Mode.Test;
        ClearUndo();
        ApplyMode();
    }

    /// <summary>根据当前模式刷新所有 UI：显示哪些面板、按钮叫什么、能不能点棋盘。</summary>
    void ApplyMode()
    {
        bool inBoard = m_Mode != Mode.List;
        MusicPlayer.SetTrack(MusicPlayer.Track.Workshop);      // 创意工坊的所有页面共用一首，页面之间切换不会重新播放
        m_ListRoot.SetActive(!inBoard);
        m_BoardRoot.SetActive(inBoard);
        m_PanelRoot.SetActive(inBoard);
        if (!inBoard)
        {
            m_Skins.gameObject.SetActive(false);
            m_Notebook.gameObject.SetActive(false);
        }
        ResetHold();

        if (!inBoard)
        {
            m_Board.InputEnabled = false;
            return;
        }

        m_Board.SetGame(m_World);

        bool editable = IsEditable;
        m_ToolsGroup.SetActive(editable);
        m_Skins.gameObject.SetActive(editable);
        m_Notebook.gameObject.SetActive(editable);
        RefreshNameLabel();

        switch (m_Mode)
        {
            case Mode.Edit:
                SetActions(
                    new[] { "测试关卡", "计算难度", "保存关卡", "返回列表" },
                    new Action[] { StartTest, StartAnalysis, ShowSaveDialog, TryLeaveEditor });
                break;

            case Mode.Test:
                SetActions(
                    new[] { m_TestPaused ? "继续" : "暂停", "重新测试", "继续摆关" },
                    new Action[] { ToggleTestPause, RestartTest, BackToEditFromTest });
                break;

            case Mode.Play:
                SetActions(
                    new[] { "退出到列表", "重新开始" },
                    new Action[] { ShowList, RestartPlay });
                break;
        }

        UpdateToolStyles();
        UpdateStatus();
        m_Board.InputEnabled = editable && !m_Modal;
    }

    void SetActions(string[] labels, Action[] callbacks)
    {
        int n = labels.Length;
        for (int i = 0; i < m_ActionButtons.Length; i++)
        {
            var b = m_ActionButtons[i];
            if (i >= n)
            {
                b.gameObject.SetActive(false);
                continue;
            }
            b.gameObject.SetActive(true);
            b.SetLabel(labels[i]);
            b.SetOnClick(callbacks[i]);
        }
        LayoutActionButtons();
    }

    void LayoutActionButtons()
    {
        if (m_ActionButtons == null)
            return;
        int n = 0;
        for (int i = 0; i < m_ActionButtons.Length; i++)
            if (m_ActionButtons[i] != null && m_ActionButtons[i].gameObject.activeSelf)
                n++;
        if (n == 0)
            return;
        float width = ActionW;
        float height = ActionH;
        float gap = ActionGap;
        float total = n * width + (n - 1) * gap;
        int shown = 0;
        for (int i = 0; i < m_ActionButtons.Length; i++)
        {
            var b = m_ActionButtons[i];
            if (b == null || !b.gameObject.activeSelf)
                continue;
            b.SetPosSize(new Vector2(ActionX - total / 2f + width / 2f + shown * (width + gap), ActionY),
                new Vector2(width, height));
            b.SetFontSize(ActionFont);
            shown++;
        }
    }

    void SetModal(bool modal)
    {
        m_Modal = modal;
        ResetHold();
        m_Board.InputEnabled = IsEditable && !m_Modal && m_Mode != Mode.List;
    }

    // ====================================================================
    //  编辑
    // ====================================================================
    void SelectTool(Tool tool)
    {
        m_Tool = tool;
        UpdateToolStyles();
    }

    /// <summary>工具按钮上的小图标，跟着当前选的蛋 / 水晶外观变。</summary>
    void RefreshToolIcons()
    {
        if (m_ToolButtons == null || m_Skins == null)
            return;
        m_ToolButtons[(int)Tool.Box].SetIcon(SkinLibrary.Egg(m_Skins.SelectedEgg));
        m_ToolButtons[(int)Tool.Wall].SetIcon(SkinLibrary.Crystal(m_Skins.SelectedCrystal));
    }

    void UpdateToolStyles()
    {
        if (m_ToolButtons == null)
            return;
        for (int i = 0; i < m_ToolButtons.Length; i++)
            m_ToolButtons[i].SetStyle((int)m_Tool == i ? PlaqueButton.Style.Selected : PlaqueButton.Style.Normal);
    }

    void OnCellPressed(GridPos cell, bool justPressed)
    {
        if (m_Modal || m_World == null || !IsEditable)
            return;

        if (justPressed)
            m_StrokeBefore = m_World.Clone();

        bool changed = false;
        switch (m_Tool)
        {
            case Tool.Box:
                changed = m_World.PlaceBox(cell, m_Skins.SelectedEgg);
                if (!changed && justPressed && m_World.IsWall(cell))
                    ShowToast("这里是水晶墙，先用「擦除」清掉它");
                break;
            case Tool.Wall:
                changed = m_World.PlaceWall(cell, m_Skins.SelectedCrystal);
                if (!changed && justPressed && (cell == m_World.Snake || m_World.BoxIndexAt(cell) >= 0 || m_World.HasGoalAt(cell)))
                    ShowToast("这一格已经有琦琦、蛋或展示盒了，不能放水晶");
                break;
            case Tool.Snake:
                changed = m_World.PlaceSnake(cell);
                if (!changed && justPressed && m_World.IsWall(cell))
                    ShowToast("这里是水晶墙，琦琦站不上去");
                break;
            case Tool.Goal:
                changed = m_World.PlaceGoal(cell);
                if (!changed && justPressed && m_World.IsWall(cell))
                    ShowToast("这里是水晶墙，不能放展示盒");
                break;
            case Tool.Erase:
                changed = m_World.Erase(cell);
                if (!changed && justPressed && cell == m_World.Snake)
                    ShowToast("琦琦不能被擦掉，想换位置就用「放置琦琦」");
                break;
        }

        if (changed)
        {
            if (m_StrokeBefore != null)
            {
                PushUndo(m_StrokeBefore);
                m_StrokeBefore = null;
            }
            if (m_Mode == Mode.Edit)
                m_Dirty = true;
            m_Board.Refresh(true);
            UpdateStatus();
        }
    }

    // ====================================================================
    //  设计手记 / 关卡分析
    // ====================================================================
    void OpenNotebook()
    {
        SetModal(true);
        var from = m_Notebook != null ? (Vector2)m_Notebook.anchoredPosition : NotebookCenter;
        DesignNotebook.Open(m_Canvas, from, () => SetModal(false));
    }

    class AnalysisJob
    {
        public volatile bool cancelled;
        public volatile bool done;
        public LevelAnalysis result;
        public string error;
    }

    /// <summary>点“计算难度”：把当前布局交给后台线程去算，算完在弹窗里显示结果。</summary>
    void StartAnalysis()
    {
        if (!ValidateLevel())
            return;

        m_World.ToArrays(out var wall, out var goal, out var boxes, out var snake);
        int size = m_World.Size;
        var job = new AnalysisJob();

        string subtitle = Truncate(m_LevelName) + "  ·  " + size + "×" + size + "  ·  " + boxes.Length + " 个蛋  ·  " + m_World.Goals.Count + " 个展示盒";
        SetModal(true);
        var dialog = AnalysisDialog.Show(m_Canvas, subtitle, () =>
        {
            job.cancelled = true;
            SetModal(false);
        });

        var thread = new System.Threading.Thread(() =>
        {
            try
            {
                job.result = LevelAnalyzer.Analyze(size, wall, goal, boxes, snake, () => job.cancelled);
            }
            catch (Exception e)
            {
                job.error = e.Message;
            }
            job.done = true;
        });
        thread.IsBackground = true;
        thread.Start();
        StartCoroutine(WaitAnalysis(job, dialog));
    }

    IEnumerator WaitAnalysis(AnalysisJob job, AnalysisDialog dialog)
    {
        float waited = 0f;
        while (!job.done || waited < 0.45f)      // 至少停一小会儿，让玩家看到“正在计算”
        {
            if (job.cancelled)
                yield break;
            waited += Time.unscaledDeltaTime;
            yield return null;
        }
        if (job.cancelled || dialog == null || !dialog.IsOpen)
            yield break;

        if (job.error != null)
            dialog.ShowError(job.error);
        else
            dialog.ShowResult(job.result);
    }

    bool ValidateLevel()
    {
        if (m_World.Boxes.Count == 0 || m_World.Goals.Count == 0)
        {
            ShowToast("先放至少一个蛋和一个展示盒吧");
            return false;
        }
        return true;
    }

    void ShowSaveDialog()
    {
        if (!ValidateLevel())
            return;

        SetModal(true);
        WorkshopUI.ShowInputDialog(m_Canvas, "保存关卡", "给这个关卡起个名字吧", m_LevelName, "保存",
            name =>
            {
                SetModal(false);
                SaveLevel(name);
            },
            () => SetModal(false));
    }

    void SaveLevel(string rawName)
    {
        string name = (rawName ?? "").Trim();
        if (name.Length == 0)
            name = DefaultLevelName;

        if (string.IsNullOrEmpty(m_LevelId))
            m_LevelId = LevelStorage.NewId();
        m_LevelName = name;

        var data = m_World.ToLevelData(m_LevelId, m_LevelName);
        if (LevelStorage.Save(data))
        {
            m_Dirty = false;
            RefreshNameLabel();
            ShowToast("已保存：" + Truncate(m_LevelName));
        }
        else
        {
            ShowToast("保存失败了，请看控制台的报错");
        }
    }

    void TryLeaveEditor()
    {
        if (!m_Dirty)
        {
            ShowList();
            return;
        }

        SetModal(true);
        WorkshopUI.ShowDialog(m_Canvas, "还没有保存", "现在离开的话，这次的修改会丢失哦。",
            new DialogButton("离开", () =>
            {
                SetModal(false);
                ShowList();
            }),
            new DialogButton("继续编辑", () => SetModal(false)));
    }

    // ====================================================================
    //  测试
    // ====================================================================
    void ToggleTestPause()
    {
        m_TestPaused = !m_TestPaused;
        ApplyMode();                 // 暂停：显示工具、可点棋盘；继续：隐藏工具、可以走
        if (!m_TestPaused && m_World.IsSolved)
            OnSolved();              // 暂停时摆成了“已通关”，继续时直接算通过
    }

    void RestartTest()
    {
        m_World = m_TestSnapshot.Clone();
        m_TestPaused = false;
        ClearUndo();
        ApplyMode();
    }

    void BackToEditFromTest()
    {
        // 带着当前世界（走过的位置 / 暂停时改的布局）回到编辑
        m_Dirty = true;
        m_TestPaused = false;
        m_Mode = Mode.Edit;
        ClearUndo();
        ApplyMode();
    }

    // ====================================================================
    //  游玩
    // ====================================================================
    void RestartPlay()
    {
        m_World = new SokobanGame(m_PlayLevel);
        ClearUndo();
        ApplyMode();
    }

    // ====================================================================
    //  每帧：键盘移动
    // ====================================================================
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E) && CanUndo)
        {
            TryUndo();
            return;
        }

        if (m_Modal)
            return;

        if (m_Mode == Mode.Play || (m_Mode == Mode.Test && !m_TestPaused))
            HandleMoveInput();
    }

    void ResetHold()
    {
        m_Hold.Reset();
        if (m_Board != null)
            m_Board.SlideSpeed = 1f;
    }

    void HandleMoveInput()
    {
        int dir = m_Hold.Poll(true);
        if (dir >= 0)
            DoMove(dir);
        else if (m_Hold.Dir < 0 && m_Board != null)
            m_Board.SlideSpeed = 1f;
    }

    void DoMove(int dirIndex)
    {
        var dir = GridMoveInput.DirVectors[dirIndex];
        m_Board.SlideSpeed = m_Hold.Repeating ? GridMoveInput.RepeatSlideSpeed : 1f;
        m_Board.FaceDirection(dir);          // 就算这一步被墙/两个箱子挡住，琦琦也会先转向
        var snap = m_World.Clone();
        if (!m_World.TryMove(dir, out _))
            return;

        PushUndo(snap);
        m_Board.Refresh(false);
        UpdateStatus();

        if (m_World.IsSolved)
            OnSolved();
        else if (m_World.HasStuckBox())
            OnFailed();
    }

    // ====================================================================
    //  失败（有蛋被卡死）
    // ====================================================================
    void OnFailed()
    {
        ResetHold();
        m_FailOpen = true;
        SetModal(true);
        UiSfx.PlayFailure();                                  // 音效照旧立刻播

        float wait = m_Board.PlayFailure();                   // 先让卡死的蛋弹跳 + 魔法光效，再弹失败窗口
        StartCoroutine(ShowFailedDialogAfter(wait, m_World, m_Mode));
    }

    IEnumerator ShowFailedDialogAfter(float wait, SokobanGame world, Mode mode)
    {
        yield return new WaitForSecondsRealtime(wait);
        if (m_World != world || m_Mode != mode || m_TestPaused)
        {
            SetModal(false);    // 等的时候玩家已经点了别的按钮：不弹窗了，把输入还给棋盘
            yield break;
        }

        if (m_Mode == Mode.Test)
        {
            WorkshopUI.ShowDialog(m_Canvas, "测试失败了", "有个蛋被卡住了，再也推不进展示盒。\n玩家也会卡在这里，改改布局吧。",
                FailUndoButtons(
                    new DialogButton("继续摆关", () =>
                    {
                        SetModal(false);
                        m_FailOpen = false;
                        BackToEditFromTest();
                    }),
                    new DialogButton("重新测试", () =>
                    {
                        SetModal(false);
                        m_FailOpen = false;
                        RestartTest();
                    })));
        }
        else
        {
            WorkshopUI.ShowDialog(m_Canvas, "失败了", "有个蛋被卡住了，再也推不进展示盒啦。",
                FailUndoButtons(
                    new DialogButton("返回列表", () =>
                    {
                        SetModal(false);
                        m_FailOpen = false;
                        ShowList();
                    }),
                    new DialogButton("重新开始", () =>
                    {
                        SetModal(false);
                        m_FailOpen = false;
                        RestartPlay();
                    })));
        }
    }

    // ====================================================================
    //  通关
    // ====================================================================
    void OnSolved()
    {
        ResetHold();
        SetModal(true);
        UiSfx.PlayWin();                                      // 音效照旧立刻播

        if (m_Mode != Mode.Test && m_Mode != Mode.Play)
        {
            SetModal(false);
            return;
        }

        float wait = m_Board.PlayCompletion();                // 先把最后一个展示盒盖好，再弹结算
        StartCoroutine(ShowSolvedDialogAfter(wait, m_World, m_Mode));
    }

    IEnumerator ShowSolvedDialogAfter(float wait, SokobanGame world, Mode mode)
    {
        yield return new WaitForSecondsRealtime(wait);
        if (m_World != world || m_Mode != mode || !world.IsSolved)
        {
            SetModal(false);    // 等的时候玩家已经点了别的按钮：不弹结算了，把输入还给棋盘
            yield break;
        }

        if (m_Mode == Mode.Test)
        {
            // 测试成功：只有一句话和一个“确认”，确认后回到编辑，并恢复成测试前的布局
            WorkshopUI.ShowDialog(m_Canvas, "你的设计很顺利！", null,
                new DialogButton("确认", () =>
                {
                    SetModal(false);
                    m_World = m_TestSnapshot.Clone();
                    m_TestPaused = false;
                    m_Mode = Mode.Edit;
                    ClearUndo();
                    ApplyMode();
                }));
        }
        else if (m_Mode == Mode.Play)
        {
            WorkshopUI.ShowDialog(m_Canvas, "恭喜过关！", "蛋都放进展示盒啦",
                new DialogButton("重新开始", () =>
                {
                    SetModal(false);
                    RestartPlay();
                }),
                new DialogButton("返回列表", () =>
                {
                    SetModal(false);
                    ShowList();
                }));
        }
        else
        {
            SetModal(false);
        }
    }

    void ClearUndo()
    {
        m_Undo.Clear();
        m_StrokeBefore = null;
        m_FailOpen = false;
    }

    void PushUndo(SokobanGame snap)
    {
        if (snap == null)
            return;
        m_Undo.Add(snap);
        if (m_Undo.Count > MaxUndo)
            m_Undo.RemoveAt(0);
    }

    void TryUndo()
    {
        if (!CanUndo || m_Undo.Count == 0 || m_World == null)
            return;
        if (m_Modal && !m_FailOpen)
            return;

        CloseWorkshopDialogs();
        m_FailOpen = false;
        SetModal(false);

        m_World = m_Undo[m_Undo.Count - 1];
        m_Undo.RemoveAt(m_Undo.Count - 1);
        m_StrokeBefore = null;
        ResetHold();
        if (m_Mode == Mode.Edit)
            m_Dirty = true;
        m_Board.SetGame(m_World);
        UpdateStatus();
    }

    DialogButton[] FailUndoButtons(params DialogButton[] rest)
    {
        if (m_Undo.Count == 0)
            return rest;
        var list = new List<DialogButton>(rest.Length + 1);
        list.Add(new DialogButton("撤回一步", () =>
        {
            SetModal(false);
            m_FailOpen = false;
            TryUndo();
        }));
        list.AddRange(rest);
        return list.ToArray();
    }

    void CloseWorkshopDialogs()
    {
        if (m_Canvas == null)
            return;
        for (int i = m_Canvas.childCount - 1; i >= 0; i--)
        {
            var child = m_Canvas.GetChild(i);
            if (child != null && child.name == "Dialog")
                Destroy(child.gameObject);
        }
    }

    // ====================================================================
    //  状态文字
    // ====================================================================
    void UpdateStatus()
    {
        if (m_World == null || m_StatusLabel == null)
            return;

        int boxes = m_World.Boxes.Count;
        int goals = m_World.Goals.Count;
        m_StatusLabel.color = Color.white;
        if (IsEditable)
        {
            const string red = "<color=#FF5A5A>";
            string egg = "蛋 " + boxes;
            string goal = "展示盒 " + goals;
            if (boxes < goals)
                egg = red + egg + "</color>";
            else if (goals < boxes)
                goal = red + goal + "</color>";
            m_StatusLabel.text = egg + "  |  " + goal;
        }
        else
        {
            m_StatusLabel.text = "已就位  " + m_World.BoxesOnGoalCount + " / " + boxes;
        }
    }
}
