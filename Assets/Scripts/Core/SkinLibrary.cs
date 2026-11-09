using UnityEngine;

/// <summary>
/// 外观库：蛋用 Resources/Art/egg/e_0 … e_11，水晶用 Resources/Art/crystal/c_*。
/// 外观只影响画面，不影响规则。存档里只记“第几号外观”，所以以后换图、加图都不会弄坏旧关卡。
/// </summary>
public static class SkinLibrary
{
    public const int EggCount = 12;

    // 水晶文件名里的编号（c_5 这张图没有，所以跳过）
    static readonly int[] CrystalFiles = { 0, 1, 2, 3, 4, 6 };
    public static int CrystalCount => CrystalFiles.Length;

    static Sprite[] s_Eggs;
    static Sprite[] s_Crystals;

    // 展示盒：底座和升起来的玻璃盒，图在 Art/Board/ 里。
    static Sprite s_Pad, s_CaseBack, s_CaseFront;

    public static Sprite DisplayPad()
    {
        if (s_Pad == null)
        {
            s_Pad = Resources.Load<Sprite>("Art/Board/display_pad");
            if (s_Pad == null)
                Debug.LogError("找不到展示盒底座图: Art/Board/display_pad（Texture Type 需为 Sprite (2D and UI)）");
        }
        return s_Pad;
    }

    /// <summary>玻璃盒的"后半部分"：地板、后面的框、玻璃。画在蛋的下面。</summary>
    public static Sprite DisplayCaseBack()
    {
        if (s_CaseBack == null)
        {
            s_CaseBack = Resources.Load<Sprite>("Art/Board/display_case_back");
            if (s_CaseBack == null)
                Debug.LogError("找不到展示盒图: Art/Board/display_case_back（Texture Type 需为 Sprite (2D and UI)）");
        }
        return s_CaseBack;
    }

    /// <summary>玻璃盒的"前半部分"：前面的底边木板和正中间的角柱。画在蛋的上面，蛋就像放在盒子里面。</summary>
    public static Sprite DisplayCaseFront()
    {
        if (s_CaseFront == null)
        {
            s_CaseFront = Resources.Load<Sprite>("Art/Board/display_case_front");
            if (s_CaseFront == null)
                Debug.LogError("找不到展示盒图: Art/Board/display_case_front（Texture Type 需为 Sprite (2D and UI)）");
        }
        return s_CaseFront;
    }

    public static int WrapEgg(int kind) => ((kind % EggCount) + EggCount) % EggCount;
    public static int WrapCrystal(int kind) => ((kind % CrystalCount) + CrystalCount) % CrystalCount;

    public static Sprite Egg(int kind)
    {
        if (s_Eggs == null)
            s_Eggs = new Sprite[EggCount];
        int k = WrapEgg(kind);
        if (s_Eggs[k] == null)
        {
            s_Eggs[k] = Resources.Load<Sprite>("Art/egg/e_" + k);
            if (s_Eggs[k] == null)
                Debug.LogError("找不到蛋的图片: Art/egg/e_" + k + "（Texture Type 需为 Sprite (2D and UI)）");
        }
        return s_Eggs[k];
    }

    public static Sprite Crystal(int kind)
    {
        if (s_Crystals == null)
            s_Crystals = new Sprite[CrystalFiles.Length];
        int k = WrapCrystal(kind);
        if (s_Crystals[k] == null)
        {
            s_Crystals[k] = Resources.Load<Sprite>("Art/crystal/c_" + CrystalFiles[k]);
            if (s_Crystals[k] == null)
                Debug.LogError("找不到水晶的图片: Art/crystal/c_" + CrystalFiles[k] + "（Texture Type 需为 Sprite (2D and UI)）");
        }
        return s_Crystals[k];
    }
}
