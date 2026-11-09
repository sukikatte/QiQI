using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 开始游戏：选关页 → 游玩。
///   选关页：顶部中间是标题图 game_menu，左上角是返回图标，下面是可向下滑动的关卡卡片
///           （每排 4 张，大小和原来一样；最后留一张「敬请期待」）。
///   游玩页：左边棋盘，右边羊皮纸侧栏（步数、重新开始、返回选关），左上角同样有返回图标。
/// UI 全部在运行时生成。关卡数据来自 CampaignLevels，规则和创意工坊共用 SokobanGame。
/// </summary>
public class CampaignController : MonoBehaviour
{
    // ---------------- 选关页布局 ----------------
    const int Columns = 4;
    static readonly Vector2 CardSize = new Vector2(320f, 350f);
    const float CardGapX = 40f;
    const float CardGapY = 30f;
    static readonly Vector2 TitlePos = new Vector2(0f, 410f);
    static readonly Vector2 TitleSize = new Vector2(560f, 240f);
    static readonly Vector2 BackIconPos = new Vector2(-850f, 440f);
    const float BackIconSize = 118f;
    const float PreviewSide = 200f;
    static readonly Vector2 ScrollPos = new Vector2(0f, -90f);
    static readonly Vector2 ScrollSize = new Vector2(1480f, 720f);   // 大约能看见两排，再往下拖

    // ---------------- 游玩页布局 ----------------
    // 棋盘直接铺在背景正中的空地上（最近一行宽 960，带透视，总高约 550），侧栏缩小放在右边
    const float BoardPixels = 960f;
    static readonly Vector2 BoardCenter = new Vector2(0f, -240f);
    static readonly Vector2 PanelCenter = new Vector2(0f, 195f);          // 说明栏：棋盘正上方的横条
    static readonly Vector2 PanelSize = new Vector2(1380f, 250f);
    const int HoldUnlockLevel = 5;           // 从第 6 关起（下标 5）才允许长按连续移动
    const int UndoUnlockLevel = 8;           // 从第 9 关起（下标 8）才能按 E 撤回

    static readonly Color Brown = new Color(0.62f, 0.36f, 0.12f);

    enum Screen { Select, Play }

    Transform m_Ui;
    GameObject m_SelectBg, m_PlayBg;
    GameObject m_SelectRoot, m_PlayRoot;
    RectTransform m_CardsRoot;
    ScrollRect m_CardScroll;

    WorkshopBoardView m_Board;
    RectTransform m_BoardHolder;
    GameObject m_InfoBar, m_PlayBack;
    TextMeshProUGUI m_LevelLabel, m_NameLabel, m_BestLabel, m_StatusLabel;

    Screen m_Screen = Screen.Select;
    int m_Index;
    SokobanGame m_World;
    int m_Steps;
    bool m_Modal;

    readonly MoveHold m_Hold = new MoveHold();

    struct UndoSnap
    {
        public GridPos snake;
        public GridPos[] boxes;
        public int steps;
    }
    readonly List<UndoSnap> m_Undo = new List<UndoSnap>();
    bool UndoEnabled => m_Index >= UndoUnlockLevel;

    // ====================================================================
    //  初始化
    // ====================================================================
    public void Init(Transform bgStage, Transform uiStage, TMP_FontAsset font)
    {
        m_Ui = uiStage;
        WorkshopUI.Font = font;

        // 选关页的背景：收藏室全景，再压一层暗色，让卡片和标题更突出
        m_SelectBg = NewBackground(bgStage, "SelectBackground", "Art/Bg/qiqi_cover", 0.5f);
        // 游玩页的背景：空旷的地板，棋盘放上去最清楚
        m_PlayBg = NewBackground(bgStage, "PlayBackground", "Art/Bg/workshopBg", 0f);

        BuildSelectScreen();
        BuildPlayScreen();
        ShowSelectNow();
    }

    static GameObject NewBackground(Transform parent, string name, string resource, float dim)
    {
        var root = WorkshopUI.NewStretch(parent, name);
        var sprite = Resources.Load<Sprite>(resource);
        if (sprite == null)
            Debug.LogError("图片加载失败: " + resource + "（Texture Type 需为 Sprite (2D and UI)）");

        var img = root.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = false;
        img.raycastTarget = false;

        if (dim > 0f)
        {
            var overlay = WorkshopUI.NewStretch(root, "Dim");
            var d = overlay.gameObject.AddComponent<Image>();
            d.color = new Color(0.07f, 0.04f, 0.02f, dim);
            d.raycastTarget = false;
        }
        root.SetAsFirstSibling();
        return root.gameObject;
    }

