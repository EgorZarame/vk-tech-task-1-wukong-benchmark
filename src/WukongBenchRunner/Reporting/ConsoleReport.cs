using Spectre.Console;
using WukongBenchRunner.Profiles;

namespace WukongBenchRunner.Reporting;

public sealed class ConsoleReport(IAnsiConsole console)
{
    public void Render(SessionReport report)
    {
        console.Write(new Rule("[bold yellow]Black Myth: Wukong Benchmark — отчёт[/]").LeftJustified());
        console.MarkupLine($"[grey]Запуск: {report.StartedAt:dd.MM.yyyy HH:mm:ss}[/]");
        console.WriteLine();

        console.Write(TwoColumns("Характеристики ПК", "Параметр", "Значение", report.System.Describe()));
        console.WriteLine();
        console.Write(Results(report.Runs));
        console.WriteLine();
        console.Write(Settings(report.Runs));

        foreach (var run in report.Runs)
        {
            console.WriteLine();
            console.MarkupLine($"[bold]Почему такие настройки для {Markup.Escape(run.Profile.Name)}:[/]");
            foreach (var reason in run.Profile.Rationale)
            {
                console.MarkupLine($"  • {Markup.Escape(reason)}");
            }
        }

        var problems = report.Runs
            .SelectMany(r => r.Warnings.Select(w => $"{r.Profile.Name}: {w}")
                .Concat(r.Error is null ? [] : [$"{r.Profile.Name}: {r.Error}"]))
            .ToList();
        if (problems.Count > 0)
        {
            console.WriteLine();
            console.MarkupLine("[bold red]Предупреждения:[/]");
            foreach (var problem in problems) console.MarkupLine($"  [red]![/] {Markup.Escape(problem)}");
        }
    }

    private static Table Results(IReadOnlyList<TestRun> runs)
    {
        var table = new Table().Border(TableBorder.Rounded).Title("[bold]Результаты[/]");
        table.AddColumn("Метрика");
        foreach (var run in runs) table.AddColumn(new TableColumn($"[bold]{Markup.Escape(run.Profile.Name)}[/]").RightAligned());

        foreach (var (metric, value) in ReportRows.Metrics)
        {
            table.AddRow([metric, .. runs.Select(r => r.Metrics is null ? "[red]ошибка[/]" : Markup.Escape(value(r.Metrics)))]);
        }
        return table;
    }

    private static Table Settings(IReadOnlyList<TestRun> runs)
    {
        var table = new Table().Border(TableBorder.Rounded).Title("[bold]Использованные настройки[/]");
        table.AddColumn("Настройка");
        foreach (var run in runs) table.AddColumn($"[bold]{Markup.Escape(run.Profile.Name)}[/]");

        var described = runs.Select(r => r.Profile.Settings.Describe()).ToList();
        for (var i = 0; i < described[0].Count; i++)
        {
            table.AddRow([described[0][i].Name, .. described.Select(d => Markup.Escape(d[i].Value))]);
        }
        return table;
    }

    private static Table TwoColumns(string title, string left, string right, IEnumerable<(string, string)> rows)
    {
        var table = new Table().Border(TableBorder.Rounded).Title($"[bold]{title}[/]");
        table.AddColumn(left);
        table.AddColumn(right);
        foreach (var (name, value) in rows) table.AddRow(Markup.Escape(name), Markup.Escape(value));
        return table;
    }

    internal static string KindLabel(TestKind kind) => kind == TestKind.Cpu ? "cpu" : "gpu";
}
