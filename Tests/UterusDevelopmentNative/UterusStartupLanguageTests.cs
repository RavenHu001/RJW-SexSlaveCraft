// AI 辅助生成：第 1～2 批创建、真实医疗菜单及结果消息的四语本地化回归。
using System.Xml.Linq;
using RimWorld;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    /// <summary>在旧手术宿主边界内执行生产报告、原生菜单和手术结果，不替换翻译或资格判断。</summary>
    private static void UterusStartupLanguageTests(string repo)
    {
        foreach (string language in new[] { "ChineseSimplified", "ChineseTraditional", "English", "Russian" })
        {
            Check("startup localization creation reports: " + language, () =>
            {
                LoadBodyManagementLanguage(repo, language);
                Require(!SSCUterusDevelopmentUtility.TryCreate(null, out _, out string reason));
                Equal("SSC_UterusDevelopment_CannotStart".Translate().ToString(), reason);
                var pawn = NewPawn(); var record = Start(pawn);
                Require(!SSCUterusDevelopmentUtility.TryCreate(pawn, out _, out reason));
                Equal("SSC_UterusDevelopment_AlreadyStarted".Translate().ToString(), reason);
                Near(.01f, record.Severity);
                var withoutTorso = SurgeryPawn(); withoutTorso.def.race.body = new BodyDef {
                    defName = "StartupLanguageNoTorso", corePart = new BodyPartRecord { def = new BodyPartDef { defName = "Core" } } };
                withoutTorso.def.race.body.ResolveReferences();
                Require(!SSCUterusDevelopmentUtility.TryCreate(withoutTorso, out _, out reason));
                Equal("SSC_UterusDevelopment_MissingTorso".Translate().ToString(), reason);
                var original = development.hediffClass;
                try
                {
                    development.hediffClass = typeof(Hediff);
                    Require(!SSCUterusDevelopmentUtility.TryCreate(NewPawn(), out _, out reason));
                    Equal("SSC_UterusDevelopment_ConfigUnavailable".Translate().ToString(), reason);
                }
                finally { development.hediffClass = original; }
            });
            Check("startup localization native health menu and doctor reports: " + language, () =>
            {
                LoadBodyManagementLanguage(repo, language);
                foreach (var example in new[] { ("identity", "SSC_UterusDevelopment_SlaveIdentityRequired"),
                    ("pregnant", "SSC_UterusDevelopment_PregnancyBlocked"), ("organ", "SSC_UterusDevelopment_MissingOrgan"),
                    ("started", "SSC_UterusDevelopment_AlreadyStarted"), ("underage", "SSC_UterusDevelopment_AdultFemaleRequired") })
                {
                    var patient = SurgeryPawn(); var bill = SurgeryBill(patient);
                    if (example.Item1 == "underage") patient.ageTracker.AgeBiologicalTicks = 17L * GenDate.TicksPerYear;
                    else ChangeSurgeryPatient(patient, example.Item1, bill);
                    var report = SSCUterusSurgeryUtility.RecipientReport(patient);
                    Require(!report.Accepted); Equal(example.Item2.Translate().ToString(), report.Reason);
                    var option = NativeSurgeryMenu(patient).Single(); Require(option.Disabled && option.Label.Contains(report.Reason));
                    Require(!option.Label.Contains("SSC_UterusDevelopment_"));
                }
                var host = SurgeryPawn(); var doctor = SurgeryPawn(PawnIdentity.Unset);
                doctor.skills.GetSkill(SkillDefOf.Medicine).Level = 9;
                var doctorReport = SSCUterusSurgeryUtility.DoctorReport(doctor, host);
                string expected = XDocument.Load(Path.Combine(repo, "1.7/Languages", language, "Keyed/SSC_Cosmetic.xml"))
                    .Root.Element("SSC_Cosmetic_MedicineRequired").Value;
                Require(!doctorReport.Accepted); Equal(string.Format(expected, 10), doctorReport.Reason);
                Require(!doctorReport.Reason.Contains("{0}"));
                Equal("SSC_UterusDevelopment_DoctorRequired".Translate().ToString(), SSCUterusSurgeryUtility.DoctorReport(null, host).Reason);
            });
            Check("startup localization real success and rejection messages: " + language, () =>
            {
                LoadBodyManagementLanguage(repo, language);
                var patient = SurgeryPawn(); var doctor = SurgeryPawn(PawnIdentity.Unset); var bill = SurgeryBill(patient);
                ResetSurgeryResult(1f);
                try
                {
                    uterusSurgery.Worker.ApplyOnPawn(patient, bill.Part, doctor, new(), bill);
                    string started = "SSC_UterusDevelopment_Started".Translate("1%");
                    Require(surgeryMessages.Count(text => text == started) == 1);
                    Near(.01f, SSCUterusDevelopmentUtility.Find(patient).Severity); Equal(1, surgeryRolls);
                    uterusSurgery.Worker.ApplyOnPawn(patient, bill.Part, doctor, new(), bill);
                    Require(surgeryMessages.Count(text => text == started) == 1);
                    Require(surgeryMessages.Contains("SSC_UterusDevelopment_AlreadyStarted".Translate().ToString()));
                    Require(surgeryMessages.All(text => !text.Contains("SSC_UterusDevelopment_") && !text.Contains("{0}")));
                    Equal(1, surgeryRolls);
                }
                finally { forcedQuality = null; }
            });
        }
        // 后续既有用例仍以中文断言原因；语言恢复不改变任何生产身体或研究状态。
        LoadBodyManagementLanguage(repo, "ChineseSimplified");
    }
}
