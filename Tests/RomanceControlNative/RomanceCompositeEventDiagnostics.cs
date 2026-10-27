// AI 辅助生成：一次真实事件同时核对业务与日志，保留独立故障/闸门回归。
using HarmonyLib;
using SexSlaveCraft;
using Verse;

internal static partial class Program
{
    private static void RunCompositeEventWithDiagnostics(SSCRomanceMoodBranch branch, int raw)
    {
        var mapsField = AccessTools.Field(typeof(Game), "maps");
        var maps = mapsField.GetValue(Current.Game);
        var settings = SSCMod.settings;
        bool captured = captureMoodDiagnostics;
        try
        {
            mapsField.SetValue(Current.Game, new List<Map> { Bare<Map>() });
            SSCMod.settings = new SSCSettings { enableSSCLogs = true, enableSSCVerboseLogs = true };
            captureMoodDiagnostics = true;
            moodDiagnostics.Clear();
            RunCompositeEvent(branch, raw);
            string line = moodDiagnostics.Single(m => m.Contains("event id="));
            Require(line.Contains("branch=" + branch.Id + " ") && line.Contains("value=" + branch.Value + " ") && line.Contains("native=committed"));
            Require(line.Contains("semantic=" + SSCRomanceMoodSemantics.Of(branch.Event) + " ") && line.Contains(" order=") && line.Contains(" removed="));
            Require(line.Contains(branch.Value == 0 ? "result=Zero" : "result=Added"));
            Require(!line.Contains("corruption", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            captureMoodDiagnostics = captured;
            SSCMod.settings = settings;
            mapsField.SetValue(Current.Game, maps);
            moodDiagnostics.Clear();
        }
    }
}
