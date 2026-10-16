using System;
using System.IO;
using System.Linq;

/// <summary>Separates repository source from versioned game resources in every test host.</summary>
internal static class SscTestPaths
{
    public static string RepositoryRoot(string candidate = null)
    {
        string configured = Environment.GetEnvironmentVariable("SSC_TEST_REPOSITORY_ROOT");
        if (!string.IsNullOrWhiteSpace(configured)) return System.IO.Path.GetFullPath(configured);
        foreach (string start in new[] { candidate, AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            if (string.IsNullOrWhiteSpace(start)) continue;
            for (var directory = new DirectoryInfo(System.IO.Path.GetFullPath(start)); directory != null; directory = directory.Parent)
                if (Directory.Exists(System.IO.Path.Combine(directory.FullName, "Tests")) &&
                    Directory.Exists(System.IO.Path.Combine(directory.FullName, "Sexslavecraft")))
                    return directory.FullName;
        }
        throw new DirectoryNotFoundException("Cannot locate SSC repository. Set SSC_TEST_REPOSITORY_ROOT.");
    }

    public static string RuntimeRoot(string repositoryRoot = null)
    {
        string configured = Environment.GetEnvironmentVariable("SSC_TEST_RUNTIME_ROOT");
        return System.IO.Path.GetFullPath(string.IsNullOrWhiteSpace(configured)
            ? System.IO.Path.Combine(RepositoryRoot(repositoryRoot), "1.7") : configured);
    }

    public static string CommonRoot(string repositoryRoot = null)
    {
        string configured = Environment.GetEnvironmentVariable("SSC_TEST_COMMON_ROOT");
        return System.IO.Path.GetFullPath(string.IsNullOrWhiteSpace(configured)
            ? System.IO.Path.Combine(RepositoryRoot(repositoryRoot), "Common") : configured);
    }

    // Inputs beginning with source/project names retain their repository location. Runtime resources
    // always resolve beneath the selected game version; no root-level resource fallback is permitted.
    public static string Combine(string repositoryRoot, params string[] paths)
    {
        string relative = System.IO.Path.Combine(paths);
        string first = relative.Replace('\\', '/').Split('/')[0];
        if (new[] { "Defs", "Languages", "Patches", "Resources", "Assemblies", "Sounds" }.Contains(first, StringComparer.OrdinalIgnoreCase))
            return System.IO.Path.Combine(RuntimeRoot(repositoryRoot), relative);
        if (string.Equals(first, "Textures", StringComparison.OrdinalIgnoreCase))
        {
            string overridden = System.IO.Path.Combine(RuntimeRoot(repositoryRoot), relative);
            return File.Exists(overridden) ? overridden : System.IO.Path.Combine(CommonRoot(repositoryRoot), relative);
        }
        return System.IO.Path.Combine(RepositoryRoot(repositoryRoot), relative);
    }

    public static string ResourceRelativePath(string repositoryRoot, string file)
        => System.IO.Path.GetRelativePath(RuntimeRoot(repositoryRoot), file);
}
