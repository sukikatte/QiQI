using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 界面和结算音效：点按钮的 click、过关的 win、失败的 failure（都在 Resources/Music/）。
/// 用一个不随场景销毁的 AudioSource 播放，所以点了"换场景"的按钮，声音也不会被切断。
/// 过关 / 失败音效播放期间，背景音乐会被压低。
/// </summary>
public static class UiSfx
{
    const float ClickVolume = 0.8f;
    const float ResultVolume = 1f;

    static AudioSource s_Source;
    static readonly Dictionary<string, AudioClip> s_Clips = new Dictionary<string, AudioClip>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        s_Source = null;      // 关闭"进入 Play 不重载域"时，保证每次新运行都重新创建
        s_Clips.Clear();
    }

    public static void PlayClick() => Play("click", ClickVolume, false);

    public static void PlayWin() => Play("win", ResultVolume, true);

    public static void PlayFailure() => Play("failure", ResultVolume, true);

    static void Play(string name, float volume, bool duckMusic)
    {
        if (!s_Clips.TryGetValue(name, out var clip) || clip == null)
        {
            clip = Resources.Load<AudioClip>("Music/" + name);
            if (clip == null)
            {
                Debug.LogError("找不到音效: Resources/Music/" + name);
                return;
            }
            s_Clips[name] = clip;
        }

        if (s_Source == null)
        {
            var go = new GameObject("UiSfx");
            Object.DontDestroyOnLoad(go);
            s_Source = go.AddComponent<AudioSource>();
            s_Source.playOnAwake = false;
            s_Source.spatialBlend = 0f;
        }
        s_Source.PlayOneShot(clip, volume);

        if (duckMusic)
            MusicPlayer.Duck(clip.length + 0.3f);
    }
}
