using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 棋盘的画面和鼠标输入。挂在棋盘区域（一个带透明 Image 的 RectTransform）上。
///
/// 画法：不再是平铺的方格，而是直接"画在背景的地板上"——
///   · 地砖是半透明的石板，带一点厚度，能透出背景地板的纹理；
///   · 带透视：远处的格子更窄、更矮，近处的更大，和背景的空间感一致；
///   · 蛋、水晶、琦琦、展示盒都按所在行缩放，并站在格子靠下的"站立点"上。
/// 层级（从下到上）：格线 → 格子底部高光（展示盒奶油色、蛋白色）→ 展示盒底座 → 水晶、鸟蛋、展示盒玻璃罩和琦琦（按前后排序）→ 鼠标悬停高亮。
/// 画面只"读"SokobanGame 的状态；Refresh() 之后，鸟蛋和琦琦会平滑滑到新位置。
/// </summary>
public class WorkshopBoardView : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    /// <summary>点了某个格子。第二个参数：true = 刚按下，false = 按住拖动经过。</summary>
    public event Action<GridPos, bool> CellPressed;

    /// <summary>是否响应鼠标（编辑时打开，游玩时关闭）。同时控制悬停高亮。</summary>
    public bool InputEnabled { get; set; }

    /// <summary>
    /// 入场演出用：把"其余部分"（格线、展示盒、水晶、琦琦）整体抬高多少像素，0 = 正常位置，
    /// 抬到 HiddenLift 就在屏幕外面看不见了。EggLifts：每个蛋各自抬高多少（null = 都在正常位置）。
    /// 这样蛋可以先掉下来，格子和水晶之后再掉。
    /// </summary>
    public const float HiddenLift = 1100f;
    public float RestLift { get; set; }
    public float[] EggLifts { get; set; }

    class Item
    {
        public RectTransform rect;
        public Image image;
        public Vector2 target;
        public bool fresh = true;     // 刚创建：直接放到位置，不做滑动
        public Vector2 pos;           // 平滑滑动中的位置（蛋用；不含摇摆 / 弹跳的偏移）
        public float bounceY;         // 失败弹跳：额外抬起的高度（像素）
        public Vector2 squash = Vector2.one;   // 失败弹跳：压扁 / 拉长
    }

    // 失败演出：卡死的蛋弹跳 + 周围冒出魔法光效
    class FailFx
    {
        public Item egg;
        public Image aura;
        public Image[] rings;
        public Image[] sparks;
        public float[] sparkAngle;
        public float t;
    }

    class GoalItem
    {
        public RectTransform rect;
        public Image glow;
        // 玻璃盒分成前后两层（后层在蛋下面，前层在蛋上面），这样蛋看起来是放在盒子里面。
        // 每层都有一个带裁剪的容器（放在实体层里参与前后排序），图片在容器里上下滑动，从容器底边"长"出来。
        public RectTransform backRoot, frontRoot;
        public RectTransform backImage, frontImage;
        public GridPos pos;
        public bool closed;               // 目标状态：true = 盒子升起盖好，false = 只有底座
        public float t;                   // 0~1 动画进度（线性）
        public float shown;               // 经过缓动后的显示进度（可能略大于 1，形成回弹）
    }

    // ---------------- 棋盘形状 ----------------
    const float CellAspect = 0.62f;       // 最近一行格子的 高/宽（小于 1 = 从斜上方看地面）
    const float Perspective = 0.14f;      // 透视强度：最远一行比最近一行窄多少（0.14 = 窄 14%）
    const float UnitOverhang = 100f;      // 最远一排的水晶最多探出棋盘上沿多少像素（决定小棋盘里蛋/水晶不跟着格子无限放大）
    const float LineWidth = 4f;           // 格线粗细（像素，最近一行；远处按透视变细）
    static readonly Color LineColor = new Color(1f, 1f, 1f, 0.9f);   // 所有格线统一：白色、0.9 不透明度
    static readonly Color FloorGoalColor = new Color(1f, 0.95f, 0.84f, 1f);  // 展示盒格子底部：奶油色高光
    static readonly Color FloorEggColor = new Color(1f, 1f, 0.97f, 1f);      // 蛋所在格子底部：白色高光

    // ---------------- 站立点和大小（宽度单位 = 当前行的格子宽度）----------------
    // 站立点 = 格子中心再往下一点；底座中心、蛋的底部、水晶的底部、琦琦的脚都落在这里。
    const float StandDrop = 0.18f;        // 站立点比格子中心低多少（占该行格子高度的比例）
    const float CrystalScale = 1.4f;      // 水晶图占一格宽度的倍数（略大于一格）
    const float CrystalRise = 0.53f;      // 水晶图中心比站立点高多少
    const float EggScale = 0.7f;          // 蛋占一格宽度的倍数（要小到能放进展示盒里）
    const float EggRise = 0.33f;          // 蛋图中心比站立点高多少（蛋的底部正好落在站立点）
    const float SnakeScale = 1.2f;        // 琦琦图占一格宽度的倍数
    const float SnakeRise = 0.445f;       // 琦琦图中心比站立点高多少

    // 展示盒：底座 box_mark 一直在；蛋在上面、且琦琦离得远时，玻璃盒 box 从底座里升起来
    // 对齐关系：底座中心 = 站立点；玻璃盒的地板中心也落在这里，所以蛋不管在盒里还是盒子沉下去，都稳稳站在底座中央。
    const float PadWidth = 1.0f;          // 底座宽（占一格）
    const float CaseWidth = 0.95f;        // 玻璃盒宽（占一格）
    const float CaseFloorRise = 0.28f;    // 玻璃盒的地板中心比盒子底边高多少
    const float CaseRiseTime = 0.45f;     // 升起用时（秒）
    const float CaseLowerTime = 0.28f;    // 落下用时（秒）
    const int NearDistance = 1;           // 琦琦离盒子几格以内算"靠近"，盒子会自动打开

    const float SlideSharpness = 24f;
    /// <summary>滑动快慢倍率：1 = 普通点按，0.5 = 长按连续走时每一步滑得更慢。</summary>
    public float SlideSpeed { get; set; } = 1f;

    // 蛋在场上轻轻左右摇摆（绕着蛋的底部）
    const float SwayDegrees = 3.2f;       // 最大摆角
    const float SwaySpeed = 2.3f;         // 摆动快慢（弧度/秒）

    // 失败演出
    const float FailDuration = 1.35f;     // 整段演出多久（之后才弹失败窗口）
    const int FailSparkCount = 14;

    RectTransform m_Root;
    RectTransform m_Tiles, m_Marks, m_Goals, m_Entities, m_Fx;
    readonly List<RectTransform> m_GoalMarks = new List<RectTransform>();
    readonly List<RectTransform> m_EggMarks = new List<RectTransform>();
    readonly List<FailFx> m_FailFx = new List<FailFx>();
    static Sprite s_RingSprite;
    Image m_Hover;
    GridPos m_HoverCell = new GridPos(-1, -1);
    int m_Size;
    float m_W;                // 最近一行的格子宽度（像素）
    float m_U;                // 蛋、水晶、琦琦、展示盒的"单位宽度"：小棋盘格子很大时不跟着等比放大，免得最远一排的水晶顶到说明栏
    float m_BoardWidth;
    float m_H;                // 最近一行的格子高度（像素）
    float m_Htot;             // 整个棋盘的总高度（像素）
    SokobanGame m_Game;

    readonly List<GoalItem> m_GoalItems = new List<GoalItem>();
    readonly List<Item> m_BoxItems = new List<Item>();
    Item m_SnakeItem;
    Image m_SnakeShadow;
    Vector2 m_SnakePos;               // 琦琦平滑移动中的位置（不含跳起的高度）

    // 琦琦的朝向动画：正面(turn_1) / 侧面(turn_2，头朝左，向右时水平翻转) / 背面(turn_3)
    enum Pose { Front = 0, Side = 1, Back = 2 }
    struct SnakeFrame
    {
        public Pose pose;
        public float flip;            // 1 = 原图（朝左），-1 = 水平翻转（朝右）；正面/背面固定为 1
    }
    const float TurnFrameTime = 0.065f;   // 转身时每一帧停留多久
    const float HopHeight = 0.10f;        // 走一步时跳起的高度（占一格宽度的比例）
    Sprite[] m_TurnSprites;
    readonly Queue<SnakeFrame> m_FrameQueue = new Queue<SnakeFrame>();
    SnakeFrame m_Shown = new SnakeFrame { pose = Pose.Front, flip = 1f };
    SnakeFrame m_End = new SnakeFrame { pose = Pose.Front, flip = 1f };   // 动画播完后会停在哪一帧
    float m_SideFlip = 1f;                // 最近一次朝向的左/右，上下转身时用它当过渡帧
    float m_FrameTimer;

    GridPos m_LastDragCell = new GridPos(-1, -1);

    Sprite m_SnakeSprite;
    readonly List<Item> m_WallItems = new List<Item>();
    readonly List<KeyValuePair<float, RectTransform>> m_SortBuffer = new List<KeyValuePair<float, RectTransform>>();

    // ====================================================================
    //  透视几何
    // ====================================================================
    /// <summary>第 t 行（t 可以是小数，0 = 最近的边，size = 最远的边）的缩放：越远越小。</summary>
    float ScaleAtT(float t) => 1f - Perspective * (t / m_Size);

    /// <summary>第 t 行边界在棋盘里的高度（从最近的边往上量，像素）。每行高度按缩放递减，所以是个二次曲线。</summary>
    float HeightAtT(float t) => m_H * (t - Perspective * t * t / (2f * m_Size));

    /// <summary>HeightAtT 的反函数。</summary>
    float TAtHeight(float y)
    {
        float a = m_H * Perspective / (2f * m_Size);
        float disc = m_H * m_H - 4f * a * y;
        if (disc < 0f) disc = 0f;
        return (m_H - Mathf.Sqrt(disc)) / (2f * a);
    }

    /// <summary>棋盘上的点（格坐标 g、行 t）→ 相对棋盘中心的局部坐标。</summary>
    Vector2 PointAt(float g, float t)
    {
        return new Vector2((g - m_Size * 0.5f) * m_W * ScaleAtT(t), HeightAtT(t) - m_Htot * 0.5f);
    }

    /// <summary>格子中心（透视后）。</summary>
    public Vector2 CellCenter(GridPos p) => PointAt(p.x + 0.5f, p.y + 0.5f);

    float ScaleAtCell(GridPos p) => ScaleAtT(p.y + 0.5f);

    /// <summary>站立点：蛋、水晶、琦琦"踩"着的位置。</summary>
    Vector2 StandPoint(GridPos p)
    {
        float s = ScaleAtCell(p);
        return CellCenter(p) + new Vector2(0f, -StandDrop * m_H * s);
    }

    /// <summary>屏幕局部 y 坐标处的缩放（用来在滑动过程中让蛋和琦琦随远近变大变小）。</summary>
    float ScaleAtLocalY(float y)
    {
        float t = Mathf.Clamp(TAtHeight(y + m_Htot * 0.5f), 0f, m_Size);
        return ScaleAtT(t);
    }

    // ====================================================================
    //  构建
    // ====================================================================
    bool m_ForceClosed;       // 通关时：不管琦琦离得多近，所有装着蛋的展示盒都盖上

    /// <summary>
    /// 通关演出：让所有装着蛋的展示盒（包括刚刚放进最后一个蛋的那个）都升起盖好。
    /// 返回这段动画大约要多少秒，调用者等这么久再弹出结算。
    /// </summary>
    public float PlayCompletion()
    {
        m_ForceClosed = true;
        bool anyRising = false;
        foreach (var g in m_GoalItems)
        {
            bool hasEgg = m_Game != null && m_Game.BoxIndexAt(g.pos) >= 0;
            if (hasEgg && !g.closed)
            {
                g.closed = true;
                anyRising = true;
            }
        }
        return anyRising ? CaseRiseTime + 0.12f : 0.12f;
    }

    // ====================================================================
    //  失败演出：卡死的蛋弹跳 + 魔法光效
    // ====================================================================
    /// <summary>
    /// 让所有被卡死的蛋连跳几下，周围冒出一圈紫色的魔法光（光晕、扩散的光环、盘旋上升的星光）。
    /// 返回演出时长（秒），调用者等这么久再弹失败窗口。
    /// </summary>
    public float PlayFailure()
    {
        ClearFailFx();
        if (m_Game == null || m_Fx == null)
            return 0.2f;

        var additive = MenuAmbientFx.GetAdditiveMaterial();
        foreach (int index in m_Game.StuckBoxIndices())
        {
            if (index < 0 || index >= m_BoxItems.Count)
                continue;

            var fx = new FailFx { egg = m_BoxItems[index] };
            fx.aura = NewFxImage("FailAura", GlowPulse.GetGlowSprite(), additive);
            fx.rings = new[] { NewFxImage("FailRing", GetRingSprite(), additive), NewFxImage("FailRing", GetRingSprite(), additive) };
            fx.sparks = new Image[FailSparkCount];
            fx.sparkAngle = new float[FailSparkCount];
            for (int i = 0; i < FailSparkCount; i++)
            {
                fx.sparks[i] = NewFxImage("FailSpark", GlowPulse.GetGlowSprite(), additive);
                fx.sparkAngle[i] = i * (Mathf.PI * 2f / FailSparkCount) + (i % 3) * 0.4f;
            }
            m_FailFx.Add(fx);
        }
        return m_FailFx.Count > 0 ? FailDuration : 0.2f;
    }

    Image NewFxImage(string name, Sprite sprite, Material material)
    {
        var img = WorkshopUI.NewImage(m_Fx, name, sprite, Vector2.zero, Vector2.one * 10f);
        img.raycastTarget = false;
        if (material != null)
            img.material = material;
        img.color = new Color(1f, 1f, 1f, 0f);
        return img;
    }

    /// <summary>撤回 / 重开时立刻停掉失败演出。</summary>
    public void StopFailure() => ClearFailFx();

    void ClearFailFx()
    {
        foreach (var fx in m_FailFx)
        {
            if (fx.aura != null) UnityEngine.Object.Destroy(fx.aura.gameObject);
            foreach (var r in fx.rings) if (r != null) UnityEngine.Object.Destroy(r.gameObject);
            foreach (var sp in fx.sparks) if (sp != null) UnityEngine.Object.Destroy(sp.gameObject);
        }
        m_FailFx.Clear();
        foreach (var item in m_BoxItems)
        {
            item.bounceY = 0f;
            item.squash = Vector2.one;
            if (item.image != null)
                item.image.color = Color.white;
        }
    }

    static readonly Color FxViolet = new Color(0.72f, 0.38f, 1f);
    static readonly Color FxPink = new Color(1f, 0.55f, 0.85f);
    static readonly Color FxGold = new Color(1f, 0.88f, 0.45f);

    void UpdateFailFx(float dt)
    {
        if (m_FailFx.Count == 0)
            return;

        for (int n = m_FailFx.Count - 1; n >= 0; n--)
        {
            var fx = m_FailFx[n];
            fx.t += dt;
            float u = Mathf.Clamp01(fx.t / FailDuration);
            var egg = fx.egg;
            if (egg == null || egg.rect == null)
            {
                m_FailFx.RemoveAt(n);
                continue;
            }

            float s = ScaleAtLocalY(egg.pos.y - EggRise * m_U * 0.9f);
            float unit = m_U * s;
            var ground = egg.pos - new Vector2(0f, EggRise * unit);       // 蛋脚下的地面点
            var center = egg.pos + new Vector2(0f, 0.1f * unit);

            // ---- 蛋：连跳三下，一次比一次矮；落地压扁、起跳拉长，同时微微泛紫 ----
            float wave = Mathf.Abs(Mathf.Sin(u * Mathf.PI * 3f));
            float decay = Mathf.Pow(1f - u, 0.8f);
            egg.bounceY = wave * 0.5f * unit * decay;
            float stretch = Mathf.Clamp01(wave * 2.2f);                     // 0 = 贴地（压扁），1 = 在空中（拉长）
            float sy = Mathf.Lerp(0.8f, 1.08f, stretch);
            float sx = Mathf.Lerp(1.16f, 0.95f, stretch);
            float settle = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.85f, 1f, u));   // 最后回到正常
            egg.squash = new Vector2(Mathf.Lerp(sx, 1f, settle), Mathf.Lerp(sy, 1f, settle));
            float tint = Mathf.Sin(u * Mathf.PI) * (0.55f + 0.2f * Mathf.Sin(fx.t * 18f));
            egg.image.color = Color.Lerp(Color.white, new Color(0.78f, 0.55f, 1f), tint);

            // ---- 光晕：渐亮、脉动、渐隐 ----
            float fade = Mathf.Clamp01(u / 0.12f) * Mathf.Clamp01((1f - u) / 0.25f);
            float pulse = 0.85f + 0.15f * Mathf.Sin(fx.t * 14f);
            fx.aura.rectTransform.anchoredPosition = center;
            fx.aura.rectTransform.sizeDelta = new Vector2(2.3f, 2.3f) * unit * pulse;
            var ac = FxViolet; ac.a = 0.75f * fade;
            fx.aura.color = ac;

            // ---- 光环：从脚下往外扩散，压扁成椭圆贴着地面，错开出现两圈 ----
            for (int i = 0; i < fx.rings.Length; i++)
            {
                float q = Mathf.Clamp01((fx.t - i * 0.32f) / 0.75f);
                var ring = fx.rings[i];
                if (q <= 0f || q >= 1f)
                {
                    ring.color = new Color(1f, 1f, 1f, 0f);
                    continue;
                }
                float size = Mathf.Lerp(0.6f, 2.6f, 1f - (1f - q) * (1f - q)) * unit;
                ring.rectTransform.anchoredPosition = ground + new Vector2(0f, 0.05f * unit);
                ring.rectTransform.sizeDelta = new Vector2(size, size * 0.5f);
                var rc = i == 0 ? FxViolet : FxPink; rc.a = 0.9f * (1f - q);
                ring.color = rc;
            }

            // ---- 星光：绕着蛋盘旋、一边往上飘一边变淡 ----
            for (int i = 0; i < fx.sparks.Length; i++)
            {
                float q = Mathf.Repeat(fx.t * 1.1f + i / (float)fx.sparks.Length, 1f);
                float angle = fx.sparkAngle[i] + q * 2.6f;
                float radius = Mathf.Lerp(0.35f, 0.95f, q) * unit;
                var pos = center + new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * 0.55f + q * 0.7f * unit - 0.1f * unit);
                float twinkle = 0.7f + 0.3f * Mathf.Sin(fx.t * 20f + i * 2.1f);
                float size = (0.10f + 0.10f * Mathf.Sin(q * Mathf.PI)) * unit;
                var spark = fx.sparks[i];
                spark.rectTransform.anchoredPosition = pos;
                spark.rectTransform.sizeDelta = new Vector2(size, size);
                var sc = (i % 3 == 0) ? FxGold : (i % 3 == 1 ? FxPink : FxViolet);
                sc.a = Mathf.Sin(q * Mathf.PI) * twinkle * fade * 1.4f;
                spark.color = sc;
            }

            // ---- 演出结束：收拾干净 ----
            if (fx.t >= FailDuration)
            {
                egg.bounceY = 0f;
                egg.squash = Vector2.one;
                egg.image.color = Color.white;
                if (fx.aura != null) UnityEngine.Object.Destroy(fx.aura.gameObject);
                foreach (var r in fx.rings) if (r != null) UnityEngine.Object.Destroy(r.gameObject);
                foreach (var sp in fx.sparks) if (sp != null) UnityEngine.Object.Destroy(sp.gameObject);
                m_FailFx.RemoveAt(n);
            }
        }
    }

    /// <summary>程序生成的细光环（白色，边缘柔和），扩散特效用。</summary>
    static Sprite GetRingSprite()
    {
        if (s_RingSprite != null)
            return s_RingSprite;

        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        float half = (size - 1) * 0.5f;
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt(Mathf.Pow((x - half) / half, 2f) + Mathf.Pow((y - half) / half, 2f));
                float a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.86f) / 0.11f);
                a = a * a * (3f - 2f * a);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        s_RingSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return s_RingSprite;
    }

    /// <summary>重建前先清掉上一次生成的所有东西。</summary>
    void ClearBuilt()
    {
        foreach (var rt in new[] { m_Tiles, m_Marks, m_Goals, m_Entities, m_Fx })
            if (rt != null)
                UnityEngine.Object.Destroy(rt.gameObject);
        if (m_Hover != null)
            UnityEngine.Object.Destroy(m_Hover.gameObject);
        m_Tiles = m_Marks = m_Goals = m_Entities = null;
        m_Hover = null;
        m_HoverCell = new GridPos(-1, -1);
        m_Fx = null;
        m_FailFx.Clear();
        m_GoalItems.Clear();
        m_GoalMarks.Clear();
        m_EggMarks.Clear();
        m_BoxItems.Clear();
        m_WallItems.Clear();
        m_SnakeItem = null;
        m_SnakeShadow = null;
    }

    /// <param name="boardWidth">最近一行的棋盘总宽度（像素）。</param>
    public void Build(int size, float boardWidth)
    {
        m_Root = (RectTransform)transform;
        ClearBuilt();
        m_BoardWidth = boardWidth;
        m_Size = size;
        m_W = boardWidth / size;
        m_U = Mathf.Min(m_W, (UnitOverhang + 0.42f * m_W) / 1.23f);
        m_H = m_W * CellAspect;
        m_Htot = HeightAtT(size);
        m_Root.sizeDelta = new Vector2(boardWidth, m_Htot);

        m_SnakeSprite = WorkshopUI.LoadSprite("Board/snake_token");
        m_TurnSprites = new Sprite[3];
        for (int i = 0; i < 3; i++)
        {
            m_TurnSprites[i] = Resources.Load<Sprite>("Art/Qiqi/turn_" + (i + 1));
            if (m_TurnSprites[i] == null)
            {
                Debug.LogWarning("找不到 Art/Qiqi/turn_" + (i + 1) + "，琦琦改用静态图。");
                m_TurnSprites[i] = m_SnakeSprite;
            }
        }

        m_Tiles = WorkshopUI.NewStretch(m_Root, "Tiles");
        m_Marks = WorkshopUI.NewStretch(m_Root, "Marks");    // 展示盒 / 蛋所在格子的底部高光，压在格线上面、底座下面
        m_Goals = WorkshopUI.NewStretch(m_Root, "Goals");
        m_Entities = WorkshopUI.NewStretch(m_Root, "Entities");
        m_Fx = WorkshopUI.NewStretch(m_Root, "Fx");          // 魔法光效：盖在所有东西上面

        // 棋盘本身不填色、完全透明，只画白色格线（外圈和内部线条一样粗）
        var lines = WorkshopUI.NewStretch(m_Tiles, "GridLines");
        for (int i = 0; i <= size; i++)
        {
            NewLine(lines, PointAt(0f, i), PointAt(size, i), LineWidth, LineColor);
            NewLine(lines, PointAt(i, 0f), PointAt(i, size), LineWidth, LineColor);
        }

        m_Hover = WorkshopUI.NewImage(m_Root, "Hover", null, Vector2.zero, new Vector2(m_W, m_H));
        m_Hover.color = new Color(1f, 0.95f, 0.7f, 0.38f);
        m_Hover.gameObject.SetActive(false);
    }

    /// <summary>
    /// 画一条线：从 a 到 b，粗细 width（像素，最近一行的粗细；远处按透视变细）。
    /// 用一个转了角度的细长矩形来画，所以竖线也能带着透视微微倾斜。
    /// </summary>
    void NewLine(Transform parent, Vector2 a, Vector2 b, float width, Color color)
    {
        Vector2 delta = b - a;
        float length = delta.magnitude;
        float thickness = width * ScaleAtLocalY((a.y + b.y) * 0.5f);
        // 两头各多出半个线宽，转角处才不会缺一小块
        var img = WorkshopUI.NewImage(parent, "Line", null, (a + b) * 0.5f, new Vector2(length + thickness, thickness));
        img.color = color;
        img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
    }

    // ====================================================================
    //  同步画面
    // ====================================================================
    public void SetGame(SokobanGame game)
    {
        if (game != null && game.Size != m_Size)
            Build(game.Size, m_BoardWidth);      // 棋盘大小变了（3×3 ~ 9×9）：整块重建
        m_Game = game;
        RestLift = 0f;
        EggLifts = null;
        SlideSpeed = 1f;
        m_ForceClosed = false;
        ClearFailFx();
        ResetFacing();
        Refresh(true);
    }

    /// <summary>
    /// 让琦琦转向 dir：向下 = 正面，向上 = 背面，向左 = 侧面，向右 = 侧面翻转。
    /// 方向变化时会依次播放中间的转身帧（例如 正面 → 侧面 → 背面），每帧 0.065 秒。
    /// </summary>
    public void FaceDirection(GridPos dir)
    {
        SnakeFrame target;
        if (dir.y > 0) target = new SnakeFrame { pose = Pose.Back, flip = 1f };
        else if (dir.y < 0) target = new SnakeFrame { pose = Pose.Front, flip = 1f };
        else if (dir.x < 0) target = new SnakeFrame { pose = Pose.Side, flip = 1f };
        else if (dir.x > 0) target = new SnakeFrame { pose = Pose.Side, flip = -1f };
        else return;

        if (target.pose == m_End.pose && (target.pose != Pose.Side || target.flip == m_End.flip))
            return;   // 已经（或即将）朝着这个方向了

        bool wasIdle = m_FrameQueue.Count == 0;

        // 正面 <-> 背面：从侧面转过去；左 <-> 右：从正面转过去
        bool frontBack = (m_End.pose == Pose.Front && target.pose == Pose.Back)
                         || (m_End.pose == Pose.Back && target.pose == Pose.Front);
        if (frontBack)
            m_FrameQueue.Enqueue(new SnakeFrame { pose = Pose.Side, flip = m_SideFlip });
        else if (m_End.pose == Pose.Side && target.pose == Pose.Side)
            m_FrameQueue.Enqueue(new SnakeFrame { pose = Pose.Front, flip = 1f });

        m_FrameQueue.Enqueue(target);
        m_End = target;
        if (target.pose == Pose.Side)
            m_SideFlip = target.flip;

        if (wasIdle)
        {
            ShowFrame(m_FrameQueue.Dequeue());   // 第一帧立刻显示，不拖泥带水
            m_FrameTimer = 0f;
        }
    }

    void ShowFrame(SnakeFrame frame)
    {
        m_Shown = frame;
        if (m_SnakeItem != null)
            m_SnakeItem.image.sprite = m_TurnSprites[(int)frame.pose];
    }

    /// <summary>不播动画，直接面朝下（切换模式 / 重开时用）。</summary>
    void ResetFacing()
    {
        m_FrameQueue.Clear();
        m_Shown = new SnakeFrame { pose = Pose.Front, flip = 1f };
        m_End = m_Shown;
        m_SideFlip = 1f;
        m_FrameTimer = 0f;
        if (m_SnakeItem != null)
            m_SnakeItem.image.sprite = m_TurnSprites[0];
    }

    /// <param name="snap">true = 所有东西立刻到位（编辑、重开）；false = 平滑滑过去（走一步）。</param>
    public void Refresh(bool snap)
    {
        if (m_Game == null)
            return;

        // 展示盒（目标）
        while (m_GoalItems.Count < m_Game.Goals.Count)
            m_GoalItems.Add(NewGoalItem());
        while (m_GoalItems.Count > m_Game.Goals.Count)
        {
            var last = m_GoalItems[m_GoalItems.Count - 1];
            UnityEngine.Object.Destroy(last.rect.gameObject);
            UnityEngine.Object.Destroy(last.backRoot.gameObject);
            UnityEngine.Object.Destroy(last.frontRoot.gameObject);
            m_GoalItems.RemoveAt(m_GoalItems.Count - 1);
        }
        for (int i = 0; i < m_GoalItems.Count; i++)
        {
            var g = m_GoalItems[i];
            var pos = m_Game.Goals[i];
            g.pos = pos;
            float s = ScaleAtCell(pos);
            var stand = StandPoint(pos);

            // 底座（连同发光）站在站立点上
            g.rect.anchoredPosition = stand;
            g.rect.localScale = new Vector3(s, s, 1f);

            // 玻璃盒：容器的底边 = 盒子底边，地板中心正好落在站立点
            var casePos = stand + new Vector2(0f, -CaseFloorRise * m_U * s);
            g.backRoot.anchoredPosition = casePos;
            g.frontRoot.anchoredPosition = casePos;
            g.backRoot.localScale = g.frontRoot.localScale = new Vector3(s, s, 1f);

            PlaceFloorMark(EnsureMark(m_GoalMarks, i, "GoalMark", FloorGoalColor, 0.62f, 0.14f, i * 0.9f),
                stand, s, 1.38f, 0.78f);

            // 展示盒里有蛋 = “已填充”：底下亮起奶油色光晕（和格子底部高光同一色系）
            bool hasEgg = m_Game.BoxIndexAt(pos) >= 0;
            g.glow.gameObject.SetActive(hasEgg);

            // 感应门：有蛋的展示盒，琦琦走远了就盖上盒子；琦琦靠近（1 格内）就打开，露出底座，方便把蛋推出来
            int dist = Mathf.Abs(pos.x - m_Game.Snake.x) + Mathf.Abs(pos.y - m_Game.Snake.y);
            g.closed = hasEgg && (m_ForceClosed || dist > NearDistance);
            if (snap)
            {
                g.t = g.closed ? 1f : 0f;
                ApplyCase(g);
            }
        }
        while (m_GoalMarks.Count > m_GoalItems.Count)
        {
            UnityEngine.Object.Destroy(m_GoalMarks[m_GoalMarks.Count - 1].gameObject);
            m_GoalMarks.RemoveAt(m_GoalMarks.Count - 1);
        }

        // 水晶墙（不会动，位置直接到位）
        while (m_WallItems.Count < m_Game.Walls.Count)
            m_WallItems.Add(NewItem("Crystal", SkinLibrary.Crystal(0), CrystalScale));
        while (m_WallItems.Count > m_Game.Walls.Count)
        {
            UnityEngine.Object.Destroy(m_WallItems[m_WallItems.Count - 1].rect.gameObject);
            m_WallItems.RemoveAt(m_WallItems.Count - 1);
        }
        for (int i = 0; i < m_WallItems.Count; i++)
        {
            var item = m_WallItems[i];
            var pos = m_Game.Walls[i];
            float s = ScaleAtCell(pos);
            item.image.sprite = SkinLibrary.Crystal(m_Game.WallKinds[i]);
            item.rect.anchoredPosition = StandPoint(pos) + new Vector2(0f, CrystalRise * m_U * s);
            item.rect.localScale = new Vector3(s, s, 1f);
        }

        // 鸟蛋
        while (m_BoxItems.Count < m_Game.Boxes.Count)
            m_BoxItems.Add(NewItem("Egg", SkinLibrary.Egg(0), EggScale));
        while (m_BoxItems.Count > m_Game.Boxes.Count)
        {
            UnityEngine.Object.Destroy(m_BoxItems[m_BoxItems.Count - 1].rect.gameObject);
            m_BoxItems.RemoveAt(m_BoxItems.Count - 1);
        }
        for (int i = 0; i < m_BoxItems.Count; i++)
        {
            var item = m_BoxItems[i];
            var pos = m_Game.Boxes[i];
            item.image.sprite = SkinLibrary.Egg(m_Game.BoxKinds[i]);
            item.target = StandPoint(pos) + new Vector2(0f, EggRise * m_U * ScaleAtCell(pos));
            if (snap || item.fresh)
            {
                item.pos = item.target;
                item.rect.anchoredPosition = item.target;
                float s = ScaleAtCell(pos);
                item.rect.localScale = new Vector3(s, s, 1f);
            }
            item.fresh = false;
        }
        while (m_EggMarks.Count < m_BoxItems.Count)
            EnsureMark(m_EggMarks, m_EggMarks.Count, "EggMark", FloorEggColor, 0.50f, 0.12f, m_EggMarks.Count * 1.1f);
        while (m_EggMarks.Count > m_BoxItems.Count)
        {
            UnityEngine.Object.Destroy(m_EggMarks[m_EggMarks.Count - 1].gameObject);
            m_EggMarks.RemoveAt(m_EggMarks.Count - 1);
        }
        UpdateEggMarks();

        // 琦琦
        if (m_SnakeItem == null)
        {
            // 脚下的影子：压在所有东西最下面，琦琦跳起时影子会缩小
            m_SnakeShadow = WorkshopUI.NewImage(m_Entities, "QiqiShadow", GlowPulse.GetGlowSprite(), Vector2.zero,
                new Vector2(m_U * 0.95f, m_U * 0.24f));
            m_SnakeShadow.color = new Color(0.12f, 0.06f, 0.02f, 0.38f);
            m_SnakeShadow.rectTransform.SetAsFirstSibling();

            m_SnakeItem = NewItem("Qiqi", m_TurnSprites[0], SnakeScale);
        }
        m_SnakeItem.target = StandPoint(m_Game.Snake) + new Vector2(0f, SnakeRise * m_U * ScaleAtCell(m_Game.Snake));
        if (snap || m_SnakeItem.fresh)
            m_SnakePos = m_SnakeItem.target;
        m_SnakeItem.fresh = false;

        SortEntities();
    }

    /// <summary>
    /// 按"离镜头远近"排前后：行数越靠上（y 越大）的越靠后，先画；同一行里 水晶 < 蛋 < 琦琦。
    /// 这样琦琦走到水晶/蛋后面时会被挡住一点，走到前面时挡住它们，有层次感。影子永远最底层。
    /// </summary>
    void SortEntities()
    {
        m_SortBuffer.Clear();
        for (int i = 0; i < m_Game.Walls.Count && i < m_WallItems.Count; i++)
            m_SortBuffer.Add(new KeyValuePair<float, RectTransform>(-m_Game.Walls[i].y * 10f, m_WallItems[i].rect));
        for (int i = 0; i < m_Game.Boxes.Count && i < m_BoxItems.Count; i++)
            m_SortBuffer.Add(new KeyValuePair<float, RectTransform>(-m_Game.Boxes[i].y * 10f + 1f, m_BoxItems[i].rect));
        // 玻璃盒后层在同一行的蛋下面、前层在蛋上面（蛋在盒子里面），但都还在琦琦后面
        for (int i = 0; i < m_GoalItems.Count; i++)
        {
            m_SortBuffer.Add(new KeyValuePair<float, RectTransform>(-m_GoalItems[i].pos.y * 10f + 0.5f, m_GoalItems[i].backRoot));
            m_SortBuffer.Add(new KeyValuePair<float, RectTransform>(-m_GoalItems[i].pos.y * 10f + 1.5f, m_GoalItems[i].frontRoot));
        }
        m_SortBuffer.Add(new KeyValuePair<float, RectTransform>(-m_Game.Snake.y * 10f + 2f, m_SnakeItem.rect));

        m_SortBuffer.Sort((a, b) => a.Key.CompareTo(b.Key));
        for (int i = 0; i < m_SortBuffer.Count; i++)
            m_SortBuffer[i].Value.SetSiblingIndex(i + 1);     // 0 号位留给影子
    }

    Item NewItem(string name, Sprite sprite, float widthScale)
    {
        var img = WorkshopUI.NewImage(m_Entities, name, sprite, Vector2.zero, new Vector2(m_U * widthScale, m_U * widthScale));
        return new Item { rect = img.rectTransform, image = img };
    }

    RectTransform EnsureMark(List<RectTransform> list, int index, string name, Color color, float baseA, float amp, float phase)
    {
        while (list.Count <= index)
        {
            var root = WorkshopUI.NewRect(m_Marks, name, Vector2.zero, Vector2.one);
            var img = WorkshopUI.NewImage(root, "Glow", GlowPulse.GetGlowSprite(), Vector2.zero, Vector2.one);
            img.preserveAspect = false;     // 把柔光圆压扁成贴地的椭圆
            var additive = MenuAmbientFx.GetAdditiveMaterial();
            if (additive != null)
                img.material = additive;
            float k = additive != null ? 1f : 0.7f;
            img.gameObject.AddComponent<GlowPulse>().Init(img, color, baseA * k, amp * k, 2.1f, phase, false, 0.05f);
            list.Add(root);
        }
        return list[index];
    }

    void PlaceFloorMark(RectTransform mark, Vector2 stand, float perspective, float widthScale, float heightScale)
    {
        mark.anchoredPosition = stand;
        mark.sizeDelta = new Vector2(widthScale * m_W, heightScale * m_H);
        mark.localScale = new Vector3(perspective, perspective, 1f);
        var img = mark.GetComponentInChildren<Image>();
        if (img != null)
            img.rectTransform.sizeDelta = mark.sizeDelta;
    }

    void UpdateEggMarks()
    {
        if (m_Game == null)
            return;
        for (int i = 0; i < m_EggMarks.Count && i < m_BoxItems.Count; i++)
        {
            bool onGoal = m_Game.HasGoalAt(m_Game.Boxes[i]);
            var mark = m_EggMarks[i];
            if (mark.gameObject.activeSelf == onGoal)
                mark.gameObject.SetActive(!onGoal);
            if (onGoal)
                continue;
            var item = m_BoxItems[i];
            float s = ScaleAtLocalY(item.pos.y - EggRise * m_U * 0.9f);
            var floor = item.pos + new Vector2(0f, -EggRise * m_U * s);
            PlaceFloorMark(mark, floor, s, 1.18f, 0.62f);
        }
    }

    GoalItem NewGoalItem()
    {
        var root = WorkshopUI.NewRect(m_Goals, "Goal", Vector2.zero, new Vector2(m_U, m_U));

        // 底座（box_mark）：保持图片比例，宽度约一格；中心就是站立点
        var padSprite = SkinLibrary.DisplayPad();
        float padW = m_U * PadWidth;
        float padH = padSprite != null ? padW * padSprite.rect.height / padSprite.rect.width : padW * 0.5f;
        WorkshopUI.NewImage(root, "Pad", padSprite, Vector2.zero, new Vector2(padW, padH));

        // 发光：用叠加混合，亮度随时间呼吸（压扁一点，贴着地面）
        var glow = WorkshopUI.NewImage(root, "Glow", GlowPulse.GetGlowSprite(), Vector2.zero,
            new Vector2(m_U * 2.1f, m_U * 1.3f));
        var additive = MenuAmbientFx.GetAdditiveMaterial();
        if (additive != null)
            glow.material = additive;
        float k = additive != null ? 1f : 0.6f;
        glow.gameObject.AddComponent<GlowPulse>().Init(glow, FloorGoalColor,
            0.55f * k, 0.18f * k, 3f, m_GoalItems.Count * 1.3f, false, 0.05f);
        glow.gameObject.SetActive(false);

        // 玻璃盒（box）：前后两层都放在实体层，和蛋、琦琦一起按前后排序。
        var item = new GoalItem { rect = root, glow = glow };
        NewCaseLayer("DisplayCaseBack", SkinLibrary.DisplayCaseBack(), out item.backRoot, out item.backImage);
        NewCaseLayer("DisplayCaseFront", SkinLibrary.DisplayCaseFront(), out item.frontRoot, out item.frontImage);
        return item;
    }

    void NewCaseLayer(string name, Sprite sprite, out RectTransform root, out RectTransform image)
    {
        float caseW = m_U * CaseWidth;
        float caseH = sprite != null ? caseW * sprite.rect.height / sprite.rect.width : caseW * 1.5f;
        root = WorkshopUI.NewRect(m_Entities, name, Vector2.zero, new Vector2(caseW, caseH * 1.25f));
        root.pivot = new Vector2(0.5f, 0f);
        root.gameObject.AddComponent<RectMask2D>();     // 裁掉底边以下的部分：盒子像是从底座里推出来
        var img = WorkshopUI.NewImage(root, "Image", sprite, Vector2.zero, new Vector2(caseW, caseH));
        image = img.rectTransform;
        image.anchorMin = image.anchorMax = new Vector2(0.5f, 0f);
        image.pivot = new Vector2(0.5f, 0f);
        image.anchoredPosition = new Vector2(0f, -caseH);
        root.gameObject.SetActive(false);
    }

    /// <summary>
    /// 玻璃盒的升降：升起时带一点回弹（先冲过头再落回，像"咔"地盖好），落下时平滑沉回底座。
    /// </summary>
    void AnimateCase(GoalItem g, float dt)
    {
        float target = g.closed ? 1f : 0f;
        if (!Mathf.Approximately(g.t, target))
            g.t = Mathf.MoveTowards(g.t, target, dt / (g.closed ? CaseRiseTime : CaseLowerTime));
        ApplyCase(g);
    }

    void ApplyCase(GoalItem g)
    {
        g.shown = g.closed ? EaseOutBack(g.t) : Mathf.SmoothStep(0f, 1f, g.t);
        bool visible = g.shown > 0.002f || g.t > 0f;
        if (g.backRoot.gameObject.activeSelf != visible)
        {
            g.backRoot.gameObject.SetActive(visible);
            g.frontRoot.gameObject.SetActive(visible);
        }
        if (visible)
        {
            float caseH = g.backRoot.sizeDelta.y / 1.25f;
            var pos = new Vector2(0f, (g.shown - 1f) * caseH);
            g.backImage.anchoredPosition = pos;
            g.frontImage.anchoredPosition = pos;
        }
    }

    static float EaseOutBack(float t)
    {
        const float c1 = 1.9f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    // ====================================================================
    //  每帧：滑动、呼吸、悬停高亮
    // ====================================================================
    void Update()
    {
        float k = 1f - Mathf.Exp(-SlideSharpness * Mathf.Max(0.15f, SlideSpeed) * Time.unscaledDeltaTime);

        // 入场演出：其余部分整体抬高（实体层里的蛋要把这段抬高减回去，再加上自己的）
        var restShift = new Vector2(0f, RestLift);
        if (m_Tiles != null) m_Tiles.anchoredPosition = restShift;
        if (m_Marks != null) m_Marks.anchoredPosition = restShift;
        if (m_Goals != null) m_Goals.anchoredPosition = restShift;
        if (m_Entities != null) m_Entities.anchoredPosition = restShift;

        for (int i = 0; i < m_BoxItems.Count; i++)
        {
            var item = m_BoxItems[i];
            item.pos = Vector2.Lerp(item.pos, item.target, k);
            // 滑动时跟着远近变大变小（减掉蛋图中心比站立点高的那部分，才是它脚下的位置）
            float s = ScaleAtLocalY(item.pos.y - EggRise * m_U * 0.9f);

            // 轻轻左右摇摆：绕蛋的底部转，所以把中心点跟着挪一下，脚不会离地
            float ang = SwayDegrees * Mathf.Sin(Time.unscaledTime * SwaySpeed + i * 1.37f);
            float rad = ang * Mathf.Deg2Rad;
            float r = EggRise * m_U * s;
            var offset = new Vector2(-r * Mathf.Sin(rad), r * (Mathf.Cos(rad) - 1f));
            offset.y -= (1f - item.squash.y) * r;          // 压扁时也让底部贴着地面
            float eggLift = EggLifts != null && i < EggLifts.Length ? EggLifts[i] : 0f;
            item.rect.anchoredPosition = item.pos + offset + new Vector2(0f, item.bounceY + eggLift - RestLift);
            item.rect.localRotation = Quaternion.Euler(0f, 0f, ang);
            item.rect.localScale = new Vector3(s * item.squash.x, s * item.squash.y, 1f);
        }
        UpdateEggMarks();
        UpdateFailFx(Time.unscaledDeltaTime);

        foreach (var g in m_GoalItems)
            AnimateCase(g, Time.unscaledDeltaTime);

        if (m_SnakeItem != null)
        {
            // 转身：按帧顺序播放中间的朝向图
            if (m_FrameQueue.Count > 0)
            {
                m_FrameTimer += Time.unscaledDeltaTime;
                if (m_FrameTimer >= TurnFrameTime)
                {
                    m_FrameTimer = 0f;
                    ShowFrame(m_FrameQueue.Dequeue());
                }
            }

            // 平滑滑到目标格；滑动过程中像小跳一样弹起一道弧线（起点和终点高度为 0）
            m_SnakePos = Vector2.Lerp(m_SnakePos, m_SnakeItem.target, k);
            float s = ScaleAtLocalY(m_SnakePos.y - SnakeRise * m_U * 0.9f);
            float remaining = Mathf.Clamp01((m_SnakeItem.target - m_SnakePos).magnitude / m_W);
            float hop = Mathf.Sin(remaining * Mathf.PI) * HopHeight * m_U * s;
            m_SnakeItem.rect.anchoredPosition = m_SnakePos + new Vector2(0f, hop);
            if (m_SnakeShadow != null)
            {
                m_SnakeShadow.rectTransform.anchoredPosition = m_SnakePos + new Vector2(0f, -SnakeRise * m_U * s + 0.02f * m_U * s);
                float shrink = s * (1f - 0.25f * (hop / (HopHeight * m_U * s + 0.0001f)));
                m_SnakeShadow.rectTransform.localScale = new Vector3(shrink, shrink, 1f);
            }

            // 待机时轻轻"呼吸"；翻转（朝右）靠 x 方向取负
            float breath = 1f + 0.025f * Mathf.Sin(Time.unscaledTime * 2.4f);
            m_SnakeItem.rect.localScale = new Vector3(m_Shown.flip * (2f - breath) * s, breath * s, 1f);
        }

        UpdateHover();
    }

    void UpdateHover()
    {
        if (m_Hover == null)
            return;

        bool show = false;
        if (InputEnabled && PointToCell(Input.mousePosition, null, out var cell))
        {
            show = true;
            if (cell != m_HoverCell)
            {
                m_HoverCell = cell;
                float tm = cell.y + 0.5f;
                var left = PointAt(cell.x, tm);
                var right = PointAt(cell.x + 1f, tm);
                float y0 = HeightAtT(cell.y), y1 = HeightAtT(cell.y + 1f);
                m_Hover.rectTransform.anchoredPosition = new Vector2((left.x + right.x) * 0.5f, (y0 + y1) * 0.5f - m_Htot * 0.5f);
                m_Hover.rectTransform.sizeDelta = new Vector2(right.x - left.x, y1 - y0);
            }
        }
        if (m_Hover.gameObject.activeSelf != show)
            m_Hover.gameObject.SetActive(show);
    }

    // ====================================================================
    //  鼠标输入
    // ====================================================================
    public void OnPointerDown(PointerEventData eventData)
    {
        if (!InputEnabled || eventData.button != PointerEventData.InputButton.Left)
            return;

        if (PointToCell(eventData.position, eventData.pressEventCamera, out var cell))
        {
            m_LastDragCell = cell;
            CellPressed?.Invoke(cell, true);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!InputEnabled || eventData.button != PointerEventData.InputButton.Left)
            return;

        if (PointToCell(eventData.position, eventData.pressEventCamera, out var cell) && cell != m_LastDragCell)
        {
            m_LastDragCell = cell;
            CellPressed?.Invoke(cell, false);
        }
    }

    /// <summary>屏幕点 → 格子（要反过来算透视：先由高度求出是第几行，再由这一行的宽度求出是第几列）。</summary>
    bool PointToCell(Vector2 screenPos, Camera cam, out GridPos cell)
    {
        cell = default;
        if (m_Root == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(m_Root, screenPos, cam, out var local))
            return false;

        float h = local.y + m_Htot * 0.5f;
        if (h < 0f || h >= m_Htot)
            return false;

        float t = TAtHeight(h);
        int y = Mathf.FloorToInt(t);
        float gx = local.x / (m_W * ScaleAtT(t)) + m_Size * 0.5f;
        int x = Mathf.FloorToInt(gx);
        if (x < 0 || y < 0 || x >= m_Size || y >= m_Size)
            return false;

        cell = new GridPos(x, y);
        return true;
    }
}
