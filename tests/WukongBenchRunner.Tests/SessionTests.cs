using WukongBenchRunner.Profiles;
using WukongBenchRunner.Reporting;
using WukongBenchRunner.Running;

namespace WukongBenchRunner.Tests;

public class BenchmarkSessionTests
{
    private readonly FakeConfig _config = new();
    private readonly NullLog _log = new();

    private sealed class ScriptedRunner(params Func<RunOutcome>[] script) : IBenchmarkRunner
    {
        private int _call;

        public Task<RunOutcome> RunAsync(string executablePath, CancellationToken cancellationToken) =>
            Task.FromResult(script[_call++]());
    }

    private BenchmarkSession Create(IBenchmarkRunner runner) =>
        new(_config, runner, new FakeSystemInfo(), TimeProvider.System, _log);

    [Fact]
    public async Task Run_AppliesEachProfileThenRestoresConfig()
    {
        var runner = new ScriptedRunner(
            () => new RunOutcome("cpu.json", SampleMetrics.Create(fps: 150, quality: 1), TimeSpan.FromMinutes(3)),
            () => new RunOutcome("gpu.json", SampleMetrics.Create(fps: 60, quality: 5, resolution: "1920 × 1080"),
                TimeSpan.FromMinutes(3)));

        var report = await Create(runner).RunAsync("b1.exe", ProfileCatalog.All, CancellationToken.None);

        Assert.Equal(["recover", "backup", "apply", "apply", "restore"], _config.Calls);
        Assert.Equal("1", _config.Applied[0].UiValues["QualityLevel"]);
        Assert.Equal("5", _config.Applied[1].UiValues["QualityLevel"]);
        Assert.All(report.Runs, r => Assert.True(r.Succeeded));
        Assert.All(report.Runs, r => Assert.Empty(r.Warnings));
        Assert.Equal("Test CPU", report.System.Cpu);
    }

    [Fact]
    public async Task Run_FirstPassFails_SecondStillRuns()
    {
        var runner = new ScriptedRunner(
            () => throw new BenchmarkRunException("таймаут"),
            () => new RunOutcome("gpu.json", SampleMetrics.Create(quality: 5, resolution: "1920 × 1080"), TimeSpan.Zero));

        var report = await Create(runner).RunAsync("b1.exe", ProfileCatalog.All, CancellationToken.None);

        Assert.False(report.Runs[0].Succeeded);
        Assert.Equal("таймаут", report.Runs[0].Error);
        Assert.True(report.Runs[1].Succeeded);
        Assert.Equal("restore", _config.Calls[^1]);
    }

    [Fact]
    public async Task Run_Cancelled_StillRestoresConfig()
    {
        var runner = new ScriptedRunner(() => throw new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            Create(runner).RunAsync("b1.exe", ProfileCatalog.All, CancellationToken.None));

        Assert.Equal("restore", _config.Calls[^1]);
    }

    [Fact]
    public async Task Run_SettingsNotApplied_ProducesWarning()
    {
        // Бенчмарк проигнорировал конфиг и отработал на «Высоком» в 1080p.
        var runner = new ScriptedRunner(
            () => new RunOutcome("cpu.json", SampleMetrics.Create(quality: 3, resolution: "1920x1080"), TimeSpan.Zero));

        var report = await Create(runner).RunAsync("b1.exe", [ProfileCatalog.Cpu], CancellationToken.None);

        Assert.Equal(2, report.Runs[0].Warnings.Count);
    }

    [Fact]
    public async Task Run_LeftoverBackup_IsRecoveredAndReported()
    {
        _config.HadCrashBackup = true;
        var runner = new ScriptedRunner(() => new RunOutcome("cpu.json", SampleMetrics.Create(), TimeSpan.Zero));

        await Create(runner).RunAsync("b1.exe", [ProfileCatalog.Cpu], CancellationToken.None);

        Assert.Contains(_log.Warnings, w => w.Contains("восстановлены"));
    }
}

public class ReportFilesTests
{
    [Fact]
    public async Task Save_WritesMarkdownJsonAndRawFiles()
    {
        using var temp = new TempDirectory();
        var runner = new FixedRunner(TestFiles.PathOf("cpu_sample.json"));
        var session = new BenchmarkSession(new FakeConfig(), runner, new FakeSystemInfo(), TimeProvider.System, new NullLog());
        var report = await session.RunAsync("b1.exe", [ProfileCatalog.Cpu], CancellationToken.None);

        var directory = ReportFiles.Save(report, temp.Path);

        var markdown = File.ReadAllText(Path.Combine(directory, "report.md"));
        Assert.Contains("| Средний FPS | 117.8 |", markdown);
        Assert.Contains("Test GPU", markdown);
        Assert.Contains("Почему такие настройки", markdown);
        Assert.Contains("\"AverageFps\": 117.8", File.ReadAllText(Path.Combine(directory, "report.json")));
        Assert.True(File.Exists(Path.Combine(directory, "cpu_raw.json")));
    }

    private sealed class FixedRunner(string file) : IBenchmarkRunner
    {
        public Task<RunOutcome> RunAsync(string executablePath, CancellationToken cancellationToken) =>
            Task.FromResult(new RunOutcome(file, new Results.ResultParser().ParseFile(file), TimeSpan.FromMinutes(2)));
    }
}
