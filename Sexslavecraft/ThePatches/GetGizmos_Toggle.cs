using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using UnityEngine;

/*
namespace SexSlaveCraft
{
    // 1. 枚举只保留两个状态
    public enum TrainingMode
    {
        Disabled,   // 0: 未启用
        Enabled     // 1: 启用
    }

    public class CompSimpleToggle : ThingComp
    {
        public TrainingMode mode = TrainingMode.Disabled;

        // 核心逻辑变量
        public int lastTrainingTick = -999999; // 初始化为负数，保证刚开始就能训练
        public bool isBeingTrained = false;     // 防并发锁

        // 兼容旧存档字段
        public bool isEnabled = false;


        // =========================================================
        // 【新增】常量定义：冷却时间 (15000 ticks ≈ 6 小时)
        // =========================================================
        public const int CooldownTicks = 15000;

        // =========================================================
        // 【新增】对外属性：像狱卒机制一样的冷却判断
        // WorkGiver 直接读这个属性，不用自己做减法
        // =========================================================
        public bool IsOnCooldown
        {
            get
            {
                // 当前时间 - 上次完成时间 < 冷却间隔 = 正在冷却
                return (Find.TickManager.TicksGame - lastTrainingTick) < CooldownTicks;
            }
        }

        // 对外属性：是否启用 (方便外部调用)
        public bool IsEnabled => mode == TrainingMode.Enabled;

        // =========================================================
        // 【新增】状态管理方法 (供 JobDriver 调用)
        // =========================================================

        // 1. 任务开始
        public void Notify_TrainingStarted()
        {
            isBeingTrained = true;
        }

        // 2. 任务【成功完成】 -> 解锁并进入冷却
        public void Notify_TrainingCompleted()
        {
            isBeingTrained = false;
            lastTrainingTick = Find.TickManager.TicksGame; // 更新时间戳
        }

        // 3. 任务【失败/中断】 -> 仅解锁，不进冷却 (允许立即重试)
        public void Notify_TrainingAborted()
        {
            isBeingTrained = false;
        }

        // =========================================================
        // 数据保存与加载
        // =========================================================
        public override void PostExposeData()
        {
            Scribe_Values.Look(ref mode, "mode", TrainingMode.Disabled);
            Scribe_Values.Look(ref lastTrainingTick, "lastTrainingTick", -999999);
            Scribe_Values.Look(ref isBeingTrained, "isBeingTrained", false);

            // 保存/读取 isEnabled，保证旧存档兼容性
            Scribe_Values.Look(ref isEnabled, "isEnabled", false);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // 1. 清理旧版本多余的模式
                if ((int)mode > 1) mode = TrainingMode.Disabled;

                // 2. 旧存档迁移 (如果布尔值是开的，但枚举没开)
                if (isEnabled && mode == TrainingMode.Disabled) mode = TrainingMode.Enabled;

                // 3. 同步
                isEnabled = (mode == TrainingMode.Enabled);
            }
        }

        // =========================================================
        // UI 与 交互
        // =========================================================

        private string GetIconPath()
        {
            string basePath = "UI/Icons/Toggle/";
            return mode == TrainingMode.Enabled ? basePath + "Icon_Enabled" : basePath + "Icon_Disabled";
        }

        private string GetLabel()
        {
            return mode == TrainingMode.Enabled ? Strings.Toggle_Label_Enabled : Strings.Toggle_Label_Disabled;
        }

        private string GetDescription()
        {
            return mode == TrainingMode.Enabled ? Strings.Toggle_Desc_Enabled : Strings.Toggle_Desc_Disabled;
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            // 🔥【关键修改】在这里进行性别判断 🔥
            // 将 parent 转为 Pawn 类型
            if (parent is Pawn p)
            {
                // 如果是男性，直接结束，不返回任何按钮
                if (p.gender == Gender.
)
                {
                    yield break;
                }
            }

            // 如果是女性(或无性别/其他)，则显示按钮
            yield return new Command_Action
            {
                defaultLabel = GetLabel(),
                defaultDesc = GetDescription(),
                icon = ContentFinder<Texture2D>.Get(GetIconPath(), true),
                action = CycleMode
            };
        }

        // 切换模式
        private void CycleMode()
        {
            // 1. 切换枚举
            mode = (TrainingMode)(((int)mode + 1) % 2);

            // 2. 同步布尔值
            isEnabled = (mode == TrainingMode.Enabled);

            // 3. 【手动保险】强制重置状态
            // 只要玩家点了一下开关，就说明想重置/改变状态，强制把“正在训练”取消。
            isBeingTrained = false;
        }
    }

    public class CompProperties_SimpleToggle : CompProperties
    {
        public CompProperties_SimpleToggle() => compClass = typeof(CompSimpleToggle);
    }
}*/