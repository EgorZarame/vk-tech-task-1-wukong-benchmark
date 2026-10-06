using System.ComponentModel;
using System.Diagnostics;
using WukongBenchRunner.Discovery;

namespace WukongBenchRunner.Running;

public sealed class WindowsBenchmarkProcess : IBenchmarkProcess
{
    public bool IsRunning() => Find().Any();

    public void Start(string executablePath)
    {
        using var _ = Process.Start(new ProcessStartInfo(executablePath)
        {
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(executablePath),
        });
    }

    public nint FindMainWindow()
    {
        foreach (var process in Find())
        {
            using (process)
            {
                if (process.MainWindowHandle != 0) return process.MainWindowHandle;
            }
        }
        return 0;
    }

    public void KillAll()
    {
        foreach (var process in Find())
        {
            using (process)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(TimeSpan.FromSeconds(15));
                }
                catch (Exception e) when (e is InvalidOperationException or Win32Exception)
                {
                    // Процесс уже завершился сам.
                }
            }
        }
    }

    private static IEnumerable<Process> Find() =>
        BenchmarkInstallation.ProcessNames.SelectMany(Process.GetProcessesByName);
}
