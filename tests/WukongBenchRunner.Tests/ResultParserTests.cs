using WukongBenchRunner.Results;
using WukongBenchRunner.Running;

namespace WukongBenchRunner.Tests;

public class ResultParserTests
{
    private readonly ResultParser _parser = new();

    [Fact]
    public void ParseFile_CpuSample_ReadsBenchmarkMetrics()
    {
        var metrics = _parser.ParseFile(TestFiles.PathOf("cpu_sample.json"));

        Assert.Equal(117.8, metrics.AverageFps);
        Assert.Equal(60.3, metrics.MinFps);
        Assert.Equal(98.3, metrics.Fps95);
        Assert.Equal(3000, metrics.FrameCount);
        Assert.Equal("1280 × 720", metrics.Reported.ScreenResolution);
        Assert.Equal(1, metrics.Reported.QualityLevel);
    }

    [Fact]
    public void ParseFile_CpuSample_IsCpuBound()
    {
        var metrics = _parser.ParseFile(TestFiles.PathOf("cpu_sample.json"));

        Assert.Equal("CPU", metrics.Bottleneck);
        Assert.True(metrics.AverageCpuFrameTimeMs > metrics.AverageGpuFrameTimeMs);
    }

    [Fact]
    public void ParseFile_GpuSample_IsGpuBound()
    {
        var metrics = _parser.ParseFile(TestFiles.PathOf("gpu_sample.json"));

        Assert.Equal("GPU", metrics.Bottleneck);
        Assert.True(metrics.AverageGpuUsage > 95);
    }

    [Fact]
    public void Parse_ComputesOnePercentLowFromRecords()
    {
        // 100 кадров: один медленный (10 FPS), остальные 60 → 1% low = 10.
        var records = string.Join(",", Enumerable.Range(0, 100)
            .Select(i => $$"""{"FrameRate": {{(i == 7 ? 10 : 60)}}, "CPUFrameTime": 5, "GPUFrameTime": 10}"""));
        var json = $$"""{"FPSAvg": 59.5, "FPSMin": 10, "FPSMax": 60, "FPS95": 60, "Records": [{{records}}]}""";

        var metrics = _parser.Parse(json);

        Assert.Equal(10, metrics.OnePercentLowFps);
        Assert.Equal(0, metrics.CpuBoundSharePercent);
    }

    [Fact]
    public void Parse_WithoutRecords_LeavesComputedMetricsEmpty()
    {
        var metrics = _parser.Parse("""{"FPSAvg": 80, "FPSMin": 50, "FPSMax": 120, "FPS95": 70}""");

        Assert.Equal(80, metrics.AverageFps);
        Assert.Null(metrics.OnePercentLowFps);
        Assert.Null(metrics.CpuBoundSharePercent);
        Assert.Equal("нет данных", metrics.Bottleneck);
    }

    [Fact]
    public void Parse_NumbersAsStrings_AreAccepted()
    {
        var metrics = _parser.Parse("""{"FPSAvg": "75.5", "fpsmin": "40"}""");

        Assert.Equal(75.5, metrics.AverageFps);
        Assert.Equal(40, metrics.MinFps);
    }

    [Theory]
    [InlineData("{ \"FPSAvg\": 1")]           // файл ещё дописывается
    [InlineData("not json")]
    [InlineData("{\"Something\": \"else\"}")] // чужой JSON без метрик
    [InlineData("{\"FPSAvg\": 0}")]
    public void Parse_InvalidInput_ThrowsResultParseException(string json)
    {
        Assert.Throws<ResultParseException>(() => _parser.Parse(json));
    }

    [Fact]
    public void ParseFile_MissingFile_ThrowsResultParseException()
    {
        Assert.Throws<ResultParseException>(() => _parser.ParseFile("/definitely/missing.json"));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(50, 50)]
    [InlineData(100, 100)]
    public void Percentile_NearestRank(double percent, double expected)
    {
        var values = Enumerable.Range(1, 100).Select(i => (double)i);

        Assert.Equal(expected, ResultParser.Percentile(values, percent));
    }
}

public class HistoryDirectoryResultSourceTests
{
    [Fact]
    public void TryFindNew_IgnoresFilesFromSnapshot()
    {
        using var temp = new TempDirectory();
        File.Copy(TestFiles.PathOf("cpu_sample.json"), temp.Combine("1700000000"));
        var source = new HistoryDirectoryResultSource(temp.Path, new ResultParser());

        var known = source.Snapshot();

        Assert.Null(source.TryFindNew(known));
    }

    [Fact]
    public void TryFindNew_ReturnsNewCompleteFile()
    {
        using var temp = new TempDirectory();
        var source = new HistoryDirectoryResultSource(temp.Path, new ResultParser());
        var known = source.Snapshot();
        File.Copy(TestFiles.PathOf("gpu_sample.json"), temp.Combine("1700000100"));

        var found = source.TryFindNew(known);

        Assert.NotNull(found);
        Assert.Equal(61.2, found.Value.Metrics.AverageFps);
    }

    [Fact]
    public void TryFindNew_SkipsHalfWrittenFile()
    {
        using var temp = new TempDirectory();
        var source = new HistoryDirectoryResultSource(temp.Path, new ResultParser());
        var known = source.Snapshot();
        File.WriteAllText(temp.Combine("1700000200"), "{\"FPSAvg\": 61.2, \"Records\": [");

        Assert.Null(source.TryFindNew(known));
    }

    [Fact]
    public void Snapshot_DirectoryDoesNotExistYet_IsEmpty()
    {
        var source = new HistoryDirectoryResultSource("/no/such/dir", new ResultParser());

        Assert.Empty(source.Snapshot());
        Assert.Null(source.TryFindNew(new HashSet<string>()));
    }
}
