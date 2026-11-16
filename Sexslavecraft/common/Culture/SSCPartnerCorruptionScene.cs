// AI 辅助生成：只读识别既有 RJW／UAP 场景，不触碰任务、行为许可或动画。
using System.Collections.Generic;
using rjw;
using Verse;
using Verse.AI;

namespace SexSlaveCraft
{
    internal readonly struct SSCPartnerCorruptionScene
    {
        // 普通 RJW 以共享接收任务为场次归属；UAP 有自己的会话对象。两条路径都以实际高潮者为对象。
        private readonly Member target;
        private readonly Member receiver;
        private readonly object uapSession;

        /// <summary>保留原任务引用与编号，防止同步换任务、重设 SexProps 或对象池复用。</summary>
        internal readonly struct Member
        {
            internal readonly Pawn Pawn;
            internal readonly JobDriver Driver;
            private readonly Job job;
            private readonly int jobId;
            private readonly SexProps props;

            internal Member(JobDriver driver)
            {
                Pawn = driver.pawn; Driver = driver; job = driver.job; jobId = job.loadID;
                props = (driver as JobDriver_Sex)?.Sexprops;
            }

            // Job 被对象池复用后引用可能没变；编号识别复用，SexProps 引用识别在同一任务里重建的交互。
            internal bool Current => IsCurrentJob(Pawn, Driver) && Driver.job == job && job.loadID == jobId &&
                ReferenceEquals((Driver as JobDriver_Sex)?.Sexprops, props);
        }

        private SSCPartnerCorruptionScene(JobDriver_Sex target, JobDriver_SexBaseReciever receiver, object uapSession)
        {
            this.target = new Member(target);
            this.receiver = receiver == null ? default : new Member(receiver);
            this.uapSession = uapSession;
        }

        internal Pawn RjwReceiver => receiver.Pawn;

        private static bool IsCurrentJob(Pawn pawn, JobDriver driver) => SSCCultureStateUtility.IsLiving(pawn) && pawn.Spawned &&
            driver != null && !driver.ended && driver.job != null && driver.pawn == pawn && pawn.CurJob == driver.job &&
            pawn.jobs?.curDriver == driver;

        /// <summary>从场次的两个方向排除 SSC 自有性交任务；外部共享接收驱动不能把 SSC 场次变成普通场次。</summary>
        private static bool IsSscScene(JobDriver driver) => driver is JobDriver_Training or JobDriver_TrainingReceiver or
            JobDriver_RitualTraining or JobDriver_SelfTraining or JobDriver_PE or JobDriver_FertilityConception or
            JobDriver_FertilityReceiver or JobDriver_FertilityFollowup or JobDriver_PetAffectionFollowup;

        internal static bool TryCapture(JobDriver_Sex driver, out SSCPartnerCorruptionScene scene, out List<Pawn> participants)
        {
            scene = default;
            participants = null;
            if (!IsCurrentJob(driver.pawn, driver) || !SSCOrgasmProgressUtility.IsCurrentDriver(driver) || IsSscScene(driver)) return false;
            if (SSCUapGroupApi.IsGroupSceneDriver(driver))
            {
                // 已知 UAP 驱动必须有自己的有效会话；接口不可用或会话过期时不能用旧 Partner 猜场次。
                if (!TryGetUapParticipants(driver, out object session, out participants)) return false;
                scene = new SSCPartnerCorruptionScene(driver, null, session);
                return true;
            }

            // 普通 RJW 以当前接收任务为场次锚。发起对象可从自己的 Partner 找锚，接收对象就是锚。
            var hub = driver as JobDriver_SexBaseReciever ?? driver.Partner?.jobs?.curDriver as JobDriver_SexBaseReciever;
            if (hub == null || !IsCurrentJob(hub.pawn, hub) || IsSscScene(hub) || SSCUapGroupApi.IsGroupSceneDriver(hub)) return false;
            scene = new SSCPartnerCorruptionScene(driver, hub, null);
            return scene.TryGetParticipants(out participants);
        }

        internal bool TryGetParticipants(out List<Pawn> participants)
        {
            participants = null;
            if (!target.Current) return false;
            // 复查时只能沿最初的会话。即使人物与 Job 仍在，也不能让新建会话承接旧事件。
            if (uapSession != null)
                return TryGetUapParticipants(target.Driver, out object current, out participants) && ReferenceEquals(current, uapSession);

            if (!receiver.Current || receiver.Driver is not JobDriver_SexBaseReciever hub || hub.parteners == null) return false;
            if (target.Pawn != receiver.Pawn && (!hub.parteners.Contains(target.Pawn) || !IsRjwInitiator(target.Pawn, hub))) return false;

            // 登记表会混有准备或离场的人。只让仍实际执行的任务决定场次用途，不能被陈旧 SSC 槽位误挡。
            foreach (Pawn pawn in hub.parteners)
                if (IsRjwInitiator(pawn, hub) && IsSscScene(pawn.jobs.curDriver)) return false;
            participants = hub.parteners;
            return true;
        }

        internal bool TryGetMember(Pawn pawn, List<Pawn> participants, out Member member)
        {
            member = default;
            var driver = pawn?.jobs?.curDriver;
            if (!IsCurrentJob(pawn, driver) || IsSscScene(driver)) return false;
            // 只查「是否实际同场」。普通 RJW 复核每名发起者是否指回共享接收者，
            // UAP 交给原会话和角色阶段；任一分支都不要求这个人与高潮者直接配对。
            if (uapSession != null)
            {
                if (!participants.Contains(pawn) || !SSCUapGroupRuntime.IsActiveSceneParticipant(uapSession, driver)) return false;
            }
            else if (pawn != receiver.Pawn && (!participants.Contains(pawn) || !IsRjwInitiator(pawn, (JobDriver_SexBaseReciever)receiver.Driver)))
                return false;
            member = new Member(driver);
            return true;
        }

        /// <summary>接收名单在 Start 前也可能登记；用发起者当前的方向和 SexProps 排除仅候选或旧场记录。</summary>
        private static bool IsRjwInitiator(Pawn pawn, JobDriver_SexBaseReciever hub)
        {
            if (pawn == hub.pawn || pawn?.jobs?.curDriver is not JobDriver_SexBaseInitiator driver ||
                !IsCurrentJob(pawn, driver) || !SSCOrgasmProgressUtility.IsCurrentDriver(driver) ||
                SSCUapGroupApi.IsGroupSceneDriver(driver) || pawn.Map != hub.pawn.Map || driver.Partner != hub.pawn) return false;
            // 接收者自己的 SexProps 可能只保留首个发起者，用它校验全名单会漏掉后来进入同场的人。
            return driver.Sexprops.initiator == pawn && driver.Sexprops.recipient == hub.pawn;
        }

        // UAP 的共享接收端若已退出，残留发起任务即使尚在当前 Job 也不再组成原场次。
        private static bool TryGetUapParticipants(JobDriver driver, out object session, out List<Pawn> participants)
        {
            if (!SSCUapGroupApi.TryGetActiveScene(driver.pawn, out session, out participants) ||
                !SSCUapGroupRuntime.IsActiveSceneParticipant(session, driver)) return false;
            Pawn hub = SSCUapGroupApi.Get<Pawn>(session, "Receiver");
            return hub == driver.pawn || SSCUapGroupRuntime.IsActiveSceneParticipant(session, hub?.jobs?.curDriver);
        }
    }
}
