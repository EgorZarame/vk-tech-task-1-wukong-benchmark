namespace WukongBenchRunner.Profiles;

public enum TestKind
{
    Cpu,
    Gpu,
}

/// <summary>Пресеты качества так, как они пронумерованы в меню бенчмарка.</summary>
public enum QualityPreset
{
    Low = 1,
    Medium = 2,
    High = 3,
    VeryHigh = 4,
    Cinematic = 5,
}

/// <summary>Графические настройки одного прохода в терминах меню бенчмарка.</summary>
/// <param name="ResolutionIndex">Индекс разрешения в выпадающем списке меню (0 — верхний пункт).</param>
/// <param name="RenderHeight">Высота внутреннего рендера в пикселях — так бенчмарк хранит «масштаб рендеринга».</param>
public sealed record GraphicsSettings(
    int Width,
    int Height,
    int ResolutionIndex,
    int RenderScalePercent,
    int RenderHeight,
    QualityPreset Quality,
    bool VSync = false,
    bool FrameRateLimit = false,
    bool MotionBlur = false,
    bool FrameGeneration = false,
    bool RayTracing = false)
{
    public string ResolutionText => $"{Width}×{Height}";

    /// <summary>Человекочитаемое описание для отчёта.</summary>
    public IReadOnlyList<(string Name, string Value)> Describe() =>
    [
        ("Разрешение", ResolutionText),
        ("Режим экрана", "Полноэкранное окно"),
        ("Масштаб рендеринга", $"{RenderScalePercent}% (внутренний рендер ~{RenderHeight}p)"),
        ("Качество (все параметры)", $"{QualityName(Quality)} ({(int)Quality} из 5)"),
        ("Трассировка лучей", OnOff(RayTracing)),
        ("Генерация кадров", OnOff(FrameGeneration)),
        ("Вертикальная синхронизация", OnOff(VSync)),
        ("Ограничение FPS", OnOff(FrameRateLimit)),
        ("Размытие в движении", OnOff(MotionBlur)),
    ];

    public static string QualityName(QualityPreset preset) => preset switch
    {
        QualityPreset.Low => "Низкое",
        QualityPreset.Medium => "Среднее",
        QualityPreset.High => "Высокое",
        QualityPreset.VeryHigh => "Очень высокое",
        QualityPreset.Cinematic => "Кинематографическое",
        _ => preset.ToString(),
    };

    private static string OnOff(bool value) => value ? "Вкл" : "Выкл";
}

/// <param name="Rationale">Почему выбраны именно такие настройки — попадает в отчёт.</param>
public sealed record BenchmarkProfile(
    TestKind Kind,
    string Name,
    GraphicsSettings Settings,
    IReadOnlyList<string> Rationale);
