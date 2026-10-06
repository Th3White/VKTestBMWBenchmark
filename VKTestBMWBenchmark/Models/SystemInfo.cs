namespace VKTestBMWBenchmark.Models;

public sealed class SystemInfo
{
    public string OperatingSystem { get; set; } = "Unknown";
    public string OsVersion { get; set; } = "Unknown";

    public string CpuName { get; set; } = "Unknown";
    public uint CpuCores { get; set; }
    public uint CpuThreads { get; set; }
    public uint CpuMaxClockMhz { get; set; }

    public string GpuName { get; set; } = "Unknown";
    public string GpuDriverVersion { get; set; } = "Unknown";
    public ulong GpuMemoryBytes { get; set; }

    public ulong RamBytes { get; set; }
    public uint RamSpeedMhz { get; set; }
    public int RamModules { get; set; }

    public string Motherboard { get; set; } = "Unknown";

    public int DisplayWidth { get; set; }
    public int DisplayHeight { get; set; }
    public int DisplayRefreshRate { get; set; }

    public double RamGb =>
        RamBytes / 1024d / 1024d / 1024d;

    public double GpuMemoryGb =>
        GpuMemoryBytes / 1024d / 1024d / 1024d;
}
