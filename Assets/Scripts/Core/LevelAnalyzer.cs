using System;
using System.Collections.Generic;

/// <summary>一次关卡分析的结果。</summary>
public class LevelAnalysis
{
    public bool truncated;            // 局面太多，没算完（下面的数字都不可靠）
    public bool solvable;             // 这一关有没有解
    public int boxCount, goalCount;
    public int minPushes;             // 最少要推几下
    public int minSteps;              // 最少要走几步（包括走路和推）
    public string route;              // 其中一条最短路线：↑↓←→ 一步一个箭头（↑ = 棋盘上方），照着走就能验证步数
    public double optimalRoutes;      // 步数最少的解法有几种（推法不同才算不同）
    public int states;                // 一共有多少种不同的局面
    public int loseStates;            // 其中"已经没救"的局面
    public int hiddenTrapStates;      // 没救、但游戏还不会弹出失败提示的局面（"暗坑"）
    public int worstDistance;         // 最难救回的局面：离通关最少还要几步
    public int difficulty;            // 1 ~ 5
    public string difficultyName;
    public float score;

    public float LoseRatio => states > 0 ? (float)loseStates / states : 0f;
}

/// <summary>
/// 关卡分析器（纯逻辑，可以放到后台线程里算）。
/// 做法：把"推一下蛋"当作一步，从开局出发把所有可能的局面都搜出来（局面 = 所有蛋的位置 + 琦琦的位置），
/// 两个局面之间的代价 = 琦琦走到蛋后面的步数 + 1（推的那一步）。然后：
///   · 最短路 → 最少步数、最少推数、最优解法数量；
///   · 从通关局面倒着搜 → 每个局面离通关还要几步；没法到达通关的局面就是"没救"的局面。
/// 游戏里会触发失败提示的局面（DeadlockChecker 判定卡死）不再往下搜，和实际玩起来一致。
/// </summary>
public static class LevelAnalyzer
{
    public const int MaxStates = 400000;
    static readonly int[] Dx = { 0, 0, -1, 1 };
    static readonly int[] Dy = { 1, -1, 0, 0 };

    const byte Normal = 0, Win = 1, Flagged = 2;

