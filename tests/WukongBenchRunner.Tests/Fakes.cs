using WukongBenchRunner.Config;
using WukongBenchRunner.Results;
using WukongBenchRunner.Running;
using WukongBenchRunner.SystemInfo;

namespace WukongBenchRunner.Tests;

internal static class TestFiles
{
    public static string PathOf(string name) => Path.Combine(AppContext.BaseDirectory, "TestData", name);

    public static string Read(string name) => File.ReadAllText(PathOf(name));
}

/// <summary>Временная папка, удаляется после теста.</summary>
internal sealed class TempDirectory : IDisposable
{
    public string Path { get; } = Directory.CreateTempSubdirectory("wbr-tests-").FullName;

    public string Combine(params string[] parts) => System.IO.Path.Combine([Path, .. parts]);

    public void Dispose() => Directory.Delete(Path, recursive: true);
}

/// <summary>Время, которое двигается только вместе с FakeDelay.</summary>
internal sealed class FakeTime : TimeProvider
{
    private long _ticks;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() => _ticks;

    public void Advance(TimeSpan duration) => _ticks += duration.Ticks;
}

internal sealed class FakeDelay(FakeTime time) : IDelay
{
    public int Calls { get; private set; }

    public Action<int>? OnWait { get; set; }

    public Task WaitAsync(TimeSpan duration, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls++;
        time.Advance(duration);
        OnWait?.Invoke(Calls);
        return Task.CompletedTask;
    }
}

internal sealed class FakeProcess : IBenchmarkProcess
{
    public bool Running { get; set; }
    public nint Window { get; set; } = 42;
    public int Starts { get; private set; }
    public int Kills { get; private set; }

    public bool IsRunning() => Running;

    public void Start(string executablePath)
    {
        Starts++;
        Running = true;
    }

    public nint FindMainWindow() => Running ? Window : 0;

    public void KillAll()
    {
        Kills++;
        Running = false;
    }
}

internal sealed class FakeNavigator : IMenuNavigator
{
    public List<int> Steps { get; } = [];
    public bool Available { get; set; } = true;

    public bool TryAdvance(int step)
    {
        if (!Available) return false;
        Steps.Add(step);
        return true;
    }
}

internal sealed class FakeResultSource : IResultSource
{
    public (string, BenchmarkMetrics)? Pending { get; set; }

    public IReadOnlySet<string> Snapshot() => new HashSet<string>();

    public (string Path, BenchmarkMetrics Metrics)? TryFindNew(IReadOnlySet<string> known) => Pending;
}

internal sealed class FakeInput : IInputSimulator
{
    public bool Foreground { get; set; } = true;
    public bool CanActivate { get; set; } = true;
    public List<string> Actions { get; } = [];

    public bool IsForeground(nint window) => Foreground;

    public bool TryActivate(nint window)
    {
        Actions.Add("activate");
        Foreground = CanActivate;
        return CanActivate;
    }

    public void PressKey(ushort virtualKey) => Actions.Add($"key:{virtualKey}");

    public void ClickRelative(nint window, double x, double y) => Actions.Add($"click:{x}:{y}");
}

internal sealed class FakeConfig : IGameConfig
{
    public List<string> Calls { get; } = [];
    public List<SettingsPatch> Applied { get; } = [];
    public bool HadCrashBackup { get; set; }

    public bool RecoverAfterCrash()
    {
        Calls.Add("recover");
        return HadCrashBackup;
    }

    public void Backup() => Calls.Add("backup");

    public void Apply(SettingsPatch patch)
    {
        Calls.Add("apply");
        Applied.Add(patch);
    }

    public void Restore() => Calls.Add("restore");
}

internal sealed class FakeSystemInfo : ISystemInfoProvider
{
    public SystemSnapshot Collect() => new("Windows 11", "Test CPU", "8 / 16", "4500 МГц",
        [new GpuInfo("Test GPU", "1.0", "12 ГБ")], "32 ГБ", "Test Board", "1920×1080 @ 144 Гц");
}

internal sealed class NullLog : ILog
{
    public List<string> Warnings { get; } = [];

    public void Info(string message)
    {
    }

    public void Warn(string message) => Warnings.Add(message);
}

internal static class SampleMetrics
{
    public static BenchmarkMetrics Create(double fps = 100, int quality = 1, string resolution = "1280 × 720") => new()
    {
        AverageFps = fps,
        MinFps = fps / 2,
        MaxFps = fps * 1.5,
        Fps95 = fps * 0.8,
        AverageCpuUsage = 70,
        AverageGpuUsage = 40,
        FrameCount = 100,
        Reported = new ReportedSettings(resolution, quality, 50, false, false),
    };
}
