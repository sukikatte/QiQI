using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 设计手记：编辑页角落里的一本合着的书（guide 图），点开后变成摊开的书（guide_open 图，弹窗），翻页看关卡设计思路。
/// </summary>
public class NotebookSpread
{
    public string leftTitle, left, rightTitle, right;
}

public class DesignNotebook : MonoBehaviour
{
    const float IconSize = 300f;
    const float BookRootScale = 0.92f;
    const float BookScale = 1.2f;
    const float LeftX = -352f;
    const float RightX = 350f;
    static readonly Vector2 PageTextSize = new Vector2(500f, 545f);
    const float PageTextY = 10f;
    const float PageTitleY = 340f;
    const float TitleFont = 42f;
    const float BodyFont = 28f;
    const float NavY = -292f;
    const float NavX = 548f;
    const float NavW = 200f;
    const float NavFont = 28f;
    static readonly Vector2 PageLabelPos = new Vector2(0f, 508f);
    const float PageLabelFont = 28f;
    static readonly Vector2 ClosePos = new Vector2(874f, 488f);
    const float CloseBtnSize = 72f;

    static readonly NotebookSpread[] Spreads =
    {
        new NotebookSpread
        {
            leftTitle = "先记住规则",
            left = "· 琦琦只能推蛋，不能拉蛋。\n" +
                   "· 一次只能推一个蛋；蛋前面还有蛋或者水晶，就推不动。\n" +
                   "· 水晶墙走不过，也推不动。\n" +
                   "· 把所有蛋都推进展示盒，就过关。\n" +
                   "· 蛋的数量不能比展示盒多，不然一定过不了关。",
            rightTitle = "什么叫卡死",
            right = "蛋再也推不进展示盒的时候，游戏会弹出失败。有两种：\n\n" +
                    "1. 蛋被“冻住”：左右、上下两个方向都被挡住。挡住的意思是，这个方向的一侧是水晶或棋盘边，或者是另一个也被冻住的蛋。最典型的就是蛋被推进了角落。\n\n" +
                    "2. 蛋贴着棋盘边，而这条边上一个展示盒都没有。",
        },
        new NotebookSpread
        {
            leftTitle = "利用边缘行",
            left = "蛋一旦贴着棋盘边，就只能沿着这条边滑，滑不出去，也推不回中间。\n\n" +
                   "· 单独一个蛋贴着边、两侧都空着：不会失败，它还能沿着边滑。\n" +
                   "· 这条边上有展示盒：这条边就是一条“滑道”，把蛋推上边，再一路滑进展示盒。\n" +
                   "· 这条边上没有展示盒：蛋一贴边就卡死。",
            rightTitle = "边缘行的陷阱",
            right = "· 两个蛋在边上挨在一起，又都不在展示盒里：互相堵住，两个都卡死。\n" +
                    "· 蛋贴着边，沿边的一侧又有水晶：两个方向都动不了，卡死。\n" +
                    "· 挨着的两个蛋里，只要有一个已经在展示盒里，另一个照样会被它堵住。想设计“先后顺序”，可以用这一招。\n" +
                    "· 角落永远是死角，除非那里本身就是展示盒。",
        },
        new NotebookSpread
        {
            leftTitle = "水晶怎么摆",
            left = "· 水晶是你的画笔：用它围出走廊、堵住捷径、造出转角。\n" +
                   "· 一堵水晶墙加上棋盘边，可以做出只能走一条路的窄道。\n" +
                   "· 别把水晶摆得离展示盒太近，容易让蛋没有路进去。\n" +
                   "· 给琦琦留出绕到蛋后面的空间：要推蛋，就得先站到蛋的另一边。",
            rightTitle = "设计的步骤",
            right = "1. 先选地图大小：新手关建议用 3×3 到 5×5，复杂的关可以尝试更大的地图。\n\n" +
                    "2. 摆好展示盒和蛋，再放琦琦。\n\n" +
                    "3. 保存前一定先点“测试关卡”，自己先走一遍！\n\n" +
                    "4. 点“计算难度”，可以查看当前关卡信息，如难度、最短路径。\n\n" +
                    "5. 最后，起个好名字，保存！",
        },
        new NotebookSpread
        {
            leftTitle = "读懂难度数字",
            left = "· 最少步数：琦琦走路加推蛋，至少要几步。下面还会画出一条最短路线，照着走一遍就能验证。\n" +
                   "· 最优解法：用最少步数过关的推法有几种。只有 1 种，谜题就很“唯一”；很多种，就比较宽松。\n" +
                   "· 局面总数：这一关能走出多少种不同的局面。",
            rightTitle = "陷阱和难度",
            right = "· 无解局面：走进去就再也过不了关的局面，占的比例越高，越容易走错。\n" +
                    "· 暗坑：已经没救，但游戏还不会弹失败的局面。暗坑多，玩家会以为还有救。\n" +
                    "· 难度分五档：入门、简单、中等、较难、困难。",
        },
        new NotebookSpread
        {
            leftTitle = "小技巧",
            left = "· 想让关卡简单一点：多放展示盒，少放水晶。\n" +
                   "· 想让关卡刁钻一点：让蛋必须先绕一圈，才能推进展示盒。\n" +
                   "· 最优解法只有 1 种的关，通关时最有成就感。\n" +
                   "· 蛋少而地图大，往往比蛋多而地图小更简单。",
            rightTitle = "祝你设计愉快！",
            right = "拿不准的时候，就去测试。\n\n" +
                    "琦琦正等着走进你布置的收藏室呢。\n\n" +
                    "—— 设计手记",
        },
    };

