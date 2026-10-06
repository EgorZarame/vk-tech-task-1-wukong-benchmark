using WukongBenchRunner.Results;

namespace WukongBenchRunner.Running;

/// <summary>Процесс бенчмарка.</summary>
public interface IBenchmarkProcess
{
    bool IsRunning();

    void Start(string executablePath);

    /// <summary>Главное окно игры или 0, если окна ещё нет.</summary>
    nint FindMainWindow();

    /// <summary>
    /// Принудительно завершает все процессы бенчмарка. Именно Kill, а не штатный выход:
    /// при выходе игра сохраняет свои настройки поверх нашего ini.
    /// </summary>
    void KillAll();
}

/// <summary>Продвигает меню бенчмарка к запуску теста.</summary>
public interface IMenuNavigator
{
    /// <summary>Делает очередное действие в меню. False, если окно недоступно и ничего не нажато.</summary>
    bool TryAdvance(int step);
}

/// <summary>Источник файлов результата.</summary>
public interface IResultSource
{
    /// <summary>Запоминает уже существующие файлы, чтобы не принять старый прогон за новый.</summary>
    IReadOnlySet<string> Snapshot();

    /// <summary>Новый завершённый результат или null, если его пока нет.</summary>
    (string Path, BenchmarkMetrics Metrics)? TryFindNew(IReadOnlySet<string> known);
}

/// <summary>Задержка — вынесена, чтобы тесты не ждали реальные секунды.</summary>
public interface IDelay
{
    Task WaitAsync(TimeSpan duration, CancellationToken cancellationToken);
}

public sealed class RealDelay : IDelay
{
    public Task WaitAsync(TimeSpan duration, CancellationToken cancellationToken) =>
        Task.Delay(duration, cancellationToken);
}

public interface ILog
{
    void Info(string message);

    void Warn(string message);
}
