using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 发光点：亮度（和一点点大小）随时间呼吸或闪烁。
/// flicker = true 用噪声做出火焰那种不规则的抖动，否则是平滑的正弦呼吸。
/// </summary>
public class GlowPulse : MonoBehaviour
{
    Image m_Image;
    Color m_Color;
    float m_Base, m_Amp, m_Speed, m_Phase, m_ScaleAmp;
    bool m_Flicker;

    public void Init(Image image, Color color, float baseAlpha, float amp, float speed,
                     float phase, bool flicker, float scaleAmp)
    {
        m_Image = image;
        m_Color = color;
        m_Base = baseAlpha;
        m_Amp = amp;
        m_Speed = speed;
        m_Phase = phase;
        m_Flicker = flicker;
        m_ScaleAmp = scaleAmp;
        Apply(0.5f);
    }

    void Update()
    {
        if (m_Image == null)
            return;

        float t = Time.unscaledTime;
        float n = m_Flicker
            ? Mathf.PerlinNoise(t * m_Speed, m_Phase)                   // 0..1 不规则抖动
            : 0.5f + 0.5f * Mathf.Sin(t * m_Speed + m_Phase);           // 0..1 平滑呼吸
        Apply(n);
    }

    void Apply(float n)
    {
        float k = (n - 0.5f) * 2f;   // -1..1
        var c = m_Color;
        c.a = Mathf.Clamp01(m_Base + m_Amp * k);
        m_Image.color = c;
        float s = 1f + m_ScaleAmp * k;
        transform.localScale = new Vector3(s, s, 1f);
    }

    // ---- 程序生成的柔光圆点贴图（白色，边缘平滑衰减） ----
    static Sprite s_GlowSprite;

    public static Sprite GetGlowSprite()
    {
        if (s_GlowSprite != null)
            return s_GlowSprite;

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
                float dx = (x - half) / half;
                float dy = (y - half) / half;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - d);
                a = a * a * (3f - 2f * a);   // smoothstep
                a *= a;                       // 让中心更集中、边缘更柔
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();

        s_GlowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return s_GlowSprite;
    }
}
