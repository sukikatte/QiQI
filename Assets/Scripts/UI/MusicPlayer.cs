using UnityEngine;

/// <summary>
/// 背景音乐：整个游戏只有一个播放器（切场景也不会销毁），每首曲子各自循环。
/// 页面只需要告诉它"现在该放哪一首"：
///   Menu      主菜单、关卡选择页
///   Game      正式游玩关卡时
///   Workshop  创意工坊的所有页面（目录、编辑、测试、游玩）
/// 切换曲子时：旧的淡出并暂停，新的淡入；回到以前的曲子会从暂停的位置接着放。
/// 同一首曲子的页面之间来回切换，什么都不会发生，所以不会重新播放。
/// 过关 / 失败的音效播放时，背景音乐会暂时压低（Duck），播完再恢复。
/// </summary>
public class MusicPlayer : MonoBehaviour
{
    public enum Track { None = -1, Menu = 0, Game = 1, Workshop = 2 }

    static readonly string[] ClipPaths = { "Music/menu", "Music/game", "Music/workshop" };
    const float TargetVolume = 0.6f;
    const float DuckedFactor = 0.15f;         // 压低后的音量比例
    const float FadeInSpeed = 1f / 1.2f;      // 每秒音量变化（1.2 秒淡入）
    const float FadeOutSpeed = 1f / 0.6f;     // 0.6 秒淡出

    static MusicPlayer s_Instance;

    class Voice
    {
        public AudioSource source;
        public float level;       // 淡入淡出进度 0~1
        public bool started;
        public bool paused;
    }

    Voice[] m_Voices;
    Track m_Current = Track.None;
    float m_DuckUntil;
    float m_Duck = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        s_Instance = null;      // 关闭"进入 Play 不重载域"时，保证每次新运行都重新创建
    }

    /// <summary>现在该放哪一首。每个页面进入时调用一次就行。</summary>
    public static void SetTrack(Track track)
    {
        if (s_Instance == null)
        {
            if (track == Track.None)
                return;
            var go = new GameObject("MusicPlayer");
            DontDestroyOnLoad(go);
            s_Instance = go.AddComponent<MusicPlayer>();
            s_Instance.Setup();
        }
        s_Instance.m_Current = track;
    }

    /// <summary>压低背景音乐一段时间（播过关 / 失败音效时用）。</summary>
    public static void Duck(float seconds)
    {
        if (s_Instance != null)
            s_Instance.m_DuckUntil = Time.unscaledTime + seconds;
    }

    void Setup()
    {
        m_Voices = new Voice[ClipPaths.Length];
        for (int i = 0; i < ClipPaths.Length; i++)
        {
            var clip = Resources.Load<AudioClip>(ClipPaths[i]);
            if (clip == null)
                Debug.LogError("找不到背景音乐: Resources/" + ClipPaths[i]);

            var src = gameObject.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = true;
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            src.volume = 0f;
            m_Voices[i] = new Voice { source = src };
        }
    }

    void Update()
    {
        if (m_Voices == null)
            return;

        float dt = Time.unscaledDeltaTime;
        float duckTarget = Time.unscaledTime < m_DuckUntil ? DuckedFactor : 1f;
        m_Duck = Mathf.MoveTowards(m_Duck, duckTarget, dt * 3f);

        for (int i = 0; i < m_Voices.Length; i++)
        {
            var v = m_Voices[i];
            if (v.source == null || v.source.clip == null)
                continue;

            if (i == (int)m_Current)
            {
                v.level = Mathf.MoveTowards(v.level, 1f, FadeInSpeed * dt);
                if (!v.started)
                {
                    v.started = true;
                    v.source.Play();                // 第一次：从头开始
                }
                else if (v.paused)
                {
                    v.paused = false;
                    v.source.UnPause();             // 暂停过：从暂停的位置接着放，不会回到开头
                }
            }
            else
            {
                v.level = Mathf.MoveTowards(v.level, 0f, FadeOutSpeed * dt);
                if (v.level <= 0f && v.started && !v.paused)
                {
                    v.paused = true;
                    v.source.Pause();
                }
            }

            v.source.volume = TargetVolume * v.level * m_Duck;
        }
    }
}
