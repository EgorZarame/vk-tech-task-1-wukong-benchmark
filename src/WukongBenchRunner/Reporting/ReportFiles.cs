using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace WukongBenchRunner.Reporting;

/// <summary>Сохраняет отчёт в папку: report.md (читать), report.json (обрабатывать), исходные JSON бенчмарка.</summary>
public static class ReportFiles
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Save(SessionReport report, string outputRoot)
    {
        var directory = Path.Combine(outputRoot, report.StartedAt.ToString("yyyy-MM-dd_HH-mm-ss"));
        Directory.CreateDirectory(directory);

        File.WriteAllText(Path.Combine(directory, "report.md"), ToMarkdown(report), Encoding.UTF8);
        File.WriteAllText(Path.Combine(directory, "report.json"), ToJson(report), Encoding.UTF8);

        foreach (var run in report.Runs.Where(r => r.ResultFile is not null && File.Exists(r.ResultFile)))
        {
            var name = $"{ConsoleReport.KindLabel(run.Profile.Kind)}_raw.json";
            File.Copy(run.ResultFile!, Path.Combine(directory, name), overwrite: true);
        }
        return directory;
    }

    public static string ToJson(SessionReport report) => JsonSerializer.Serialize(new
    {
        report.StartedAt,
        report.System,
        Runs = report.Runs.Select(r => new
        {
            r.Profile.Kind,
            r.Profile.Name,
            r.Profile.Settings,
            r.Metrics,
            r.ResultFile,
            DurationSeconds = r.Duration?.TotalSeconds,
            r.Error,
            r.Warnings,
        }),
    }, JsonOptions);

    public static string ToMarkdown(SessionReport report)
    {
        var md = new StringBuilder();
        md.AppendLine("# Black Myth: Wukong Benchmark — отчёт");
        md.AppendLine();
        md.AppendLine($"Запуск: {report.StartedAt:dd.MM.yyyy HH:mm:ss}");
        md.AppendLine();

        md.AppendLine("## Характеристики ПК");
        md.AppendLine();
        AppendTable(md, ["Параметр", "Значение"], report.System.Describe().Select(r => new[] { r.Name.Trim(), r.Value }));

        md.AppendLine("## Результаты");
        md.AppendLine();
        AppendTable(md,
            ["Метрика", .. report.Runs.Select(r => r.Profile.Name)],
            ReportRows.Metrics.Select(row => new[] { row.Metric }
                .Concat(report.Runs.Select(r => r.Metrics is null ? "ошибка" : row.Value(r.Metrics)))));

        md.AppendLine("## Настройки");
        md.AppendLine();
        var described = report.Runs.Select(r => r.Profile.Settings.Describe()).ToList();
        AppendTable(md,
            ["Настройка", .. report.Runs.Select(r => r.Profile.Name)],
            described[0].Select((row, i) => new[] { row.Name }.Concat(described.Select(d => d[i].Value))));

        foreach (var run in report.Runs)
        {
            md.AppendLine($"### Почему такие настройки: {run.Profile.Name}");
            md.AppendLine();
            foreach (var reason in run.Profile.Rationale) md.AppendLine($"- {reason}");
            md.AppendLine();

            if (run.Error is not null) md.AppendLine($"> **Ошибка:** {run.Error}").AppendLine();
            foreach (var warning in run.Warnings) md.AppendLine($"> ⚠️ {warning}").AppendLine();
        }
        return md.ToString();
    }

    private static void AppendTable(StringBuilder md, IReadOnlyList<string> header, IEnumerable<IEnumerable<string>> rows)
    {
        md.AppendLine("| " + string.Join(" | ", header) + " |");
        md.AppendLine("|" + string.Concat(header.Select(_ => "---|")));
        foreach (var row in rows) md.AppendLine("| " + string.Join(" | ", row.Select(c => c.Replace("|", "\\|"))) + " |");
        md.AppendLine();
    }
}
