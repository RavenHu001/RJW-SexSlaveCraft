using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    /// <summary>直接运行生产资格代码，覆盖阶段 1 的数据定义、条件矩阵和状态优先级。</summary>
    private static void RunTrainerSpecializationTests(string repo)
    {
        Run("训导官枚举追加及三种 Hediff 定义一致", () =>
        {
            // 旧方向的序号是存档兼容边界；新方向只能追加在末尾。
            Assert((int)SexSlaveSpecializationType.PetRabbit == 5);
            Assert((int)SexSlaveSpecializationType.TrainerOfficer == 6);

            // 编译 C# 不会加载游戏 XML，所以单独解析实际定义并核对三种状态的名称。
            var xml = XDocument.Load(Path.Combine(repo, "Defs/HediffDefs/SSC_HediffDefs_TrainerSpecialization.xml"));
            string[] names = xml.Root.Elements("HediffDef")
                .Select(def => (string)def.Element("defName")).ToArray();
            Assert(names.Length == 3 && names.Distinct().Count() == 3);
            Assert(names.Contains("SSC_Hediff_TrainerOfficer")
                && names.Contains("SSC_Hediff_TrainerOfficer_Final")
                && names.Contains("SSC_Hediff_TrainerOfficer_FinalDisabled"));

            // 普通 Hediff 的显示阶段必须与资格代码使用相同的 20% 边界。
            var ordinary = xml.Root.Elements("HediffDef")
                .Single(def => (string)def.Element("defName") == "SSC_Hediff_TrainerOfficer");
            Assert(ordinary.Element("stages").Elements("li")
                .Any(stage => (string)stage.Element("minSeverity") == "0.20"));
        });

        Run("持续条件以当前 SSC 身份、自由殖民者、实际主人和锁链阶段为准", () =>
        {
            // 空角色、缺组件及错误 SSC 身份各有稳定原因，不能被当作未选择方向。
            Assert(!TrainerSpecializationUtility.MeetsContinuousConditions(null, out var reason)
                && reason == TrainerSpecializationFailure.MissingPawnOrTrainingComp);
            Pawn missing = Pawn(PawnIdentity.Slave); missing.Training = null;
            Assert(!TrainerSpecializationUtility.MeetsContinuousConditions(missing, out reason)
                && reason == TrainerSpecializationFailure.MissingPawnOrTrainingComp);
            Pawn master = Pawn(PawnIdentity.Master);
            Assert(!TrainerSpecializationUtility.MeetsContinuousConditions(master, out reason)
                && reason == TrainerSpecializationFailure.NotSexSlave);

            // 先验证实际绑定主人，再以锁链当前严重度测试第 2/3/4 阶段。
            // 即使历史恶堕达到最高值，当前锁链不足仍必须拒绝。
            Pawn slave = Pawn(PawnIdentity.Slave);
            Assert(!TrainerSpecializationUtility.MeetsContinuousConditions(slave, out reason)
                && reason == TrainerSpecializationFailure.NoBoundMaster);
            Assert(SSCBondUtility.Bind(master, slave));
            var chain = SSCBondUtility.GetChain(slave);
            chain.Severity = 0.4999f;
            slave.needs.Corruption.HighestCorruptionLevel = 1f;
            Assert(!TrainerSpecializationUtility.MeetsContinuousConditions(slave, out reason)
                && reason == TrainerSpecializationFailure.ChainStageTooLow);
            chain.Severity = 0.5f;
            Assert(TrainerSpecializationUtility.MeetsContinuousConditions(slave, out reason)
                && reason == TrainerSpecializationFailure.None);
            chain.Severity = 0.9f;
            Assert(TrainerSpecializationUtility.MeetsContinuousConditions(slave, out _));

            // 离图、倒地和工作暂停影响实际执行，不应擦除培养与终极状态。
            slave.Map = null; slave.Downed = true; slave.workSettings.Active = false;
            Assert(TrainerSpecializationUtility.MeetsContinuousConditions(slave, out _));

            // 分别覆盖原版奴隶、囚犯和非殖民者；它们都不在执行者范围内。
            slave.IsSlave = true;
            Assert(!TrainerSpecializationUtility.MeetsContinuousConditions(slave, out reason)
                && reason == TrainerSpecializationFailure.NotFreeColonist);
            slave.IsSlave = false; slave.IsPrisonerOfColony = true;
            Assert(!TrainerSpecializationUtility.MeetsContinuousConditions(slave, out reason)
                && reason == TrainerSpecializationFailure.NotFreeColonist);
            slave.IsPrisonerOfColony = false; slave.IsColonist = false;
            Assert(!TrainerSpecializationUtility.MeetsContinuousConditions(slave, out reason)
                && reason == TrainerSpecializationFailure.NotFreeColonist);

            // 保留锁链但去掉其实际主人，确保不会由其他关系回退补成绑定。
            slave.IsColonist = true; chain.LinkedPawn = null;
            Assert(!TrainerSpecializationUtility.MeetsContinuousConditions(slave, out reason)
                && reason == TrainerSpecializationFailure.NoBoundMaster);
        });

        Run("普通训导官仅当前方向达到 20% 后取得资格，历史与异常进度不授予资格", () =>
        {
            // 普通方向按当前进度跨过 20% 边界；达到 100% 仍只是普通完成。
            Pawn slave = QualifiedTrainerPawn();
            var comp = slave.Training;
            comp.SetSpecialization(SexSlaveSpecializationType.TrainerOfficer);
            comp.specializationProgress = 0.1999f;
            Assert(!TrainerSpecializationUtility.HasTrainerQualification(slave, out var reason)
                && reason == TrainerSpecializationFailure.BasicStageNotReached);
            comp.specializationProgress = 0.2f;
            Assert(TrainerSpecializationUtility.HasTrainerQualification(slave, out reason)
                && reason == TrainerSpecializationFailure.None);
            comp.specializationProgress = 1f;
            Assert(!TrainerSpecializationUtility.HasFinalRecord(slave));

            // 切走时公共组件会保存历史；历史进度不能在其他方向继续赋权，
            // 切回后则应恢复此前达到的基础资格。
            comp.SetSpecialization(SexSlaveSpecializationType.Cow);
            Assert(!TrainerSpecializationUtility.HasTrainerQualification(slave, out reason)
                && reason == TrainerSpecializationFailure.WrongSpecialization);
            comp.SetSpecialization(SexSlaveSpecializationType.TrainerOfficer);
            Assert(TrainerSpecializationUtility.HasTrainerQualification(slave, out _));

            // 损坏的非有限进度即使在数值比较中可能越过阈值，也必须安全失败。
            comp.specializationProgress = float.NaN;
            Assert(!TrainerSpecializationUtility.HasTrainerQualification(slave, out _));
            comp.specializationProgress = float.PositiveInfinity;
            Assert(!TrainerSpecializationUtility.HasTrainerQualification(slave, out _));
        });

        Run("有效终极跨方向任职，失去持续条件立即停用但保留完成记录", () =>
        {
            // 有效终极是独立于当前方向的完成记录；培养奶牛或无方向时仍可任职。
            Pawn slave = QualifiedTrainerPawn();
            slave.Training.SetSpecialization(SexSlaveSpecializationType.Cow);
            var final = new Hediff { def = SSCDefOf.SSC_Hediff_TrainerOfficer_Final };
            slave.health.hediffSet.hediffs.Add(final);
            Assert(TrainerSpecializationUtility.HasFinalRecord(slave));
            Assert(TrainerSpecializationUtility.HasActiveFinalEffect(slave));
            Assert(TrainerSpecializationUtility.HasTrainerQualification(slave, out _));
            slave.Training.SetSpecialization(SexSlaveSpecializationType.None);
            Assert(TrainerSpecializationUtility.HasTrainerQualification(slave, out _));

            // 模拟低频维护尚未将有效标记替换为禁用标记的瞬间：
            // 当前锁链退阶应立即拒绝资格，但读取过程不能删除唯一完成证据。
            SSCBondUtility.GetChain(slave).Severity = 0.3f;
            Assert(TrainerSpecializationUtility.HasFinalRecord(slave));
            Assert(!TrainerSpecializationUtility.HasActiveFinalEffect(slave));
            Assert(!TrainerSpecializationUtility.HasTrainerQualification(slave, out var reason)
                && reason == TrainerSpecializationFailure.ChainStageTooLow);
            Assert(slave.health.hediffSet.hediffs.Contains(final));
        });

        Run("禁用和双终极标记保留记录但均不给资格，查询不改写状态", () =>
        {
            // 即使当前普通方向已超过 20%，禁用终极标记仍优先阻止任职。
            Pawn slave = QualifiedTrainerPawn();
            slave.Training.SetSpecialization(SexSlaveSpecializationType.TrainerOfficer);
            slave.Training.specializationProgress = 0.5f;
            var disabled = new Hediff { def = SSCDefOf.SSC_Hediff_TrainerOfficer_FinalDisabled };
            slave.health.hediffSet.hediffs.Add(disabled);
            Assert(TrainerSpecializationUtility.HasFinalRecord(slave));
            Assert(!TrainerSpecializationUtility.HasActiveFinalEffect(slave));
            Assert(!TrainerSpecializationUtility.HasTrainerQualification(slave, out var reason)
                && reason == TrainerSpecializationFailure.FinalDisabled);

            // 双标记是待生命周期阶段归并的异常状态。查询必须保守拒绝，
            // 且不应在菜单或工作扫描期间偷偷修改 Hediff 列表。
            var final = new Hediff { def = SSCDefOf.SSC_Hediff_TrainerOfficer_Final };
            slave.health.hediffSet.hediffs.Add(final);
            int count = slave.health.hediffSet.hediffs.Count;
            Assert(!TrainerSpecializationUtility.HasActiveFinalEffect(slave));
            Assert(!TrainerSpecializationUtility.HasTrainerQualification(slave, out reason)
                && reason == TrainerSpecializationFailure.FinalDisabled);
            Assert(slave.health.hediffSet.hediffs.Count == count
                && slave.health.hediffSet.hediffs.Contains(disabled)
                && slave.health.hediffSet.hediffs.Contains(final));
        });
    }

    /// <summary>建立真实绑定并把当前锁链推进到刚达到“屈服性奴”的边界。</summary>
    private static Pawn QualifiedTrainerPawn()
    {
        // 使用生产 Bind 入口生成锁链与主人引用，避免测试自己伪造资格算法。
        Pawn master = Pawn(PawnIdentity.Master);
        Pawn slave = Pawn(PawnIdentity.Slave);
        Assert(SSCBondUtility.Bind(master, slave));
        SSCBondUtility.GetChain(slave).Severity = 0.5f;
        return slave;
    }
}
