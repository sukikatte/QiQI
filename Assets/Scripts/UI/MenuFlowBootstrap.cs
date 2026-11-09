using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 运行时生成菜单 UI（仅 Play 时创建）。
/// </summary>
public class MenuFlowBootstrap : MonoBehaviour
{
    [Header("背景动画（窗外云、灯火/水晶发光、小蛇呼吸眨眼）")]
    [SerializeField] bool enableAmbientFx = true;

    [Header("主菜单布局")]
    [SerializeField] Vector2 titlePos = new Vector2(0f, 300f);
    [SerializeField] Vector2 titleSize = new Vector2(1360f, 420f);

    [Header("卷轴（打开状态）")]
    [SerializeField] Vector2 scrollPos = new Vector2(0f, -200f);
    [SerializeField] Vector2 scrollSize = new Vector2(1600f, 1600f);

    [Header("打开后整体缩放/偏移（1 和 (0,0) = 原样；想整体缩小/挪动卷轴+按钮时再改）")]
    [SerializeField] float openLayoutScale = 1f;
    [SerializeField] Vector2 openLayoutOffset = Vector2.zero;

    [Header("合着的卷轴（开场画面，点击后展开；紧挨着小蛇右侧）")]
    [SerializeField] Vector2 closedScrollPos = new Vector2(220f, -290f);
    [SerializeField] Vector2 closedScrollSize = new Vector2(264f, 528f);
    [SerializeField] Vector2 hintPos = new Vector2(350f, -430f);

    [Header("按钮相对卷轴中心")]
    [SerializeField] Vector2 startPos = new Vector2(-260f, 40f);
    [SerializeField] Vector2 workshopPos = new Vector2(260f, 40f);
    [SerializeField] Vector2 quitPos = new Vector2(0f, -200f);

    [Header("按钮大小")]
    [SerializeField] Vector2 buttonSize = new Vector2(600f, 240f);
    [SerializeField] Vector2 quitButtonSize = new Vector2(400f, 200f);

    static TMP_FontAsset s_ChineseFont;

    void Awake()
    {
        EnsureEventSystem();
        var canvas = CreateCanvas();
        string scene = SceneManager.GetActiveScene().name;

        // 两个 1920×1080 的舞台，都居中、等比缩放，所以都不会拉伸变形：
        //   背景舞台：按"铺满"缩放（窗口不是 16:9 时裁掉多出来的边缘），背景图和背景动画放这里 —— 屏幕上永远没有黑边
        //   内容舞台：按"完整放进去"缩放，所有按钮、文字、棋盘放这里 —— 永远不会被裁掉
        var bgStage = CreateStage(canvas.transform, "BackgroundStage", true);
        var uiStage = CreateStage(canvas.transform, "ContentStage", false);

        switch (scene)
        {
            case "MainMenu":
                MusicPlayer.SetTrack(MusicPlayer.Track.Menu);       // 主菜单：放菜单音乐（已经在放就不会重来）
                BuildMainMenu(bgStage, uiStage);
                break;
            case "MainGame":
                // 开始游戏：选关页 → 游玩，全部由 CampaignController 负责
                new GameObject("CampaignController")
                    .AddComponent<CampaignController>()
                    .Init(bgStage, uiStage, GetChineseFont());
                break;
            case "Workshop":
                if (!CampaignProgress.WorkshopUnlocked)
                {
                    SceneManager.LoadScene("MainMenu");
                    break;
                }
                // 创意工坊：背景 + 关卡列表 / 编辑器 / 测试 / 游玩，全部由 WorkshopController 负责
                CreateBackground(bgStage, "Art/Bg/workshopBg");
                new GameObject("WorkshopController")
                    .AddComponent<WorkshopController>()
                    .Init(uiStage, GetChineseFont());
                break;
            default:
                BuildSubPage(uiStage, GetChineseFont(), scene);
                break;
        }
    }

