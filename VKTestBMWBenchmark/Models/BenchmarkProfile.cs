namespace VKTestBMWBenchmark.Models;

/// <summary>
/// Набор настроек для одного прохода бенчмарка.
/// </summary>
public sealed class BenchmarkProfile
{
    public string Name { get; init; } = "";

    /// <summary>Почему выбраны именно такие настройки.</summary>
    public string Rationale { get; init; } = "";

    /// <summary>
    /// Значения внутри UISettingData=((...)) — их читает меню игры
    /// и именно они определяют, с чем запустится тест.
    /// </summary>
    public Dictionary<string, string> UiSettings { get; init; } = new();

    /// <summary>
    /// Ключи движка Unreal: секция → (ключ → значение).
    /// Дублируют UI-настройки, чтобы игра не «поправила» их при старте.
    /// </summary>
    public Dictionary<string, Dictionary<string, string>> IniValues { get; init; } = new();

    /// <summary>Человекочитаемое описание для отчёта.</summary>
    public List<(string Name, string Value)> Summary { get; init; } = new();
}
