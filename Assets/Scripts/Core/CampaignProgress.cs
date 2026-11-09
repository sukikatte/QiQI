using UnityEngine;

/// <summary>开始游戏的进度：每一关通关时的最少步数（存在 PlayerPrefs 里，0 = 还没通关）。</summary>
public static class CampaignProgress
{
    static string Key(int level) => "qiqi_campaign_best_" + level;

    public static int BestSteps(int level) => PlayerPrefs.GetInt(Key(level), 0);

    public static bool IsCleared(int level) => BestSteps(level) > 0;

    /// <summary>第一关一开始就能进；后面的关要先通关它的上一关。</summary>
    public static bool IsUnlocked(int level) => level <= 0 || IsCleared(level - 1);

    public const int WorkshopUnlockLevel = 9;    // 第 10 关（下标 9）通关后解锁创意工坊
    public static bool WorkshopUnlocked => IsCleared(WorkshopUnlockLevel);

    /// <summary>记录一次通关。返回 true 表示刷新了最少步数。</summary>
    public static bool Record(int level, int steps)
    {
        int best = BestSteps(level);
        if (best > 0 && best <= steps)
            return false;
        PlayerPrefs.SetInt(Key(level), Mathf.Max(1, steps));
        PlayerPrefs.Save();
        return true;
    }

    // ---------------- 剧情：看过没有 ----------------
    static string StoryKey(string id) => "qiqi_story_seen_" + id;

    public static bool StorySeen(string id) => PlayerPrefs.GetInt(StoryKey(id), 0) == 1;

    public static void MarkStorySeen(string id)
    {
        PlayerPrefs.SetInt(StoryKey(id), 1);
        PlayerPrefs.Save();
    }

    /// <summary>清空所有关卡记录和剧情进度（选关页按 F8），方便重新体验教学。</summary>
    public static void ResetAll()
    {
        for (int i = 0; i < 16; i++)
            PlayerPrefs.DeleteKey(Key(i));
        foreach (var id in CampaignStory.AllIds)
            PlayerPrefs.DeleteKey(StoryKey(id));
        PlayerPrefs.Save();
    }
}
