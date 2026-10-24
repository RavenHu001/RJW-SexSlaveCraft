// AI 辅助生成
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>正常医疗账单的胃部开发启动入口；只在原版手术成功后建立身体记录。</summary>
    public sealed class Recipe_StartStomachDevelopment : Recipe_Surgery
    {
        /// <summary>供原版菜单判断可枚举的身体目标；执行资格由报告、接单及结果入口分别复查。</summary>
        public override bool AvailableOnNow(Thing thing, BodyPartRecord part = null)
        {
            if (!base.AvailableOnNow(thing, part)) return false;
            BodyPartRecord stomach = SSCStomachDevelopmentUtility.FindStomach(thing as Pawn);
            // 健康页在获得拒绝报告后仍会检查部位布尔值；提前拒绝身份或研究会把原因一起隐藏。
            // 只允许当前有效胃部进入菜单候选；这里返回 true 不代表已获准创建或执行账单。
            return stomach != null && (part == null || part == stomach);
        }

        /// <summary>保留原版准入，向医疗清单提供本手术当前不可用的本地化原因。</summary>
        public override AcceptanceReport AvailableReport(Thing thing, BodyPartRecord part = null)
        {
            // 保留原版年龄、种族等基础筛选；本配方没有性别或妊娠限制。
            if (!base.AvailableOnNow(thing, part)) return false;
            return SSCStomachSurgeryUtility.RecipientReport(thing as Pawn, part);
        }

        /// <summary>枚举当前有效 Stomach，保留可显示拒绝原因的菜单项；不提供生殖器官或全身目标。</summary>
        public override IEnumerable<BodyPartRecord> GetPartsToApplyOn(Pawn pawn, RecipeDef recipe)
        {
            BodyPartRecord stomach = SSCStomachDevelopmentUtility.FindStomach(pawn);
            // 不在此重复检查身份、研究或口部资格；原版菜单用 AvailableReport 将该项设为不可点击。
            if (stomach != null && AvailableOnNow(pawn, stomach)) yield return stomach;
        }

        /// <summary>复查资格、执行原版成败，再以 1% 建立开发；不自行麻醉或消费材料。</summary>
        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            // 旧账单或手术期间的身体变化不得落地到其他部位；资格失效不另加失败惩罚。
            var report = SSCStomachSurgeryUtility.RecipientReport(pawn, part);
            if (!report.Accepted) { Reject(pawn, report.Reason); return; }
            if (part == null) { Reject(pawn, "SSC_StomachDevelopment_MissingStomach".Translate()); return; }
            report = SSCStomachSurgeryUtility.DoctorReport(billDoer, pawn);
            if (!report.Accepted) { Reject(pawn, report.Reason); return; }
            // SurgeryFlesh 继承原版质量、药物效力、失败伤口和反馈；本配方直接死亡概率为 0。
            if (CheckSurgeryFail(billDoer, pawn, ingredients, part, bill)) return;
            // 原版结果回调也可能改变身体；成功创建之前再确认身份、器官及目标胃部。
            report = SSCStomachSurgeryUtility.RecipientReport(pawn, part);
            if (!report.Accepted) { Reject(pawn, report.Reason); return; }
            if (!SSCStomachDevelopmentUtility.TryCreate(pawn, out _, out string reason))
            { Reject(pawn, reason); return; }
            // 只有创建成功才报告已开始；阶段属性由身体 Hediff 直接提供。
            TaleRecorder.RecordTale(TaleDefOf.DidSurgery, billDoer, pawn);
            // 成功通知的句式放入语言资源，初始进度由生产常量格式化后填入，避免文案另维护一份数值。
            string progress = (SSCStomachDevelopmentUtility.InitialProgress * 100f).ToString("0.##") + "%";
            Messages.Message("SSC_StomachDevelopment_Started".Translate(progress), pawn, MessageTypeDefOf.PositiveEvent);
        }

        /// <summary>报告启动未完成的原因，不发放心情、伤口或开发记录。</summary>
        private static void Reject(Pawn pawn, string reason)
            => Messages.Message(reason, pawn, MessageTypeDefOf.RejectInput, false);
    }
}
