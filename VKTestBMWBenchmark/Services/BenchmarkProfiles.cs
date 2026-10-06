using VKTestBMWBenchmark.Models;

namespace VKTestBMWBenchmark.Services;

/// <summary>
/// Профили CPU- и GPU-теста.
///
/// Значения UISettingData (проверено по меню игры и по JSON-результату):
///   уровни качества 1..5 = Низкое / Среднее / Высокое / Очень высокое / Кинематографичное;
///   ImageQuality — высота рендера в пикселях (масштаб = ImageQuality / высота окна);
///   InsertFrame 1 = генерация кадров включена, 0 = выключена;
///   Rtx 1 = полная трассировка лучей, RtxLevel 1 = «Низк.» … 3 = «Ультра»;
///   ScreenMode 1 = окно без рамок, 2 = оконный режим.
/// [ScalabilityGroups] игра пересчитывает сама при старте, но мы держим
/// их согласованными с меню: sg.* = (уровень меню − 1).
/// </summary>
public static class BenchmarkProfiles
{
    private const string GameSection = "/Script/GSGameSettings.GSGameUserSettings";

    // Минимальный масштаб рендера, который принимает бенчмарк.
    private const int MinRenderScalePercent = 25;

    // Выходное разрешение CPU-теста: самое низкое из стандартных 16:9.
    private static readonly SystemInfoService.DisplayMode CpuOutput = new(1280, 720, 0);

    private static readonly string[] QualityKeys =
    {
        "ViewDistance", "AntiAliasing", "PostProcessing", "ShadowQuality",
        "TextureQuality", "FxQuality", "MaterialQuality", "VegetationQuality",
        "GlobalIllumination", "ReflectionQuality"
    };

    private static readonly string[] ScalabilityKeys =
    {
        "sg.ViewDistanceQuality", "sg.AntiAliasingQuality", "sg.ShadowQuality",
        "sg.GlobalIlluminationQuality", "sg.ReflectionQuality", "sg.PostProcessQuality",
        "sg.TextureQuality", "sg.EffectsQuality", "sg.FoliageQuality", "sg.ShadingQuality"
    };

    public static BenchmarkProfile CreateCpuProfile()
    {
        int renderWidth = CpuOutput.Width * MinRenderScalePercent / 100;
        int renderHeight = CpuOutput.Height * MinRenderScalePercent / 100;

        return Create(
            name: "CPU-тест",
            rationale:
                "Нужно убрать видеокарту из уравнения: чем меньше работы у GPU на кадр, " +
                "тем выше FPS и тем раньше упираемся в процессор (логика игры, анимация, " +
                "физика, подготовка draw calls). Одного масштаба рендера мало: пост-обработка, " +
                "апскейлер и интерфейс считаются в выходном разрешении, поэтому окно уменьшено " +
                $"до {CpuOutput.Width}×{CpuOutput.Height}, а рендер внутри — до минимальных " +
                $"{MinRenderScalePercent}% ({renderWidth}×{renderHeight}). Все пресеты «Низкое», " +
                "трассировка лучей и размытие выключены. Генерация кадров выключена: она " +
                "добавляет кадры на GPU без участия CPU и завысила бы результат. VSync и " +
                "лимит FPS сняты, чтобы ничего не ограничивало частоту кадров сверху.",
            output: CpuOutput,
            windowed: true,
            renderScalePercent: MinRenderScalePercent,
            qualityLevel: 1,
            rayTracing: false,
            rayTracingLevel: 0,
            motionBlur: false);
    }