    static readonly Rect PageButtonRaw = new Rect(200f, 225f, 1136f, 610f);
    const float PageButtonAspect = 1136f / 610f;

    const float OpenTime = 0.55f;
    const float CloseTime = 0.3f;
    const float FadeOutTime = 0.17f;                     // 翻页：旧的内容淡出
    const float FadeInTime = 0.26f;                      // 再让新的内容淡入（像 PPT 的淡入淡出）
    const float DimAlpha = 0.62f;

    class Page
    {
        public RectTransform root;
        public CanvasGroup group;
        public TextMeshProUGUI title, body;
    }

    // 合着的书（图标）上的待机小动画
    bool m_IsIcon;
    RectTransform m_IconRect;
    Vector2 m_IconBase;
    float m_IconSize = 300f;
    Image m_Halo;
    Image[] m_Sparks;
    const int SparkCount = 7;
    float m_BookRootScale = 0.92f;

    // 摊开的书
    RectTransform m_BookRoot;
    RectTransform m_Book;
    Image m_Dim, m_Glow;
    CanvasGroup m_Content;
    Page m_LeftPage, m_RightPage;
    TextMeshProUGUI m_PageLabel;
    GameObject m_Prev, m_Next;
    CanvasGroup m_PageLabelGroup, m_CloseGroup;
    int m_Spread;
    Vector2 m_FromPos;
    Action m_OnClosed;
    bool m_Busy;                // 正在打开 / 翻页 / 合上
    bool m_Closing;
    float m_GlowAmount;

    // ====================================================================
    //  合着的书（编辑页角落的按钮）
    // ====================================================================
    public static RectTransform CreateIcon(Transform parent, Vector2 pos, Action onClick)
    {
        var sprite = Resources.Load<Sprite>("Art/UI/guide");
        float iconSize = IconSize;

        // 外面套一个容器：光芒画在书的后面，容器一隐藏，光芒也跟着隐藏
        var container = WorkshopUI.NewRect(parent, "DesignNotebook", pos, new Vector2(iconSize, iconSize));

        var additive = MenuAmbientFx.GetAdditiveMaterial();
        var glowSprite = GlowPulse.GetGlowSprite();

        var halo = WorkshopUI.NewImage(container, "Halo", glowSprite, Vector2.zero, new Vector2(iconSize * 1.7f, iconSize * 1.7f));
        halo.color = new Color(1f, 0.8f, 0.38f, 0.5f);
        if (additive != null)
            halo.material = additive;

        var sparks = new Image[SparkCount];
        for (int i = 0; i < SparkCount; i++)
        {
            float size = 34f + (i % 3) * 14f;
            sparks[i] = WorkshopUI.NewImage(container, "Spark_" + i, glowSprite, Vector2.zero, new Vector2(size, size));
            sparks[i].color = new Color(1f, 0.9f, 0.55f, 0f);
            if (additive != null)
                sparks[i].material = additive;
        }

        var image = WorkshopUI.NewImage(container, "Icon", sprite, Vector2.zero, new Vector2(iconSize, iconSize), false, true);
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => onClick?.Invoke());

