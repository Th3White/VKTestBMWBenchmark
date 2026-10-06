using System.Globalization;
using System.Text;
using VKTestBMWBenchmark.Models;

namespace VKTestBMWBenchmark.Services;

/// <summary>
/// Собирает итоговый отчёт: печатает его в консоль
/// и сохраняет в Markdown вместе с исходными JSON бенчмарка.
/// </summary>
public sealed class ReportWriter
{
    private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

    public sealed record PassResult(BenchmarkProfile Profile, BenchmarkResult Result);

    public string Save(
        string outputDirectory,
        SystemInfo system,
        IReadOnlyList<PassResult> passes)
    {
        Directory.CreateDirectory(outputDirectory);

        foreach (PassResult pass in passes)
        {
            string rawName = pass.Profile.Name.StartsWith("CPU") ? "cpu_raw.json" : "gpu_raw.json";

            File.Copy(
                pass.Result.SourceFile,
                Path.Combine(outputDirectory, rawName),
                overwrite: true);
        }

        string reportPath = Path.Combine(outputDirectory, "report.md");

        File.WriteAllText(
            reportPath,
            BuildMarkdown(system, passes),
            new UTF8Encoding(false));

        return reportPath;
    }

    public void PrintToConsole(SystemInfo system, IReadOnlyList<PassResult> passes)
    {
        Header("ХАРАКТЕРИСТИКИ КОМПЬЮТЕРА");

        foreach (var (name, value) in SystemRows(system))
            Row(name, value);

        Header("РЕЗУЛЬТАТЫ");

        var metricRows = MetricRows(passes);

        int nameWidth = metricRows.Max(r => r.Name.Length) + 2;
        const int columnWidth = 16;

        Console.Write("".PadRight(nameWidth));
        foreach (PassResult pass in passes)
            Console.Write(pass.Profile.Name.PadLeft(columnWidth));
        Console.WriteLine();

        foreach (var (name, values) in metricRows)
        {
            Console.Write(name.PadRight(nameWidth));

            foreach (string value in values)
                Console.Write(value.PadLeft(columnWidth));

            Console.WriteLine();
        }

        foreach (PassResult pass in passes)
        {
            Header($"НАСТРОЙКИ: {pass.Profile.Name}");

            foreach (var (name, value) in pass.Profile.Summary)
                Row(name, value);

            Console.WriteLine();
            Console.WriteLine("  Как применились (по данным бенчмарка):");
            Console.WriteLine($"  {AppliedSettings(pass.Result)}");
        }
    }

    private static string BuildMarkdown(SystemInfo system, IReadOnlyList<PassResult> passes)
    {
        var md = new StringBuilder();

        md.AppendLine("# Black Myth: Wukong Benchmark Tool — отчёт");
        md.AppendLine();
        md.AppendLine($"Дата: {DateTime.Now.ToString("dd.MM.yyyy HH:mm", Ru)}");

        if (passes.Count > 0)
            md.AppendLine($"Версия бенчмарка: {passes[0].Result.GameVer}");

        md.AppendLine();
        md.AppendLine("## Характеристики компьютера");
        md.AppendLine();
        md.AppendLine("| Параметр | Значение |");
        md.AppendLine("|---|---|");

        foreach (var (name, value) in SystemRows(system))
            md.AppendLine($"| {name} | {value} |");

        md.AppendLine();
        md.AppendLine("## Результаты");
        md.AppendLine();
        md.Append("| Метрика |");
        foreach (PassResult pass in passes)
            md.Append($" {pass.Profile.Name} |");
        md.AppendLine();
        md.Append("|---|");
        foreach (PassResult _ in passes)
            md.Append("---:|");
        md.AppendLine();

        foreach (var (name, values) in MetricRows(passes))
            md.AppendLine($"| {name} | {string.Join(" | ", values)} |");

        md.AppendLine();
        md.AppendLine("*Узкое место* считается по покадровым записям бенчмарка: если работа CPU " +
                      "занимает ≥ 80% полного времени кадра, FPS ограничивает процессор, " +
                      "если ≤ 60% — видеокарта.");

        foreach (PassResult pass in passes)
        {
            md.AppendLine();
            md.AppendLine($"## Настройки: {pass.Profile.Name}");
            md.AppendLine();
            md.AppendLine("| Параметр | Значение |");
            md.AppendLine("|---|---|");

            foreach (var (name, value) in pass.Profile.Summary)
                md.AppendLine($"| {name} | {value} |");

            md.AppendLine();
            md.AppendLine($"Подтверждение из файла результата: {AppliedSettings(pass.Result)}");
            md.AppendLine();
            md.AppendLine($"**Почему так:** {pass.Profile.Rationale}");
        }

        return md.ToString();
    }