    public static BenchmarkProfile CreateGpuProfile(SystemInfoService.DisplayMode display)
    {
        return Create(
            name: "GPU-тест",
            rationale:
                "Нужна максимальная нагрузка на видеокарту: родное разрешение экрана " +
                $"({display.Width}×{display.Height}) со 100% масштабом рендера (без " +
                "апскейла), все пресеты «Кинематографичное» и полная трассировка лучей " +
                "(path tracing) на уровне «Ультра» — самая тяжёлая для GPU часть игры. " +
                "Размытие в движении включено (дополнительный пост-эффект). Генерация " +
                "кадров выключена, чтобы FPS отражал реальную скорость рендера, а не " +
                "интерполированные кадры. VSync и лимит FPS сняты.",
            output: display,
            windowed: false,
            renderScalePercent: 100,
            qualityLevel: 5,
            rayTracing: true,
            rayTracingLevel: 3,
            motionBlur: true);
    }

    private static BenchmarkProfile Create(
        string name,
        string rationale,
        SystemInfoService.DisplayMode output,
        bool windowed,
        int renderScalePercent,
        int qualityLevel,
        bool rayTracing,
        int rayTracingLevel,
        bool motionBlur)
    {
        int renderWidth = output.Width * renderScalePercent / 100;
        int renderHeight = output.Height * renderScalePercent / 100;

        // В UISettingData: 1 = окно без рамок, 2 = окно.
        // В движке (FullscreenMode): 1 = WindowedFullscreen, 2 = Windowed.
        string screenMode = windowed ? "2" : "1";

        var ui = new Dictionary<string, string>
        {
            ["ScreenMode"] = screenMode,
            ["ImageQuality"] = renderHeight.ToString(),
            ["Vsync"] = "0",
            ["LockFrameRate"] = "0",
            ["InsertFrame"] = "0",
            ["MotionBlur"] = motionBlur ? "1" : "0",
            ["Rtx"] = rayTracing ? "1" : "0",
            ["RtxLevel"] = rayTracing ? rayTracingLevel.ToString() : "1",
            ["QualityLevel"] = qualityLevel.ToString()
        };

        foreach (string key in QualityKeys)
        {
            ui[key] = qualityLevel.ToString();
        }

        var game = new Dictionary<string, string>
        {
            ["FullscreenMode"] = screenMode,
            ["LastConfirmedFullscreenMode"] = screenMode,
            ["PreferredFullscreenMode"] = screenMode,
            ["ResolutionSizeX"] = output.Width.ToString(),
            ["ResolutionSizeY"] = output.Height.ToString(),
            ["LastUserConfirmedResolutionSizeX"] = output.Width.ToString(),
            ["LastUserConfirmedResolutionSizeY"] = output.Height.ToString(),
            ["bUseVSync"] = "False",
            ["bUseDynamicResolution"] = "False",
            ["FrameRateLimit"] = "0.000000"
        };

        var scalability = new Dictionary<string, string>
        {
            ["sg.ResolutionQuality"] = renderScalePercent.ToString(),
            ["sg.RayTracingQuality"] = rayTracing ? rayTracingLevel.ToString() : "0"
        };

        foreach (string key in ScalabilityKeys)
        {
            scalability[key] = (qualityLevel - 1).ToString();
        }

        string qualityName = qualityLevel switch
        {
            1 => "Низкое",
            2 => "Среднее",
            3 => "Высокое",
            4 => "Очень высокое",
            5 => "Кинематографичное",
            _ => qualityLevel.ToString()
        };

        return new BenchmarkProfile
        {
            Name = name,
            Rationale = rationale,
            UiSettings = ui,
            IniValues = new()
            {
                [GameSection] = game,
                ["ScalabilityGroups"] = scalability
            },
            Summary = new()
            {
                ("Режим экрана", windowed ? "Окно" : "Окно без рамок"),
                ("Выходное разрешение", $"{output.Width}×{output.Height}"),
                ("Масштаб рендера", $"{renderScalePercent}% ({renderWidth}×{renderHeight})"),
                ("Пресет качества", $"{qualityName} (все параметры)"),
                ("Полная трассировка лучей", rayTracing ? "Вкл., уровень «Ультра»" : "Выкл."),
                ("Генерация кадров", "Выкл."),
                ("Размытие в движении", motionBlur ? "Вкл." : "Выкл."),
                ("VSync / лимит FPS", "Выкл. / нет")
            }
        };
    }
}
