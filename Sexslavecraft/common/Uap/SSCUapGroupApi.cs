// AI 辅助生成
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>UAP 软依赖边界；启动时验证成员形状，运行状态始终比较实际对象而非 thingID。</summary>
    internal static class SSCUapGroupApi
    {
        internal static Type Dialog, Component, Request, Matcher, Launcher, Manager, Receiver, Initiator, Passive, Auxiliary;
        // 只有必需接口核对和整套 Harmony 注册全部成功后才置 true；设置 UI 使用同一门槛。
        internal static bool Ready;
        internal static MethodInfo Create, RestoreOne, Stop;
        private static FieldInfo sessions, pawnSessions;
        private static readonly Dictionary<(Type, string), FieldInfo> fields = new Dictionary<(Type, string), FieldInfo>();
        /// <summary>从当前运行 Def 集取得共享接收任务，避免在未加载 UAP 时触发其 DefOf 静态构造。</summary>
        internal static JobDef ReceiverDef => DefDatabase<JobDef>.GetNamed("UAP_GroupSexReceiver");
        /// <summary>取得当前 UAP 发起任务定义，后续仍核对实际创建的运行驱动。</summary>
        internal static JobDef InitiatorDef => DefDatabase<JobDef>.GetNamed("UAP_GroupSexInitiator");
        /// <summary>取得 UAP 集合及被动参与者任务；具体模式由本次登记与目标区分。</summary>
        internal static JobDef PassiveDef => DefDatabase<JobDef>.GetNamed("UAP_GroupPassiveParticipant");

        /// <summary>按声明类型、完整参数和返回类型核对唯一入口；签名不符时显式失败，不猜选重载。</summary>
        internal static MethodInfo Method(Type type, string name, Type result, params Type[] args)
        {
            MethodInfo method = AccessTools.DeclaredMethod(type, name, args);
            if (method == null || method.ReturnType != result || method.ContainsGenericParameters)
                throw new MissingMethodException(type.FullName, name);
            return method;
        }

        /// <summary>缓存字段元数据而非字段内容；缺失字段抛错交给整套安装失败处理。</summary>
        internal static FieldInfo Field(Type type, string name)
        {
            if (!fields.TryGetValue((type, name), out FieldInfo field))
            {
                field = AccessTools.Field(type, name) ?? throw new MissingFieldException(type.FullName, name);
                fields.Add((type, name), field);
            }
            return field;
        }

        /// <summary>先完整解析所需类型和字段；任一必需接口变化都不安装半套豁免补丁。</summary>
        internal static bool Resolve()
        {
            Dialog = AccessTools.TypeByName("UAP_Animations.Dialog_GroupTogether");
            if (Dialog == null) return false;
            // 所有关联类型必须来自同一个实际 UAP 程序集，避免全局同名查找拼接出混合版本。
            Type T(string name) => Dialog.Assembly.GetType("UAP_Animations." + name, true);
            Component = T("GroupTogetherMapComponent"); Request = T("GroupBedRequest");
            Matcher = T("UAPGroupAnimationMatcher"); Launcher = T("UAPGroupRJWLauncher"); Manager = T("UAPGroupSexSessionManager");
            Receiver = T("JobDriver_UAPGroupSexReceiver"); Initiator = T("JobDriver_UAPGroupSexInitiator");
            Passive = T("JobDriver_UAPGroupPassiveParticipant"); Auxiliary = T("JobDriver_UAPGroupAuxiliaryPenetrator");
            Type session = T("UAPGroupSexSession"), match = T("UAPGroupAnimationMatch"), plan = T("UAPGroupLaunchPlan");
            Type pair = T("UAPGroupMechanicalPairPlan");
            // 同名字段也可能改变类型；安装前逐项核对，避免运行到 UI 前缀时才发生强制转换异常。
            void F(Type type, string name, Type expected)
            {
                if (Field(type, name).FieldType != expected) throw new InvalidOperationException("UAP field type changed: " + type.Name + "." + name);
            }
            F(Dialog, "anchor", typeof(Thing)); F(Dialog, "readyPawns", typeof(List<Pawn>)); F(Dialog, "selectedAnimationDefName", typeof(string));
            F(Request, "map", typeof(Map)); F(Request, "anchor", typeof(Thing)); F(Request, "participants", typeof(List<Pawn>)); F(Request, "selectedMatch", match);
            foreach (Type type in new[] { session, plan })
            {
                F(type, "Receiver", typeof(Pawn)); F(type, "PrimaryInitiator", typeof(Pawn));
                F(type, "MechanicalPairs", typeof(List<>).MakeGenericType(pair)); F(type, "PassiveParticipants", typeof(List<Pawn>));
            }
            F(session, "SessionId", typeof(int)); F(session, "Map", typeof(Map)); F(session, "Anchor", typeof(Thing));
            F(session, "Participants", typeof(List<Pawn>)); F(plan, "OrderedParticipants", typeof(List<Pawn>)); F(match, "DefName", typeof(string));
            F(pair, "Initiator", typeof(Pawn)); F(pair, "Receiver", typeof(Pawn));
            F(Manager, "sessionsById", typeof(Dictionary<,>).MakeGenericType(typeof(int), session));
            F(Manager, "sessionByPawnThingId", typeof(Dictionary<int, int>));
            // 创建会话及外观清理沿用 UAP 自身实现；out 参数类型也属于必需接口契约。
            Create = Method(Manager, "TryCreateGroupSession", typeof(bool), typeof(Thing), typeof(List<Pawn>), match,
                session.MakeByRefType(), typeof(string).MakeByRefType());
            RestoreOne = Method(Launcher, "RestoreParticipantClothing", typeof(void), typeof(Pawn));
            Stop = Method(T("UAPGroupAnimationBridge"), "TryStopGroupAnimation", typeof(void), typeof(List<Pawn>));
            sessions = Field(Manager, "sessionsById"); pawnSessions = Field(Manager, "sessionByPawnThingId");
            if (!Create.IsStatic || !RestoreOne.IsStatic || !Stop.IsStatic || !sessions.IsStatic || !pawnSessions.IsStatic)
                throw new InvalidOperationException("UAP static entry changed");
            return true;
        }

        /// <summary>通过已集中管理的字段访问当前实例数据；不缓存会话、角色或许可的最终值。</summary>
        internal static T Get<T>(object instance, string name) => (T)Field(instance.GetType(), name).GetValue(instance);
        /// <summary>枚举 UAP 实际机械配对，保持发起和接收方向，不按性别、身份或姿势重新推断。</summary>
        internal static IEnumerable<(Pawn Actor, Pawn Target)> Pairs(object plan)
        {
            foreach (object pair in Get<IEnumerable>(plan, "MechanicalPairs"))
                yield return (Get<Pawn>(pair, "Initiator"), Get<Pawn>(pair, "Receiver"));
        }

        /// <summary>只识别本轮核实的四种群体驱动；其他 UAP 或 RJW 任务不进入读档取消范围。</summary>
        internal static bool GroupDriver(Type type) => type == Receiver || type == Initiator || type == Passive || type == Auxiliary;
        /// <summary>同时比较 Pawn 索引的会话编号和会话表的对象引用，避免相同编号落到替换会话。</summary>
        internal static bool PawnSessionIs(SSCUapGroupRuntime.Batch batch, Pawn pawn)
        {
            var index = (IDictionary)pawnSessions.GetValue(null);
            return index.Contains(pawn.thingIDNumber) && (int)index[pawn.thingIDNumber] == batch.SessionId &&
                ((IDictionary)sessions.GetValue(null))[batch.SessionId] == batch.Session;
        }
        /// <summary>核对会话地图、发生点、完整 Pawn 对象集合及每人索引，防止沿旧编号借用另一场。</summary>
        internal static bool OwnsSession(SSCUapGroupRuntime.Batch batch) => batch.Session != null &&
            Get<Map>(batch.Session, "Map") == batch.Map && Get<Thing>(batch.Session, "Anchor") == batch.Anchor &&
            SSCUapGroupRuntime.SamePeople(batch, Get<List<Pawn>>(batch.Session, "Participants")) &&
            batch.People.All(p => PawnSessionIs(batch, p.Pawn));

        /// <summary>调用原 UAP 构造会话并接收原失败原因；这里只绑定实际返回对象，不标记行为已开始。</summary>
        internal static bool CreateSession(SSCUapGroupRuntime.Batch batch, out string reason)
        {
            object[] args = { batch.Anchor, batch.People.Select(p => p.Pawn).ToList(), batch.Match, null, null };
            bool result = (bool)Create.Invoke(null, args);
            batch.Session = args[3]; reason = (string)args[4];
            if (result) batch.SessionId = Get<int>(batch.Session, "SessionId");
            return result;
        }

        /// <summary>比较后移除；另一场已覆盖的映射必须保留，不能调用 UAP 原来按类型清场的 Abort。</summary>
        internal static void Detach(SSCUapGroupRuntime.Batch batch)
        {
            if (batch.Session == null) return;
            var index = (IDictionary)pawnSessions.GetValue(null);
            foreach (var person in batch.People)
                if (PawnSessionIs(batch, person.Pawn)) index.Remove(person.Pawn.thingIDNumber);
            // 人物映射与会话表分别比较后移除；某个人已转入新场时不删除其新映射。
            var table = (IDictionary)sessions.GetValue(null);
            if (table[batch.SessionId] == batch.Session) table.Remove(batch.SessionId);
        }
        /// <summary>由调用方先筛出仍属本场的人物，再委托 UAP 停止动画及恢复服装显示。</summary>
        internal static void Restore(List<Pawn> pawns)
        {
            Stop.Invoke(null, new object[] { pawns });
            foreach (Pawn pawn in pawns) RestoreOne.Invoke(null, new object[] { pawn });
        }
        /// <summary>新游戏只清静态索引；不调用旧会话的结束逻辑，避免操作上一游戏的 Pawn。</summary>
        internal static void ClearSessions()
        {
            if (!Ready) return;
            ((IDictionary)sessions.GetValue(null)).Clear();
            ((IDictionary)pawnSessions.GetValue(null)).Clear();
        }
    }
}
