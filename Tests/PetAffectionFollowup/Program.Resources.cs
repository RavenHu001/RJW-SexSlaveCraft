using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using SexSlaveCraft;

internal static partial class Program
{
    private static void RunResourceCases()
    {
        Run("亲昵后续JobDef及四语任务报告和开始消息完整，镜像一致", () =>
        {
            DirectoryInfo location = new(AppContext.BaseDirectory);
            while (location != null && !File.Exists(Path.Combine(location.FullName, "Sexslavecraft/SexSlaveCraft_Alpha.csproj"))) location = location.Parent;
            Assert(location != null, "无法定位仓库");
            string root = location.FullName;
            XElement job = XDocument.Load(Path.Combine(root, "Defs/JobDefs/SSC_PetAffectionFollowupJobDefs.xml")).Root.Elements().Single();
            Assert((string)job.Element("defName") == "SSC_Job_PetAffectionFollowup" &&
                (string)job.Element("driverClass") == typeof(JobDriver_PetAffectionFollowup).FullName &&
                (bool)job.Element("casualInterruptible") == false && ((string)job.Element("reportString")).Contains("TargetA"), "后续任务定义错误");
            foreach (string language in new[] { "ChineseSimplified", "ChineseTraditional", "English", "Russian" })
            {
                string report = "Languages/" + language + "/DefInjected/JobDef/SSC_PetAffectionFollowupJobDefs.xml";
                string keyed = "Languages/" + language + "/Keyed/SSC_PetAffectionFollowup.xml";
                foreach (string relative in new[] { report, keyed })
                    Assert(File.ReadAllBytes(Path.Combine(root, relative)).SequenceEqual(File.ReadAllBytes(Path.Combine(root, "Sexslavecraft", relative))),
                        "镜像不一致 " + relative);
                string text = XDocument.Load(Path.Combine(root, report)).Root.Element("SSC_Job_PetAffectionFollowup.reportString")?.Value;
                string message = XDocument.Load(Path.Combine(root, keyed)).Root.Element("SSC_Message_PetAffectionFollowupStarted")?.Value;
                Assert(!string.IsNullOrWhiteSpace(text) && text.Contains("TargetA") && !string.IsNullOrWhiteSpace(message) &&
                    message.Contains("{0}") && message.Contains("{1}"), "报告或开始提示缺少配对占位 " + language);
            }
        });
    }
}
