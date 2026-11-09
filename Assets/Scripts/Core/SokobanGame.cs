using System.Collections.Generic;

/// <summary>
/// 推箱子核心逻辑（和画面无关）：编辑器、测试、正式游玩共用同一套规则。
/// 规则：网格移动，一次一格；不能一次推两个箱子；箱子不能推出棋盘；
/// 水晶墙既不能走过，也不能被推动（箱子也推不进墙里）；
/// 通关 = 场上至少有一个箱子，且每个箱子都在展示盒里（目标位上）。
/// </summary>
public class SokobanGame
{
    public int Size { get; private set; }
    public GridPos Snake;
    public readonly List<GridPos> Boxes = new List<GridPos>();
    public readonly List<int> BoxKinds = new List<int>();     // 和 Boxes 一一对应：这个蛋用哪张外观
    public readonly List<GridPos> Goals = new List<GridPos>();
    public readonly List<GridPos> Walls = new List<GridPos>();
    public readonly List<int> WallKinds = new List<int>();    // 和 Walls 一一对应：这块水晶用哪张外观

    public SokobanGame(int size, GridPos snake)
    {
        Size = size < 2 ? 2 : size;
        Snake = snake;
    }

    public SokobanGame(LevelData data)
    {
        Size = data.boardSize < 2 ? 8 : data.boardSize;
        Snake = InBounds(data.snakeStart) ? data.snakeStart : new GridPos(0, 0);

        // 先摆墙：墙不能压在琦琦脚下
        for (int i = 0; i < data.walls.Count; i++)
        {
            var w = data.walls[i];
            if (InBounds(w) && w != Snake && WallIndexAt(w) < 0)
            {
                Walls.Add(w);
                WallKinds.Add(SkinLibrary.WrapCrystal(i < data.wallKinds.Count ? data.wallKinds[i] : 0));
            }
        }

        foreach (var g in data.goals)
        {
            if (InBounds(g) && !HasGoalAt(g) && !IsWall(g))
                Goals.Add(g);
        }

        for (int i = 0; i < data.boxes.Count; i++)
        {
            var b = data.boxes[i];
            if (InBounds(b) && b != Snake && BoxIndexAt(b) < 0 && !IsWall(b))
            {
                // 旧存档没有外观记录：按顺序轮流分配
                int kind = i < data.boxKinds.Count ? data.boxKinds[i] : i;
                Boxes.Add(b);
                BoxKinds.Add(SkinLibrary.WrapEgg(kind));
            }
        }
    }

    public SokobanGame Clone()
    {
        var c = new SokobanGame(Size, Snake);
        c.Boxes.AddRange(Boxes);
        c.BoxKinds.AddRange(BoxKinds);
        c.Goals.AddRange(Goals);
        c.Walls.AddRange(Walls);
        c.WallKinds.AddRange(WallKinds);
        return c;
    }

    public LevelData ToLevelData(string id, string name)
    {
        return new LevelData
        {
            id = id,
            name = name,
            boardSize = Size,
            snakeStart = Snake,
            boxes = new List<GridPos>(Boxes),
            boxKinds = new List<int>(BoxKinds),
            goals = new List<GridPos>(Goals),
            walls = new List<GridPos>(Walls),
            wallKinds = new List<int>(WallKinds)
        };
    }

    // ---------------- 查询 ----------------
    public bool InBounds(GridPos p) => p.x >= 0 && p.y >= 0 && p.x < Size && p.y < Size;

    public int BoxIndexAt(GridPos p)
    {
        for (int i = 0; i < Boxes.Count; i++)
            if (Boxes[i] == p) return i;
        return -1;
    }

    public int GoalIndexAt(GridPos p)
    {
        for (int i = 0; i < Goals.Count; i++)
            if (Goals[i] == p) return i;
        return -1;
    }

    public int WallIndexAt(GridPos p)
    {
        for (int i = 0; i < Walls.Count; i++)
            if (Walls[i] == p) return i;
        return -1;
    }

    public bool HasGoalAt(GridPos p) => GoalIndexAt(p) >= 0;
    public bool IsWall(GridPos p) => WallIndexAt(p) >= 0;

    public int BoxesOnGoalCount
    {
        get
        {
            int n = 0;
            foreach (var b in Boxes)
                if (HasGoalAt(b)) n++;
            return n;
        }
    }

    public bool IsSolved => Boxes.Count > 0 && BoxesOnGoalCount == Boxes.Count;

    /// <summary>
    /// 有没有哪个蛋已经被卡死（再也推不进展示盒了）。规则见 DeadlockChecker：
    /// 冻结（左右、上下两个方向都被水晶墙 / 棋盘边 / 互相堵着的蛋挡住），
    /// 或者贴着棋盘边、而这条边上没有展示盒。已经在展示盒里的蛋不算。
    /// </summary>
    public bool HasStuckBox() => StuckBoxIndices().Count > 0;

