using System.Runtime.InteropServices;

namespace WukongBenchRunner.SystemInfo;

/// <summary>Запасной вариант для не-Windows (режим отчёта по готовым файлам): только то, что даёт .NET.</summary>
public sealed class BasicSystemInfoProvider : ISystemInfoProvider
{
    public SystemSnapshot Collect()
    {
        var memory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        return new SystemSnapshot(
            OperatingSystem: $"{RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})",
            Cpu: SystemSnapshot.NotAvailable,
            CpuCores: $"? / {Environment.ProcessorCount}",
            CpuClock: SystemSnapshot.NotAvailable,
            Gpus: [],
            Ram: memory > 0 ? $"{memory / (double)(1L << 30):0.#} ГБ" : SystemSnapshot.NotAvailable,
            Motherboard: SystemSnapshot.NotAvailable,
            Display: SystemSnapshot.NotAvailable);
    }
}
