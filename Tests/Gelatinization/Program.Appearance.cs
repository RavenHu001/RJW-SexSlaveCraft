// AI 辅助生成。直接链接生产配置、草稿、弹窗和材质代码；UI/存档宿主不代表实机验收。
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using FacialAnimation;
using SexSlaveCraft;
using UnityEngine;
using Verse;

internal static partial class Program
{
    // _Color 现在只携带游戏调制与源Alpha，皮肤参数从独立uniform读取。
    private static Color GelTint(Material material)
    { Color value=material.GetColor("_GelColor");value.a*=material.color.a;return value; }

    /// <summary>建立真实生产外观组件与完成状态，避免只用FullGel布尔值掩盖身体身份隔离。</summary>
    private static HediffComp_FullGelAppearance AppearanceOwner(Pawn pawn)
    {
        pawn.FullGel=true;
        return FullGelAppearanceUtility.GetOwner(pawn).TryGetComp<HediffComp_FullGelAppearance>();
    }
    /// <summary>经生产最终材质入口渲染原版身体，模型不请求Graphic数据库。</summary>
    private static Material AppearanceBody(Pawn pawn,Material source)
    {
        Material result=source;
        var node=new PawnRenderNode_Body {tree=new PawnRenderTree {pawn=pawn}};
        Harmony_FullGelatinizationHeadTint.MaterialPostfix(node,new PawnDrawParms {pawn=pawn},ref result);return result;
    }
    private static Material AppearanceSource()=>new Material(new Shader("Body-original")){mainTexture=new Texture("SharedBody")};
    private static FullGelAppearance AppearanceValue(float red=.1f,float green=.7f,float blue=.3f,float alpha=.4f)=>new FullGelAppearance(new Color(red,green,blue,1),alpha);

