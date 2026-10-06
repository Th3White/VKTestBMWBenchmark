using Microsoft.Win32;
using System.Text.RegularExpressions;
using VKTestBMWBenchmark.Models;

namespace VKTestBMWBenchmark.Services;

public sealed class SteamLocator
{
    private const string BenchmarkAppId = "3132990";
    private const string LauncherName = "b1_benchmark.exe";

    public BenchmarkInstallation? FindBenchmark()
    {
        string? steamDirectory = FindSteamDirectory();

        if (string.IsNullOrWhiteSpace(steamDirectory))
        {
            return null;
        }

        foreach (string libraryDirectory in GetSteamLibraries(steamDirectory))
        {
            string manifestPath = Path.Combine(
                libraryDirectory,
                "steamapps",
                $"appmanifest_{BenchmarkAppId}.acf");

            if (!File.Exists(manifestPath))
            {
                continue;
            }

            string manifestText = File.ReadAllText(manifestPath);

            Match installDirMatch = Regex.Match(
                manifestText,
                @"""installdir""\s+""([^""]+)""",
                RegexOptions.IgnoreCase);

            if (!installDirMatch.Success)
            {
                continue;
            }

            string installFolderName =
                installDirMatch.Groups[1].Value;

            string installDirectory = Path.Combine(
                libraryDirectory,
                "steamapps",
                "common",
                installFolderName);

            if (!Directory.Exists(installDirectory))
            {
                continue;
            }

            string launcherPath =
                Path.Combine(installDirectory, LauncherName);

            // Если exe вдруг находится не прямо в корне,
            // пробуем найти его внутри каталога Benchmark Tool.
            if (!File.Exists(launcherPath))
            {
                try
                {
                    launcherPath =
                        Directory
                            .EnumerateFiles(
                                installDirectory,
                                LauncherName,
                                SearchOption.AllDirectories)
                            .FirstOrDefault() ?? "";
                }
                catch
                {
                    launcherPath = "";
                }
            }

            if (string.IsNullOrWhiteSpace(launcherPath))
            {
                continue;
            }

            return new BenchmarkInstallation
            {
                SteamDirectory = steamDirectory,
                LibraryDirectory = libraryDirectory,
                InstallDirectory = installDirectory,
                LauncherPath = launcherPath
            };
        }

        return null;
    }

    private static IEnumerable<string> GetSteamLibraries(
        string steamDirectory)
    {
        var libraries = new List<string>();

        AddLibrary(libraries, steamDirectory);

        string libraryFoldersFile = Path.Combine(
            steamDirectory,
            "steamapps",
            "libraryfolders.vdf");

        if (!File.Exists(libraryFoldersFile))
        {
            return libraries;
        }

        string text = File.ReadAllText(libraryFoldersFile);

        MatchCollection matches = Regex.Matches(
            text,
            @"""path""\s+""([^""]+)""",
            RegexOptions.IgnoreCase);

        foreach (Match match in matches)
        {
            string path = match.Groups[1].Value;

            // Steam иногда хранит пути как D:\\SteamLibrary
            path = path.Replace(@"\\", @"\");
            path = path.Replace('/', Path.DirectorySeparatorChar);

            AddLibrary(libraries, path);
        }

        return libraries;
    }

    private static void AddLibrary(
        List<string> libraries,
        string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            path = Path.GetFullPath(path);

            if (!Directory.Exists(path))
            {
                return;
            }

            bool alreadyExists = libraries.Any(existing =>
                string.Equals(
                    existing,
                    path,
                    StringComparison.OrdinalIgnoreCase));

            if (!alreadyExists)
            {
                libraries.Add(path);
            }
        }
        catch
        {
            // Некорректный путь просто пропускаем.
        }
    }

    private static string? FindSteamDirectory()
    {
        var candidates = new List<string?>();

        // Steam для текущего пользователя.
        candidates.Add(ReadRegistryValue(
            RegistryHive.CurrentUser,
            RegistryView.Registry64,
            @"Software\Valve\Steam",
            "SteamPath"));

        candidates.Add(ReadRegistryValue(
            RegistryHive.CurrentUser,
            RegistryView.Registry32,
            @"Software\Valve\Steam",
            "SteamPath"));

        // Steam, установленный системно.
        candidates.Add(ReadRegistryValue(
            RegistryHive.LocalMachine,
            RegistryView.Registry64,
            @"Software\Valve\Steam",
            "InstallPath"));

        candidates.Add(ReadRegistryValue(
            RegistryHive.LocalMachine,
            RegistryView.Registry32,
            @"Software\Valve\Steam",
            "InstallPath"));

        // Стандартный путь как запасной вариант.
        string programFilesX86 =
            Environment.GetFolderPath(
                Environment.SpecialFolder.ProgramFilesX86);

        candidates.Add(
            Path.Combine(programFilesX86, "Steam"));

        foreach (string? candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            string path = candidate
                .Trim()
                .Trim('"')
                .Replace('/', Path.DirectorySeparatorChar);

            if (!Directory.Exists(path))
            {
                continue;
            }

            string steamExe =
                Path.Combine(path, "steam.exe");

            if (File.Exists(steamExe))
            {
                return path;
            }
        }

        return null;
    }

    private static string? ReadRegistryValue(
        RegistryHive hive,
        RegistryView view,
        string subKeyPath,
        string valueName)
    {
        try
        {
            using RegistryKey baseKey =
                RegistryKey.OpenBaseKey(hive, view);

            using RegistryKey? key =
                baseKey.OpenSubKey(subKeyPath);

            return key?
                .GetValue(valueName)?
                .ToString();
        }
        catch
        {
            return null;
        }
    }
}