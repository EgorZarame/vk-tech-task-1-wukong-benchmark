using System.Text;
using Spectre.Console;
using WukongBenchRunner;
using WukongBenchRunner.Config;
using WukongBenchRunner.Discovery;
using WukongBenchRunner.Infrastructure;
using WukongBenchRunner.Profiles;
using WukongBenchRunner.Reporting;
using WukongBenchRunner.Results;
using WukongBenchRunner.Running;
using WukongBenchRunner.SystemInfo;

Console.OutputEncoding = Encoding.UTF8;
var console = AnsiConsole.Console;
var log = new ConsoleLog(console);

CommandLine options;
try
{
    options = CommandLine.Parse(args);
}
catch (ArgumentException e)
{
    console.MarkupLine($"[red]{Markup.Escape(e.Message)}[/]");
    console.WriteLine(CommandLine.Usage);
    return 1;
}

if (options.ShowHelp)
{
    console.WriteLine(CommandLine.Usage);
    return 0;
}

ISystemInfoProvider systemInfo = OperatingSystem.IsWindows()
    ? new WindowsSystemInfoProvider()
    : new BasicSystemInfoProvider();

if (options.FromResults is var (cpuFile, gpuFile))
{
    return RenderFromFiles(cpuFile, gpuFile);
}

if (!OperatingSystem.IsWindows())
{
    console.MarkupLine("[yellow]Black Myth: Wukong Benchmark Tool работает только на Windows — полный прогон здесь недоступен.[/]");
    console.MarkupLine("[grey]Чтобы посмотреть отчёт, используйте --from-results <cpu.json> <gpu.json> (примеры лежат в samples/).[/]");
    return 2;
}

var installation = LocateInstallation(options.BenchmarkDirectory);
if (installation is null) return 3;

log.Info($"Бенчмарк: {installation.ExecutablePath}");
log.Info($"Конфиг: {installation.ConfigPath}");

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    // Не убиваем процесс сразу: сначала finally закроет бенчмарк и вернёт конфиг.
    e.Cancel = true;
    log.Warn("Остановка… закрываю бенчмарк и восстанавливаю настройки.");
    cancellation.Cancel();
};

var process = new WindowsBenchmarkProcess();
var runner = new BenchmarkRunner(
    process,
    new MenuNavigator(process, new Win32Input(), new MenuLayout()),
    new HistoryDirectoryResultSource(installation.HistoryDirectory, new ResultParser()),
    new RealDelay(),
    TimeProvider.System,
    new RunnerOptions { Timeout = options.Timeout },
    log);
var session = new BenchmarkSession(new GameConfigFile(installation.ConfigPath), runner, systemInfo, TimeProvider.System, log);

SessionReport report;
try
{
    report = await session.RunAsync(installation.ExecutablePath, options.Profiles, cancellation.Token);
}
catch (OperationCanceledException)
{
    log.Warn("Прервано пользователем.");
    return 130;
}
catch (Exception e) when (e is FileNotFoundException or InvalidOperationException)
{
    console.MarkupLine($"[red]{Markup.Escape(e.Message)}[/]");
    return 4;
}

return Finish(report);

int RenderFromFiles(string cpu, string gpu)
{
    var parser = new ResultParser();
    var runs = new List<TestRun>();
    foreach (var (profile, file) in new[] { (ProfileCatalog.Cpu, cpu), (ProfileCatalog.Gpu, gpu) })
    {
        try
        {
            var metrics = parser.ParseFile(file);
            runs.Add(new TestRun(profile, metrics, Path.GetFullPath(file), null, null,
                SettingsVerifier.Verify(profile.Settings, metrics.Reported)));
        }
        catch (ResultParseException e)
        {
            runs.Add(new TestRun(profile, null, null, null, e.Message, []));
        }
    }
    return Finish(new SessionReport(DateTimeOffset.Now, systemInfo.Collect(), runs));
}

int Finish(SessionReport report)
{
    console.WriteLine();
    new ConsoleReport(console).Render(report);
    var saved = ReportFiles.Save(report, options.OutputDirectory);
    console.WriteLine();
    console.MarkupLine($"[green]Отчёт сохранён:[/] {Markup.Escape(saved)}");
    return report.Runs.All(r => r.Succeeded) ? 0 : 5;
}

BenchmarkInstallation? LocateInstallation(string? directory)
{
    if (directory is not null)
    {
        var manual = BenchmarkInstallation.FromRoot(directory);
        if (manual is null) console.MarkupLine($"[red]В папке {Markup.Escape(directory)} нет b1.exe.[/]");
        return manual;
    }

    var (found, searched) = new SteamLibraryLocator().Locate();
    if (found is not null) return found;

    console.MarkupLine("[red]Black Myth: Wukong Benchmark Tool не найден. Искал в:[/]");
    foreach (var path in searched) console.MarkupLine($"  [grey]{Markup.Escape(path)}[/]");
    console.MarkupLine("Укажите папку вручную: [bold]WukongBenchRunner --dir \"D:\\SteamLibrary\\steamapps\\common\\Black Myth Wukong Benchmark Tool\"[/]");
    return null;
}
