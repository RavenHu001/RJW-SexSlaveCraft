// AI 辅助生成：独立条件、阶段阈值与语义代表；完整目录和真实事件仍逐分支验证。
using SexSlaveCraft;

internal static class RomanceRepresentativeCases
{
    internal static readonly (int Stage, int Mask)[] ActiveFilters =
    {
        (1, 0),
        (1, 7),
        (1, 63),
        (2, 31),
        (2, 47),
        (2, 55),
        (2, 59),
        (2, 61),
        (2, 62),
        (2, 63),
        (3, 31),
        (3, 47),
        (3, 55),
        (3, 59),
        (3, 61),
        (3, 62),
        (3, 63),
        (4, 31),
        (4, 47),
        (4, 55),
        (4, 59),
        (4, 61),
        (4, 62),
        (4, 63),
    };

    internal static readonly (int Stage, int Permissions, int Guarantees)[] PassiveFilters =
    {
        (1, 1, 0),
        (1, 2, 0),
        (1, 3, 0),
        (1, 3, 1),
        (1, 3, 2),
        (1, 3, 4),
        (2, 1, 0),
        (2, 2, 0),
        (2, 3, 0),
        (2, 3, 1),
        (2, 3, 2),
        (2, 3, 4),
        (3, 1, 0),
        (3, 2, 0),
        (3, 3, 0),
        (3, 3, 1),
        (3, 3, 2),
        (3, 3, 4),
        (4, 1, 0),
        (4, 2, 0),
        (4, 3, 0),
        (4, 3, 1),
        (4, 3, 2),
        (4, 3, 4),
    };

    internal static readonly (string Previous, string Next)[] Replacements =
    {
        ("E01-S3-N-D", "E01-S3-N-G"),
        ("E01-S3-N-G", "E01-S3-N-C"),
        ("E01-S3-N-C", "E01-S3-M-D"),
        ("E01-S3-M-D", "E01-S3-M-G"),
        ("E01-S3-M-G", "E01-S3-M-C"),
        ("E01-S3-M-C", "E01-S4-N-D"),
        ("E01-S4-N-D", "E01-S4-N-G"),
        ("E01-S4-N-G", "E01-S4-N-C"),
        ("E01-S4-N-C", "E01-S4-M-D"),
        ("E01-S4-M-D", "E01-S4-M-G"),
        ("E01-S4-M-G", "E01-S4-M-C"),
        ("E01-S4-M-C", "E01-S3-N-D"),
        ("E01-S3-N-G", "E01-S3-N-G"),
        ("E01-S3-M-G", "E01-S3-M-G"),
        ("E01-S4-N-G", "E01-S4-N-G"),
        ("E01-S4-M-G", "E01-S4-M-G"),
        ("E01-S3-N-C", "E01-S3-N-D"),
        ("E01-S3-N-D", "E01-S3-N-C"),
        ("E01-S3-N-G", "E01-S3-M-D"),
        ("E01-S3-N-D", "E01-S3-M-D"),
    };

