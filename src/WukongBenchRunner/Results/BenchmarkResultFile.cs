namespace WukongBenchRunner.Results;

/// <summary>
/// JSON, который бенчмарк пишет в <c>%TEMP%\b1\BenchMarkHistory\Tool</c>.
/// Имена свойств совпадают с ключами файла.
/// </summary>
public sealed class BenchmarkResultFile
{
    public double FPSAvg { get; set; }
    public double FPSMax { get; set; }
    public double FPSMin { get; set; }

    /// <summary>FPS, который держится в 95% времени (аналог «5% low»).</summary>
    public double FPS95 { get; set; }

    public double CPUAvg { get; set; }
    public double GPUAvg { get; set; }
    public double VideoMem { get; set; }

    public string? GameVer { get; set; }
    public string? SysVer { get; set; }
    public string? CPUModel { get; set; }
    public string? GPUModel { get; set; }
    public string? GpuDriverVer { get; set; }
    public string? VideoMemSize { get; set; }
    public string? SysMem { get; set; }

    // Настройки, с которыми бенчмарк реально отработал, — по ним проверяем, что конфиг применился.
    public int ScreenMode { get; set; }
    public string? ScreenResolution { get; set; }
    public int QualityLevel { get; set; }
    public int ImageQuality { get; set; }
    public int Rtx { get; set; }
    public int InsertFrame { get; set; }

    public List<FrameRecord> Records { get; set; } = [];
}

public sealed class FrameRecord
{
    public double FrameRate { get; set; }
    public double CPUUsage { get; set; }
    public double GPUUsage { get; set; }
    public double CPUFrameTime { get; set; }
    public double GPUFrameTime { get; set; }
    public double VideoMemoryUsage { get; set; }
}
