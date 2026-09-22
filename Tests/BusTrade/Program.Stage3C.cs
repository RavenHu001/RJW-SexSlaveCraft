using System;
using System.Linq;
using rjw;
using RimWorld;
using SexSlaveCraft;
using Verse;
using Verse.AI;

internal static partial class Program
{
    /// <summary>逐例重置规则、事件登记和宠物随机边界，保证无跨例许可或通知残留。</summary>
    private static void ResetRestrictions()
    {
        SSCMod.settings = new Settings();
        DefDatabase<SSCRestrictionProfileDef>.AllDefsListForReading.Clear();
        SSCRestrictionJobGuard.Events.Clear(); SSCRestrictionJobGuard.Waits.Clear();
        PetSpecializationUtility.Gains.Clear(); Rand.ChanceCalls = Rand.Rolls = 0; Rand.ChanceResult = true;
        Find.TickManager.TicksGame = 100000;
    }

    /// <summary>创建宠物狗和动物搭档，是否提供床位决定生产代码选中的RJW普通或强制驱动。</summary>
    private static (Pawn dog, Pawn animal) DogPair(bool bed)
    {
        var p = People(); p.bus.IsBus = false; p.bus.Training.IsPetDogSpecialized = true;
        p.trader.RaceProps.Animal = true;
        if (bed) p.bus.ownership.OwnedBed = new Building_Bed();
        return (p.bus, p.trader);
    }

