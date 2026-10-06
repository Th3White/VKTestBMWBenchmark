using System.Text.Json;
using System.Text.Json.Serialization;

namespace VKTestBMWBenchmark.Models;

/// <summary>
/// Результат одного прохода. Формат совпадает с JSON-файлом,
/// который Benchmark Tool пишет в %TEMP%\b1\BenchMarkHistory\Tool\&lt;unix-time&gt;.
/// </summary>
public sealed class BenchmarkResult
{
    public long TimeStamp { get; set; }

    public double FPSAvg { get; set; }
    public double FPSMax { get; set; }
    public double FPSMin { get; set; }
    public double FPS95 { get; set; }

    public double CPUAvg { get; set; }
    public double GPUAvg { get; set; }
    public double VideoMem { get; set; }

    public string GameVer { get; set; } = "";
    public string SysVer { get; set; } = "";
    public string CPUModel { get; set; } = "";
    public string GPUModel { get; set; } = "";
    public string GpuDriverVer { get; set; } = "";
    public string VideoMemSize { get; set; } = "";
    public string SysMem { get; set; } = "";

    // Настройки, с которыми бенчмарк реально отработал.
    public int ScreenMode { get; set; }
    public string ScreenResolution { get; set; } = "";
    public int QualityLevel { get; set; }
    public int ImageQuality { get; set; }
    public int ViewDistance { get; set; }
    public int AntiAliasing { get; set; }
    public int PostProcessing { get; set; }
    public int ShadowQuality { get; set; }
    public int TextureQuality { get; set; }
    public int MaterialQuality { get; set; }
    public int VegetationQuality { get; set; }
    public int MotionBlur { get; set; }
    public int Rtx { get; set; }
    public int Dlss { get; set; }
    public int InsertFrame { get; set; }
    public int Dx12 { get; set; }

    public List<FrameRecord> Records { get; set; } = new();

    [JsonIgnore]
    public string SourceFile { get; set; } = "";

    [JsonIgnore]
    public TimeSpan Duration => Records.Count < 2
        ? TimeSpan.Zero
        : TimeSpan.FromMilliseconds(
            Records[^1].TimeStamp - Records[0].TimeStamp);

    /// <summary>
    /// 1% low: средний FPS по 1% самых медленных кадров.
    /// Показывает статтеры лучше, чем минимум.
    /// </summary>
    [JsonIgnore]
    public double OnePercentLowFps
    {
        get
        {
            if (Records.Count == 0)
                return 0;

            int count = Math.Max(1, Records.Count / 100);

            return Records
                .Select(r => r.FrameRate)
                .Order()
                .Take(count)
                .Average();
        }
    }

    [JsonIgnore]
    public double AvgCpuFrameTimeMs =>
        Records.Count == 0 ? 0 : Records.Average(r => r.CPUFrameTime);

    [JsonIgnore]
    public double AvgGpuUsage =>
        Records.Count == 0 ? GPUAvg : Records.Average(r => r.GPUUsage);

    [JsonIgnore]
    public double MaxVideoMemoryGb =>
        Records.Count == 0 ? VideoMem : Records.Max(r => r.VideoMemoryUsage);

    [JsonIgnore]
    public double AvgFrameTimeMs =>
        Records.Count == 0 ? 0 : Records.Average(r => 1000.0 / Math.Max(r.FrameRate, 0.001));

    /// <summary>
    /// Какую часть полного времени кадра занимает работа CPU.
    /// GPUFrameTime бенчмарка для этого не годится: проверено, что при
    /// упоре в процессор он всё равно ≈ полному времени кадра (GPU ждёт CPU).
    /// </summary>
    [JsonIgnore]
    public double CpuShareOfFrame =>
        AvgFrameTimeMs <= 0 ? 0 : Math.Min(1, AvgCpuFrameTimeMs / AvgFrameTimeMs);

    [JsonIgnore]
    public string Bottleneck
    {
        get
        {
            if (Records.Count == 0)
                return "нет данных";

            if (CpuShareOfFrame >= 0.8)
                return "CPU";

            if (CpuShareOfFrame <= 0.6)
                return "GPU";

            return "смешанное";
        }
    }

    public static BenchmarkResult? TryLoad(string path)
    {
        try
        {
            // Файл может ещё дописываться — открываем с FileShare.ReadWrite.
            using var stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            var result = JsonSerializer.Deserialize<BenchmarkResult>(stream);

            if (result is null || result.FPSAvg <= 0)
                return null;

            result.SourceFile = path;
            return result;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }
}

public sealed class FrameRecord
{
    public long TimeStamp { get; set; }
    public double FrameRate { get; set; }
    public double CPUUsage { get; set; }
    public double GPUUsage { get; set; }
    public double CPUFrameTime { get; set; }
    public double GPUFrameTime { get; set; }
    public double VideoMemoryUsage { get; set; }
}