    public static LevelAnalysis Analyze(int size, bool[] wall, bool[] goal, int[] startBoxes, int startSnake, Func<bool> cancelled = null)
    {
        var result = new LevelAnalysis { boxCount = startBoxes.Length };
        foreach (bool g in goal)
            if (g) result.goalCount++;

        int cells = size * size;
        var checker = new DeadlockChecker(size, wall, goal);

        // ---------------- 1. 搜出所有局面 ----------------
        var nodeBoxes = new List<int[]>();
        var nodeSnake = new List<int>();
        var nodeStatus = new List<byte>();
        var edgeStart = new List<int>();     // 每个局面的出边从哪里开始
        var edgeTo = new List<int>();
        var edgeCost = new List<int>();
        var edgeInfo = new List<int>();      // 这条边推的是哪个格子里的蛋、往哪个方向：cell * 4 + 方向
        var index = new Dictionary<string, int>();

        int AddNode(int[] boxes, int snake)
        {
            string key = MakeKey(boxes, snake);
            if (index.TryGetValue(key, out int id))
                return id;
            id = nodeBoxes.Count;
            index[key] = id;
            nodeBoxes.Add(boxes);
            nodeSnake.Add(snake);
            nodeStatus.Add(StatusOf(boxes, goal, checker));
            return id;
        }

        var start = (int[])startBoxes.Clone();
        Array.Sort(start);
        AddNode(start, startSnake);

        var boxAt = new int[cells];
        for (int i = 0; i < cells; i++) boxAt[i] = -1;
        var dist = new int[cells];
        var queue = new int[cells];

        for (int node = 0; node < nodeBoxes.Count; node++)
        {
            edgeStart.Add(edgeTo.Count);
            if (nodeStatus[node] != Normal)
                continue;
            if (cancelled != null && (node & 255) == 0 && cancelled())
            {
                result.truncated = true;
                result.states = nodeBoxes.Count;
                return result;
            }

            var boxes = nodeBoxes[node];
            for (int i = 0; i < boxes.Length; i++) boxAt[boxes[i]] = i;

            // 琦琦在不推动任何蛋的情况下能走到哪里（以及要走几步）
            for (int i = 0; i < cells; i++) dist[i] = -1;
            int head = 0, tail = 0;
            int snake = nodeSnake[node];
            dist[snake] = 0;
            queue[tail++] = snake;
            while (head < tail)
            {
                int c = queue[head++];
                int cx = c % size, cy = c / size;
                for (int d = 0; d < 4; d++)
                {
                    int nx = cx + Dx[d], ny = cy + Dy[d];
                    if (nx < 0 || ny < 0 || nx >= size || ny >= size) continue;
                    int n = ny * size + nx;
                    if (dist[n] >= 0 || wall[n] || boxAt[n] >= 0) continue;
                    dist[n] = dist[c] + 1;
                    queue[tail++] = n;
                }
            }

            for (int b = 0; b < boxes.Length; b++)
            {
                int bx = boxes[b] % size, by = boxes[b] / size;
                for (int d = 0; d < 4; d++)
                {
                    int behindX = bx - Dx[d], behindY = by - Dy[d];
                    int aheadX = bx + Dx[d], aheadY = by + Dy[d];
                    if (behindX < 0 || behindY < 0 || behindX >= size || behindY >= size) continue;
                    if (aheadX < 0 || aheadY < 0 || aheadX >= size || aheadY >= size) continue;
                    int behind = behindY * size + behindX;
                    int ahead = aheadY * size + aheadX;
                    if (dist[behind] < 0 || wall[ahead] || boxAt[ahead] >= 0) continue;

                    var next = (int[])boxes.Clone();
                    next[b] = ahead;
                    Array.Sort(next);

                    if (nodeBoxes.Count >= MaxStates && !index.ContainsKey(MakeKey(next, boxes[b])))
                    {
                        result.truncated = true;
                        result.states = nodeBoxes.Count;
                        for (int i = 0; i < boxes.Length; i++) boxAt[boxes[i]] = -1;
                        return result;
                    }
                    int to = AddNode(next, boxes[b]);
                    edgeTo.Add(to);
                    edgeCost.Add(dist[behind] + 1);
                    edgeInfo.Add(boxes[b] * 4 + d);
                }
            }

            for (int i = 0; i < boxes.Length; i++) boxAt[boxes[i]] = -1;
        }
        edgeStart.Add(edgeTo.Count);

        int count = nodeBoxes.Count;
        result.states = count;

        // ---------------- 2. 从开局出发的最短路（按步数）+ 最优解法计数 ----------------
        var steps = new long[count];
        var ways = new double[count];
        for (int i = 0; i < count; i++) steps[i] = long.MaxValue;
        var parentNode = new int[count];
        var parentEdge = new int[count];
        steps[0] = 0;
        ways[0] = 1;
        var heap = new MinHeap();
        heap.Push(0, 0);
        while (heap.Count > 0)
        {
            heap.Pop(out long d, out int u);
            if (d > steps[u]) continue;
            for (int e = edgeStart[u]; e < edgeStart[u + 1]; e++)
            {
                int v = edgeTo[e];
                long nd = d + edgeCost[e];
                if (nd < steps[v])
                {
                    steps[v] = nd;
                    parentNode[v] = u;
                    parentEdge[v] = e;
                    ways[v] = ways[u];
                    heap.Push(nd, v);
                }
                else if (nd == steps[v])
                {
                    ways[v] = Math.Min(ways[v] + ways[u], 1e15);
                }
            }
        }

        long best = long.MaxValue;
        for (int i = 0; i < count; i++)
            if (nodeStatus[i] == Win && steps[i] < best)
                best = steps[i];
        result.solvable = best != long.MaxValue;
        if (result.solvable)
        {
            result.minSteps = (int)best;
            int target = 0;
            for (int i = 0; i < count; i++)
                if (nodeStatus[i] == Win && steps[i] == best) { target = i; break; }
            result.route = BuildRoute(size, wall, nodeBoxes, nodeSnake, edgeInfo, parentNode, parentEdge, target);
            for (int i = 0; i < count; i++)
                if (nodeStatus[i] == Win && steps[i] == best)
                    result.optimalRoutes += ways[i];
        }

        // ---------------- 3. 最少推数（每推一下算 1）----------------
        if (result.solvable)
        {
            var pushes = new int[count];
            for (int i = 0; i < count; i++) pushes[i] = -1;
            var q = new Queue<int>();
            pushes[0] = 0;
            q.Enqueue(0);
            int bestPush = -1;
            while (q.Count > 0 && bestPush < 0)
            {
                int u = q.Dequeue();
                if (nodeStatus[u] == Win) { bestPush = pushes[u]; break; }
                for (int e = edgeStart[u]; e < edgeStart[u + 1]; e++)
                {
                    int v = edgeTo[e];
                    if (pushes[v] >= 0) continue;
                    pushes[v] = pushes[u] + 1;
                    q.Enqueue(v);
                }
            }
            result.minPushes = Math.Max(bestPush, 0);
        }

        // ---------------- 4. 倒着搜：每个局面离通关还要几步 ----------------
        var revHead = new int[count];
        for (int i = 0; i < count; i++) revHead[i] = -1;
        var revNext = new int[edgeTo.Count];
        var revFrom = new int[edgeTo.Count];
        for (int u = 0; u < count; u++)
        {
            for (int e = edgeStart[u]; e < edgeStart[u + 1]; e++)
            {
                int v = edgeTo[e];
                revFrom[e] = u;
                revNext[e] = revHead[v];
                revHead[v] = e;
            }
        }

        var toGoal = new long[count];
        for (int i = 0; i < count; i++) toGoal[i] = long.MaxValue;
        for (int i = 0; i < count; i++)
        {
            if (nodeStatus[i] == Win)
            {
                toGoal[i] = 0;
                heap.Push(0, i);
            }
        }
        while (heap.Count > 0)
        {
            heap.Pop(out long d, out int v);
            if (d > toGoal[v]) continue;
            for (int e = revHead[v]; e >= 0; e = revNext[e])
            {
                int u = revFrom[e];
                long nd = d + edgeCost[e];
                if (nd < toGoal[u])
                {
                    toGoal[u] = nd;
                    heap.Push(nd, u);
                }
            }
        }

        long worst = 0;
        for (int i = 0; i < count; i++)
        {
            if (toGoal[i] == long.MaxValue)
            {
                result.loseStates++;
                if (nodeStatus[i] == Normal)
                    result.hiddenTrapStates++;
            }
            else if (toGoal[i] > worst)
            {
                worst = toGoal[i];
            }
        }
        result.worstDistance = (int)worst;

        Rate(result);
        return result;
    }

