// AI 辅助生成：1.7 文化共享的 100 tick 按需快照；无主动扫描、永久身份或保存字段。
using System.Collections.Generic;
using Verse;

namespace SexSlaveCraft
{
    public sealed class SSCCultureGameComponent : GameComponent
    {
        // 所有按需项目共享首次成功读取开启的 100 tick 代；组件不注册逐 tick 扫描。
        private const int Lifetime = 100;
        private readonly Game owner;
        // 引用比较避免 Pawn 值语义影响去重；名册跨项目复用，scratch 仅作单次统计的工作集。
        private readonly HashSet<Pawn> members = new HashSet<Pawn>(SSCBondOverviewReferenceComparer<Pawn>.Instance);
        private readonly HashSet<Pawn> scratch = new HashSet<Pawn>(SSCBondOverviewReferenceComparer<Pawn>.Instance);
        // 管理集合保留到本代结束，普通总数与性别细分共用一次物理来源收集；细分仍按需准备。
        private readonly HashSet<Pawn> managed = new HashSet<Pawn>(SSCBondOverviewReferenceComparer<Pawn>.Instance);
        private readonly SSCCultureGenderBuckets managedGender = new SSCCultureGenderBuckets();
        private readonly SSCCultureGenderBuckets adultManagedGender = new SSCCultureGenderBuckets();
        private readonly SSCCultureGenderBuckets dutyGender = new SSCCultureGenderBuckets();
        private bool managedGenderReady, dutyGenderReady;
        // 仅缓存被请求主人的数量，包含有效的零；不为所有主人预建个人条目。
        private readonly Dictionary<Pawn, int> boundCounts = new Dictionary<Pawn, int>(SSCBondOverviewReferenceComparer<Pawn>.Instance);
        // ready 表示加载已完成，valid 表示已有当前代；各 Ready 分别标记已计算的项目，零值也可命中。
        private bool ready, valid, membersReady, mastersReady, managedReady, leaderReady;
        private int generationTick, lastReadTick, masterCount, managedCount;
        private SSCCultureLeaderResult leader;

        // 绑定所属 Game，用于拒绝切档后的旧组件和旧初始化回调；缓存字段不参与存档。
        public SSCCultureGameComponent(Game game) { owner = game; }

        // 初始建局、载入与最终初始化都走同一重置入口，重入也只使缓存失效，不扫描人口。
        public override void FinalizeInit() => InitializeWhenFinished();
        public override void LoadedGame() => InitializeWhenFinished();
        public override void StartedNewGame() => InitializeWhenFinished();

        private void InitializeWhenFinished()
        {
            // 交叉引用完成不等于地图/其他组件初始化完成；只在长事件结束后开放读取。
            // 延迟回调核对 Game，防止切档后旧回调使另一实例的缓存有效，不在回调中收集人口。
            ready = false;
            ClearGeneration();
            LongEventHandler.ExecuteWhenFinished(() => { if (Current.Game == owner) ready = true; });
        }

        private void ClearGeneration()
        {
            // 同时清除准备标记、Pawn 引用和结果，防止过期或不可查询状态泄漏上一代的数据。
            // ready 独立于代内缓存；正常过期只需重建结果，无须重新等待初始化回调。
            valid = membersReady = mastersReady = managedReady = leaderReady = false;
            members.Clear(); scratch.Clear(); boundCounts.Clear();
            // 细分贡献与桶必须同步失效；旧 Game 或回退时不能留下可用于排除本人的上一批性别。
            managed.Clear(); managedGender.Clear(); adultManagedGender.Clear(); dutyGender.Clear();
            managedGenderReady = dutyGenderReady = false;
            leader = default;
            masterCount = managedCount = 0;
        }

        private bool BeginRead()
        {
            // 未就绪是“不可查询”，并非有效的零人数；失败读取不能建立空快照。
            // 不缓存 Def 查找，也不依赖是否有当前地图（纯商队存档仍可合法统计）。
            if (Current.Game != owner || !ready || !ModsConfig.IdeologyActive
                || Current.ProgramState != ProgramState.Playing || Scribe.mode != LoadSaveMode.Inactive
                || LongEventHandler.AnyEventNowOrWaiting || Find.TickManager == null)
            { ClearGeneration(); return false; }

            int now = Find.TickManager.TicksGame;
            // 用上次读取时刻检测周期内回退；后算项目不更新 generationTick，不延长旧名册寿命。
            // 提升到 long 后再做 tick 差值，避免 int 差值溢出影响过期判定。
            if (!valid || now < lastReadTick || (long)now - generationTick >= Lifetime)
            {
                ClearGeneration();
                valid = true;
                generationTick = now;
            }
            // 每次可查询读取都记录时刻；即使未越过起点，向前读取后再回退也必须换代。
            lastReadTick = now;
            return true;
        }

        private void EnsureMembers()
        {
            // 主人数、成员资格与领袖共用未筛身份的名册；收集成功后才标记就绪。
            if (membersReady) return;
            SSCCulturePopulationUtility.CollectMembers(members);
            membersReady = true;
        }

