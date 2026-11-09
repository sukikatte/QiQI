using static StoryDialogue;

/// <summary>开始游戏里的剧情台词：第一关的教学、通关后的对话、第二关出现水晶时的提醒。</summary>
public static class CampaignStory
{
    // 每一段剧情的编号（用来记录"看过了"，看过的不再自动弹出）
    public const string IntroId = "intro";
    public const string TutorialId = "tutorial";
    public const string AfterLevel1Id = "after1";
    public const string Level2Id = "level2";
    public const string Level6Id = "level6";
    public const string Level9Id = "level9";
    public const string WorkshopId = "workshop";
    public static readonly string[] AllIds = { IntroId, TutorialId, AfterLevel1Id, Level2Id, Level6Id, Level9Id, WorkshopId };

    /// <summary>第一关开始前：琦琦迎接玩家，蛋掉了一地，玩家答应帮忙。onEggsFall = "哦糟糕"那一句出现时，只让蛋掉下来。</summary>
    public static Line[] Intro(System.Action onEggsFall)
    {
        return new[]
        {
            Qiqi("嗨，我的朋友，欢迎来到我的收藏室！", Fx.Hop),
            Qiqi("快来看看我在森林里找到的蛋们——它们太美了，我都舍不得吃。", Fx.Hop),
            Qiqi("哦糟糕！怎么全都掉下来了？你们快别乱动啦！", Fx.Shake, onEggsFall),
            Qiqi("真对不起呀，我的朋友。我不在家的时候，调皮的蛋们全都滚得到处都是……我得先整理一下了。"),
            Me("既然这样，我来帮你一起收拾吧！"),
            Qiqi("那简直太好啦！你来给我当指挥吧！", Fx.Hop),
        };
    }

    /// <summary>进入第一关以后的弹窗：怎么操作、目标是什么、别走进死路、琦琦一次只推得动一颗蛋。</summary>
    public static Line[] Tutorial()
    {
        return new[]
        {
            Qiqi("准备好啦，我们开始吧！你来规划路线，我来负责推蛋！", Fx.Hop),
            Qiqi("想让我动起来，就按键盘上的 ↑ ↓ ← → 方向键，或者 W、A、S、D，我会朝那个方向走一步。"),
            Qiqi("我们的目标，是把所有的蛋都推进地上的收纳柜里。"),
            Qiqi("不过要当心，别把蛋推进死路！比如角落里，或者贴着边缘、那一边又没有收纳柜的地方——蛋一旦卡住，就再也推不出来啦。"),
            Qiqi("还有哦，我的力气不大，一次只推得动一颗蛋。好了，开始吧！", Fx.Hop),
        };
    }

    /// <summary>第一关通关后：琦琦和玩家都觉得好玩，琦琦提议再玩一关。</summary>
    public static Line[] AfterLevel1()
    {
        return new[]
        {
            Qiqi("没想到，收拾这帮小家伙还挺好玩的！", Fx.Hop),
            Me("是啊，我也玩得很开心！"),
            Qiqi("既然这么有趣，我们要不要再玩一会儿？我还有好多好玩的摆法，正好再布置一个新的给你看！", Fx.Hop),
            Me("好呀，来吧！"),
        };
    }

    /// <summary>第二关开始前：加入了水晶。</summary>
    public static Line[] Level2()
    {
        return new[]
        {
            Qiqi("好啦！这一次，我把我的水晶也摆进来了。", Fx.Hop),
            Qiqi("水晶是障碍物。"),
            Qiqi("水晶我可推不动哦，一定要绕开它！"),
        };
    }

    /// <summary>第六关开始前：告诉玩家可以长按方向键连续移动。</summary>
    public static Line[] Level6()
    {
        return new[]
        {
            Qiqi("越来越难了哦！", Fx.Hop),
            Qiqi("你现在可以长按方向键，让我连续走好几步啦！按住大约两秒，我就会接着往前挪。不过我走得不会太快，想停的时候松开就好。"),
        };
    }

    /// <summary>第九关开始前：告诉玩家可以按 E 撤回上一步。</summary>
    public static Line[] Level9()
    {
        return new[]
        {
            Qiqi("现在开始可以撤回上一步动作啦！", Fx.Hop),
            Qiqi("按 E 就能撤回。推错了按一下，我退回刚才那一步，不用从头再来哦。"),
        };
    }

    /// <summary>第十关通关后：解锁创意工坊。</summary>
    public static Line[] AfterLevel10()
    {
        return new[]
        {
            Qiqi("你真厉害！感不感兴趣，自己钻研几间收藏室呀？", Fx.Hop),
            Me("感兴趣！"),
            Qiqi("你去创意工坊看看，那是我给你准备的礼物呀。现在你可以自己创建关卡啦！", Fx.Hop),
        };
    }
}
