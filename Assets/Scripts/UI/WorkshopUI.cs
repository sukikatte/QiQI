using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public struct DialogButton
{
    public string label;
    public Action onClick;

    public DialogButton(string label, Action onClick)
    {
        this.label = label;
        this.onClick = onClick;
    }
}

/// <summary>创意工坊的 UI 小工具：统一的取图、建节点、文字、弹窗。</summary>
public static class WorkshopUI
{
    public static TMP_FontAsset Font;

    public static readonly Color Ink = new Color(0.27f, 0.16f, 0.08f, 1f);        // 羊皮纸上的深褐色字
    public static readonly Color InkSoft = new Color(0.50f, 0.33f, 0.16f, 1f);    // 次要文字
    public static readonly Color Cream = new Color(1f, 0.94f, 0.78f, 1f);         // 木牌上的奶油色字

    static readonly Dictionary<string, Sprite> s_Sprites = new Dictionary<string, Sprite>();

    /// <param name="path">相对 Resources/Art/ 的路径，例如 UI/panel_parchment、Board/snake_token。</param>
    public static Sprite LoadSprite(string path)
    {
        if (s_Sprites.TryGetValue(path, out var cached) && cached != null)
            return cached;

        var sprite = Resources.Load<Sprite>("Art/" + path);
        if (sprite == null)
        {
            Debug.LogError("图片加载失败: Art/" + path + "（Texture Type 需为 Sprite (2D and UI)）");
            return null;
        }
        s_Sprites[path] = sprite;
        return sprite;
    }

    // ---------------- 从大图里裁一块当图标 ----------------
    /// <summary>
    /// 从一张已经导入的图（Resources/Art/ 下）里裁出一块矩形当 Sprite。
    /// rect 的原点在图的左上角（和看图软件里一样）。
    /// </summary>
    public static Sprite CropSprite(Sprite source, Rect rectFromTop)
    {
        if (source == null)
            return null;
        var tex = source.texture;
        var rect = new Rect(rectFromTop.x, tex.height - rectFromTop.y - rectFromTop.height, rectFromTop.width, rectFromTop.height);
        return Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
    }

    static Sprite s_DesignBar;

    /// <summary>
    /// 创意工坊顶部的设计栏：用 blank 图，把上面多余的一段绳子裁掉一部分（绳子挂到屏幕顶上去就行）。
    /// 返回的图宽 1536、高 DesignBarCropHeight（原图像素）。
    /// </summary>
    public const float DesignBarCropTop = 150f, DesignBarCropHeight = 652f;
    public static Sprite DesignBarSprite()
    {
        if (s_DesignBar != null)
            return s_DesignBar;
        var blank = Resources.Load<Sprite>("Art/UI/blank");
        if (blank == null)
        {
            Debug.LogError("图片加载失败: Art/UI/blank");
            return null;
        }
        s_DesignBar = CropSprite(blank, new Rect(0f, DesignBarCropTop, blank.texture.width, DesignBarCropHeight));
        return s_DesignBar;
    }

    // ---------------- 基础节点 ----------------
    public static RectTransform NewRect(Transform parent, string name, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var r = (RectTransform)go.transform;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = pos;
        r.sizeDelta = size;
        return r;
    }

    public static RectTransform NewStretch(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var r = (RectTransform)go.transform;
        Stretch(r);
        return r;
    }

