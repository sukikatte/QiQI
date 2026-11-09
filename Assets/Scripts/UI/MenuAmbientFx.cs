using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 主菜单背景的"动起来"效果（在背景图上方叠加的几层）：
///   1. 窗外天空和云缓慢飘动（窗口形状的遮罩里滚动一条天空图）
///   2. 灯笼火光闪烁，收纳架里的蛋和水晶发光呼吸
///   3. 小蛇呼吸起伏、偶尔眨眼
/// 所有位置都用"背景图上的归一化坐标"（0~1，左上角为原点），
/// 背景是拉伸铺满画布的，所以不管分辨率怎么变，这些点都会对准画面里的同一处。
/// </summary>
public static class MenuAmbientFx
{
    // 窗口在背景图里的包围盒（归一化，原点在左下，和 Unity 的 anchor 一致）
    static readonly Vector2 WindowAnchorMin = new Vector2(0.028646f, 0.743056f);
    static readonly Vector2 WindowAnchorMax = new Vector2(0.171224f, 0.978009f);

    // 小蛇脚底中心，作为呼吸缩放的支点（归一化，原点在左下）
    static readonly Vector2 SnakePivot = new Vector2(0.491f, 0.053f);

    // 云飘动速度：每秒移动多少个窗口宽度（0.0143 ≈ 140 秒一圈）
    const float CloudSpeed = 0.0143f;

    struct GlowDef
    {
        public float u, v;       // 背景图上的位置（左上角原点，0~1）
        public float w, h;       // 大小（画布像素）
        public Color color;
        public float baseAlpha, amp, speed;
        public bool flicker;

        public GlowDef(float u, float v, float w, float h, Color color,
                       float baseAlpha, float amp, float speed, bool flicker = false)
        {
            this.u = u; this.v = v; this.w = w; this.h = h; this.color = color;
            this.baseAlpha = baseAlpha; this.amp = amp; this.speed = speed; this.flicker = flicker;
        }
    }

    static Material s_AdditiveMaterial;

    public static void Build(Transform parent)
    {
        BuildWindowSky(parent);
        BuildGlows(parent);
        BuildSnake(parent);
    }

