// 仅模拟本次核对过的 RimWorld 1.6 结算时序，不模拟加工寻路、渲染或完整账单系统。
using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Verse
{
    public class ThingCountClass
    {
        public Thing thing;
        public int Count;
        public ThingCountClass(Thing thing, int count) { this.thing = thing; Count = count; }
    }

    public struct LocalTargetInfo
    {
        public Thing Thing;
        public LocalTargetInfo(Thing thing) { Thing = thing; }
    }

    public class UnfinishedThing : Thing
    {
        public List<Thing> ingredients;
    }
}

namespace RimWorld
{
    public class Bill_Production
    {
        public RecipeDef recipe;
        public int repeatCount = 1;
        public int completedIterations;

        public void Notify_IterationCompleted(Pawn actor, List<Thing> ingredients)
        {
            // 对应真实 Bill_Production：先扣重复次数，再通知配方工作者。
            // 保留这一顺序，防止工作者内部提前返回被误判成“账单未计完成”。
            repeatCount--;
            completedIterations++;
            recipe.Worker.Notify_IterationCompleted(actor, ingredients);
        }
    }
}

namespace Verse.AI
{
    public enum TargetIndex { A, B, C }
    public enum JobCondition { Succeeded, Incompletable }

    public class Job
    {
        public Bill_Production bill;
        public RecipeDef RecipeDef => bill?.recipe;
        public List<ThingCountClass> placedThings;
        public LocalTargetInfo targetC;
        public LocalTargetInfo GetTarget(TargetIndex index) => index == TargetIndex.C ? targetC : default;
    }

    public class Pawn_JobTracker
    {
        public Job curJob;
        public JobCondition? endedWith;
        public void EndCurrentJob(JobCondition condition) { endedWith = condition; curJob = null; }
    }

    public class Toil
    {
        public Pawn actor;
        public Action initAction;
    }

    public static class Toils_Recipe
    {
        public static int OriginalCompletionCalls;
        public static List<string> CompletionOrder = new List<string>();

        /// <summary>保留实际最终动作中的有副作用顺序，交由真实 SSC 补丁决定是否允许执行。</summary>
        public static Toil FinishRecipeAndStartStoringProduct(TargetIndex index)
        {
            var toil = new Toil();
            toil.initAction = () =>
            {
                OriginalCompletionCalls++;
                Job job = toil.actor.CurJob;
                RecipeWorker worker = job.RecipeDef.Worker;
                List<Thing> ingredients;

                // CalculateIngredients 会优先取半成品内原料并消耗半成品；
                // 普通路径取 placedThings 后清空其记录。本替身不模拟可堆叠物品的分堆。
                CompletionOrder.Add("CalculateIngredients");
                if (job.GetTarget(TargetIndex.C).Thing is UnfinishedThing unfinished)
                {
                    ingredients = unfinished.ingredients;
                    worker.ConsumeIngredient(unfinished, job.RecipeDef, toil.actor.Map);
                }
                else
                {
                    ingredients = job.placedThings.Where(x => x.Count > 0).Select(x => x.thing).ToList();
                    foreach (ThingCountClass entry in job.placedThings) entry.Count = 0;
                }
                job.placedThings = null;

                // 原版先生成 XML 产物，再销毁材料，随后通知账单；本配方 XML 无产物，
                // 人格产物由链接的 Recipe_PSTagWorker 在完成回调中实际创建。
                CompletionOrder.Add("MakeRecipeProducts");
                CompletionOrder.Add("ConsumeIngredients");
                foreach (Thing ingredient in ingredients) worker.ConsumeIngredient(ingredient, job.RecipeDef, toil.actor.Map);
                CompletionOrder.Add("NotifyBillCompleted");
                job.bill.Notify_IterationCompleted(toil.actor, ingredients);
            };
            return toil;
        }
    }
}
