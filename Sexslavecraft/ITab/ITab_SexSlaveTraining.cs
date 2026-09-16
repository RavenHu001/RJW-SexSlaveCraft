using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;

// EN: This file renders the training control tab for eligible pawns.
// EN: It lets the player toggle training, pick acts, and constrain trainer assignment.
// CN: 这个文件绘制可用 Pawn 的调教控制 ITab。
// CN: 它允许玩家开关调教、选择姿势，并限制 trainer 指派。
namespace SexSlaveCraft
{
    public class ITab_SexSlaveTraining : ITab
    {
        private const float SectionSpacing = 12f;
        private static readonly Vector2 WinSize = new Vector2(360f, 560f);

        private Vector2 scrollPosition;

        public ITab_SexSlaveTraining()
        {
            size = WinSize;
            labelKey = "Tab_SexSlaveTraining";
            tutorTag = "SexSlaveTraining";
        }

        public override bool IsVisible
        {
            get
            {
                Pawn p = SelPawn;
                if (p == null) return false;
                if (!SSCIdentityUtility.IsSupportedVanillaStatus(p)) return false;
                return p.GetComp<CompSexSlaveTraining>() != null;
            }
        }

        /// <summary>绘制调教总栏；即使菜单或控件抛出异常，也在内部列表结束后关闭外层滚动区域。</summary>
        protected override void FillTab()
        {
            Pawn pawn = SelPawn;
            if (pawn == null) return;

            CompSexSlaveTraining comp = pawn.GetComp<CompSexSlaveTraining>();
            if (comp == null) return;

            Rect outerRect = new Rect(0f, 0f, size.x, size.y).ContractedBy(10f);
            float contentHeight = CalculateContentHeight(pawn, comp);
            Rect viewRect = new Rect(0f, 0f, outerRect.width, outerRect.height);
            Rect contentRect = new Rect(0f, 0f, outerRect.width - 18f, contentHeight);

            Widgets.BeginScrollView(viewRect, ref scrollPosition, contentRect);
            try
            {
                float curY = 0f;
                curY = DrawIdentitySection(new Rect(0f, curY, contentRect.width, GetIdentitySectionHeight(comp)), comp) + SectionSpacing;

                if (comp.pawnIdentity == PawnIdentity.Master)
                {
                    DrawMessageSection(new Rect(0f, curY, contentRect.width, 78f), Strings.ITab_AlreadyMaster, Color.cyan);
                }
                else if (comp.pawnIdentity == PawnIdentity.Unset)
                {
                    DrawMessageSection(new Rect(0f, curY, contentRect.width, 78f), Strings.ITab_IdentityUnset, Color.yellow);
                }
                else if (!ResearchUtils.IsResearchFinished(SSCDefOf.SSC_BasicTraining))
                {
                    DrawLockedSection(new Rect(0f, curY, contentRect.width, 108f));
                }
                else
                {
                    curY = DrawTrainingSection(new Rect(0f, curY, contentRect.width, 124f), pawn, comp) + SectionSpacing;
                    curY = DrawScheduleSection(new Rect(0f, curY, contentRect.width, 210f), pawn, comp) + SectionSpacing;
                    curY = DrawSpecializationSection(new Rect(0f, curY, contentRect.width, 124f), pawn, comp) + SectionSpacing;

                    if (pawn.health.hediffSet.HasHediff(SSCDefOf.SSC_Lactating_SubState))
                    {
                        curY = DrawMilkSection(new Rect(0f, curY, contentRect.width, 88f), pawn, comp) + SectionSpacing;
                    }

                    curY = DrawPoseSection(new Rect(0f, curY, contentRect.width, 82f), comp) + SectionSpacing;
                    DrawTrainerSection(new Rect(0f, curY, contentRect.width, 82f), pawn, comp);
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        private static float CalculateContentHeight(Pawn pawn, CompSexSlaveTraining comp)
        {
            float height = GetIdentitySectionHeight(comp);

            if (comp.pawnIdentity == PawnIdentity.Master || comp.pawnIdentity == PawnIdentity.Unset)
            {
                return height + SectionSpacing + 84f;
            }

            if (!ResearchUtils.IsResearchFinished(SSCDefOf.SSC_BasicTraining))
            {
                return height + SectionSpacing + 112f;
            }

            height += SectionSpacing + 124f;
            height += SectionSpacing + 210f;
            height += SectionSpacing + 124f;
            if (pawn.health.hediffSet.HasHediff(SSCDefOf.SSC_Lactating_SubState))
            {
                height += SectionSpacing + 88f;
            }

            height += SectionSpacing + 82f;
            height += SectionSpacing + 82f;
            return height + 4f;
        }

        /// <summary>为身份按钮、调教员开关和性奴接收状态预留高度。</summary>
        private static float GetIdentitySectionHeight(CompSexSlaveTraining comp)
        {
            return comp.pawnIdentity == PawnIdentity.Slave ? 146f : 126f;
        }

        /// <summary>绘制 SSC 身份及共用调教员开关；主人和未选择显示不可修改的固定状态。</summary>
        private float DrawIdentitySection(Rect rect, CompSexSlaveTraining comp)
        {
            DrawSection(rect, Strings.ITab_IdentityHeader, delegate(Rect innerRect)
            {
                Listing_Standard listing = BeginSectionListing(innerRect);
                try
                {

                    string idLabel = GetIdentityLabel(comp.pawnIdentity);
                    if (listing.ButtonText(idLabel))
                    {
                        List<FloatMenuOption> identityOptions = new List<FloatMenuOption>
                        {
                            new FloatMenuOption(Strings.ITab_SetIdentityUnset, delegate
                            {
                                SSCIdentityUtility.TrySetIdentity(SelPawn, PawnIdentity.Unset);
                            }),
                            new FloatMenuOption(Strings.ITab_SetAsSlave, delegate
                            {
                                SSCIdentityUtility.TrySetIdentity(SelPawn, PawnIdentity.Slave);
                            }),
                            new FloatMenuOption(Strings.ITab_SetAsMaster, delegate
                            {
                                SSCIdentityUtility.TrySetIdentity(SelPawn, PawnIdentity.Master);
                            })
                        };
                        Find.WindowStack.Add(new FloatMenu(identityOptions));
                    }

                    listing.Gap(6f);
                    DrawTrainerIdentityToggle(listing, SelPawn, comp);

                    if (comp.pawnIdentity == PawnIdentity.Slave)
                    {
                        listing.Gap(6f);
                        GUI.color = new Color(0.75f, 0.75f, 0.75f);
                        listing.Label(comp.IsEnabled ? Strings.ITab_StatusReady : Strings.ITab_StatusDisabled);
                        GUI.color = Color.white;
                    }

                }
                finally
                {
                    listing.End();
                }
            });

            return rect.yMax;
        }

        /// <summary>仅性奴可修改个人开关；绘制后恢复 GUI 状态，并说明身份与工作开关的区别。</summary>
        private static void DrawTrainerIdentityToggle(Listing_Standard listing, Pawn pawn, CompSexSlaveTraining comp)
        {
            bool enabled = SSCIdentityUtility.IsTrainer(pawn);
            bool previous = enabled;
            string tooltip = (comp.pawnIdentity == PawnIdentity.Master ? "SSC_TrainerIdentity_MasterTip"
                : comp.pawnIdentity == PawnIdentity.Slave ? "SSC_TrainerIdentity_SlaveTip"
                : "SSC_TrainerIdentity_UnsetTip").Translate();
            bool oldEnabled = GUI.enabled;
            try
            {
                GUI.enabled = oldEnabled && comp.pawnIdentity == PawnIdentity.Slave;
                listing.CheckboxLabeled("SSC_TrainerIdentity_Label".Translate(), ref enabled, tooltip);
            }
            finally
            {
                GUI.enabled = oldEnabled;
            }
            if (enabled != previous) SSCIdentityUtility.SetTrainerEnabled(pawn, enabled);
        }

        /// <summary>绘制调教设置，并保证控件中断时结束本节列表。</summary>
        private float DrawTrainingSection(Rect rect, Pawn pawn, CompSexSlaveTraining comp)
        {
            DrawSection(rect, Strings.ITab_TrainingSettingsHeader, delegate(Rect innerRect)
            {
                Listing_Standard listing = BeginSectionListing(innerRect);
                try
                {
                    DrawTrainingToggle(listing, pawn, comp);
                    listing.Gap(8f);
                    DrawCooldownStatus(listing, comp);
                    listing.Gap(8f);
                    DrawAllowOthersToggle(listing, pawn, comp);
                }
                finally
                {
                    listing.End();
                }
            });

            return rect.yMax;
        }

        /// <summary>绘制特化菜单，并保证菜单创建失败时结束本节列表。</summary>
        private float DrawSpecializationSection(Rect rect, Pawn pawn, CompSexSlaveTraining comp)
        {
            DrawSection(rect, Strings.ITab_SpecializationHeader, delegate(Rect innerRect)
            {
                Listing_Standard listing = BeginSectionListing(innerRect);
                try
                {
                    DrawSpecializationSelector(listing, pawn, comp);
                }
                finally
                {
                    listing.End();
                }
            });

            return rect.yMax;
        }

        /// <summary>绘制调教排班，并保证控件中断时结束本节列表。</summary>
        private float DrawScheduleSection(Rect rect, Pawn pawn, CompSexSlaveTraining comp)
        {
            DrawSection(rect, "SSC_Schedule_Header".Translate(), delegate(Rect innerRect)
            {
                Listing_Standard listing = BeginSectionListing(innerRect);
                try
                {

                    bool enabled = comp.scheduledTrainingEnabled;
                    listing.CheckboxLabeled("SSC_Schedule_Enable".Translate(), ref enabled);
                    comp.scheduledTrainingEnabled = enabled;

                    if (enabled)
                    {
                        listing.Gap(4f);
                        string hourLabel = "SSC_Schedule_HourButton".Translate(
                            comp.scheduledTrainingHour.ToString("00"),
                            comp.ScheduledTrainingEndHour.ToString("00"));
                        if (listing.ButtonText(hourLabel))
                        {
                            List<FloatMenuOption> options = new List<FloatMenuOption>();
                            for (int hour = 0; hour < 24; hour++)
                            {
                                int selectedHour = hour;
                                int endHour = (hour + CompSexSlaveTraining.ScheduledTrainingWindowHours) % 24;
                                string label = "SSC_Schedule_HourOption".Translate(
                                    hour.ToString("00"),
                                    endHour.ToString("00"));
                                options.Add(new FloatMenuOption(label, delegate
                                {
                                    comp.scheduledTrainingHour = selectedHour;
                                }));
                            }
                            Find.WindowStack.Add(new FloatMenu(options));
                        }

                        string intervalLabel = "SSC_Schedule_IntervalButton".Translate(comp.scheduledTrainingIntervalDays);
                        if (listing.ButtonText(intervalLabel))
                        {
                            List<FloatMenuOption> options = new List<FloatMenuOption>();
                            for (int days = 1; days <= 7; days++)
                            {
                                int selectedDays = days;
                                options.Add(new FloatMenuOption(
                                    "SSC_Schedule_IntervalOption".Translate(days),
                                    delegate { comp.scheduledTrainingIntervalDays = selectedDays; }));
                            }
                            Find.WindowStack.Add(new FloatMenu(options));
                        }

                        listing.Gap(4f);
                        GUI.color = comp.IsScheduledTrainingAvailableNow
                            ? new Color(0.45f, 1f, 0.55f)
                            : new Color(0.75f, 0.75f, 0.75f);
                        TaggedString scheduleStatus = comp.IsScheduledTrainingAvailableNow
                            ? "SSC_Schedule_StatusNow".Translate()
                            : "SSC_Schedule_StatusWaiting".Translate();
                        listing.Label(scheduleStatus);
                        GUI.color = Color.white;
                    }
                    else
                    {
                        listing.Gap(6f);
                        GUI.color = new Color(0.75f, 0.75f, 0.75f);
                        listing.Label("SSC_Schedule_StatusAnytime".Translate());
                        GUI.color = Color.white;
                    }

                }
                finally
                {
                    listing.End();
                }
            });

            return rect.yMax;
        }

        /// <summary>绘制泌乳配置与进度，并保证异常路径结束本节列表。</summary>
        private float DrawMilkSection(Rect rect, Pawn pawn, CompSexSlaveTraining comp)
        {
            DrawSection(rect, Strings.ITab_MilkProductionToggle, delegate(Rect innerRect)
            {
                Listing_Standard listing = BeginSectionListing(innerRect);
                try
                {
                    DrawMilkToggle(listing, pawn, comp);
                    listing.Gap(6f);

                    Hediff lactating = pawn.health.hediffSet.GetFirstHediffOfDef(SSCDefOf.SSC_Lactating_SubState);
                    HediffComp_PermanentLactating lactatingComp = lactating?.TryGetComp<HediffComp_PermanentLactating>();
                    if (lactatingComp != null)
                    {
                        string progress = lactatingComp.CompLabelInBracketsExtra;
                        if (!string.IsNullOrEmpty(progress))
                        {
                            GUI.color = new Color(0.75f, 0.75f, 0.75f);
                            listing.Label(progress);
                            GUI.color = Color.white;
                        }
                    }

                }
                finally
                {
                    listing.End();
                }
            });

            return rect.yMax;
        }

        /// <summary>绘制姿势选择菜单，并保证菜单创建失败时结束本节列表。</summary>
        private float DrawPoseSection(Rect rect, CompSexSlaveTraining comp)
        {
            DrawSection(rect, Strings.ITab_PoseHeader, delegate(Rect innerRect)
            {
                Listing_Standard listing = BeginSectionListing(innerRect);
                try
                {
                    DrawPoseSelector(listing, comp);
                }
                finally
                {
                    listing.End();
                }
            });

            return rect.yMax;
        }

        /// <summary>绘制指定调教员菜单，并保证候选生成异常时结束本节列表。</summary>
        private float DrawTrainerSection(Rect rect, Pawn pawn, CompSexSlaveTraining comp)
        {
            DrawSection(rect, Strings.ITab_TrainerHeader, delegate(Rect innerRect)
            {
                Listing_Standard listing = BeginSectionListing(innerRect);
                try
                {
                    DrawTrainerSelector(listing, pawn, comp);
                }
                finally
                {
                    listing.End();
                }
            });

            return rect.yMax;
        }

        /// <summary>绘制研究锁定说明，并保证异常路径结束本节列表。</summary>
        private void DrawLockedSection(Rect rect)
        {
            DrawSection(rect, Strings.ITab_TrainingLocked, delegate(Rect innerRect)
            {
                Listing_Standard listing = BeginSectionListing(innerRect);
                try
                {
                    GUI.color = Color.yellow;
                    listing.Label(Strings.ITab_TrainingLockedDesc);
                    GUI.color = Color.white;
                }
                finally
                {
                    listing.End();
                }
            });
        }

        private void DrawMessageSection(Rect rect, string message, Color color)
        {
            DrawSection(rect, string.Empty, delegate(Rect innerRect)
            {
                Text.Anchor = TextAnchor.MiddleLeft;
                GUI.color = color;
                Widgets.Label(innerRect, message);
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
            });
        }

        private static Listing_Standard BeginSectionListing(Rect innerRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(innerRect);
            return listing;
        }

        private static void DrawSection(Rect rect, string title, Action<Rect> drawContents)
        {
            Widgets.DrawMenuSection(rect);

            Rect innerRect = rect.ContractedBy(12f);
            if (!string.IsNullOrEmpty(title))
            {
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(innerRect.x, innerRect.y, innerRect.width, 28f), title);
                innerRect.yMin += 28f;
            }

            drawContents(innerRect);
        }

        /// <summary>列出同时满足身份、工作及主人限制的可指派对象，清空入口始终保留。</summary>
        private List<FloatMenuOption> GetTrainerOptions(Pawn slave)
        {
            List<FloatMenuOption> list = new List<FloatMenuOption>();
            List<Pawn> candidates = TrainerAssignmentUtility.GetTrainerCandidates(slave).ToList();
            Pawn forcedMaster = TrainerAssignmentUtility.GetForcedMaster(slave);

            if (candidates.Count == 0)
            {
                list.Add(new FloatMenuOption(Strings.ITab_NoTrainerAvailable, null));
                list.Add(new FloatMenuOption(Strings.ITab_ClearTrainer, delegate
                {
                    SSCBondUtility.TryAssignTrainer(slave, null);
                }));
                return list;
            }

            foreach (Pawn candidate in candidates)
            {
                string label = candidate.LabelShort;
                Action action;

                if (forcedMaster != null && candidate == forcedMaster)
                {
                    label += Strings.ITab_TrainerMasterSuffix;
                }

                action = delegate { SSCBondUtility.TryAssignTrainer(slave, candidate); };
                list.Add(new FloatMenuOption(label, action));
            }

            list.Add(new FloatMenuOption(Strings.ITab_ClearTrainer, delegate { SSCBondUtility.TryAssignTrainer(slave, null); }));
            return list;
        }

        private static void DrawTrainingToggle(Listing_Standard listing, Pawn pawn, CompSexSlaveTraining comp)
        {
            bool isEnabled = comp.IsEnabled;
            bool oldState = isEnabled;

            listing.CheckboxLabeled(Strings.ITab_AllowTraining, ref isEnabled);
            if (oldState == isEnabled) return;

            comp.mode = isEnabled ? TrainingMode.Enabled : TrainingMode.Disabled;
            comp.isBeingTrained = false;

            if (!isEnabled) return;
            if (Trainjudge.TryCanBeFuckedWithReason(pawn, out string shortReason, out string detailedReport))
            {
                Messages.Message("SSC_Message_TrainingEnabledEligible".Translate(), pawn, MessageTypeDefOf.NeutralEvent, false);
                return;
            }

            Messages.Message(shortReason, pawn, MessageTypeDefOf.RejectInput, false);
            Log.Warning($"[SSC_ITAB] Training enable check failed: {pawn.LabelShort}. {shortReason}\n{detailedReport}");
        }

        private static string GetIdentityLabel(PawnIdentity identity)
        {
            switch (identity)
            {
                case PawnIdentity.Master:
                    return Strings.ITab_IdentityMaster;
                case PawnIdentity.Slave:
                    return Strings.ITab_IdentitySlave;
                default:
                    return Strings.ITab_IdentityUnset;
            }
        }

        private static void DrawCooldownStatus(Listing_Standard listing, CompSexSlaveTraining comp)
        {
            if (comp.IsOnCooldown)
            {
                int ticksLeft = CompSexSlaveTraining.CooldownTicks - (Find.TickManager.TicksGame - comp.lastTrainingTick);
                listing.Label((TaggedString)Strings.ITab_CooldownStatus(ticksLeft.ToStringTicksToPeriod()));
                return;
            }

            listing.Label(comp.IsEnabled ? (TaggedString)Strings.ITab_StatusReady : (TaggedString)Strings.ITab_StatusDisabled);
        }

        private static void DrawPoseSelector(Listing_Standard listing, CompSexSlaveTraining comp)
        {
            string modeLabel = Strings.GetModeLabel(comp.selectedMode);
            if (!listing.ButtonText(modeLabel)) return;

            List<FloatMenuOption> acts = new List<FloatMenuOption>();
            foreach (TrainingActType type in Enum.GetValues(typeof(TrainingActType)))
            {
                acts.Add(new FloatMenuOption(Strings.GetModeLabel(type), delegate { comp.selectedMode = type; }));
            }

            Find.WindowStack.Add(new FloatMenu(acts));
        }

        /// <summary>显示原始指派及停用/暂不可执行状态，允许直接重新选择。</summary>
        private void DrawTrainerSelector(Listing_Standard listing, Pawn pawn, CompSexSlaveTraining comp)
        {
            string trainerName = TrainerAssignmentUtility.GetAssignedTrainerLabel(pawn);
            if (listing.ButtonText(trainerName))
            {
                Find.WindowStack.Add(new FloatMenu(GetTrainerOptions(pawn)));
            }
        }

        private static void DrawSpecializationSelector(Listing_Standard listing, Pawn pawn, CompSexSlaveTraining comp)
        {
            string currentLabel = GetSpecializationLabel(pawn, comp);

            if (listing.ButtonText(currentLabel))
            {
                string busOptionLabel = Strings.ITab_SelectSpecializationBus;
                bool busFinalized = BusSpecializationUtility.HasFinalBusState(pawn);
                bool busEnabled = !busFinalized;
                string busDisabledReason = null;
                if (busEnabled)
                {
                    busEnabled = BusSpecializationUtility.CanUseBusSpecialization(pawn, out busDisabledReason);
                }

                if (busFinalized)
                {
                    busOptionLabel = busOptionLabel + " " + Strings.ITab_SpecializationFinalizedSuffix;
                }
                else if (!busEnabled)
                {
                    busOptionLabel = busOptionLabel + " (" + busDisabledReason + ")";
                }

                string cowOptionLabel = Strings.ITab_SelectSpecializationCow;
                bool cowFinalized = BusSpecializationUtility.HasFinalCowState(pawn);
                bool cowEnabled = !cowFinalized;
                string cowDisabledReason = null;
                if (cowEnabled)
                {
                    cowEnabled = BusSpecializationUtility.CanUseCowSpecialization(pawn, out cowDisabledReason);
                }

                if (cowFinalized)
                {
                    cowOptionLabel = cowOptionLabel + " " + Strings.ITab_SpecializationFinalizedSuffix;
                }
                else if (!cowEnabled)
                {
                    cowOptionLabel = cowOptionLabel + " (" + cowDisabledReason + ")";
                }

                List<FloatMenuOption> options = new List<FloatMenuOption>
                {
                    new FloatMenuOption(Strings.ITab_SelectSpecializationNone, delegate
                    {
                        comp.SetSpecialization(SexSlaveSpecializationType.None);
                    }),
                    new FloatMenuOption(busOptionLabel, busEnabled ? (Action)delegate
                    {
                        comp.SetSpecialization(SexSlaveSpecializationType.Bus);
                        BusSpecializationUtility.EnsureBusHediffFromSpecialization(pawn);
                    } : null),
                    new FloatMenuOption(cowOptionLabel, cowEnabled ? (Action)delegate
                    {
                        comp.SetSpecialization(SexSlaveSpecializationType.Cow);
                        BusSpecializationUtility.EnsureCowHediffFromSpecialization(pawn);
                    } : null),
                    BuildPetSpecializationOption(pawn, comp, SexSlaveSpecializationType.PetCat),
                    BuildPetSpecializationOption(pawn, comp, SexSlaveSpecializationType.PetDog),
                    BuildPetSpecializationOption(pawn, comp, SexSlaveSpecializationType.PetRabbit)
                };
                Find.WindowStack.Add(new FloatMenu(options));
            }

            listing.Gap(6f);
            // 当前方向只读取自己的终极状态；未选择方向时，沿用上方标签的已终极化摘要。
            bool displayedTypeFinalized = IsTypeFinalized(pawn, comp.specializationType)
                || (comp.specializationType == SexSlaveSpecializationType.None && HasAnyFinalizedState(pawn));
            string progressText = displayedTypeFinalized
                ? Strings.ITab_SpecializationComplete
                : comp.specializationProgress.ToStringPercent();
            listing.Label(Strings.ITab_SpecializationProgress(progressText));
            DrawRabbitReproductionModeSelector(listing, pawn, comp);
        }

        private static bool HasAnyFinalizedState(Pawn pawn)
        {
            return BusSpecializationUtility.HasFinalBusState(pawn)
                || BusSpecializationUtility.HasFinalCowState(pawn)
                || PetSpecializationUtility.HasFinalPetState(pawn, SexSlaveSpecializationType.PetCat)
                || PetSpecializationUtility.HasFinalPetState(pawn, SexSlaveSpecializationType.PetDog)
                || PetSpecializationUtility.HasFinalPetState(pawn, SexSlaveSpecializationType.PetRabbit);
        }

        private static bool IsTypeFinalized(Pawn pawn, SexSlaveSpecializationType type)
        {
            switch (type)
            {
                case SexSlaveSpecializationType.Bus:
                    return BusSpecializationUtility.HasFinalBusState(pawn);
                case SexSlaveSpecializationType.Cow:
                    return BusSpecializationUtility.HasFinalCowState(pawn);
                case SexSlaveSpecializationType.PetCat:
                case SexSlaveSpecializationType.PetDog:
                case SexSlaveSpecializationType.PetRabbit:
                    return PetSpecializationUtility.HasFinalPetState(pawn, type);
                default:
                    return false;
            }
        }

        private static string GetSpecializationLabel(Pawn pawn, CompSexSlaveTraining comp)
        {
            string label;
            switch (comp.specializationType)
            {
                case SexSlaveSpecializationType.Bus:
                    label = Strings.ITab_SpecializationBus;
                    break;
                case SexSlaveSpecializationType.Cow:
                    label = Strings.ITab_SpecializationCow;
                    break;
                case SexSlaveSpecializationType.PetCat:
                case SexSlaveSpecializationType.PetDog:
                case SexSlaveSpecializationType.PetRabbit:
                    label = PetSpecializationUtility.GetSpecializationLabel(comp.specializationType);
                    break;
                default:
                    label = Strings.ITab_SpecializationNone;
                    break;
            }

            if (comp.specializationType == SexSlaveSpecializationType.None && HasAnyFinalizedState(pawn))
            {
                if (BusSpecializationUtility.HasFinalBusState(pawn))
                {
                    return Strings.ITab_SpecializationBus + " " + Strings.ITab_SpecializationFinalizedSuffix;
                }

                if (BusSpecializationUtility.HasFinalCowState(pawn))
                {
                    return Strings.ITab_SpecializationCow + " " + Strings.ITab_SpecializationFinalizedSuffix;
                }

                foreach (SexSlaveSpecializationType petType in new[]
                         {
                             SexSlaveSpecializationType.PetCat,
                             SexSlaveSpecializationType.PetDog,
                             SexSlaveSpecializationType.PetRabbit
                         })
                {
                    if (PetSpecializationUtility.HasFinalPetState(pawn, petType))
                    {
                        string finalizedLabel = PetSpecializationUtility.GetSpecializationLabel(petType) + " " + Strings.ITab_SpecializationFinalizedSuffix;
                        if (petType == SexSlaveSpecializationType.PetCat || petType == SexSlaveSpecializationType.PetRabbit)
                            finalizedLabel += " " + Strings.ITab_SpecializationUnfinishedSuffix;
                        return finalizedLabel;
                    }
                }
            }

            bool finalized = IsTypeFinalized(pawn, comp.specializationType);
            if (finalized)
            {
                label += " " + Strings.ITab_SpecializationFinalizedSuffix;
            }
            if (comp.specializationType == SexSlaveSpecializationType.PetCat ||
                comp.specializationType == SexSlaveSpecializationType.PetRabbit)
            {
                label += " " + Strings.ITab_SpecializationUnfinishedSuffix;
            }

            return label;
        }

        private static FloatMenuOption BuildPetSpecializationOption(Pawn pawn, CompSexSlaveTraining comp, SexSlaveSpecializationType type)
        {
            string optionLabel = PetSpecializationUtility.GetSelectLabel(type);
            // 猫、兔特化尚未完成，玩家入口保持可见但不可选择。
            if (type == SexSlaveSpecializationType.PetCat || type == SexSlaveSpecializationType.PetRabbit)
            {
                return new FloatMenuOption(optionLabel + " " + Strings.ITab_SpecializationUnfinishedSuffix, null);
            }

            bool finalized = PetSpecializationUtility.HasFinalPetState(pawn, type);
            bool enabled = !finalized;
            string disabledReason = null;
            if (enabled)
            {
                enabled = PetSpecializationUtility.CanUsePetSpecialization(pawn, type, out disabledReason);
            }

            if (finalized)
            {
                optionLabel = optionLabel + " " + Strings.ITab_SpecializationFinalizedSuffix;
            }
            else if (!enabled)
            {
                optionLabel = optionLabel + " (" + disabledReason + ")";
            }

            return new FloatMenuOption(optionLabel, enabled ? (Action)delegate
            {
                comp.SetSpecialization(type);
                PetSpecializationUtility.EnsurePetHediffFromSpecialization(pawn);
            } : null);
        }

        private static void DrawRabbitReproductionModeSelector(Listing_Standard listing, Pawn pawn, CompSexSlaveTraining comp)
        {
            if (comp == null || pawn == null) return;
            if (!comp.IsPetRabbitSpecialized && !PetSpecializationUtility.HasAnyPetState(pawn, SexSlaveSpecializationType.PetRabbit)) return;

            string modeLabel = GetRabbitReproductionModeLabel(comp.rabbitReproductionMode);
            // 旧档仍展示已保存的模式，未完成期间不开放切换入口。
            listing.Label(Strings.ITab_RabbitReproductionMode(modeLabel) + " " + Strings.ITab_SpecializationUnfinishedSuffix);
        }

        private static string GetRabbitReproductionModeLabel(RabbitReproductionMode mode)
        {
            switch (mode)
            {
                case RabbitReproductionMode.Clone:
                    return Strings.ITab_RabbitReproductionClone;
                default:
                    return Strings.ITab_RabbitReproductionOffspring;
            }
        }

        private static void DrawAllowOthersToggle(Listing_Standard listing, Pawn pawn, CompSexSlaveTraining comp)
        {
            bool allowOthers = comp.allowOthersForTrainingOrSex;
            bool oldState = allowOthers;
            listing.CheckboxLabeled(Strings.ITab_AllowOthers, ref allowOthers);
            if (allowOthers == oldState) return;

            comp.allowOthersForTrainingOrSex = allowOthers;
            if (!allowOthers && comp.IsBusSpecialized)
            {
                Messages.Message(Strings.ITab_AllowOthersWarning, pawn, MessageTypeDefOf.CautionInput, false);
            }
        }

        private static void DrawMilkToggle(Listing_Standard listing, Pawn pawn, CompSexSlaveTraining comp)
        {
            if (!pawn.health.hediffSet.HasHediff(SSCDefOf.SSC_Lactating_SubState)) return;

            bool enabled = comp.milkProductionEnabled;
            listing.CheckboxLabeled(Strings.ITab_MilkProductionToggle, ref enabled);
            comp.milkProductionEnabled = enabled;
        }
    }
}
