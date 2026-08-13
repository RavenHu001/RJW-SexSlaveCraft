using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine; // 必须引用，用于 GUI 绘制
using Verse;
using RimWorld;

namespace SexSlaveCraft
{
    public class ITab_PersonalityCard : ITab
    {
        // 设置窗口大小（增高以容纳底部分配按钮）
        private static readonly Vector2 WinSize = new Vector2(500f, 530f);

        public ITab_PersonalityCard()
        {
            this.size = WinSize;
            this.labelKey = "TabCharacter"; // 使用原版翻译 "角色"
            this.tutorTag = "Character";
        }

        // 只有当物品存在且里面存了有效数据时，才显示此页签
        public override bool IsVisible
        {
            get
            {
                var comp = SelThing.TryGetComp<CompPersonalityStore>();
                return comp != null && !string.IsNullOrEmpty(comp.nickName);
            }
        }

        // 主绘制逻辑
        protected override void FillTab()
        {
            // 1. 获取组件
            var comp = SelThing.TryGetComp<CompPersonalityStore>();
            if (comp == null) return;

            // 2. 设定绘图区域 (留出边距)
            Rect rect = new Rect(0f, 0f, this.size.x, this.size.y).ContractedBy(17f);

            GUI.BeginGroup(rect);

            // --- A. 头部信息 (名字与状态) ---
            DrawHeader(rect, comp);

            // --- B. 分割线 ---
            float curY = 60f;
            GUI.color = Color.gray;
            Widgets.DrawLineHorizontal(0f, curY, rect.width);
            GUI.color = Color.white;
            curY += 10f;

            // --- C. 分栏布局 ---
            float columnWidth = (rect.width - 20f) / 2f;
            float heightLeft = rect.height - curY;

            // 左栏：技能
            Rect leftRect = new Rect(0f, curY, columnWidth, heightLeft);
            DrawSkills(leftRect, comp);

            // 右栏：特质与记忆
            Rect rightRect = new Rect(columnWidth + 20f, curY, columnWidth, heightLeft - 50f);
            DrawTraitsAndInfo(rightRect, comp);

            // --- D. 底部分配按钮 ---
            DrawAssignmentButton(rect);

            GUI.EndGroup();
        }

        private void DrawHeader(Rect rect, CompPersonalityStore comp)
        {
            // 名字
            Text.Font = GameFont.Medium;
            Rect nameRect = new Rect(0f, 0f, rect.width, 30f);
            Widgets.Label(nameRect, comp.nickName);
            // 🔥 显示背景故事
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.7f, 0.7f, 0.7f); // 灰色字体

            string bgInfo = "";

            // 安全读取童年
            if (comp.childhood != null)
            {
                bgInfo += comp.childhood.title;
            }

            // 安全读取成年
            if (comp.adulthood != null)
            {
                bgInfo += " | " + comp.adulthood.title;
            }
            else
            {
                // 如果是小孩（无成年背景），显示提示
                bgInfo += Strings.PES_Underage;
            }

            Rect bgRect = new Rect(0f, 22f, rect.width, 20f);
            Widgets.Label(bgRect, bgInfo);

            GUI.color = Color.white;

            // 描述信息
            Text.Font = GameFont.Small;
            GUI.color = new Color(0.8f, 0.8f, 0.8f); // 浅灰色
            Rect descRect = new Rect(0f, 30f, rect.width, 25f);

            string info = "";
            if (!string.IsNullOrEmpty(comp.lastName)) info += Strings.PES_FullName(comp.lastName);
            if (comp.hasSexSlaveTrait) info += Strings.PES_SexSlaveTag;
            if (comp.storedRelations.Count > 0) info += Strings.PES_RelationsCount(comp.storedRelations.Count);

            Widgets.Label(descRect, info);
            GUI.color = Color.white;
        }

