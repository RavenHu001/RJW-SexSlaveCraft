// AI 辅助生成。调用生产外观组件、准入、ITab区块与控件包装；引擎边界不替代实机验收。
using System;
using System.IO;
using System.Linq;
using SexSlaveCraft;
using UnityEngine;
using Verse;

internal static partial class Program
{
    private static void SaveAppearance(HediffComp_FullGelAppearance comp)
    {
        Scribe_Values.Data.Clear(); Scribe_Values.Mode=1;
        try { comp.CompExposeData(); } finally { Scribe_Values.Mode=0; }
    }

    private static void LoadAppearance(HediffComp_FullGelAppearance comp)
    {
        Scribe_Values.Mode=2;
        try { comp.CompExposeData(); } finally { Scribe_Values.Mode=0; }
    }

    private static void PersonalRenderingTests(string root)
    {
        foreach(bool legacy in new[]{false,true})
        foreach(bool saved in new[]{false,true})
        {
            bool old=legacy, value=saved;
            Run($"个人开关{saved}优先旧全局{legacy}",()=>
            {
                SSCMod.settings.enableFullGelatinizedBodyTint=old;
                var c=AppearanceOwner(new Pawn());c.SetRenderingEnabled(value);SaveAppearance(c);
                Check(Scribe_Values.Data.ContainsKey("sscGelRenderingEnabled"),"默认值也强制保存个人字段");
                SSCMod.settings.enableFullGelatinizedBodyTint=!old;
                var loaded=AppearanceOwner(new Pawn());LoadAppearance(loaded);
                Check(loaded.RenderingEnabled==value,"独立个人值不再受旧设置控制");
            });
        }
        foreach(bool legacy in new[]{false,true})
        {
            bool old=legacy;
            Run($"缺字段旧档仅一次继承全局{legacy}",()=>
            {
                SSCMod.settings.enableFullGelatinizedBodyTint=old;
                Scribe_Values.Data.Clear();var c=AppearanceOwner(new Pawn());LoadAppearance(c);
                Check(c.RenderingEnabled==old,"缺字段继承旧选择");SaveAppearance(c);
                SSCMod.settings.enableFullGelatinizedBodyTint=!old;LoadAppearance(c);
                Check(c.RenderingEnabled==old,"后续读档不重复迁移");
            });
        }
        Run("新身体默认开启且不读取旧全局运行值",()=>
        {
            SSCMod.settings.enableFullGelatinizedBodyTint=false;
            var p=new Pawn();var c=AppearanceOwner(p);
            Check(c.RenderingEnabled&&FullGelAppearanceUtility.ShouldRender(p),"旧设置只用于读档");
            SSCMod.settings=null;Check(FullGelAppearanceUtility.ShouldRender(p),"不依赖静态设置初始化");
        });
        Run("个人开关只刷新目标且同值不重建",()=>
        {
            var a=new Pawn();var ca=AppearanceOwner(a);var b=new Pawn();AppearanceOwner(b);
            int portraits=RimWorld.PortraitsCache.Dirty;
            Check(FullGelAppearanceUtility.SetRenderingEnabled(a,ca.parent,false),"关闭成功");
            FullGelAppearanceUtility.SetRenderingEnabled(a,ca.parent,false);
            Check(a.Drawer.renderer.Dirty==1&&b.Drawer.renderer.Dirty==0&&RimWorld.PortraitsCache.Dirty==portraits+1,"局部刷新一次");
            Check(!FullGelAppearanceUtility.ShouldRender(a)&&FullGelAppearanceUtility.ShouldRender(b),"两人互不影响");
        });
        Run("关闭再调色恢复默认不改变个人开关",()=>
        {
            var p=new Pawn();var c=AppearanceOwner(p);c.SetRenderingEnabled(false);
            FullGelAppearanceUtility.Apply(p,c.parent,AppearanceValue(),false);
            Check(!c.RenderingEnabled&&c.Custom,"改色保留关闭");
            FullGelAppearanceUtility.Apply(p,c.parent,AppearanceValue(),true);
            Check(!c.RenderingEnabled&&!c.Custom&&c.Appearance.Color.a==.85f,"默认配色与开关独立");
        });
        Run("克隆复制个人开关后独立变化",()=>
        {
            var a=AppearanceOwner(new Pawn());var b=AppearanceOwner(new Pawn());a.SetRenderingEnabled(false);
            FullGelAppearanceUtility.Copy(a.parent,b.parent);Check(!b.RenderingEnabled,"复制关闭状态");
            b.SetRenderingEnabled(true);Check(!a.RenderingEnabled,"复制后各自独立");
        });
        Run("移除重获Hediff重新默认且旧开关回调失效",()=>
        {
            var p=new Pawn();var c=AppearanceOwner(p);c.SetRenderingEnabled(false);p.health.RemoveHediff(c.parent);
            Check(!FullGelAppearanceUtility.ShouldRender(p),"无状态不渲染");var next=AppearanceOwner(p);
            Check(next.RenderingEnabled&&!FullGelAppearanceUtility.SetRenderingEnabled(p,c.parent,false),"旧回调不修改新身体");
        });
        Run("后台线程不能切换个人开关",()=>
        {
            var p=new Pawn();var c=AppearanceOwner(p);UnityData.IsInMainThread=false;
            try{Check(!FullGelAppearanceUtility.SetRenderingEnabled(p,c.parent,false)&&c.RenderingEnabled,"线程保护");}
            finally{UnityData.IsInMainThread=true;}
        });
        Run("关闭退出原版FA五官和SA的凝胶绘制阶段",PersonalRenderingLayers);
        Run("ITab只对完成Hediff出现且关闭仍能调色",PersonalRenderingTab);
        Run("ITab快照不会操作替换后的身体",()=>
        {
            var p=new Pawn();var old=AppearanceOwner(p);var draw=ITab_SexSlaveTraining.CaptureGel(p,320);
            p.health.RemoveHediff(old.parent);var next=AppearanceOwner(p);Widgets.Toggle=true;draw();Widgets.Toggle=false;
            Check(next.RenderingEnabled,"失效面板点击被拒绝");
        });
        Run("禁用按钮显式置灰并拒绝意外点击",()=>
        {
            Widgets.Drawn.Clear();Widgets.IgnoreButtonDisabled=true;Widgets.Buttons.Add("locked");
            try{Check(!SSCUIWidgets.Button(new Rect(0,0,200,30),"locked",false),"显式点击保护");}
            finally{Widgets.IgnoreButtonDisabled=false;}
            var draw=Widgets.Drawn.Single();Check(draw.Disabled&&draw.Color.r==.55f&&GUI.enabled&&GUI.color.r==1,"置灰及GUI恢复");
        });
        Run("禁用复选框显式传disabled并保留原因提示",()=>
        {
            Widgets.Drawn.Clear();int tips=TooltipHandler.Tips;Widgets.Toggle=true;
            Check(!SSCUIWidgets.Checkbox(new Rect(0,0,250,30),"trainer",false,false,"reason"),"不可开启");
            Widgets.Toggle=false;var draw=Widgets.Drawn.Single();
            Check(draw.Disabled&&draw.Color.r==.55f&&tips+1==TooltipHandler.Tips,"原版disabled及置灰");
        });
        Run("父GUI已禁用时任职与外观开关均不可操作",()=>
        {
            GUI.enabled=false;Widgets.Toggle=true;
            try{Check(SSCUIWidgets.Checkbox(new Rect(0,0,250,30),"trainer",true,true),"保留保存值");Check(!GUI.enabled,"保留父级禁用");}
            finally{GUI.enabled=true;Widgets.Toggle=false;}
        });
        Run("控件异常也恢复GUI颜色与输入状态",()=>
        {
            Widgets.ThrowCheckbox=true;
            try{SSCUIWidgets.Checkbox(new Rect(0,0,200,30),"trainer",false,false);}
            catch(InvalidOperationException){}
            finally{Widgets.ThrowCheckbox=false;}
            Check(GUI.enabled&&GUI.color.r==1,"finally恢复");
        });
        foreach(float width in new[]{300f,480f,640f})
        foreach(float row in new[]{102f,192f})
        {
            float w=width,h=row;
            Run($"外观布局宽{w}分量高{h}不重叠",()=>PersonalRenderingLayout(w,h));
        }
        Run("外观弹窗RGBHSV切换仅显示三个分量",()=>
        {
            var p=new Pawn();var c=AppearanceOwner(p);var window=new Dialog_FullGelAppearance(p,c.parent);
            Widgets.Buttons.Add("RGB");window.DoWindowContents(new Rect(0,0,640,580));
            window.DoWindowContents(new Rect(0,0,640,580));window.DoWindowContents(new Rect(0,0,640,580));
            Check(Widgets.Visible==(Widgets.ColorComponents.Red|Widgets.ColorComponents.Green|Widgets.ColorComponents.Blue),"分页生效");
            Check(!c.Custom&&p.Drawer.renderer.Dirty==0,"页签不改身体");
        });
        Run("外观明度滑块保留独立透明度",()=>
        {
            var p=new Pawn();var c=AppearanceOwner(p);var window=new Dialog_FullGelAppearance(p,c.parent);Widgets.Brightness=.3f;
            Widgets.Buttons.Add("Accept");window.DoWindowContents(new Rect(0,0,640,580));window.DoWindowContents(new Rect(0,0,640,580));
            Check(Math.Abs(c.Appearance.Color.b-.3f)<1e-5&&c.Appearance.Color.a==.85f,"改变明度不改变Alpha");
        });
        foreach(var identity in new[]{PawnIdentity.Unset,PawnIdentity.Master,PawnIdentity.Slave})
        foreach(bool qualified in new[]{false,true})
        foreach(bool saved in new[]{false,true})
        {
            var id=identity;bool q=qualified,choice=saved;
            Run($"任职UI身份{id}资格{q}保存{choice}",()=>
            {
                var p=new Pawn {TrainerQualified=q};p.Training.pawnIdentity=id;p.Training.slaveTrainerEnabled=choice;
                Widgets.Drawn.Clear();Widgets.Toggle=true;ITab_SexSlaveTraining.DrawTrainer(p,q);Widgets.Toggle=false;
                bool editable=id==PawnIdentity.Slave&&(q||choice);
                Check(Widgets.Drawn.Single().Disabled==!editable,"原版disabled与任职权限一致");
                Check(p.Training.slaveTrainerEnabled==(editable?!choice:choice),"无资格只能关闭旧选择，主人和未选择保持只读");
            });
        }
        Run("任职资格在绘制后改变仍由setter拒绝开启",()=>
        {
            var p=new Pawn {TrainerQualified=false};p.Training.pawnIdentity=PawnIdentity.Slave;
            Widgets.Toggle=true;ITab_SexSlaveTraining.DrawTrainer(p,true);
            Check(!p.Training.slaveTrainerEnabled,"旧UI快照不能授予任职资格");
        });
        Run("身份区删除训练状态且入口位于所有分支之前",()=>
        {
            string s=File.ReadAllText(Path.Combine(root,"Sexslavecraft/ITab/ITab_SexSlaveTraining.cs"));
            string gate=File.ReadAllText(Path.Combine(root,"Sexslavecraft/common/FullGelatinizationUtility.cs"));
            Check(gate.Contains("return FullGelAppearanceUtility.ShouldRender(pawn);"),"生产渲染入口转发个人准入");
            Check(!s.Contains("view.Status")&&!s.Contains("StatusHeight"),"删除绘制与预留高度");
            Check(s.Contains("listing.Label(comp.IsEnabled ?"),"训练区仍保留真实状态");
            Check(s.IndexOf("DrawGelAppearanceSection(new Rect")<s.IndexOf("DrawMasterPanel(new Rect"),"外观不被身份研究分支遮挡");
        });
    }

