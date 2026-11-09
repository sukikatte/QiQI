using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 关卡本地存档：persistentDataPath/levels/ 下每关一个 JSON，外加一个 index.json 索引。
/// 索引丢了或损坏时，会重新扫描目录重建，所以不会因此丢关卡。
/// </summary>
public static class LevelStorage
{
    [Serializable]
    class IndexEntry
    {
        public string id;
        public string name;
        public long createdTicks;
        public long updatedTicks;
    }

    [Serializable]
    class IndexFile
    {
        public List<IndexEntry> levels = new List<IndexEntry>();
    }

    const string IndexFileName = "index.json";

    static string Dir
    {
        get
        {
            string dir = Path.Combine(Application.persistentDataPath, "levels");
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            return dir;
        }
    }

    static string IndexPath => Path.Combine(Dir, IndexFileName);
    static string LevelPath(string id) => Path.Combine(Dir, id + ".json");

    public static string NewId()
    {
        return "lv_" + DateTime.UtcNow.Ticks.ToString("x");
    }

    /// <summary>读取所有关卡，新创建的排在最上面，旧的往下排。</summary>
    public static List<LevelData> LoadAll()
    {
        var result = new List<LevelData>();
        try
        {
            var index = ReadIndex();
            foreach (var entry in index.levels)
            {
                var level = ReadLevel(entry.id);
                if (level != null)
                    result.Add(level);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("读取关卡列表失败: " + e.Message);
        }

        result.Sort((a, b) => CreatedTicks(b).CompareTo(CreatedTicks(a)));
        return result;
    }

    public static bool Save(LevelData level)
    {
        if (level == null || string.IsNullOrEmpty(level.id))
            return false;

        try
        {
            if (level.createdTicks == 0)
            {
                var existing = ReadLevel(level.id);
                if (existing != null && existing.createdTicks != 0)
                    level.createdTicks = existing.createdTicks;
                else if (existing != null && existing.updatedTicks != 0)
                    level.createdTicks = existing.updatedTicks;
                else
                    level.createdTicks = DateTime.UtcNow.Ticks;
            }
            level.updatedTicks = DateTime.UtcNow.Ticks;
            File.WriteAllText(LevelPath(level.id), JsonUtility.ToJson(level, true));

            var index = ReadIndex();
            index.levels.RemoveAll(e => e.id == level.id);
            index.levels.Insert(0, new IndexEntry
            {
                id = level.id,
                name = level.name,
                createdTicks = level.createdTicks,
                updatedTicks = level.updatedTicks
            });
            WriteIndex(index);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("保存关卡失败: " + e.Message);
            return false;
        }
    }

    public static bool Delete(string id)
    {
        if (string.IsNullOrEmpty(id))
            return false;

        try
        {
            string path = LevelPath(id);
            if (File.Exists(path))
                File.Delete(path);

            var index = ReadIndex();
            index.levels.RemoveAll(e => e.id == id);
            WriteIndex(index);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("删除关卡失败: " + e.Message);
            return false;
        }
    }

    static LevelData ReadLevel(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        string path = LevelPath(id);
        if (!File.Exists(path))
            return null;

        try
        {
            var level = JsonUtility.FromJson<LevelData>(File.ReadAllText(path));
            if (level != null && string.IsNullOrEmpty(level.id))
                level.id = id;
            return level;
        }
        catch (Exception e)
        {
            Debug.LogWarning("关卡文件损坏，已跳过: " + id + " (" + e.Message + ")");
            return null;
        }
    }

    static IndexFile ReadIndex()
    {
        string path = IndexPath;
        if (File.Exists(path))
        {
            try
            {
                var index = JsonUtility.FromJson<IndexFile>(File.ReadAllText(path));
                if (index != null && index.levels != null)
                    return index;
            }
            catch (Exception)
            {
                // 索引损坏：往下走，重建
            }
        }
        return RebuildIndex();
    }

    static IndexFile RebuildIndex()
    {
        var index = new IndexFile();
        foreach (string file in Directory.GetFiles(Dir, "*.json"))
        {
            if (Path.GetFileName(file) == IndexFileName)
                continue;

            string id = Path.GetFileNameWithoutExtension(file);
            var level = ReadLevel(id);
            if (level != null)
                index.levels.Add(new IndexEntry
                {
                    id = id,
                    name = level.name,
                    createdTicks = CreatedTicks(level),
                    updatedTicks = level.updatedTicks
                });
        }

        try { WriteIndex(index); } catch (Exception) { }
        return index;
    }

    static void WriteIndex(IndexFile index)
    {
        File.WriteAllText(IndexPath, JsonUtility.ToJson(index, true));
    }

    static long CreatedTicks(LevelData level)
    {
        if (level == null)
            return 0;
        return level.createdTicks != 0 ? level.createdTicks : level.updatedTicks;
    }
}