        private void DrawSkills(Rect rect, CompPersonalityStore comp)
        {
            // 使用 Listing_Standard 自动布局
            Listing_Standard list = new Listing_Standard();
            list.Begin(rect);

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft; // 文字左对齐垂直居中

            // 标题
            list.Label(Strings.PES_SkillsHeader);
            list.Gap(5f);

            // 按游戏原版顺序排序 (射击 -> 格斗 -> ...)
            var orderedSkills = comp.storedSkills.OrderBy(s => s.def.listOrder).ToList();

            foreach (var sData in orderedSkills)
            {
                // 获取一行矩形
                Rect lineRect = list.GetRect(24f);

                // 1. 技能名
                Rect labelRect = new Rect(lineRect.x, lineRect.y, 110f, lineRect.height);
                Widgets.Label(labelRect, sData.def.LabelCap);

                // 2. 兴趣火图标 (Passion)
                if (sData.passion != Passion.None)
                {
                    Rect passionRect = new Rect(labelRect.xMax - 24f, lineRect.y + 2f, 20f, 20f);
                    Texture2D passionTex = (sData.passion == Passion.Major)
                        ? SkillUI.PassionMajorIcon
                        : SkillUI.PassionMinorIcon;

                    GUI.DrawTexture(passionRect, passionTex);
                }

                // 3. 等级数值
                Rect levelNumRect = new Rect(labelRect.xMax, lineRect.y, 30f, lineRect.height);
                Widgets.Label(levelNumRect, sData.level.ToString());

                // 4. 经验条背景
                Rect barRect = new Rect(levelNumRect.xMax, lineRect.y + 6f, lineRect.width - 145f, 10f);
                Widgets.DrawBoxSolid(barRect, new Color(0.2f, 0.2f, 0.2f)); // 深灰底色

                // 5. 经验条前景
                if (sData.level > 0 || sData.xp > 0)
                {
                    float fillPercent = Mathf.Clamp01((float)sData.level / 20f);
                    if (fillPercent > 0)
                    {
                        Rect fillRect = new Rect(barRect.x, barRect.y, barRect.width * fillPercent, barRect.height);
                        // 如果有大火，条子稍微亮一点
                        Color barColor = (sData.passion == Passion.Major)
                            ? new Color(0.9f, 0.85f, 0.6f)
                            : new Color(0.9f, 0.9f, 0.9f);
                        Widgets.DrawBoxSolid(fillRect, barColor);
                    }
                }

                // 6. 鼠标悬停提示 (Tooltip)
                string tip = Strings.PES_SkillLevel(sData.def.LabelCap, sData.level);
                if (sData.passion != Passion.None) tip += Strings.PES_SkillPassion(sData.passion.ToString());
                // 这里只简单显示上次升级后的XP，因为没有存总XP
                tip += Strings.PES_SkillXP(sData.xp.ToString("F0"));

                TooltipHandler.TipRegion(lineRect, tip);
            }

            Text.Anchor = TextAnchor.UpperLeft; // 还原对齐
            list.End();
        }

        private void DrawTraitsAndInfo(Rect rect, CompPersonalityStore comp)
        {
            Listing_Standard list = new Listing_Standard();
            list.Begin(rect);

            Text.Font = GameFont.Small;

            // --- 特质部分 ---
            list.Label(Strings.PES_TraitsHeader);
            list.Gap(5f);

            if (comp.storedTraits.Count > 0)
            {
                foreach (var t in comp.storedTraits)
                {
                    // 格式化特质名字 (例如 "嗜血", "裸体主义")
                    string label = t.LabelCap;
                    // 如果有程度 (比如 "神经质: 严重")，原版 LabelCap 应该已经包含了，或者手动拼接
                    // TraitDef degreeData = t.def.DataAtDegree(t.Degree); ...

                    Rect traitRect = list.GetRect(24f);
                    Widgets.Label(traitRect, " - " + label);

                    // 鼠标悬停显示特质描述
                    string desc = t.def.DataAtDegree(t.Degree)?.description ?? t.def.description;
                    TooltipHandler.TipRegion(traitRect, desc);
                }
            }
            else
            {
                GUI.color = Color.gray;
                list.Label(Strings.PES_NoTraits);
                GUI.color = Color.white;
            }

            list.Gap(20f);

            // --- 其他信息 ---
            if (comp.chainHediffData != null && comp.chainHediffData.masterPawn != null)
            {
                list.Label(Strings.PES_BondHeader);
                list.Label(Strings.PES_MasterLabel(comp.chainHediffData.masterPawn.LabelShort));

                // 绘制严重度条
                Rect barRect = list.GetRect(15f);
                Widgets.FillableBar(barRect, comp.chainHediffData.severity);
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(barRect, $"{comp.chainHediffData.severity:P0}");
                Text.Anchor = TextAnchor.UpperLeft;
            }

            // --- 记忆预览 (可选) ---
            if (comp.storedMemories.Count > 0)
            {
                list.Gap(10f);
                list.Label(Strings.PES_MemoriesHeader(comp.storedMemories.Count));
                GUI.color = Color.gray;
                list.Label(Strings.PES_MemoriesHint);
                GUI.color = Color.white;
            }

            list.End();
        }

