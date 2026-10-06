using System.Text;
using System.Text.RegularExpressions;
using VKTestBMWBenchmark.Models;

namespace VKTestBMWBenchmark.Services;

public sealed class BenchmarkConfigService
{
    public string GetConfigPath(BenchmarkInstallation installation)
    {
        return Path.Combine(
            installation.InstallDirectory,
            "b1",
            "Saved",
            "Config",
            "Windows",
            "GameUserSettings.ini");
    }

    public bool ConfigExists(BenchmarkInstallation installation)
    {
        string configPath = GetConfigPath(installation);

        return File.Exists(configPath);
    }

    public string[] ReadConfig(BenchmarkInstallation installation)
    {
        string configPath = GetConfigPath(installation);

        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException(
                "Файл GameUserSettings.ini не найден.",
                configPath);
        }

        return File.ReadAllLines(configPath);
    }

    public IEnumerable<string> GetInterestingSettings(
        BenchmarkInstallation installation)
    {
        string[] lines = ReadConfig(installation);

        string[] interestingKeys =
        {
            "ResolutionSizeX",
            "ResolutionSizeY",
            "FullscreenMode",

            "sg.ResolutionQuality",
            "sg.ViewDistanceQuality",
            "sg.AntiAliasingQuality",
            "sg.ShadowQuality",
            "sg.GlobalIlluminationQuality",
            "sg.RayTracingQuality",
            "sg.ReflectionQuality",
            "sg.PostProcessQuality",
            "sg.TextureQuality",
            "sg.EffectsQuality",
            "sg.FoliageQuality",
            "sg.ShadingQuality"
        };

        foreach (string line in lines)
        {
            string trimmedLine = line.Trim();

            foreach (string key in interestingKeys)
            {
                if (trimmedLine.StartsWith(
                    key + "=",
                    StringComparison.OrdinalIgnoreCase))
                {
                    yield return trimmedLine;
                    break;
                }
            }
        }
    }

    public string GetBackupPath()
    {
        string localAppData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);

        return Path.Combine(
            localAppData,
            "VKTestBMWBenchmark",
            "Backup",
            "GameUserSettings.original.ini");
    }

    /// <summary>
    /// Сохраняет пользовательские настройки перед тестами.
    /// Если резервная копия уже лежит на месте, значит прошлый запуск
    /// прервался до восстановления: тогда сначала возвращаем оригинал,
    /// а не затираем его настройками от теста.
    /// </summary>
    public string BackupOriginalConfig(
        BenchmarkInstallation installation)
    {
        string configPath = GetConfigPath(installation);

        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException(
                "Невозможно создать резервную копию: файл настроек не найден.",
                configPath);
        }

        string backupPath = GetBackupPath();

        if (File.Exists(backupPath))
        {
            File.Copy(backupPath, configPath, overwrite: true);
            return backupPath;
        }

        string? backupDirectory =
            Path.GetDirectoryName(backupPath);

        if (!string.IsNullOrWhiteSpace(backupDirectory))
        {
            Directory.CreateDirectory(backupDirectory);
        }

        File.Copy(configPath, backupPath, overwrite: false);

        return backupPath;
    }

    public void RestoreOriginalConfig(
        BenchmarkInstallation installation)
    {
        string backupPath = GetBackupPath();
        string configPath = GetConfigPath(installation);

        if (!File.Exists(backupPath))
        {
            throw new FileNotFoundException(
                "Резервная копия исходных настроек не найдена.",
                backupPath);
        }

        File.Copy(
            backupPath,
            configPath,
            overwrite: true);

        // Копия больше не нужна: её наличие при следующем запуске
        // означало бы, что восстановление не произошло.
        File.Delete(backupPath);
    }

    public void ApplyProfile(
        BenchmarkInstallation installation,
        BenchmarkProfile profile)
    {
        string configPath = GetConfigPath(installation);

        var lines = ReadConfig(installation).ToList();

        // Игра хранит выбор из меню одной строкой:
        // UISettingData=(("Key", "Value"),("Key2", "Value2"),...)
        int uiLineIndex = lines.FindIndex(l =>
            l.TrimStart().StartsWith("UISettingData=", StringComparison.OrdinalIgnoreCase));

        if (uiLineIndex < 0)
        {
            throw new InvalidDataException(
                "В GameUserSettings.ini нет UISettingData. " +
                "Запусти Benchmark Tool вручную один раз, чтобы он создал настройки.");
        }

        lines[uiLineIndex] = UpdateUiSettingData(lines[uiLineIndex], profile.UiSettings);

        foreach (var (section, values) in profile.IniValues)
        {
            foreach (var (key, value) in values)
            {
                SetIniValue(lines, section, key, value);
            }
        }

        // Пишем без BOM, как сама игра.
        File.WriteAllLines(configPath, lines, new UTF8Encoding(false));
    }

    private static string UpdateUiSettingData(
        string line,
        IReadOnlyDictionary<string, string> values)
    {
        foreach (var (key, value) in values)
        {
            string pattern = $@"\(""{Regex.Escape(key)}"",\s*""[^""]*""\)";
            string replacement = $@"(""{key}"", ""{value}"")";

            if (Regex.IsMatch(line, pattern))
            {
                line = Regex.Replace(line, pattern, replacement);
            }
            else
            {
                // Ключа ещё нет — добавляем перед закрывающей скобкой.
                int closing = line.LastIndexOf(')');
                string separator = line[..closing].TrimEnd().EndsWith('(') ? "" : ",";

                line = line.Insert(closing, separator + replacement);
            }
        }

        return line;
    }

    private static void SetIniValue(
        List<string> lines,
        string section,
        string key,
        string value)
    {
        string header = $"[{section}]";

        int sectionStart = lines.FindIndex(l =>
            string.Equals(l.Trim(), header, StringComparison.OrdinalIgnoreCase));

        if (sectionStart < 0)
        {
            if (lines.Count > 0 && lines[^1].Trim().Length > 0)
                lines.Add("");

            lines.Add(header);
            lines.Add($"{key}={value}");
            return;
        }

        int sectionEnd = lines.FindIndex(sectionStart + 1, l => l.TrimStart().StartsWith('['));

        if (sectionEnd < 0)
            sectionEnd = lines.Count;

        for (int i = sectionStart + 1; i < sectionEnd; i++)
        {
            if (lines[i].TrimStart().StartsWith(key + "=", StringComparison.OrdinalIgnoreCase))
            {
                lines[i] = $"{key}={value}";
                return;
            }
        }

        // Вставляем после последней непустой строки секции.
        int insertAt = sectionEnd;

        while (insertAt > sectionStart + 1 && lines[insertAt - 1].Trim().Length == 0)
            insertAt--;

        lines.Insert(insertAt, $"{key}={value}");
    }
}
