using WukongBenchRunner.SystemInfo;

namespace WukongBenchRunner.Tests;

/// <summary>Проверки реального WMI — выполняются только на Windows (в CI на windows-latest).</summary>
public class WindowsOnlyTests
{
    [Fact]
    public void WindowsSystemInfoProvider_CollectsRealHardware()
    {
        if (!OperatingSystem.IsWindows()) return;

        var snapshot = new WindowsSystemInfoProvider().Collect();

        Assert.NotEqual(SystemSnapshot.NotAvailable, snapshot.OperatingSystem);
        Assert.NotEqual(SystemSnapshot.NotAvailable, snapshot.Cpu);
        Assert.NotEqual(SystemSnapshot.NotAvailable, snapshot.Ram);
        Assert.Contains("ГБ", snapshot.Ram);
    }
}
