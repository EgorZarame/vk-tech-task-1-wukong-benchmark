using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace WukongBenchRunner.Discovery;

/// <summary>
/// Ищет бенчмарк во всех библиотеках Steam: путь к Steam берётся из реестра,
/// список библиотек — из <c>steamapps\libraryfolders.vdf</c>, имя папки — из <c>appmanifest_3132990.acf</c>.
/// </summary>
public sealed partial class SteamLibraryLocator(Func<string?> steamPathProvider)
{
    public SteamLibraryLocator() : this(ReadSteamPathFromRegistry)
    {
    }

    /// <summary>Возвращает установку и список мест, где искали (для понятной ошибки).</summary>
    public (BenchmarkInstallation? Installation, IReadOnlyList<string> Searched) Locate()
    {
        var searched = new List<string>();
        foreach (var library in EnumerateLibraries())
        {
            var steamApps = Path.Combine(library, "steamapps");
            var manifest = Path.Combine(steamApps, $"appmanifest_{BenchmarkInstallation.SteamAppId}.acf");
            var folder = File.Exists(manifest)
                ? ParseInstallDir(File.ReadAllText(manifest)) ?? BenchmarkInstallation.DefaultFolderName
                : BenchmarkInstallation.DefaultFolderName;

            var root = Path.Combine(steamApps, "common", folder);
            searched.Add(root);
            var installation = BenchmarkInstallation.FromRoot(root);
            if (installation is not null) return (installation, searched);
        }
        return (null, searched);
    }

    private IEnumerable<string> EnumerateLibraries()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var steam = steamPathProvider();
        string[] roots = steam is null
            ? [@"C:\Program Files (x86)\Steam", @"C:\Program Files\Steam"]
            : [steam];

        foreach (var root in roots)
        {
            if (seen.Add(Normalize(root))) yield return root;

            var vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(vdf)) continue;

            foreach (var library in ParseLibraryFolders(File.ReadAllText(vdf)))
            {
                if (seen.Add(Normalize(library))) yield return library;
            }
        }
    }

    /// <summary>Пути библиотек из libraryfolders.vdf (формат KeyValues: <c>"path"  "D:\\SteamLibrary"</c>).</summary>
    public static IReadOnlyList<string> ParseLibraryFolders(string vdf) =>
        PathRegex().Matches(vdf).Select(m => Unescape(m.Groups[1].Value)).ToList();

    /// <summary>Имя папки установки из appmanifest_*.acf.</summary>
    public static string? ParseInstallDir(string acf)
    {
        var match = InstallDirRegex().Match(acf);
        return match.Success ? Unescape(match.Groups[1].Value) : null;
    }

    private static string Unescape(string value) => value.Replace(@"\\", @"\");

    private static string Normalize(string path) => path.Replace('/', '\\').TrimEnd('\\');

    private static string? ReadSteamPathFromRegistry()
    {
        if (!OperatingSystem.IsWindows()) return null;
        return ReadRegistry();

        [SupportedOSPlatform("windows")]
        static string? ReadRegistry() =>
            Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string
            ?? Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null) as string;
    }

    [GeneratedRegex("""^\s*"path"\s+"([^"]+)"\s*$""", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex PathRegex();

    [GeneratedRegex("""^\s*"installdir"\s+"([^"]+)"\s*$""", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex InstallDirRegex();
}
