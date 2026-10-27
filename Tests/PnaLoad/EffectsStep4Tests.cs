using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using rjw.Modules.Attraction;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private delegate void ApplyAttraction(ref AttractionRequest request);

    // 直接执行生产查询、注册、倍率及解释；宿主不替代真实 RJW 启动扫描和最终择偶。
    private static void RunEffectsStep4Tests()
    {
        Run("第四步生产进度零与满值均为来源且查询不改进度", () =>
        {
            var p = NewPawn(); var production = AddPnaProducer(p);
            foreach (float progress in new[] { 0f, .01f, .6f, 1f })
            { production.RawSeverity = progress; Assert(SSCPnaSourceUtility.IsPnaProducer(p), "产出来源"); Near(progress, production.Severity, 0); }
        });
        Run("第四步负荷成瘾主人身份及兔产出均不单独构成来源", () =>
        {
            var p = NewPawn(); Input(p, 1); Assert(!SSCPnaSourceUtility.IsPnaProducer(p), "负荷成瘾不是来源");
            p.health.hediffSet.hediffs.Add(new Hediff { pawn = p, def = new HediffDef { defName = "SSC_Hediff_RabbitCloneLowPNA" } });
            Assert(!SSCPnaSourceUtility.IsPnaProducer(p), "兔产出排除"); p.IsMaster = true;
            Assert(!SSCPnaSourceUtility.IsPnaProducer(p), "主人身份不是来源");
        });
        foreach (string invalid in new[] { "null", "dead", "destroyed", "discarded", "suspended", "nonflesh", "nohealth" })
            Run("第四步来源拒绝无效身体 " + invalid, () => Assert(!SSCPnaSourceUtility.IsPnaProducer(InvalidAttractionPawn(invalid, true)), "无效来源"));
        Run("第四步主人持有普通产出仍是来源", () =>
        { var p = NewPawn(); p.IsMaster = true; AddPnaProducer(p); Assert(SSCPnaSourceUtility.IsPnaProducer(p), "药效免疫不取消来源"); });
        Run("第四步原生扫描条件可发现唯一SSC方法并创建ref委托", () =>
        {
            var applicators = typeof(AttractionPreference_SSCPna).Assembly.GetTypes()
                .Where(t => !t.IsAbstract && typeof(AttractionPreference).IsAssignableFrom(t))
                .SelectMany(t => t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                .Where(m => m.IsDefined(typeof(StandardPreferenceAttribute))).ToArray();
            Equal(1, applicators.Length); Equal(typeof(void), applicators[0].ReturnType);
            Equal(typeof(AttractionRequest).MakeByRefType(), applicators[0].GetParameters().Single().ParameterType);
            var apply = (ApplyAttraction)applicators[0].CreateDelegate(typeof(ApplyAttraction));
            var observer = PnaAddict(.8f); var target = NewPawn(); AddPnaProducer(target);
            var request = new AttractionRequest(AttractionPurpose.ForFucking, observer, target);
            apply(ref request); apply(ref request); Equal(1, request.PreferenceCount);
            var preference = request.GetPreference(AttractionPreference_SSCPna.PreferenceKey);
            Equal(AttractionMode.Physical, preference.Mode); Equal(FactorOperation.Multiply, preference.Operation);
            float factor = 2f; preference.Evaluate(ref request, ref factor); Near(2.5, factor);
        });
        foreach (AttractionPurpose purpose in new[] { AttractionPurpose.General, AttractionPurpose.ForFucking, AttractionPurpose.ForRape })
            foreach (float need in new[] { .8f, .05f, .01f, 0f })
                Run($"第四步各评价目的及原版阶段 {purpose}/{need}", () =>
                {
                    var observer = PnaAddict(need); var target = NewPawn(); AddPnaProducer(target);
                    Near(need <= .01f ? 1.5 : 1.25, PnaAttraction(observer, target, purpose));
                });
        Run("第四步三个目的均保留来源判断及主人锁链保护", () =>
        {
            foreach (AttractionPurpose purpose in new[] { AttractionPurpose.General, AttractionPurpose.ForFucking, AttractionPurpose.ForRape })
            {
                var observer = PnaAddict(0); var target = NewPawn(); var producer = AddPnaProducer(target);
                Near(1.5, PnaAttraction(observer, target, purpose));
                observer.IsMaster = true; Near(1, PnaAttraction(observer, target, purpose)); observer.IsMaster = false;
                var chain = new Hediff_ChainOfSexSlave { pawn = observer, def = ChainDef, LinkedPawn = NewPawn() };
                observer.health.hediffSet.hediffs.Add(chain); Near(1, PnaAttraction(observer, target, purpose));
                observer.health.hediffSet.hediffs.Remove(chain); target.IsMaster = true;
                Near(1.5, PnaAttraction(observer, target, purpose));
                target.health.hediffSet.hediffs.Remove(producer); Near(1, PnaAttraction(observer, target, purpose));
                target.health.hediffSet.hediffs.Add(producer); Near(1, PnaAttraction(NewPawn(), target, purpose));
            }
        });
        Run("第四步未成瘾和非来源对象均无额外吸引", () =>
        {
            var target = NewPawn(); AddPnaProducer(target); Near(1, PnaAttraction(NewPawn(), target));
            var observer = PnaAddict(0); var loadedTarget = NewPawn(); Input(loadedTarget, 1);
            Near(1, PnaAttraction(observer, loadedTarget));
        });
        Run("第四步观察方向不反转也不评价自身", () =>
        {
            var addict = PnaAddict(0); var producer = NewPawn(); AddPnaProducer(producer);
            Near(1.5, PnaAttraction(addict, producer)); Near(1, PnaAttraction(producer, addict));
            AddPnaProducer(addict); Near(1, PnaAttraction(addict, addict));
        });
        Run("第四步主人作为观察者免疫但作为来源可被偏好", () =>
        {
            var master = PnaAddict(0); master.IsMaster = true; AddPnaProducer(master);
            var other = PnaAddict(0); AddPnaProducer(other);
            Near(1, PnaAttraction(master, other)); Near(1.5, PnaAttraction(other, master));
            Assert(SSCPnaExposureUtility.GetAddiction(master) != null, "只读查询不代替身份清理");
        });
        Run("第四步有效链观察者受保护且查询不清理残留成瘾", () =>
        {
            var observer = PnaAddict(0); var addiction = SSCPnaExposureUtility.GetAddiction(observer);
            observer.health.hediffSet.hediffs.Add(new Hediff_ChainOfSexSlave { pawn = observer, def = ChainDef, LinkedPawn = NewPawn() });
            var target = NewPawn(); AddPnaProducer(target); Near(1, PnaAttraction(observer, target));
            Assert(SSCPnaExposureUtility.GetAddiction(observer) == addiction, "评价不修复身体");
        });
        Run("第四步失效残链不阻断真实成瘾偏好", () =>
        {
            var observer = PnaAddict(.8f); observer.health.hediffSet.hediffs.Add(new Hediff_ChainOfSexSlave { pawn = observer, def = ChainDef });
            var target = NewPawn(); AddPnaProducer(target); Near(1.25, PnaAttraction(observer, target));
        });
        foreach (string invalid in new[] { "null", "dead", "nohealth" })
            Run("第四步无效观察者安全退出 " + invalid, () =>
            { var target = NewPawn(); AddPnaProducer(target); Near(1, PnaAttraction(InvalidAttractionPawn(invalid, false), target)); });
        Run("第四步成瘾严重度不插值且无合法需求沿用普通档", () =>
        {
            var observer = PnaAddict(.8f); var target = NewPawn(); AddPnaProducer(target);
            foreach (float severity in new[] { .1f, .9f })
            {
                SSCPnaExposureUtility.GetAddiction(observer).RawSeverity = severity;
                Near(1.25, PnaAttraction(observer, target)); observer.needs.PnaNeed = 0; Near(1.5, PnaAttraction(observer, target));
                observer.needs.PnaNeed = .8f;
            }
            observer.needs.PnaNeed = null; Near(1.25, PnaAttraction(observer, target));
        });
        Run("第四步缺需求管理器的异常观察者安全退出", () =>
        {
            var observer = PnaAddict(0); observer.needs = null; var target = NewPawn(); AddPnaProducer(target);
            Near(1, PnaAttraction(observer, target));
        });
        Run("第四步倍率服从成瘾虚阶段而非自行推算需求", () =>
        {
            var observer = NewPawn(); observer.needs.PnaNeed = .8f;
            observer.health.hediffSet.hediffs.Add(new ForcedWithdrawal { pawn = observer, def = AddictionDef });
            var target = NewPawn(); AddPnaProducer(target); Near(1.5, PnaAttraction(observer, target));
        });
        Run("第四步已注册请求重读戒断来源与身份变化", () =>
        {
            var observer = PnaAddict(.8f); var target = NewPawn(); var producer = AddPnaProducer(target);
            var request = new AttractionRequest(AttractionPurpose.ForFucking, observer, target); AttractionPreference_SSCPna.ApplyTo(ref request);
            var preference = request.GetPreference(AttractionPreference_SSCPna.PreferenceKey);
            observer.needs.PnaNeed = 0; float factor = 1; preference.Evaluate(ref request, ref factor); Near(1.5, factor);
            target.health.hediffSet.hediffs.Remove(producer); factor = 1; preference.Evaluate(ref request, ref factor); Near(1, factor);
            target.health.hediffSet.hediffs.Add(producer); observer.IsMaster = true; factor = 1; preference.Evaluate(ref request, ref factor); Near(1, factor);
            observer.IsMaster = false; observer.health.hediffSet.hediffs.Remove(SSCPnaExposureUtility.GetAddiction(observer));
            factor = 1; preference.Evaluate(ref request, ref factor); Near(1, factor);
        });
        Run("第四步解释翻译使用真实倍率且只乘一次零分保持零", () =>
        {
            var observer = PnaAddict(0); var target = NewPawn(); AddPnaProducer(target);
            foreach (AttractionPurpose purpose in new[] { AttractionPurpose.General, AttractionPurpose.ForFucking, AttractionPurpose.ForRape })
            {
                var request = new AttractionRequest(purpose, observer, target); AttractionPreference_SSCPna.ApplyTo(ref request);
                var preference = request.GetPreference(AttractionPreference_SSCPna.PreferenceKey); float factor = 2;
                string explanation = preference.Explain(ref request, ref factor).ToString(); Near(3, factor);
                Assert(explanation.Contains("SSC_Pna_AttractionExplanation") && explanation.Contains("1.5,3"), "翻译键和真实参数");
                factor = 0; preference.Evaluate(ref request, ref factor); Near(0, factor);
            }
        });
        Run("第四步反复评价与解释不结算健康时间需求生产或清旧状态", () =>
        {
            var observer = PnaAddict(0); Raw(observer, .8f); Tick(observer, 50);
            var body = Record(observer); var addiction = SSCPnaExposureUtility.GetAddiction(observer);
            var target = NewPawn(); var producer = AddPnaProducer(target); producer.RawSeverity = .6f;
            target.health.hediffSet.hediffs.Add(new Hediff { pawn = target, def = new HediffDef { defName = "Hediff_PNA_Combat_Buff" } });
            int beforeObserver = observer.health.Notifications, beforeTarget = target.health.Notifications;
            float beforeBody = body.Severity, beforeAddiction = addiction.Severity;
            for (int i = 0; i < 20; i++)
            {
                var purpose = new[] { AttractionPurpose.General, AttractionPurpose.ForFucking, AttractionPurpose.ForRape }[i % 3];
                var request = new AttractionRequest(purpose, observer, target); AttractionPreference_SSCPna.ApplyTo(ref request);
                float factor = 1; request.GetPreference(AttractionPreference_SSCPna.PreferenceKey).Explain(ref request, ref factor); Near(1.5, factor);
            }
            Near(beforeBody, body.Severity, 0); Near(beforeAddiction, addiction.Severity, 0); Equal(50, Comp(observer).PendingTicks);
            Near(0, observer.needs.PnaNeed.Value); Near(.6, producer.Severity); Equal(2, target.health.hediffSet.hediffs.Count);
            Equal(beforeObserver, observer.health.Notifications); Equal(beforeTarget, target.health.Notifications);
        });
    }

    private sealed class ForcedWithdrawal : Hediff { public override int CurStageIndex => 1; }
    private static Hediff AddPnaProducer(Pawn pawn)
    {
        var production = new Hediff { pawn = pawn, def = new HediffDef { defName = SSCPnaSourceUtility.ProducerDefName } };
        pawn.health.hediffSet.hediffs.Add(production); return production;
    }
    private static Pawn PnaAddict(float need)
    { var p = NewPawn(); AddOldAddiction(p, .7f); p.needs.PnaNeed = need; return p; }
    private static float PnaAttraction(Pawn observer, Pawn target, AttractionPurpose purpose = AttractionPurpose.ForFucking)
    {
        var request = new AttractionRequest(purpose, observer, target); AttractionPreference_SSCPna.ApplyTo(ref request);
        float factor = 1f; request.GetPreference(AttractionPreference_SSCPna.PreferenceKey)?.Evaluate(ref request, ref factor); return factor;
    }
    private static Pawn InvalidAttractionPawn(string kind, bool producer)
    {
        if (kind == "null") return null;
        var p = NewPawn(); if (producer) AddPnaProducer(p);
        switch (kind)
        {
            case "dead": p.Dead = true; break; case "destroyed": p.Destroyed = true; break;
            case "discarded": p.Discarded = true; break; case "suspended": p.Suspended = true; break;
            case "nonflesh": p.RaceProps.IsFlesh = false; break; case "nohealth": p.health = null; break;
        }
        return p;
    }
}
