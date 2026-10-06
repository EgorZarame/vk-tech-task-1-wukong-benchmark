using WukongBenchRunner.Results;

namespace WukongBenchRunner.Running;

public sealed record RunnerOptions
{
    /// <summary>Сам тест идёт ~2–3 минуты, остальное — запуск, компиляция шейдеров и меню.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(15);

    /// <summary>Пауза между действиями в меню: экраны бенчмарка появляются с анимацией.</summary>
    public TimeSpan StepInterval { get; init; } = TimeSpan.FromSeconds(3);

    /// <summary>Сколько проверок подряд процесса может не быть, прежде чем считать, что он упал.</summary>
    public int MissingProcessChecks { get; init; } = 10;
}

public sealed record RunOutcome(string ResultPath, BenchmarkMetrics Metrics, TimeSpan Duration);

public interface IBenchmarkRunner
{
    Task<RunOutcome> RunAsync(string executablePath, CancellationToken cancellationToken);
}

/// <summary>
/// Один прогон: запустить → проходить меню, пока не появится новый файл результата → закрыть.
/// </summary>
public sealed class BenchmarkRunner(
    IBenchmarkProcess process,
    IMenuNavigator navigator,
    IResultSource results,
    IDelay delay,
    TimeProvider time,
    RunnerOptions options,
    ILog log) : IBenchmarkRunner
{
    public async Task<RunOutcome> RunAsync(string executablePath, CancellationToken cancellationToken)
    {
        if (process.IsRunning())
        {
            throw new InvalidOperationException("Бенчмарк уже запущен. Закройте его и запустите инструмент снова.");
        }

        var known = results.Snapshot();
        var started = time.GetTimestamp();
        process.Start(executablePath);
        log.Info("Бенчмарк запущен, прохожу меню. Не трогайте мышь и клавиатуру.");

        try
        {
            var step = 0;
            var missingChecks = 0;
            while (time.GetElapsedTime(started) < options.Timeout)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Результат проверяем ДО действия: после теста открывается экран итогов,
                // и лишнее нажатие там запустило бы тест заново.
                if (results.TryFindNew(known) is { } found)
                {
                    var duration = time.GetElapsedTime(started);
                    log.Info($"Результат получен за {duration:mm\\:ss}: {Path.GetFileName(found.Path)}");
                    return new RunOutcome(found.Path, found.Metrics, duration);
                }

                // Лаунчер b1.exe передаёт управление игре, и процесса может ненадолго не быть —
                // считаем падением только несколько пустых проверок подряд.
                missingChecks = process.IsRunning() ? 0 : missingChecks + 1;
                if (missingChecks >= options.MissingProcessChecks)
                {
                    throw new BenchmarkRunException("Бенчмарк закрылся, не сохранив результат.");
                }

                if (navigator.TryAdvance(step)) step++;

                await delay.WaitAsync(options.StepInterval, cancellationToken);
            }

            throw new BenchmarkRunException(
                $"Бенчмарк не выдал результат за {options.Timeout.TotalMinutes:0} мин. " +
                "Возможно, меню выглядит иначе — запустите тест вручную один раз и проверьте.");
        }
        finally
        {
            process.KillAll();
        }
    }
}

public sealed class BenchmarkRunException(string message) : Exception(message);
