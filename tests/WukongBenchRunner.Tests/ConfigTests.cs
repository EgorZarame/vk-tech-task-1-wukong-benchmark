using System.Text;
using WukongBenchRunner.Config;
using WukongBenchRunner.Profiles;

namespace WukongBenchRunner.Tests;

public class IniDocumentTests
{
    [Fact]
    public void Set_ExistingKey_ReplacesOnlyThatLine()
    {
        var doc = IniDocument.Parse("[A]\nx=1\ny=2\n[B]\nx=3");

        doc.Set("A", "x", "9");

        Assert.Equal("[A]\nx=9\ny=2\n[B]\nx=3", doc.ToString());
    }

    [Fact]
    public void Set_MissingKey_InsertsAtEndOfSectionBeforeBlankLine()
    {
        var doc = IniDocument.Parse("[A]\nx=1\n\n[B]\ny=2");

        doc.Set("A", "z", "5");

        Assert.Equal("[A]\nx=1\nz=5\n\n[B]\ny=2", doc.ToString());
    }

    [Fact]
    public void Set_MissingSection_AppendsSection()
    {
        var doc = IniDocument.Parse("[A]\nx=1");

        doc.Set("B", "y", "2");

        Assert.Equal("[A]\nx=1\n\n[B]\ny=2", doc.ToString());
    }

    [Fact]
    public void Parse_PreservesCrLf()
    {
        var doc = IniDocument.Parse("[A]\r\nx=1\r\n");

        doc.Set("A", "x", "2");

        Assert.Equal("[A]\r\nx=2\r\n", doc.ToString());
    }

    [Fact]
    public void Get_DoesNotReadKeyFromAnotherSection()
    {
        var doc = IniDocument.Parse("[A]\nx=1\n[B]\ny=2");

        Assert.Null(doc.Get("A", "y"));
        Assert.Equal("2", doc.Get("B", "y"));
    }
}

public class UiSettingDataTests
{
    private const string Raw = """(("ScreenMode", "1"),("Rtx", "0"),("QualityLevel", "3"))""";

    [Fact]
    public void Parse_ReadsAllPairs()
    {
        var values = UiSettingData.Parse(Raw);

        Assert.Equal(3, values.Count);
        Assert.Equal("3", values["QualityLevel"]);
    }

    [Fact]
    public void Apply_ChangesOnlyRequestedValues()
    {
        var result = UiSettingData.Apply(Raw, new Dictionary<string, string> { ["QualityLevel"] = "5" });

        Assert.Equal("""(("ScreenMode", "1"),("Rtx", "0"),("QualityLevel", "5"))""", result);
    }

    [Fact]
    public void Apply_UnknownKey_Throws()
    {
        var error = Assert.Throws<ConfigFormatException>(() =>
            UiSettingData.Apply(Raw, new Dictionary<string, string> { ["NoSuchKey"] = "1" }));

        Assert.Contains("NoSuchKey", error.Message);
    }
}

public class SettingsPatchTests
{
    private static IniDocument Apply(BenchmarkProfile profile) =>
        IniDocument.Parse(SettingsPatch.From(profile.Settings).ApplyTo(TestFiles.Read("GameUserSettings.ini")));

    private static IReadOnlyDictionary<string, string> Ui(IniDocument doc) =>
        UiSettingData.Parse(doc.Get(SettingsPatch.MainSection, SettingsPatch.UiSettingKey)!);

    [Fact]
    public void CpuProfile_SetsLowQualityAndLowResolution()
    {
        var doc = Apply(ProfileCatalog.Cpu);
        var ui = Ui(doc);

        Assert.Equal("1", ui["QualityLevel"]);
        Assert.All(SettingsPatch.UiQualityKeys, key => Assert.Equal("1", ui[key]));
        Assert.Equal("356", ui["ImageQuality"]);
        Assert.Equal("1280", doc.Get(SettingsPatch.MainSection, "ResolutionSizeX"));
        Assert.Equal("720", doc.Get(SettingsPatch.MainSection, "ResolutionSizeY"));
        Assert.Equal("50", doc.Get(SettingsPatch.ScalabilitySection, "sg.ResolutionQuality"));
    }

    [Fact]
    public void GpuProfile_SetsCinematicQualityAndFullRenderScale()
    {
        var doc = Apply(ProfileCatalog.Gpu);
        var ui = Ui(doc);

        Assert.Equal("5", ui["QualityLevel"]);
        Assert.All(SettingsPatch.UiQualityKeys, key => Assert.Equal("5", ui[key]));
        Assert.Equal("100", doc.Get(SettingsPatch.ScalabilitySection, "sg.ResolutionQuality"));
        Assert.Equal("1920", doc.Get(SettingsPatch.MainSection, "ResolutionSizeX"));
    }

    [Theory]
    [InlineData(TestKind.Cpu, "0")]
    [InlineData(TestKind.Gpu, "4")]
    public void ScalabilityGroups_AreMenuValueMinusOne(TestKind kind, string expected)
    {
        var doc = Apply(kind == TestKind.Cpu ? ProfileCatalog.Cpu : ProfileCatalog.Gpu);

        Assert.All(SettingsPatch.ScalabilityKeys,
            key => Assert.Equal(expected, doc.Get(SettingsPatch.ScalabilitySection, key)));
    }