    internal static readonly (int Mask, int Category, bool Allow, bool Original, SSCRomanceAction Action)[] Guarantees =
    {
        (0, 0, false, false, SSCRomanceAction.Romance),
        (0, 0, false, false, SSCRomanceAction.Proposal),
        (0, 0, false, true, SSCRomanceAction.Romance),
        (0, 0, false, true, SSCRomanceAction.Proposal),
        (0, 0, true, false, SSCRomanceAction.Romance),
        (0, 0, true, false, SSCRomanceAction.Proposal),
        (0, 0, true, true, SSCRomanceAction.Romance),
        (0, 0, true, true, SSCRomanceAction.Proposal),
        (0, 1, false, false, SSCRomanceAction.Romance),
        (0, 1, false, false, SSCRomanceAction.Proposal),
        (0, 1, false, true, SSCRomanceAction.Romance),
        (0, 1, false, true, SSCRomanceAction.Proposal),
        (0, 1, true, false, SSCRomanceAction.Romance),
        (0, 1, true, false, SSCRomanceAction.Proposal),
        (0, 1, true, true, SSCRomanceAction.Romance),
        (0, 1, true, true, SSCRomanceAction.Proposal),
        (0, 2, false, false, SSCRomanceAction.Romance),
        (0, 2, false, false, SSCRomanceAction.Proposal),
        (0, 2, false, true, SSCRomanceAction.Romance),
        (0, 2, false, true, SSCRomanceAction.Proposal),
        (0, 2, true, false, SSCRomanceAction.Romance),
        (0, 2, true, false, SSCRomanceAction.Proposal),
        (0, 2, true, true, SSCRomanceAction.Romance),
        (0, 2, true, true, SSCRomanceAction.Proposal),
        (1, 0, false, false, SSCRomanceAction.Romance),
        (1, 0, false, false, SSCRomanceAction.Proposal),
        (1, 0, false, true, SSCRomanceAction.Romance),
        (1, 0, false, true, SSCRomanceAction.Proposal),
        (1, 0, true, false, SSCRomanceAction.Romance),
        (1, 0, true, false, SSCRomanceAction.Proposal),
        (1, 0, true, true, SSCRomanceAction.Romance),
        (1, 0, true, true, SSCRomanceAction.Proposal),
        (1, 1, false, false, SSCRomanceAction.Romance),
        (1, 1, false, false, SSCRomanceAction.Proposal),
        (1, 2, false, false, SSCRomanceAction.Romance),
        (1, 2, false, false, SSCRomanceAction.Proposal),
        (2, 0, false, false, SSCRomanceAction.Romance),
        (2, 0, false, false, SSCRomanceAction.Proposal),
        (2, 1, false, false, SSCRomanceAction.Romance),
        (2, 1, false, false, SSCRomanceAction.Proposal),
        (2, 1, false, true, SSCRomanceAction.Romance),
        (2, 1, false, true, SSCRomanceAction.Proposal),
        (2, 1, true, false, SSCRomanceAction.Romance),
        (2, 1, true, false, SSCRomanceAction.Proposal),
        (2, 1, true, true, SSCRomanceAction.Romance),
        (2, 1, true, true, SSCRomanceAction.Proposal),
        (2, 2, false, false, SSCRomanceAction.Romance),
        (2, 2, false, false, SSCRomanceAction.Proposal),
        (4, 0, false, false, SSCRomanceAction.Romance),
        (4, 0, false, false, SSCRomanceAction.Proposal),
        (4, 1, false, false, SSCRomanceAction.Romance),
        (4, 1, false, false, SSCRomanceAction.Proposal),
        (4, 2, false, false, SSCRomanceAction.Romance),
        (4, 2, false, false, SSCRomanceAction.Proposal),
        (4, 2, false, true, SSCRomanceAction.Romance),
        (4, 2, false, true, SSCRomanceAction.Proposal),
        (4, 2, true, false, SSCRomanceAction.Romance),
        (4, 2, true, false, SSCRomanceAction.Proposal),
        (4, 2, true, true, SSCRomanceAction.Romance),
        (4, 2, true, true, SSCRomanceAction.Proposal),
    };

    internal static readonly (string Branch, int Raw)[] Admissions =
    {
        ("E01-S3-N-D", -10),
        ("E02-S3-N-D", -10),
        ("E03-S3-N-G", 0),
        ("E04-S3-N-D", -10),
        ("E05-S3-N-D", -10),
        ("E06-S4-N-D", -10),
        ("E07-S4-N-G", 0),
        ("E08-S4-N-D", -10),
        ("E09-S4-N-D", -10),
        ("E10-S4-N-D", -10),
        ("E11-S4-N-D", -10),
        ("E12-S4-N-D", -10),
        ("E13-S4-N-D", -10),
        ("E02-S3-M-G", 0),
        ("E03-S3-N-D", -10),
        ("E03-S3-M-G", 0),
        ("E04-S4-N-G", 0),
        ("E07-S4-N-D", -10),
        ("E05-S3-N-G", 0),
        ("E05-S3-N-G", 1),
        ("E09-S4-N-G", 0),
        ("E09-S4-N-G", 1),
        ("E13-S4-N-G", 0),
        ("E13-S4-N-G", 1),
    };

