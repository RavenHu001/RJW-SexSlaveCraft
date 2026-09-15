#if SSC_TEST_WITH_ANIMATIONS
using System.Collections.Generic;
using Verse;

namespace Rimworld_Animations
{
    public class GroupAnimationDef : Def
    {
        public bool Compatible = true;
        public List<Pawn> Order;
        public int Priority = 1;
        public int Checks;
        public List<Pawn> SeenParticipants;

        /// <summary>记录候选检查并返回预设结果；模拟外部接口，不复制框架的角色匹配算法。</summary>
        public bool CanAnimationBeUsed(List<Pawn> participants, out List<Pawn> order, out int priority)
        {
            Checks++;
            SeenParticipants = new List<Pawn>(participants);
            order = Order;
            priority = Priority;
            return Compatible;
        }
    }

    public class CompExtendedAnimator : ThingComp
    {
        /// <summary>提供生产反射入口所需的只读动画状态；本宿主不执行绘制。</summary>
        public bool IsAnimating => false;
    }

    public static class AnimationUtility
    {
        public static int Starts;
        public static GroupAnimationDef Selected;
        public static List<Pawn> Participants;
        public static Thing Anchor;
        public static Pawn LengthPawn;

        /// <summary>记录启动参数，供测试验证候选、角色顺序和锚点确实传入框架入口。</summary>
        public static void StartGroupAnimation(List<Pawn> participants, GroupAnimationDef animation, Thing anchor)
        {
            Starts++;
            Selected = animation;
            Participants = participants;
            Anchor = anchor;
        }

        /// <summary>返回固定动画时长，并记录生产入口查询的角色。</summary>
        public static int GetAnimationLength(Pawn pawn)
        {
            LengthPawn = pawn;
            return 120;
        }

        /// <summary>清除上一测试的外部框架调用记录。</summary>
        public static void Reset()
        {
            Starts = 0;
            Selected = null;
            Participants = null;
            Anchor = null;
            LengthPawn = null;
        }
    }
}
#endif
