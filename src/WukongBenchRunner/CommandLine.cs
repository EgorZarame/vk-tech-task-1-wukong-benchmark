using System.Globalization;
using WukongBenchRunner.Profiles;

namespace WukongBenchRunner;

public sealed record CommandLine
{
    public string? BenchmarkDirectory { get; init; }
    public string OutputDirectory { get; init; } = Path.Combine(Environment.CurrentDirectory, "results");
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(15);
    public IReadOnlyList<BenchmarkProfile> Profiles { get; init; } = ProfileCatalog.All;

    /// <summary>Режим без запуска игры: отчёт по уже готовым JSON (CPU, GPU).</summary>
    public (string Cpu, string Gpu)? FromResults { get; init; }

    public bool ShowHelp { get; init; }

    public const string Usage = """
        Использование: WukongBenchRunner [параметры]

          --dir <путь>              Папка Black Myth Wukong Benchmark Tool (по умолчанию ищется через Steam)
          --only <cpu|gpu>          Выполнить только один проход
          --timeout <минуты>        Таймаут одного прохода (по умолчанию 15)
          --output <путь>           Куда сохранить отчёт (по умолчанию ./results)
          --from-results <cpu.json> <gpu.json>
                                    Построить отчёт по готовым файлам результатов, не запуская бенчмарк
          -h, --help                Справка
        """;

    /// <exception cref="ArgumentException">Неверные аргументы.</exception>
    public static CommandLine Parse(IReadOnlyList<string> args)
    {
        var result = new CommandLine();
        for (var i = 0; i < args.Count; i++)
        {
            result = args[i] switch
            {
                "-h" or "--help" or "/?" => result with { ShowHelp = true },
                "--dir" => result with { BenchmarkDirectory = Next(args, ref i) },
                "--output" => result with { OutputDirectory = Path.GetFullPath(Next(args, ref i)) },
                "--timeout" => result with { Timeout = TimeSpan.FromMinutes(ParseMinutes(Next(args, ref i))) },
                "--only" => result with { Profiles = [ParseProfile(Next(args, ref i))] },
                "--from-results" => result with { FromResults = (Next(args, ref i), Next(args, ref i)) },
                // Путь без ключа — удобно перетащить папку на exe.
                var path when !path.StartsWith('-') && result.BenchmarkDirectory is null =>
                    result with { BenchmarkDirectory = path },
                var unknown => throw new ArgumentException($"Неизвестный параметр: {unknown}"),
            };
        }
        return result;
    }

    private static string Next(IReadOnlyList<string> args, ref int i)
    {
        if (i + 1 >= args.Count) throw new ArgumentException($"После {args[i]} нужно значение");
        return args[++i];
    }

    private static double ParseMinutes(string value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var minutes) && minutes > 0
            ? minutes
            : throw new ArgumentException($"Некорректный таймаут: {value}");

    private static BenchmarkProfile ParseProfile(string value) => value.ToLowerInvariant() switch
    {
        "cpu" => ProfileCatalog.Cpu,
        "gpu" => ProfileCatalog.Gpu,
        _ => throw new ArgumentException($"--only принимает cpu или gpu, получено: {value}"),
    };
}