    /// <summary>覆盖完整交易/宠物入口的许可前置、方向、收益与冷却契约；不复制生产分支算法。</summary>
    private static void RunStage3CTests()
    {
        Run("3C交易预检拒绝保持双方原工作和成交成长", () =>
        {
            var p = People(); p.bus.Training.restrictionConfig.rules.consensualInitiation = SSCRestrictionValue.OwnerOnly;
            var a = new Job { def = new JobDef() }; var b = new Job { def = new JobDef() };
            p.bus.jobs.curJob = a; p.trader.jobs.curJob = b;
            Trade(); NoInteraction();
            Assert(p.bus.CurJob == a && p.trader.CurJob == b && ConditioningUtility.Gains.Count == 1, "拒绝不打断原工作、不撤销交易收益");
        });
        Run("3C公交车被动强制允许不放开主动交易分支", () =>
        {
            var p = People(); p.bus.Training.restrictionConfig.rules.consensualInitiation = SSCRestrictionValue.Deny;
            DefDatabase<SSCRestrictionProfileDef>.AllDefsListForReading.Add(new SSCRestrictionProfileDef
            { defName = "Bus", specialization = SexSlaveSpecializationType.Bus, forced = new SSCRestrictionOverrides { receiveConsensual = SSCRestrictionValue.Allow, receiveForced = SSCRestrictionValue.Allow } });
            Trade(); NoInteraction(); Assert(Rand.Rolls == 1, "只掷一次，不改走另一分支");
        });
        Run("3C商人发起强制分支受接收装备条目限制", () =>
        {
            var p = People(0); Rand.Roll = 11;
            p.bus.apparel.WornApparel.Add(new Apparel { def = new ThingDef { modExtensions = {
                new SSCRestrictionEquipmentExtension { forced = new SSCRestrictionOverrides { receiveForced = SSCRestrictionValue.Deny } } } } });
            Trade(); NoInteraction(); Assert(ConditioningUtility.Gains.Count == 1, "交易成长独立于行为发生");
        });
        Run("3C实际主人商人不被装备和损坏配置否决", () =>
        {
            var p = People(0); Rand.Roll = 11; p.bus.BoundMaster = p.trader; p.bus.Training.restrictionConfig.version = 999;
            Trade(); Started(p.trader, p.bus, xxx.RapeRandom);
        });
        Run("3C交易检查双方主动和被动配置", () =>
        {
            var p = People(); p.trader.Training.pawnIdentity = PawnIdentity.Slave;
            p.trader.BoundMaster = p.bus.BoundMaster;
            Trade(); NoInteraction();
            p.trader.Training.restrictionConfig.rules.receiveConsensual = true; Trade(); Started(p.bus, p.trader, xxx.quick_sex);
        });
        Run("3C交易登记原等待归属且预约变化仍可失败清理", () =>
        {
            var p = People();
            p.trader.jobs.OnRequest = job => { p.bus.Training.restrictionConfig.rules.consensualInitiation = SSCRestrictionValue.Deny; return true; };
            p.bus.jobs.OnRequest = job => SSCRestrictionPolicy.Evaluate(new SSCRestrictionRequest(p.bus, p.trader, SSCInteractionKind.Consensual, true)).Allowed;
            Trade();
            Assert(p.trader.CurJob == null && p.bus.CurJob == null && Messages.Entries.Count == 0, "预检后变化仍由正式预约拒绝，清理等待");
            Assert(SSCRestrictionJobGuard.Waits.Count == 1, "即使正式预约失败也已经登记等待归属");
        });
        Run("3C宠物床上事件拒绝保留工作成长和已消耗冷却", () =>
        {
            var p = DogPair(true); p.dog.Training.restrictionConfig.rules.consensualInitiation = SSCRestrictionValue.Deny;
            DogSpecializationUtility.NotifyAnimalTrainingCompleted(p.dog, p.animal);
            Assert(Pawn_JobTracker.Requests.Count == 0 && Messages.Entries.Count == 0 && PetSpecializationUtility.Gains.Count == 1, "拒绝只取消行为");
            Assert(p.dog.Training.lastDogAnimalInteractionTick == Find.TickManager.TicksGame && Rand.ChanceCalls == 1, "冷却由抽签消耗");
            DogSpecializationUtility.NotifyAnimalTrainingCompleted(p.dog, p.animal);
            Assert(Rand.ChanceCalls == 1 && PetSpecializationUtility.Gains.Count == 2, "第二次工作仍成长，但不重复抽签");
        });
        Run("3C宠物地面事件按真实强制驱动检查主动条目", () =>
        {
            var p = DogPair(false);
            DogSpecializationUtility.NotifyAnimalTamingAttempted(p.dog, p.animal);
            Assert(Pawn_JobTracker.Requests.Count == 0 && PetSpecializationUtility.Gains.Count == 1, "普通主动允许不放开强制事件");
            Find.TickManager.TicksGame += GenDate.TicksPerDay; p.dog.Training.restrictionConfig.rules.allowForcedInitiation = true;
            DogSpecializationUtility.NotifyAnimalTamingAttempted(p.dog, p.animal);
            Assert(p.dog.CurJob?.def == xxx.bestiality && Messages.Entries.Count == 0, "许可开启后调度，尚无开始提示");
        });
        Run("3C宠物事件直接启动且拒绝不改选强制路径", () =>
        {
            var p = DogPair(true); p.dog.jobs.QueueOrderedJobs = true;
            DogSpecializationUtility.NotifyAnimalTrainingCompleted(p.dog, p.animal);
            Assert(p.dog.CurJob?.def == xxx.bestialityForFemale && Messages.Entries.Count == 0, "Shift不会延后事件");
            Assert(SSCRestrictionJobGuard.Events[p.dog.CurJob].Source == SSCRestrictionEvent.Dog, "事件绑定到实际Job");
        });
        Run("3C宠物身体不合格不调度，抽签失败仍消费冷却", () =>
        {
            var p = DogPair(true); p.dog.CanAnimalSex = false;
            DogSpecializationUtility.NotifyAnimalTrainingCompleted(p.dog, p.animal);
            Assert(Pawn_JobTracker.Requests.Count == 0 && Rand.ChanceCalls == 1, "正常身体条件保留");
            Find.TickManager.TicksGame += GenDate.TicksPerDay; Rand.ChanceResult = false;
            DogSpecializationUtility.NotifyAnimalTrainingCompleted(p.dog, p.animal);
            Assert(Rand.ChanceCalls == 2 && p.dog.Training.lastDogAnimalInteractionTick == Find.TickManager.TicksGame, "骰子失败也不重试");
        });
        Run("3C宠物行为收益只在真实处理通知时发放", () =>
        {
            var p = DogPair(true); DogSpecializationUtility.NotifyAnimalTrainingCompleted(p.dog, p.animal);
            Assert(PetSpecializationUtility.Gains.Count == 1, "收到任务只有工作成长");
            DogSpecializationUtility.NotifyAnimalInteractionProcessed(new SexProps { pawn = p.dog, partner = p.animal });
            Assert(PetSpecializationUtility.Gains.Count == 2 && PetSpecializationUtility.Gains.Last() > PetSpecializationUtility.Gains.First(), "真实行为通知才提供行为成长");
        });
        Run("3C仅有性奴身份但未绑定时不启用个人限制", () =>
        {
            var p = People(); p.bus.BoundMaster = null;
            p.bus.Training.restrictionConfig.rules.consensualInitiation = SSCRestrictionValue.Deny;
            Trade(); Started(p.bus, p.trader, xxx.quick_sex);
        });
        Run("3C总开关停用仍保留交易及宠物资格门槛", () =>
        {
            var p = People(); p.bus.Training.restrictionConfig = null; SSCMod.settings.enableSexSlaveProtectionRules = false;
            Trade(); Started(p.bus, p.trader, xxx.quick_sex);
        });
    }
}
