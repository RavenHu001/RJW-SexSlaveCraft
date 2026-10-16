using System;
using System.Linq;
using rjw;
using rjw.Modules.Interactions;
using SexSlaveCraft;
using Verse;

internal static class Program
{
    private static int passed,failed;
    private static readonly string[] Names={"Hand","Foot","Oral","Breast","Genitals","Anus"};
    private static void Reset()
    {
        Scribe.mode=LoadSaveMode.Inactive;Scribe.node.Clear();SSCMod.settings.useOldScoring=false;
        SSCDefOf.SSC_BodyPartTraining=new();Hediff.OnSeverity=null;InfestationHealth.OnAdded=null;
        SSCPnaSexSupplyUtility.Calls=0;SSCPnaSexSupplyUtility.OnApplied=null;CorruptionUtility.OnApplied=null;
        DefDatabase<HediffDef>.AllDefsListForReading.Clear();
        foreach(var n in Names)DefDatabase<HediffDef>.AllDefsListForReading.Add(new(){defName="SSC_Exp_"+n});
        Rand.DrawState=0;SexProps.ResolveCalls=0;
    }
    private static void Run(string name,Action test)
    {try{Reset();test();passed++;Console.WriteLine("PASS "+name);}catch(Exception e){failed++;Console.WriteLine("FAIL "+name+": "+e);}}
    private static void Check(bool ok,string message="assertion") {if(!ok)throw new Exception(message);}
    private static void Near(float a,float b) {Check(Math.Abs(a-b)<.000001f,$"{a} != {b}");}
    private static HediffDef Def(string n)=>DefDatabase<HediffDef>.GetNamed("SSC_Exp_"+n);
    private static BodyPartRecord Body(Pawn p,string n)
    {var b=new BodyPartRecord{def=new(){defName=n}};p.RaceProps.body.AllParts.Add(b);return b;}
    private static VanillaLewdablePart Part(Pawn p,string n,LewdablePartFamily family)=>new(){Owner=p,BodyPart=Body(p,n),Family=family};
    private static SexProps Scene(out Pawn master,out Pawn target,params LewdablePartFamily[] families)
    {
        master=new();target=new();master.Training.pawnIdentity=PawnIdentity.Master;
        var props=new SexProps{pawn=master,partner=target,dictionaryKey=new(){defName="Selected"}};
        props._resolved=new(){Initiator=master,Recipient=target,Interaction=new(){Def=props.dictionaryKey}};
        foreach(var f in families)props._resolved.RecipientParts.Add(Part(target,f.ToString(),f));
        return props;
    }
    private static SSCOrdinarySexOutcomeUtility.Context Context(SexProps p,SSCOrdinarySexPartsSnapshot snapshot=null)
    {
        snapshot??=SSCOrdinarySexPartsUtility.Capture(p);
        return SSCOrdinarySexOutcomeUtility.Capture(p.initiator,p.recipient,true,()=>true,()=>true,null,snapshot,()=>snapshot?.Matches(p)==true);
    }
    private static void Complete(SexProps p,Action original=null)
    {using var c=Context(p);original?.Invoke();Check(c.TryClaim());c.Dispatch();c.Dispatch();}
    private static float Exp(Pawn p,string n,BodyPartRecord part=null)=>p.health.hediffSet.hediffs
        .Where(h=>h.def==Def(n)&&(part==null||h.Part==part)).Sum(h=>h.Severity);
    private static Hediff Existing(Pawn p,string n,BodyPartRecord part,float value)
    {var h=new Hediff{pawn=p,Part=part,def=Def(n),Severity=value};p.health.hediffSet.hediffs.Add(h);return h;}

