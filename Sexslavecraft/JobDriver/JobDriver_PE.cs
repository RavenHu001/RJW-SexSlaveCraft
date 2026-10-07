using RimWorld;
using rjw;
using System.Collections.Generic;
using Verse;
using Verse.AI;

// EN: This file implements personality excretion as a sex job.
// EN: It finishes the RJW scene, produces personality gel, and leaves the victim behind as a hollow pawn.
// CN: 这个文件实现“人格排泄”对应的性行为 Job。
// CN: 它会完成 RJW 场景、生成对应的人格凝胶，并把受害者留在场上作为空壳 Pawn。
namespace SexSlaveCraft
{
    public class JobDriver_PE : JobDriver_SexBaseInitiator
    {
        private readonly JobDef partnerJob = SSCDefOf.SSC_TrainingReceiver;

        /// <summary>为人格排泄任务预约唯一接收目标；预约冲突时返回 false。</summary>
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (PersonalityExcretionJobUtility.IsOccupiedByOther(Partner, pawn)) return false;
            return pawn.Reserve(Partner, job, 1, 0, null, errorOnFailed);
        }

        /// <summary>构造移动、接收方准备、场景执行和人格提取步骤，并在目标状态失效时中止任务。</summary>
        protected override IEnumerable<Toil> MakeNewToils()
        {
            setup_ticks();

            this.FailOnDespawnedNullOrForbidden(iTarget);
            this.FailOn(() => pawn.Drafted || pawn.IsFighting());
            this.FailOn(() => Partner.IsFighting());
            this.FailOn(() => !pawn.CanReserve(Partner, 1, 0));
            // 候选或在途任务可能早于另一位执行者开始；已有有效配对时只退出后来者。
            this.FailOn(() => PersonalityExcretionJobUtility.IsOccupiedByOther(Partner, pawn));

            // 状态检查回调：兔子分身或已失去人格排泄状态的目标不能继续此任务。
            this.FailOn(() =>
            {
                if (RabbitCloneUtility.IsRabbitClone(Partner)) return true;
                Hediff h = Partner.health.hediffSet.GetFirstHediffOfDef(SSCDefOf.SSC_PersonalityExcreting);
                return h == null;
            });

            yield return Toils_Goto.GotoThing(iTarget, PathEndMode.OnCell);

            // EN: Step 1: start the receiver Job so the victim enters the same personality-excretion scene.
            // CN: 步骤 1：启动 receiver Job，让受害者进入同一段人格排泄场景。
            Toil startPartnerJob = new Toil();
            startPartnerJob.defaultCompleteMode = ToilCompleteMode.Instant;
            // 准备回调：创建接收任务，失败时记录原因并结束本次发起任务。
            startPartnerJob.initAction = delegate
            {
                // 固定本次排泄任务和目标，避免接收启动的外部回调使 Partner 指向复用后的任务。
                var context = new TrainingJobUtility.JobContext(this);
                if (!context.IsCurrent) return;
                Pawn target = Partner;
                bool started = TrainingJobUtility.TryStartPersonalityExcretionReceiver(pawn, target, job, partnerJob);
                // 交接期间换工作时直接退出，不对新任务执行 EndCurrentJob 或写失败冷却。
                if (!context.IsCurrent) return;
                if (!started)
                {
                    // 争用只是本执行者未取得目标，不污染正在进行的任务或目标的身体校验冷却。
                    if (!PersonalityExcretionJobUtility.IsOccupiedByOther(target, pawn))
                        TrainingJobUtility.MarkValidationFailure(target, "SSC_PE_RECEIVER");
                    if (context.IsCurrent) pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
                }
            };
            yield return startPartnerJob;

            // EN: Step 2: run the RJW scene with the fixed anal setup used by personality excretion.
            // CN: 步骤 2：用人格排泄固定的 anal 设定跑完这段 RJW 场景。
            Toil sexToil = new Toil();
            sexToil.defaultCompleteMode = ToilCompleteMode.Never;
            sexToil.defaultDuration = duration;
            sexToil.handlingFacing = true;

            // 开始回调：同步双方和行为数据，通过校验且 Start 未中止任务后才通知对话兼容层。
            sexToil.initAction = delegate
            {
                // 场景开始前的第二次位置同步也必须保留发起任务，且允许外部正常取消。
                var context = new TrainingJobUtility.JobContext(this);
                if (!context.IsCurrent) return;
                if (!TrainingJobUtility.SyncPartnerPosition(pawn, Partner))
                {
                    // 失败处理只终止仍然有效的原任务，保留同步期间接管的其他工作。
                    if (context.IsCurrent) pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
                    return;
                }
                TrainingJobUtility.EnsureAwake(Partner);

                // EN: Personality excretion is fixed to anal so the generated SexProps always match this excretion scene.
                // CN: 人格排泄流程固定使用 anal，保证生成出来的 SexProps 始终和排泄场景一致。
                if (Sexprops == null)
                {
                    Sexprops = SexUtility.SelectSextype(pawn, Partner, false, false);
                    // 外部动作选择结束后确认归属，再应用本次场景固定的动作参数。
                    if (!context.IsCurrent) return;
                    RJWSexPropsUtility.ApplySexType(Sexprops, pawn, Partner, xxx.rjwSextype.Anal, "Sex_Anal");
                }

                if (!TrainingJobUtility.TryValidateStartOrAbort(pawn, Partner, "SSC_PE"))
                {
                    return;
                }
                // 家具同步可能重入任务调度；不能把旧场景的同步失败施加到新工作上。
                bool synchronized = OnaholeCompatibilityUtility.TrySynchronizeOnaholeSexProps(Partner, Sexprops);
                if (!context.IsCurrent) return;
                if (!synchronized)
                {
                    TrainingJobUtility.MarkValidationFailure(Partner, "SSC_PE_ONAHOLE_PROPS");
                    pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
                    return;
                }
                SSCLog.Verbose($"[SSC_PE] Start personality excretion scene: actor={pawn.LabelShort}, victim={Partner.LabelShort}");
                Start();
                // Start 也可能取消任务；不允许失效回调继续进入后续场景逻辑。
                if (!context.IsCurrent) return;
            };

            // 每帧回调：维持位置、更新 RJW 与体力计时，时长结束后进入提取步骤。
            sexToil.tickAction = delegate
            {
                if ((Find.TickManager.TicksGame + pawn.thingIDNumber) % ticks_between_hearts == 0)
                {
                    ThrowMetaIconF(pawn.Position, pawn.Map, FleckDefOf.Heart);
                }

                if (pawn.Position != Partner.Position) pawn.Position = Partner.Position;

                SexTick(pawn, Partner);
                SexUtility.reduce_rest(Partner);
                SexUtility.reduce_rest(pawn, 2f);

                if (ticks_left <= 0) ReadyForNextToil();
            };

            // 接收方检查回调：目标脱离预期接收任务时记录失败原因并中止场景。
            sexToil.FailOn(() =>
            {
                bool invalidReceiver = !OnaholeCompatibilityUtility.IsValidTrainingReceiver(Partner, partnerJob);
                if (invalidReceiver)
                {
                    TrainingJobUtility.MarkValidationFailure(Partner, "SSC_PE_RECEIVER_LOST");
                }
                return invalidReceiver;
            });
            // 收尾回调：清理 RJW 场景并解除与兼容设备的参与者关联。
            sexToil.AddFinishAction(delegate
            {
                base.End();
                OnaholeCompatibilityUtility.TryUnregisterOnaholePartner(Partner, pawn);
            });

            yield return sexToil;

            // EN: Step 3: after the scene ends, convert the victim into personality gel plus a hollow pawn body.
            // CN: 步骤 3：场景结束后，把受害者转换成人格凝胶加空壳 Pawn 身体。
            yield return new Toil
            {
                defaultCompleteMode = ToilCompleteMode.Instant,
                initAction = delegate
                {
                    CompletePersonalityExtraction();
                }
            };
        }

