using System.Collections.Generic;

/// <summary>
/// 开始游戏里的关卡（内置，由人工设计并用程序求解器验证过：每一关都有解）。
/// 地图符号：# 水晶墙   $ 蛋   . 展示盒   @ 琦琦   * 蛋已经在展示盒里   - 空地。第一行是棋盘最上面一行。
/// 想加关卡：在下面再添一个 Def 就行，选关页会自动多出一张卡片。
/// </summary>
public static class CampaignLevels
{
    public class Def
    {
        public string name;
        public string[] rows;      // N 行，每行 N 个字符（N = 3 ~ 9，越靠前的关越小）
        public int[] eggs;         // 这一关的蛋轮流用哪些外观
        public int[] crystals;     // 这一关的水晶轮流用哪些外观
    }

    static readonly Def[] s_Defs =
    {
        new Def
        {
            name = "初来乍到",
            rows = new[]
            {
                "..-",
                "$$-",
                "-@-",
            },
            eggs = new[] { 3, 2 }, crystals = new[] { 0 },
        },
        new Def
        {
            name = "成双成对",
            rows = new[]
            {
                "--..",
                "-#$-",
                "@$--",
                "----",
            },
            eggs = new[] { 4, 5 }, crystals = new[] { 1 },
        },
        new Def
        {
            name = "水晶小径",
            rows = new[]
            {
                "-----",
                "-$@$-",
                "--##.",
                "-----",
                "-#--.",
            },
            eggs = new[] { 1, 8 }, crystals = new[] { 2, 3 },
        },
        new Def
        {
            name = "转角遇见",
            rows = new[]
            {
                "------",
                "------",
                "----$-",
                "@#-$--",
                ".#-##-",
                ".#----",
            },
            eggs = new[] { 6, 9 }, crystals = new[] { 4 },
        },
        new Def
        {
            name = "三蛋归巢",
            rows = new[]
            {
                ".-#---",
                "----$-",
                "-$-#$-",
                ".-@#--",
                "---#--",
                ".---##",
            },
            eggs = new[] { 2, 7, 11 }, crystals = new[] { 5, 1 },
        },
        new Def
        {
            name = "回廊迷局",
            rows = new[]
            {
                "----###",
                "--$-##-",
                "-$-----",
                "----#..",
                "---$#-.",
                "-------",
                "--@----",
            },
            eggs = new[] { 0, 10, 3 }, crystals = new[] { 0, 2 },
        },
        new Def
        {
            name = "星夜回廊",
            rows = new[]
            {
                "--#----.",
                "---$$---",
                "-##-----",
                "-#-$----",
                "-#-##---",
                "--------",
                "------##",
                "---..@--",
            },
            eggs = new[] { 0, 10, 4 }, crystals = new[] { 3, 4, 0 },
        },
        new Def
        {
            name = "请先让路",
            rows = new[]
            {
                "..-..",
                "-***-",
                "$$@$$",
                "-----",
                "-----",
            },
            eggs = new[] { 0, 3, 6, 9, 2, 5, 8 }, crystals = new[] { 0 },
        },
        new Def
        {
            name = "七珍同室",
            rows = new[]
            {
                ".....",
                "-$$$-",
                "$*@*$",
                "-----",
                "-----",
            },
            eggs = new[] { 1, 4, 7, 10, 2, 5, 8 }, crystals = new[] { 0 },
        },
        new Def
        {
            name = "半格之隔",
            rows = new[]
            {
                ".....",
                "-$$#-",
                "--@--",
                "-$$$-",
                "-----",
            },
            eggs = new[] { 11, 6, 3, 0, 9 }, crystals = new[] { 2 },
        },
        new Def
        {
            name = "夹岸陈列",
            rows = new[]
            {
                "......",
                "#$$$$#",
                "--@---",
                "--$$--",
                "------",
                "------",
            },
            eggs = new[] { 4, 8, 1, 7, 11, 2 }, crystals = new[] { 4, 1 },
        },
        new Def
        {
            name = "最后的收藏",
            rows = new[]
            {
                ".....",
                "*$*.$",
                "-$@$-",
                "-$$--",
                "-----",
            },
            eggs = new[] { 0, 3, 6, 9, 1, 4, 7, 10 }, crystals = new[] { 0 },
        },
    };

    public static int Count => s_Defs.Length;

    public static string Name(int index) => s_Defs[index].name;

    /// <summary>把第 index 关的地图变成 LevelData（和创意工坊的关卡用同一种数据）。</summary>
    public static LevelData Build(int index)
    {
        var def = s_Defs[index];
        int size = def.rows.Length;
        var data = new LevelData
        {
            id = "campaign_" + index,
            name = def.name,
            boardSize = size,
            boxes = new List<GridPos>(),
            boxKinds = new List<int>(),
            goals = new List<GridPos>(),
            walls = new List<GridPos>(),
            wallKinds = new List<int>(),
        };

        for (int r = 0; r < size; r++)
        {
            int y = size - 1 - r;
            string line = def.rows[r];
            for (int x = 0; x < line.Length && x < size; x++)
            {
                var p = new GridPos(x, y);
                switch (line[x])
                {
                    case '#':
                        data.wallKinds.Add(def.crystals[data.walls.Count % def.crystals.Length]);
                        data.walls.Add(p);
                        break;
                    case '$':
                        data.boxKinds.Add(def.eggs[data.boxes.Count % def.eggs.Length]);
                        data.boxes.Add(p);
                        break;
                    case '*':
                        data.goals.Add(p);
                        data.boxKinds.Add(def.eggs[data.boxes.Count % def.eggs.Length]);
                        data.boxes.Add(p);
                        break;
                    case '.':
                        data.goals.Add(p);
                        break;
                    case '@':
                        data.snakeStart = p;
                        break;
                }
            }
        }
        return data;
    }
}
