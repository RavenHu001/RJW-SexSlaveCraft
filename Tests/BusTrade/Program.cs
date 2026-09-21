using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;
using Verse.AI;
using rjw;

internal static partial class Program
{
    private static int passed, failed;

    /// <summary>运行真实生产交易后缀的行为回归并返回可供脚本使用的退出码。</summary>
    private static int Main()
    {
        Run("交易完成后缀指向 TradeDeal.TryExecute", PatchRegistration);
        Run("普通女性公交车与男性商人交易启动 Quickie", FemaleBusQuickie);
        Run("双方仅有接收能力仍可启动自愿任务", BothParticipantsMayReceive);
        Run("没有 Sex/Rape 定义时仍通过 RJW 定义启动自愿任务", MissingLegacyConsensualDefinition);
        Run("没有 Sex/Rape 定义时强制分支由商人向公交车启动 RandomRape", ForcedParticipants);
        Run("交易失败不触发任务、提示或成长", () => NoTrade(false, true));
        Run("没有实际交易不触发任务、提示或成长", () => NoTrade(true, false));
        Run("飞船交易不触发任务或成长", ShipTrade);
        Run("非公交车谈判者不触发任务或成长", NonBusTrade);
        Run("双方任一不可用时均不分配任务", UnavailableParticipants);
        Run("双方当前互动或不可中断工作不被交易打断", BusyParticipants);
        Run("按住队列修饰键也立即启动交易互动", QueueModifierDoesNotDefer);
        Run("等待方原有短 Wait 被刷新为完整等待任务", ExistingWaitIsRefreshed);
        Run("不同地图或超过 15 格时不分配任务", MapAndDistance);
        Run("15 格内向目标格预约寻路后启动", ReservationAndDistanceBoundary);
        Run("自愿分支任一角色无可用身体能力时跳过", ConsensualEligibility);
        Run("强制分支保留双方 RJW 能力门槛", ForcedEligibility);
        Run("等待方拒绝任务时不指派发起者、不显示成功", PartnerRejects);
        Run("发起方拒绝任务时清理本次等待、不显示成功", InitiatorRejects);
        Run("拒绝时保留等待方已切换的新任务", PreserveReplacementJob);
        Run("等待被延迟时仅移除本次排队等待", RemoveDeferredWait);
        Run("发起任务被延迟时清理自身排队任务及本次等待", RemoveDeferredInteraction);
        Run("失败时清理被移到队列的等待、保留新工作", RemoveRequeuedWait);
        Run("自愿与强制分支都正确处理任务拒绝", ForcedRejection);
        Run("真实 RJW 定义缺失时跳过并记录诊断", MissingActualDefinitions);
        Run("0% 与 10% 恶堕率概率边界保持不变", ProbabilityBoundaries);
        Run("缺少恶堕需求按零恶堕概率处理", MissingCorruptionNeed);
        Run("普通公交车按既有计分顺序与公式成长", NormalProgression);
        Run("终极公交车可启动任务但不继续成长", FinalProgression);
        Run("任务无法启动时仍保留交易成长规则", ProgressionWithoutJob);
        RunStage3CTests();
        Console.WriteLine($"结果：{passed}/{passed + failed} 项通过。");
        return failed == 0 ? 0 : 1;
    }

