using UnityEngine;

/// <summary>
/// 方向键 / WASD，以及按住后连续走的节奏。关卡和创意工坊共用这一套，避免两处抄一份。
/// </summary>
public static class GridMoveInput
{
    public const float HoldDelay = 2f;            // 按住多久后开始连续走
    public const float RepeatInterval = 0.56f;    // 连续走时每一步的间隔
    public const float RepeatSlideSpeed = 0.5f;   // 连续走时滑过去的速度是点按的一半

    public static readonly KeyCode[][] DirKeys =
    {
        new[] { KeyCode.UpArrow, KeyCode.W },
        new[] { KeyCode.DownArrow, KeyCode.S },
        new[] { KeyCode.LeftArrow, KeyCode.A },
        new[] { KeyCode.RightArrow, KeyCode.D },
    };

    public static readonly GridPos[] DirVectors =
    {
        new GridPos(0, 1), new GridPos(0, -1), new GridPos(-1, 0), new GridPos(1, 0),
    };

    static bool AnyKeyDown(KeyCode[] keys)
    {
        foreach (var k in keys)
            if (Input.GetKeyDown(k)) return true;
        return false;
    }

    public static bool AnyKey(KeyCode[] keys)
    {
        foreach (var k in keys)
            if (Input.GetKey(k)) return true;
        return false;
    }

    public static int ReadPressedDir()
    {
        for (int d = 0; d < DirKeys.Length; d++)
            if (AnyKeyDown(DirKeys[d])) return d;
        return -1;
    }
}

/// <summary>跟踪当前按住的方向，以及是否已经进入“连续走”。</summary>
public sealed class MoveHold
{
    public int Dir { get; private set; } = -1;
    public bool Repeating { get; private set; }

    float m_Start;
    float m_Next;

    public void Reset()
    {
        Dir = -1;
        Repeating = false;
    }

    /// <summary>
    /// 这一帧该不该走一步：刚按下立刻走；按住超过 HoldDelay 之后每隔 RepeatInterval 再走。
    /// allowRepeat 为 false 时只响应按下（关卡前几关还没解锁长按）。
    /// 返回方向下标，没有要走的步则 -1。
    /// </summary>
    public int Poll(bool allowRepeat)
    {
        int pressed = GridMoveInput.ReadPressedDir();
        if (pressed >= 0)
        {
            Dir = pressed;
            m_Start = Time.unscaledTime;
            Repeating = false;
            return pressed;
        }

        if (Dir < 0)
            return -1;

        if (!GridMoveInput.AnyKey(GridMoveInput.DirKeys[Dir]))
        {
            Reset();
            return -1;
        }

        if (!allowRepeat)
            return -1;

        float now = Time.unscaledTime;
        if (!Repeating && now - m_Start >= GridMoveInput.HoldDelay)
        {
            Repeating = true;
            m_Next = now;
        }
        if (Repeating && now >= m_Next)
        {
            m_Next = now + GridMoveInput.RepeatInterval;
            return Dir;
        }
        return -1;
    }
}
