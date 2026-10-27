using System.Globalization;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private sealed record DocumentedMood(string Id, int Number, int Stage, bool Masochist, string Band, int Value, int LongDays);
    private sealed record DocumentedControl(string Id, SSCRomanceControlState State, int Stage, bool Masochist, int Value);
    private static Dictionary<string, DocumentedMood> documentedMoods;
    private static Dictionary<string, DocumentedControl> documentedControls;

    // 独立预期继续来自维护者的分支表；实际事件不以生产目录中的数值自证。
    private static void LoadDocumentedMoodExpectations(string repo)
    {
        string document = File.ReadAllText(Path.Combine(repo, "Docs/Development/Reference/性奴浪漫综合心情完整分支表.md"));
        static IEnumerable<string[]> Rows(string text, string prefix) => text.Split('\n')
            .Where(line => line.StartsWith(prefix))
            .Select(line => line.Trim().Trim('|').Split('|').Select(c => c.Trim()).ToArray());
        static int Value(string cell) => int.Parse(cell.Replace('−', '-'), CultureInfo.InvariantCulture);
        documentedMoods = Rows(document.Split("## 2. 完整事件分支")[1].Split("## 3.")[0], "| `E")
            .Select(row =>
            {
                string id = row[0].Trim('`'); var tokens = id.Split('-');
                int number = int.Parse(tokens[0][1..]);
                int longDays = number == 13 ? 30 : new[] { 5, 9, 10, 11, 12 }.Contains(number) ? 25 : 0;
                return new DocumentedMood(id, number, int.Parse(tokens[1][1..]), tokens[2] == "M", tokens[3], Value(row[4]), longDays);
            }).ToDictionary(row => row.Id);
        documentedControls = Rows(document.Split("## 4. 持续思想分支补全")[1].Split("## 5.")[0], "| `C-")
            .Select(row =>
            {
                string id = row[0].Trim('`'); var tokens = id.Split('-');
                var state = tokens[1] == "A" ? SSCRomanceControlState.Active : tokens[1] == "B" ? SSCRomanceControlState.Passive : SSCRomanceControlState.Both;
                return new DocumentedControl(id, state, int.Parse(tokens[2][1..]), tokens[3] == "M", Value(row[4]));
            }).ToDictionary(row => row.Id);
    }

    private static void AssertDocumentedEvent(SSCRomanceMoodBranch branch, int raw, Thought_MemorySSCCompositeRomance memory)
    {
        var expected = documentedMoods[branch.Id];
        Equal((SSCRomanceCompositeEvent)expected.Number, branch.Event); Equal(expected.Stage, branch.Stage);
        Equal(expected.Masochist, branch.Masochist); Equal(expected.Value, branch.Value);
        Equal(raw <= -10 ? "D" : raw >= 60 ? "C" : "G", expected.Band);
        Require(SSCRomanceMoodCatalog.TryResolve((SSCRomanceCompositeEvent)expected.Number, expected.Stage, expected.Masochist, raw, out var selection));
        Equal(expected.Id, selection.Branch.Id);
        int days = expected.Value == 0 ? 0 : expected.LongDays > 0 && expected.Value < 0 && raw > 0 ? expected.LongDays : 3;
        Equal(days * 60000, selection.DurationTicks);
        Equal(days == 0 ? SSCRomanceMoodGroup.None : days == 3 ? SSCRomanceMoodGroup.Short : SSCRomanceMoodGroup.Long, selection.Group);
        var data = selection.CreateSnapshot(); Require(SSCRomanceMoodCatalog.TryReadSnapshot(data, out var restored));
        Equal(expected.Id, restored.Id); Equal(raw, data.RawOpinion); Require(data.HasRawOpinion && data.IsComposite);
        Equal(days * 60000, data.NominalDurationTicks); Equal(expected.Value, data.Copy().Value);
        Require(!ReferenceEquals(data, data.Copy()));
        if (expected.Value == 0)
        {
            Require(memory == null);
            Require(!Thought_MemorySSCCompositeRomance.TryCreate(selection, out var zero) && zero == null);
            return;
        }
        Require(memory != null); Equal(expected.Value + 15, memory.CurStageIndex);
        Equal((float)expected.Value, memory.MoodOffset()); Equal(days * 60000, memory.DurationTicks);
        Equal(expected.Id, memory.Data.BranchId); Equal(expected.Value, memory.Data.Value);
    }

    private static void AssertDocumentedControl(SSCRomanceMoodBranch branch, SSCRomanceMoodBranch selected, Thought_SituationalSSCRomanceControl thought)
    {
        var expected = documentedControls[branch.Id];
        Require(SSCRomanceMoodCatalog.TryResolveControl(expected.State, expected.Stage, expected.Masochist, out var direct));
        Equal(expected.Id, direct.Id); Equal(expected.Id, selected.Id); Equal(expected.Value, direct.Value);
        Equal(expected.State, branch.Control); Equal(expected.Stage, branch.Stage); Equal(expected.Masochist, branch.Masochist);
        Require(branch.IsContinuous && !branch.Opinion.HasValue); Equal(expected.Value, branch.Value);
        Equal((float)expected.Value, thought.CurStage.baseMoodEffect);
    }
}