    public static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
    }

    public static void AnchorTo(RectTransform r, Vector2 anchor, Vector2 pivot, Vector2 pos)
    {
        r.anchorMin = r.anchorMax = anchor;
        r.pivot = pivot;
        r.anchoredPosition = pos;
    }

    public static Image NewImage(Transform parent, string name, Sprite sprite, Vector2 pos, Vector2 size,
                                 bool sliced = false, bool raycast = false)
    {
        var r = NewRect(parent, name, pos, size);
        var img = r.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
        img.preserveAspect = !sliced;
        img.raycastTarget = raycast;
        return img;
    }

    public static TextMeshProUGUI NewLabel(Transform parent, string name, string text, float fontSize, Color color,
                                           Vector2 pos, Vector2 size,
                                           TextAlignmentOptions align = TextAlignmentOptions.Center, bool wrap = false)
    {
        var r = NewRect(parent, name, pos, size);
        var t = r.gameObject.AddComponent<TextMeshProUGUI>();
        if (Font != null)
            t.font = Font;
        t.text = text;
        t.fontSize = fontSize;
        t.color = color;
        t.alignment = align;
        t.enableWordWrapping = wrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        return t;
    }

    // ---------------- 弹窗 ----------------
    /// <summary>
    /// 模态弹窗：一个标题、一段说明、若干按钮。点任何按钮都会先关闭弹窗，再执行它的回调。
    /// </summary>
    public static GameObject ShowDialog(Transform canvas, string title, string message, params DialogButton[] buttons)
    {
        var overlay = NewDimmer(canvas);
        bool hasMessage = !string.IsNullOrEmpty(message);
        float height = hasMessage ? 440f : 340f;
        var panel = NewImage(overlay, "Panel", LoadSprite("UI/panel_parchment"), Vector2.zero,
            new Vector2(820f, height), true, true);

        if (hasMessage)
        {
            NewLabel(panel.transform, "Title", title, 56f, Ink, new Vector2(0f, 135f), new Vector2(740f, 80f));
            NewLabel(panel.transform, "Message", message, 34f, InkSoft, new Vector2(0f, 25f), new Vector2(700f, 150f),
                TextAlignmentOptions.Center, true);
            AddDialogButtons(panel.transform, overlay.gameObject, buttons, -140f);
        }
        else
        {
            NewLabel(panel.transform, "Title", title, 62f, Ink, new Vector2(0f, 50f), new Vector2(760f, 110f));
            AddDialogButtons(panel.transform, overlay.gameObject, buttons, -95f);
        }
        return overlay.gameObject;
    }

    /// <summary>中间飘一块羊皮纸，几秒后自己消失，不用点。</summary>
    public static void ShowAutoPopup(Transform canvas, string title, string message, float seconds = 2f)
    {
        var old = canvas.Find("AutoPopup");
        if (old != null)
            UnityEngine.Object.Destroy(old.gameObject);

        var root = NewRect(canvas, "AutoPopup", Vector2.zero, new Vector2(760f, 280f));
        root.SetAsLastSibling();
        var group = root.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        var panel = NewImage(root, "Panel", LoadSprite("UI/panel_parchment"), Vector2.zero, new Vector2(760f, 280f), true, false);
        NewLabel(panel.transform, "Title", title, 52f, Ink, new Vector2(0f, 58f), new Vector2(680f, 70f));
        NewLabel(panel.transform, "Message", message, 34f, InkSoft, new Vector2(0f, -36f), new Vector2(640f, 90f),
            TextAlignmentOptions.Center, true);
        root.gameObject.AddComponent<AutoPopup>().Play(seconds);
    }

    /// <summary>选地图大小的弹窗：3×3 ~ 9×9，点哪个就用哪个。</summary>
    public static GameObject ShowSizeDialog(Transform canvas, int minSize, int maxSize, Action<int> onPicked, Action onCancel)
    {
        var overlay = NewDimmer(canvas);
        var panel = NewImage(overlay, "Panel", LoadSprite("UI/panel_parchment"), Vector2.zero, new Vector2(860f, 520f), true, true);
        var overlayGo = overlay.gameObject;

        NewLabel(panel.transform, "Title", "选择地图大小", 56f, Ink, new Vector2(0f, 190f), new Vector2(760f, 80f));
        NewLabel(panel.transform, "Message", "地图越小，每个格子越大；总面积不变",
            30, InkSoft, new Vector2(0f, 125f), new Vector2(760f, 50f));

        const float w = 140f, h = 80f, gap = 16f, font = 38f;
        int count = maxSize - minSize + 1;
        int perRow = 5;
        for (int i = 0; i < count; i++)
        {
            int size = minSize + i;
            int row = i / perRow;
            int inRow = Mathf.Min(perRow, count - row * perRow);
            int col = i % perRow;
            float rowWidth = inRow * w + (inRow - 1) * gap;
            float x = -rowWidth / 2f + w / 2f + col * (w + gap);
            float y = 30f - row * (h + gap);
            PlaqueButton.Create(panel.transform, "Size_" + size, size + "×" + size, new Vector2(x, y), new Vector2(w, h), font, () =>
            {
                UnityEngine.Object.Destroy(overlayGo);
                onPicked?.Invoke(size);
            });
        }

        PlaqueButton.Create(panel.transform, "Cancel", "取消",
            new Vector2(0f, -170f), new Vector2(240f, 80f), 36, () =>
            {
                UnityEngine.Object.Destroy(overlayGo);
                onCancel?.Invoke();
            });
        return overlayGo;
    }

    /// <summary>带输入框的弹窗（用来给关卡起名）。回车等同于点确认按钮。</summary>
    public static GameObject ShowInputDialog(Transform canvas, string title, string message, string defaultText,
                                             string confirmLabel, Action<string> onConfirm, Action onCancel)
    {
        var overlay = NewDimmer(canvas);
        var panel = NewImage(overlay, "Panel", LoadSprite("UI/panel_parchment"), Vector2.zero, new Vector2(820f, 500f), true, true);

        NewLabel(panel.transform, "Title", title, 56f, Ink, new Vector2(0f, 170f), new Vector2(740f, 80f));
        NewLabel(panel.transform, "Message", message, 32, InkSoft, new Vector2(0f, 105f), new Vector2(700f, 50f));

        var input = NewInputField(panel.transform, new Vector2(0f, 20f), new Vector2(620f, 78f),
            defaultText, "关卡名称");

        var overlayGo = overlay.gameObject;
        Action confirm = () =>
        {
            string value = input != null ? input.text : defaultText;
            UnityEngine.Object.Destroy(overlayGo);
            onConfirm?.Invoke(value);
        };
        Action cancel = () =>
        {
            UnityEngine.Object.Destroy(overlayGo);
            onCancel?.Invoke();
        };

        const float btnY = -150f, btnW = 250f, btnH = 80f, btnGap = 30f, btnFont = 36f;
        PlaqueButton.Create(panel.transform, "Confirm", confirmLabel, new Vector2(-(btnW + btnGap) * 0.5f, btnY), new Vector2(btnW, btnH), btnFont, confirm);
        PlaqueButton.Create(panel.transform, "Cancel", "取消", new Vector2((btnW + btnGap) * 0.5f, btnY), new Vector2(btnW, btnH), btnFont, cancel);

        if (input != null)
        {
            input.onSubmit.AddListener(_ => confirm());
            input.Select();
            input.ActivateInputField();
        }
        return overlayGo;
    }

    public static RectTransform NewDimmer(Transform canvas)
    {
        // 做得足够大，不管窗口什么比例，遮罩都能盖住整个屏幕（舞台本身可能比屏幕小）
        var overlay = NewRect(canvas, "Dialog", Vector2.zero, new Vector2(8000f, 8000f));
        var dim = overlay.gameObject.AddComponent<Image>();
        dim.color = new Color(0.05f, 0.03f, 0.02f, 0.62f);
        dim.raycastTarget = true;     // 挡住下面的点击
        overlay.SetAsLastSibling();
        return overlay;
    }

    static void AddDialogButtons(Transform panel, GameObject overlay, DialogButton[] buttons, float y)
    {
        if (buttons == null || buttons.Length == 0)
            return;

        const float width = 250f, height = 80f, gap = 30f, font = 36f;
        float total = buttons.Length * width + (buttons.Length - 1) * gap;
        for (int i = 0; i < buttons.Length; i++)
        {
            var b = buttons[i];
            float x = -total / 2f + width / 2f + i * (width + gap);
            PlaqueButton.Create(panel, "Button_" + i, b.label, new Vector2(x, y), new Vector2(width, height), font, () =>
            {
                UnityEngine.Object.Destroy(overlay);
                b.onClick?.Invoke();
            });
        }
    }

    static TMP_InputField NewInputField(Transform parent, Vector2 pos, Vector2 size, string text, string placeholder)
    {
        var r = NewRect(parent, "Input", pos, size);
        r.gameObject.SetActive(false);    // 先关着，配置完再打开，避免组件初始化时字段还是空的

        var bg = r.gameObject.AddComponent<Image>();
        bg.color = new Color(1f, 0.97f, 0.88f, 1f);
        bg.raycastTarget = true;
        var input = r.gameObject.AddComponent<TMP_InputField>();

        var area = NewStretch(r, "TextArea");
        area.offsetMin = new Vector2(20f, 6f);
        area.offsetMax = new Vector2(-20f, -6f);
        area.gameObject.AddComponent<RectMask2D>();

        var txt = NewLabel(area, "Text", "", 36, Ink, Vector2.zero, Vector2.zero, TextAlignmentOptions.MidlineLeft);
        Stretch(txt.rectTransform);
        var ph = NewLabel(area, "Placeholder", placeholder, 36, new Color(Ink.r, Ink.g, Ink.b, 0.4f),
            Vector2.zero, Vector2.zero, TextAlignmentOptions.MidlineLeft);
        Stretch(ph.rectTransform);
        ph.fontStyle = FontStyles.Italic;

        input.textViewport = area;
        input.textComponent = txt;
        input.placeholder = ph;
        input.targetGraphic = bg;
        if (Font != null)
            input.fontAsset = Font;
        input.pointSize = 36;
        input.characterLimit = 20;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.customCaretColor = true;
        input.caretColor = Ink;
        input.selectionColor = new Color(0.95f, 0.65f, 0.25f, 0.45f);
        input.text = text ?? "";

        r.gameObject.SetActive(true);
        return input;
    }
}

class AutoPopup : MonoBehaviour
{
    public void Play(float seconds)
    {
        StartCoroutine(Run(seconds));
    }

    IEnumerator Run(float seconds)
    {
        var group = GetComponent<CanvasGroup>();
        if (group != null)
            group.alpha = 1f;
        yield return new WaitForSecondsRealtime(Mathf.Max(0.2f, seconds));
        const float fade = 0.25f;
        for (float t = 0f; t < fade; t += Time.unscaledDeltaTime)
        {
            if (group != null)
                group.alpha = 1f - t / fade;
            yield return null;
        }
        Destroy(gameObject);
    }
}