    /// <summary>固定 1920×1080 的舞台容器；cover = true 铺满屏幕（会裁边），false 完整放进屏幕（可能留空）。</summary>
    static Transform CreateStage(Transform canvas, string name, bool cover)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(canvas, false);
        go.AddComponent<StageFitter>().Configure(cover);
        return go.transform;
    }

    static TMP_FontAsset GetChineseFont()
    {
        if (s_ChineseFont != null)
            return s_ChineseFont;

        // 主字体：霞鹜文楷（Medium，开源 OFL 协议）；缺的字（极少数符号）回退到 Arial Unicode
        var source = Resources.Load<Font>("Fonts/LXGWWenKai-Medium");
        var fallback = Resources.Load<Font>("Fonts/ArialUnicode");
        if (source == null)
            source = fallback;
        if (source == null)
        {
            Debug.LogError("Missing Resources/Fonts/LXGWWenKai-Medium.");
            return TMP_Settings.defaultFontAsset;
        }

        s_ChineseFont = TMP_FontAsset.CreateFontAsset(source);
        if (s_ChineseFont != null)
        {
            s_ChineseFont.name = "ChineseRuntimeSDF";
            if (fallback != null && fallback != source)
            {
                var fallbackAsset = TMP_FontAsset.CreateFontAsset(fallback);
                if (fallbackAsset != null)
                {
                    fallbackAsset.name = "ChineseFallbackSDF";
                    if (s_ChineseFont.fallbackFontAssetTable == null)      // 运行时新建的字体资源，这张表默认是空的
                        s_ChineseFont.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>();
                    s_ChineseFont.fallbackFontAssetTable.Add(fallbackAsset);
                }
            }
        }
        return s_ChineseFont;
    }

    void BuildMainMenu(Transform bgParent, Transform parent)
    {
        CreateBackground(bgParent, "Art/Bg/qiqi_cover");

        // 背景动画（窗外云、发光、小蛇呼吸/眨眼）：和背景在同一个舞台里，位置永远对得上背景图
        if (enableAmbientFx)
            MenuAmbientFx.Build(bgParent);

        // 游戏名：开场从上方落下
        var title = CreateImage(parent, "Title", "Art/UI/gamename", titlePos, titleSize);
        if (title != null)
        {
            var titleGroup = title.gameObject.AddComponent<CanvasGroup>();
            title.gameObject.AddComponent<TitleDropIntro>()
                .Play(title as RectTransform, titleGroup, titlePos);
        }

        // 层级（从下到上）：背景 → 标题 → 打开的卷轴 → 按钮 → 合着的卷轴
        var scrollLayer = CreateLayoutRoot(parent, "OpenLayout_Scroll");
        var scrollRoot = CreateImage(scrollLayer, "Scroll", "Art/UI/scroll", scrollPos, scrollSize);
        if (scrollRoot == null)
            return;

        var buttonLayer = CreateLayoutRoot(parent, "OpenLayout_Buttons");
        var buttonsRoot = CreateEmptyRect(buttonLayer, "Buttons", scrollPos, scrollSize);

        WorkshopUI.Font = GetChineseFont();
        CreateImageButton(buttonsRoot, "StartGameButton", "Art/UI/startgame", startPos, buttonSize,
            () => ScreenTransition.LoadScene("MainGame"));
        var workshopBtn = CreateImageButton(buttonsRoot, "WorkshopButton", "Art/UI/workshop", workshopPos, buttonSize,
            () =>
            {
                if (CampaignProgress.WorkshopUnlocked)
                    ScreenTransition.LoadScene("Workshop");
                else
                    WorkshopUI.ShowAutoPopup(parent, "还没解锁", "通关第十关后解锁。");
            });
        if (workshopBtn != null && !CampaignProgress.WorkshopUnlocked)
            AddWorkshopLock(workshopBtn);
        CreateImageButton(buttonsRoot, "QuitButton", "Art/UI/quit", quitPos, quitButtonSize, QuitGame);

        // 合着的卷轴：开场主角，放在最上层，点击后触发展开
        var closedRoot = CreateImage(parent, "ScrollClosed", "Art/UI/scroll_0", closedScrollPos, closedScrollSize);
        if (closedRoot == null)
        {
            // 没有合着的图就直接显示打开状态
            return;
        }

        // 打开的卷轴 + 按钮：开场先隐藏，由 ScrollOpenIntro 在点击后淡入
        var scrollGroup = scrollRoot.gameObject.AddComponent<CanvasGroup>();
        var buttonsGroup = buttonsRoot.gameObject.AddComponent<CanvasGroup>();

        var closedImage = closedRoot.GetComponent<Image>();
        closedImage.raycastTarget = true;
        var closedGroup = closedRoot.gameObject.AddComponent<CanvasGroup>();

        var hintLabel = CreateLabel(parent, "ScrollHint", "点击卷轴", GetChineseFont(), 36,
            hintPos, new Vector2(600f, 60f));
        hintLabel.color = new Color(1f, 0.93f, 0.75f, 1f);
        var hintGroup = hintLabel.gameObject.AddComponent<CanvasGroup>();

        var intro = closedRoot.gameObject.AddComponent<ScrollOpenIntro>();
        // 打开后卷轴中心在画面上的位置（考虑整体缩放和偏移），合着的卷轴会移到这里
        Vector2 openCenter = scrollPos * openLayoutScale + openLayoutOffset;
        intro.Init(closedRoot as RectTransform, closedGroup, hintGroup,
            new[] { scrollRoot as RectTransform, buttonsRoot as RectTransform },
            new[] { scrollGroup, buttonsGroup },
            openCenter);

        var closedButton = closedRoot.gameObject.AddComponent<Button>();
        closedButton.targetGraphic = closedImage;
        closedButton.transition = Selectable.Transition.None;
        // 点卷轴时展开动画会立刻关掉它的反馈脚本，所以这里自己播 click，反馈脚本不再重复播
        closedButton.onClick.AddListener(() =>
        {
            UiSfx.PlayClick();
            intro.Open();
        });
        closedRoot.gameObject.AddComponent<UIButtonFeedback>().playClickSound = false;
    }

    static void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    static Sprite LoadSprite(string resourcePath)
    {
        var sprite = Resources.Load<Sprite>(resourcePath);
        if (sprite == null)
            Debug.LogError("图片加载失败: " + resourcePath + "（Texture Type 需为 Sprite (2D and UI)）");
        return sprite;
    }

    static void CreateBackground(Transform parent, string resourcePath)
    {
        var image = CreateFullscreenImage(parent, "Background", resourcePath);
        if (image != null)
            image.transform.SetAsFirstSibling();
    }

    /// <summary>铺满整个画面的图片（背景、小蛇抠图用同样的拉伸方式，所以位置完全重合）。</summary>
    static Image CreateFullscreenImage(Transform parent, string name, string resourcePath)
    {
        var sprite = LoadSprite(resourcePath);
        if (sprite == null) return null;

        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = false;
        image.raycastTarget = false;
        return image;
    }

    /// <summary>打开状态的整体容器：统一缩放 + 偏移，里面的子物体坐标不用改。</summary>
    Transform CreateLayoutRoot(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = Vector2.zero;
        rect.anchoredPosition = openLayoutOffset;
        rect.localScale = new Vector3(openLayoutScale, openLayoutScale, 1f);
        return go.transform;
    }

    static Transform CreateEmptyRect(Transform parent, string name, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
        return go.transform;
    }

    static Transform CreateImage(Transform parent, string name, string resourcePath, Vector2 pos, Vector2 size)
    {
        var sprite = LoadSprite(resourcePath);
        if (sprite == null) return null;

        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;

        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return go.transform;
    }

    static GameObject CreateImageButton(Transform parent, string name, string resourcePath, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction onClick)
    {
        var sprite = LoadSprite(resourcePath);
        if (sprite == null) return null;

        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;

        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = true;

        var button = go.GetComponent<Button>();
        button.targetGraphic = image;
        // 缩放反馈由 UIButtonFeedback 负责，这里关掉 Button 自带变色，避免叠两套
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(onClick);
        go.AddComponent<UIButtonFeedback>();
        return go;
    }

    static void AddWorkshopLock(GameObject button)
    {
        var image = button.GetComponent<Image>();
        if (image != null)
            image.color = new Color(0.58f, 0.55f, 0.52f, 1f);

        var lockSprite = Resources.Load<Sprite>("Art/UI/lock_icon");
        if (lockSprite == null)
            return;
        var lockGo = new GameObject("Lock", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        lockGo.transform.SetParent(button.transform, false);
        var lockRect = lockGo.GetComponent<RectTransform>();
        lockRect.anchorMin = lockRect.anchorMax = new Vector2(0.5f, 0.5f);
        lockRect.pivot = new Vector2(0.5f, 0.5f);
        lockRect.anchoredPosition = Vector2.zero;
        lockRect.sizeDelta = new Vector2(88f, 88f);
        var lockImg = lockGo.GetComponent<Image>();
        lockImg.sprite = lockSprite;
        lockImg.preserveAspect = true;
        lockImg.raycastTarget = false;
    }

    void BuildSubPage(Transform parent, TMP_FontAsset font, string title, string backgroundResource = null)
    {
        if (!string.IsNullOrEmpty(backgroundResource))
            CreateBackground(parent, backgroundResource);

        CreateLabel(parent, "Title", title, font, 64, new Vector2(0f, 180f), new Vector2(1000f, 120f));
        CreateButton(parent, "BackButton", "返回主页", font, new Vector2(0f, -40f), () => ScreenTransition.LoadScene("MainMenu"));
    }

    static void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
            return;

        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<StandaloneInputModule>();
    }

    static Canvas CreateCanvas()
    {
        var go = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1
            | AdditionalCanvasShaderChannels.Normal
            | AdditionalCanvasShaderChannels.Tangent;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    static TextMeshProUGUI CreateLabel(Transform parent, string name, string content, TMP_FontAsset font, float size, Vector2 pos, Vector2 rectSize)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = rectSize;

        var text = go.GetComponent<TextMeshProUGUI>();
        text.font = font;
        text.text = content;
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(0.18f, 0.16f, 0.14f, 1f);
        text.enableWordWrapping = false;
        text.raycastTarget = false;
        return text;
    }

    static void CreateButton(Transform parent, string name, string label, TMP_FontAsset font, Vector2 pos, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = new Vector2(280f, 64f);

        var image = go.GetComponent<Image>();
        image.color = new Color(0.86f, 0.72f, 0.48f, 1f);

        var button = go.GetComponent<Button>();
        var colors = button.colors;
        colors.highlightedColor = new Color(0.92f, 0.80f, 0.58f, 1f);
        colors.pressedColor = new Color(0.75f, 0.60f, 0.38f, 1f);
        button.colors = colors;
        button.onClick.AddListener(onClick);

        var labelGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelGo.transform.SetParent(go.transform, false);
        var labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        var text = labelGo.GetComponent<TextMeshProUGUI>();
        text.font = font;
        text.text = label;
        text.fontSize = 36f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(0.18f, 0.16f, 0.14f, 1f);
        text.raycastTarget = false;
    }
}
