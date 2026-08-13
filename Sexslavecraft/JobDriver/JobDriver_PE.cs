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

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(Partner, job, 1, 0, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            setup_ticks();

            this.FailOnDespawnedNullOrForbidden(iTarget);
            this.FailOn(() => pawn.Drafted || pawn.IsFighting());
            this.FailOn(() => Partner.IsFighting());
            this.FailOn(() => !pawn.CanReserve(Partner, 1, 0));

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
            startPartnerJob.initAction = delegate
            {
                if (!TrainingJobUtility.TryStartPersonalityExcretionReceiver(pawn, Partner, job, partnerJob))
                {
                    TrainingJobUtility.MarkValidationFailure(Partner, "SSC_PE_RECEIVER");
                    pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
                }
            };
            yield return startPartnerJob;

            // EN: Step 2: run the RJW scene with the fixed anal setup used by personality excretion.
            // CN: 步骤 2：用人格排泄固定的 anal 设定跑完这段 RJW 场景。
            Toil sexToil = new Toil();
            sexToil.defaultCompleteMode = ToilCompleteMode.Never;
            sexToil.defaultDuration = duration;
            sexToil.handlingFacing = true;

            sexToil.initAction = delegate
            {
                TrainingJobUtility.SyncPartnerPosition(pawn, Partner);
                TrainingJobUtility.EnsureAwake(Partner);

                // EN: Personality excretion is fixed to anal so the generated SexProps always match this excretion scene.
                // CN: 人格排泄流程固定使用 anal，保证生成出来的 SexProps 始终和排泄场景一致。
                if (Sexprops == null)
                {
                    Sexprops = SexUtility.SelectSextype(pawn, Partner, false, false);
                    RJWSexPropsUtility.ApplySexType(Sexprops, pawn, Partner, xxx.rjwSextype.Anal, "Sex_Anal");
                }

                if (!TrainingJobUtility.TryValidateStartOrAbort(pawn, Partner, "SSC_PE"))
                {
                    return;
                }
                if (!OnaholeCompatibilityUtility.TrySynchronizeOnaholeSexProps(Partner, Sexprops))
                {
                    TrainingJobUtility.MarkValidationFailure(Partner, "SSC_PE_ONAHOLE_PROPS");
                    pawn.jobs.EndCurrentJob(JobCondition.Incompletable);
                    return;
                }
                SSCLog.Verbose($"[SSC_PE] Start personality excretion scene: actor={pawn.LabelShort}, victim={Partner.LabelShort}");
                Start();
                RimTalkCompatibilityUtility.NotifySexStarted(pawn, Partner, "personality excretion", "Anal");
            };

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

            sexToil.FailOn(() =>
            {
                bool invalidReceiver = !OnaholeCompatibilityUtility.IsValidTrainingReceiver(Partner, partnerJob);
                if (invalidReceiver)
                {
                    TrainingJobUtility.MarkValidationFailure(Partner, "SSC_PE_RECEIVER_LOST");
                }
                return invalidReceiver;
            });
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

        private void CompletePersonalityExtraction()
        {
            Pawn victim = Partner;
            SexUtility.ProcessSex(Sexprops);

            // EN: Step 1: create the personality gel and store the victim's personality payload inside it.
            // CN: 步骤 1：生成人格凝胶，并把受害者的人格载荷写进去。
            Thing product = CreateStoredPersonalityProduct(victim);

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