    /// <summary>重置模拟游戏全局状态，执行独立用例并报告失败原因。</summary>
    private static void Run(string name, Action test)
    {
        try { Reset(); test(); passed++; Console.WriteLine("通过：" + name); }
        catch (Exception error) { failed++; Console.WriteLine("失败：" + name + "\n" + error.Message); }
    }
    /// <summary>重置提示、任务、成长、定义和随机状态，避免重复场景间泄漏数据。</summary>
    private static void Reset()
    {
        ResetRestrictions();
        Messages.Entries.Clear();
        Pawn_JobTracker.Requests.Clear();
        ConditioningUtility.Gains.Clear();
        SSCLog.Entries.Clear();
        DefDatabase<JobDef>.Named.Clear();
        xxx.quick_sex = new JobDef { defName = "Quickie", driverClass = typeof(JobDriver_SexBaseInitiator) };
        xxx.RapeRandom = new JobDef { defName = "RandomRape", driverClass = typeof(JobDriver_Rape) };
        TrainingOutcomeUtility.Gain = 0.12f;
        TrainingOutcomeUtility.ScoredActor = TrainingOutcomeUtility.ScoredTarget = null;
        Rand.Roll = 1;
        TradeSession.playerNegotiator = null;
        TradeSession.trader = null;
    }
    /// <summary>条件失败时提供明确的行为断言错误。</summary>
    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    /// <summary>创建同地图、相距一格的女性公交车和男性商人，默认恶堕率达到必定自愿阈值。</summary>
    private static (Pawn bus, Pawn trader) People(float corruption = 0.2f)
    {
        var map = new Map();
        var bus = new Pawn
        {
            LabelShort = "Bus", Map = map, IsBus = true, gender = Gender.Female,
            CanFuck = false, CanBeFucked = true
        };
        bus.Training.pawnIdentity = PawnIdentity.Slave;
        bus.Training.restrictionConfig.rules.consensualInitiation = SSCRestrictionValue.Allow;
        bus.Training.restrictionConfig.rules.receiveForced = true;
        bus.needs.Corruption.CurLevel = corruption;
        var trader = new Pawn
        {
            LabelShort = "Trader", Map = map, gender = Gender.Male,
            CanFuck = true, CanBeFucked = false, Position = new IntVec3 { x = 1 }
        };
        TradeSession.playerNegotiator = bus;
        TradeSession.trader = trader;
        return (bus, trader);
    }
    /// <summary>从完整交易后缀入口运行用例，不调用内部辅助方法。</summary>
    private static void Trade() => Patch_TradeDeal_TryExecute_BusSex.Postfix(true, true);
    /// <summary>让旧版补丁具备可执行定义和身体能力，以独立暴露后续任务管理缺陷。</summary>
    private static void EnableLegacyJobLookup(Pawn bus)
    {
        bus.CanFuck = true;
        DefDatabase<JobDef>.Named["Sex"] = xxx.quick_sex;
        DefDatabase<JobDef>.Named["Rape"] = xxx.RapeRandom;
    }
    /// <summary>检查双方任务角色、目标、等待时长和请求先后。</summary>
    private static void Started(Pawn actor, Pawn partner, JobDef def)
    {
        Assert(actor.CurJob?.def == def, $"发起者应接收 {def.defName}");
        Assert(actor.CurJob.targetA.Thing == partner, "任务 A 目标必须为对方角色");
        Assert(partner.CurJob?.def == JobDefOf.Wait && partner.CurJob.expiryInterval == 600,
            "对方应在原地等待 600 ticks，供 RJW 驱动接管");
        Assert(Pawn_JobTracker.Requests.Count == 2 && Pawn_JobTracker.Requests[0].Pawn == partner
            && Pawn_JobTracker.Requests[1].Pawn == actor, "应先获得等待方的任务接收，再给发起方分配任务");
        Assert(Messages.Entries.Count == 0, "收到任务不代表实际发生");
        // 此套件把原生场景开始作为显式边界；真实Start、去重与存档另由InteractionProtection调用生产守卫验证。
        SSCRestrictionJobGuard.NotifyStarted(actor.CurJob);
        Assert(Messages.Entries.Count == 1, "模拟原生开始后应仅提示一次");
        Assert(Messages.Entries[0].Targets.Pawns.Contains(actor) && Messages.Entries[0].Targets.Pawns.Contains(partner),
            "提示应指向实际参与者");
    }
    /// <summary>检查没有任何任务分配或互动成功提示。</summary>
    private static void NoInteraction()
    {
        Assert(Pawn_JobTracker.Requests.Count == 0, "不可互动时不应分配任一任务");
        Assert(Messages.Entries.Count == 0, "不可互动时不应显示成功提示");
    }
    /// <summary>检查不属于公交车交易的情况完全没有相关副作用。</summary>
    private static void NoEffects()
    {
        NoInteraction();
        Assert(ConditioningUtility.Gains.Count == 0, "不满足交易前提时不得增加公交车成长");
    }
    /// <summary>核对 Harmony 后缀约定与补丁目标元数据。</summary>
    private static void PatchRegistration()
    {
        var patch = typeof(Patch_TradeDeal_TryExecute_BusSex);
        var target = patch.GetCustomAttribute<HarmonyPatch>();
        Assert(target?.Type == typeof(TradeDeal) && target.Method == "TryExecute", "交易补丁目标应保持正确");
        Assert(patch.GetMethod("Postfix") != null, "按 Harmony 约定应存在 Postfix 入口");
    }
    /// <summary>验证女性谈判者用Quickie向商人发起，发生提示由显式Start边界触发。</summary>
    private static void FemaleBusQuickie()
    {
        var p = People();
        Rand.Roll = 100;
        Trade();
        Started(p.bus, p.trader, xxx.quick_sex);
        Assert(Messages.Entries[0].Text.StartsWith("SSC_Message_TradeConsensualSex|"), "应显示自愿互动提示");
    }
    /// <summary>验证双方仅有接收身体能力时仍可使用原有普通互动路径。</summary>
    private static void BothParticipantsMayReceive()
    {
        var p = People();
        p.trader.CanFuck = false;
        p.trader.CanBeFucked = true;
        Trade();
        Started(p.bus, p.trader, xxx.quick_sex);
    }
    /// <summary>验证不依赖不存在的旧Sex定义，直接使用RJW真实定义引用。</summary>
    private static void MissingLegacyConsensualDefinition()
    {
        var p = People();
        p.bus.CanFuck = true; // Isolate the missing JobDef defect from the female eligibility defect.
        Assert(DefDatabase<JobDef>.GetNamedSilentFail("Sex") == null, "本用例必须缺少旧 Sex 定义");
        Trade();
        Started(p.bus, p.trader, xxx.quick_sex);
    }
    /// <summary>验证强制分支的发起者是商人，目标是谈判者。</summary>
    private static void ForcedParticipants()
    {
        var p = People(0f);
        Rand.Roll = 11;
        Assert(DefDatabase<JobDef>.GetNamedSilentFail("Rape") == null, "本用例必须缺少旧 Rape 定义");
        Trade();
        Started(p.trader, p.bus, xxx.RapeRandom);
        Assert(Messages.Entries[0].Text.StartsWith("SSC_Message_TradeRapeOccurred|"), "应保持强制分支提示");
    }
    /// <summary>验证失败或空交易不调度任务、不产生消息或成长。</summary>
    private static void NoTrade(bool success, bool actual)
    {
        People();
        Patch_TradeDeal_TryExecute_BusSex.Postfix(success, actual);
        NoEffects();
    }
    /// <summary>验证飞船交易继续保持原有不触发范围。</summary>
    private static void ShipTrade()
    {
        People();
        TradeSession.trader = new TradeShip();
        Trade();
        NoEffects();
    }
    /// <summary>验证非公交车谈判者不进入该特化事件。</summary>
    private static void NonBusTrade()
    {
        var p = People();
        p.bus.IsBus = false;
        Trade();
        NoEffects();
    }
    /// <summary>逐个隔离参与者不可用状态，覆盖双方和两种概率分支。</summary>
    private static void UnavailableParticipants()
    {
        foreach (bool forced in new[] { false, true })
        foreach (bool changeBus in new[] { false, true })
        foreach (Action<Pawn> change in new Action<Pawn>[]
        {
            p => p.Dead = true, p => p.Downed = true, p => p.Drafted = true,
            p => p.InMentalState = true, p => p.Fighting = true,
            p => p.Spawned = false, p => p.jobs = null, p => p.Map = null
        })
        {
            Reset();
            var p = People(forced ? 0f : 0.2f);
            EnableLegacyJobLookup(p.bus);
            Rand.Roll = forced ? 11 : 1;
            change(changeBus ? p.bus : p.trader);
            Trade();
            NoInteraction();
        }
    }
    /// <summary>检查已有互动、玩家不可中断工作和强制完成工作均原样保留。</summary>
    private static void BusyParticipants()
    {
        foreach (bool changeBus in new[] { false, true })
        foreach (int kind in new[] { 0, 1, 2 })
        {
            Reset();
            var p = People();
            EnableLegacyJobLookup(p.bus);
            var busy = changeBus ? p.bus : p.trader;
            var previous = new Job { def = new JobDef { defName = "CurrentWork" } };
            busy.jobs.curJob = previous;
            if (kind == 0) busy.jobs.curDriver = new JobDriver_Sex();
            if (kind == 1) busy.jobs.Interruptible = false;
            if (kind == 2) previous.def.forceCompleteBeforeNextJob = true;
            Trade();
            NoInteraction();
            Assert(busy.CurJob == previous && busy.jobs.EndCalls == 0 && busy.jobs.StopCalls == 0,
                "忙碌角色当前工作必须完整保留");
        }
    }
    /// <summary>模拟有序任务被要求排队的边界，确认交易结果不会取决于玩家仍按着 Shift。</summary>
    private static void QueueModifierDoesNotDefer()
    {
        var p = People();
        EnableLegacyJobLookup(p.bus);
        p.bus.jobs.QueueOrderedJobs = p.trader.jobs.QueueOrderedJobs = true;
        Trade();
        Started(p.bus, p.trader, xxx.quick_sex);
    }
    /// <summary>让等待方已有快过期的同类任务，确认新的完整等待确实成为当前工作。</summary>
    private static void ExistingWaitIsRefreshed()
    {
        var p = People();
        EnableLegacyJobLookup(p.bus);
        var previous = new Job { def = JobDefOf.Wait, expiryInterval = 1 };
        p.trader.jobs.curJob = previous;
        Trade();
        Started(p.bus, p.trader, xxx.quick_sex);
        Assert(p.trader.CurJob != previous, "不能把旧 Wait 的存在误认作本次等待启动成功");
    }
    /// <summary>验证地图及距离门槛在任何任务中断之前检查。</summary>
    private static void MapAndDistance()
    {
        foreach (bool separateMap in new[] { true, false })
        {
            Reset();
            var p = People();
            if (separateMap) p.trader.Map = new Map();
            else p.trader.Position = new IntVec3 { x = 16 };
            Trade();
            NoInteraction();
        }
    }
    /// <summary>验证15格边界内的原有预约及寻路目标参数。</summary>
    private static void ReservationAndDistanceBoundary()
    {
        var p = People();
        p.trader.Position = new IntVec3 { x = 15 };
        Trade();
        Started(p.bus, p.trader, xxx.quick_sex);
        Assert(p.bus.ReachChecks == 1 && p.bus.LastReachTarget.Thing == p.trader
            && p.bus.LastPathEndMode == PathEndMode.OnCell && p.bus.LastDanger == Danger.Deadly,
            "应按 RJW 到达目标格所需的模式预约寻路");
        Reset();
        p = People();
        p.bus.Reachable = false;
        Trade();
        NoInteraction();
    }
    /// <summary>逐方验证普通事件身体条件不会被新许可代替。</summary>
    private static void ConsensualEligibility()
    {
        foreach (bool changeBus in new[] { false, true })
        {
            Reset();
            var p = People();
            var target = changeBus ? p.bus : p.trader;
            target.CanFuck = target.CanBeFucked = false;
            Trade();
            NoInteraction();
        }
    }
    /// <summary>逐方验证强制分支的原有RJW能力门槛。</summary>
    private static void ForcedEligibility()
    {
        foreach (bool changeBus in new[] { false, true })
        {
            Reset();
            var p = People(0f);
            Rand.Roll = 11;
            if (changeBus) p.bus.CanGetRaped = false;
            else p.trader.CanRape = false;
            Trade();
            NoInteraction();
        }
    }
    /// <summary>验证等待方不接收任务时不启动发起者，也不提前提示发生。</summary>
    private static void PartnerRejects()
    {
        var p = People();
        EnableLegacyJobLookup(p.bus);
        p.trader.jobs.AcceptJobs = false;
        Trade();
        Assert(Pawn_JobTracker.Requests.Count == 1 && Pawn_JobTracker.Requests[0].Pawn == p.trader,
            "等待被拒绝后不应尝试发起任务");
        Assert(p.bus.CurJob == null && Messages.Entries.Count == 0, "等待失败不应报告启动");
        Assert(SSCLog.Entries.Count > 0, "失败应留下可开启的详细诊断");
    }
    /// <summary>验证发起者不接收任务时撤销本事件等待。</summary>
    private static void InitiatorRejects()
    {
        var p = People();
        EnableLegacyJobLookup(p.bus);
        p.bus.jobs.AcceptJobs = false;
        Trade();
        Assert(Pawn_JobTracker.Requests.Count == 2 && p.trader.jobs.EndCalls == 1 && p.trader.CurJob == null,
            "发起者拒绝后应结束本次等待");
        Assert(p.bus.CurJob == null && Messages.Entries.Count == 0, "发起者拒绝不应报告启动");
    }
    /// <summary>验证失败清理不能结束另一方后来替换的新工作。</summary>
    private static void PreserveReplacementJob()
    {
        var p = People();
        EnableLegacyJobLookup(p.bus);
        var replacement = new Job { def = new JobDef { defName = "OtherWork" } };
        p.bus.jobs.OnRequest = job => { p.trader.jobs.curJob = replacement; return false; };
        Trade();
        Assert(p.trader.CurJob == replacement && p.trader.jobs.EndCalls == 0,
            "失败清理仅能结束本次等待，不能结束新任务");
        Assert(Messages.Entries.Count == 0, "任务失败时不得提示成功");
    }
    /// <summary>模拟原任务收尾将等待推迟到队列，确认失败不会留下稍后意外启动的等待。</summary>
    private static void RemoveDeferredWait()
    {
        var p = People();
        var unrelated = new Job { def = new JobDef { defName = "QueuedWork" } };
        var current = new Job { def = new JobDef { defName = "FinishingWork" } };
        p.trader.jobs.curJob = current;
        p.trader.jobs.jobQueue.Jobs.Add(unrelated);
        p.trader.jobs.OnRequest = job => { p.trader.jobs.jobQueue.Jobs.Add(job); return false; };
        Trade();
        Assert(p.trader.CurJob == current && p.trader.jobs.jobQueue.Jobs.SequenceEqual(new[] { unrelated }),
            "延迟等待应被移除，无关当前和排队工作应保留");
        Assert(p.bus.CurJob == null && Messages.Entries.Count == 0 && Pawn_JobTracker.Requests.Count == 1,
            "等待未启动时不得发起互动或提示成功");
    }
    /// <summary>模拟发起任务被推迟，确认不会在此次交易已失败后仍从队列开始。</summary>
    private static void RemoveDeferredInteraction()
    {
        var p = People();
        var unrelated = new Job { def = new JobDef { defName = "QueuedWork" } };
        p.bus.jobs.jobQueue.Jobs.Add(unrelated);
        p.bus.jobs.OnRequest = job => { p.bus.jobs.jobQueue.Jobs.Add(job); return false; };
        Trade();
        Assert(p.bus.jobs.jobQueue.Jobs.SequenceEqual(new[] { unrelated }), "仅清理本次未启动的发起任务");
        Assert(p.trader.CurJob == null && p.trader.jobs.EndCalls == 1 && Messages.Entries.Count == 0,
            "延迟发起任务应视为失败并释放等待方");
    }
    /// <summary>模拟发起失败时等待任务已被移到队列，清理残留同时保留其他逻辑的新任务。</summary>
    private static void RemoveRequeuedWait()
    {
        var p = People();
        var unrelated = new Job { def = new JobDef { defName = "QueuedWork" } };
        var replacement = new Job { def = new JobDef { defName = "ReplacementWork" } };
        p.trader.jobs.jobQueue.Jobs.Add(unrelated);
        p.bus.jobs.OnRequest = job =>
        {
            p.trader.jobs.jobQueue.Jobs.Add(p.trader.CurJob);
            p.trader.jobs.curJob = replacement;
            return false;
        };
        Trade();
        Assert(p.trader.jobs.jobQueue.Jobs.SequenceEqual(new[] { unrelated }), "被重新排队的本次等待也必须清理");
        Assert(p.trader.CurJob == replacement && p.trader.jobs.EndCalls == 0 && Messages.Entries.Count == 0,
            "清理队列不能结束等待方的新工作或误报成功");
    }
    /// <summary>验证强制分支任一端调度失败也精确清理。</summary>
    private static void ForcedRejection()
    {
        foreach (bool receiverRejects in new[] { false, true })
        {
            Reset();
            var p = People(0f);
            EnableLegacyJobLookup(p.bus);
            Rand.Roll = 11;
            (receiverRejects ? p.bus : p.trader).jobs.AcceptJobs = false;
            Trade();
            Assert(p.bus.CurJob == null && p.trader.CurJob == null && Messages.Entries.Count == 0,
                "强制分支失败时不得遗留等待或显示已启动");
            Assert(Pawn_JobTracker.Requests.Count == (receiverRejects ? 1 : 2), "任务接收顺序应与自愿分支一致");
        }
    }
    /// <summary>验证真实RJW定义缺失时跳过并写诊断，不回退旧定义。</summary>
    private static void MissingActualDefinitions()
    {
        foreach (bool forced in new[] { false, true })
        {
            Reset();
            People(forced ? 0f : 0.2f);
            Rand.Roll = forced ? 11 : 1;
            xxx.quick_sex = xxx.RapeRandom = null;
            Trade();
            NoInteraction();
            Assert(SSCLog.Entries.Count > 0, "真实定义缺失应提供诊断");
        }
    }
    /// <summary>检查已知概率边界的结果，不在测试中复刻生产概率公式。</summary>
    private static void ProbabilityBoundaries()
    {
        foreach (var sample in new[]
        {
            (0f, 10, "Quickie"), (0f, 11, "RandomRape"), (0f, 90, "RandomRape"), (0f, 91, "Forgiven"),
            (0.1f, 55, "Quickie"), (0.1f, 56, "RandomRape"), (0.1f, 95, "RandomRape"), (0.1f, 96, "Forgiven")
        })
        {
            Reset();
            var p = People(sample.Item1);
            Rand.Roll = sample.Item2;
            Trade();
            if (sample.Item3 == "Forgiven")
            {
                Assert(Pawn_JobTracker.Requests.Count == 0 && Messages.Entries.Count == 1
                    && Messages.Entries[0].Text.StartsWith("SSC_Message_TradeRejectedForgiven|"),
                    $"恶堕 {sample.Item1}、骰点 {sample.Item2} 应为原谅分支");
            }
            else Started(sample.Item3 == "Quickie" ? p.bus : p.trader,
                sample.Item3 == "Quickie" ? p.trader : p.bus,
                sample.Item3 == "Quickie" ? xxx.quick_sex : xxx.RapeRandom);
        }
    }
    /// <summary>验证缺少需求仍按原有零恶堕分支概率。</summary>
    private static void MissingCorruptionNeed()
    {
        var p = People();
        p.bus.needs = null;
        Rand.Roll = 11;
        Trade();
        Started(p.trader, p.bus, xxx.RapeRandom);
    }
    /// <summary>验证成功成交的普通成长计分顺序、上限和折半规则。</summary>
    private static void NormalProgression()
    {
        foreach (var sample in new[] { (0.12f, 0.22f), (0.15f, 0.175f), (0.5f, 0.175f) })
        {
            Reset();
            var p = People();
            TrainingOutcomeUtility.Gain = sample.Item1;
            Trade();
            Assert(ConditioningUtility.Gains.Count == 1 && ConditioningUtility.Gains[0].Pawn == p.bus
                && Math.Abs(ConditioningUtility.Gains[0].Gain - sample.Item2) < 0.00001f,
                "普通公交车成长应保持既有基础量、上限和折半规则");
            Assert(TrainingOutcomeUtility.ScoredActor == p.trader && TrainingOutcomeUtility.ScoredTarget == p.bus,
                "成长计分的角色顺序应保持商人在前、公交车在后");
        }
    }
    /// <summary>验证终极状态可触发互动，但不继续领取交易成长。</summary>
    private static void FinalProgression()
    {
        var p = People();
        p.bus.IsFinalBus = true;
        Trade();
        Started(p.bus, p.trader, xxx.quick_sex);
        Assert(ConditioningUtility.Gains.Count == 0 && TrainingOutcomeUtility.ScoredActor == null,
            "终极公交车不得再次计分或成长");
    }
    /// <summary>验证不可达导致行为未调度时，成功成交成长仍保留。</summary>
    private static void ProgressionWithoutJob()
    {
        var p = People();
        p.bus.Reachable = false;
        Trade();
        NoInteraction();
        Assert(ConditioningUtility.Gains.Count == 1 && Math.Abs(ConditioningUtility.Gains[0].Gain - 0.22f) < 0.00001f,
            "互动无法启动时交易成长仍应保持");
    }
}
