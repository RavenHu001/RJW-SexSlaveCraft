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
    public partial class ITab_SexSlaveTraining : ITab
    {
        private const float SectionSpacing = 12f;
        // 节框留白与标题高度由绘制和动态测量共用；列表间距也显式固定，
        // 避免身份区计算的高度与 Listing_Standard 实际推进的位置不一致。
        private const float SectionPadding = 12f;
        private const float SectionTitleHeight = 28f;
        private const float IdentityButtonHeight = 30f;
        private const float IdentityListingSpacing = 2f;
        // 展开入口属于左侧滚动内容；高度和间隔同时用于绘制及总高度计算。
        private const float RestrictionToggleHeight = 30f;
        private const float RestrictionToggleSpacing = 8f;
        // 训导官有独立于当前培养方向的终极记录；多一行状态时同步增加绘制区与滚动高度。
        private const float SpecializationSectionHeight = 124f;
        private const float TrainerStatusExtraHeight = 44f;
        private const float RabbitModeExtraHeight = 28f;
        private static readonly Vector2 WinSize = new Vector2(360f, 560f);

        private Vector2 scrollPosition;
        private Vector2 restrictionScrollPosition;
        private float restrictionContentHeight = 1400f;
        private Pawn restrictionPawn;
        private static bool restrictionsExpanded;
        private static Game restrictionSession;

        /// <summary>初始化调教页尺寸、标题和教程标识。</summary>
        public ITab_SexSlaveTraining()
        {
            size = WinSize;
            labelKey = "Tab_SexSlaveTraining";
            tutorTag = "SexSlaveTraining";
        }

        /// <summary>按屏幕约束向右扩展规则栏，保持主调教栏的位置和宽度，整个面板处于原生窗口输入范围内。</summary>
        protected override void UpdateSize()
        {
            if (restrictionSession != Current.Game)
            {
                restrictionSession = Current.Game;
                restrictionsExpanded = false;
            }
            // 展开偏好保留在会话内；切到未绑定角色或停用系统时，不显示右栏也不占据右栏宽度。
            size = new Vector2(Mathf.Min(restrictionsExpanded && CanShowRestrictions(SelPawn)
#if SSC_RW17
                && SSCBehaviorControlUI.CanFitSidebar(UI.screenWidth)
#endif
                ? 740f : WinSize.x, UI.screenWidth - 16f),
                Mathf.Min(WinSize.y, Mathf.Max(100f, PaneTopY - 40f)));
        }

        /// <summary>普通限制生效或具备自我调教资格时可展开；总限制关闭时保持现有面板停用语义。</summary>
        private static bool CanShowRestrictions(Pawn pawn)
        {
#if SSC_RW17
            // 两个模块的展示资格独立于旧总开关；第一阶段/解绑保留值也需要显示原因。
            return SSCBehaviorControlUI.CanInspect(pawn);
#else
            return (SSCMod.settings?.enableSexSlaveProtectionRules ?? true) &&
                (SSCRestrictionResolver.IsApplicable(pawn) || SSCSelfTrainingEligibility.IsEligible(pawn));
#endif
        }

        /// <summary>保持原按钮位置，对不受限制的角色明确置灰并拦截点击，悬停说明不可用原因。</summary>
        private static void DrawRestrictionToggle(Rect rect, Pawn pawn)
        {
            bool available = CanShowRestrictions(pawn);
            bool oldEnabled = GUI.enabled;
            Color oldColor = GUI.color;
            try
            {
                GUI.enabled = oldEnabled && available;
                if (!GUI.enabled) GUI.color = oldColor * new Color(0.55f, 0.55f, 0.55f, 1f);
                string key = available && restrictionsExpanded ? "SSC_Restrictions_Collapse" : "SSC_Restrictions_Expand";
#if SSC_RW17
                if (!SSCBehaviorControlUI.CanFitSidebar(UI.screenWidth)) key = "SSC_Romance_OpenBehaviorWindow";
#endif
                if (Widgets.ButtonText(rect, key.Translate()) && GUI.enabled)
#if SSC_RW17
                {
                    if (SSCBehaviorControlUI.CanFitSidebar(UI.screenWidth)) restrictionsExpanded = !restrictionsExpanded;
                    else Find.WindowStack.Add(new Dialog_SSCRestrictions(pawn));
                }
#else
                    restrictionsExpanded = !restrictionsExpanded;
#endif
            }
            finally { GUI.enabled = oldEnabled; GUI.color = oldColor; }
            if (!available)
                TooltipHandler.TipRegion(rect, (!(SSCMod.settings?.enableSexSlaveProtectionRules ?? true)
                    ? "SSC_Restrictions_PanelDisabled" : "SSC_Restrictions_PanelUnavailable").Translate());
        }

        /// <summary>绘制独立滚动的右侧角色配置；切换角色只重置滚动，不写入默认或修改保存值。</summary>
        private void DrawRestrictionPanel(Rect rect, Pawn pawn)
        {
            if (restrictionPawn != pawn)
            {
                restrictionPawn = pawn;
                restrictionScrollPosition = Vector2.zero;
            }
            Widgets.DrawMenuSection(rect);
            Rect viewport = rect.ContractedBy(8f);
            Rect content = new Rect(0f, 0f, Mathf.Max(1f, viewport.width - 20f),
                Mathf.Max(viewport.height, restrictionContentHeight));
            Widgets.BeginScrollView(viewport, ref restrictionScrollPosition, content);
            try
            {
                var listing = new Listing_Standard { maxOneColumn = true };
                listing.Begin(content);
                try {
#if SSC_RW17
                    SSCBehaviorControlUI.DrawPawn(listing, pawn);
#else
                    SSCRestrictionUI.DrawPawn(listing, pawn);
#endif
                }
                finally { restrictionContentHeight = listing.CurHeight + 12f; listing.End(); }
            }
            finally { Widgets.EndScrollView(); }
        }

        /// <summary>仅为支持的原版身份且具有训练组件的角色显示调教页，不要求完成研究。</summary>
        public override bool IsVisible
        {
            get
            {
                Pawn p = SelPawn;
                if (p == null) return false;
                if (!SSCIdentityUtility.IsSupportedVanillaStatus(p) && !SSCSelfTrainingEligibility.IsEligible(p)) return false;
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
            // 原版先绘制右上角关闭键（顶部 4～22 像素），再调用 FillTab。
            // 两侧内容均从 24 像素以下开始，滚动到任何位置也不会覆盖关闭键。
            outerRect.yMin = Mathf.Max(outerRect.yMin, 24f);
            Rect trainingRect = new Rect(outerRect.x, outerRect.y,
                Mathf.Min(WinSize.x - 20f, outerRect.width), outerRect.height);
            if (restrictionsExpanded && CanShowRestrictions(pawn)
#if SSC_RW17
                && SSCBehaviorControlUI.CanFitSidebar(UI.screenWidth)
#endif
                )
                DrawRestrictionPanel(new Rect(trainingRect.xMax + 10f, outerRect.y,
                    outerRect.xMax - trainingRect.xMax - 10f, outerRect.height), pawn);
            // 身份资格、翻译文字和测量结果只为本次绘制生成一次。
            // 滚动范围及节框使用同一份结果，长译文换行时后续内容随之下移。
            float contentWidth = trainingRect.width - 18f;
            TrainerIdentityView identityView = BuildTrainerIdentityView(pawn, comp, contentWidth);
            GelAppearanceView gelView = BuildGelAppearanceView(pawn, contentWidth);
#if SSC_RW17
            // 身体管理不受后续身份和研究分支影响；测量与绘制复用同一份只读视图。
            BodyManagementView bodyView = BuildBodyManagementView(pawn, comp, contentWidth);
#endif
            float specializationHeight = GetSpecializationSectionHeight(pawn, comp, contentWidth);
            // 主人信息同样只测量本次事件一次；绘制和滚动高度共用快照，界面不补建成长或缰绳。
            MasterPanelView masterView = comp.pawnIdentity == PawnIdentity.Master
                ? BuildMasterPanelView(pawn, comp, contentWidth) : default(MasterPanelView);
            // 预留通用 Slave 分组；注册表为空时不画空框、不增加原面板高度。
            var slaveAbilityRows = comp.pawnIdentity == PawnIdentity.Slave
                ? BuildAbilityToggleRows(pawn, comp, SSCAbilityToggleGroup.Slave, contentWidth - SectionPadding * 2f) : null;
            float slaveAbilityHeight = AbilityToggleSectionHeight(slaveAbilityRows);
            float contentHeight = CalculateContentHeight(pawn, comp, identityView.SectionHeight, specializationHeight,
                contentWidth, masterView.SectionHeight) + (slaveAbilityHeight > 0f ? slaveAbilityHeight + SectionSpacing : 0f)
                + (gelView.Height > 0f ? gelView.Height + SectionSpacing : 0f);
#if SSC_RW17
            if (bodyView.Height > 0f) contentHeight += bodyView.Height + SectionSpacing;
#endif
            Rect viewRect = trainingRect;
            Rect contentRect = new Rect(0f, 0f, contentWidth, contentHeight);

            Widgets.BeginScrollView(viewRect, ref scrollPosition, contentRect);
            try
            {
                float curY = 0f;
                curY = DrawIdentitySection(new Rect(0f, curY, contentRect.width,
                    identityView.SectionHeight), comp, identityView) + SectionSpacing;

#if SSC_RW17
                // 紧跟身份区，凝胶／身体条目增加时入口不会被继续向下推；总滚动高度不变。
                DrawRestrictionToggle(new Rect(0f, curY, contentRect.width, RestrictionToggleHeight), pawn);
                curY += RestrictionToggleHeight + RestrictionToggleSpacing;
#endif

                // 在所有身份/研究分支之前绘制；个人开关关闭也能继续编辑外观。
                if (gelView.Height > 0f)
                {
                    DrawGelAppearanceSection(new Rect(0f, curY, contentRect.width, gelView.Height), gelView);
                    curY += gelView.Height + SectionSpacing;
                }

#if SSC_RW17
                // 共用身体栏只绘制一次，主人、未设身份及研究未完成时也能操作已有身体状态。
                if (bodyView.Height > 0f)
                {
                    DrawBodyManagementSection(new Rect(0f, curY, contentRect.width, bodyView.Height), bodyView);
                    curY += bodyView.Height + SectionSpacing;
                }
#endif

#if !SSC_RW17
                // 冻结版本保留旧布局；当前入口前移只对1.7生效。
                DrawRestrictionToggle(new Rect(0f, curY, contentRect.width, RestrictionToggleHeight), pawn);
                curY += RestrictionToggleHeight + RestrictionToggleSpacing;
#endif
                if (CanShowSpecializationSection(comp))
                {
                    curY = DrawSpecializationSection(new Rect(0f, curY, contentRect.width,
                        specializationHeight), pawn, comp) + SectionSpacing;
                }

                if (slaveAbilityHeight > 0f)
                {
                    DrawAbilityToggleSection(new Rect(0f, curY, contentRect.width, slaveAbilityHeight), slaveAbilityRows);
                    curY += slaveAbilityHeight + SectionSpacing;
                }

                if (comp.pawnIdentity == PawnIdentity.Master)
                {
                    DrawMasterPanel(new Rect(0f, curY, contentRect.width, masterView.SectionHeight), masterView);
                }
                else if (comp.pawnIdentity == PawnIdentity.Unset)
                {
                    if (SSCIdentityUtility.CanRestoreLegacySlaveIdentity(pawn))
                        DrawLegacyIdentityRecoverySection(new Rect(0f, curY, contentRect.width,
                            GetLegacyIdentityRecoveryHeight(contentRect.width)), pawn, comp);
                    else
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

#if !SSC_RW17
                    // 旧版本保留原泌乳栏；1.7 已在所有身份共用的身体栏绘制。
                    if (pawn.health.hediffSet.HasHediff(SSCDefOf.SSC_Lactating_SubState))
                    {
                        curY = DrawMilkSection(new Rect(0f, curY, contentRect.width, 88f), pawn, comp) + SectionSpacing;
                    }
#endif

                    curY = DrawPoseSection(new Rect(0f, curY, contentRect.width, 82f), comp) + SectionSpacing;
                    DrawTrainerSection(new Rect(0f, curY, contentRect.width, 82f), pawn, comp);
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        /// <summary>为身份、限制展开入口及当前可见各节累计滚动高度，保持按钮和后续内容均可滚动访问。</summary>
        private static float CalculateContentHeight(Pawn pawn, CompSexSlaveTraining comp, float identityHeight,
            float specializationHeight, float width, float masterHeight)
        {
            float height = identityHeight + RestrictionToggleHeight + RestrictionToggleSpacing
                + SectionSpacing + specializationHeight;

            // 主人分支使用实际测量，未选择身份仍保留原来的恢复入口及高度。
            if (comp.pawnIdentity == PawnIdentity.Master) return height + masterHeight + 6f;
            if (comp.pawnIdentity == PawnIdentity.Unset)
            {
                if (SSCIdentityUtility.CanRestoreLegacySlaveIdentity(pawn))
                    return height + GetLegacyIdentityRecoveryHeight(width) + 6f;
                return height + 84f;
            }

            if (!ResearchUtils.IsResearchFinished(SSCDefOf.SSC_BasicTraining))
            {
                return height + SectionSpacing + 112f;
            }

            height += SectionSpacing + 124f;
            height += SectionSpacing + 210f;
#if !SSC_RW17
            // 与旧版本的绘制分支保持一致；1.7 的身体栏高度由同一份视图另行累计。
            if (pawn.health.hediffSet.HasHediff(SSCDefOf.SSC_Lactating_SubState))
            {
                height += SectionSpacing + 88f;
            }
#endif

            height += SectionSpacing + 82f;
            height += SectionSpacing + 82f;
            return height + 4f;
        }

        /// <summary>读取一次资格，并按实际字体、标签和可用宽度计算身份区高度。</summary>
        private static TrainerIdentityView BuildTrainerIdentityView(Pawn pawn, CompSexSlaveTraining comp, float width)
        {
            // 勾选状态来自保存选择。资格只决定能否任职与重新开启；失格后
            // 仍显示已保存的勾选，让玩家可以关闭该选择而不误报为个人关闭。
            bool isSlave = comp.pawnIdentity == PawnIdentity.Slave;
            bool enabled = comp.pawnIdentity == PawnIdentity.Master || isSlave && comp.slaveTrainerEnabled;
            TrainerSpecializationFailure failure = TrainerSpecializationFailure.None;
            bool qualified = isSlave && TrainerSpecializationUtility.HasTrainerQualification(pawn, out failure);
            bool active = comp.pawnIdentity == PawnIdentity.Master || enabled && qualified;
            string label = "SSC_TrainerIdentity_Label".Translate();
            if (isSlave)
                label += active ? "SSC_TrainerIdentity_Active".Translate() :
                    enabled ? "SSC_TrainerIdentity_SavedInactive".Translate() :
                    "SSC_TrainerIdentity_Off".Translate();

            // 显示状态复用上面的资格结果，不再通过 IsTrainer 重读锁链。
            // 点击提交仍走 SetTrainerEnabled 的权威检查，不能用界面快照授予资格。
            string tooltip = (comp.pawnIdentity == PawnIdentity.Master ? "SSC_TrainerIdentity_MasterTip"
                : isSlave ? "SSC_TrainerIdentity_SlaveTip" : "SSC_TrainerIdentity_UnsetTip").Translate();
            if (isSlave && !qualified)
                tooltip += "\n\n" + "SSC_TrainerIdentity_QualificationTip".Translate() +
                    "\n" + GetTrainerFailureReason(failure);

            // 永久资格来自人格保存字段；当前 Master 身份、绑定及手动选择设置都不能替代它。
            // 只在本次绘制共享文案和测量，不迁移或写回角色数据；缺设置时按默认关闭展示。
            string masterQualificationLabel = SSCMasterQualificationDisplay.GetLabel(comp.masterQualificationUnlocked);
            string masterQualificationTooltip = SSCMasterQualificationDisplay.GetPawnTooltip(
                SSCMod.settings?.allowDirectMasterSelection ?? false);

            // 绘制节框会使用 Small 字体；测量时使用相同字体并恢复原设置。
            // 复选框右侧占 24 像素，标签须扣掉这段宽度再测量，以免文本被勾选框遮挡。
            GameFont oldFont = Text.Font;
            try
            {
                Text.Font = GameFont.Small;
                float innerWidth = Mathf.Max(1f, width - SectionPadding * 2f);
                float toggleHeight = Mathf.Max(24f, Text.CalcHeight(label, Mathf.Max(1f, innerWidth - 24f)));
                float masterQualificationHeight = SSCMasterQualificationDisplay.GetHeight(
                    masterQualificationLabel, innerWidth, Text.LineHeight);
                float contentHeight = IdentityButtonHeight + 2f + 6f + toggleHeight + IdentityListingSpacing;
                contentHeight += masterQualificationHeight + 6f + IdentityListingSpacing;
                return new TrainerIdentityView
                {
                    IsSlave = isSlave, Enabled = enabled, Qualified = qualified,
                    Label = label, Tooltip = tooltip,
                    ToggleHeight = toggleHeight,
                    MasterQualificationLabel = masterQualificationLabel,
                    MasterQualificationTooltip = masterQualificationTooltip,
                    MasterQualificationHeight = masterQualificationHeight,
                    // 训练状态留在训练区；身份区按实际内容收缩，不留下已删除状态行的空白。
                    SectionHeight = SectionPadding * 2f + SectionTitleHeight + contentHeight
                };
            }
            finally { Text.Font = oldFont; }
        }

        /// <summary>按终极成果列表、普通训导官状态和兔模式计算区块高度，供绘制和滚动范围共用。</summary>
        private static float GetSpecializationSectionHeight(Pawn pawn, CompSexSlaveTraining comp, float width)
        {
            if (!CanShowSpecializationSection(comp)) return 0f;
#if SSC_RW17
            return GetCompactSpecializationSectionHeight(pawn, comp, width);
#else
            // 状态行在窄栏和长译文下可能换成两行；兔特化的繁殖模式仍可能
            // 同时出现，因此两项额外高度分别计算，避免跨方向终极记录遮挡后续控件。
            bool showTrainerStatus = comp.specializationType == SexSlaveSpecializationType.TrainerOfficer ||
                TrainerSpecializationUtility.HasFinalRecord(pawn);
            bool showRabbitMode = comp.IsPetRabbitSpecialized ||
                PetSpecializationUtility.HasAnyPetState(pawn, SexSlaveSpecializationType.PetRabbit);
            float progressExtraHeight = 0f;
            if (comp.specializationType == SexSlaveSpecializationType.Combatant)
            {
                GameFont previousFont = Text.Font;
                try
                {
                    Text.Font = GameFont.Small;
                    string progress = Strings.ITab_SpecializationProgress(GetSpecializationProgressText(pawn, comp));
                    progressExtraHeight = Mathf.Max(0f,
                        Text.CalcHeight(progress, Mathf.Max(1f, width - SectionPadding * 2f)) - Text.LineHeight);
                }
                finally { Text.Font = previousFont; }
            }
            return SpecializationSectionHeight + progressExtraHeight +
                (showTrainerStatus ? TrainerStatusExtraHeight : 0f) +
                (showRabbitMode ? RabbitModeExtraHeight : 0f);
#endif
        }

        /// <summary>绘制 SSC 身份及调教员开关；已有绑定时禁用身份按钮并提示原因。</summary>
        private float DrawIdentitySection(Rect rect, CompSexSlaveTraining comp, TrainerIdentityView view)
        {
            DrawSection(rect, Strings.ITab_IdentityHeader, delegate(Rect innerRect)
            {
                Listing_Standard listing = BeginSectionListing(innerRect);
                // 本栏只能纵向滚动。即使某个 UI 补丁改变实际字体高度，
                // 也不允许原版 Listing 将后续接收状态移到不可见的第二列。
                listing.maxOneColumn = true;
                listing.verticalSpacing = IdentityListingSpacing;
                try
                {

                    string idLabel = GetIdentityLabel(comp.pawnIdentity);
                    bool identityLocked = SSCIdentityUtility.IsIdentityLocked(SelPawn);
                    Rect identityRect = listing.GetRect(IdentityButtonHeight);
                    bool identityClicked = SSCUIWidgets.Button(identityRect, idLabel, !identityLocked);
                    // 同为不可切换，仍区分永久印记锁和普通绑定锁，避免解绑后的定型人格收到错误提示。
                    if (identityLocked)
                        TooltipHandler.TipRegion(identityRect,
#if SSC_RW17
                            SSCImprintUtility.GetImprintLockedIdentity(SelPawn) != PawnIdentity.Unset ?
                                "SSC_Imprint_GelTooltip".Translate() :
#endif
                                "SSC_Identity_BoundTip".Translate());
                    listing.Gap(2f);
                    if (identityClicked)
                    {
                        Pawn identityPawn = SelPawn;
                        List<FloatMenuOption> identityOptions = new List<FloatMenuOption>
                        {
                            new FloatMenuOption(Strings.ITab_SetIdentityUnset, delegate
                            {
                                if (identityPawn?.TryGetComp<CompSexSlaveTraining>() != comp) return;
                                SSCIdentityUtility.TrySetIdentity(identityPawn, PawnIdentity.Unset);
                            }),
                            new FloatMenuOption(Strings.ITab_SetAsSlave, delegate
                            {
                                if (identityPawn?.TryGetComp<CompSexSlaveTraining>() != comp) return;
                                SSCIdentityUtility.TrySetIdentity(identityPawn, PawnIdentity.Slave);
                            })
                        };
                        bool canSelectMaster = comp.pawnIdentity == PawnIdentity.Master ||
                            SSCMasterQualificationUtility.CanSelectMaster(identityPawn);
                        string masterOptionLabel = Strings.ITab_SetAsMaster;
                        if (!canSelectMaster)
                            masterOptionLabel += " (" + "SSC_Identity_MasterQualificationRequired".Translate() + ")";
                        identityOptions.Add(new FloatMenuOption(masterOptionLabel,
                            canSelectMaster ? (Action)delegate
                            {
                                if (identityPawn?.TryGetComp<CompSexSlaveTraining>() != comp) return;
                                if (!SSCIdentityUtility.TrySetIdentity(identityPawn, PawnIdentity.Master))
                                    Messages.Message("SSC_Identity_SelectionUnavailable".Translate(), identityPawn,
                                        MessageTypeDefOf.RejectInput, false);
                            } : null));
                        Find.WindowStack.Add(new FloatMenu(identityOptions));
                    }

                    listing.Gap(6f);
                    // 使用上方同一次测量的行高，四语长状态不会覆盖调教员开关。
                    SSCMasterQualificationDisplay.Draw(listing.GetRect(view.MasterQualificationHeight),
                        view.MasterQualificationLabel, view.MasterQualificationTooltip);
                    listing.Gap(6f);
                    DrawTrainerIdentityToggle(listing, SelPawn, view);


                }
                finally
                {
                    listing.End();
                }
            });

            return rect.yMax;
        }

        /// <summary>将只读资格失败码转换为菜单和任职开关共用的具体说明。</summary>
        private static string GetTrainerFailureReason(TrainerSpecializationFailure failure)
        {
            // 失格原因只影响显示；菜单和工作入口仍调用资格工具重新判断，
            // 避免翻译文本或界面刷新时机成为另一套权限规则。
            return ("SSC_TrainerIdentity_Failure_" + failure).Translate();
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
#if SSC_RW17
                bool previousWrap = Text.WordWrap;
                TextAnchor previousAnchor = Text.Anchor;
#endif
                try
                {
                    // 状态行已按实际宽度测高，仍强制单列，避免界面补丁使其横向溢出。
                    listing.maxOneColumn = true;
#if SSC_RW17
                    listing.verticalSpacing = SpecializationListingSpacing;
                    Text.WordWrap = true;
                    Text.Anchor = TextAnchor.UpperLeft;
#endif
                    DrawSpecializationSelector(listing, pawn, comp);
                }
                finally
                {
#if SSC_RW17
                    Text.WordWrap = previousWrap;
                    Text.Anchor = previousAnchor;
#endif
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
                    if (enabled != comp.scheduledTrainingEnabled && CanEditTrainingConfiguration(pawn, comp))
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
                                    if (!CanEditTrainingConfiguration(pawn, comp)) return;
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
                                    delegate
                                    {
                                        if (!CanEditTrainingConfiguration(pawn, comp)) return;
                                        comp.scheduledTrainingIntervalDays = selectedDays;
                                    }));
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

#if !SSC_RW17
        /// <summary>保留旧版本的泌乳配置与进度；1.7 改由共用身体栏绘制。</summary>
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
#endif

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

        private static float GetLegacyIdentityRecoveryHeight(float width)
        {
            return SectionPadding * 2f + Text.CalcHeight("SSC_Identity_LegacyRecoveryHint".Translate(),
                Mathf.Max(1f, width - SectionPadding * 2f)) + 8f + IdentityButtonHeight;
        }

        /// <summary>历史锁链只提供手动恢复入口；确认后再次核对原组件、锁链及主人，不自动恢复。</summary>
        private static void DrawLegacyIdentityRecoverySection(Rect rect, Pawn pawn, CompSexSlaveTraining comp)
        {
            DrawSection(rect, string.Empty, delegate(Rect innerRect)
            {
                string hint = "SSC_Identity_LegacyRecoveryHint".Translate();
                float hintHeight = Text.CalcHeight(hint, innerRect.width);
                Widgets.Label(new Rect(innerRect.x, innerRect.y, innerRect.width, hintHeight), hint);
                Rect button = new Rect(innerRect.x, innerRect.y + hintHeight + 8f,
                    innerRect.width, IdentityButtonHeight);
                if (!Widgets.ButtonText(button, "SSC_Identity_LegacyRecoveryButton".Translate())) return;
                Hediff_ChainOfSexSlave chain = SSCBondUtility.GetChain(pawn);
                Pawn owner = chain?.LinkedPawn;
                if (!SSCIdentityUtility.CanRestoreLegacySlaveIdentity(pawn)) return;
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "SSC_Identity_LegacyRecoveryConfirm".Translate(pawn.LabelShort, owner.LabelShort), () =>
                    {
                        if (pawn.TryGetComp<CompSexSlaveTraining>() != comp || SSCBondUtility.GetChain(pawn) != chain ||
                            SSCBondUtility.GetBoundMaster(pawn) != owner || !SSCIdentityUtility.TryRestoreLegacySlaveIdentity(pawn))
                        {
                            Messages.Message("SSC_Identity_LegacyRecoveryRejected".Translate(), pawn,
                                MessageTypeDefOf.RejectInput, false);
                            return;
                        }
                        Messages.Message("SSC_Identity_LegacyRecoveryCompleted".Translate(pawn.LabelShort), pawn,
                            MessageTypeDefOf.PositiveEvent, false);
                    }));
            });
        }

        /// <summary>在统一节框中绘制带颜色的身份提示，并恢复文字对齐与颜色。</summary>
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

        /// <summary>在节内容区域开始一个标准列表，调用方负责在结束时关闭。</summary>
        private static Listing_Standard BeginSectionListing(Rect innerRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(innerRect);
            return listing;
        }

        /// <summary>绘制节背景及可选标题，再将收缩后的内容区域交给指定绘制函数。</summary>
        private static void DrawSection(Rect rect, string title, Action<Rect> drawContents)
        {
            Widgets.DrawMenuSection(rect);

            Rect innerRect = rect.ContractedBy(SectionPadding);
            if (!string.IsNullOrEmpty(title))
            {
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(innerRect.x, innerRect.y, innerRect.width, SectionTitleHeight), title);
                innerRect.yMin += SectionTitleHeight;
            }

            drawContents(innerRect);
        }

        /// <summary>列出合格且可完成调教授权的对象；普通互动不随指派开放，强制来源禁止时不能换人。</summary>
        private List<FloatMenuOption> GetTrainerOptions(Pawn slave)
#if SSC_RW17
            => SSCTrainerAssignmentMenu.Options(slave, current: () => SelPawn == slave);
#else
            => SSCTrainerAssignmentMenu.Options(slave);
#endif

        /// <summary>切换日常训练状态并清除当前训练占用；启用时显示现有资格校验结果。</summary>
        private static void DrawTrainingToggle(Listing_Standard listing, Pawn pawn, CompSexSlaveTraining comp)
        {
            bool isEnabled = comp.IsEnabled;
            bool oldState = isEnabled;

            listing.CheckboxLabeled(Strings.ITab_AllowTraining, ref isEnabled);
            if (oldState == isEnabled) return;
            if (!CanEditTrainingConfiguration(pawn, comp)) return;

            comp.mode = isEnabled ? TrainingMode.Enabled : TrainingMode.Disabled;
            comp.isBeingTrained = false;

            if (!isEnabled) return;
            if (Trainjudge.TryCanBeFuckedWithReason(pawn, out string shortReason))
            {
                Messages.Message("SSC_Message_TrainingEnabledEligible".Translate(), pawn, MessageTypeDefOf.NeutralEvent, false);
                return;
            }

            Messages.Message(shortReason, pawn, MessageTypeDefOf.RejectInput, false);
        }

        /// <summary>把当前 SSC 身份转换为调教页使用的本地化标签。</summary>
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

        /// <summary>显示剩余训练冷却时间，或在无冷却时显示当前启用状态。</summary>
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

        /// <summary>列出支持的训练姿势，并保存玩家选择的模式。</summary>
        private static void DrawPoseSelector(Listing_Standard listing, CompSexSlaveTraining comp)
        {
            Pawn pawn = comp.parent as Pawn;
            string modeLabel = Strings.GetModeLabel(comp.selectedMode);
            if (!listing.ButtonText(modeLabel)) return;

            List<FloatMenuOption> acts = new List<FloatMenuOption>();
            foreach (TrainingActType type in Enum.GetValues(typeof(TrainingActType)))
            {
                acts.Add(new FloatMenuOption(Strings.GetModeLabel(type), delegate
                {
                    if (!CanEditTrainingConfiguration(pawn, comp)) return;
                    comp.selectedMode = type;
                }));
            }

            Find.WindowStack.Add(new FloatMenu(acts));
        }

        /// <summary>显示原始指派并允许打开候选；选择非主人时同步授权调教，个人禁止不再把整个菜单禁用。</summary>
        private void DrawTrainerSelector(Listing_Standard listing, Pawn pawn, CompSexSlaveTraining comp)
        {
            string trainerName = TrainerAssignmentUtility.GetAssignedTrainerLabel(pawn);
            Rect row = listing.GetRect(30f);
#if SSC_RW17
            FloatMenuOption enable = SSCTrainerAssignmentMenu.EnableOption(pawn,
                current: () => SelPawn == pawn && pawn.TryGetComp<CompSexSlaveTraining>() == comp && SSCIdentityUtility.IsSexSlave(pawn));
            if (enable != null)
            {
                string enableLabel = "SSC_TrainerWork_Enable".Translate();
                float buttonWidth = Mathf.Min(row.width * .4f, Mathf.Max(58f, Text.CalcSize(enableLabel).x + 16f));
                Rect button = new Rect(row.xMax - buttonWidth, row.y, buttonWidth, row.height);
                if (Widgets.ButtonText(button, enableLabel)) enable.action();
                TooltipHandler.TipRegion(button, "SSC_TrainerWork_CategoryTip".Translate());
                row.width -= buttonWidth + 6f;
            }
#endif
            if (Widgets.ButtonText(row, trainerName) && CanEditTrainingConfiguration(pawn, comp))
                Find.WindowStack.Add(new FloatMenu(GetTrainerOptions(pawn)));
            string trainerTip = "SSC_Restrictions_AssignAuthorizesTip".Translate();
#if SSC_RW17
            if (comp.selectedTrainer != null)
                trainerTip += "\n\n" + SSCTrainerWorkUtility.Reason(
                    SSCTrainerWorkUtility.GetState(comp.selectedTrainer, pawn), comp.selectedTrainer);
#endif
            TooltipHandler.TipRegion(row, trainerTip);
        }

        /// <summary>设置回调只接受仍属于原 Pawn 的 SSC 性奴组件，不用已绘制面板代替后台身份检查。</summary>
        private static bool CanEditTrainingConfiguration(Pawn pawn, CompSexSlaveTraining comp)
        {
            if (comp != null && pawn?.TryGetComp<CompSexSlaveTraining>() == comp && SSCIdentityUtility.IsSexSlave(pawn))
                return true;
            Messages.Message("SSC_Training_TargetIdentityRequired".Translate(), pawn, MessageTypeDefOf.RejectInput, false);
            return false;
        }

        /// <summary>绘制特化选择和进度，按资格及终极状态限制选项，并同步所选方向的基础状态。</summary>
        private static void DrawSpecializationSelector(Listing_Standard listing, Pawn pawn, CompSexSlaveTraining comp)
        {
            string currentLabel = GetSpecializationLabel(pawn, comp);

            if (listing.ButtonText(currentLabel))
            {
                var options = new List<FloatMenuOption>
                {
                    new FloatMenuOption(Strings.ITab_SelectSpecializationNone,
                        () => comp.SetSpecialization(SexSlaveSpecializationType.None)),
                    BuildCombatantSpecializationOption(pawn, comp)
                };
#if SSC_RW17
                options.Add(BuildFertilitySpecializationOption(pawn, comp));
#endif
                if (CanShowLegacySpecializationOptions(comp))
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

                    options.AddRange(new[]
                    {
                        new FloatMenuOption(busOptionLabel, busEnabled ? (Action)delegate
                        {
                            if (!CanShowLegacySpecializationOptions(comp)) return;
                            comp.SetSpecialization(SexSlaveSpecializationType.Bus);
                            BusSpecializationUtility.EnsureBusHediffFromSpecialization(pawn);
                        } : null),
                        new FloatMenuOption(cowOptionLabel, cowEnabled ? (Action)delegate
                        {
                            if (!CanShowLegacySpecializationOptions(comp)) return;
                            comp.SetSpecialization(SexSlaveSpecializationType.Cow);
                            BusSpecializationUtility.EnsureCowHediffFromSpecialization(pawn);
                        } : null),
                        BuildPetSpecializationOption(pawn, comp, SexSlaveSpecializationType.PetCat),
                        BuildPetSpecializationOption(pawn, comp, SexSlaveSpecializationType.PetDog),
                        BuildPetSpecializationOption(pawn, comp, SexSlaveSpecializationType.PetRabbit),
                        BuildTrainerSpecializationOption(pawn, comp)
                    });
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }

            listing.Gap(6f);
            // 进度行沿用同一个完成门槛，悬停说明按当前战斗员／猫／狗方向提供终极化引导。
            // 查询不创建健康状态或授予技能，已取得宠物终极成果时不再提示重复加工。
            string progressTip = GetSpecializationProgressTip(pawn, comp);
            listing.Label((TaggedString)Strings.ITab_SpecializationProgress(GetSpecializationProgressText(pawn, comp)), -1f, progressTip);
#if SSC_RW17
            string trainerText = GetOrdinaryTrainerStatusText(pawn);
            if (trainerText != null)
                listing.Label((TaggedString)trainerText, -1f, "SSC_TrainerIdentity_RestrictionTip".Translate().ToString());
#else
            TrainerSpecializationDisplayState trainerState = TrainerSpecializationUtility.GetDisplayState(pawn);
            if (trainerState != TrainerSpecializationDisplayState.None)
            {
                // 终极记录可在培养其他方向时继续存在，故始终显示独立状态；
                // 普通方向的基础资格和 100% 完成则仅在当前培养训导官时出现。
                string status = ("SSC_TrainerIdentity_Status_" + trainerState).Translate().ToString();
                string tip = trainerState == TrainerSpecializationDisplayState.FinalDisabled
                    ? GetTrainerFinalDisabledReason(pawn)
                    : "SSC_TrainerIdentity_RestrictionTip".Translate().ToString();
                listing.Label("SSC_TrainerIdentity_StatusLine".Translate(status), -1f, tip);
            }
#endif
            DrawRabbitReproductionModeSelector(listing, pawn, comp);
#if SSC_RW17
            DrawFinalSpecializationStates(listing, pawn);
#endif
        }

        /// <summary>菜单打开和点击时均检查性奴身份及研究；选择后由组件同步普通状态。</summary>
        private static FloatMenuOption BuildCombatantSpecializationOption(Pawn pawn, CompSexSlaveTraining comp)
        {
            bool available = CombatantSpecializationUtility.CanSelect(pawn, out string reason);
            string label = Strings.ITab_SelectSpecializationCombatant;
            if (CombatantSpecializationUtility.HasFinalState(pawn))
                label += " " + Strings.ITab_SpecializationFinalizedSuffix;
            else if (!available && !string.IsNullOrEmpty(reason)) label += " (" + reason + ")";
            return new FloatMenuOption(label, available ? (Action)delegate
            {
                if (!CombatantSpecializationUtility.CanSelect(pawn, out _)) return;
                comp.SetSpecialization(SexSlaveSpecializationType.Combatant);
            } : null);
        }

        /// <summary>终极记录禁用时优先说明当前持续条件；标记等待维护时给出专门提示。</summary>
        private static string GetTrainerFinalDisabledReason(Pawn pawn)
        {
            return TrainerSpecializationUtility.MeetsContinuousConditions(pawn, out TrainerSpecializationFailure failure)
                ? "SSC_TrainerIdentity_FinalDisabledPending".Translate().ToString()
                : GetTrainerFailureReason(failure);
        }

        /// <summary>构造始终可见的训导官菜单项，并在点击时复查研究和培养条件。</summary>
        private static FloatMenuOption BuildTrainerSpecializationOption(Pawn pawn, CompSexSlaveTraining comp)
        {
            bool available = TrainerSpecializationUtility.CanSelectTrainerSpecialization(pawn,
                out TrainerSpecializationFailure failure);
            string label = Strings.ITab_SelectSpecializationTrainerOfficer;
            if (!available)
                label += failure == TrainerSpecializationFailure.AlreadyFinalized
                    ? " " + Strings.ITab_SpecializationFinalizedSuffix
                    : " (" + GetTrainerFailureReason(failure) + ")";

            // 已置灰的选项没有动作；可用选项仍须在点击时复查，避免菜单打开后
            // 解绑、退阶、身份变化或研究状态变化绕过选择门槛。
            return new FloatMenuOption(label, available ? (Action)delegate
            {
                if (!TrainerSpecializationUtility.CanSelectTrainerSpecialization(pawn,
                    out TrainerSpecializationFailure currentFailure))
                {
                    Messages.Message("SSC_TrainerIdentity_SelectionRejected".Translate(
                        GetTrainerFailureReason(currentFailure)), pawn, MessageTypeDefOf.RejectInput, false);
                    return;
                }
                if (pawn.TryGetComp<CompSexSlaveTraining>() != comp) return;
                if (!CanShowLegacySpecializationOptions(comp)) return;
                comp.SetSpecialization(SexSlaveSpecializationType.TrainerOfficer);
            } : null);
        }

        /// <summary>根据资格和终极状态构造宠物方向菜单项，合法选择后同步特化及基础状态。</summary>
        private static FloatMenuOption BuildPetSpecializationOption(Pawn pawn, CompSexSlaveTraining comp, SexSlaveSpecializationType type)
        {
            string optionLabel = PetSpecializationUtility.GetSelectLabel(type);
            bool enabled = PetSpecializationUtility.CanSelectPetSpecialization(pawn, type,
                out PetSpecializationFailure failure);
            if (!enabled)
            {
                string reason = PetSpecializationUtility.GetSelectionFailureReason(failure);
                optionLabel += failure == PetSpecializationFailure.NotImplemented ||
                    failure == PetSpecializationFailure.AlreadyFinalized ? " " + reason : " (" + reason + ")";
            }

            return new FloatMenuOption(optionLabel, enabled ? (Action)delegate
            {
                if (!PetSpecializationUtility.TrySelectPetSpecialization(pawn, comp, type,
                    out PetSpecializationFailure currentFailure))
                    Messages.Message("SSC_PetSpecialization_SelectionRejected".Translate(
                        PetSpecializationUtility.GetSelectionFailureReason(currentFailure)),
                        pawn, MessageTypeDefOf.RejectInput, false);
            } : null);
        }

        /// <summary>对兔特化角色显示已保存的繁殖模式；未完成的切换功能仍保持只读。</summary>
        private static void DrawRabbitReproductionModeSelector(Listing_Standard listing, Pawn pawn, CompSexSlaveTraining comp)
        {
            if (comp == null || pawn == null) return;
            if (!comp.IsPetRabbitSpecialized && !PetSpecializationUtility.HasAnyPetState(pawn, SexSlaveSpecializationType.PetRabbit)) return;

            string modeLabel = GetRabbitReproductionModeLabel(comp.rabbitReproductionMode);
            // 旧档仍展示已保存的模式，未完成期间不开放切换入口。
            listing.Label(Strings.ITab_RabbitReproductionMode(modeLabel) + " " + Strings.ITab_SpecializationUnfinishedSuffix);
        }

        /// <summary>将繁殖模式转换为本地化名称，未知值按后代模式显示。</summary>
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

#if !SSC_RW17
        /// <summary>仅在旧版本角色具有泌乳状态时显示并保存产奶开关。</summary>
        private static void DrawMilkToggle(Listing_Standard listing, Pawn pawn, CompSexSlaveTraining comp)
        {
            if (!pawn.health.hediffSet.HasHediff(SSCDefOf.SSC_Lactating_SubState)) return;

            bool enabled = comp.milkProductionEnabled;
            listing.CheckboxLabeled(Strings.ITab_MilkProductionToggle, ref enabled);
            comp.milkProductionEnabled = enabled;
        }
#endif
    }
}