    private static void AppearanceTests(string root)
    {
        Run("默认HSV饱和度90且保留原色相明度和透明度",()=>
        {
            // 独立按RGB极值计算HSV，不以同一个默认常量与自身比较证明转换正确。
            Color c=FullGelAppearanceUtility.Default.Color;
            float max=Math.Max(c.r,Math.Max(c.g,c.b)),min=Math.Min(c.r,Math.Min(c.g,c.b));
            float saturation=(max-min)/max,hue=(4+(c.r-c.g)/(max-min))*60;
            Check(Math.Abs(saturation-.9f)<1e-6f&&Math.Abs(hue-272.5f)<1e-4f&&max==.68f&&c.a==.85f,"只改变默认S");
        });
        Run("默认S90不会覆盖明确保存的旧自定义配色",()=>
        {
            // 保留上一轮实际RGB作为旧档数据，不能替换成当前默认再做往返测试。
            var a=AppearanceOwner(new Pawn());a.Set(new FullGelAppearance(new Color(.46f,.20f,.68f,1),.85f),false);
            Scribe_Values.Data.Clear();Scribe_Values.Mode=1;try{a.CompExposeData();}finally{Scribe_Values.Mode=0;}
            var b=AppearanceOwner(new Pawn());Scribe_Values.Mode=2;try{b.CompExposeData();}finally{Scribe_Values.Mode=0;}
            Check(b.Custom&&b.Appearance.Color.r==.46f&&b.Appearance.Color.g==.20f&&!b.Appearance.Same(FullGelAppearanceUtility.Default),"不迁移自定义值，恢复默认由玩家显式触发");
        });
        // 配置生命周期：默认/非法数值/读写/替换状态都执行生产数据逻辑。
        Run("个体外观旧状态缺组件仍用已验收默认值",()=>{var p=new Pawn();var c=AppearanceOwner(p);c.parent.comps.Clear();Check(FullGelAppearanceUtility.Read(c.parent).Same(FullGelAppearanceUtility.Default),"缺组件默认");});
        Run("新外观组件使用S90深紫和85%",()=>{var c=AppearanceOwner(new Pawn());Check(!c.Custom&&c.Appearance.Color.r==FullGelAppearanceUtility.DefaultRed&&c.Appearance.Color.a==.85f,"无自定义标记时读取统一默认值");});
        Run("配置归一化拒绝NaN无穷并限制越界",()=>{var a=AppearanceValue(float.NaN,-1,4,float.PositiveInfinity);Check(a.Color.r==FullGelAppearanceUtility.DefaultRed&&a.Color.g==0&&a.Color.b==1&&a.Color.a==.85f,"非法数据安全");});
        Run("保存和读取模型保留个体配置",AppearanceSaveLoad);
        Run("缺字段的旧档恢复当前默认配置",()=>{var c=AppearanceOwner(new Pawn());c.Set(AppearanceValue(),false);Scribe_Values.Data.Clear();Scribe_Values.Mode=2;try{c.CompExposeData();}finally{Scribe_Values.Mode=0;}Check(!c.Custom&&c.Appearance.Same(FullGelAppearanceUtility.Default),"缺字段默认");});
        Run("移除状态后原组件不再提供指令",()=>{var p=new Pawn();var c=AppearanceOwner(p);p.health.RemoveHediff(c.parent);Check(!c.CompGetGizmos().Any(),"移除即失效");});
        Run("重新获得状态使用独立默认外观",()=>{var p=new Pawn();var c=AppearanceOwner(p);c.Set(AppearanceValue(),false);p.health.RemoveHediff(c.parent);Check(AppearanceOwner(p).Appearance.Same(FullGelAppearanceUtility.Default),"新身体不继承旧值");});
        Run("普通与开发模式均不提供额外命令栏调色按钮",()=>
        {
            var c=AppearanceOwner(new Pawn());bool previous=Prefs.DevMode;
            try{foreach(bool dev in new[]{false,true}){Prefs.DevMode=dev;Check(!c.CompGetGizmos().Any(),"不添加重复调色指令");}}
            finally{Prefs.DevMode=previous;}
        });
        Run("个人渲染关闭后仍不提供命令栏调色按钮",()=>
        {
            var c=AppearanceOwner(new Pawn());c.SetRenderingEnabled(false);
            Check(!c.CompGetGizmos().Any(),"关闭状态同样只保留ITab外观入口");
        });
        Run("关闭个人渲染仍能保存个体外观",()=>{var p=new Pawn();var c=AppearanceOwner(p);c.SetRenderingEnabled(false);Check(FullGelAppearanceUtility.Apply(p,c.parent,AppearanceValue(),false)&&c.Custom,"关闭不删除配置");Check(AppearanceBody(p,AppearanceSource()).shader.name=="Body-original","关闭不渲染");});
        Run("旧窗口不能写入替换后的身体",()=>{var p=new Pawn();var c=AppearanceOwner(p);p.health.RemoveHediff(c.parent);var next=AppearanceOwner(p);Check(!FullGelAppearanceUtility.Apply(p,c.parent,AppearanceValue(),false)&&!next.Custom,"精确身体身份");});
        Run("应用真实变化只刷新地图头像一次",()=>{var p=new Pawn();var c=AppearanceOwner(p);int dirty=RimWorld.PortraitsCache.Dirty;Check(FullGelAppearanceUtility.Apply(p,c.parent,AppearanceValue(),false),"应用成功");Check(p.Drawer.renderer.Dirty==1&&RimWorld.PortraitsCache.Dirty==dirty+1,"地图与肖像刷新");FullGelAppearanceUtility.Apply(p,c.parent,AppearanceValue(),false);Check(p.Drawer.renderer.Dirty==1,"同值不重建");});
        Run("应用恢复默认清除自定义并恢复85%",()=>{var p=new Pawn();var c=AppearanceOwner(p);c.Set(AppearanceValue(),false);FullGelAppearanceUtility.Apply(p,c.parent,AppearanceValue(),true);Check(!c.Custom&&c.Appearance.Same(FullGelAppearanceUtility.Default),"默认精确还原");});
        Run("配置应用只允许主线程",()=>{var p=new Pawn();var c=AppearanceOwner(p);UnityData.IsInMainThread=false;try{Check(!FullGelAppearanceUtility.Apply(p,c.parent,AppearanceValue(),false)&&!c.Custom,"工作线程不写配置");}finally{UnityData.IsInMainThread=true;}});
        Run("克隆外观按值复制后独立编辑",()=>{var a=AppearanceOwner(new Pawn());var b=AppearanceOwner(new Pawn());a.Set(AppearanceValue(),false);FullGelAppearanceUtility.Copy(a.parent,b.parent);Check(b.Custom&&b.Appearance.Same(a.Appearance),"复制数值");b.Set(AppearanceValue(1,0,0,1),false);Check(a.Appearance.Color.r==.1f,"不共享可变对象");});

        // UI模型只替代原版控件边界；草稿、按钮及Apply路径均链接生产类。
        Run("草稿颜色控件重设Alpha不改变不透明度",()=>{var d=new FullGelAppearanceDraft(FullGelAppearanceUtility.Default);d.Rgb=new Color(1,0,0,1);Check(d.Value.Color.a==.85f,"Alpha独立");});
        Run("HEX只接受六位RGB且不改变透明度",()=>{var d=new FullGelAppearanceDraft(FullGelAppearanceUtility.Default);Check(d.SetHex("#00FF00")&&d.Value.Color.g==1&&d.Value.Color.a==.85f,"合法颜色");foreach(string s in new[]{"bad","#ABC","#FF000080","#GGGGGG",""})Check(!d.SetHex(s),"拒绝非法/混入Alpha");});
        Run("草稿恢复默认不立即写入角色",()=>{var c=AppearanceOwner(new Pawn());c.Set(AppearanceValue(),false);var d=new FullGelAppearanceDraft(c.Appearance);d.Reset();Check(d.ResetRequested&&d.Value.Same(FullGelAppearanceUtility.Default)&&c.Appearance.Color.r==.1f,"恢复仅草稿");});
        Run("弹窗复用原版色轮数值色板滑块",AppearanceWidgets);
        Run("弹窗取消丢弃RGB与不透明度草稿",()=>AppearanceWindow("Cancel"));
        Run("弹窗确认后同时应用RGB与不透明度",()=>AppearanceWindow("Accept"));
        Run("弹窗恢复默认按钮经确认生效",AppearanceWindowReset);
        Run("弹窗非法HEX阻止应用",AppearanceWindowInvalid);
        // 缓存隔离与性能：固定源对象，区分个体配置变化与源材质变化。
        Run("同贴图多人配置使用隔离材质",AppearanceIsolation);
        Run("个体颜色修改会使稳定源缓存刷新",AppearanceRefresh);
        Run("连续调色复用当前材质而非累积历史变体",AppearanceHistory);
        Run("个体材质稳定一万次不复制不分配",()=>{var p=new Pawn();AppearanceOwner(p).Set(AppearanceValue(),false);var source=AppearanceSource();var node=new PawnRenderNode_Body {tree=new PawnRenderTree {pawn=p}};var parms=new PawnDrawParms {pawn=p};StableCalls(()=>{var m=source;Harmony_FullGelatinizationHeadTint.MaterialPostfix(node,parms,ref m);return m;});});
        Run("共享源材质并行渲染不同个体不串色",AppearanceParallel);
        Run("原版身体最终材质保留源Alpha并只乘一次",()=>{var p=new Pawn();AppearanceOwner(p).Set(AppearanceValue(),false);var source=AppearanceSource();source.color=new Color(.2f,.4f,.6f,.5f);var m=AppearanceBody(p,source);Check(GelTint(m).a==.2f&&m.color.r==1&&source.color.r==.2f,"源Alpha与个体Alpha相乘一次");});
        Run("SA服装预缓存最终阶段读取个体颜色",AppearanceApparel);
        Run("新旧默认参数的像素输出保持一致",AppearanceDefaultPixels);
        // 颜色通道与Alpha分别选边界，保留五色及完全透明／低透明／默认／不透明，不展开二者全交叉。
        foreach(var (color,alpha) in new[]
        {
            (new Color(1,0,0,1),0f), (new Color(1,0,0,1),.01f),
            (new Color(1,0,0,1),.85f), (new Color(1,0,0,1),1f),
            (new Color(0,1,0,1),.85f), (new Color(0,0,1,1),.85f),
            (new Color(0,0,0,1),.85f), (new Color(1,1,1,1),.85f)
        })
        {var captured=color;float opacity=alpha;Run($"任意RGB({color.r},{color.g},{color.b})与Alpha{alpha}保眼和动画",()=>AppearanceChannels(captured,opacity));}
    }