        /// <summary>
        /// 在ITab底部绘制"指定目标"按钮，用于将此凝胶分配给某个空壳Pawn。
        /// </summary>
        private void DrawAssignmentButton(Rect outerRect)
        {
            // 分割线
            float btnY = outerRect.height - 42f;
            GUI.color = Color.gray;
            Widgets.DrawLineHorizontal(0f, btnY - 8f, outerRect.width);
            GUI.color = Color.white;

            var mapComp = Find.CurrentMap?.GetComponent<MapComponent_PersonalityAssignment>();
            Pawn currentTarget = mapComp?.GetAssignedTarget(SelThing);

            // 按钮文本
            string btnLabel;
            if (currentTarget != null)
                btnLabel = Strings.PES_AssignedTo(currentTarget.LabelShort);
            else
                btnLabel = Strings.PES_AssignTarget;

            Rect btnRect = new Rect(10f, btnY, 220f, 30f);
            if (Widgets.ButtonText(btnRect, btnLabel))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();

                if (Find.CurrentMap != null)
                {
                    foreach (Pawn p in Find.CurrentMap.mapPawns.AllPawnsSpawned)
                    {
                        if (!p.RaceProps.Humanlike) continue;
                        if (p.Dead) continue;
                        if (!p.health.hediffSet.HasHediff(SSCDefOf.SSC_PersonalityExcreted_Done)) continue;

                        // 检查是否已被其他凝胶分配
                        Thing existingGel = mapComp?.GetAssignedGel(p);
                        string suffix = "";
                        if (existingGel != null && existingGel != SelThing)
                        {
                            var otherComp = existingGel.TryGetComp<CompPersonalityStore>();
                            string otherName = otherComp?.nickName ?? "?";
                            suffix = Strings.PES_AlreadyAssigned(otherName);
                        }

                        Pawn localP = p;
                        options.Add(new FloatMenuOption(p.LabelShort + suffix, delegate
                        {
                            mapComp?.Assign(SelThing, localP);
                        }));
                    }
                }

                // 取消分配选项
                if (currentTarget != null)
                {
                    options.Add(new FloatMenuOption(Strings.PES_Unassign, delegate
                    {
                        mapComp?.Unassign(SelThing);
                    }));
                }

                if (options.Count == 0)
                    options.Add(new FloatMenuOption(Strings.PES_NoHollowTargets, null));

                Find.WindowStack.Add(new FloatMenu(options));
            }

            // 在按钮右侧显示当前分配状态提示
            if (currentTarget != null)
            {
                Rect statusRect = new Rect(btnRect.xMax + 10f, btnY, outerRect.width - btnRect.xMax - 20f, 30f);
                GUI.color = new Color(0.6f, 0.9f, 0.6f);
                Text.Font = GameFont.Tiny;
                Widgets.Label(statusRect, Strings.PES_WaitingForInsert);
                Text.Font = GameFont.Small;
                GUI.color = Color.white;
            }
        }
    }
}