    [Theory]
    [InlineData(TestKind.Cpu)]
    [InlineData(TestKind.Gpu)]
    public void BothProfiles_DisableEverythingThatDistortsResults(TestKind kind)
    {
        var doc = Apply(kind == TestKind.Cpu ? ProfileCatalog.Cpu : ProfileCatalog.Gpu);
        var ui = Ui(doc);

        Assert.Equal("0", ui["Vsync"]);
        Assert.Equal("0", ui["LockFrameRate"]);
        Assert.Equal("0", ui["InsertFrame"]);
        Assert.Equal("0", ui["MotionBlur"]);
        Assert.Equal("False", doc.Get(SettingsPatch.MainSection, "bUseVSync"));
        Assert.Equal("0.000000", doc.Get(SettingsPatch.MainSection, "FrameRateLimit"));
    }

    [Fact]
    public void Apply_KeepsUnrelatedSettings()
    {
        var doc = Apply(ProfileCatalog.Gpu);

        Assert.Equal("50", Ui(doc)["Brightness"]);
        Assert.Equal("5", doc.Get(SettingsPatch.MainSection, "Version"));
        Assert.Equal("False", doc.Get("/Script/Engine.GameUserSettings", "bUseDesiredScreenHeight"));
    }

    [Fact]
    public void Apply_ConfigWithoutUiSettingData_ThrowsClearError()
    {
        var patch = SettingsPatch.From(ProfileCatalog.Cpu.Settings);

        var error = Assert.Throws<ConfigFormatException>(() => patch.ApplyTo("[ScalabilityGroups]\nsg.ShadowQuality=1"));

        Assert.Contains("вручную", error.Message);
    }

    [Fact]
    public void GpuProfile_IsHeavierThanCpuProfileOnEveryAxis()
    {
        var cpu = ProfileCatalog.Cpu.Settings;
        var gpu = ProfileCatalog.Gpu.Settings;

        Assert.True(gpu.Quality > cpu.Quality);
        Assert.True(gpu.RenderScalePercent > cpu.RenderScalePercent);
        Assert.True(gpu.Width * gpu.Height > cpu.Width * cpu.Height);
    }
}

public class GameConfigFileTests
{
    [Fact]
    public void BackupApplyRestore_ReturnsOriginalBytes()
    {
        using var temp = new TempDirectory();
        var path = temp.Combine("GameUserSettings.ini");
        File.Copy(TestFiles.PathOf("GameUserSettings.ini"), path);
        var original = File.ReadAllBytes(path);
        var config = new GameConfigFile(path);

        config.Backup();
        config.Apply(SettingsPatch.From(ProfileCatalog.Gpu.Settings));
        Assert.NotEqual(original, File.ReadAllBytes(path));

        config.Restore();
        Assert.Equal(original, File.ReadAllBytes(path));
        Assert.False(File.Exists(config.BackupPath));
    }

    [Fact]
    public void RecoverAfterCrash_RestoresLeftoverBackup()
    {
        using var temp = new TempDirectory();
        var path = temp.Combine("GameUserSettings.ini");
        File.Copy(TestFiles.PathOf("GameUserSettings.ini"), path);
        var original = File.ReadAllText(path);
        var crashed = new GameConfigFile(path);
        crashed.Backup();
        crashed.Apply(SettingsPatch.From(ProfileCatalog.Cpu.Settings));
        // Инструмент «упал» здесь, Restore не вызван.

        var recovered = new GameConfigFile(path).RecoverAfterCrash();

        Assert.True(recovered);
        Assert.Equal(original, File.ReadAllText(path));
    }

    [Fact]
    public void RecoverAfterCrash_WithoutBackup_DoesNothing()
    {
        using var temp = new TempDirectory();

        Assert.False(new GameConfigFile(temp.Combine("missing.ini")).RecoverAfterCrash());
    }

    [Fact]
    public void Apply_PreservesUtf16Encoding()
    {
        using var temp = new TempDirectory();
        var path = temp.Combine("GameUserSettings.ini");
        File.WriteAllText(path, TestFiles.Read("GameUserSettings.ini"), Encoding.Unicode);

        new GameConfigFile(path).Apply(SettingsPatch.From(ProfileCatalog.Cpu.Settings));

        var bytes = File.ReadAllBytes(path);
        Assert.Equal(0xFF, bytes[0]);
        Assert.Equal(0xFE, bytes[1]);
        Assert.Contains("ResolutionSizeX=1280", File.ReadAllText(path, Encoding.Unicode));
    }

    [Fact]
    public void Backup_MissingConfig_ExplainsWhatToDo()
    {
        using var temp = new TempDirectory();

        var error = Assert.Throws<FileNotFoundException>(() => new GameConfigFile(temp.Combine("x.ini")).Backup());

        Assert.Contains("вручную", error.Message);
    }
}