    /// <summary>把"最短路线"还原成一步一步的箭头：每次推蛋前，先走到蛋后面（最短的走法），再推一下。</summary>
    static string BuildRoute(int size, bool[] wall, List<int[]> nodeBoxes, List<int> nodeSnake, List<int> edgeInfo,
                             int[] parentNode, int[] parentEdge, int target)
    {
        var edges = new List<int>();      // 从开局到 target，经过的边
        var froms = new List<int>();      // 对应每条边的起点局面
        for (int v = target; v != 0; v = parentNode[v])
        {
            edges.Add(parentEdge[v]);
            froms.Add(parentNode[v]);
        }
        edges.Reverse();
        froms.Reverse();

        const string Arrows = "↑↓←→";
        var sb = new System.Text.StringBuilder();
        int cells = size * size;
        var prev = new int[cells];
        var queue = new int[cells];
        var blocked = new bool[cells];

        for (int k = 0; k < edges.Count; k++)
        {
            int u = froms[k];
            int box = edgeInfo[edges[k]] / 4, pushDir = edgeInfo[edges[k]] % 4;
            int behind = (box / size - Dy[pushDir]) * size + (box % size - Dx[pushDir]);

            for (int i = 0; i < cells; i++) { prev[i] = -2; blocked[i] = wall[i]; }
            foreach (int b in nodeBoxes[u]) blocked[b] = true;
            int head = 0, tail = 0;
            int snake = nodeSnake[u];
            prev[snake] = -1;
            queue[tail++] = snake;
            while (head < tail && prev[behind] == -2)
            {
                int c = queue[head++];
                for (int d = 0; d < 4; d++)
                {
                    int nx = c % size + Dx[d], ny = c / size + Dy[d];
                    if (nx < 0 || ny < 0 || nx >= size || ny >= size) continue;
                    int n = ny * size + nx;
                    if (prev[n] != -2 || blocked[n]) continue;
                    prev[n] = c;
                    queue[tail++] = n;
                }
            }

            var walk = new List<char>();
            for (int c = behind; prev[c] >= 0; c = prev[c])
            {
                int from = prev[c];
                for (int d = 0; d < 4; d++)
                    if (from % size + Dx[d] == c % size && from / size + Dy[d] == c / size)
                        walk.Add(Arrows[d]);
            }
            walk.Reverse();
            foreach (char ch in walk) sb.Append(ch);
            sb.Append(Arrows[pushDir]);
        }
        return sb.ToString();
    }

