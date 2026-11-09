using System.Collections.Generic;

/// <summary>
/// 卡死判定（和 Unity 无关的纯逻辑）：游戏里的失败提示、关卡分析器用的是同一套规则。
/// 格子用一个整数表示：cell = y * size + x。
///
/// 一个蛋"卡死"，指它再也没法被推进展示盒了，有两种情况：
///   1. 冻结：左右、上下两个方向都被挡住。某个方向"被挡住"= 这个方向的两侧里，有一侧是水晶墙 / 棋盘边
///      （往那边推不动，也没法站到那一侧去推），或者有一侧是另一个同样被冻住的蛋（互相堵着）。
///   2. 贴边没救：蛋贴着棋盘边，只能沿着边滑，而这条边上一个展示盒都没有。
/// 已经在展示盒里的蛋不算卡死（但它依然可以去堵别的蛋）。
/// </summary>
public class DeadlockChecker
{
    readonly int m_Size;
    readonly bool[] m_Wall;
    readonly bool[] m_Goal;
    readonly bool[] m_GoalInColumn;
    readonly bool[] m_GoalInRow;
    readonly int[] m_BoxAt;           // 每个格子里是第几个蛋，没有就是 -1
    bool[] m_Visiting = new bool[8];

    public DeadlockChecker(int size, bool[] wall, bool[] goal)
    {
        m_Size = size;
        m_Wall = wall;
        m_Goal = goal;
        m_GoalInColumn = new bool[size];
        m_GoalInRow = new bool[size];
        for (int c = 0; c < goal.Length; c++)
        {
            if (!goal[c])
                continue;
            m_GoalInColumn[c % size] = true;
            m_GoalInRow[c / size] = true;
        }
        m_BoxAt = new int[size * size];
        for (int i = 0; i < m_BoxAt.Length; i++)
            m_BoxAt[i] = -1;
    }

    /// <summary>有没有卡死的蛋。result 不为空时，把所有卡死的蛋的编号（boxes 数组的下标）放进去。</summary>
    public bool Check(int[] boxes, List<int> result = null)
    {
        if (m_Visiting.Length < boxes.Length)
            m_Visiting = new bool[boxes.Length];

        for (int i = 0; i < boxes.Length; i++)
            m_BoxAt[boxes[i]] = i;

        bool any = false;
        for (int i = 0; i < boxes.Length; i++)
        {
            int cell = boxes[i];
            if (m_Goal[cell])
                continue;

            if (Frozen(boxes, i) || OnDeadEdge(cell))
            {
                any = true;
                if (result == null)
                    break;
                result.Add(i);
            }
        }

        for (int i = 0; i < boxes.Length; i++)
            m_BoxAt[boxes[i]] = -1;
        return any;
    }

    bool Frozen(int[] boxes, int i)
    {
        m_Visiting[i] = true;
        bool frozen = AxisBlocked(boxes, i, 1, 0) && AxisBlocked(boxes, i, 0, 1);
        m_Visiting[i] = false;
        return frozen;
    }

    bool AxisBlocked(int[] boxes, int i, int dx, int dy)
    {
        int x = boxes[i] % m_Size, y = boxes[i] / m_Size;
        for (int sign = -1; sign <= 1; sign += 2)
        {
            int nx = x + dx * sign, ny = y + dy * sign;
            if (nx < 0 || ny < 0 || nx >= m_Size || ny >= m_Size || m_Wall[ny * m_Size + nx])
                return true;
            int j = m_BoxAt[ny * m_Size + nx];
            if (j >= 0 && (m_Visiting[j] || Frozen(boxes, j)))
                return true;
        }
        return false;
    }

    bool OnDeadEdge(int cell)
    {
        int x = cell % m_Size, y = cell / m_Size;
        if ((x == 0 || x == m_Size - 1) && !m_GoalInColumn[x])
            return true;
        if ((y == 0 || y == m_Size - 1) && !m_GoalInRow[y])
            return true;
        return false;
    }
}