    /// <summary>调用生产CompExposeData往返，Scribe边界只模拟字段存取，不等同实机存档验收。</summary>
    private static void AppearanceSaveLoad()
    {
        var a=AppearanceOwner(new Pawn());a.Set(AppearanceValue(),false);Scribe_Values.Data.Clear();Scribe_Values.Mode=1;
        try{a.CompExposeData();}finally{Scribe_Values.Mode=0;}
        var b=AppearanceOwner(new Pawn());Scribe_Values.Mode=2;try{b.CompExposeData();}finally{Scribe_Values.Mode=0;}
        Check(a.Appearance.Same(b.Appearance)&&b.Custom,"生产ExposeData读写同一字段");
    }
    /// <summary>计数原版控件边界调用，确认仅打开窗口不保存、不刷新地图。</summary>
    private static void AppearanceWidgets()
    {
        var p=new Pawn();var c=AppearanceOwner(p);int wheel=Widgets.Wheels,fields=Widgets.Fields,selector=Widgets.Selectors,slider=Widgets.Sliders;
        var w=new Dialog_FullGelAppearance(p,c.parent);w.DoWindowContents(new Rect(0,0,640,580));
        Check(Widgets.Wheels==wheel+1&&Widgets.Fields==fields+1&&Widgets.Selectors==selector+1&&Widgets.Sliders==slider+2,"实际调用原版边界");Check(!c.Custom&&p.Drawer.renderer.Dirty==0,"只看窗口不写存档/刷新图形");
    }
    /// <summary>以脚本化控件输入走生产窗口两轮GUI事件，检查取消与延迟确认。</summary>
    private static void AppearanceWindow(string button)
    {
        var p=new Pawn();var c=AppearanceOwner(p);var w=new Dialog_FullGelAppearance(p,c.parent);
        Widgets.Rgb=new Color(1,0,0,1);Widgets.Slider=25;Widgets.Buttons.Add(button);w.DoWindowContents(new Rect(0,0,640,580));
        if(button=="Cancel"){Check(w.Closed&&!c.Custom,"取消不保存");return;}
        Check(!c.Custom&&!w.Closed,"等待延迟输入提交");w.DoWindowContents(new Rect(0,0,640,580));
        Check(w.Closed&&c.Custom&&c.Appearance.Color.r==1&&c.Appearance.Color.a==.25f,"确认后统一写回");
    }
    /// <summary>恢复默认先改草稿；确认前检查原身体自定义标记仍在。</summary>
    private static void AppearanceWindowReset()
    {
        var p=new Pawn();var c=AppearanceOwner(p);c.Set(AppearanceValue(),false);var w=new Dialog_FullGelAppearance(p,c.parent);
        Widgets.Buttons.Add("SSC_GelAppearance_Reset");w.DoWindowContents(new Rect(0,0,640,580));Check(c.Custom,"恢复按钮先改草稿");
        Widgets.Buttons.Add("Accept");w.DoWindowContents(new Rect(0,0,640,580));w.DoWindowContents(new Rect(0,0,640,580));Check(w.Closed&&!c.Custom,"确认恢复默认");
    }
    /// <summary>非法HEX时确认保持禁用，避免保存半输入或错误颜色。</summary>
    private static void AppearanceWindowInvalid()
    {
        var p=new Pawn();var c=AppearanceOwner(p);var w=new Dialog_FullGelAppearance(p,c.parent);Widgets.Hex="#INVALID";Widgets.Buttons.Add("Accept");
        w.DoWindowContents(new Rect(0,0,640,580));w.DoWindowContents(new Rect(0,0,640,580));Check(!w.Closed&&!c.Custom,"错误输入不能写回");Widgets.Buttons.Clear();
    }
    /// <summary>交错渲染同源贴图的两个身体，核对副本身份和已返回的颜色都不串用。</summary>
    private static void AppearanceIsolation()
    {
        var a=new Pawn();var b=new Pawn();AppearanceOwner(a).Set(AppearanceValue(1,0,0,.2f),false);AppearanceOwner(b).Set(AppearanceValue(0,0,1,.9f),false);
        var source=AppearanceSource();var first=AppearanceBody(a,source);var second=AppearanceBody(b,source);
        Check(first!=second&&GelTint(first).r==1&&GelTint(first).a==.2f&&GelTint(second).b==1&&GelTint(second).a==.9f,"不串色");
        Check(AppearanceBody(a,source)==first&&GelTint(first).a==.2f&&source.color.a==1,"交错调用不改已有结果");
    }
    /// <summary>只改个体配置而不改源材质，验证有效RGBA参与缓存失效。</summary>
    private static void AppearanceRefresh()
    {
        var p=new Pawn();var c=AppearanceOwner(p);var source=AppearanceSource();var m=AppearanceBody(p,source);int copies=Material.Copies;
        c.Set(AppearanceValue(),false);Check(AppearanceBody(p,source)==m&&GelTint(m).g==.7f&&Material.Copies==copies+1,"只在配置变化时复制");
        int stable=Material.Copies;AppearanceBody(p,source);Check(Material.Copies==stable,"恢复稳定路径");
    }
    /// <summary>连续应用不同颜色仍复用同一个材质对象，防止滑块历史累积。</summary>
    private static void AppearanceHistory()
    {
        var p=new Pawn();var c=AppearanceOwner(p);var source=AppearanceSource();var first=AppearanceBody(p,source);int n=Material.Created;
        for(int i=0;i<200;i++){c.Set(AppearanceValue(i/199f,.2f,.5f,i/199f),false);Check(AppearanceBody(p,source)==first,"复用当前副本");}
        Check(Material.Created==n,"不按滑块历史颜色永久建材质");
    }
    /// <summary>并行读取两个身体的独立快照，不以串行测试代替线程隔离。</summary>
    private static void AppearanceParallel()
    {
        var a=new Pawn();var b=new Pawn();AppearanceOwner(a).Set(AppearanceValue(1,0,0,.2f),false);AppearanceOwner(b).Set(AppearanceValue(0,1,0,.8f),false);var source=AppearanceSource();
        Parallel.For(0,128,i=>{var m=AppearanceBody(i%2==0?a:b,source);Check(i%2==0?GelTint(m).r==1&&GelTint(m).a==.2f:GelTint(m).g==1&&GelTint(m).a==.8f,"并行参数隔离");});
    }
    /// <summary>模拟旧SA服装的主线程预缓存，再走最终材质入口，检查不漏uniform。</summary>
    private static void AppearanceApparel()
    {
        var p=new Pawn();AppearanceOwner(p).Set(AppearanceValue(),false);var apparel=new RimWorld.Apparel {Wearer=p};
        var rec=new RimWorld.ApparelGraphicRecord(Original("SizedApparel/Breast"),apparel);
        Harmony_FullGelatinizationBodyTint.SizedApparelApparelPostfix(apparel,ref rec,true);
        var source=new Material(rec.graphic.shader){mainTexture=new Texture("Apparel"),color=rec.graphic.color};
        Material m=source;Harmony_FullGelatinizationHeadTint.MaterialPostfix(new PawnRenderNode_Apparel(),new PawnDrawParms {pawn=p},ref m);
        Check(GelTint(m).g==.7f&&GelTint(m).a==.4f,"旧服装入口不漏个体uniform");
    }
    /// <summary>比较参数分离前后相同RGBA的像素公式，检查默认调整不改变混合算法。</summary>
    private static void AppearanceDefaultPixels()
    {
        var a=FullGelAppearanceUtility.Default.Color;
        foreach(float source in new[]{0f,.2f,.65f,1f})foreach(float draw in new[]{0f,.25f,1f,1.5f})
        {
            Check(GelShaderMath.sscGelChannel(source,draw*a.r)==GelShaderMath.sscGelChannel(source,a.r*draw),"默认RGB变换保持原式");
            Check(Math.Abs(GelShaderMath.sscGelAlpha(.7f,(draw*.6f)*a.a,1)-GelShaderMath.sscGelAlpha(.7f,(a.a*.6f)*draw,1))<1e-6f,"默认Alpha等效");
        }
    }
    /// <summary>执行同一生产片元标量函数，核对皮肤参数和五官通道分离。</summary>
    private static void AppearanceChannels(Color color,float opacity)
    {
        var r=EyeRig();AppearanceOwner(r.parms.pawn).Set(new FullGelAppearance(color,opacity),false);var m=r.Apply(new PawnRenderNode_Head());
        Check(m.GetColor("_GelColor").r==color.r&&GelTint(m).a==opacity&&m.color.r==1&&m.color.a==1,"参数独立保留零通道");
        foreach(float tint in new[]{0f,.25f,1f})
        {
            float eye=GelShaderMath.sscGelOriginalChannel(.8f,.5f,tint);
            Check(Math.Abs(GelShaderMath.sscGelEyeBlend(.1f,eye,1)-.4f*tint)<1e-6f,"原眼睛不被凝胶色或透明度调制");
            Check(GelShaderMath.sscGelEyeBlend(GelShaderMath.sscGelAlpha(1,opacity*tint,1),GelShaderMath.sscGelAlpha(1,tint,1),1)==tint,"眼睛Alpha仍响应游戏淡出");
        }
        var skin=r.Apply(Fa(new HeadControllerComp()));Check(skin.GetColor("_GelColor").r==color.r&&skin.GetTexture("_EyeMask")==null,"FA皮肤统一参数");
        var feature=r.Apply(Fa(new EyeballControllerComp()));Check(feature.color.r==r.source.color.r&&feature.color.a==1&&feature.shader==r.source.shader,"FA五官保留原动画材质");
    }
}
