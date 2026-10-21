using System;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static Pawn FluidMaster(float amount)
    {
        Pawn source = ToggleMaster();
        source.health.hediffSet.hediffs.Remove(source.FluidOrgans[0]);
        source.FluidOrgans.Clear();
        AddFluidOrgan(source, amount);
        return source;
    }

    private static void RunFluidDoseTests()
    {
        // 原始极值在适配器读取处截断，执行真正聚合和真正 PNA 输入，不复制生产公式。
        foreach (var row in new[] { (0f,.03f),(-1f,.03f),(7.49f,.03f),(7.5f,.03f),(10f,.04f),
            (20f,.08f),(40f,.16f),(249f,.996f),(250f,1f),(1_000_000_000f,1f),
            (float.MaxValue,1f),(float.PositiveInfinity,1f),(float.NegativeInfinity,.03f),(float.NaN,.03f) })
            Run("侵染体液读取边界 " + row.Item1, () =>
            {
                var source=FluidMaster(row.Item1);
                Near(row.Item2,SSCMasterInfestationFluidUtility.CaptureDose(source));
                var target=InfestationTarget(); FinishInfestation(source,target);
                Near(row.Item2,SSCPnaExposureUtility.GetLoad(target));
            });
        Run("侵染无器官及空列表使用总量下限",()=>
        {
            var source=FluidMaster(0);source.FluidOrgans.Clear();
            Near(.03,SSCMasterInfestationFluidUtility.CaptureDose(source));
            source.FluidOrgans=null;Near(.03,SSCMasterInfestationFluidUtility.CaptureDose(source));
            Near(.03,SSCMasterInfestationFluidUtility.CaptureDose(null));
        });
        Run("侵染小器官合计后才补下限",()=>
        { var source=FluidMaster(5);AddFluidOrgan(source,5);Near(.04,SSCMasterInfestationFluidUtility.CaptureDose(source)); });
        Run("侵染同一缓存器官只读取一次",()=>
        {
            var source=FluidMaster(20);var organ=(rjw.FluidOrgan)source.FluidOrgans[0];source.FluidOrgans.Add(organ);
            Near(.08,SSCMasterInfestationFluidUtility.CaptureDose(source));Equal(1,organ.Comp.Reads);
        });
        foreach (float first in new[]{250f,1e9f,float.MaxValue,float.PositiveInfinity})
            Run("侵染饱和后不再读取后续器官 "+first,()=>
            {
                var source=FluidMaster(first);var next=AddFluidOrgan(source,20);next.Comp.ThrowRead=true;
                Near(1,SSCMasterInfestationFluidUtility.CaptureDose(source));Equal(0,next.Comp.Reads);
            });
        Run("侵染累加剩余额度前比较不会溢出",()=>
        {
            var source=FluidMaster(249);AddFluidOrgan(source,float.MaxValue);var next=AddFluidOrgan(source,20);
            Near(1,SSCMasterInfestationFluidUtility.CaptureDose(source));Equal(0,next.Comp.Reads);
        });
        foreach(string invalid in new[]{"detached","foreign","missing","noPart","notProducing","noFluid","noComp"})
            Run("侵染排除无效器官 "+invalid,()=>
            {
                var source=FluidMaster(250);var organ=(rjw.FluidOrgan)source.FluidOrgans[0];
                switch(invalid)
                {
                    case "detached":source.health.hediffSet.hediffs.Remove(organ);break;
                    case "foreign":organ.pawn=NewPawn();break;
                    case "missing":source.health.hediffSet.hediffs.Add(new Hediff_MissingPart{Part=organ.Part});break;
                    case "noPart":organ.Part=null;break;
                    case "notProducing":organ.Def.produceFluidOnOrgasm=false;break;
                    case "noFluid":organ.Comp.Fluid=null;break;
                    case "noComp":organ.Comp=null;break;
                }
                Near(.03,SSCMasterInfestationFluidUtility.CaptureDose(source));
            });
        Run("侵染单器官异常保留其他合法产量且不重复刷日志",()=>
        {
            var source=FluidMaster(250);((rjw.FluidOrgan)source.FluidOrgans[0]).Comp.ThrowRead=true;
            AddFluidOrgan(source,10);int count=Log.Warnings.Count;
            Near(.04,SSCMasterInfestationFluidUtility.CaptureDose(source));
            Near(.04,SSCMasterInfestationFluidUtility.CaptureDose(source));
            Assert(Log.Warnings.Count-count<=1,"兼容失败最多提醒一次");
        });
        Run("侵染整体读取异常保留下限",()=>
        {var source=FluidMaster(20);source.ThrowFluidList=true;Near(.03,SSCMasterInfestationFluidUtility.CaptureDose(source));});
        foreach(float amount in new[]{0f,10f,40f,250f})
            Run("侵染动态剂量不缩放需求成瘾或恶堕 "+amount,()=>
            {
                var source=FluidMaster(amount);var target=InfestationTarget();AddOldAddiction(target,.4f);target.needs.PnaNeed=.2f;
                FinishInfestation(source,target);
                Near(1,target.needs.PnaNeed.Value);Near(amount>=250?.62:.42,SSCPnaExposureUtility.GetAddiction(target).Severity);
                Near(.01,target.needs.CorruptionNeed.CurLevel);
            });
        Run("侵染高产首次顶满只按原规则建立成瘾",()=>
        {var source=FluidMaster(1e9f);var target=InfestationTarget();FinishInfestation(source,target);Near(1,SSCPnaExposureUtility.GetLoad(target));Near(1,SSCPnaExposureUtility.GetAddiction(target).Severity);});
        Run("侵染最低量仍优先普通产出者且只结算一次",()=>
        {var source=FluidMaster(0);AddPnaProducer(source);var target=InfestationTarget();FinishInfestation(source,target);Near(.03,SSCPnaExposureUtility.GetLoad(target));});
        Run("侵染最低量在已满身体仍沿用末期成瘾规则",()=>
        {var source=FluidMaster(0);var target=InfestationTarget();Raw(target,2);AddOldAddiction(target,.4f);FinishInfestation(source,target);Near(1,SSCPnaExposureUtility.GetLoad(target));Near(.62,SSCPnaExposureUtility.GetAddiction(target).Severity);});
        Run("侵染产量冻结在原结算前且重复分发不重读",()=>
        {
            var source=FluidMaster(20);var target=InfestationTarget();var organ=(rjw.FluidOrgan)source.FluidOrgans[0];
            using var context=SSCOrdinarySexOutcomeUtility.Capture(source,target,true,()=>true,()=>true);
            organ.Comp.Amount=250;Assert(context.TryClaim(),"合法认领");context.Dispatch();context.Dispatch();
            Near(.08,SSCPnaExposureUtility.GetLoad(target));Equal(1,organ.Comp.Reads);
        });
        Run("侵染无产量下限也走锁链保护分流",()=>
        {var source=FluidMaster(0);var target=InfestationTarget();Hediff_ChainOfSexSlave.AddToPawn(target,NewPawn());FinishInfestation(source,target);Near(.03,SSCPnaExposureUtility.GetLoad(target));Assert(SSCPnaExposureUtility.GetAddiction(target)==null,"不产生普通成瘾");});
        Run("侵染独立旧签名也使用动态剂量",()=>
        {var source=FluidMaster(40);var target=InfestationTarget();SSCPnaSexSupplyUtility.ApplyInfestation(source,target,()=>true);Near(.16,SSCPnaExposureUtility.GetLoad(target));});
    }
}