    internal static readonly (string Branch, int Raw)[] EquivalentReplacements =
    {
        ("E01-S3-N-D", -10),
        ("E02-S3-N-D", -10),
        ("E03-S3-N-G", 0),
        ("E04-S3-N-D", -10),
        ("E05-S3-N-D", -10),
        ("E06-S4-N-D", -10),
        ("E07-S4-N-G", 0),
        ("E08-S4-N-D", -10),
        ("E09-S4-N-D", -10),
        ("E10-S4-N-D", -10),
        ("E11-S4-N-D", -10),
        ("E12-S4-N-D", -10),
        ("E13-S4-N-D", -10),
        ("E05-S3-N-G", 0),
        ("E05-S3-N-G", 1),
        ("E09-S4-N-G", 0),
        ("E09-S4-N-G", 1),
        ("E10-S4-N-G", 0),
        ("E10-S4-N-G", 1),
        ("E11-S4-N-G", 0),
        ("E11-S4-N-G", 1),
        ("E12-S4-N-G", 0),
        ("E12-S4-N-G", 1),
        ("E13-S4-N-G", 0),
        ("E13-S4-N-G", 1),
        ("E02-S3-M-G", 0),
    };

    internal static readonly (string Branch, int Raw)[] SameResultBranches =
    {
        ("E01-S3-N-D", -10),
        ("E01-S3-M-G", 0),
        ("E01-S4-N-C", 60),
        ("E01-S4-M-G", 0),
        ("E02-S3-N-D", -10),
        ("E02-S3-M-G", 0),
        ("E02-S4-N-C", 60),
        ("E02-S4-M-G", 0),
        ("E03-S3-N-D", -10),
        ("E03-S3-M-G", 0),
        ("E03-S4-N-C", 60),
        ("E03-S4-M-G", 0),
        ("E04-S3-N-D", -10),
        ("E04-S3-M-G", 0),
        ("E04-S4-N-G", 0),
        ("E04-S4-M-G", 0),
        ("E05-S3-N-D", -10),
        ("E05-S3-M-G", 1),
        ("E05-S4-N-C", 60),
        ("E05-S4-M-G", 0),
    };

    internal static readonly (int Event, int Stage)[] UnsupportedStages =
    {
        (0, 4),
        (1, 0),
        (1, 2),
        (1, 5),
        (2, 2),
        (2, 5),
        (3, 2),
        (3, 5),
        (4, 2),
        (4, 5),
        (5, 2),
        (5, 5),
        (6, 0),
        (6, 3),
        (6, 5),
        (7, 3),
        (7, 5),
        (8, 3),
        (8, 5),
        (9, 3),
        (9, 5),
        (10, 3),
        (10, 5),
        (11, 3),
        (11, 5),
        (12, 3),
        (12, 5),
        (13, 3),
        (13, 5),
        (14, 4),
    };
}

internal static partial class Program
{
    private static SSCRomanceMoodBranch RepresentativeBranch(string id)
    {
        if (!SSCRomanceMoodCatalog.TryGet(id, out var branch)) throw new InvalidOperationException("Missing representative branch: " + id);
        return branch;
    }

    private static IEnumerable<(SSCRomanceMoodBranch Previous, SSCRomanceMoodBranch Next)> RepresentativeReplacements()
    {
        foreach (var pair in RomanceRepresentativeCases.Replacements)
            yield return (RepresentativeBranch(pair.Previous), RepresentativeBranch(pair.Next));
    }

    private static IEnumerable<(SSCRomanceMoodBranch Branch, int Raw)> RepresentativeBranches((string Branch, int Raw)[] samples)
    {
        foreach (var sample in samples) yield return (RepresentativeBranch(sample.Branch), sample.Raw);
    }
}
