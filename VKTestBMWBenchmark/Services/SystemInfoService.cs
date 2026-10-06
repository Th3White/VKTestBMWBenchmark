using Microsoft.Win32;
using System.Management;
using System.Runtime.InteropServices;
using VKTestBMWBenchmark.Models;

namespace VKTestBMWBenchmark.Services;

public sealed class SystemInfoService
{
    public SystemInfo GetSystemInfo()
    {
        var info = new SystemInfo();

        ReadOperatingSystem(info);
        ReadCpu(info);
        ReadGpu(info);
        ReadRam(info);
        ReadMotherboard(info);
        ReadDisplay(info);

        return info;
    }

    private static void ReadOperatingSystem(SystemInfo info)
    {
        using var searcher = new ManagementObjectSearcher(
            "SELECT Caption, Version FROM Win32_OperatingSystem");

        foreach (ManagementObject obj in searcher.Get())
        {
            info.OperatingSystem =
                obj["Caption"]?.ToString()?.Trim() ?? "Unknown";

            info.OsVersion =
                obj["Version"]?.ToString()?.Trim() ?? "Unknown";

            break;
        }
    }

    private static void ReadCpu(SystemInfo info)
    {
        using var searcher = new ManagementObjectSearcher(
            "SELECT Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed FROM Win32_Processor");

        foreach (ManagementObject obj in searcher.Get())
        {
            info.CpuName =
                obj["Name"]?.ToString()?.Trim() ?? "Unknown";

            if (obj["NumberOfCores"] != null)
            {
                info.CpuCores =
                    Convert.ToUInt32(obj["NumberOfCores"]);
            }

            if (obj["NumberOfLogicalProcessors"] != null)
            {
                info.CpuThreads =
                    Convert.ToUInt32(obj["NumberOfLogicalProcessors"]);
            }

            if (obj["MaxClockSpeed"] != null)
            {
                info.CpuMaxClockMhz =
                    Convert.ToUInt32(obj["MaxClockSpeed"]);
            }

            break;
        }
    }

    private static void ReadGpu(SystemInfo info)
    {
        using var searcher = new ManagementObjectSearcher(
            "SELECT Name, DriverVersion, PNPDeviceID FROM Win32_VideoController");

        var adapters = new List<(string Name, string Driver, string PnpId)>();

        foreach (ManagementObject obj in searcher.Get())
        {
            string name =
                obj["Name"]?.ToString()?.Trim() ?? "";

            string driver =
                obj["DriverVersion"]?.ToString()?.Trim() ?? "Unknown";

            string pnpId =
                obj["PNPDeviceID"]?.ToString()?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(name))
                continue;

            if (name.Contains(
                "Microsoft Basic",
                StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            adapters.Add((name, driver, pnpId));
        }

        if (adapters.Count == 0)
            return;

        // В первую очередь выбираем дискретную видеокарту.
        var selected = adapters.FirstOrDefault(gpu =>
            gpu.Name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) ||
            gpu.Name.Contains("Radeon", StringComparison.OrdinalIgnoreCase) ||
            gpu.Name.Contains("AMD", StringComparison.OrdinalIgnoreCase) ||
            gpu.Name.Contains("Intel Arc", StringComparison.OrdinalIgnoreCase));

        // Если дискретная карта не найдена — используем первый адаптер.
        if (string.IsNullOrWhiteSpace(selected.Name))
        {
            selected = adapters[0];
        }

        info.GpuName = selected.Name;
        info.GpuDriverVersion = selected.Driver;
        info.GpuMemoryBytes = ReadGpuMemory(selected.Name);
    }