        image.gameObject.AddComponent<UIButtonFeedback>();

        var idle = image.gameObject.AddComponent<DesignNotebook>();
        idle.m_IsIcon = true;
        idle.m_IconRect = image.rectTransform;
        idle.m_IconBase = Vector2.zero;
        idle.m_IconSize = iconSize;
        idle.m_Halo = halo;
        idle.m_Sparks = sparks;
        return container;
    }

    void Update()
    {
        if (m_IsIcon)
        {
            // 待机：像漂浮着一样轻轻上下浮动、微微摇晃
            float t = Time.unscaledTime;
            m_IconRect.anchoredPosition = m_IconBase + new Vector2(0f, Mathf.Sin(t * 1.6f) * 5f);
            m_IconRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 1.1f + 1f) * 1.6f);

            // 光芒：外圈的光晕慢慢呼吸，几颗小光点绕着书飘、一闪一闪（提醒玩家这里有东西）
            float breathe = 0.5f + 0.5f * Mathf.Sin(t * 2.0f);
            var haloColor = m_Halo.color;
            haloColor.a = 0.30f + 0.38f * breathe;
            m_Halo.color = haloColor;
            m_Halo.rectTransform.localScale = Vector3.one * (0.94f + 0.1f * breathe);

            for (int i = 0; i < m_Sparks.Length; i++)
            {
                float phase = i * (Mathf.PI * 2f / m_Sparks.Length);
                float angle = t * 0.45f + phase;
                float radius = m_IconSize * (0.56f + 0.06f * Mathf.Sin(t * 1.3f + i * 1.7f));
                m_Sparks[i].rectTransform.anchoredPosition = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * 0.82f);
                float twinkle = Mathf.Max(0f, Mathf.Sin(t * 2.6f + i * 2.1f));
                var c = m_Sparks[i].color;
                c.a = 0.15f + 0.85f * twinkle * twinkle;
                m_Sparks[i].color = c;
                m_Sparks[i].rectTransform.localScale = Vector3.one * (0.6f + 0.6f * twinkle);
            }
            return;
        }

        if (m_Glow != null)
        {
            // 金色的光晕在书后面轻轻呼吸
            float pulse = 0.82f + 0.18f * Mathf.Sin(Time.unscaledTime * 2.2f);
            var c = m_Glow.color;
            c.a = 0.55f * m_GlowAmount * pulse;
            m_Glow.color = c;
        }
    }

    // ====================================================================
    //  摊开的书（弹窗）
    // ====================================================================
    /// <param name="fromPos">合着的书的位置：打开时书从这里"翻开"，合上时回到这里。</param>
    public static void Open(Transform canvas, Vector2 fromPos, Action onClosed)
    {
        var overlay = WorkshopUI.NewDimmer(canvas);
        var notebook = overlay.gameObject.AddComponent<DesignNotebook>();
        notebook.m_OnClosed = onClosed;
        float rootScale = BookRootScale;
        notebook.m_BookRootScale = rootScale;
        notebook.m_FromPos = fromPos / rootScale;
        notebook.Build(overlay);
    }

    void Build(RectTransform overlay)
    {
        // 点书外面的暗处也能合上
        var dimButton = overlay.gameObject.AddComponent<Button>();
        dimButton.transition = Selectable.Transition.None;
        dimButton.onClick.AddListener(Close);
        m_Dim = overlay.GetComponent<Image>();
        var dimColor = m_Dim.color;
        dimColor.a = 0f;
        m_Dim.color = dimColor;

        var openSprite = Resources.Load<Sprite>("Art/UI/guide_open");

        // 书后面的光晕
        var bookSize = new Vector2(1536f, 864f) * BookScale;
        float leftX = LeftX;
        float rightX = RightX;
        float navY = NavY;
        float navX = NavX;
        float navW = NavW;
        float navFont = NavFont;
        const string prev = "上一页";
        const string next = "下一页";
        Vector2 pageLabelPos = PageLabelPos;
        float pageLabelFont = PageLabelFont;
        Vector2 closePos = ClosePos;
        float closeSize = CloseBtnSize;

        m_BookRoot = WorkshopUI.NewRect(overlay, "BookRoot", Vector2.zero, Vector2.zero);
        m_BookRoot.localScale = new Vector3(m_BookRootScale, m_BookRootScale, 1f);

        m_Glow = WorkshopUI.NewImage(m_BookRoot, "Glow", GlowPulse.GetGlowSprite(), Vector2.zero, bookSize * 1.18f);
        m_Glow.color = new Color(1f, 0.82f, 0.4f, 0f);
        var additive = MenuAmbientFx.GetAdditiveMaterial();
        if (additive != null)
            m_Glow.material = additive;

        var bookImage = WorkshopUI.NewImage(m_BookRoot, "Book", openSprite, m_FromPos, bookSize, false, true);
        m_Book = bookImage.rectTransform;
        m_Book.localScale = new Vector3(0.05f, 0.1f, 1f);

        // 内容放在一个 CanvasGroup 里：书展开以后再淡入
        var content = WorkshopUI.NewStretch(m_Book, "Content");
        m_Content = content.gameObject.AddComponent<CanvasGroup>();
        m_Content.alpha = 0f;
        m_Content.blocksRaycasts = false;

        var first = Spreads[0];
        m_LeftPage = BuildPage(content, new Vector2(leftX, 0f), first.leftTitle, first.left);
        m_RightPage = BuildPage(content, new Vector2(rightX, 0f), first.rightTitle, first.right);

        var cream = new Color(1f, 0.94f, 0.78f);
        m_PageLabel = WorkshopUI.NewLabel(content, "PageLabel", "", pageLabelFont, cream, pageLabelPos, new Vector2(300f, 40f));
        m_PageLabelGroup = m_PageLabel.gameObject.AddComponent<CanvasGroup>();
        m_Prev = NewPageButton(content, "Prev", prev, new Vector2(-navX, navY), navW, navFont, () => Flip(-1));
        m_Next = NewPageButton(content, "Next", next, new Vector2(navX, navY), navW, navFont, () => Flip(1));

        m_CloseGroup = NewCloseButton(overlay, closePos, closeSize, Close);
        m_CloseGroup.alpha = 0f;

        RefreshNav();
        StartCoroutine(OpenRoutine());
    }

    Page BuildPage(Transform parent, Vector2 localPos, string title, string body)
    {
        var textSize = PageTextSize;
        float titleY = PageTitleY;
        float textY = PageTextY;
        float titleFont = TitleFont;
        float bodyFont = BodyFont;
        var root = WorkshopUI.NewRect(parent, "Page", localPos, new Vector2(textSize.x, 760f));
        var page = new Page { root = root, group = root.gameObject.AddComponent<CanvasGroup>() };
        page.title = WorkshopUI.NewLabel(root, "Title", title, titleFont, WorkshopUI.Ink, new Vector2(0f, titleY), new Vector2(textSize.x, 60f));
        page.body = WorkshopUI.NewLabel(root, "Body", body, bodyFont, WorkshopUI.Ink, new Vector2(0f, textY), textSize, TextAlignmentOptions.TopLeft, true);
        page.body.lineSpacing = 8f;
        page.body.paragraphSpacing = 4f;
        page.body.enableAutoSizing = true;
        page.body.fontSizeMax = bodyFont;
        page.body.fontSizeMin = bodyFont * 0.75f;
        return page;
    }

    static void SetPage(Page page, string title, string body)
    {
        page.title.text = title;
        page.body.text = body;
    }

    void RefreshNav()
    {
        m_PageLabel.text = "第 " + (m_Spread + 1) + " / " + Spreads.Length + " 页";
        RefreshNavButtons();
    }

    void RefreshNavButtons()
    {
        m_Prev.SetActive(m_Spread > 0);
        m_Next.SetActive(m_Spread < Spreads.Length - 1);
    }

    /// <summary>用 page 图做的按钮：图只有底，字是盖在上面的。</summary>
    static GameObject NewPageButton(Transform parent, string name, string label, Vector2 pos, float width, float fontSize, Action onClick)
    {
        var size = new Vector2(width, width / PageButtonAspect);
        var image = WorkshopUI.NewImage(parent, name, PageButtonSprite(), pos, size, false, true);
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => onClick?.Invoke());

        // 字放在木头底的正中（图上宝石在上面，木头区域比图的中心略低一点）
        var text = WorkshopUI.NewLabel(image.transform, "Text", label, fontSize, WorkshopUI.Cream,
            new Vector2(0f, -size.y * 0.045f), new Vector2(size.x * 0.62f, size.y * 0.42f));
        text.fontStyle = FontStyles.Bold;
        text.enableAutoSizing = true;
        text.fontSizeMax = fontSize;
        text.fontSizeMin = fontSize * 0.6f;

        image.gameObject.AddComponent<UIButtonFeedback>();
        return image.gameObject;
    }

    /// <summary>圆形的关闭按钮：深木色的圆 + 金边 + 一个奶油色的叉。</summary>
    static CanvasGroup NewCloseButton(Transform parent, Vector2 pos, float size, Action onClick)
    {
        var disc = WorkshopUI.NewImage(parent, "Close", DiscSprite(), pos, new Vector2(size, size), false, true);
        disc.preserveAspect = true;
        var button = disc.gameObject.AddComponent<Button>();
        button.targetGraphic = disc;
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => onClick?.Invoke());

        var cream = new Color(1f, 0.94f, 0.78f);
        for (int i = 0; i < 2; i++)
        {
            var bar = WorkshopUI.NewImage(disc.transform, "X_" + i, null, Vector2.zero, new Vector2(7f, 34f));
            bar.color = cream;
            bar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? 45f : -45f);
        }

        disc.gameObject.AddComponent<UIButtonFeedback>();
        return disc.gameObject.AddComponent<CanvasGroup>();
    }

    static Sprite s_Disc;

    static Sprite DiscSprite()
    {
        if (s_Disc != null)
            return s_Disc;
        const int N = 128;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var fill = new Color(0.30f, 0.17f, 0.08f, 0.96f);
        var rim = new Color(0.95f, 0.70f, 0.20f, 1f);
        var pixels = new Color[N * N];
        float c = (N - 1) * 0.5f;
        for (int y = 0; y < N; y++)
        {
            for (int x = 0; x < N; x++)
            {
                float r = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                float edge = Mathf.Clamp01(c - r + 0.5f);                     // 最外面的抗锯齿
                float rimMix = Mathf.Clamp01((r - (c - 9f)) * 0.7f);          // 靠外 9 像素是金边
                var col = Color.Lerp(fill, rim, rimMix);
                col.a *= edge;
                pixels[y * N + x] = col;
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        s_Disc = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
        return s_Disc;
    }

    static Sprite s_PageButton;

    static Sprite PageButtonSprite()
    {
        if (s_PageButton == null)
            s_PageButton = WorkshopUI.CropSprite(Resources.Load<Sprite>("Art/UI/page"), PageButtonRaw);
        return s_PageButton;
    }

    // ---------------- 打开：书从合着的位置飘出来、翻开、放大 ----------------
    IEnumerator OpenRoutine()
    {
        m_Busy = true;
        var wobble = 0f;
        for (float t = 0f; t < OpenTime; t += Time.unscaledDeltaTime)
        {
            float u = t / OpenTime;
            float grow = EaseOutBack(u, 1.5f);
            float unfold = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.08f, 0.75f, u));      // 合着是窄的，翻开才变宽
            float scale = Mathf.LerpUnclamped(0.1f, 1f, grow);
            m_Book.localScale = new Vector3(scale * Mathf.Lerp(0.42f, 1f, unfold), scale, 1f);
            m_Book.anchoredPosition = Vector2.Lerp(m_FromPos, Vector2.zero, Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, u * 1.25f)));
            wobble = (1f - u) * (1f - u) * 16f;
            m_Book.localRotation = Quaternion.Euler(0f, 0f, wobble * Mathf.Cos(u * 9f));
            SetDim(u);
            m_GlowAmount = Mathf.SmoothStep(0f, 1f, u);
            yield return null;
        }
        m_Book.localScale = Vector3.one;
        m_Book.anchoredPosition = Vector2.zero;
        m_Book.localRotation = Quaternion.identity;
        SetDim(1f);
        m_GlowAmount = 1f;

        for (float t = 0f; t < 0.22f; t += Time.unscaledDeltaTime)
        {
            m_Content.alpha = t / 0.22f;
            yield return null;
        }
        m_Content.alpha = 1f;
        m_Content.blocksRaycasts = true;
        m_Busy = false;
    }

    void SetDim(float u)
    {
        var c = m_Dim.color;
        c.a = DimAlpha * Mathf.Clamp01(u);
        m_Dim.color = c;
        if (m_CloseGroup != null)
            m_CloseGroup.alpha = Mathf.Clamp01(u);
    }

    // ---------------- 翻页：旧内容淡出，换成新内容，再淡入（类似 PPT 的淡入淡出）----------------
    void Flip(int delta)
    {
        if (m_Busy || m_Closing)
            return;
        int target = m_Spread + delta;
        if (target < 0 || target >= Spreads.Length)
            return;
        StartCoroutine(FlipRoutine(target));
    }

    IEnumerator FlipRoutine(int target)
    {
        m_Busy = true;
        m_Next.GetComponent<Button>().interactable = false;
        m_Prev.GetComponent<Button>().interactable = false;

        for (float t = 0f; t < FadeOutTime; t += Time.unscaledDeltaTime)
        {
            SetPageAlpha(1f - Mathf.SmoothStep(0f, 1f, t / FadeOutTime));
            yield return null;
        }
        SetPageAlpha(0f);

        // 内容在看不见的时候换掉
        var s = Spreads[target];
        SetPage(m_LeftPage, s.leftTitle, s.left);
        SetPage(m_RightPage, s.rightTitle, s.right);
        m_Spread = target;
        RefreshNav();

        for (float t = 0f; t < FadeInTime; t += Time.unscaledDeltaTime)
        {
            SetPageAlpha(Mathf.SmoothStep(0f, 1f, t / FadeInTime));
            yield return null;
        }
        SetPageAlpha(1f);

        m_Next.GetComponent<Button>().interactable = true;
        m_Prev.GetComponent<Button>().interactable = true;
        m_Busy = false;
    }

    void SetPageAlpha(float a)
    {
        m_LeftPage.group.alpha = a;
        m_RightPage.group.alpha = a;
        m_PageLabelGroup.alpha = a;
    }

    // ---------------- 合上：书缩窄、转一转、飘回合着的位置 ----------------
    void Close()
    {
        if (m_Closing || (m_Busy && m_Content.alpha < 0.5f))
            return;
        m_Closing = true;
        StopAllCoroutines();
        StartCoroutine(CloseRoutine());
    }

    IEnumerator CloseRoutine()
    {
        m_Content.blocksRaycasts = false;
        float startAlpha = m_Content.alpha;
        Vector3 startScale = m_Book.localScale;
        Vector2 startPos = m_Book.anchoredPosition;
        for (float t = 0f; t < CloseTime; t += Time.unscaledDeltaTime)
        {
            float u = t / CloseTime;
            float ease = u * u * (3f - 2f * u);
            m_Content.alpha = startAlpha * Mathf.Clamp01(1f - u * 3.5f);        // 字先淡掉
            float scale = Mathf.Lerp(1f, 0.1f, ease);
            float fold = Mathf.Lerp(1f, 0.42f, Mathf.InverseLerp(0.1f, 0.8f, u));  // 合拢：变窄
            m_Book.localScale = new Vector3(scale * fold, scale, 1f);
            m_Book.anchoredPosition = Vector2.Lerp(startPos, m_FromPos, ease);
            m_Book.localRotation = Quaternion.Euler(0f, 0f, -10f * ease);
            SetDim(1f - u);
            m_GlowAmount = 1f - u;
            yield return null;
        }
        m_OnClosed?.Invoke();
        Destroy(gameObject);
    }

    static float EaseOutBack(float t, float overshoot)
    {
        float c3 = overshoot + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + overshoot * Mathf.Pow(t - 1f, 2f);
    }
}
