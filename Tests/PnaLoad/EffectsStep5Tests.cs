using System;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    /// <summary>直接验证真实来源到共享身体输入；完成认领及双向分发由 InteractionProtection 验证。</summary>
    private static void RunEffectsStep5Tests()
    {
        Run("第五步负荷未到末期仍基础加深已有成瘾0.01", () =>
        {
            var source = NewPawn(); AddPnaProducer(source); var recipient = PnaAddict(.2f); Raw(recipient, 1.8f);
            SSCPnaSexSupplyUtility.ApplyFromProducer(source, recipient);
            Near(.92, SSCPnaExposureUtility.GetLoad(recipient)); Near(.3, SSCPnaExposureUtility.GetNeed(recipient).CurLevel);
            Near(.71, SSCPnaExposureUtility.GetAddiction(recipient).Severity);
        });
        Run("第五步基础和末期成瘾合并一次写入", () =>
        {
            var source = NewPawn(); AddPnaProducer(source); var recipient = NewPawn(); Raw(recipient, 1.92f);
            var addiction = new SupplyAddiction { pawn = recipient, def = AddictionDef, Severity = .4f };
            recipient.health.hediffSet.hediffs.Add(addiction); recipient.needs.PnaNeed = .2f; addiction.Writes = 0;
            SSCPnaSexSupplyUtility.ApplyFromProducer(source, recipient);
            Near(.61, addiction.Severity); Equal(1, addiction.Writes);
        });
        Run("第五步首次建立成瘾不叠加本次基础和末期增量", () =>
        {
            var source = NewPawn(); AddPnaProducer(source); var recipient = NewPawn(); Raw(recipient, 1.96f);
            recipient.health.OnAdded = h => { if (SSCPnaExposureUtility.IsAddictionDef(h.def)) h.Severity = .35f; };
            Assert(SSCPnaSexSupplyUtility.ApplyFromProducer(source, recipient).AddictionCreated, "首次创建");
            Near(.35, SSCPnaExposureUtility.GetAddiction(recipient).Severity);
        });
        foreach (bool master in new[] { false, true })
            Run("第五步基础成瘾写入回调改变保护状态仍核对 master=" + master, () =>
            {
                var source = NewPawn(); AddPnaProducer(source); var recipient = NewPawn(); Raw(recipient, .4f);
                var addiction = new SupplyAddiction { pawn = recipient, def = AddictionDef, Severity = .4f };
                recipient.health.hediffSet.hediffs.Add(addiction); recipient.needs.PnaNeed = .2f;
                addiction.OnWrite = () =>
                {
                    if (master) recipient.IsMaster = true;
                    else Hediff_ChainOfSexSlave.AddToPawn(recipient, NewPawn());
                };
                SSCPnaSexSupplyUtility.ApplyFromProducer(source, recipient);
                Assert(SSCPnaExposureUtility.GetAddiction(recipient) == null && SSCPnaExposureUtility.GetNeed(recipient) == null, "保护后清成瘾需求");
                if (master) Assert(SSCPnaExposureUtility.GetBodyRecord(recipient) == null, "主人清药");
                else Assert(SSCPnaExposureUtility.IsRooted(recipient) && RootedRecord(recipient) != null, "性奴分流");
            });
        Run("第五步末期已有成瘾满值时加深被原版上限钳制", () =>
        {
            var source = NewPawn(); AddPnaProducer(source); var recipient = PnaAddict(.2f); Raw(recipient, 2f);
            SSCPnaExposureUtility.GetAddiction(recipient).Severity = 1f;
            SSCPnaSexSupplyUtility.ApplyFromProducer(source, recipient);
            Near(1, SSCPnaExposureUtility.GetAddiction(recipient).Severity);
        });
        Run("第五步每次真实供给仅加2%负荷和10%需求且不消费生产进度", () =>
        {
            var source = NewPawn(); var production = AddPnaProducer(source); production.RawSeverity = .63f;
            var recipient = PnaAddict(.2f); Raw(recipient, .4f);
            var result = SSCPnaSexSupplyUtility.ApplyFromProducer(source, recipient);
            Assert(result.Accepted && !result.AddictionCreated, "有效输入"); Near(.22, result.FinalLoad);
            Near(.3, SSCPnaExposureUtility.GetNeed(recipient).CurLevel); Near(.71, SSCPnaExposureUtility.GetAddiction(recipient).Severity);
            Near(.63f, production.Severity, 0); Near(0, SSCPnaExposureUtility.GetLoad(source));
        });
        Run("第五步接近满需求钳制，首次新需求沿用原版设满", () =>
        {
            var source = NewPawn(); AddPnaProducer(source); var recipient = PnaAddict(.96f);
            SSCPnaSexSupplyUtility.ApplyFromProducer(source, recipient); Near(1, SSCPnaExposureUtility.GetNeed(recipient).CurLevel);
            var first = NewPawn(); Raw(first, 1.96f);
            Assert(SSCPnaSexSupplyUtility.ApplyFromProducer(source, first).AddictionCreated, "首次成瘾");
            Near(1, SSCPnaExposureUtility.GetNeed(first).CurLevel); Near(1, SSCPnaExposureUtility.GetAddiction(first).Severity);
        });
        Run("第五步末期基础与额外加深合计0.21", () =>
        {
            var source = NewPawn(); AddPnaProducer(source); var recipient = PnaAddict(.05f); Raw(recipient, 1.92f);
            SSCPnaSexSupplyUtility.ApplyFromProducer(source, recipient);
            Near(.98, SSCPnaExposureUtility.GetLoad(recipient)); Near(.15, SSCPnaExposureUtility.GetNeed(recipient).CurLevel);
            Near(.91, SSCPnaExposureUtility.GetAddiction(recipient).Severity);
        });
        Run("第五步低负荷不创建需求，不强加非法需求", () =>
        {
            var source = NewPawn(); AddPnaProducer(source); var recipient = NewPawn();
            SSCPnaSexSupplyUtility.ApplyFromProducer(source, recipient);
            Near(.02, SSCPnaExposureUtility.GetLoad(recipient)); Assert(SSCPnaExposureUtility.GetNeed(recipient) == null, "无需求");
            recipient.needs.AllowPnaNeed = false; Raw(recipient, 2f);
            SSCPnaSexSupplyUtility.ApplyFromProducer(source, recipient);
            Assert(SSCPnaExposureUtility.GetNeed(recipient) == null, "非法需求未添加");
        });
        Run("第五步主人接收免疫，有效链接收性奴负荷", () =>
        {
            var source = NewPawn(); AddPnaProducer(source); var master = NewPawn(); master.IsMaster = true;
            Assert(!SSCPnaSexSupplyUtility.ApplyFromProducer(source, master).Accepted, "主人免疫"); Near(0, SSCPnaExposureUtility.GetLoad(master));
            var rooted = NewPawn(); Hediff_ChainOfSexSlave.AddToPawn(rooted, master);
            Assert(SSCPnaSexSupplyUtility.ApplyFromProducer(source, rooted).Rooted, "性奴分流"); Near(.02, SSCPnaExposureUtility.GetLoad(rooted));
            Assert(SSCPnaExposureUtility.GetAddiction(rooted) == null && SSCPnaExposureUtility.GetNeed(rooted) == null, "无成瘾需求");
        });
        Run("第五步主人实际产出仍可供给，身份本身不是来源", () =>
        {
            var master = NewPawn(); master.IsMaster = true; var recipient = NewPawn();
            Assert(!SSCPnaSexSupplyUtility.ApplyFromProducer(master, recipient).Accepted, "身份非来源");
            AddPnaProducer(master); Assert(SSCPnaSexSupplyUtility.ApplyFromProducer(master, recipient).Accepted, "实际来源");
            Near(.02, SSCPnaExposureUtility.GetLoad(recipient));
        });
        Run("第五步普通药效成瘾与兔产出不会传递", () =>
        {
            var source = PnaAddict(.2f); Raw(source, 1f);
            source.health.hediffSet.hediffs.Add(new Hediff { pawn = source, def = new HediffDef { defName = "SSC_Hediff_RabbitCloneLowPNA" } });
            var recipient = NewPawn(); Assert(!SSCPnaSexSupplyUtility.ApplyFromProducer(source, recipient).Accepted, "非来源");
            Assert(SSCPnaExposureUtility.GetBodyRecord(recipient) == null, "没有传播");
        });
        Run("第五步零与满生产进度剂量相同，不自体输入", () =>
        {
            var source = NewPawn(); var production = AddPnaProducer(source);
            foreach (float progress in new[] { 0f, 1f })
            {
                production.RawSeverity = progress; var recipient = NewPawn();
                SSCPnaSexSupplyUtility.ApplyFromProducer(source, recipient); Near(.02, SSCPnaExposureUtility.GetLoad(recipient)); Near(progress, production.Severity, 0);
            }
            Assert(!SSCPnaSexSupplyUtility.ApplyFromProducer(source, source).Accepted, "无自体输入");
        });
        foreach (string invalid in new[] { "null", "dead", "destroyed", "discarded", "suspended", "nonflesh", "nohealth" })
            Run("第五步无效产出身体不供给 " + invalid, () =>
            {
                var recipient = NewPawn();
                Assert(!SSCPnaSexSupplyUtility.ApplyFromProducer(InvalidAttractionPawn(invalid, true), recipient).Accepted, "无效来源");
                Assert(SSCPnaExposureUtility.GetBodyRecord(recipient) == null, "无残留");
            });
        Run("第五步供给健康回调成为主人时仍由共享入口清药", () =>
        {
            var source = NewPawn(); AddPnaProducer(source); var recipient = NewPawn();
            recipient.health.OnAdded = h => recipient.IsMaster = true;
            Assert(!SSCPnaSexSupplyUtility.ApplyFromProducer(source, recipient).Accepted, "身份回调拒绝");
            Assert(SSCPnaExposureUtility.GetBodyRecord(recipient) == null, "没有遗留药效");
        });
    }

    // 仅控制原版严重度 Setter 的同步回调边界，真实输入和保护判断仍执行生产代码。
    private sealed class SupplyAddiction : Hediff
    {
        public int Writes;
        public Action OnWrite;
        public override float Severity
        {
            get => base.Severity;
            set { Writes++; base.Severity = value; OnWrite?.Invoke(); }
        }
    }
}
