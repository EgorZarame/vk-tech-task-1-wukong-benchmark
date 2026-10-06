using WukongBenchRunner.Results;

namespace WukongBenchRunner.Running;

/// <summary>Следит за папкой BenchMarkHistory: каждый завершённый прогон — новый файл.</summary>
public sealed class HistoryDirectoryResultSource(string directory, IResultParser parser) : IResultSource
{
    public IReadOnlySet<string> Snapshot() =>
        Directory.Exists(directory)
            ? Directory.GetFiles(directory).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>();

    public (string Path, BenchmarkMetrics Metrics)? TryFindNew(IReadOnlySet<string> known)
    {
        if (!Directory.Exists(directory)) return null;

        var candidates = Directory.GetFiles(directory)
            .Where(f => !known.Contains(f))
            .OrderByDescending(File.GetLastWriteTimeUtc);

        foreach (var file in candidates)
        {
            try
            {
                return (file, parser.ParseFile(file));
            }
            catch (ResultParseException)
            {
                // Файл ещё дописывается — проверим на следующей итерации.
            }
        }
        return null;
    }
}