    // ====================================================================
    //  选关页
    // ====================================================================
    void BuildSelectScreen()
    {
        var root = WorkshopUI.NewStretch(m_Ui, "SelectScreen");
        m_SelectRoot = root.gameObject;

        var titleSprite = Resources.Load<Sprite>("Art/UI/game_menu");
        if (titleSprite != null)
            WorkshopUI.NewImage(root, "Title", titleSprite, TitlePos, TitleSize);
        else
            WorkshopUI.NewLabel(root, "Title", "关卡选择", 72, WorkshopUI.Cream, TitlePos, TitleSize);

        var scroll = WorkshopUI.NewRect(root, "CardScroll", ScrollPos, ScrollSize);
        var sr = scroll.gameObject.AddComponent<ScrollRect>();
        m_CardScroll = sr;

        var viewport = WorkshopUI.NewStretch(scroll, "Viewport");
        var vpImage = viewport.gameObject.AddComponent<Image>();
        vpImage.color = new Color(0f, 0f, 0f, 0f);     // 透明，但能拖、能滚轮
        vpImage.raycastTarget = true;
        viewport.gameObject.AddComponent<RectMask2D>();

        m_CardsRoot = WorkshopUI.NewRect(viewport, "Cards", Vector2.zero, Vector2.zero);
        m_CardsRoot.anchorMin = new Vector2(0f, 1f);
        m_CardsRoot.anchorMax = new Vector2(1f, 1f);
        m_CardsRoot.pivot = new Vector2(0.5f, 1f);
        m_CardsRoot.anchoredPosition = Vector2.zero;
        m_CardsRoot.sizeDelta = new Vector2(0f, 0f);

        // 用网格从顶部往下排：子物体的坐标是相对父物体中心的，手动算 y 会整排掉到视口下面
        var grid = m_CardsRoot.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = CardSize;
        grid.spacing = new Vector2(CardGapX, CardGapY);
        grid.padding = new RectOffset(0, 0, 8, 36);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Columns;
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        var fitter = m_CardsRoot.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        sr.viewport = viewport;
        sr.content = m_CardsRoot;
        sr.horizontal = false;
        sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.inertia = true;
        sr.scrollSensitivity = 48f;

        NewBackIcon(root, () => ScreenTransition.LoadScene("MainMenu"));
        // 返回图标和标题盖在滚动列表上面，免得被卡片挡住
        root.Find("Title")?.SetAsLastSibling();
        root.Find("BackIcon")?.SetAsLastSibling();
    }

    /// <summary>左上角的返回图标按钮（圆形金边木牌 + 箭头）。</summary>
    static GameObject NewBackIcon(Transform parent, System.Action onClick)
    {
        var sprite = Resources.Load<Sprite>("Art/UI/back_icon");
        var rect = WorkshopUI.NewRect(parent, "BackIcon", BackIconPos, new Vector2(BackIconSize, BackIconSize));
        var img = rect.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = true;
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = img;
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => onClick());
        rect.gameObject.AddComponent<UIButtonFeedback>();