        public bool TryGetMasterCount(out int count)
        {
            // bool 区分“不可查询”与“有效零人”；未开始有效读取时不得发布缓存结果。
            count = 0;
            if (!BeginRead()) return false;
            if (!mastersReady)
            {
                // 第一次请求才筛主人；同代后续请求直接取结果，不重复遍历名册。
                EnsureMembers();
                masterCount = SSCCulturePopulationUtility.CountMasters(members);
                mastersReady = true;
            }
            // 准备标记与数值分离，因此零主人也能作为有效缓存返回。
            count = masterCount;
            return true;
        }

        public bool TryIsMember(Pawn pawn, out bool member)
        {
            // 本接口只查人物栏资格，不附加主人身份；不可查询和有效的非成员由返回值区分。
            member = false;
            if (!BeginRead()) return false;
            EnsureMembers();
            // 引用集合查询不触发人物栏 UI 更新，也不补入未出现在原版来源中的玩家阵营 Pawn。
            member = pawn != null && members.Contains(pawn);
            return true;
        }

        public bool TryGetManagedChainedCount(out int count)
        {
            // 环境锁链人数独立准备，不因请求它而构建人物栏名册或个人绑定统计。
            count = 0;
            if (!BeginRead()) return false;
            if (!managedReady)
            {
                // 完成这一代唯一一次环境扫描后才置位；保留完整集合，细分不再枚举世界来源。
                SSCCulturePopulationUtility.CollectManagedChained(managed);
                managedCount = managed.Count;
                managedReady = true;
            }
            // 同代复用包括零人在内的结果，新增其他项目不会延长其有效期。
            count = managedCount;
            return true;
        }

        private void BuildManagedGender()
        {
            // 正向神圣只过滤性别，保留旧集合其余条件；新负向环境单独收集成人桶。
            // 两份桶和贡献均在同次准备中捕获，后续查询只做常量次枚举和引用字典查找。
            foreach (Pawn pawn in managed)
            {
                managedGender.Add(pawn);
                if (SSCCultureGenderUtility.IsAdult(pawn)) adultManagedGender.Add(pawn);
            }
            managedGenderReady = true;
        }

        private void BuildDutyGender()
        {
            // 原名册不被年龄/身份过滤覆盖，主人及正式领袖仍使用完整名册；义务桶不按文化拆分。
            foreach (Pawn pawn in members)
                if (SSCCultureGenderUtility.IsDutyCandidate(pawn)) dutyGender.Add(pawn);
            dutyGenderReady = true;
        }

        public bool TryGetAcceptedManagedChainedCount(SSCCultureGenderPolicy policy, out int count)
        {
            // 默认不限或非主即奴直接走旧总数，避免无性别过滤时准备无关细分。
            if (policy == SSCCultureGenderPolicy.Unrestricted || policy == SSCCultureGenderPolicy.NonMasterRequired)
                return TryGetManagedChainedCount(out count);
            count = 0;
            if (!TryGetManagedChainedCount(out _)) return false;
            if (!managedGenderReady) BuildManagedGender();
            count = managedGender.Count(policy, SSCCultureGenderFilter.Accepted);
            return true;
        }

        public bool TryGetGenderViolationCount(SSCCultureGenderPolicy policy, Pawn observer, out int count)
        {
            count = 0;
            if (!TryGetManagedChainedCount(out _)) return false;
            if (!managedGenderReady) BuildManagedGender();
            // 返回排除本人后的完整值，三人计罚上限由反馈层处理，不改变其他人数查询。
            count = adultManagedGender.Count(policy, SSCCultureGenderFilter.Rejected, observer);
            return true;
        }

        public bool TryGetUnfulfilledDutyCount(SSCCultureGenderPolicy policy, Pawn observer, out int count)
        {
            count = 0;
            if (!BeginRead()) return false;
            EnsureMembers();
            if (!dutyGenderReady) BuildDutyGender();
            count = dutyGender.Count(policy, SSCCultureGenderFilter.Required, observer);
            return true;
        }

        public bool TryGetLeader(out SSCCultureLeaderResult result)
        {
            // 不可查询用 false 表示；有效的空缺、非主人和冲突均是可缓存的正式查询结果。
            result = default;
            if (!BeginRead()) return false;
            if (!leaderReady)
            {
                // 领袖资格依赖共享名册，但不依赖主人数；查询本身不修复或刷新原版职位。
                EnsureMembers();
                leader = SSCCultureLeaderUtility.Query(members);
                leaderReady = true;
            }
            // 将整份结果返回，保留 Conflict，避免调用者把冲突任意解释为一位领袖。
            result = leader;
            return true;
        }

        public bool TryGetBoundSlaveCount(Pawn master, out int count)
        {
            // 先核对组件生命周期；失效组件不能以个人不适用为由伪装成成功查询。
            count = 0;
            if (!BeginRead()) return false;
            // 个人适用资格读取现状，只有实际查询的有效主人进入字典；不预扫所有主人。
            if (!SSCCultureStateUtility.IsLiving(master) || !SSCIdentityUtility.IsMaster(master)) return true;
            if (!boundCounts.TryGetValue(master, out count))
            {
                // 只有该主人的首次有效请求才遍历既有绑定迭代器，零结果同样入字典。
                count = SSCCulturePopulationUtility.CountBoundSlaves(master, scratch);
                boundCounts.Add(master, count);
            }
            return true;
        }
    }
}
