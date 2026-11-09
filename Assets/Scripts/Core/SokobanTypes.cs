using System;
using System.Collections.Generic;

/// <summary>棋盘格坐标。原点在左下角，x 向右，y 向上（和屏幕上的方向键一致）。</summary>
[Serializable]
public struct GridPos : IEquatable<GridPos>
{
    public int x;
    public int y;

    public GridPos(int x, int y)
    {
        this.x = x;
        this.y = y;
    }

    public static GridPos operator +(GridPos a, GridPos b) => new GridPos(a.x + b.x, a.y + b.y);
    public static bool operator ==(GridPos a, GridPos b) => a.x == b.x && a.y == b.y;
    public static bool operator !=(GridPos a, GridPos b) => !(a == b);

    public bool Equals(GridPos other) => this == other;
    public override bool Equals(object obj) => obj is GridPos g && this == g;
    public override int GetHashCode() => (x * 397) ^ y;
    public override string ToString() => "(" + x + "," + y + ")";
}

/// <summary>一个关卡的存档数据（JSON 序列化）。</summary>
[Serializable]
public class LevelData
{
    public string id;
    public string name;
    public int boardSize = 8;
    public GridPos snakeStart;
    public List<GridPos> boxes = new List<GridPos>();
    public List<int> boxKinds = new List<int>();     // 和 boxes 一一对应：每个蛋用哪一张外观（旧存档没有这项）
    public List<GridPos> goals = new List<GridPos>();
    public List<GridPos> walls = new List<GridPos>();      // 水晶墙：不能走过，也推不动
    public List<int> wallKinds = new List<int>();    // 和 walls 一一对应：每块水晶用哪一张外观
    public long createdTicks;                        // 第一次保存的时间，列表按这个排：新的在上
    public long updatedTicks;

    public LevelData Clone()
    {
        return new LevelData
        {
            id = id,
            name = name,
            boardSize = boardSize,
            snakeStart = snakeStart,
            boxes = new List<GridPos>(boxes),
            boxKinds = new List<int>(boxKinds),
            goals = new List<GridPos>(goals),
            walls = new List<GridPos>(walls),
            wallKinds = new List<int>(wallKinds),
            createdTicks = createdTicks,
            updatedTicks = updatedTicks
        };
    }
}