    private static int Main()
    {
        foreach(var (family,name) in new[]{(LewdablePartFamily.Hand,"Hand"),(LewdablePartFamily.Foot,"Foot"),
            (LewdablePartFamily.Mouth,"Oral"),(LewdablePartFamily.Tongue,"Oral"),(LewdablePartFamily.Beak,"Oral"),
            (LewdablePartFamily.Breasts,"Breast"),(LewdablePartFamily.Penis,"Genitals"),
            (LewdablePartFamily.Vagina,"Genitals"),(LewdablePartFamily.Anus,"Anus")})
            Run("actual family mapping "+family,()=>{
                var s=Scene(out var m,out var t,family);Body(t,"Unused");
                Complete(s);Near(Exp(t,name),.01f);Check(t.health.hediffSet.hediffs.Count==1);
                Check(t.health.hediffSet.hediffs[0].Part==s._resolved.RecipientParts[0].BodyPart);Check(SexProps.ResolveCalls==0);
            });
        foreach(var f in new[]{LewdablePartFamily.Undefined,LewdablePartFamily.Tail,LewdablePartFamily.Udders,
            LewdablePartFamily.FemaleOvipositor,LewdablePartFamily.MaleOvipositor})
            Run("unmapped family preserves other components "+f,()=>{var s=Scene(out _,out var t,f);Complete(s);Check(t.health.hediffSet.hediffs.Count==0);Near(t.needs.corruption.CurLevel,.01f);Check(SSCPnaSexSupplyUtility.Calls==1);});
        Run("duplicate lists and penetration endpoints pay once",()=>{
            var s=Scene(out var m,out var t,LewdablePartFamily.Hand);var hand=s._resolved.RecipientParts[0];
            var other=Part(m,"Mouth",LewdablePartFamily.Mouth);s._resolved.InitiatorParts.Add(other);
            s._resolved.RecipientParts.Add(hand);s._resolved.Penetrations.Add(Tuple.Create((ILewdablePart)other,hand));
            Complete(s);Near(Exp(t,"Hand"),.01f);Near(Exp(m,"Oral"),0);
        });
        foreach(bool full in new[]{false,true}) Run("two parts share frozen total full="+full,()=>{
            var s=Scene(out _,out var t,LewdablePartFamily.Hand,LewdablePartFamily.Foot);
            if(full)Existing(t,"Hand",s._resolved.RecipientParts[0].BodyPart,1);
            Complete(s);Near(Exp(t,"Hand"),full?1:.005f);Near(Exp(t,"Foot"),.005f);
        });
        Run("part near maximum does not redistribute",()=>{
            var s=Scene(out _,out var t,LewdablePartFamily.Hand,LewdablePartFamily.Foot);
            Existing(t,"Hand",s._resolved.RecipientParts[0].BodyPart,.998f);Complete(s);Near(Exp(t,"Hand"),1);Near(Exp(t,"Foot"),.005f);
        });
        Run("all full still receives PNA and corruption",()=>{
            var s=Scene(out _,out var t,LewdablePartFamily.Hand);Existing(t,"Hand",s._resolved.RecipientParts[0].BodyPart,1);
            Complete(s);Near(Exp(t,"Hand"),1);Near(t.needs.corruption.CurLevel,.01f);Check(SSCPnaSexSupplyUtility.Calls==1);
        });
        Run("tiny gain overrides initial severity",()=>{
            var s=Scene(out var m,out var t,LewdablePartFamily.Hand);m.skills.Social.Level=1;Complete(s);Near(Exp(t,"Hand"),.0005f);
        });
        Run("current definition maximum is respected",()=>{
            var s=Scene(out _,out var t,LewdablePartFamily.Hand);Def("Hand").maxSeverity=.003f;Complete(s);Near(Exp(t,"Hand"),.003f);
        });
        foreach(bool before in new[]{false,true}) Run("research snapshot and recheck before="+before,()=>{
            var s=Scene(out _,out var t,LewdablePartFamily.Hand);SSCDefOf.SSC_BodyPartTraining.IsFinished=before;
            Complete(s,()=>SSCDefOf.SSC_BodyPartTraining.IsFinished=!before);Near(Exp(t,"Hand"),0);Check(SSCPnaSexSupplyUtility.Calls==1);
        });
        Run("pre-infestation corruption threshold is frozen",()=>{
            var s=Scene(out _,out var t,LewdablePartFamily.Hand);t.needs.corruption.CurLevel=.249f;
            var d=new HediffDef{defName="Hediff_ChainOfSexSlave",stages=new(){new(){minSeverity=.1f},new(){minSeverity=.3f}}};
            DefDatabase<HediffDef>.AllDefsListForReading.Add(d);t.health.hediffSet.hediffs.Add(new(){pawn=t,def=d,Severity=.1f});
            Complete(s);Near(Exp(t,"Hand"),.0143f);Near(t.needs.corruption.CurLevel,.259f);
        });
        Run("original corruption and opinion cannot inflate budget",()=>{
            var s=Scene(out _,out var t,LewdablePartFamily.Hand);t.needs.corruption.CurLevel=.249f;
            Complete(s,()=>{t.needs.corruption.CurLevel=.75f;t.relations.Opinion=100;});Near(Exp(t,"Hand"),.013f);
        });
        Run("chain multiplier frozen before original outcome",()=>{
            var s=Scene(out _,out var t,LewdablePartFamily.Hand);t.needs.corruption.CurLevel=.3f;
            var d=new HediffDef{defName="Hediff_ChainOfSexSlave",stages=new(){new(){minSeverity=.1f},new(){minSeverity=.3f},new(){minSeverity=.5f},new(){minSeverity=.9f}}};
            DefDatabase<HediffDef>.AllDefsListForReading.Add(d);var h=new Hediff{pawn=t,def=d,Severity=.3f};t.health.hediffSet.hediffs.Add(h);
            Complete(s,()=>h.Severity=.9f);Near(Exp(t,"Hand"),.0195f);
        });
        Run("old scoring samples without moving native random stream",()=>{
            var s=Scene(out var m,out var t,LewdablePartFamily.Hand);SSCMod.settings.useOldScoring=true;t.needs.corruption.CurLevel=.3f;Rand.DrawState=17;
            Complete(s);Near(Exp(t,"Hand"),.0123f);Check(Rand.DrawState==17);Near(t.needs.corruption.CurLevel,.31f);
        });
        foreach(var c in new[]{0f,.0499f,.05f,.2499f,.25f,.4999f,.5f,.7499f,.75f}) Run("existing corruption multiplier boundary "+c,()=>{
            var p=new Pawn();p.needs.corruption.CurLevel=c;
            var expected=c>=.75f?2:c>=.5f?1.8f:c>=.25f?1.5f:c>=.05f?1.3f:1;
            Near(TrainingExpUtility.GetCorruptionStageMultiplier(p),expected);
        });
        foreach(float x in new[]{-1f,0,float.NaN,float.PositiveInfinity,float.NegativeInfinity}) Run("invalid budget input "+x,()=>{
            Near(TrainingExpUtility.CalculateExperienceBudget(x,.2f,1,1),0);
            Near(TrainingExpUtility.CalculateExperienceBudget(50,x,1,1),0);
            Near(TrainingExpUtility.CalculateExperienceBudget(50,.2f,x,1),0);
            Near(TrainingExpUtility.CalculateExperienceBudget(50,.2f,1,x),0);
        });
        Run("overflow budget is zero",()=>Near(TrainingExpUtility.CalculateExperienceBudget(float.MaxValue,float.MaxValue,2,2),0));
        Run("master target whole infestation immunity",()=>{var s=Scene(out _,out var t,LewdablePartFamily.Hand);t.Training.pawnIdentity=PawnIdentity.Master;Complete(s);Near(Exp(t,"Hand"),0);Check(SSCPnaSexSupplyUtility.Calls==0);});
        Run("off source has no development",()=>{var s=Scene(out var m,out var t,LewdablePartFamily.Hand);m.Infestation=false;Complete(s);Near(Exp(t,"Hand"),0);});
        Run("source in recipient direction still affects target only",()=>{
            var s=Scene(out var m,out var t,LewdablePartFamily.Hand);s.pawn=t;s.partner=m;
            var r=s._resolved;(r.Initiator,r.Recipient)=(t,m);(r.InitiatorParts,r.RecipientParts)=(r.RecipientParts,r.InitiatorParts);
            Complete(s);Near(Exp(t,"Hand"),.01f);Check(m.health.hediffSet.hediffs.Count==0);
        });
        Run("removed candidate consumes its original share",()=>{
            var s=Scene(out _,out var t,LewdablePartFamily.Hand,LewdablePartFamily.Foot);
            Complete(s,()=>s._resolved.RecipientParts[0].BodyPart.Missing=true);Near(Exp(t,"Hand"),0);Near(Exp(t,"Foot"),.005f);
        });
        Run("new cache cannot add an unused body part",()=>{
            var s=Scene(out _,out var t,LewdablePartFamily.Hand);var saved=SSCOrdinarySexPartsUtility.Capture(s);
            s._resolved.RecipientParts[0]=Part(t,"UnusedHand",LewdablePartFamily.Hand);Check(!saved.Matches(s));
        });
        Run("empty cache uses saved evidence without lazy resolve",()=>{
            var s=Scene(out _,out var t,LewdablePartFamily.Hand);Complete(s,()=>s._resolved=null);Near(Exp(t,"Hand"),.01f);Check(SexProps.ResolveCalls==0);
        });
        Run("changed interaction only cancels development",()=>{
            var s=Scene(out _,out var t,LewdablePartFamily.Hand);Complete(s,()=>s.dictionaryKey=new(){defName="Other"});Near(Exp(t,"Hand"),0);Check(SSCPnaSexSupplyUtility.Calls==1);Near(t.needs.corruption.CurLevel,.01f);
        });
        Run("replacement SexProps cannot inherit evidence",()=>{
            var s=Scene(out _,out _,LewdablePartFamily.Hand);var saved=SSCOrdinarySexPartsUtility.Capture(s);
            Check(!saved.Matches(new(){pawn=s.pawn,partner=s.partner,dictionaryKey=s.dictionaryKey,_resolved=s._resolved}));
        });
        foreach(var invalid in new[]{"owner","pair","interaction","nullpart","contact","body"}) Run("bad evidence "+invalid,()=>{
            var s=Scene(out var m,out var t,LewdablePartFamily.Hand);
            switch(invalid){case "owner":((VanillaLewdablePart)s._resolved.RecipientParts[0]).Owner=m;break;
                case "pair":s._resolved.Recipient=new();break;case "interaction":s._resolved.Interaction.Def=new();break;
                case "nullpart":s._resolved.RecipientParts[0]=null;break;case "contact":s._resolved.Penetrations.Add(null);break;
                case "body":t.RaceProps.body=new();break;}
            Complete(s);Near(Exp(t,"Hand"),0);Near(t.needs.corruption.CurLevel,.01f);
        });
        foreach(bool replace in new[]{false,true}) Run("removed original RJW organ replace="+replace,()=>{
            var s=Scene(out _,out var t);var b=Body(t,"Genitals");var h=new Hediff{pawn=t,Part=b,def=new(){defName="Organ"}};t.health.hediffSet.hediffs.Add(h);
            s._resolved.RecipientParts.Add(new RJWLewdablePart{SexPart=new TestSexPart{AsHediff=h},Family=LewdablePartFamily.Penis});
            Complete(s,()=>{t.health.hediffSet.hediffs.Remove(h);if(replace)t.health.hediffSet.hediffs.Add(new(){pawn=t,Part=b,def=h.def});});Near(Exp(t,"Genitals"),0);
        });
        Run("different original organs on one record deduplicate with surviving evidence",()=>{
            var s=Scene(out _,out var t);var b=Body(t,"Genitals");var h=new Hediff{pawn=t,Part=b,def=new(){defName="Organ"}};var h2=new Hediff{pawn=t,Part=b,def=h.def};t.health.hediffSet.hediffs.AddRange(new[]{h,h2});
            foreach(var organ in new[]{h,h2})s._resolved.RecipientParts.Add(new RJWLewdablePart{SexPart=new TestSexPart{AsHediff=organ},Family=LewdablePartFamily.Penis});
            Complete(s,()=>t.health.hediffSet.hediffs.Remove(h));Near(Exp(t,"Genitals"),.01f);
        });
        foreach(var phase in new[]{"original","pna","corruption","body"}) Run("personality callback stops remaining writes "+phase,()=>{
            var s=Scene(out _,out var t,LewdablePartFamily.Hand,LewdablePartFamily.Foot);
            Action change=()=>{SSCPersonalityRuntimeUtility.TryBeginMutation(t,out var mutation);mutation.Dispose();};
            if(phase=="pna")SSCPnaSexSupplyUtility.OnApplied=change;
            if(phase=="corruption")CorruptionUtility.OnApplied=change;
            if(phase=="body")InfestationHealth.OnAdded=_=>change();
            using var c=Context(s);if(phase=="original")change();var claimed=c.TryClaim();if(claimed)c.Dispatch();c.Dispatch();
            Near(Exp(t,"Hand")+Exp(t,"Foot"),phase=="body"?.005f:0);
        });
        Run("replaced corruption need stops infestation",()=>{
            var s=Scene(out _,out var t,LewdablePartFamily.Hand);Complete(s,()=>t.needs.corruption=new());Near(Exp(t,"Hand"),0);Check(SSCPnaSexSupplyUtility.Calls==0);
        });
        foreach(var initial in new[]{true,false}) Run("new record severity callback cannot attach after personality replacement initial="+initial,()=>{
            var s=Scene(out _,out var t,LewdablePartFamily.Hand);int calls=0;
            Hediff.OnSeverity=_=>{if(++calls!=(initial?1:2))return;SSCPersonalityRuntimeUtility.TryBeginMutation(t,out var mutation);mutation.Dispose();};
            Complete(s);Near(Exp(t,"Hand"),0);
        });
        Run("organ removed during record creation cannot receive experience",()=>{
            var s=Scene(out _,out var t);var b=Body(t,"Genitals");var h=new Hediff{pawn=t,Part=b,def=new(){defName="Organ"}};t.health.hediffSet.hediffs.Add(h);
            s._resolved.RecipientParts.Add(new RJWLewdablePart{SexPart=new TestSexPart{AsHediff=h},Family=LewdablePartFamily.Penis});
            Hediff.OnSeverity=_=>t.health.hediffSet.hediffs.Remove(h);Complete(s);Near(Exp(t,"Genitals"),0);
        });
        Run("deep saved evidence survives independent snapshot objects",()=>{
            var s=Scene(out var m,out var t,LewdablePartFamily.Hand);var old=SSCOrdinarySexPartsUtility.Capture(s);var row=old.Parts[0];
            Scribe.mode=LoadSaveMode.Saving;Scribe_Deep.Look(ref old,"parts");SSCOrdinarySexPartsSnapshot restored=null;
            Scribe.mode=LoadSaveMode.LoadingVars;Scribe_Deep.Look(ref restored,"parts");Scribe.mode=LoadSaveMode.Inactive;
            Check(!ReferenceEquals(old,restored)&&!ReferenceEquals(row,restored.Parts[0]));old.Parts.Clear();s._resolved=null;restored.Bind(s);
            using var c=Context(s,restored);Check(c.TryClaim());c.Dispatch();Near(Exp(t,"Hand"),.01f);Check(SexProps.ResolveCalls==0);
        });
        Run("missing legacy evidence stays null",()=>{SSCOrdinarySexPartsSnapshot x=null;Scribe.mode=LoadSaveMode.LoadingVars;Scribe_Deep.Look(ref x,"absent");Scribe.mode=LoadSaveMode.Inactive;Check(x==null);});
        Run("direct writer rejects foreign and missing parts",()=>{
            var t=new Pawn();var foreign=Body(new Pawn(),"Hand");Check(!TrainingExpUtility.TryApplyExperienceToPart(t,foreign,Def("Hand"),.01f));
            var b=Body(t,"Hand");b.Missing=true;Check(!TrainingExpUtility.TryApplyExperienceToPart(t,b,Def("Hand"),.01f));
        });
        Run("direct writer does not reduce already over-cap value",()=>{
            var t=new Pawn();var b=Body(t,"Hand");var h=Existing(t,"Hand",b,1);Def("Hand").maxSeverity=.5f;
            Check(!TrainingExpUtility.TryApplyExperienceToPart(t,b,Def("Hand"),.01f));Near(h.Severity,1);
        });
        Run("daily hand mapping retains both original hands and full multiplier",()=>{
            var p=new Pawn();Body(p,"LeftHand");Body(p,"RightHand");TrainingExpUtility.ApplyExperienceFromScore(p,xxx.rjwSextype.Handjob,50);Near(Exp(p,"Hand"),.1f);
        });
        Run("daily breast uses RJW precise lookup",()=>{
            var p=new Pawn();p.Breasts=Body(p,"SpecialBreasts");Body(p,"Chest");TrainingExpUtility.ApplyExperienceFromScore(p,xxx.rjwSextype.Boobjob,50);Near(Exp(p,"Breast"),.05f);Check(p.health.hediffSet.hediffs.Single().Part==p.Breasts);
        });
        Run("ritual-style caller multiplier remains full",()=>{
            var p=new Pawn();Body(p,"Jaw");TrainingExpUtility.ApplyExperienceFromScore(p,xxx.rjwSextype.Oral,50,3);Near(Exp(p,"Oral"),.15f);
        });
        Console.WriteLine($"RESULT: {passed}/{passed+failed} passed");return failed==0?0:1;
    }
}
