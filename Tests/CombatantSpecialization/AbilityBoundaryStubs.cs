// 原版边界仅记录是否进入激活及实例冷却；不模拟原版效果、Scribe 或游戏计时。
using System;
using System.Collections.Generic;
using Verse;

namespace Verse
{
    public partial class Pawn
    {
        public bool Drafted;
        public RimWorld.Pawn_AbilityTracker abilities;
        public bool IsHashIntervalTick(int interval) => true;
    }
    public struct AcceptanceReport
    {
        public bool Accepted;
        public static implicit operator AcceptanceReport(bool value) => new AcceptanceReport { Accepted = value };
        public static implicit operator AcceptanceReport(string reason) => false;
        public static implicit operator bool(AcceptanceReport value) => value.Accepted;
    }
    public static class Translator { public static string Translate(this string key) => key; }
    public class HediffCompProperties { public Type compClass; }
    public class HediffComp
    {
        public Hediff parent;
        public HediffCompProperties props;
        public virtual void CompPostTick(ref float adjustment) { }
        public virtual void CompPostPostRemoved() { }
    }
}
namespace RimWorld
{
    public class AbilityDef { }
    public class Ability
    {
        public Pawn pawn;
        public AbilityDef def;
        public bool BaseAllowed = true;
        public int ActivationCalls, Cooldown;
        public Ability() { }
        public Ability(Pawn pawn) { this.pawn = pawn; }
        public Ability(Pawn pawn, AbilityDef def) { this.pawn = pawn; this.def = def; }
        public virtual AcceptanceReport CanCast => BaseAllowed && Cooldown == 0;
        public virtual bool Activate(LocalTargetInfo target, LocalTargetInfo dest)
        {
            ActivationCalls++;
            Cooldown = 10000;
            return true;
        }
    }
    public class Pawn_AbilityTracker
    {
        private readonly Pawn pawn;
        private readonly Dictionary<AbilityDef, Ability> abilities = new Dictionary<AbilityDef, Ability>();
        public Pawn_AbilityTracker(Pawn pawn) { this.pawn = pawn; }
        public Ability GetAbility(AbilityDef def) => abilities.TryGetValue(def, out var a) ? a : null;
        public void GainAbility(AbilityDef def)
        {
            if (!abilities.ContainsKey(def)) abilities.Add(def, new SexSlaveCraft.Ability_CombatOverdrive(pawn, def));
        }
        public void RemoveAbility(AbilityDef def) => abilities.Remove(def);
    }
}
