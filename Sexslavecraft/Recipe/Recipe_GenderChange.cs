using RimWorld;
using rjw;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace SexSlaveCraft
{
    public class Recipe_GenderChange_MtF : Recipe_Surgery
    {
        // -------------------------------------------------------
        // 新增部分：控制手术菜单的显示
        // -------------------------------------------------------
        /// <summary>在原版可用性检查通过后，将手术限制为男性角色。</summary>
        public override bool AvailableOnNow(Thing thing, BodyPartRecord part = null)
        {
            // 1. 先执行原版的基础检查（如：是否被囚禁、是否有医疗床等）
            if (!base.AvailableOnNow(thing, part))
            {
                return false;
            }

            // 2. 检查对象是否为 Pawn（生物）
            if (thing is Pawn pawn)
            {
                // 3. 核心限制：必须是男性 (
                // )
                // 如果不是男性，返回 false，手术选项将不会出现在菜单里
                if (pawn.gender != Gender.Male)
                {
                    return false;
                }
            }

            return true;
        }

        // -------------------------------------------------------
        // 原有部分：手术执行逻辑
        // -------------------------------------------------------
        /// <summary>手术成功后调整性别、器官和外观，并在术后记忆定义存在时添加心情记忆。</summary>
        /// <remarks>记忆定义缺失时保留查询错误日志，跳过添加，避免已完成的手术在收尾时抛出空引用异常。</remarks>
        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
        {
            // 双重保险：虽然菜单限制了，但为了防止意外，执行前再查一次
            if (pawn.gender != Gender.Male) return;

            if (billDoer != null)
            {
                if (CheckSurgeryFail(billDoer, pawn, ingredients, part, bill)) return;
                TaleRecorder.RecordTale(TaleDefOf.DidSurgery, billDoer, pawn);
            }

            // 变性操作
            pawn.gender = Gender.Female;

            // 把原有男性器官与胸部替换成女性配置。
            RebuildSexPartsForFemale(pawn);

            // 智能体型适配
            SafeFixBodyType(pawn);

            // 去除胡子 & 刷新
            if (pawn.style != null) pawn.style.beardDef = BeardDefOf.NoBeard;
            pawn.Drawer.renderer.SetAllGraphicsDirty();
            PortraitsCache.SetDirty(pawn);
            // ==========================================
            // NEW: 添加手术后的心情记忆
            // ==========================================
            if (pawn.needs != null && pawn.needs.mood != null)
            {
                // 获取我们在 XML 里定义的 ThoughtDef
                ThoughtDef successThought = DefDatabase<ThoughtDef>.GetNamed("SSC_Thought_GenderChangeSuccess");

                // 原版记忆入口不接受空定义；缺失时保留 GetNamed 的诊断并安全跳过。
                if (successThought != null)
                {
                    pawn.needs.mood.thoughts.memories.TryGainMemory(successThought);
                }
            }
        }

        /// <summary>移除原有生殖器官与胸部状态，通过 RJW 接口建立女性器官配置。</summary>
        private void RebuildSexPartsForFemale(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null) return;

            List<Hediff> genitals = pawn.GetGenitalsList();
            if (genitals != null)
            {
                foreach (Hediff hediff in genitals.ToList())
                {
                    if (Genital_Helper.is_penis(hediff) || Genital_Helper.is_vagina(hediff))
                    {
                        pawn.health.RemoveHediff(hediff);
                    }
                }
            }

            List<Hediff> breasts = pawn.GetBreastList();
            if (breasts != null)
            {
                foreach (Hediff hediff in breasts.ToList())
                {
                    pawn.health.RemoveHediff(hediff);
                }
            }

            SexPartAdder.add_genitals(pawn, gender: Gender.Female);
            SexPartAdder.add_breasts(pawn, gender: Gender.Female);
            SexPartAdder.add_anus(pawn, gender: Gender.Female);
        }

        // --- 安全适配逻辑 (保持不变) ---
        /// <summary>优先使用异种框架允许的体型；未处理时将原版男性体型改为女性体型。</summary>
        private void SafeFixBodyType(Pawn pawn)
        {
            bool hasAlienRaces = ModLister.GetActiveModWithIdentifier("erdelf.HumanoidAlienRaces") != null;
            bool handled = false;

            if (hasAlienRaces)
            {
                handled = TryFixAlienBodyType(pawn);
            }

            if (!handled)
            {
                if (pawn.story.bodyType == BodyTypeDefOf.Male)
                {
                    pawn.story.bodyType = BodyTypeDefOf.Female;
                }
            }
        }

        // --- 反射部分 (保持不变) ---
        /// <summary>通过反射读取异种允许的体型并选择合适项；接口缺失或处理异常时返回失败。</summary>
        private bool TryFixAlienBodyType(Pawn pawn)
        {
            try
            {
                Type alienDefType = Type.GetType("AlienRace.ThingDef_AlienRace, AlienRace");
                if (alienDefType == null) return false;

                if (!alienDefType.IsInstanceOfType(pawn.def)) return false;

                FieldInfo alienRaceField = alienDefType.GetField("alienRace");
                object alienRaceObj = alienRaceField.GetValue(pawn.def);
                if (alienRaceObj == null) return false;

                FieldInfo generalSettingsField = alienRaceObj.GetType().GetField("generalSettings");
                object generalSettingsObj = generalSettingsField.GetValue(alienRaceObj);
                if (generalSettingsObj == null) return false;

                FieldInfo alienPartGeneratorField = generalSettingsObj.GetType().GetField("alienPartGenerator");
                object partGenObj = alienPartGeneratorField.GetValue(generalSettingsObj);
                if (partGenObj == null) return false;

                FieldInfo bodyTypesField = partGenObj.GetType().GetField("bodyTypes");
                List<BodyTypeDef> allowedBodies = bodyTypesField.GetValue(partGenObj) as List<BodyTypeDef>;

                if (allowedBodies == null || allowedBodies.Count == 0) return false;

                if (allowedBodies.Contains(pawn.story.bodyType)) return true;

                if (allowedBodies.Contains(BodyTypeDefOf.Female))
                {
                    pawn.story.bodyType = BodyTypeDefOf.Female;
                }
                else
                {
                    pawn.story.bodyType = allowedBodies[0];
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
