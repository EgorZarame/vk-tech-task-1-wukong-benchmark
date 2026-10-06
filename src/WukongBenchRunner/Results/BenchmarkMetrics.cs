namespace WukongBenchRunner.Results;

/// <summary>Итог одного прохода: метрики самого бенчмарка плюс посчитанные из покадровых данных.</summary>
public sealed record BenchmarkMetrics
{
    public required double AverageFps { get; init; }
    public required double MinFps { get; init; }
    public required double MaxFps { get; init; }
    public required double Fps95 { get; init; }

    /// <summary>1% low — FPS, ниже которого только 1% кадров. Null, если покадровых данных нет.</summary>
    public double? OnePercentLowFps { get; init; }

    public required double AverageCpuUsage { get; init; }
    public required double AverageGpuUsage { get; init; }

    public double? AverageCpuFrameTimeMs { get; init; }
    public double? AverageGpuFrameTimeMs { get; init; }

    /// <summary>Доля кадров, где процессор готовил кадр дольше, чем видеокарта его рисовала, %.</summary>
    public double? CpuBoundSharePercent { get; init; }

    public required int FrameCount { get; init; }

    public required ReportedSettings Reported { get; init; }

    public string? GameVersion { get; init; }

    /// <summary>Узкое место по покадровым данным: что дольше считало кадры.</summary>
    public string Bottleneck => CpuBoundSharePercent switch
    {
        null => "нет данных",
        >= 60 => "CPU",
        <= 40 => "GPU",
        _ => "смешанное",
    };
}

/// <summary>Настройки, которые бенчмарк сам записал в файл результата.</summary>
public sealed record ReportedSettings(
    string? ScreenResolution,
    int QualityLevel,
    int RenderScalePercent,
    bool RayTracing,
    bool FrameGeneration);