        if (sprite == null)
        {
            // 没有图标图片时退而求其次，至少还有个字
            img.color = new Color(0.4f, 0.25f, 0.1f, 1f);
            WorkshopUI.NewLabel(rect, "Text", "返回", 34, WorkshopUI.Cream, Vector2.zero, new Vector2(BackIconSize, 60f));
        }
        return rect.gameObject;
    }

    void RefreshCards()
    {
        StopAllCoroutines();
        for (int i = m_CardsRoot.childCount - 1; i >= 0; i--)
            DestroyImmediate(m_CardsRoot.GetChild(i).gameObject);

        int total = CampaignLevels.Count + 1;     // 最后一张永远是「敬请期待」
        for (int i = 0; i < total; i++)
        {
            var card = NewCard(i, out var group);
            StartCoroutine(CardIntro(card, group, i * 0.05f));
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(m_CardsRoot);
        SnapScrollToTop();
        StartCoroutine(SnapScrollToTopNextFrame());
    }

    void SnapScrollToTop()
    {
        if (m_CardScroll == null || m_CardsRoot == null)
            return;
        m_CardScroll.StopMovement();
        Canvas.ForceUpdateCanvases();
        // 内容锚在视口上沿：y=0 就是第一排。不要用 normalizedPosition，内容高度还没算稳时会把列表弹到空白处。
        m_CardsRoot.anchoredPosition = Vector2.zero;
    }

    IEnumerator SnapScrollToTopNextFrame()
    {
        yield return null;
        SnapScrollToTop();
        yield return new WaitForEndOfFrame();
        SnapScrollToTop();
    }

    RectTransform NewCard(int index, out CanvasGroup group)
    {
        bool real = index < CampaignLevels.Count;
        var card = WorkshopUI.NewImage(m_CardsRoot, "Card_" + index, WorkshopUI.LoadSprite("UI/panel_parchment"), Vector2.zero,
            CardSize, true, true);
        group = card.gameObject.AddComponent<CanvasGroup>();
        var t = card.transform;

        if (real && CampaignProgress.IsUnlocked(index))
        {
            int level = index;
            var data = CampaignLevels.Build(level);
            bool cleared = CampaignProgress.IsCleared(level);

            WorkshopUI.NewLabel(t, "Number", "第 " + (level + 1) + " 关", 34, WorkshopUI.Ink,
                new Vector2(0f, 132f), new Vector2(260f, 44f));
            BuildPreview(t, level, data, new Vector2(0f, 22f), PreviewSide);
            WorkshopUI.NewLabel(t, "Name", CampaignLevels.Name(level), 30, WorkshopUI.Ink,
                new Vector2(0f, -108f), new Vector2(280f, 40f));
            WorkshopUI.NewLabel(t, "Status", cleared ? "最少 " + CampaignProgress.BestSteps(level) + " 步" : "尚未通关", 22,
                cleared ? Brown : WorkshopUI.InkSoft, new Vector2(0f, -142f), new Vector2(280f, 30f));

            if (cleared)
            {
                // 右上角斜着贴一枚金色“已通关”印章
                var badge = WorkshopUI.NewImage(t, "Badge", WorkshopUI.LoadSprite("UI/btn_gold"), new Vector2(104f, 150f),
                    new Vector2(104f, 42f), true);
                badge.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -8f);
                WorkshopUI.NewLabel(badge.transform, "Text", "已通关", 22, WorkshopUI.Ink, Vector2.zero, new Vector2(100f, 36f));
            }

            var button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = card;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => StartLevel(level));
            card.gameObject.AddComponent<UIButtonFeedback>();
        }
        else
        {
            // 没解锁的关、以及还没做的关：灰一点，中间一把锁
            card.color = new Color(0.66f, 0.6f, 0.56f, 1f);
            string title = real ? "第 " + (index + 1) + " 关" : "敬请期待";
            string subtitle = real ? "通关上一关后解锁" : "更多关卡制作中";
            WorkshopUI.NewLabel(t, "Number", title, 34, WorkshopUI.InkSoft, new Vector2(0f, 132f), new Vector2(260f, 44f));

            var well = WorkshopUI.NewImage(t, "Well", null, new Vector2(0f, 22f), new Vector2(PreviewSide, PreviewSide));
            well.color = new Color(0.25f, 0.15f, 0.07f, 0.35f);
            WorkshopUI.NewImage(t, "Lock", Resources.Load<Sprite>("Art/UI/lock_icon"), new Vector2(0f, 22f), new Vector2(110f, 110f));

            WorkshopUI.NewLabel(t, "Name", subtitle, 28, WorkshopUI.InkSoft, new Vector2(0f, -108f), new Vector2(280f, 40f));
        }
        return card.rectTransform;
    }

    static Sprite s_QiqiPhoto;
    static readonly Rect QiqiPhotoRaw = new Rect(500f, 345f, 510f, 490f);

    static Sprite QiqiPhoto()
    {
        if (s_QiqiPhoto == null)
        {
            s_QiqiPhoto = WorkshopUI.CropSprite(Resources.Load<Sprite>("Art/Qiqi/qiqi_snake"), QiqiPhotoRaw);
            if (s_QiqiPhoto == null)
                s_QiqiPhoto = Resources.Load<Sprite>("Art/Qiqi/turn_1");
        }
        return s_QiqiPhoto;
    }

    /// <summary>卡片中间不再画整张地图，改成琦琦照片 / 这一关的蛋 / 这一关的水晶。</summary>
    static void BuildPreview(Transform parent, int level, LevelData d, Vector2 pos, float side)
    {
        var root = WorkshopUI.NewRect(parent, "Preview", pos, new Vector2(side, side));
        var well = WorkshopUI.NewImage(root, "Well", null, Vector2.zero, new Vector2(side, side));
        well.color = new Color(0.30f, 0.18f, 0.08f, 0.55f);

        int mode = level % 3;
        if (mode == 2 && (d.walls == null || d.walls.Count == 0))
            mode = 1;
        if (mode == 1 && (d.boxes == null || d.boxes.Count == 0))
            mode = 0;

        if (mode == 0)
            PlaceShowcaseSprite(root, "Qiqi", QiqiPhoto(), Vector2.zero, side * 0.88f, 0f);
        else if (mode == 1)
            PlaceShowcaseCluster(root, UniqueSprites(d.boxKinds, SkinLibrary.Egg), side, 0.74f);
        else
            PlaceShowcaseCluster(root, UniqueSprites(d.wallKinds, SkinLibrary.Crystal), side, 0.80f);
    }

    static List<Sprite> UniqueSprites(List<int> kinds, System.Func<int, Sprite> spriteOf)
    {
        var result = new List<Sprite>();
        if (kinds == null)
            return result;
        for (int i = 0; i < kinds.Count; i++)
        {
            var s = spriteOf(kinds[i]);
            if (s != null && !result.Contains(s))
                result.Add(s);
            if (result.Count >= 3)
                break;
        }
        return result;
    }

    static void PlaceShowcaseCluster(Transform root, List<Sprite> sprites, float side, float itemScale)
    {
        if (sprites == null || sprites.Count == 0)
        {
            PlaceShowcaseSprite(root, "Qiqi", QiqiPhoto(), Vector2.zero, side * 0.88f, 0f);
            return;
        }

        int n = Mathf.Min(3, sprites.Count);
        float size = side * itemScale * (n == 1 ? 1f : 0.78f);
        Vector2[] poses =
        {
            Vector2.zero,
            new Vector2(-side * 0.18f, -6f), new Vector2(side * 0.18f, 8f),
            new Vector2(0f, 22f), new Vector2(-side * 0.22f, -18f), new Vector2(side * 0.22f, -14f),
        };
        int start = n == 1 ? 0 : n == 2 ? 1 : 3;
        for (int i = 0; i < n; i++)
            PlaceShowcaseSprite(root, "Item_" + i, sprites[i], poses[start + i], size, (i - (n - 1) * 0.5f) * 10f);
    }

    static void PlaceShowcaseSprite(Transform parent, string name, Sprite sprite, Vector2 pos, float size, float tilt)
    {
        if (sprite == null)
            return;
        var img = WorkshopUI.NewImage(parent, name, sprite, pos, new Vector2(size, size));
        img.preserveAspect = true;
        img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, tilt);
    }

    /// <summary>卡片入场：只淡入，不改坐标（坐标交给网格排，改了会把整排挤出视口）。</summary>
    static IEnumerator CardIntro(RectTransform card, CanvasGroup group, float delay)
    {
        group.alpha = 0f;
        for (float t = 0f; t < delay; t += Time.unscaledDeltaTime)
            yield return null;

        const float duration = 0.32f;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            if (card == null) yield break;
            group.alpha = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / duration), 3f);
            yield return null;
        }
        if (card == null) yield break;
        group.alpha = 1f;
    }

    // ====================================================================
    //  游玩页
    // ====================================================================
    void BuildPlayScreen()
    {
        var root = WorkshopUI.NewStretch(m_Ui, "PlayScreen");
        m_PlayRoot = root.gameObject;

        var boardHolder = WorkshopUI.NewRect(root, "BoardHolder", BoardCenter, Vector2.zero);
        m_BoardHolder = boardHolder;
        var boardRect = WorkshopUI.NewRect(boardHolder, "Board", Vector2.zero, new Vector2(BoardPixels, BoardPixels));
        m_Board = boardRect.gameObject.AddComponent<WorkshopBoardView>();
        m_Board.Build(8, BoardPixels);
        m_Board.InputEnabled = false;

        var panel = WorkshopUI.NewImage(root, "InfoBar", WorkshopUI.LoadSprite("UI/panel_parchment"), PanelCenter,
            PanelSize, true, true);
        m_InfoBar = panel.gameObject;
        var p = panel.transform;

        // 左：第几关 + 关名；中：步数 + 记录；右：两个按钮
        m_LevelLabel = WorkshopUI.NewLabel(p, "Level", "", 54, WorkshopUI.Ink, new Vector2(-480f, 38f), new Vector2(380f, 70f));
        m_NameLabel = WorkshopUI.NewLabel(p, "Name", "", 34, Brown, new Vector2(-480f, -30f), new Vector2(380f, 48f));
        m_StatusLabel = WorkshopUI.NewLabel(p, "Status", "", 36, WorkshopUI.Ink, new Vector2(30f, 20f), new Vector2(660f, 52f));
        m_BestLabel = WorkshopUI.NewLabel(p, "Best", "", 28, WorkshopUI.InkSoft, new Vector2(30f, -34f), new Vector2(660f, 40f));

        PlaqueButton.Create(p, "Restart", "重新开始", new Vector2(510f, 36f), new Vector2(250f, 64f), 30, RestartLevel);
        PlaqueButton.Create(p, "ToSelect", "返回选关", new Vector2(510f, -40f), new Vector2(250f, 64f), 30, GoSelect);

        m_PlayBack = NewBackIcon(root, GoSelect);
    }

    // ====================================================================
    //  切换界面
    // ====================================================================
    /// <summary>带转场：回到选关页。</summary>
    void GoSelect()
    {
        ScreenTransition.Run(ShowSelectNow);
    }

    void ShowSelectNow()
    {
        StopAllCoroutines();
        m_Screen = Screen.Select;
        m_Modal = false;
        ResetHold();
        MusicPlayer.SetTrack(MusicPlayer.Track.Menu);       // 选关页：和主菜单同一首
        m_SelectRoot.SetActive(true);
        m_SelectBg.SetActive(true);
        m_PlayRoot.SetActive(false);
        m_PlayBg.SetActive(false);
        RefreshCards();
    }

    /// <summary>带转场：进入某一关。</summary>
    void StartLevel(int index)
    {
        if (!CampaignProgress.IsUnlocked(index))
            return;
        ScreenTransition.Run(() => EnterLevel(index));
    }

    void EnterLevel(int index)
    {
        StopAllCoroutines();
        m_Index = index;
        m_Screen = Screen.Play;
        m_Modal = false;
        ResetHold();
        MusicPlayer.SetTrack(MusicPlayer.Track.Game);       // 进入关卡：换成游戏音乐

        m_SelectRoot.SetActive(false);
        m_SelectBg.SetActive(false);
        m_PlayRoot.SetActive(true);
        m_PlayBg.SetActive(true);

        m_LevelLabel.text = "第 " + (index + 1) + " 关";
        m_NameLabel.text = CampaignLevels.Name(index);
        RestartLevel();

        // 第一次玩第一关：棋盘先藏在屏幕上面，对话里再分两次掉下来
        if (index == 0 && !CampaignProgress.StorySeen(CampaignStory.IntroId))
            HideBoardForIntro();
        else
            ShowBoardNow();

        // 第一次玩：第一关有开场对话和教学，第二关有水晶的提醒
        if (index == 0 && !CampaignProgress.StorySeen(CampaignStory.TutorialId))
        {
            SetPlayHudVisible(false);
            StartCoroutine(Level1Story(m_World));
        }
        else if (index == 1 && !CampaignProgress.StorySeen(CampaignStory.Level2Id))
        {
            SetPlayHudVisible(true);
            StartCoroutine(Level2Story(m_World));
        }
        else if (index == HoldUnlockLevel && !CampaignProgress.StorySeen(CampaignStory.Level6Id))
        {
            SetPlayHudVisible(true);
            StartCoroutine(Level6Story(m_World));
        }
        else if (index == UndoUnlockLevel && !CampaignProgress.StorySeen(CampaignStory.Level9Id))
        {
            SetPlayHudVisible(true);
            StartCoroutine(Level9Story(m_World));
        }
        else
        {
            SetPlayHudVisible(true);
        }
    }

    // ====================================================================
    //  剧情
    // ====================================================================
    bool m_StoryDone;

    /// <summary>播一段对话，播完（或者跳过）才往下走。</summary>
    IEnumerator PlayStory(StoryDialogue.Line[] lines, float dim = 0.4f, bool allowSkip = true)
    {
        m_StoryDone = false;
        StoryDialogue.Play(m_Ui, lines, () => m_StoryDone = true, allowSkip, dim);
        while (!m_StoryDone)
            yield return null;
    }

    void SetPlayHudVisible(bool visible)
    {
        if (m_InfoBar != null)
            m_InfoBar.SetActive(visible);
        if (m_PlayBack != null)
            m_PlayBack.SetActive(visible);
    }

    IEnumerator Level1Story(SokobanGame world)
    {
        m_Modal = true;
        while (ScreenTransition.Busy)
            yield return null;                 // 等转场的爱心散完

        // 开场：先什么都不放。琦琦说到"糟糕"的时候只掉蛋；
        // 对话结束、"准备好了"之前，网格、收纳柜和琦琦再掉下来。
        if (!CampaignProgress.StorySeen(CampaignStory.IntroId))
        {
            HideBoardForIntro();
            yield return PlayStory(CampaignStory.Intro(DropEggs), 0.4f, false);
            CampaignProgress.MarkStorySeen(CampaignStory.IntroId);
            yield return DropRestRoutine();
            ShowBoardNow();                    // 跳过时也会把所有东西放到位
        }
        else
        {
            ShowBoardNow();
        }

        // 进入游戏页以后：怎么操作、目的、小心死路、一次只能推一颗
        yield return PlayStory(CampaignStory.Tutorial(), 0.28f, false);
        CampaignProgress.MarkStorySeen(CampaignStory.TutorialId);
        SetPlayHudVisible(true);
        m_Modal = false;
    }

    IEnumerator Level2Story(SokobanGame world)
    {
        m_Modal = true;
        while (ScreenTransition.Busy)
            yield return null;
        yield return PlayStory(CampaignStory.Level2(), 0.28f);
        CampaignProgress.MarkStorySeen(CampaignStory.Level2Id);
        m_Modal = false;
    }

    IEnumerator Level6Story(SokobanGame world)
    {
        m_Modal = true;
        while (ScreenTransition.Busy)
            yield return null;
        yield return PlayStory(CampaignStory.Level6(), 0.28f);
        CampaignProgress.MarkStorySeen(CampaignStory.Level6Id);
        m_Modal = false;
    }

    IEnumerator Level9Story(SokobanGame world)
    {
        m_Modal = true;
        while (ScreenTransition.Busy)
            yield return null;
        yield return PlayStory(CampaignStory.Level9(), 0.28f);
        CampaignProgress.MarkStorySeen(CampaignStory.Level9Id);
        m_Modal = false;
    }

    void ShowBoardNow()
    {
        m_BoardHolder.anchoredPosition = BoardCenter;
        m_Board.RestLift = 0f;
        m_Board.EggLifts = null;
    }

    /// <summary>开场用：网格、收纳柜、琦琦、蛋全都抬到屏幕上面，先看不见。</summary>
    void HideBoardForIntro()
    {
        m_BoardHolder.anchoredPosition = BoardCenter;
        m_Board.RestLift = WorkshopBoardView.HiddenLift;
        m_Board.EggLifts = HiddenEggLifts();
    }

    float[] HiddenEggLifts()
    {
        int n = Mathf.Max(1, m_World.Boxes.Count);
        var lifts = new float[n];
        for (int i = 0; i < n; i++)
            lifts[i] = WorkshopBoardView.HiddenLift;
        return lifts;
    }

    /// <summary>"哦糟糕！怎么全都掉下来了"：只把蛋从上面砸下来，网格还藏着。</summary>
    void DropEggs()
    {
        StartCoroutine(DropEggsRoutine());
    }

    IEnumerator DropEggsRoutine()
    {
        int n = Mathf.Max(1, m_World.Boxes.Count);
        var lifts = HiddenEggLifts();
        m_Board.EggLifts = lifts;

        // 两颗蛋错开一点点掉，看起来才像"到处乱滚"
        const float stagger = 0.11f;
        const float fall = 0.34f;
        float[] bounce = { 52f, 18f };
        float[] bounceTime = { 0.24f, 0.14f };

        float t = 0f;
        float total = stagger * (n - 1) + fall + bounceTime[0] + bounceTime[1];
        while (t < total)
        {
            t += Time.unscaledDeltaTime;
            for (int i = 0; i < n; i++)
            {
                float local = t - i * stagger;
                if (local < 0f)
                    continue;
                if (local < fall)
                {
                    float u = local / fall;
                    lifts[i] = WorkshopBoardView.HiddenLift * (1f - u * u);
                }
                else
                {
                    float b = local - fall;
                    float y = 0f;
                    float acc = 0f;
                    for (int k = 0; k < bounce.Length; k++)
                    {
                        if (b < acc + bounceTime[k])
                        {
                            float u = (b - acc) / bounceTime[k];
                            y = bounce[k] * 4f * u * (1f - u);
                            break;
                        }
                        acc += bounceTime[k];
                    }
                    lifts[i] = y;
                }
            }
            yield return null;
        }
        for (int i = 0; i < n; i++)
            lifts[i] = 0f;
    }

    /// <summary>"准备好了"之前：网格、收纳柜、琦琦从上面掉下来，蛋已经在格子上了。</summary>
    IEnumerator DropRestRoutine()
    {
        if (m_Board.EggLifts == null)
            m_Board.EggLifts = new float[Mathf.Max(1, m_World.Boxes.Count)];

        const float fall = 0.38f;
        for (float t = 0f; t < fall; t += Time.unscaledDeltaTime)
        {
            float u = t / fall;
            m_Board.RestLift = WorkshopBoardView.HiddenLift * (1f - u * u);
            yield return null;
        }

        float[] bounce = { 40f, 14f };
        float[] time = { 0.24f, 0.14f };
        for (int b = 0; b < bounce.Length; b++)
        {
            for (float t = 0f; t < time[b]; t += Time.unscaledDeltaTime)
            {
                float u = t / time[b];
                m_Board.RestLift = bounce[b] * 4f * u * (1f - u);
                yield return null;
            }
        }
        m_Board.RestLift = 0f;
    }

    void RestartLevel()
    {
        m_Modal = false;
        m_Undo.Clear();
        m_World = new SokobanGame(CampaignLevels.Build(m_Index));
        m_Steps = 0;
        m_Board.SetGame(m_World);
        UpdateStatus();
    }

    void UpdateStatus()
    {
        int best = CampaignProgress.BestSteps(m_Index);
        m_BestLabel.text = best > 0 ? "最少步数记录：" + best : "还没有通关记录";
        m_StatusLabel.text = "步数 " + m_Steps + "  |  已就位 " + m_World.BoxesOnGoalCount + " / " + m_World.Boxes.Count;
    }

    // ====================================================================
    //  每帧：键盘
    // ====================================================================
    void Update()
    {
        if (m_Modal)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (m_Screen == Screen.Play) GoSelect();
            else ScreenTransition.LoadScene("MainMenu");
            return;
        }

        if (m_Screen == Screen.Select && Input.GetKeyDown(KeyCode.F8))
        {
            // 开发用：清空通关记录和剧情，重新体验第一关的教学
            CampaignProgress.ResetAll();
            RefreshCards();
            m_Modal = true;
            WorkshopUI.ShowDialog(m_Ui, "进度已清空", "通关记录和剧情都重置了，从第一关开始会重新播放教学。",
                new DialogButton("好", () => m_Modal = false));
            return;
        }

        if (m_Screen == Screen.Play && Input.GetKeyDown(KeyCode.E))
        {
            UndoMove();
            return;
        }

        if (m_Screen == Screen.Play)
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
        int dir = m_Hold.Poll(m_Index >= HoldUnlockLevel);
        if (dir >= 0)
            DoMove(dir);
        else if (m_Hold.Dir < 0 && m_Board != null)
            m_Board.SlideSpeed = 1f;
    }

    void DoMove(int dirIndex)
    {
        var dir = GridMoveInput.DirVectors[dirIndex];
        m_Board.SlideSpeed = m_Hold.Repeating ? GridMoveInput.RepeatSlideSpeed : 1f;
        m_Board.FaceDirection(dir);          // 被墙挡住时琦琦也会先转向

        var snap = CaptureUndo();
        if (!m_World.TryMove(dir, out _))
            return;

        if (UndoEnabled)
            m_Undo.Add(snap);

        m_Steps++;
        m_Board.Refresh(false);
        UpdateStatus();

        if (m_World.IsSolved)
            OnSolved();
        else if (m_World.HasStuckBox())
            OnFailed();
    }

    UndoSnap CaptureUndo()
    {
        var boxes = new GridPos[m_World.Boxes.Count];
        m_World.Boxes.CopyTo(boxes);
        return new UndoSnap { snake = m_World.Snake, boxes = boxes, steps = m_Steps };
    }

    void UndoMove()
    {
        if (!UndoEnabled || m_Undo.Count == 0 || m_World == null)
            return;

        var snap = m_Undo[m_Undo.Count - 1];
        m_Undo.RemoveAt(m_Undo.Count - 1);
        m_World.Snake = snap.snake;
        int n = Mathf.Min(snap.boxes.Length, m_World.Boxes.Count);
        for (int i = 0; i < n; i++)
            m_World.Boxes[i] = snap.boxes[i];
        m_Steps = snap.steps;

        ResetHold();
        m_Board.StopFailure();
        m_Board.SlideSpeed = 1f;
        m_Board.Refresh(false);
        UpdateStatus();
    }

    // ====================================================================
    //  通关
    // ====================================================================
    void OnFailed()
    {
        ResetHold();
        m_Modal = true;
        UiSfx.PlayFailure();                                  // 音效照旧立刻播

        float wait = m_Board.PlayFailure();                   // 先让卡死的蛋弹跳 + 魔法光效，再弹失败窗口
        StartCoroutine(ShowFailedDialogAfter(wait, m_World));
    }

    IEnumerator ShowFailedDialogAfter(float wait, SokobanGame world)
    {
        yield return new WaitForSecondsRealtime(wait);
        if (m_World != world || m_Screen != Screen.Play)
            yield break;        // 等的时候玩家已经点了重新开始 / 返回选关

        if (UndoEnabled && m_Undo.Count > 0)
        {
            WorkshopUI.ShowDialog(m_Ui, "失败了", "有个蛋被卡住了，再也推不进展示盒啦。",
                new DialogButton("撤回一步", () =>
                {
                    m_Modal = false;
                    UndoMove();
                }),
                new DialogButton("重新开始", () =>
                {
                    m_Modal = false;
                    RestartLevel();
                }),
                new DialogButton("返回选关", GoSelect));
        }
        else
        {
            WorkshopUI.ShowDialog(m_Ui, "失败了", "有个蛋被卡住了，再也推不进展示盒啦。",
                new DialogButton("返回选关", GoSelect),
                new DialogButton("重新开始", () =>
                {
                    m_Modal = false;
                    RestartLevel();
                }));
        }
    }

    void OnSolved()
    {
        ResetHold();
        m_Modal = true;

        UiSfx.PlayWin();                                      // 音效照旧立刻播
        bool firstClear = !CampaignProgress.IsCleared(m_Index);
        bool newBest = CampaignProgress.Record(m_Index, m_Steps);
        UpdateStatus();

        float wait = m_Board.PlayCompletion();                // 先把最后一个展示盒盖好，再弹结算
        StartCoroutine(ShowSolvedDialogAfter(wait, m_World, newBest, firstClear));
    }

    IEnumerator ShowSolvedDialogAfter(float wait, SokobanGame world, bool newBest, bool firstClear)
    {
        yield return new WaitForSecondsRealtime(wait);
        if (m_World != world || m_Screen != Screen.Play || !world.IsSolved)
            yield break;        // 等的时候玩家已经点了重新开始 / 返回选关

        // 第一次通关第一关：先来一段对话，再出结算
        if (m_Index == 0 && firstClear && !CampaignProgress.StorySeen(CampaignStory.AfterLevel1Id))
        {
            yield return PlayStory(CampaignStory.AfterLevel1());
            CampaignProgress.MarkStorySeen(CampaignStory.AfterLevel1Id);
            if (m_World != world || m_Screen != Screen.Play)
                yield break;
        }

        // 通关第十关：琦琦把创意工坊作为礼物送给玩家
        if (m_Index == CampaignProgress.WorkshopUnlockLevel && !CampaignProgress.StorySeen(CampaignStory.WorkshopId))
        {
            yield return PlayStory(CampaignStory.AfterLevel10());
            CampaignProgress.MarkStorySeen(CampaignStory.WorkshopId);
            if (m_World != world || m_Screen != Screen.Play)
                yield break;
        }

        string message = "用了 " + m_Steps + " 步" + (newBest ? "（新纪录！）" : "");
        bool hasNext = m_Index + 1 < CampaignLevels.Count;

        if (hasNext)
        {
            int next = m_Index + 1;
            WorkshopUI.ShowDialog(m_Ui, "恭喜过关！", message,
                new DialogButton("返回选关", GoSelect),
                new DialogButton("下一关", () => StartLevel(next)));
        }
        else
        {
            WorkshopUI.ShowDialog(m_Ui, "全部通关！", message + "\n更多关卡敬请期待",
                new DialogButton("重新开始", () =>
                {
                    m_Modal = false;
                    RestartLevel();
                }),
                new DialogButton("返回选关", GoSelect));
        }
    }
}
