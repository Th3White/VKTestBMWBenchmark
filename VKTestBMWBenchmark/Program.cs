using VKTestBMWBenchmark.Models;
using VKTestBMWBenchmark.Services;

Console.OutputEncoding = System.Text.Encoding.UTF8;

// --no-pause: не ждать Enter в конце (для запуска из скриптов).
bool pauseAtEnd = !args.Contains("--no-pause", StringComparer.OrdinalIgnoreCase);

// --only cpu | --only gpu: выполнить только один проход.
int onlyIndex = Array.FindIndex(args, a => a.Equals("--only", StringComparison.OrdinalIgnoreCase));
string? onlyPass = onlyIndex >= 0 && onlyIndex + 1 < args.Length ? args[onlyIndex + 1] : null;

Console.WriteLine("==========================================");
Console.WriteLine(" Black Myth: Wukong Benchmark Automation");
Console.WriteLine("==========================================");
Console.WriteLine();

int exitCode = 0;

try
{
    Console.WriteLine("Получение характеристик компьютера...");

    SystemInfo system = new SystemInfoService().GetSystemInfo();

    Console.WriteLine($"  {system.CpuName} | {system.GpuName} | {system.RamGb:F0} ГБ RAM");
    Console.WriteLine();

    Console.WriteLine("Поиск Steam и Black Myth: Wukong Benchmark Tool...");

    BenchmarkInstallation benchmark = new SteamLocator().FindBenchmark()
        ?? throw new InvalidOperationException(
            "Benchmark Tool не найден. Установи его из Steam (App ID 3132990).");

    Console.WriteLine($"  {benchmark.InstallDirectory}");
    Console.WriteLine();

    var configService = new BenchmarkConfigService();

    if (!configService.ConfigExists(benchmark))
    {
        throw new InvalidOperationException(
            "GameUserSettings.ini не найден. Запусти Benchmark Tool вручную один раз, " +
            "прими соглашение и закрой его — после этого автоматизация заработает.");
    }

    var display = SystemInfoService.GetPrimaryDisplayMode();

    BenchmarkProfile[] profiles =
    {
        BenchmarkProfiles.CreateCpuProfile(),
        BenchmarkProfiles.CreateGpuProfile(display)
    };

    if (onlyPass is not null)
    {
        profiles = profiles
            .Where(p => p.Name.StartsWith(onlyPass, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (profiles.Length == 0)
            throw new ArgumentException("--only принимает значения cpu или gpu.");
    }

    var passes = new List<ReportWriter.PassResult>();
    var runner = new BenchmarkRunner();

    // Игра не должна быть запущена, иначе она перезапишет ini при выходе.
    BenchmarkRunner.KillGame();

    configService.BackupOriginalConfig(benchmark);
    Console.WriteLine("Исходные настройки игры сохранены, после тестов они будут восстановлены.");

    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine();
    Console.WriteLine("Во время тестов не трогай мышь и клавиатуру: инструмент сам");
    Console.WriteLine("нажимает кнопки в меню бенчмарка. Каждый проход занимает ~3-4 минуты.");
    Console.ResetColor();

    try
    {
        for (int i = 0; i < profiles.Length; i++)
        {
            BenchmarkProfile profile = profiles[i];

            Console.WriteLine();
            Console.WriteLine($"[{i + 1}/{profiles.Length}] {profile.Name}");

            configService.ApplyProfile(benchmark, profile);
            Console.WriteLine("  Настройки применены.");

            BenchmarkResult result = runner.Run(benchmark);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  Средний FPS: {result.FPSAvg}, 5-й перцентиль: {result.FPS95}");
            Console.ResetColor();

            passes.Add(new ReportWriter.PassResult(profile, result));
        }
    }
    finally
    {
        BenchmarkRunner.KillGame();
        configService.RestoreOriginalConfig(benchmark);
        Console.WriteLine();
        Console.WriteLine("Исходные настройки игры восстановлены.");
    }

    var reportWriter = new ReportWriter();

    reportWriter.PrintToConsole(system, passes);

    string outputDirectory = Path.Combine(
        AppContext.BaseDirectory,
        "Results",
        DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));

    string reportPath = reportWriter.Save(outputDirectory, system, passes);

    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("Отчёт сохранён:");
    Console.WriteLine(reportPath);
    Console.ResetColor();
}
catch (Exception ex)
{
    exitCode = 1;

    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine();
    Console.WriteLine("Ошибка:");
    Console.WriteLine(ex.Message);
    Console.ResetColor();
}

if (pauseAtEnd)
{
    Console.WriteLine();
    Console.WriteLine("Нажмите Enter для выхода...");
    Console.ReadLine();
}

return exitCode;