        /// <summary>结算已完成场景，保存人格凝胶、更新目标为空壳状态，并施加提取后的恢复期。</summary>
        private void CompletePersonalityExtraction()
        {
            Pawn victim = Partner;
            SexUtility.ProcessSex(Sexprops);

            // EN: Step 1: create the personality gel and store the victim's personality payload inside it.
            // CN: 步骤 1：生成人格凝胶，并把受害者的人格载荷写进去。
            Thing product = CreateStoredPersonalityProduct(victim);
            if (product.TryGetComp<CompPersonalityStore>() != null)
            {
                // 两条抽取入口都在成功保存快照后清源成果，避免场景抽取留下猫狗技能或历史。
                PetCatAbilityUtility.DetachAfterExtraction(victim);
                PetDogAbilityUtility.DetachAfterExtraction(victim);
                CombatantSpecializationGelUtility.DetachAfterExtraction(victim);
            }

            // EN: Step 2: strip the victim into a hollow pawn and swap the personality-excretion state hediff.
            // CN: 步骤 2：把受害者剥离成空壳 Pawn，并切换人格排泄状态 Hediff。
            StripVictimPersonality(victim);
            ReplaceExcretionHediff(victim);
            GenSpawn.Spawn(product, victim.Position, victim.Map);

            // EN: Step 3: add the post-excretion coma so personality insertion cannot happen immediately.
            // CN: 步骤 3：补上人格排泄适应症，避免立刻继续做人格植入流程。
            Messages.Message(Strings.Message_PersonalityExcretionComplete(victim.LabelShort), victim, MessageTypeDefOf.NeutralEvent);
            AddPostExcretionComa(victim);
            SSCLog.Important($"[SSC_PE] Personality excretion completed: victim={victim.LabelShort}, product={product.def.defName}, hollowState={victim.health.hediffSet.HasHediff(SSCDefOf.SSC_PersonalityExcreted_Done)}");
        }

