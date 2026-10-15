using System;
using System.Collections.Generic;
using RimWorld;
using rjw;
using rjw.Modules.Interactions;
using SexSlaveCraft;
using Verse;

internal static class Program
{
    private static int passed, failed;
    private static readonly Pawn A = new(), B = new(), Other = new();
    private static InteractionDef Add(string name, xxx.rjwSextype type, bool reverse = false)
    {
        var def = new InteractionDef { defName = name, Type = type, Reverse = reverse };
        DefDatabase<InteractionDef>.Values[name] = def; return def;
    }
    private static SexInteractionResolved Valid(SexProps props)
    {
        var a = new Part { Owner = props.initiator }; var b = new Part { Owner = props.recipient };
        return new SexInteractionResolved { Initiator = props.initiator, Recipient = props.recipient,
            Interaction = props.interaction, InitiatorParts = new() { a }, RecipientParts = new() { b },
            Penetrations = new() { Tuple.Create<ILewdablePart, ILewdablePart>(a, b) } };
    }
    private static SexProps Old()
    {
        var props = new SexProps { pawn = A, partner = B, interaction = new(Add("Old", xxx.rjwSextype.Oral)) };
        props.resolved = Valid(props); _ = props.sexType; return props;
    }
    private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Run(string name, Action action)
    {
        DefDatabase<InteractionDef>.Values.Clear(); SexInteractionHelper.Calls = 0; SexInteractionHelper.Resolver = Valid;
        try { action(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception ex) { failed++; Console.WriteLine("FAIL " + name + ": " + ex.Message); }
    }
    private static int Main()
    {
        Run("强制动作替换旧解析与已读取动作类型，保留场景标记", () =>
        {
            var props = Old(); var old = props._resolved; props.isRape = props.isWhoring = props.usedCondom = true; props.orgasms = 3;
            var def = Add("Sex_Anal", xxx.rjwSextype.Anal);
            SexInteractionHelper.Resolver = p => { Assert(p._resolved == null, "解析前旧部位未清除"); return Valid(p); };
            Assert(RJWSexPropsUtility.ApplySexType(props, A, B, xxx.rjwSextype.Anal, def.defName), "未成功");
            Assert(props.sexType == xxx.rjwSextype.Anal && props.resolved.Interaction.Def == def && props.dictionaryKey == def, "动作或 Def 不一致");
            Assert(props._resolved != old && props.isRape && props.isWhoring && props.usedCondom && props.orgasms == 3, "场景标记丢失");
            Assert(SexInteractionHelper.Calls == 1, "重复解析");
        });
        foreach (bool reverse in new[] { false, true })
            Run("同步反向及发起视角 " + reverse, () =>
            {
                var props = Old(); props.isRevese = !reverse; props.isReceiver = true;
                Add("Forced", xxx.rjwSextype.Anal, reverse);
                Assert(RJWSexPropsUtility.ApplySexType(props, A, B, xxx.rjwSextype.Anal, "Forced"), "失败");
                Assert(props.isRevese == reverse && !props.isReceiver && props.resolved.Initiator == A && props.resolved.Recipient == B, "方向错误");
            });
        foreach (string invalid in new[] { "missing", "type", "null", "self" })
            Run("无效请求不改原完整选择 " + invalid, () =>
            {
                var props = Old(); var old = props._resolved;
                Add("Forced", invalid == "type" ? xxx.rjwSextype.Oral : xxx.rjwSextype.Anal);
                Assert(!RJWSexPropsUtility.ApplySexType(props, invalid == "null" ? null : A, invalid == "self" ? A : B,
                    xxx.rjwSextype.Anal, invalid == "missing" ? "Absent" : "Forced"), "未拒绝");
                Assert(props._resolved == old && props.dictionaryKey.defName == "Old" && SexInteractionHelper.Calls == 0, "破坏原选择");
            });
        foreach (string invalid in new[] { "null", "def", "initiator", "recipient", "partA", "partB", "contact", "noList", "changedProps" })
            Run("拒绝不一致解析且不留下旧部位 " + invalid, () =>
            {
                var props = Old(); Add("Forced", xxx.rjwSextype.Anal);
                SexInteractionHelper.Resolver = p =>
                {
                    var result = Valid(p);
                    switch (invalid)
                    {
                        case "null": return null;
                        case "def": result.Interaction = new(DefDatabase<InteractionDef>.Values["Old"]); break;
                        case "initiator": result.Initiator = Other; break;
                        case "recipient": result.Recipient = Other; break;
                        case "partA": result.InitiatorParts[0] = new Part { Owner = Other }; break;
                        case "partB": result.RecipientParts[0] = new Part { Owner = Other }; break;
                        case "contact": result.Penetrations[0] = Tuple.Create<ILewdablePart, ILewdablePart>(new Part { Owner = Other }, result.RecipientParts[0]); break;
                        case "noList": result.InitiatorParts = null; break;
                        case "changedProps": p.partner = Other; break;
                    }
                    return result;
                };
                Assert(!RJWSexPropsUtility.ApplySexType(props, A, B, xxx.rjwSextype.Anal, "Forced"), "坏解析未拒绝");
                Assert(props._resolved == null && SexInteractionHelper.Calls == 1, "沿用了旧解析");
            });
        foreach (bool penetrative in new[] { false, true })
            Run("允许非插入及相反插入方向 " + penetrative, () =>
            {
                var props = Old(); Add("Forced", xxx.rjwSextype.Anal);
                SexInteractionHelper.Resolver = p => { var result = Valid(p); result.Penetrations.Clear();
                    if (penetrative) result.Penetrations.Add(Tuple.Create(result.RecipientParts[0], result.InitiatorParts[0])); return result; };
                Assert(RJWSexPropsUtility.ApplySexType(props, A, B, xxx.rjwSextype.Anal, "Forced"), "合法方向被拒");
            });
        Run("口交总类兼容本机 Fellatio 交互并保留原结算类型", () =>
        {
            var props = Old(); Add("Sex_Fellatio", xxx.rjwSextype.Fellatio);
            Assert(RJWSexPropsUtility.ApplyTrainingAct(props, A, B, TrainingActType.Oral), "口交映射被拒");
            Assert(props.sexType == xxx.rjwSextype.Oral && props.resolved.Interaction.Def.defName == "Sex_Fellatio", "原分类或实际动作改变");
        });
        Run("阶段切换在下一动作开始前重新解析", () =>
        {
            var props = Old(); Add("Sex_Anal", xxx.rjwSextype.Anal); Add("Sex_Vaginal", xxx.rjwSextype.Vaginal);
            Assert(RJWSexPropsUtility.ApplyTrainingAct(props, A, B, TrainingActType.Anal), "第一阶段"); var old = props._resolved;
            Assert(RJWSexPropsUtility.ApplyTrainingAct(props, A, B, TrainingActType.Vaginal), "下一阶段");
            Assert(props.resolved != old && props.resolved.Interaction.Def.defName == "Sex_Vaginal" && SexInteractionHelper.Calls == 2, "保留旧阶段部位");
        });
        // 仪式保留固定六阶段；无解剖匹配时使用当前交互的空结果，不能借旧动作部位继续。
        foreach (var item in new[] {
            ("Sex_Handjob", xxx.rjwSextype.Handjob), ("Sex_Footjob", xxx.rjwSextype.Footjob),
            ("Sex_Sixtynine", xxx.rjwSextype.Sixtynine), ("Sex_Breastjob", xxx.rjwSextype.Boobjob),
            ("Sex_Anal", xxx.rjwSextype.Anal), ("Sex_Vaginal", xxx.rjwSextype.Vaginal) })
            Run("固定仪式无部位匹配仍保留当前动作 " + item.Item1, () =>
            {
                var props = Old(); var old = props._resolved; var def = Add(item.Item1, item.Item2);
                SexInteractionHelper.Resolver = _ => null;
                Assert(RJWSexPropsUtility.ApplySexType(props, A, B, item.Item2, item.Item1, allowEmptyParts: true), "解剖门槛阻断仪式");
                Assert(props.resolved != old && props.resolved.Interaction.Def == def && props.sexType == item.Item2, "保留旧动作");
                Assert(props.resolved.Initiator == A && props.resolved.Recipient == B && props.resolved.InitiatorParts.Count == 0 &&
                    props.resolved.RecipientParts.Count == 0 && props.resolved.Penetrations.Count == 0, "捏造部位或保留旧接触");
                Assert(SexInteractionHelper.Calls == 1, "读取时重新解析");
            });
        Run("固定仪式有合法部位时仍采用真实结果", () =>
        {
            var props = Old(); Add("Forced", xxx.rjwSextype.Anal);
            Assert(RJWSexPropsUtility.ApplySexType(props, A, B, xxx.rjwSextype.Anal, "Forced", allowEmptyParts: true), "合法部位失败");
            Assert(props.resolved.InitiatorParts.Count == 1 && props.resolved.Penetrations.Count == 1, "丢弃真实部位");
        });
        Run("固定仪式不能用空结果放行串场的解析", () =>
        {
            var props = Old(); Add("Forced", xxx.rjwSextype.Anal);
            SexInteractionHelper.Resolver = p => { var r = Valid(p); r.Recipient = Other; return r; };
            Assert(!RJWSexPropsUtility.ApplySexType(props, A, B, xxx.rjwSextype.Anal, "Forced", allowEmptyParts: true), "放行错误参与者");
        });
        Console.WriteLine($"结果：{passed}/{passed + failed} 项通过。"); return failed == 0 ? 0 : 1;
    }
}
