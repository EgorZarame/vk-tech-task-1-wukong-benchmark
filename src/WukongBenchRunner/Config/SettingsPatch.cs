using System.Globalization;
using WukongBenchRunner.Profiles;

namespace WukongBenchRunner.Config;

/// <summary>Набор изменений GameUserSettings.ini для одного профиля.</summary>
/// <param name="UiValues">Ключи внутри UISettingData.</param>
/// <param name="IniValues">Обычные ключи ini: (секция, ключ) → значение.</param>
public sealed record SettingsPatch(
    IReadOnlyDictionary<string, string> UiValues,
    IReadOnlyDictionary<(string Section, string Key), string> IniValues)
{
    public const string MainSection = "/Script/GSGameSettings.GSGameUserSettings";
    public const string ScalabilitySection = "ScalabilityGroups";
    public const string UiSettingKey = "UISettingData";

    // Параметры качества в меню (значения 1..5).
    internal static readonly string[] UiQualityKeys =
    [
        "ViewDistance", "AntiAliasing", "PostProcessing", "ShadowQuality", "TextureQuality",
        "FxQuality", "MaterialQuality", "VegetationQuality", "GlobalIllumination", "ReflectionQuality",
    ];

    // Группы качества Unreal Engine (значения 0..4 = значение меню − 1).
    internal static readonly string[] ScalabilityKeys =
    [
        "sg.ViewDistanceQuality", "sg.AntiAliasingQuality", "sg.ShadowQuality", "sg.GlobalIlluminationQuality",
        "sg.ReflectionQuality", "sg.PostProcessQuality", "sg.TextureQuality", "sg.EffectsQuality",
        "sg.FoliageQuality", "sg.ShadingQuality",
    ];

    /// <summary>
    /// Настройки дублируются в двух местах: меню читает UISettingData, движок — ScalabilityGroups
    /// и ключи GSGameUserSettings. Пишем в оба, иначе при старте одно перезапишет другое.
    /// </summary>
    public static SettingsPatch From(GraphicsSettings s)
    {
        var quality = (int)s.Quality;
        var ui = new Dictionary<string, string>
        {
            ["ScreenMode"] = "1",                 // полноэкранное окно
            ["ScreenResolution"] = Str(s.ResolutionIndex),
            ["ImageQuality"] = Str(s.RenderHeight),
            ["WindowFullImageQuality"] = "0",
            ["SuperResolutionSampling"] = "3",    // апскейлер по умолчанию; при 100% он не снижает разрешение
            ["Vsync"] = Bool(s.VSync),
            ["LockFrameRate"] = Bool(s.FrameRateLimit),
            ["MotionBlur"] = Bool(s.MotionBlur),
            ["InsertFrame"] = Bool(s.FrameGeneration),
            ["Rtx"] = Bool(s.RayTracing),
            ["QualityLevel"] = Str(quality),
        };
        foreach (var key in UiQualityKeys) ui[key] = Str(quality);

        var ini = new Dictionary<(string, string), string>
        {
            [(MainSection, "FullscreenMode")] = "1",
            [(MainSection, "LastConfirmedFullscreenMode")] = "1",
            [(MainSection, "PreferredFullscreenMode")] = "1",
            [(MainSection, "ResolutionSizeX")] = Str(s.Width),
            [(MainSection, "ResolutionSizeY")] = Str(s.Height),
            [(MainSection, "LastUserConfirmedResolutionSizeX")] = Str(s.Width),
            [(MainSection, "LastUserConfirmedResolutionSizeY")] = Str(s.Height),
            [(MainSection, "bUseVSync")] = s.VSync ? "True" : "False",
            [(MainSection, "FrameRateLimit")] = "0.000000",
            [(ScalabilitySection, "sg.ResolutionQuality")] = Str(s.RenderScalePercent),
        };
        foreach (var key in ScalabilityKeys) ini[(ScalabilitySection, key)] = Str(quality - 1);

        return new SettingsPatch(ui, ini);
    }

    /// <summary>Применяет изменения к тексту ini и возвращает новый текст.</summary>
    public string ApplyTo(string iniText)
    {
        var doc = IniDocument.Parse(iniText);

        var uiRaw = doc.Get(MainSection, UiSettingKey)
            ?? throw new ConfigFormatException(
                $"В [{MainSection}] нет {UiSettingKey}. Запустите бенчмарк вручную хотя бы раз, " +
                "чтобы он создал полный конфиг.");
        doc.Set(MainSection, UiSettingKey, UiSettingData.Apply(uiRaw, UiValues));

        foreach (var ((section, key), value) in IniValues)
        {
            doc.Set(section, key, value);
        }
        return doc.ToString();
    }

    private static string Str(int value) => value.ToString(CultureInfo.InvariantCulture);
    private static string Bool(bool value) => value ? "1" : "0";
}
