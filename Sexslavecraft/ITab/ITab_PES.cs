using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;

namespace SexSlaveCraft
{
    public class ITab_PersonalityCard : ITab
    {
        private static readonly Vector2 WinSize = new Vector2(500f, 530f);
        private const float ColumnGap = 20f;
        private const float SectionGap = 8f;
        private const float ScrollbarSpace = 18f;
        private Thing displayedGel;
        private Vector2 skillScrollPosition;
        private Vector2 traitScrollPosition;

        /// <summary>初始化人格页签的尺寸、标题翻译键和教学标记。</summary>
        public ITab_PersonalityCard()
        {
            size = WinSize;
            labelKey = "TabCharacter";
            tutorTag = "Character";
        }

        /// <summary>仅在选中物品带有具名人格快照时显示页签。</summary>
        public override bool IsVisible => !string.IsNullOrEmpty(SelThing?.TryGetComp<CompPersonalityStore>()?.nickName);

        /// <summary>绘制固定头部、独立滚动的双栏内容和固定操作区，并恢复进入页签前的绘图状态。</summary>
        protected override void FillTab()
        {
            Thing gel = SelThing;
            CompPersonalityStore comp = gel?.TryGetComp<CompPersonalityStore>();
            if (comp == null) return;
            if (displayedGel != gel)
            {
                displayedGel = gel;
                skillScrollPosition = Vector2.zero;
                traitScrollPosition = Vector2.zero;
            }
            Color previousColor = GUI.color;
            GameFont previousFont = Text.Font;
            TextAnchor previousAnchor = Text.Anchor;
            bool previousWordWrap = Text.WordWrap;
            Rect outer = new Rect(0f, 0f, size.x, size.y).ContractedBy(17f);
            GUI.BeginGroup(outer);
            try
            {
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.WordWrap = true;
                float rowHeight = Mathf.Max(22f, Text.LineHeight);
                float headerHeight = DrawHeader(outer.width, rowHeight, comp);
                DrawDivider(0f, headerHeight, outer.width);
                // 先预留操作区，再分配正文，避免多特质或新增技能覆盖底部按钮。
                float footerHeight = rowHeight + 38f;
                Rect footer = new Rect(0f, outer.height - footerHeight, outer.width, footerHeight);
                float bodyY = headerHeight + 10f;
                float bodyHeight = Mathf.Max(0f, footer.y - 12f - bodyY);
                float leftWidth = (outer.width - ColumnGap) * 0.55f;
                DrawSkills(new Rect(0f, bodyY, leftWidth, bodyHeight), comp, rowHeight);
                DrawTraitsAndInfo(new Rect(leftWidth + ColumnGap, bodyY,
                    outer.width - leftWidth - ColumnGap, bodyHeight), comp, rowHeight);
                DrawAssignmentButton(footer, rowHeight);
            }
            finally
            {
                GUI.EndGroup();
                GUI.color = previousColor;
                Text.Font = previousFont;
                Text.Anchor = previousAnchor;
                Text.WordWrap = previousWordWrap;
            }
        }

        /// <summary>分别绘制昵称、身份及两行背景，完整姓名通过悬停查看，返回实际占用高度。</summary>
        private float DrawHeader(float width, float rowHeight, CompPersonalityStore comp)
        {
            string identity = comp.hasSexSlaveTrait ? Strings.PES_SexSlaveTag.Trim() : string.Empty;
            float identityWidth = identity.Length == 0 ? 0f : Mathf.Min(width * 0.35f, Text.CalcSize(identity).x + 12f);
            Text.Font = GameFont.Medium;
            float titleHeight = Mathf.Max(30f, Text.LineHeight);
            Rect nameRect = new Rect(0f, 0f, width - identityWidth, titleHeight);
            string fullName = new NameTriple(comp.firstName ?? string.Empty, comp.nickName ?? string.Empty,
                comp.lastName ?? string.Empty).ToStringFull;
            DrawSingleLine(nameRect, comp.nickName, Strings.PES_FullName(fullName));
            Text.Font = GameFont.Small;
            if (identityWidth > 0f)
                DrawSingleLine(new Rect(width - identityWidth, 0f, identityWidth, titleHeight), identity,
                    anchor: TextAnchor.MiddleRight);
            float y = titleHeight + 4f;
            DrawSingleLine(new Rect(0f, y, width, rowHeight),
                Strings.PES_Childhood(comp.childhood?.title ?? Strings.PES_NoBackground), muted: true);
            y += rowHeight;
            DrawSingleLine(new Rect(0f, y, width, rowHeight),
                Strings.PES_Adulthood(comp.adulthood?.title ?? Strings.PES_NoBackground), muted: true);
            return y + rowHeight + 6f;
        }