    private static void PersonalRenderingLayers()
    {
        var rig=EyeRig();var c=FullGelAppearanceUtility.GetOwner(rig.parms.pawn).TryGetComp<HediffComp_FullGelAppearance>();
        foreach(var node in new PawnRenderNode[]{new PawnRenderNode_Body(),new PawnRenderNode_Head(),Feature(),new PawnRenderNode_Hair()})
        {
            c.SetRenderingEnabled(true);var on=rig.Apply(node);Check(on!=rig.source,"开启应用皮肤或阶段");
            c.SetRenderingEnabled(false);Check(rig.Apply(node)==rig.source&&rig.source.renderQueue==2000,"关闭恢复原来源");
            c.SetRenderingEnabled(true);Check(rig.Apply(node)==on,"重新开启复用有效缓存");
        }
        var sa=new SaRig();var sc=FullGelAppearanceUtility.GetOwner(sa.pawn).TryGetComp<HediffComp_FullGelAppearance>();
        sa.Apply();sc.SetRenderingEnabled(false);Check(sa.Apply()==sa.source,"SA关闭恢复原材质");
    }

    private static void PersonalRenderingTab()
    {
        var p=new Pawn();Check(ITab_SexSlaveTraining.GelHeight(p,320)==0,"无Hediff不显示");
        var c=AppearanceOwner(p);Check(ITab_SexSlaveTraining.GelHeight(p,320)>0,"有Hediff显示");
        Widgets.Toggle=true;ITab_SexSlaveTraining.DrawGel(p,320);Check(!c.RenderingEnabled,"个人开关立即生效");
        Widgets.Buttons.Add("SSC_GelAppearance_Edit");int count=Find.WindowStack.Windows.Count;
        ITab_SexSlaveTraining.DrawGel(p,320);Check(Find.WindowStack.Windows.Count==count+1,"关闭仍可打开同一弹窗");
        p.health.RemoveHediff(c.parent);Check(ITab_SexSlaveTraining.GelHeight(p,320)==0,"移除不留空框");
    }

    private static void PersonalRenderingLayout(float width,float fieldsHeight)
    {
        Widgets.Drawn.Clear();Widgets.FieldHeight=fieldsHeight;
        try
        {
            var p=new Pawn();var c=AppearanceOwner(p);var window=new Dialog_FullGelAppearance(p,c.parent);
            window.DoWindowContents(new Rect(0,0,width,320));window.DoWindowContents(new Rect(0,0,width,320));
            Check(Widgets.HexRect.y>=Widgets.FieldRect.yMax,"HEX在分量输入实际底边之后");
            Check(Widgets.PaletteRect.y>Widgets.FieldRect.yMax&&Widgets.PaletteRect.y>Widgets.WheelRect.yMax,"色板不叠色轮或数值");
            Check(Widgets.ContentRect.height>Widgets.PaletteRect.yMax,"滚动高度包含色板和后续内容");
            Check(Widgets.Drawn.Where(d=>d.Kind=="Swatch").Any(d=>d.Color.a==.85f),"色块预览真实透明度");
            Check(Widgets.Drawn.Where(d=>d.Kind=="Accept").All(d=>d.Rect.y==282),"底栏固定于滚动区外");
        }
        finally{Widgets.FieldHeight=102;}
    }
}
