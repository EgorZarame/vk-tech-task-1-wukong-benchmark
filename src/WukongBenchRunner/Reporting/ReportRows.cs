using System.Globalization;
using WukongBenchRunner.Results;

namespace WukongBenchRunner.Reporting;

/// <summary>Общая раскладка таблицы результатов для консоли и Markdown.</summary>
internal static class ReportRows
{
    public static IReadOnlyList<(string Metric, Func<BenchmarkMetrics, string> Value)> Metrics { get; } =
    [
        ("Средний FPS", m => Fps(m.AverageFps)),
        ("Минимальный FPS", m => Fps(m.MinFps)),
        ("Максимальный FPS", m => Fps(m.MaxFps)),
        ("FPS 95% (держится 95% времени)", m => Fps(m.Fps95)),
        ("1% low (из покадровых данных)", m => Fps(m.OnePercentLowFps)),
        ("Средняя загрузка CPU", m => Percent(m.AverageCpuUsage)),
        ("Средняя загрузка GPU", m => Percent(m.AverageGpuUsage)),
        ("Среднее время кадра CPU", m => Ms(m.AverageCpuFrameTimeMs)),
        ("Среднее время кадра GPU", m => Ms(m.AverageGpuFrameTimeMs)),
        ("Кадров, где ограничивал CPU", m => Percent(m.CpuBoundSharePercent)),
        ("Узкое место", m => m.Bottleneck),
        ("Кадров в замере", m => m.FrameCount.ToString(CultureInfo.InvariantCulture)),
        ("Фактическое разрешение", m => m.Reported.ScreenResolution ?? "—"),
        ("Версия бенчмарка", m => m.GameVersion ?? "—"),
    ];

    private static string Fps(double? value) => value is null ? "—" : value.Value.ToString("0.0", CultureInfo.InvariantCulture);

    private static string Percent(double? value) => value is null ? "—" : value.Value.ToString("0.0", CultureInfo.InvariantCulture) + "%";

    private static string Ms(double? value) => value is null ? "—" : value.Value.ToString("0.00", CultureInfo.InvariantCulture) + " мс";
}
