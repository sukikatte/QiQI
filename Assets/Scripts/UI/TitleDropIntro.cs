using System.Collections;
using UnityEngine;

/// <summary>
/// 游戏名从屏幕上方缓缓落下：先快后慢，轻轻停住，没有弹跳。
/// </summary>
public class TitleDropIntro : MonoBehaviour
{
    const float StartDelay = 0.4f;    // 画面出现后等一下再下来
    const float DropHeight = 700f;    // 从目标位置上方多高开始
    const float DropTime = 2.4f;      // 下落总时长（越大越慢）
    const float FadeInTime = 1.0f;    // 下落过程中慢慢淡入

    public void Play(RectTransform rect, CanvasGroup group, Vector2 targetPos)
    {
        // 立刻藏到上方，避免第一帧闪一下
        rect.anchoredPosition = targetPos + new Vector2(0f, DropHeight);
        group.alpha = 0f;
        StartCoroutine(DropRoutine(rect, group, targetPos));
    }

    IEnumerator DropRoutine(RectTransform rect, CanvasGroup group, Vector2 target)
    {
        for (float t = 0f; t < StartDelay; t += Time.unscaledDeltaTime)
            yield return null;

        for (float t = 0f; t < DropTime; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.Clamp01(t / DropTime);

            // 位置：缓出（EaseOutSine），平稳地减速到目标位置
            float drop = 1f - EaseOutSine(k);            // 1 → 0
            rect.anchoredPosition = target + new Vector2(0f, DropHeight * drop);

            group.alpha = Mathf.Clamp01(t / FadeInTime);
            yield return null;
        }

        rect.anchoredPosition = target;
        rect.localRotation = Quaternion.identity;
        group.alpha = 1f;
    }

    static float EaseOutSine(float x)
    {
        return Mathf.Sin(x * Mathf.PI * 0.5f);
    }
}
