namespace WukongBenchRunner.Discovery;

/// <summary>Все пути, нужные для работы с установленным бенчмарком.</summary>
public sealed record BenchmarkInstallation(string RootDirectory, string ExecutablePath, string ConfigPath, string HistoryDirectory)
{
    public const int SteamAppId = 3132990;
    public const string DefaultFolderName = "Black Myth Wukong Benchmark Tool";

    /// <summary>Имена процессов: лаунчер b1.exe и сама игра на Unreal Engine.</summary>
    public static readonly string[] ProcessNames = ["b1", "b1-Win64-Shipping"];

    /// <summary>
    /// Каждый завершённый прогон бенчмарк пишет JSON в <c>%TEMP%\b1\BenchMarkHistory\Tool\&lt;unix-время&gt;</c>.
    /// </summary>
    public static string DefaultHistoryDirectory =>
        Path.Combine(Path.GetTempPath(), "b1", "BenchMarkHistory", "Tool");

    /// <summary>Собирает пути от корня установки; null, если exe не найден.</summary>
    public static BenchmarkInstallation? FromRoot(string root)
    {
        string[] candidates =
        [
            Path.Combine(root, "b1.exe"),
            Path.Combine(root, "b1", "Binaries", "Win64", "b1-Win64-Shipping.exe"),
        ];
        var exe = candidates.FirstOrDefault(File.Exists);
        if (exe is null) return null;

        var config = Path.Combine(root, "b1", "Saved", "Config", "Windows", "GameUserSettings.ini");
        return new BenchmarkInstallation(root, exe, config, DefaultHistoryDirectory);
    }
}
