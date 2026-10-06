// 训练Tracker边界仅提供原版公开结果及调用记录；不复制种族体型、前置或原版训练算法。
using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Verse
{
    public enum AnimalType { Normal, Dryad }
    public partial class Pawn
    {
        public Pawn_TrainingTracker training = new();
        public Pawn_MindState mindState = new();
        public Pawn_PlayerSettings playerSettings = new();
    }
    public class Pawn_MindState { public int lastAssignedInteractTime = -100, interactionsToday; }
}

namespace RimWorld
{
    public class TrainableDef : Def { public int steps = 5; }
    public static class TrainableUtility { public static readonly List<TrainableDef> TrainableDefsInListOrder = new(); }
    public class Pawn_PlayerSettings { public Pawn Master; }
    public sealed class TrainingState
    {
        public bool Wanted, CanBeTrained = true, CanAssignToTrain = true, Learned;
        public int Steps;
    }
    public class Pawn_TrainingTracker
    {
        public readonly Dictionary<TrainableDef, TrainingState> States = new();
        public readonly List<(TrainableDef project, Pawn trainer, bool complete)> Requests = new();
        public Action<TrainableDef, Pawn, bool> OnTrain;
        public bool NativeTrainEffective = true;
        public TrainingState State(TrainableDef def)
        {
            if (!States.TryGetValue(def, out var state)) States[def] = state = new TrainingState();
            return state;
        }
        public bool GetWanted(TrainableDef def) => State(def).Wanted;
        public bool CanBeTrained(TrainableDef def) => State(def).CanBeTrained;
        public AcceptanceReport CanAssignToTrain(TrainableDef def) => State(def).CanAssignToTrain;
        public bool HasLearned(TrainableDef def) => State(def).Learned;
        public void Train(TrainableDef def, Pawn trainer, bool complete = false)
        {
            Requests.Add((def, trainer, complete));
            if (NativeTrainEffective)
            {
                var state = State(def); state.Learned = true; state.CanBeTrained = false; state.Steps = def.steps;
            }
            OnTrain?.Invoke(def, trainer, complete);
        }
    }
}
