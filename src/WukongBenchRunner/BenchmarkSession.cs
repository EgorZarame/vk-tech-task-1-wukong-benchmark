using WukongBenchRunner.Config;
using WukongBenchRunner.Profiles;
using WukongBenchRunner.Results;
using WukongBenchRunner.Running;
using WukongBenchRunner.SystemInfo;

namespace WukongBenchRunner;

/// <summary>Результат одного прохода: либо метрики, либо причина неудачи.</summary>
public sealed record TestRun(
    BenchmarkProfile Profile,
    BenchmarkMetrics? Metrics,
    string? ResultFile,
    TimeSpan? Duration,
    string? Error,
    IReadOnlyList<string> Warnings)
{
    public bool Succeeded => Metrics is not null;
}

public sealed record SessionReport(DateTimeOffset StartedAt, SystemSnapshot System, IReadOnlyList<TestRun> Runs);

/// <summary>
/// Основной сценарий: бэкап конфига → для каждого профиля (настройки → прогон) → восстановление конфига.
/// Оригинальный конфиг возвращается при любой ошибке и при Ctrl+C.
/// </summary>
public sealed class BenchmarkSession(
    IGameConfig config,
    IBenchmarkRunner runner,
    ISystemInfoProvider systemInfo,
    TimeProvider time,
    ILog log)
{
    public async Task<SessionReport> RunAsync(
        string executablePath,
        IReadOnlyList<BenchmarkProfile> profiles,
        CancellationToken cancellationToken)
    {
        var startedAt = time.GetLocalNow();
        log.Info("Собираю характеристики ПК…");
        var system = systemInfo.Collect();

        if (config.RecoverAfterCrash())
        {
            log.Warn("Найден бэкап конфига от прерванного запуска — оригинальные настройки восстановлены.");
        }

        config.Backup();
        var runs = new List<TestRun>();
        try
        {
            foreach (var profile in profiles)
            {
                runs.Add(await RunProfileAsync(executablePath, profile, cancellationToken));
            }
        }
        finally
        {
            config.Restore();
            log.Info("Исходные настройки бенчмарка восстановлены.");
        }

        return new SessionReport(startedAt, system, runs);
    }

    private async Task<TestRun> RunProfileAsync(string exe, BenchmarkProfile profile, CancellationToken ct)
    {
        log.Info($"{profile.Name}: применяю настройки ({profile.Settings.ResolutionText}, " +
                 $"{GraphicsSettings.QualityName(profile.Settings.Quality)}, {profile.Settings.RenderScalePercent}%)");
        try
        {
            config.Apply(SettingsPatch.From(profile.Settings));
            var outcome = await runner.RunAsync(exe, ct);
            return new TestRun(profile, outcome.Metrics, outcome.ResultPath, outcome.Duration, null,
                SettingsVerifier.Verify(profile.Settings, outcome.Metrics.Reported));
        }
        catch (Exception e) when (e is BenchmarkRunException or ConfigFormatException or IOException
                                      or InvalidOperationException)
        {
            // Один упавший проход не должен отменять второй.
            log.Warn($"{profile.Name} не выполнен: {e.Message}");
            return new TestRun(profile, null, null, null, e.Message, []);
        }
    }
}

/// <summary>Сверяет заказанные настройки с теми, что бенчмарк записал в результат.</summary>
public static class SettingsVerifier
{
    public static IReadOnlyList<string> Verify(GraphicsSettings expected, ReportedSettings actual)
    {
        var warnings = new List<string>();

        if (actual.QualityLevel != 0 && actual.QualityLevel != (int)expected.Quality)
        {
            warnings.Add($"Бенчмарк отработал с пресетом {actual.QualityLevel}, ожидался {(int)expected.Quality}.");
        }

        if (!string.IsNullOrEmpty(actual.ScreenResolution) && !SameResolution(actual.ScreenResolution, expected))
        {
            warnings.Add($"Фактическое разрешение {actual.ScreenResolution}, ожидалось {expected.ResolutionText}. " +
                         "Список разрешений в меню зависит от монитора.");
        }

        if (actual.RayTracing != expected.RayTracing)
        {
            warnings.Add($"Трассировка лучей: фактически {(actual.RayTracing ? "вкл" : "выкл")}.");
        }

        if (actual.FrameGeneration != expected.FrameGeneration)
        {
            warnings.Add($"Генерация кадров: фактически {(actual.FrameGeneration ? "вкл" : "выкл")}.");
        }

        return warnings;
    }

    // Бенчмарк пишет разрешение как "1280 × 720" / "1280x720" — сравниваем только числа.
    private static bool SameResolution(string reported, GraphicsSettings expected)
    {
        var numbers = System.Text.RegularExpressions.Regex.Matches(reported, @"\d+")
            .Select(m => int.Parse(m.Value, System.Globalization.CultureInfo.InvariantCulture))
            .ToList();
        return numbers.Count >= 2 && numbers[0] == expected.Width && numbers[1] == expected.Height;
    }
}
