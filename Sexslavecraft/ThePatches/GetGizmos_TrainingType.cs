using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
/*
namespace SexSlaveCraft
{
    // =============================================================
    // 1. 枚举定义
    // =============================================================
    public enum TrainingActType
    {
        Auto,               // 自动
        Vaginal,            // 本番
        Anal,               // 后庭
        Oral,               // 口交
        Boobjob,            // 乳交
        Handjob,            // 手交
        Footjob,            // 足交
        Fingering,          // 指交
        MutualMasturbation, // 互相自慰
        Fisting,            // 拳交
        Rimming,            // 舔肛
        Sixtynine           // 69
    }

    // =============================================================
    // 2. CompProperties
    // =============================================================
    public class CompProperties_TrainingMode : CompProperties
    {
        public CompProperties_TrainingMode()
        {
            this.compClass = typeof(CompTrainingMode);
        }
    }

    // =============================================================
    // 3. 逻辑组件类 (Gizmo 核心)
    // =============================================================
    public class CompTrainingMode : ThingComp
    {
        public TrainingActType selectedMode = TrainingActType.Auto;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref selectedMode, "selectedMode", TrainingActType.Auto);
        }

        // ---------------------------------------------------------
        // 核心逻辑：根据动作类型返回图标路径
        // ---------------------------------------------------------
        private string GetIconPath(TrainingActType type)
        {
            // 基础路径：Textures/UI/Icons/Training/
            string basePath = "UI/Icons/Training/";

            switch (type)
            {
                // 1. 阴道组 (Icon_Vaginal)
                case TrainingActType.Vaginal:
                case TrainingActType.Fingering:
                case TrainingActType.Fisting:
                case TrainingActType.Sixtynine:
                    return basePath + "Icon_Vaginal";

                // 2. 后庭组 (Icon_Anal)
                case TrainingActType.Anal:
                case TrainingActType.Rimming:
                    return basePath + "Icon_Anal";

                // 3. 口部组 (Icon_Oral)
                case TrainingActType.Oral:
                    return basePath + "Icon_Oral";

                // 4. 胸部组 (Icon_Breasts)
                case TrainingActType.Boobjob:
                    return basePath + "Icon_Breasts";

                // 5. 手部组 (Icon_Hand)
                case TrainingActType.Handjob:
                case TrainingActType.MutualMasturbation:
                    return basePath + "Icon_Hand";

                // 6. 足部组 (Icon_Foot)
                case TrainingActType.Footjob:
                    return basePath + "Icon_Foot";

                // 0. 默认 / 自动
                case TrainingActType.Auto:
                default:
                    return basePath + "Icon_Auto";
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            Pawn p = this.parent as Pawn;

            // 基础检查
            if (p == null || !p.Spawned) yield break;

            // =========================================================
            // 1. 性别过滤：男性不显示
            // =========================================================
            // 支持变性手术动态刷新 (男->女 后会自动显示)
            if (p.gender == Gender.
) yield break;

            // =========================================================
            // 2. 身份过滤
            // =========================================================
            // 必须是：殖民者 OR 囚犯 OR 奴隶
            // (这里增加了 IsSlave 检查，确保与 WorkGiver 逻辑一致)
            if (!SSCIdentityUtility.IsSupportedVanillaStatus(p)) yield break;

            yield return new Command_Action
            {
                // 标题：调教姿势: [当前模式中文名]
                defaultLabel = Strings.TrainingMode_GizmoLabel(selectedMode),

                // 描述
                defaultDesc = Strings.TrainingMode_GizmoDesc,

                // 图标：根据当前模式动态变化
                icon = ContentFinder<Texture2D>.Get(GetIconPath(selectedMode), true),

                // 点击后的菜单逻辑
                action = delegate
                {
                    List<FloatMenuOption> options = new List<FloatMenuOption>();

                    foreach (TrainingActType type in System.Enum.GetValues(typeof(TrainingActType)))
                    {
                        // 菜单项文本：获取枚举的中文翻译
                        options.Add(new FloatMenuOption(Strings.GetModeLabel(type), delegate
                        {
                            selectedMode = type;
                        }));
                    }
                    Find.WindowStack.Add(new FloatMenu(options));
                }
            };
        }
    }
}*/