    /// <summary>所有被卡死的蛋的编号（失败动画要让它们弹跳）。</summary>
    public List<int> StuckBoxIndices()
    {
        var result = new List<int>();
        ToArrays(out var wall, out var goal, out var boxes, out _);
        new DeadlockChecker(Size, wall, goal).Check(boxes, result);
        return result;
    }

    /// <summary>把当前局面拆成整数数组（cell = y * Size + x），给卡死判定和关卡分析用。</summary>
    public void ToArrays(out bool[] wall, out bool[] goal, out int[] boxes, out int snake)
    {
        wall = new bool[Size * Size];
        goal = new bool[Size * Size];
        foreach (var w in Walls) wall[w.y * Size + w.x] = true;
        foreach (var g in Goals) goal[g.y * Size + g.x] = true;
        boxes = new int[Boxes.Count];
        for (int i = 0; i < boxes.Length; i++)
            boxes[i] = Boxes[i].y * Size + Boxes[i].x;
        snake = Snake.y * Size + Snake.x;
    }

    bool IsBlocked(GridPos p) => !InBounds(p) || IsWall(p);

    // ---------------- 玩法：移动 ----------------
    /// <summary>
    /// 尝试朝 dir 走一格。返回是否真的移动了；pushed 表示这一步推动了一个箱子。
    /// </summary>
    public bool TryMove(GridPos dir, out bool pushed)
    {
        pushed = false;
        var target = Snake + dir;
        if (!InBounds(target) || IsWall(target))
            return false;

        int boxIndex = BoxIndexAt(target);
        if (boxIndex >= 0)
        {
            var beyond = target + dir;
            // 推到棋盘外、推进水晶墙里、或者前面还有箱子（一次不能推两个）都推不动
            if (!InBounds(beyond) || IsWall(beyond) || BoxIndexAt(beyond) >= 0)
                return false;

            Boxes[boxIndex] = beyond;
            pushed = true;
        }

        Snake = target;
        return true;
    }

    // ---------------- 编辑 ----------------
    /// <summary>
    /// 放箱子（kind = 蛋的外观编号）。不能放在琦琦脚下、墙上，也不能和别的箱子重叠；
    /// 如果这格已经有蛋，就只是把它换成新的外观。
    /// </summary>
    public bool PlaceBox(GridPos p, int kind)
    {
        if (!InBounds(p) || p == Snake || IsWall(p))
            return false;

        kind = SkinLibrary.WrapEgg(kind);
        int i = BoxIndexAt(p);
        if (i >= 0)
        {
            if (BoxKinds[i] == kind) return false;
            BoxKinds[i] = kind;
            return true;
        }

        Boxes.Add(p);
        BoxKinds.Add(kind);
        return true;
    }

    /// <summary>放水晶墙（kind = 水晶的外观编号）。琦琦、蛋、展示盒所在的格子不能放；已有水晶则只换外观。</summary>
    public bool PlaceWall(GridPos p, int kind)
    {
        if (!InBounds(p) || p == Snake || BoxIndexAt(p) >= 0 || HasGoalAt(p))
            return false;

        kind = SkinLibrary.WrapCrystal(kind);
        int i = WallIndexAt(p);
        if (i >= 0)
        {
            if (WallKinds[i] == kind) return false;
            WallKinds[i] = kind;
            return true;
        }

        Walls.Add(p);
        WallKinds.Add(kind);
        return true;
    }

    /// <summary>放琦琦（全图唯一）。放到有箱子的格子会把那个箱子清掉；不能放在墙上。</summary>
    public bool PlaceSnake(GridPos p)
    {
        if (!InBounds(p) || p == Snake || IsWall(p))
            return false;

        int boxIndex = BoxIndexAt(p);
        if (boxIndex >= 0)
        {
            Boxes.RemoveAt(boxIndex);
            BoxKinds.RemoveAt(boxIndex);
        }
        Snake = p;
        return true;
    }

    public bool PlaceGoal(GridPos p)
    {
        if (!InBounds(p) || HasGoalAt(p) || IsWall(p))
            return false;
        Goals.Add(p);
        return true;
    }

    /// <summary>擦除：先擦箱子，再擦水晶墙，最后擦展示盒。琦琦不能被擦掉。</summary>
    public bool Erase(GridPos p)
    {
        int boxIndex = BoxIndexAt(p);
        if (boxIndex >= 0)
        {
            Boxes.RemoveAt(boxIndex);
            BoxKinds.RemoveAt(boxIndex);
            return true;
        }

        int wallIndex = WallIndexAt(p);
        if (wallIndex >= 0)
        {
            Walls.RemoveAt(wallIndex);
            WallKinds.RemoveAt(wallIndex);
            return true;
        }

        int goalIndex = GoalIndexAt(p);
        if (goalIndex >= 0)
        {
            Goals.RemoveAt(goalIndex);
            return true;
        }
        return false;
    }
}
