using RimWorld;
using rjw;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;

/*namespace SexSlaveCraft
{
    public class JobDriver_TrainingReceiver : JobDriver_SexBaseRecieverLoved
    {
        // 核心逻辑：定义这一帧该干什么
        protected override IEnumerable<Toil> MakeNewToils()
        {
            // 1. 初始化
            DoSetup();

            // 2. 预定调教员
            // (因为 JobDriver_Training 里释放了预定，所以这一步会成功)
            yield return Toils_Reserve.Reserve(TargetIndex.A, 1, 0);

            // 3. 执行性爱过程
            Toil sexToil = CreateSimpleSexToil();
            yield return sexToil;
        }

        // ================================================================
        // 手写一个干净的 SexToil (修复了报错的版本)
        // ================================================================
        private Toil CreateSimpleSexToil()
        {
            Toil toil = new Toil();

            // 只要 Initiator (调教员) 不结束，我就一直做下去
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            toil.socialMode = RandomSocialMode.Off;

            // 设置为 false，让调教员控制朝向
            toil.handlingFacing = false;

            // 每帧执行
            toil.tickAction = delegate
            {
                // 【修复 IsHashIntervalTick 报错】
                // 直接使用底层算法：(当前游戏Tick + 自身Hash偏移) % 间隔 == 0
                // 这样无论哪个版本都绝对能运行
                if ((Find.TickManager.TicksGame + pawn.HashOffset()) % ticks_between_hearts == 0)
                {
                    ThrowMetaIconF(pawn.Position, pawn.Map, FleckDefOf.Heart);
                }
            };

            // 结束时的清理工作
            toil.AddFinishAction(delegate
            {
                if (xxx.is_human(pawn))
                {
                    CompRJW compRJW = pawn.GetCompRJW();
                    if (compRJW != null)
                    {
                        compRJW.drawNude = false;
                        pawn.Drawer.renderer.SetAllGraphicsDirty();
                    }
                }

                // 【修复 GlobalTextureAtlasManager 报错】
                // 删除了那个不可访问的类，改用标准的头像缓存刷新
                try
                {
                    PortraitsCache.SetDirty(pawn);
                }
                catch { }
            });

            return toil;
        }
    }
}*/