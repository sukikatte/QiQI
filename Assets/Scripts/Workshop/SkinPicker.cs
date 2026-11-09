using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 编辑器左侧面板下半：蛋、水晶各占一行，可以左右滑动挑选外观。
/// 上半的摆放工具由创意工坊加进来，和这两行外观选在同一块羊皮纸上。
/// </summary>
public class SkinPicker : MonoBehaviour
{
    public const float PanelWidth = 460f;
    public const float PanelHeight = 680f;

    /// <summary>参数：true = 选的是蛋，false = 选的是水晶。</summary>
    public event Action<bool> Picked;

    public int SelectedEgg { get; private set; }
    public int SelectedCrystal { get; private set; }

    Image[] m_EggFrames;
    Image[] m_CrystalFrames;

    public static SkinPicker Create(Transform parent, Vector2 pos, Vector2 size)
    {
        var panel = WorkshopUI.NewImage(parent, "SkinPanel", WorkshopUI.LoadSprite("UI/panel_parchment"), pos,
            size, true, true);
        var picker = panel.gameObject.AddComponent<SkinPicker>();
        picker.Build(panel.transform);
        return picker;
    }

    void Build(Transform p)
    {
        WorkshopUI.NewLabel(p, "Heading", "编辑选择器", 40, new Color(0.62f, 0.36f, 0.12f),
            new Vector2(0f, 310f), new Vector2(440f, 52f), TextAlignmentOptions.Center);
        m_EggFrames = BuildStrip(p, "滑动选择蛋外观", new Vector2(0f, -100f), SkinLibrary.EggCount, i => SkinLibrary.Egg(i), true);
        m_CrystalFrames = BuildStrip(p, "滑动选择水晶外观", new Vector2(0f, -260f), SkinLibrary.CrystalCount, i => SkinLibrary.Crystal(i), false);
        RefreshFrames();
    }

    Image[] BuildStrip(Transform parent, string title, Vector2 pos, int count, Func<int, Sprite> spriteOf, bool eggs)
    {
        const float viewW = 400f, viewH = 108f, cell = 88f, gap = 10f;

        WorkshopUI.NewLabel(parent, title + "Title", title, 36, new Color(0.62f, 0.36f, 0.12f),
            pos + new Vector2(0f, viewH * 0.5f + 26f), new Vector2(viewW, 36f), TextAlignmentOptions.MidlineLeft);

        var view = WorkshopUI.NewRect(parent, title + "View", pos, new Vector2(viewW, viewH));
        var viewImage = view.gameObject.AddComponent<Image>();
        viewImage.color = new Color(0.42f, 0.24f, 0.10f, 0.14f);
        viewImage.raycastTarget = true;
        view.gameObject.AddComponent<RectMask2D>();

        var sr = view.gameObject.AddComponent<ScrollRect>();
        sr.horizontal = true;
        sr.vertical = false;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.inertia = true;
        sr.decelerationRate = 0.135f;
        sr.scrollSensitivity = 48f;
        sr.viewport = view;

        view.gameObject.AddComponent<SkinStripScroll>().scroll = sr;

        float contentW = gap + count * (cell + gap);
        var content = WorkshopUI.NewRect(view, "Content", Vector2.zero, new Vector2(contentW, viewH));
        content.anchorMin = content.anchorMax = new Vector2(0f, 0.5f);
        content.pivot = new Vector2(0f, 0.5f);
        content.anchoredPosition = Vector2.zero;
        sr.content = content;

        var frames = new Image[count];
        for (int i = 0; i < count; i++)
        {
            int index = i;
            float x = gap + cell * 0.5f + i * (cell + gap);
            var cellRect = WorkshopUI.NewRect(content, title + "_" + i, new Vector2(x, 0f), new Vector2(cell, cell));
            cellRect.anchorMin = cellRect.anchorMax = new Vector2(0f, 0.5f);
            cellRect.pivot = new Vector2(0.5f, 0.5f);
            cellRect.anchoredPosition = new Vector2(x, 0f);
            frames[i] = NewCell(cellRect, spriteOf(i), new Vector2(cell, cell),
                () => { if (eggs) PickEgg(index); else PickCrystal(index); });
        }
        return frames;
    }

    static readonly Color HoverFill = new Color(1f, 1f, 1f, 0.10f);
    const float FrameThickness = 3f;

    static Image NewCell(RectTransform root, Sprite sprite, Vector2 size, Action onClick)
    {
        var rootImage = root.gameObject.AddComponent<Image>();
        rootImage.color = new Color(1f, 1f, 1f, 0f);
        rootImage.raycastTarget = true;

        var frameSize = size + new Vector2(10f, 10f);
        var frame = WorkshopUI.NewImage(root, "Frame", null, Vector2.zero, frameSize);
        frame.color = new Color(1f, 0.82f, 0.35f, 0.16f);
        var line = new Color(0.93f, 0.66f, 0.16f, 1f);
        NewFrameEdge(frame.transform, new Vector2(0f, (frameSize.y - FrameThickness) * 0.5f), new Vector2(frameSize.x, FrameThickness), line);
        NewFrameEdge(frame.transform, new Vector2(0f, -(frameSize.y - FrameThickness) * 0.5f), new Vector2(frameSize.x, FrameThickness), line);
        NewFrameEdge(frame.transform, new Vector2(-(frameSize.x - FrameThickness) * 0.5f, 0f), new Vector2(FrameThickness, frameSize.y), line);
        NewFrameEdge(frame.transform, new Vector2((frameSize.x - FrameThickness) * 0.5f, 0f), new Vector2(FrameThickness, frameSize.y), line);
        frame.gameObject.SetActive(false);

        WorkshopUI.NewImage(root, "Thumb", sprite, Vector2.zero, size * 0.86f);

        var button = root.gameObject.AddComponent<Button>();
        button.targetGraphic = rootImage;
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => onClick());
        var feedback = root.gameObject.AddComponent<UIButtonFeedback>();
        feedback.hoverTint = HoverFill;
        feedback.pressedTint = new Color(1f, 1f, 1f, 0.2f);
        return frame;
    }

    static void NewFrameEdge(Transform parent, Vector2 pos, Vector2 size, Color color)
    {
        var edge = WorkshopUI.NewImage(parent, "Edge", null, pos, size);
        edge.color = color;
    }

    void PickEgg(int index)
    {
        SelectedEgg = index;
        RefreshFrames();
        Picked?.Invoke(true);
    }

    void PickCrystal(int index)
    {
        SelectedCrystal = index;
        RefreshFrames();
        Picked?.Invoke(false);
    }

    void RefreshFrames()
    {
        for (int i = 0; i < m_EggFrames.Length; i++)
            m_EggFrames[i].gameObject.SetActive(i == SelectedEgg);
        for (int i = 0; i < m_CrystalFrames.Length; i++)
            m_CrystalFrames[i].gameObject.SetActive(i == SelectedCrystal);
    }

}

/// <summary>竖向滚轮也拿来左右滑外观条。</summary>
class SkinStripScroll : MonoBehaviour, IScrollHandler
{
    public ScrollRect scroll;
    public void OnScroll(PointerEventData eventData)
    {
        if (scroll == null || scroll.content == null)
            return;
        float range = Mathf.Max(1f, scroll.content.rect.width - ((RectTransform)scroll.transform).rect.width);
        scroll.horizontalNormalizedPosition = Mathf.Clamp01(
            scroll.horizontalNormalizedPosition - eventData.scrollDelta.y * (80f / range));
    }
}