    // ---------------- 难度评分 ----------------
    static void Rate(LevelAnalysis r)
    {
        if (!r.solvable)
        {
            r.difficulty = 0;
            r.difficultyName = "无解";
            return;
        }

        float score = r.minPushes * 1.1f
                      + r.boxCount * 2f
                      + r.LoseRatio * 30f
                      + (float)Math.Log10(r.states + 1) * 3f;
        if (r.optimalRoutes <= 1.5) score += 6f;
        else if (r.optimalRoutes <= 4.5) score += 3f;
        r.score = score;

        string[] names = { "入门", "简单", "中等", "较难", "困难" };
        float[] cuts = { 20f, 32f, 46f, 62f };
        int level = 0;
        while (level < cuts.Length && score >= cuts[level]) level++;
        r.difficulty = level + 1;
        r.difficultyName = names[level];
    }

    static byte StatusOf(int[] boxes, bool[] goal, DeadlockChecker checker)
    {
        bool allOnGoal = boxes.Length > 0;
        foreach (int b in boxes)
            if (!goal[b]) { allOnGoal = false; break; }
        if (allOnGoal)
            return Win;
        return checker.Check(boxes) ? Flagged : Normal;
    }

    static string MakeKey(int[] boxes, int snake)
    {
        var chars = new char[boxes.Length + 1];
        for (int i = 0; i < boxes.Length; i++)
            chars[i] = (char)(boxes[i] + 1);
        chars[boxes.Length] = (char)(snake + 1);
        return new string(chars);
    }

    /// <summary>简单的最小堆：按 (代价, 编号) 取出代价最小的。</summary>
    sealed class MinHeap
    {
        readonly List<KeyValuePair<long, int>> m_Items = new List<KeyValuePair<long, int>>();
        public int Count => m_Items.Count;

        public void Push(long key, int value)
        {
            m_Items.Add(new KeyValuePair<long, int>(key, value));
            int i = m_Items.Count - 1;
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (m_Items[parent].Key <= m_Items[i].Key) break;
                (m_Items[parent], m_Items[i]) = (m_Items[i], m_Items[parent]);
                i = parent;
            }
        }

        public void Pop(out long key, out int value)
        {
            key = m_Items[0].Key;
            value = m_Items[0].Value;
            int last = m_Items.Count - 1;
            m_Items[0] = m_Items[last];
            m_Items.RemoveAt(last);
            int i = 0;
            while (true)
            {
                int l = i * 2 + 1, r = l + 1, smallest = i;
                if (l < m_Items.Count && m_Items[l].Key < m_Items[smallest].Key) smallest = l;
                if (r < m_Items.Count && m_Items[r].Key < m_Items[smallest].Key) smallest = r;
                if (smallest == i) break;
                (m_Items[smallest], m_Items[i]) = (m_Items[i], m_Items[smallest]);
                i = smallest;
            }
        }
    }
}
