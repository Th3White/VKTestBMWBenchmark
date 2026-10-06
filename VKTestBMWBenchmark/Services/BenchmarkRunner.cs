using System.Diagnostics;
using VKTestBMWBenchmark.Models;

namespace VKTestBMWBenchmark.Services;

/// <summary>
/// Запускает один проход бенчмарка и возвращает его результат.
///
/// У Benchmark Tool нет режима командной строки, поэтому старт
/// эмулируется вводом: Enter на заставке → клик «Тест быстродействия»
/// → Enter в диалоге «Подтвердить». Этот цикл повторяется, пока
/// не появится файл результата: во время самого теста Enter и
/// левый клик игнорируются (выход — правой кнопкой мыши), так что
/// повторы безопасны и не требуют распознавания экрана.
/// </summary>
public sealed class BenchmarkRunner
{
    private static readonly string[] GameProcessNames =
    {
        "b1-Win64-Shipping",
        "b1_benchmark"
    };

    // Положение пункта «Тест быстродействия» в главном меню
    // в долях ширины/высоты окна.
    private const double MenuBenchmarkX = 0.115;
    private const double MenuBenchmarkY = 0.45;

    private static readonly TimeSpan StepInterval = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(15);

    public static string HistoryDirectory => Path.Combine(
        Path.GetTempPath(), "b1", "BenchMarkHistory", "Tool");

    public BenchmarkResult Run(BenchmarkInstallation installation)
    {
        KillGame();

        Directory.CreateDirectory(HistoryDirectory);

        var existingFiles = new HashSet<string>(
            Directory.GetFiles(HistoryDirectory),
            StringComparer.OrdinalIgnoreCase);

        Log("Запуск Benchmark Tool...");

        Process.Start(new ProcessStartInfo
        {
            FileName = installation.LauncherPath,
            WorkingDirectory = installation.InstallDirectory,
            UseShellExecute = true
        });

        var stopwatch = Stopwatch.StartNew();
        bool startLogged = false;

        try
        {
            while (stopwatch.Elapsed < Timeout)
            {
                BenchmarkResult? result = TryFindNewResult(existingFiles);

                if (result is not null)
                {
                    Log($"Проход завершён за {stopwatch.Elapsed:mm\\:ss}.");
                    return result;
                }

                IntPtr window = FindGameWindow();

                if (window != IntPtr.Zero && NativeInput.TryActivate(window))
                {
                    if (!startLogged)
                    {
                        Log("Окно игры найдено, запускаю тест через меню...");
                        startLogged = true;
                    }

                    // «Нажмите любую кнопку» на заставке.
                    NativeInput.PressKey(NativeInput.VkReturn);
                    Thread.Sleep(1500);

                    // Пункт «Тест быстродействия».
                    NativeInput.ClickRelative(window, MenuBenchmarkX, MenuBenchmarkY);
                    Thread.Sleep(1500);

                    // Кнопка «Подтвердить» выделена по умолчанию.
                    NativeInput.PressKey(NativeInput.VkReturn);
                }

                Thread.Sleep(StepInterval);
            }

            throw new TimeoutException(
                $"Бенчмарк не выдал результат за {Timeout.TotalMinutes} минут.");
        }
        finally
        {
            // Закрываем жёстко: при штатном выходе игра перезаписывает
            // GameUserSettings.ini, а он нужен нам под контролем.
            KillGame();
        }
    }

    private static BenchmarkResult? TryFindNewResult(HashSet<string> existingFiles)
    {
        var newFiles = Directory
            .GetFiles(HistoryDirectory)
            .Where(f => !existingFiles.Contains(f))
            .OrderByDescending(File.GetLastWriteTimeUtc);

        foreach (string file in newFiles)
        {
            // Даём игре дописать файл: он весит ~1.5 МБ.
            if (DateTime.UtcNow - File.GetLastWriteTimeUtc(file) < TimeSpan.FromSeconds(2))
                continue;

            BenchmarkResult? result = BenchmarkResult.TryLoad(file);

            if (result is not null)
                return result;
        }

        return null;
    }

    private static IntPtr FindGameWindow()
    {
        foreach (Process process in Process.GetProcessesByName("b1-Win64-Shipping"))
        {
            using (process)
            {
                if (process.MainWindowHandle != IntPtr.Zero)
                    return process.MainWindowHandle;
            }
        }

        return IntPtr.Zero;
    }

    public static void KillGame()
    {
        foreach (string name in GameProcessNames)
        {
            foreach (Process process in Process.GetProcessesByName(name))
            {
                using (process)
                {
                    try
                    {
                        process.Kill(entireProcessTree: true);
                        process.WaitForExit(10_000);
                    }
                    catch (Exception ex) when (
                        ex is InvalidOperationException or System.ComponentModel.Win32Exception)
                    {
                        // Процесс уже завершился.
                    }
                }
            }
        }
    }

    private static void Log(string message)
    {
        Console.WriteLine($"  [{DateTime.Now:HH:mm:ss}] {message}");
    }
}
