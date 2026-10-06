using Spectre.Console;
using WukongBenchRunner.Running;

namespace WukongBenchRunner.Infrastructure;

public sealed class ConsoleLog(IAnsiConsole console) : ILog
{
    public void Info(string message) =>
        console.MarkupLine($"[grey]{DateTime.Now:HH:mm:ss}[/] {Markup.Escape(message)}");

    public void Warn(string message) =>
        console.MarkupLine($"[grey]{DateTime.Now:HH:mm:ss}[/] [yellow]{Markup.Escape(message)}[/]");
}
