// AI 辅助生成：修复医生门槛及原版健康页禁用项的专项回归，不复制被测资格或菜单生成逻辑。
using HarmonyLib;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static readonly Dictionary<Pawn, bool> controlledMedicalMechs = new();
    private static bool queryingSurgeryMenu;
    private static List<FloatMenuOption> surgeryMenu;

    /// <summary>安装监管状态与 GUI 边界，实际执行原版配方技能检查和健康页菜单生成。</summary>
    private static void SurgeryAdmissionRegressionTests(Harmony harmony)
    {
        // 独立宿主不启动地图及机械师监管系统；只提供该前提，原版技能与启用工作判定继续执行。
        harmony.Patch(AccessTools.PropertyGetter(typeof(Pawn), nameof(Pawn.IsColonyMechPlayerControlled)),
            prefix: new HarmonyMethod(typeof(Program), nameof(MedicalMechControlBoundary)));
        // 在账单列表开始绘制前调用它收到的原版菜单工厂，不运行字体、窗口或渲染器。
        harmony.Patch(AccessTools.Method(typeof(BillStack), nameof(BillStack.DoListing)),
            prefix: new HarmonyMethod(typeof(Program), nameof(CaptureSurgeryMenu)));
        harmony.Patch(AccessTools.Method(typeof(RecipeDef), nameof(RecipeDef.PotentiallyMissingIngredients)),
            prefix: new HarmonyMethod(typeof(Program), nameof(MenuIngredientBoundary)));
        // FloatMenuOption 的标签及 action 保留原版赋值，仅隔离依赖字体设备的宽高测量。
        harmony.Patch(AccessTools.Method(typeof(FloatMenuOption), nameof(FloatMenuOption.SetSizeMode)),
            prefix: new HarmonyMethod(typeof(Program), nameof(Skip)));
        DoctorSkillRegressionTests();
        HealthMenuRegressionTests();
    }

    /// <summary>创建无 skills 跟踪器的己方医护机械体；监管状态仅作为测试环境前提提供。</summary>
    private static Pawn MedicalMech(int skill = 10, bool controlled = true, bool medicalWork = true)
    {
        var doctor = SurgeryPawn(PawnIdentity.Unset); doctor.skills = null;
        FleshTypeDefOf.Mechanoid ??= new FleshTypeDef { defName = "Mechanoid" };
        AccessTools.Field(typeof(RaceProperties), "fleshType").SetValue(doctor.RaceProps, FleshTypeDefOf.Mechanoid);
        AccessTools.Field(typeof(Thing), "factionInt").SetValue(doctor, surgeryPlayer);
        doctor.RaceProps.mechFixedSkillLevel = skill;
        doctor.RaceProps.mechEnabledWorkTypes = medicalWork
            ? new() { new WorkTypeDef { defName = "Doctor", relevantSkills = new() { SkillDefOf.Medicine } } }
            : new();
        controlledMedicalMechs.Add(doctor, controlled);
        Require(doctor.IsColonyMech); return doctor;
    }

    /// <summary>兼顾受控、低技能、未启用医疗工作及非机械体无技能等拒绝分支。</summary>
    private static void DoctorSkillRegressionTests()
    {
        Check("fix: controlled medical mech with native fixed medicine ten may accept surgery", () =>
        {
            var patient = SurgeryPawn(); var doctor = MedicalMech(); var bill = SurgeryBill(patient);
            Require(doctor.skills == null); Require(uterusSurgery.PawnSatisfiesSkillRequirements(doctor));
            Require(SSCUterusSurgeryUtility.DoctorReport(doctor, patient).Accepted);
            Require(bill.PawnAllowedToStartAnew(doctor));
        });
        foreach (string rejection in new[] { "skill nine", "uncontrolled", "no medical work" })
            Check("fix: native mechanical doctor requirement still rejects " + rejection, () =>
            {
                var doctor = MedicalMech(rejection == "skill nine" ? 9 : 10,
                    rejection != "uncontrolled", rejection != "no medical work");
                var patient = SurgeryPawn(); Require(!uterusSurgery.PawnSatisfiesSkillRequirements(doctor));
                Require(!SSCUterusSurgeryUtility.DoctorReport(doctor, patient).Accepted);
                Require(!SurgeryBill(patient).PawnAllowedToStartAnew(doctor));
            });
        Check("fix: ordinary pawn with no skills remains rejected", () =>
        {
            var doctor = SurgeryPawn(PawnIdentity.Unset); doctor.skills = null;
            Require(!SSCUterusSurgeryUtility.DoctorReport(doctor, SurgeryPawn()).Accepted);
        });
        Check("fix: disabled human medical skill remains rejected", () =>
        {
            var doctor = SurgeryPawn(PawnIdentity.Unset); var skill = doctor.skills.GetSkill(SkillDefOf.Medicine);
            // 只给原版技能缓存提供已禁用的状态；检查仍经实际 GetLevel/PawnSatisfies，不替换生产返回值。
            AccessTools.Field(typeof(SkillRecord), "cachedTotallyDisabled").SetValue(skill, BoolUnknown.True);
            Require(!SSCUterusSurgeryUtility.DoctorReport(doctor, SurgeryPawn()).Accepted);
        });
        Check("fix: qualified mechanical doctor can complete successful native surgery", () =>
        {
            var patient = SurgeryPawn(); var bill = SurgeryBill(patient); ResetSurgeryResult(1);
            try
            {
                uterusSurgery.Worker.ApplyOnPawn(patient, bill.Part, MedicalMech(), new(), bill);
                Equal(1, surgeryRolls); Near(.01f, SSCUterusDevelopmentUtility.Find(patient).Severity);
                Near(0, Factor(patient)); Equal(1, tales);
            }
            finally { forcedQuality = null; }
        });
    }

    /// <summary>直接进入原版健康页菜单工厂，确认拒绝项存在且不可点击，并继续拒绝账单执行。</summary>
    private static void HealthMenuRegressionTests()
    {
        foreach (string rejection in new[] { "pregnant", "identity", "organ", "started", "underage" })
            Check("fix: native health menu shows disabled surgery with reason: " + rejection, () =>
            {
                var patient = SurgeryPawn(); var bill = SurgeryBill(patient);
                if (rejection == "underage") patient.ageTracker.AgeBiologicalTicks = 17L * GenDate.TicksPerYear;
                else ChangeSurgeryPatient(patient, rejection, bill);
                var report = uterusSurgery.Worker.AvailableReport(patient);
                Require(!report.Accepted && !report.Reason.NullOrEmpty());
                var option = NativeSurgeryMenu(patient).Single();
                Require(option.Disabled && option.Label.Contains(report.Reason), option.Label);
                Require(!bill.PawnAllowedToStartAnew(SurgeryPawn(PawnIdentity.Unset)));
                // 菜单布尔值此时仅表示有效身体候选，不能代替接单或结果入口的完整资格检查。
                Require(uterusSurgery.Worker.AvailableOnNow(patient, patient.RaceProps.body.corePart));
                ResetSurgeryResult(1);
                try
                {
                    uterusSurgery.Worker.ApplyOnPawn(patient, bill.Part, SurgeryPawn(PawnIdentity.Unset), new(), bill);
                    Equal(0, surgeryRolls); Equal(0, tales);
                }
                finally { forcedQuality = null; }
            });
        Check("fix: native health menu has one enabled option for eligible body", () =>
        {
            var patient = SurgeryPawn(); var option = NativeSurgeryMenu(patient).Single();
            Require(!option.Disabled); Require(SSCUterusDevelopmentUtility.Find(patient) == null);
        });
        Check("fix: no available Torso yields no fabricated health menu target", () =>
        {
            var patient = SurgeryPawn(); patient.def.race.body = new BodyDef { defName = "NoTorsoMenu",
                corePart = new BodyPartRecord { def = new BodyPartDef { defName = "Core" } } };
            patient.def.race.body.ResolveReferences();
            Require(!NativeSurgeryMenu(patient).Any()); Require(!uterusSurgery.Worker.AvailableOnNow(patient));
        });
        Check("fix: foreign body part still fails structural and execution checks", () =>
        {
            var patient = SurgeryPawn(); var part = new BodyPartRecord();
            Require(!uterusSurgery.Worker.AvailableOnNow(patient, part));
            Require(!uterusSurgery.Worker.AvailableReport(patient, part).Accepted);
        });
    }

    /// <summary>让原版 DrawMedOperationsTab 构造选项；仅隔离缺少地图库存和 GUI 绘制的边界。</summary>
    private static List<FloatMenuOption> NativeSurgeryMenu(Pawn patient)
    {
        patient.def.recipes = new() { uterusSurgery };
        queryingSurgeryMenu = true; surgeryMenu = null;
        try
        {
            AccessTools.Method(typeof(HealthCardUtility), "DrawMedOperationsTab").Invoke(null,
                new object[] { new UnityEngine.Rect(0, 0, 600, 600), patient, patient, 0f });
            Require(surgeryMenu != null); return surgeryMenu;
        }
        finally { queryingSurgeryMenu = false; }
    }

    /// <summary>提供夹具机械体的监管状态，不覆盖普通医生或其他 Pawn 的原版查询。</summary>
    private static bool MedicalMechControlBoundary(Pawn __instance, ref bool __result)
    {
        if (!controlledMedicalMechs.TryGetValue(__instance, out bool controlled)) return true;
        __result = controlled; return false;
    }

    /// <summary>执行实际健康页传入的原版工厂并保存选项，不模拟菜单过滤或标签拼接。</summary>
    private static bool CaptureSurgeryMenu(Func<List<FloatMenuOption>> recipeOptionsMaker, ref Bill __result)
    {
        if (!queryingSurgeryMenu) return true;
        surgeryMenu = recipeOptionsMaker(); __result = null; return false;
    }

    /// <summary>本组只验证资格提示，视为库存齐全；真实材料登记与消费仍由既有专项覆盖。</summary>
    private static bool MenuIngredientBoundary(RecipeDef __instance, ref IEnumerable<ThingDef> __result)
    {
        if (!queryingSurgeryMenu || __instance != uterusSurgery) return true;
        __result = Enumerable.Empty<ThingDef>(); return false;
    }
}
