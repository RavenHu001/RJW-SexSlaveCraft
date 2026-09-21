using System;
using System.Collections.Generic;
using Verse;

namespace SexSlaveCraft
{
    /// <summary>标识人格可保存训练进度的特化方向。</summary>
    public enum SexSlaveSpecializationType
    {
        None,
        Bus,
        Cow,
        PetCat,
        PetDog,
        PetRabbit
    }

    public partial class CompSexSlaveTraining
    {
        public SexSlaveSpecializationType specializationType = SexSlaveSpecializationType.None;
        public float specializationProgress = 0f;
        private Dictionary<string, float> perTypeProgress;

        /// <summary>导出独立的各方向进度快照，并用当前进度覆盖尚未归档的同方向记录。</summary>
        public Dictionary<string, float> ExportSpecializationProgress()
        {
            return CopySpecializationProgress(specializationType, specializationProgress, perTypeProgress);
        }

        /// <summary>用人格快照整体替换身体原有训练历史；旧凝胶缺少历史时只恢复已知的当前方向。</summary>
        public void RestoreSpecializationProgress(
            SexSlaveSpecializationType type,
            float currentProgress,
            Dictionary<string, float> savedProgress)
        {
            SexSlaveSpecializationType restoredType = NormalizeSpecializationType(type);
            perTypeProgress = CopySpecializationProgress(restoredType, currentProgress, savedProgress);

            // 先取消旧方向，避免切换入口把接收身体的当前进度重新写入刚导入的历史。
            specializationType = SexSlaveSpecializationType.None;
            specializationProgress = 0f;
            SetSpecialization(restoredType);

            // 无当前方向时切换入口不会触发清理，仍需去掉身体上不再生效的基础状态。
            // 新方向的健康状态继续由人格植入流程末尾的统一对账恢复。
            if (restoredType == SexSlaveSpecializationType.None && parent is Pawn pawn)
            {
                RemoveInactiveSpecializationStates(pawn, this, restoredType);
            }
        }

        /// <summary>复制可识别的方向历史，清理损坏数值，并以当前字段作为该方向的权威进度。</summary>
        private static Dictionary<string, float> CopySpecializationProgress(
            SexSlaveSpecializationType type,
            float currentProgress,
            Dictionary<string, float> savedProgress)
        {
            var result = new Dictionary<string, float>(StringComparer.Ordinal);
            if (savedProgress != null)
            {
                foreach (KeyValuePair<string, float> entry in savedProgress)
                {
                    SexSlaveSpecializationType savedType;
                    if (!Enum.TryParse(entry.Key, out savedType) ||
                        NormalizeSpecializationType(savedType) == SexSlaveSpecializationType.None ||
                        entry.Key != savedType.ToString())
                    {
                        continue;
                    }

                    result[entry.Key] = NormalizeSpecializationProgress(entry.Value);
                }
            }

            type = NormalizeSpecializationType(type);
            if (type != SexSlaveSpecializationType.None)
            {
                result[type.ToString()] = NormalizeSpecializationProgress(currentProgress);
            }

            return result;
        }

        /// <summary>把未知枚举值视为尚未选择方向，避免损坏存档生成无法识别的训练状态。</summary>
        private static SexSlaveSpecializationType NormalizeSpecializationType(SexSlaveSpecializationType type)
        {
            return Enum.IsDefined(typeof(SexSlaveSpecializationType), type) ? type : SexSlaveSpecializationType.None;
        }

        /// <summary>把损坏的非有限进度归零，并将普通数值限制在有效的零到一范围内。</summary>
        private static float NormalizeSpecializationProgress(float progress)
        {
            if (float.IsNaN(progress) || float.IsInfinity(progress)) return 0f;
            return Math.Max(0f, Math.Min(1f, progress));
        }

        /// <summary>切换同一角色的当前方向并保留各方向历史；限制默认由方向变更后的统一生命周期事件处理。</summary>
        public void SetSpecialization(SexSlaveSpecializationType type)
        {
            SexSlaveSpecializationType previousType = specializationType;
            bool changedType = previousType != type;

            if (changedType)
            {
                // 特化进度按类型独立保存，切换特化不会清空已投入的进度。
                if (previousType != SexSlaveSpecializationType.None)
                {
                    SetSavedProgress(previousType, specializationProgress);
                }

                specializationProgress = type == SexSlaveSpecializationType.None ? 0f : GetSavedProgress(type);

                if (parent is Pawn pawn)
                {
                    RemoveInactiveSpecializationStates(pawn, this, type);
                }
            }

            specializationType = type;
            if (type == SexSlaveSpecializationType.None)
            {
                specializationProgress = 0f;
                rabbitReproductionMode = RabbitReproductionMode.Offspring;
                return;
            }

            // 不再写旧“允许其他人”开关。公交车只声明新系统中的两项被动默认/强制值，
            // 方向切换本身不能扩大主动许可或调教许可，也不能抹掉已有个体选择。

            if (type != SexSlaveSpecializationType.PetRabbit)
            {
                rabbitReproductionMode = RabbitReproductionMode.Offspring;
            }
        }

        /// <summary>读取同一角色此前归档的方向进度，没有历史记录时从零开始。</summary>
        private float GetSavedProgress(SexSlaveSpecializationType type)
        {
            if (perTypeProgress == null) return 0f;
            return perTypeProgress.TryGetValue(type.ToString(), out float value) ? value : 0f;
        }

        /// <summary>归档离开方向前的最新进度，供同一人格之后切回该方向使用。</summary>
        private void SetSavedProgress(SexSlaveSpecializationType type, float value)
        {
            perTypeProgress = perTypeProgress ?? new Dictionary<string, float>();
            perTypeProgress[type.ToString()] = value;
        }
    }
}
