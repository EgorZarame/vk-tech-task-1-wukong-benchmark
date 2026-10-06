using System.Globalization;
using System.Management;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace WukongBenchRunner.SystemInfo;

/// <summary>Характеристики ПК через WMI. Каждое поле собирается отдельно: сбой одного запроса не ломает отчёт.</summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsSystemInfoProvider : ISystemInfoProvider
{
    private const string DisplayAdaptersClassKey =
        @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";

    public SystemSnapshot Collect()
    {
        var cpu = Query("Win32_Processor").FirstOrDefault();
        var videoControllers = Query("Win32_VideoController")
            .Where(v => !string.IsNullOrWhiteSpace(Str(v, "Name")))
            .ToList();

        return new SystemSnapshot(
            OperatingSystem: Safe(OperatingSystemName),
            Cpu: cpu is null ? SystemSnapshot.NotAvailable : Str(cpu, "Name").Trim(),
            CpuCores: cpu is null
                ? SystemSnapshot.NotAvailable
                : $"{Str(cpu, "NumberOfCores")} / {Str(cpu, "NumberOfLogicalProcessors")}",
            CpuClock: cpu is null ? SystemSnapshot.NotAvailable : $"{Str(cpu, "MaxClockSpeed")} МГц",
            Gpus: videoControllers.Select(ToGpu).ToList(),
            Ram: Safe(RamDescription),
            Motherboard: Safe(() =>
            {
                var board = Query("Win32_BaseBoard").First();
                return $"{Str(board, "Manufacturer")} {Str(board, "Product")}".Trim();
            }),
            Display: Safe(() =>
            {
                var active = videoControllers.First(v => Str(v, "CurrentHorizontalResolution").Length > 0);
                return $"{Str(active, "CurrentHorizontalResolution")}×{Str(active, "CurrentVerticalResolution")} " +
                       $"@ {Str(active, "CurrentRefreshRate")} Гц";
            }));
    }

    private static string OperatingSystemName()
    {
        var os = Query("Win32_OperatingSystem").First();
        return $"{Str(os, "Caption")} (сборка {Str(os, "BuildNumber")}, {Str(os, "OSArchitecture")})";
    }

    private static string RamDescription()
    {
        var modules = Query("Win32_PhysicalMemory").ToList();
        var totalBytes = modules.Sum(m => Convert.ToDouble(m["Capacity"] ?? 0, CultureInfo.InvariantCulture));
        var speed = modules
            .Select(m => Str(m, "ConfiguredClockSpeed") is { Length: > 0 } s ? s : Str(m, "Speed"))
            .FirstOrDefault(s => s.Length > 0);

        var text = $"{totalBytes / (1L << 30):0.#} ГБ ({modules.Count} мод.)";
        return speed is null ? text : $"{text}, {speed} МТ/с";
    }

    private static GpuInfo ToGpu(ManagementBaseObject controller)
    {
        var name = Str(controller, "Name");
        return new GpuInfo(name, Str(controller, "DriverVersion"), FormatBytes(ReadVideoMemory(name, controller)));
    }

    /// <summary>
    /// AdapterRAM в WMI — uint32 и обрезается до 4 ГБ, поэтому сначала берём
    /// HardwareInformation.qwMemorySize из ключа драйвера видеоадаптера.
    /// </summary>
    private static double ReadVideoMemory(string name, ManagementBaseObject controller)
    {
        try
        {
            using var adapters = Registry.LocalMachine.OpenSubKey(DisplayAdaptersClassKey);
            foreach (var subKeyName in adapters?.GetSubKeyNames() ?? [])
            {
                using var adapter = adapters!.OpenSubKey(subKeyName);
                if (adapter?.GetValue("DriverDesc") as string != name) continue;
                if (adapter.GetValue("HardwareInformation.qwMemorySize") is long bytes and > 0) return bytes;
            }
        }
        catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            // Нет прав на ключ — используем значение WMI.
        }
        return Convert.ToDouble(controller["AdapterRAM"] ?? 0, CultureInfo.InvariantCulture);
    }

    private static string FormatBytes(double bytes) =>
        bytes > 0 ? $"{bytes / (1L << 30):0.#} ГБ" : SystemSnapshot.NotAvailable;

    private static IEnumerable<ManagementBaseObject> Query(string wmiClass)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher($"SELECT * FROM {wmiClass}");
            return searcher.Get().Cast<ManagementBaseObject>().ToList();
        }
        catch (ManagementException)
        {
            return [];
        }
    }

    private static string Str(ManagementBaseObject obj, string property)
    {
        try
        {
            return obj[property]?.ToString()?.Trim() ?? "";
        }
        catch (ManagementException)
        {
            return "";
        }
    }

    private static string Safe(Func<string> read)
    {
        try
        {
            var value = read();
            return string.IsNullOrWhiteSpace(value) ? SystemSnapshot.NotAvailable : value;
        }
        catch (Exception e) when (e is ManagementException or InvalidOperationException)
        {
            return SystemSnapshot.NotAvailable;
        }
    }
}
