// AI 辅助生成：显式选择测试范围；All不丢弃用例，独立文案入口保持原有范围。
using System;
using System.Collections.Generic;
using System.Text.Json;

internal static class SscTestProfile
{
    private static readonly string suite = typeof(SscTestProfile).Assembly.GetName().Name;
    private static readonly HashSet<string> special = new HashSet<string>(StringComparer.Ordinal);
    private static readonly string scope;
    private static readonly bool textOnly;
    private static readonly bool wholeSpecial;
    private static bool started;

    static SscTestProfile()
    {
        scope = Environment.GetEnvironmentVariable("SSC_TEST_SCOPE") ?? "All";
        if (!string.Equals(scope, "All", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(scope, "Core", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(scope, "Special", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid SSC_TEST_SCOPE: " + scope);
        textOnly = suite == "RomanceControlNative" && Environment.GetEnvironmentVariable("SSC_ROMANCE_TEXT_ONLY") == "1";
        using (var stream = typeof(SscTestProfile).Assembly.GetManifestResourceStream("Ssc.TestProfiles.json"))
        {
            if (stream == null) throw new InvalidOperationException("Missing embedded test profile manifest.");
            using (var document = JsonDocument.Parse(stream))
            {
                if (document.RootElement.GetProperty("SchemaVersion").GetInt32() != 1)
                    throw new InvalidOperationException("Unsupported test profile manifest.");
                bool found = false;
                foreach (var row in document.RootElement.GetProperty("Suites").EnumerateArray())
                {
                    if (row.GetProperty("Name").GetString() != suite) continue;
                    if (found) throw new InvalidOperationException("Duplicate suite profile: " + suite);
                    found = true;
                    wholeSpecial = row.GetProperty("Kind").GetString() == "Special";
                    foreach (var name in row.GetProperty("SpecialCases").EnumerateArray())
                        if (!special.Add(name.GetString())) throw new InvalidOperationException("Duplicate special case in " + suite);
                }
                if (!found) throw new InvalidOperationException("Unregistered suite profile: " + suite);
            }
        }
    }

    internal static void Start()
    {
        if (started || textOnly) return;
        started = true;
        Console.WriteLine("SCOPE: " + scope);
    }

    internal static bool ShouldRun(string name)
    {
        Start();
        if (textOnly || string.Equals(scope, "All", StringComparison.OrdinalIgnoreCase)) return true;
        bool isSpecial = wholeSpecial || special.Contains(name);
        return string.Equals(scope, "Special", StringComparison.OrdinalIgnoreCase) ? isSpecial : !isSpecial;
    }

    internal static bool IncludesSuite => textOnly || string.Equals(scope, "All", StringComparison.OrdinalIgnoreCase)
        || (string.Equals(scope, "Core", StringComparison.OrdinalIgnoreCase) ? !wholeSpecial : wholeSpecial || special.Count > 0);

    internal static bool IncludesCore => textOnly || !string.Equals(scope, "Special", StringComparison.OrdinalIgnoreCase);
}