        /// <summary>创建适合目标的人格凝胶并写入快照；物品缺少存储组件时记录错误，仍返回已创建物品。</summary>
        private static Thing CreateStoredPersonalityProduct(Pawn victim)
        {
            ThingDef productDef = PersonalityGelUtility.GetPersonalityGelDefForPawn(victim);
            Thing product = ThingMaker.MakeThing(productDef);
            CompPersonalityStore storeComp = product.TryGetComp<CompPersonalityStore>();
            if (storeComp != null)
            {
                storeComp.StorePawnData(victim);
            }
            else
            {
                Log.Error("[SSC] 生成的物品缺少 CompPersonalityStore，数据丢失！");
            }

            return product;
        }

        /// <summary>清除目标关系、补上人格排泄特质，并将社交及其他技能调整到空壳身体的默认水平。</summary>
        private static void StripVictimPersonality(Pawn victim)
        {
            // EN: The body stays on the map, but its old social identity must be scrubbed before personality insertion.
            // CN: 身体会继续留在地图上，但旧的人格标记必须先被抹掉，才能进行后续人格植入。
            victim.relations?.ClearAllRelations();

            if (victim.story != null && !victim.story.traits.HasTrait(SSCDefOf.SSC_Trait_PE))
            {
                victim.story.traits.GainTrait(new Trait(SSCDefOf.SSC_Trait_PE));
            }

            if (victim.skills == null) return;
            foreach (SkillRecord skill in victim.skills.skills)
            {
                skill.Level = skill.def == SkillDefOf.Social ? 0 : 6;
                skill.xpSinceLastLevel = 0;
            }
        }

        /// <summary>移除正在人格排泄的状态，并添加排泄完成状态，标记目标已成为可植入的空壳。</summary>
        private static void ReplaceExcretionHediff(Pawn victim)
        {
            Hediff oldHediff = victim.health.hediffSet.GetFirstHediffOfDef(SSCDefOf.SSC_PersonalityExcreting);
            if (oldHediff != null)
            {
                victim.health.RemoveHediff(oldHediff);
            }

            victim.health.AddHediff(SSCDefOf.SSC_PersonalityExcreted_Done);
        }

        // EN: This post-excretion coma is the recovery gate after excretion, matching the insertion-side coma flow.
        // CN: 这个“人格排泄适应症”就是排泄后的恢复门槛，它和植入侧的昏迷流程相互对应。
        /// <summary>在定义可用时施加人格排泄适应症，并将新状态的严重度设为 0.5。</summary>
        private static void AddPostExcretionComa(Pawn victim)
        {
            if (SSCDefOf.SSC_PostExcretionComa == null) return;

            Hediff coma = victim.health.AddHediff(SSCDefOf.SSC_PostExcretionComa);
            if (coma != null)
            {
                coma.Severity = 0.5f;
            }
        }
    }
}