        /// <summary>在固定标题下滚动显示技能，将名称、热情、右对齐等级和等级条分成独立列。</summary>
        private void DrawSkills(Rect rect, CompPersonalityStore comp, float rowHeight)
        {
            DrawSingleLine(new Rect(rect.x, rect.y, rect.width, rowHeight), Strings.PES_SkillsHeader);
            List<StoredSkillData> skills = comp.storedSkills?.Where(skill => skill?.def != null)
                .OrderBy(skill => skill.def.listOrder).ToList() ?? new List<StoredSkillData>();
            float lineHeight = Mathf.Max(24f, rowHeight);
            Rect viewport = new Rect(rect.x, rect.y + rowHeight + 4f, rect.width,
                Mathf.Max(0f, rect.height - rowHeight - 4f));
            if (viewport.height <= 0f) return;
            float contentWidth = viewport.width - ScrollbarSpace;
            float contentHeight = skills.Count * lineHeight;
            ClampScroll(ref skillScrollPosition, contentHeight, viewport.height);
            Widgets.BeginScrollView(viewport, ref skillScrollPosition,
                new Rect(0f, 0f, contentWidth, Mathf.Max(viewport.height, contentHeight)));
            try
            {
                for (int i = 0; i < skills.Count; i++)
                {
                    StoredSkillData skill = skills[i];
                    Rect row = new Rect(0f, i * lineHeight, contentWidth, lineHeight);
                    if (i % 2 == 0) Widgets.DrawBoxSolid(row, new Color(1f, 1f, 1f, 0.025f));
                    float barWidth = 70f;
                    float numberX = contentWidth - barWidth - 34f;
                    float passionX = numberX - 26f;
                    DrawSingleLine(new Rect(0f, row.y, passionX - 4f, lineHeight), skill.def.LabelCap);
                    if (skill.passion != Passion.None)
                    {
                        Texture2D icon = skill.passion == Passion.Major ? SkillUI.PassionMajorIcon : SkillUI.PassionMinorIcon;
                        GUI.DrawTexture(new Rect(passionX, row.y + (lineHeight - 20f) / 2f, 20f, 20f), icon);
                    }
                    DrawSingleLine(new Rect(numberX, row.y, 28f, lineHeight), skill.level.ToString(),
                        anchor: TextAnchor.MiddleRight);
                    Rect bar = new Rect(contentWidth - barWidth, row.y + (lineHeight - 8f) / 2f, barWidth, 8f);
                    Widgets.DrawBoxSolid(bar, new Color(0.2f, 0.2f, 0.2f));
                    float fill = Mathf.Clamp01(skill.level / 20f);
                    if (fill > 0f)
                        Widgets.DrawBoxSolid(new Rect(bar.x, bar.y, bar.width * fill, bar.height),
                            skill.passion == Passion.Major ? new Color(0.9f, 0.85f, 0.6f) : new Color(0.85f, 0.85f, 0.85f));
                    string tip = Strings.PES_SkillLevel(skill.def.LabelCap, skill.level);
                    if (skill.passion != Passion.None) tip += Strings.PES_SkillPassion(skill.passion.ToString());
                    tip += Strings.PES_SkillXP(skill.xp.ToString("F0"));
                    TooltipHandler.TipRegion(row, tip);
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }

        /// <summary>按可用高度限制特质滚动区，将隶属关系和记录摘要保留在滚动区域之外。</summary>
        private void DrawTraitsAndInfo(Rect rect, CompPersonalityStore comp, float rowHeight)
        {
            List<Trait> traits = comp.storedTraits?.Where(trait => trait?.def != null).ToList() ?? new List<Trait>();
            DrawSingleLine(new Rect(rect.x, rect.y, rect.width, rowHeight), Strings.PES_TraitsCount(traits.Count));
            float contentWidth = rect.width - ScrollbarSpace;
            float[] heights = traits.Select(trait => Mathf.Max(24f,
                Text.CalcHeight(trait.LabelCap, contentWidth - 12f) + 2f)).ToArray();
            float contentHeight = traits.Count == 0 ? rowHeight : heights.Sum();
            float summaryHeight = rowHeight * 5f + 12f;
            float availableHeight = Mathf.Max(0f, rect.height - rowHeight - 4f - SectionGap - summaryHeight);
            float viewportHeight = Mathf.Min(contentHeight, availableHeight);
            Rect viewport = new Rect(rect.x, rect.y + rowHeight + 4f, rect.width, viewportHeight);
            if (viewportHeight > 0f)
            {
                ClampScroll(ref traitScrollPosition, contentHeight, viewportHeight);
                Widgets.BeginScrollView(viewport, ref traitScrollPosition,
                    new Rect(0f, 0f, contentWidth, Mathf.Max(viewportHeight, contentHeight)));
                try
                {
                    if (traits.Count == 0)
                        DrawSingleLine(new Rect(0f, 0f, contentWidth, rowHeight), Strings.PES_NoTraits, muted: true);
                    float y = 0f;
                    for (int i = 0; i < traits.Count; i++)
                    {
                        Trait trait = traits[i];
                        Rect row = new Rect(0f, y, contentWidth, heights[i]);
                        if (i % 2 == 0) Widgets.DrawBoxSolid(row, new Color(1f, 1f, 1f, 0.025f));
                        Widgets.Label(new Rect(0f, y, 10f, rowHeight), "·");
                        // 测量和绘制使用同一字号与宽度，长特质换行后不会压住下一条。
                        Widgets.Label(new Rect(12f, y, contentWidth - 12f, heights[i]), trait.LabelCap);
                        TooltipHandler.TipRegion(row, trait.def.DataAtDegree(trait.Degree)?.description ?? trait.def.description);
                        y += heights[i];
                    }
                }
                finally
                {
                    Widgets.EndScrollView();
                }
            }
            // 少量特质时摘要随列表收缩上移，多量特质时停留在正文底部。
            DrawSummary(new Rect(rect.x, viewport.yMax + SectionGap, rect.width, summaryHeight), comp, rowHeight);
        }

        /// <summary>显示不随特质列表滚动的主人、锁链严重度以及关系和记忆数量。</summary>
        private void DrawSummary(Rect rect, CompPersonalityStore comp, float rowHeight)
        {
            DrawDivider(rect.x, rect.y, rect.width);
            float y = rect.y + 4f;
            DrawSingleLine(new Rect(rect.x, y, rect.width, rowHeight), Strings.PES_BondHeader);
            y += rowHeight;
            Pawn master = comp.chainHediffData?.masterPawn;
            DrawSingleLine(new Rect(rect.x, y, rect.width, rowHeight),
                master == null ? Strings.PES_NoMaster : Strings.PES_MasterLabel(master.LabelShort).Trim());
            y += rowHeight;
            DrawSingleLine(new Rect(rect.x, y, rect.width - 46f, rowHeight), Strings.PES_ChainSeverityLabel, muted: true);
            DrawSingleLine(new Rect(rect.xMax - 46f, y, 46f, rowHeight),
                comp.chainHediffData == null ? "—" : comp.chainHediffData.severity.ToString("P0"), anchor: TextAnchor.MiddleRight);
            y += rowHeight;
            Widgets.FillableBar(new Rect(rect.x, y, rect.width, 6f), Mathf.Clamp01(comp.chainHediffData?.severity ?? 0f));
            y += 8f;
            DrawSingleLine(new Rect(rect.x, y, rect.width, rowHeight), Strings.PES_RelationshipSummary(comp.storedRelations?.Count ?? 0));
            y += rowHeight;
            DrawSingleLine(new Rect(rect.x, y, rect.width, rowHeight), Strings.PES_MemorySummary(comp.storedMemories?.Count ?? 0),
                Strings.PES_MemoriesHint.Trim());
        }

        /// <summary>在正文之外绘制目标状态及整行分配按钮，长目标名称通过悬停查看。</summary>
        private void DrawAssignmentButton(Rect rect, float rowHeight)
        {
            DrawDivider(rect.x, rect.y - 6f, rect.width);
            var mapComp = Find.CurrentMap?.GetComponent<MapComponent_PersonalityAssignment>();
            Pawn currentTarget = mapComp?.GetAssignedTarget(SelThing);
            DrawSingleLine(new Rect(rect.x, rect.y, rect.width, rowHeight),
                currentTarget == null ? Strings.PES_TargetUnassigned : Strings.PES_AssignedTo(currentTarget.LabelShort),
                currentTarget == null ? null : Strings.PES_AssignedTo(currentTarget.LabelShort) + "\n" + Strings.PES_WaitingForInsert);
            if (Widgets.ButtonText(new Rect(rect.x, rect.y + rowHeight + 6f, rect.width, 32f),
                currentTarget == null ? Strings.PES_AssignTarget : Strings.PES_ChangeTarget))
                ShowAssignmentMenu(mapComp, currentTarget);
        }

        /// <summary>打开现有的空壳分配菜单，保留占用提示、目标选择和取消分配行为。</summary>
        private void ShowAssignmentMenu(MapComponent_PersonalityAssignment mapComp, Pawn currentTarget)
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            if (Find.CurrentMap != null && mapComp != null)
            {
                foreach (Pawn pawn in Find.CurrentMap.mapPawns.AllPawnsSpawned)
                {
                    if (!pawn.RaceProps.Humanlike || pawn.Dead ||
                        !pawn.health.hediffSet.HasHediff(SSCDefOf.SSC_PersonalityExcreted_Done)) continue;
                    Thing existingGel = mapComp.GetAssignedGel(pawn);
                    string suffix = existingGel != null && existingGel != SelThing
                        ? Strings.PES_AlreadyAssigned(existingGel.TryGetComp<CompPersonalityStore>()?.nickName ?? "?") : string.Empty;
                    Pawn target = pawn;
                    Thing gel = SelThing;
                    // 捕获打开菜单时的凝胶，避免切换选中物品后把分配应用到另一件物品。
                    options.Add(new FloatMenuOption(pawn.LabelShort + suffix, () => mapComp.Assign(gel, target)));
                }
            }
            if (currentTarget != null && mapComp != null)
            {
                Thing gel = SelThing;
                options.Add(new FloatMenuOption(Strings.PES_Unassign, () => mapComp.Unassign(gel)));
            }
            if (options.Count == 0) options.Add(new FloatMenuOption(Strings.PES_NoHollowTargets, null));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        /// <summary>限制滚动位置，防止切换内容数量后停留在空白区域，同时关闭水平滚动。</summary>
        private static void ClampScroll(ref Vector2 position, float contentHeight, float viewportHeight)
        {
            position.x = 0f;
            position.y = Mathf.Clamp(position.y, 0f, Mathf.Max(0f, contentHeight - viewportHeight));
        }

        /// <summary>绘制单行省略文本并提供完整提示，局部对齐及颜色设置不会影响后续控件。</summary>
        private static void DrawSingleLine(Rect rect, string text, string tooltip = null, bool muted = false,
            TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            Color previousColor = GUI.color;
            TextAnchor previousAnchor = Text.Anchor;
            bool previousWordWrap = Text.WordWrap;
            try
            {
                GUI.color = muted ? new Color(0.72f, 0.72f, 0.72f) : Color.white;
                Text.Anchor = anchor;
                Text.WordWrap = false;
                Widgets.Label(rect, (text ?? string.Empty).Truncate(rect.width));
                TooltipHandler.TipRegion(rect, tooltip ?? text ?? string.Empty);
            }
            finally
            {
                GUI.color = previousColor;
                Text.Anchor = previousAnchor;
                Text.WordWrap = previousWordWrap;
            }
        }

        /// <summary>绘制低对比度分隔线，保持各区域边界清晰。</summary>
        private static void DrawDivider(float x, float y, float width)
        {
            Widgets.DrawBoxSolid(new Rect(x, y, width, 1f), new Color(0.4f, 0.4f, 0.4f));
        }
    }
}
