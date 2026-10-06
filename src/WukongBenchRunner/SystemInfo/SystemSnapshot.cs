namespace WukongBenchRunner.SystemInfo;

public sealed record GpuInfo(string Name, string DriverVersion, string VideoMemory);

public sealed record SystemSnapshot(
    string OperatingSystem,
    string Cpu,
    string CpuCores,
    string CpuClock,
    IReadOnlyList<GpuInfo> Gpus,
    string Ram,
    string Motherboard,
    string Display)
{
    public const string NotAvailable = "N/A";

    public IReadOnlyList<(string Name, string Value)> Describe()
    {
        var rows = new List<(string, string)>
        {
            ("ОС", OperatingSystem),
            ("Процессор", Cpu),
            ("Ядра / потоки", CpuCores),
            ("Макс. частота CPU", CpuClock),
        };
        if (Gpus.Count == 0) rows.Add(("Видеокарта", NotAvailable));
        for (var i = 0; i < Gpus.Count; i++)
        {
            var suffix = Gpus.Count > 1 ? $" #{i + 1}" : "";
            rows.Add(($"Видеокарта{suffix}", Gpus[i].Name));
            rows.Add(($"  Видеопамять{suffix}", Gpus[i].VideoMemory));
            rows.Add(($"  Драйвер{suffix}", Gpus[i].DriverVersion));
        }
        rows.Add(("Оперативная память", Ram));
        rows.Add(("Материнская плата", Motherboard));
        rows.Add(("Монитор", Display));
        return rows;
    }
}

public interface ISystemInfoProvider
{
    SystemSnapshot Collect();
}
