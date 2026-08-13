using SexSlaveCraft;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace SexSlaveCraft
{
    // =========================================================
    // 1. 配方定义类：你的 XML 控制中心
    // =========================================================
    public class RecipeDef_PSTag : RecipeDef
    {
        // 恢复这三个字段，彻底交由 XML 控制
        public HediffDef hediffToAdd;
        public float severity = 1f;
        public bool filterIfTagExists = true;
        public bool requireBusSpecializationComplete = false;
        public SexSlaveSpecializationType requireSpecializationType = SexSlaveSpecializationType.None;
        public bool requireSpecializationComplete = false;
        public bool requirePersonalityExcretionCompleted = false;
        public List<HediffDef> exclusiveTags;
    }

    // =========================================================
    // 2. 配方工作者：负责搬运数据，并动态注入 XML 里定义的 Tag
    // =========================================================
    public class Recipe_PSTagWorker : RecipeWorker
    {
        public override void Notify_IterationCompleted(Pawn billDoer, List<Thing> ingredients)
        {
            Thing sourceItem = ingredients.FirstOrDefault(x => x.TryGetComp<CompPersonalityStore>() != null);
            if (sourceItem == null) return;

            CompPersonalityStore sourceComp = sourceItem.TryGetComp<CompPersonalityStore>();
            ThingDef targetDef = PersonalityGelUtility.GetEditedThingDef(sourceItem.def);

            if (targetDef == null) return;

            Thing newItem = ThingMaker.MakeThing(targetDef);
            CompPersonalityStore targetComp = newItem.TryGetComp<CompPersonalityStore>();

            if (targetComp != null)
            {
                // 继承原数据
                targetComp.CopyFrom(sourceComp);

                // 🔥 【核心注入逻辑】读取 XML 里的 hediffToAdd，动态打入雕像体内！
                if (recipe is RecipeDef_PSTag smartRecipe && smartRecipe.hediffToAdd != null)
                {
                    if (smartRecipe.hediffToAdd == SSCDefOf.SSC_Hediff_Bus_Final)
                    {
                        targetComp.RemoveTag(SSCDefOf.SSC_Hediff_Bus);
                    }

                    if (smartRecipe.hediffToAdd == SSCDefOf.SSC_Hediff_Cow_Final)
                    {
                        targetComp.RemoveTag(SSCDefOf.SSC_Hediff_Cow);
                    }

                    HediffDef petBaseTag = PetSpecializationUtility.GetBaseHediffForFinal(smartRecipe.hediffToAdd);
                    if (petBaseTag != null)
                    {
                        targetComp.RemoveTag(petBaseTag);
                    }

                    if (smartRecipe.exclusiveTags != null)
                    {
                        foreach (HediffDef exclusiveTag in smartRecipe.exclusiveTags)
                        {
                            if (exclusiveTag != null)
                            {
                                targetComp.RemoveTag(exclusiveTag);
                            }
                        }
                    }

                    // 使用你之前写好的 SetTag 方法
                    targetComp.SetTag(smartRecipe.hediffToAdd, smartRecipe.severity);
                }
            }

            if (!sourceItem.Destroyed) sourceItem.Destroy(DestroyMode.Vanish);
            GenPlace.TryPlaceThing(newItem, billDoer.Position, billDoer.Map, ThingPlaceMode.Near);
        }
    }
}