    /// <summary>
    /// Win32_VideoController.AdapterRAM — 32-битное поле и упирается в 4 ГБ,
    /// поэтому точный объём видеопамяти берём из реестра драйвера.
    /// </summary>
    private static ulong ReadGpuMemory(string gpuName)
    {
        const string classKey =
            @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";

        try
        {
            using RegistryKey? root = Registry.LocalMachine.OpenSubKey(classKey);

            if (root is null)
                return 0;

            foreach (string subKeyName in root.GetSubKeyNames())
            {
                using RegistryKey? adapter = root.OpenSubKey(subKeyName);

                string? description = adapter?.GetValue("DriverDesc")?.ToString();

                if (!string.Equals(description, gpuName, StringComparison.OrdinalIgnoreCase))
                    continue;

                object? value = adapter!.GetValue("HardwareInformation.qwMemorySize");

                return value switch
                {
                    long l => (ulong)l,
                    byte[] bytes when bytes.Length >= 8 => BitConverter.ToUInt64(bytes, 0),
                    byte[] bytes when bytes.Length >= 4 => BitConverter.ToUInt32(bytes, 0),
                    _ => 0
                };
            }
        }
        catch
        {
            // Нет доступа к ключу — просто не покажем объём.
        }

        return 0;
    }

    private static void ReadRam(SystemInfo info)
    {
        ulong totalRam = 0;

        using var searcher = new ManagementObjectSearcher(
            "SELECT Capacity, ConfiguredClockSpeed, Speed FROM Win32_PhysicalMemory");

        foreach (ManagementObject obj in searcher.Get())
        {
            if (obj["Capacity"] != null)
            {
                totalRam +=
                    Convert.ToUInt64(obj["Capacity"]);
            }

            object? speed = obj["ConfiguredClockSpeed"] ?? obj["Speed"];

            if (speed != null)
            {
                info.RamSpeedMhz = Math.Max(
                    info.RamSpeedMhz,
                    Convert.ToUInt32(speed));
            }

            info.RamModules++;
        }

        info.RamBytes = totalRam;
    }

    private static void ReadMotherboard(SystemInfo info)
    {
        using var searcher = new ManagementObjectSearcher(
            "SELECT Manufacturer, Product FROM Win32_BaseBoard");

        foreach (ManagementObject obj in searcher.Get())
        {
            string manufacturer = obj["Manufacturer"]?.ToString()?.Trim() ?? "";
            string product = obj["Product"]?.ToString()?.Trim() ?? "";

            info.Motherboard = $"{manufacturer} {product}".Trim();
            break;
        }
    }

    private static void ReadDisplay(SystemInfo info)
    {
        DisplayMode mode = GetPrimaryDisplayMode();

        info.DisplayWidth = mode.Width;
        info.DisplayHeight = mode.Height;
        info.DisplayRefreshRate = mode.RefreshRate;
    }

    public readonly record struct DisplayMode(int Width, int Height, int RefreshRate);

    /// <summary>
    /// Реальный режим основного монитора в пикселях (без учёта DPI-масштаба).
    /// Benchmark Tool запускается в оконном полноэкранном режиме
    /// именно в этом разрешении.
    /// </summary>
    public static DisplayMode GetPrimaryDisplayMode()
    {
        var devMode = new DEVMODE
        {
            dmSize = (short)Marshal.SizeOf<DEVMODE>()
        };

        const int EnumCurrentSettings = -1;

        if (!EnumDisplaySettings(null, EnumCurrentSettings, ref devMode))
            return new DisplayMode(1920, 1080, 60);

        return new DisplayMode(
            devMode.dmPelsWidth,
            devMode.dmPelsHeight,
            devMode.dmDisplayFrequency);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplaySettings(
        string? deviceName, int modeNum, ref DEVMODE devMode);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;
        public short dmSpecVersion;
        public short dmDriverVersion;
        public short dmSize;
        public short dmDriverExtra;
        public int dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public int dmDisplayOrientation;
        public int dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmFormName;
        public short dmLogPixels;
        public int dmBitsPerPel;
        public int dmPelsWidth;
        public int dmPelsHeight;
        public int dmDisplayFlags;
        public int dmDisplayFrequency;
        public int dmICMMethod;
        public int dmICMIntent;
        public int dmMediaType;
        public int dmDitherType;
        public int dmReserved1;
        public int dmReserved2;
        public int dmPanningWidth;
        public int dmPanningHeight;
    }
}