    private static List<(string Name, string Value)> SystemRows(SystemInfo s)
    {
        var rows = new List<(string, string)>
        {
            ("ОС", $"{s.OperatingSystem} ({s.OsVersion})"),
            ("CPU", s.CpuName),
            ("Ядра / потоки", $"{s.CpuCores} / {s.CpuThreads}"),
        };

        if (s.CpuMaxClockMhz > 0)
            rows.Add(("Базовая частота CPU", $"{s.CpuMaxClockMhz} МГц"));

        rows.Add(("GPU", s.GpuName));

        if (s.GpuMemoryBytes > 0)
            rows.Add(("Видеопамять", $"{s.GpuMemoryGb:F0} ГБ"));

        rows.Add(("Драйвер GPU", s.GpuDriverVersion));

        string ram = $"{s.RamGb:F0} ГБ";

        if (s.RamModules > 0)
            ram += $", {s.RamModules} шт.";

        if (s.RamSpeedMhz > 0)
            ram += $", {s.RamSpeedMhz} МТ/с";

        rows.Add(("RAM", ram));
        rows.Add(("Материнская плата", s.Motherboard));

        if (s.DisplayWidth > 0)
            rows.Add(("Монитор", $"{s.DisplayWidth}×{s.DisplayHeight} @ {s.DisplayRefreshRate} Гц"));

        return rows;
    }

    private static List<(string Name, List<string> Values)> MetricRows(IReadOnlyList<PassResult> passes)
    {
        List<string> Col(Func<BenchmarkResult, string> selector) =>
            passes.Select(p => selector(p.Result)).ToList();

        return new()
        {
            ("Средний FPS", Col(r => F(r.FPSAvg))),
            ("Максимальный FPS", Col(r => F(r.FPSMax))),
            ("Минимальный FPS", Col(r => F(r.FPSMin))),
            ("5-й перцентиль FPS", Col(r => F(r.FPS95))),
            ("1% low FPS", Col(r => F(r.OnePercentLowFps))),
            ("Время кадра, мс", Col(r => F(r.AvgFrameTimeMs, 2))),
            ("из них работа CPU, мс", Col(r => F(r.AvgCpuFrameTimeMs, 2))),
            ("Доля CPU в кадре, %", Col(r => F(r.CpuShareOfFrame * 100))),
            ("Загрузка GPU, %", Col(r => F(r.AvgGpuUsage))),
            ("Видеопамять (пик), ГБ", Col(r => F(r.MaxVideoMemoryGb, 1))),
            ("Узкое место", Col(r => r.Bottleneck)),
            ("Длительность", Col(r => r.Duration.ToString(@"m\:ss"))),
            ("Кадров записано", Col(r => r.Records.Count.ToString()))
        };
    }

    private static string AppliedSettings(BenchmarkResult r)
    {
        return $"разрешение {r.ScreenResolution}, масштаб рендера {r.ImageQuality}%, " +
               // В поле Rtx бенчмарк пишет уровень трассировки, 0 — выключена.
               $"пресет {r.QualityLevel}, RT {(r.Rtx > 0 ? $"вкл. (уровень {r.Rtx})" : "выкл.")}, " +
               $"генерация кадров {(r.InsertFrame == 1 ? "вкл." : "выкл.")}, " +
               $"DX12 {(r.Dx12 == 1 ? "вкл." : "выкл.")}";
    }

    private static string F(double value, int digits = 0) =>
        value.ToString("F" + digits, Ru);

    private static void Header(string title)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"=== {title} ===");
        Console.ResetColor();
    }

    private static void Row(string name, string value)
    {
        Console.WriteLine($"  {name.PadRight(26)}{value}");
    }
}