    // ---------------- 窗外天空 ----------------
    static void BuildWindowSky(Transform parent)
    {
        var maskSprite = Resources.Load<Sprite>("Art/Fx/window_mask");
        var skySprite = Resources.Load<Sprite>("Art/Fx/window_sky_strip");
        if (maskSprite == null || skySprite == null)
        {
            Debug.LogWarning("窗外动画的图片没找到（Resources/Art/Fx/window_mask、window_sky_strip）。");
            return;
        }

        // 窗口遮罩：只有遮罩里（天空像素）的部分会显示滚动的天空，其余仍是背景原图
        var window = new GameObject("WindowSky", typeof(RectTransform), typeof(Image), typeof(Mask));
        window.transform.SetParent(parent, false);
        var wr = (RectTransform)window.transform;
        wr.anchorMin = WindowAnchorMin;
        wr.anchorMax = WindowAnchorMax;
        wr.offsetMin = Vector2.zero;
        wr.offsetMax = Vector2.zero;

        var maskImage = window.GetComponent<Image>();
        maskImage.sprite = maskSprite;
        maskImage.raycastTarget = false;
        window.GetComponent<Mask>().showMaskGraphic = false;

        var strip = new GameObject("SkyStrip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        strip.transform.SetParent(window.transform, false);
        var sr = (RectTransform)strip.transform;
        sr.anchorMin = new Vector2(0f, 0f);
        sr.anchorMax = new Vector2(0f, 1f);
        sr.pivot = new Vector2(0f, 0.5f);
        sr.anchoredPosition = Vector2.zero;
        var stripImage = strip.GetComponent<Image>();
        stripImage.sprite = skySprite;
        stripImage.preserveAspect = false;
        stripImage.raycastTarget = false;

        window.AddComponent<WindowSkyScroll>().Init(sr, CloudSpeed);
    }

    // ---------------- 灯火 / 蛋 / 水晶发光 ----------------
    static void BuildGlows(Transform parent)
    {
        var root = new GameObject("Glows", typeof(RectTransform));
        root.transform.SetParent(parent, false);
        var rr = (RectTransform)root.transform;
        rr.anchorMin = Vector2.zero;
        rr.anchorMax = Vector2.one;
        rr.offsetMin = Vector2.zero;
        rr.offsetMax = Vector2.zero;

        var warm = new Color(1f, 0.62f, 0.25f);
        var gold = new Color(1f, 0.85f, 0.45f);
        var pink = new Color(1f, 0.5f, 0.78f);
        var teal = new Color(0.45f, 0.95f, 0.95f);
        var violet = new Color(0.78f, 0.5f, 1f);
        var green = new Color(0.55f, 1f, 0.6f);
        var blue = new Color(0.6f, 0.8f, 1f);

        var defs = new[]
        {
            // 灯笼：噪声驱动的火焰抖动
            new GlowDef(0.2325f, 0.250f, 460f, 460f, warm, 0.38f, 0.22f, 3.2f, true),

            // 收纳架（每排一团柔光，颜色、速度各不相同，像在轻轻呼吸）
            new GlowDef(0.410f, 0.215f, 360f, 190f, pink,   0.20f, 0.12f, 0.9f),
            new GlowDef(0.410f, 0.365f, 360f, 190f, gold,   0.20f, 0.12f, 1.1f),
            new GlowDef(0.270f, 0.430f, 260f, 220f, teal,   0.20f, 0.12f, 0.8f),
            new GlowDef(0.590f, 0.255f, 400f, 180f, gold,   0.20f, 0.12f, 1.0f),
            new GlowDef(0.590f, 0.380f, 400f, 180f, pink,   0.20f, 0.12f, 0.7f),
            new GlowDef(0.770f, 0.255f, 380f, 180f, teal,   0.20f, 0.12f, 0.95f),
            new GlowDef(0.770f, 0.370f, 380f, 180f, warm,   0.20f, 0.12f, 1.15f),
            new GlowDef(0.770f, 0.480f, 380f, 180f, violet, 0.20f, 0.12f, 0.85f),

            // 地上的水晶和摆件
            new GlowDef(0.183f, 0.790f, 240f, 240f, violet, 0.26f, 0.16f, 1.3f),
            new GlowDef(0.280f, 0.810f, 260f, 260f, warm,   0.26f, 0.16f, 1.0f),
            new GlowDef(0.763f, 0.640f, 210f, 210f, warm,   0.24f, 0.14f, 1.2f),
            new GlowDef(0.879f, 0.600f, 220f, 220f, violet, 0.26f, 0.16f, 0.9f),
            new GlowDef(0.806f, 0.835f, 190f, 190f, violet, 0.22f, 0.14f, 1.4f),
            new GlowDef(0.745f, 0.850f, 180f, 180f, green,  0.20f, 0.12f, 1.1f),
            new GlowDef(0.259f, 0.620f, 150f, 150f, green,  0.20f, 0.12f, 1.25f),
            new GlowDef(0.653f, 0.100f, 220f, 220f, blue,   0.22f, 0.12f, 0.75f),
        };

        var additive = GetAdditiveMaterial();
        var glowSprite = GlowPulse.GetGlowSprite();

        for (int i = 0; i < defs.Length; i++)
        {
            var d = defs[i];
            var go = new GameObject("Glow_" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(root.transform, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(d.u, 1f - d.v);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(d.w, d.h);

            var image = go.GetComponent<Image>();
            image.sprite = glowSprite;
            image.raycastTarget = false;
            if (additive != null)
                image.material = additive;

            // 没有叠加着色器时（找不到时的兜底），用普通混合，亮度降低一点避免发白
            float k = additive != null ? 1f : 0.6f;
            // 每个光点的相位错开，避免同步闪
            float phase = i * 1.713f;
            go.AddComponent<GlowPulse>().Init(image, d.color, d.baseAlpha * k, d.amp * k, d.speed,
                phase, d.flicker, d.flicker ? 0.06f : 0.04f);
        }
    }

    public static Material GetAdditiveMaterial()
    {
        if (s_AdditiveMaterial != null)
            return s_AdditiveMaterial;

        var shader = Shader.Find("Qiqi/UIAdditive");
        if (shader == null)
        {
            Debug.LogWarning("找不到 Qiqi/UIAdditive 着色器（Resources/Shaders/UIAdditive.shader），发光改用普通混合。");
            return null;
        }
        s_AdditiveMaterial = new Material(shader);
        return s_AdditiveMaterial;
    }

    // ---------------- 小蛇：呼吸 + 眨眼 ----------------
    static void BuildSnake(Transform parent)
    {
        var snakeSprite = Resources.Load<Sprite>("Art/Qiqi/qiqi_snake");
        if (snakeSprite == null)
        {
            Debug.LogWarning("小蛇抠图没找到（Resources/Art/Qiqi/qiqi_snake）。");
            return;
        }

        // 整体容器铺满画布，支点设在小蛇脚底，缩放时"脚不动、头动"
        var root = new GameObject("SnakeIdle", typeof(RectTransform));
        root.transform.SetParent(parent, false);
        var rr = (RectTransform)root.transform;
        rr.anchorMin = Vector2.zero;
        rr.anchorMax = Vector2.one;
        rr.offsetMin = Vector2.zero;
        rr.offsetMax = Vector2.zero;
        rr.pivot = SnakePivot;
        rr.offsetMin = Vector2.zero;
        rr.offsetMax = Vector2.zero;

        CreateStretchedImage(root.transform, "Snake", snakeSprite);

        Image blink = null;
        var blinkSprite = Resources.Load<Sprite>("Art/Fx/qiqi_blink");
        if (blinkSprite != null)
            blink = CreateStretchedImage(root.transform, "Blink", blinkSprite);

        root.AddComponent<SnakeIdle>().Init(rr, blink);
    }

    static Image CreateStretchedImage(Transform parent, string name, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);

        var rect = (RectTransform)go.transform;
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
}
