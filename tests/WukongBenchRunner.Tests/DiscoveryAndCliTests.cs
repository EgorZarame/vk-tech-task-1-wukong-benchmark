using WukongBenchRunner.Discovery;
using WukongBenchRunner.Profiles;

namespace WukongBenchRunner.Tests;

public class SteamLibraryLocatorTests
{
    private const string LibraryFolders = """
        "libraryfolders"
        {
            "0"
            {
                "path"      "C:\\Program Files (x86)\\Steam"
                "label"     ""
                "apps"
                {
                    "228980"        "0"
                }
            }
            "1"
            {
                "path"      "D:\\SteamLibrary"
                "apps"
                {
                    "3132990"       "8123456789"
                }
            }
        }
        """;

    [Fact]
    public void ParseLibraryFolders_ReadsAllPathsAndUnescapes()
    {
        var paths = SteamLibraryLocator.ParseLibraryFolders(LibraryFolders);

        Assert.Equal([@"C:\Program Files (x86)\Steam", @"D:\SteamLibrary"], paths);
    }

    [Fact]
    public void ParseInstallDir_ReadsFolderName()
    {
        const string acf = """
            "AppState"
            {
                "appid"     "3132990"
                "name"      "Black Myth: Wukong Benchmark Tool"
                "installdir"        "Black Myth Wukong Benchmark Tool"
            }
            """;

        Assert.Equal("Black Myth Wukong Benchmark Tool", SteamLibraryLocator.ParseInstallDir(acf));
    }

    [Fact]
    public void Locate_FindsBenchmarkInSecondaryLibrary()
    {
        using var temp = new TempDirectory();
        var steam = temp.Combine("Steam");
        var library = temp.Combine("SteamLibrary");
        Directory.CreateDirectory(Path.Combine(steam, "steamapps"));
        File.WriteAllText(Path.Combine(steam, "steamapps", "libraryfolders.vdf"),
            $"\"libraryfolders\"\n{{\n \"0\" {{ \n  \"path\"  \"{steam.Replace(@"\", @"\\")}\"\n }}\n \"1\" {{\n  \"path\"  \"{library.Replace(@"\", @"\\")}\"\n }}\n}}");
        var root = Path.Combine(library, "steamapps", "common", "Wukong Bench");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "b1.exe"), "");
        File.WriteAllText(Path.Combine(library, "steamapps", "appmanifest_3132990.acf"),
            "\"AppState\"\n{\n \"installdir\"  \"Wukong Bench\"\n}");

        var (installation, _) = new SteamLibraryLocator(() => steam).Locate();

        Assert.NotNull(installation);
        Assert.Equal(Path.Combine(root, "b1.exe"), installation.ExecutablePath);
        Assert.EndsWith(Path.Combine("b1", "Saved", "Config", "Windows", "GameUserSettings.ini"), installation.ConfigPath);
    }

    [Fact]
    public void Locate_NotInstalled_ReportsSearchedPaths()
    {
        using var temp = new TempDirectory();

        var (installation, searched) = new SteamLibraryLocator(() => temp.Path).Locate();

        Assert.Null(installation);
        Assert.Single(searched);
    }
}

public class CommandLineTests
{
    [Fact]
    public void Parse_Defaults_RunBothProfiles()
    {
        var options = CommandLine.Parse([]);

        Assert.Equal(ProfileCatalog.All, options.Profiles);
        Assert.Null(options.BenchmarkDirectory);
        Assert.Equal(TimeSpan.FromMinutes(15), options.Timeout);
    }

    [Fact]
    public void Parse_AllOptions()
    {
        var options = CommandLine.Parse(["--dir", @"D:\Bench", "--only", "gpu", "--timeout", "20"]);

        Assert.Equal(@"D:\Bench", options.BenchmarkDirectory);
        Assert.Equal([ProfileCatalog.Gpu], options.Profiles);
        Assert.Equal(TimeSpan.FromMinutes(20), options.Timeout);
    }

    [Fact]
    public void Parse_PositionalPath_IsBenchmarkDirectory()
    {
        Assert.Equal(@"D:\Bench", CommandLine.Parse([@"D:\Bench"]).BenchmarkDirectory);
    }

    [Fact]
    public void Parse_FromResults_TakesTwoFiles()
    {
        var options = CommandLine.Parse(["--from-results", "a.json", "b.json"]);

        Assert.Equal(("a.json", "b.json"), options.FromResults);
    }

    [Theory]
    [InlineData("--only", "ram")]
    [InlineData("--timeout", "-5")]
    [InlineData("--unknown")]
    [InlineData("--dir")]
    public void Parse_InvalidArguments_Throw(params string[] args)
    {
        Assert.Throws<ArgumentException>(() => CommandLine.Parse(args));
    }
}
