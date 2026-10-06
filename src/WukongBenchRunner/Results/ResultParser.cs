using System.Text.Json;

namespace WukongBenchRunner.Results;

public interface IResultParser
{
    /// <exception cref="ResultParseException">Файл не читается или это не результат бенчмарка.</exception>
    BenchmarkMetrics ParseFile(string path);

    BenchmarkMetrics Parse(string json);
}

public sealed class ResultParser : IResultParser
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString,
    };

    public BenchmarkMetrics ParseFile(string path)
    {
        string json;
        try
        {
            // FileShare.ReadWrite: бенчмарк может ещё держать файл открытым.
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            json = reader.ReadToEnd();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new ResultParseException($"Не удалось прочитать {path}: {e.Message}", e);
        }
        return Parse(json);
    }

    public BenchmarkMetrics Parse(string json)
    {
        BenchmarkResultFile? file;
        try
        {
            file = JsonSerializer.Deserialize<BenchmarkResultFile>(json, Options);
        }
        catch (JsonException e)
        {
            throw new ResultParseException($"Некорректный JSON результата: {e.Message}", e);
        }

        // Файл, который ещё дописывается, или чужой JSON — без среднего FPS это не результат.
        if (file is null || file.FPSAvg <= 0)
        {
            throw new ResultParseException("В файле нет FPSAvg — это не завершённый результат бенчмарка.");
        }

        return ToMetrics(file);
    }

    internal static BenchmarkMetrics ToMetrics(BenchmarkResultFile file)
    {
        var frames = file.Records.Where(r => r.FrameRate > 0).ToList();
        var timed = frames.Where(r => r.CPUFrameTime > 0 && r.GPUFrameTime > 0).ToList();

        return new BenchmarkMetrics
        {
            AverageFps = file.FPSAvg,
            MinFps = file.FPSMin,
            MaxFps = file.FPSMax,
            Fps95 = file.FPS95,
            OnePercentLowFps = frames.Count == 0 ? null : Percentile(frames.Select(r => r.FrameRate), 1),
            AverageCpuUsage = file.CPUAvg,
            AverageGpuUsage = file.GPUAvg,
            AverageCpuFrameTimeMs = timed.Count == 0 ? null : timed.Average(r => r.CPUFrameTime),
            AverageGpuFrameTimeMs = timed.Count == 0 ? null : timed.Average(r => r.GPUFrameTime),
            CpuBoundSharePercent = timed.Count == 0
                ? null
                : 100.0 * timed.Count(r => r.CPUFrameTime > r.GPUFrameTime) / timed.Count,
            FrameCount = file.Records.Count,
            GameVersion = file.GameVer,
            Reported = new ReportedSettings(
                file.ScreenResolution,
                file.QualityLevel,
                file.ImageQuality,
                file.Rtx != 0,
                file.InsertFrame != 0),
        };
    }

    /// <summary>Перцентиль методом ближайшего ранга (как считают 1% low утилиты вроде CapFrameX).</summary>
    internal static double Percentile(IEnumerable<double> values, double percent)
    {
        var sorted = values.Order().ToArray();
        if (sorted.Length == 0) throw new ArgumentException("Пустая выборка", nameof(values));

        var rank = (int)Math.Ceiling(percent / 100.0 * sorted.Length);
        return sorted[Math.Clamp(rank - 1, 0, sorted.Length - 1)];
    }
}

public sealed class ResultParseException(string message, Exception? inner = null) : Exception(message, inner);
