using PromeRotation.Data;

namespace ErosUI;

// 职业环境数据（框架内部使用）。对外注入入口是 ErosUIFramework.Configure。
// 在 Rotation 构造函数最前面调用 Configure 注入职业身份与数据源，框架其余部分
// 只读本类、不接触任何职业数据。宿主每次切换职业都会重新构造 Rotation 实例，
// 所以构造时注入即可保证框架看到的永远是当前职业。
internal static class ErosUIJobEnv
{
    /// <summary>是否已经注入过职业环境。没注入就读写设置会清掉用户配置，框架用它拦住这种误用。</summary>
    public static bool Configured { get; private set; }

    /// <summary>职业短名（如 SAM / RPR），用作窗口名与配置文件名（{JobTag}.json）。</summary>
    public static string JobTag { get; private set; } = "SAM";

    /// <summary>职业中文名，用于设置窗口标题。</summary>
    public static string JobName { get; private set; } = "武士";

    /// <summary>作者身份，决定配置目录、窗口标题前缀与日志前缀。</summary>
    /// <remarks>默认占位值为 "author"，必须经 Configure 改成自己的作者名，
    /// 且与 RotationMetadata 的 Author 参数一致（详见 README「作者身份」）。</remarks>
    public static string 作者 { get; private set; } = "author";

    /// <summary>QT 开关表：键 → 默认值。由使用方注入，缺省为空。</summary>
    public static IReadOnlyDictionary<string, bool> QtAll { get; private set; } = new Dictionary<string, bool>();

    /// <summary>判断某键是否为元键（元键不进 QT 面板）。</summary>
    public static Func<string, bool> QtIsMetaKey { get; private set; } = _ => false;

    /// <summary>判断某键在指定模式（高难/日随）下是否可见。</summary>
    public static Func<string, bool, bool> QtIsVisibleInMode { get; private set; } = (_, _) => true;

    /// <summary>读取某键的默认值。</summary>
    public static Func<string, bool> QtDefault { get; private set; } = _ => false;

    /// <summary>QT 联动表：写入某键时，一并写入哪些键（invert 为 true 时取反）。</summary>
    public static IReadOnlyDictionary<string, (string key, bool invert)[]> QtCascadeRules { get; private set; }
        = new Dictionary<string, (string, bool)[]>();

    /// <summary>热键面板的按钮名全集，设置页 Hotkey 显隐列表以此为准。</summary>
    public static string[] HotkeyNames { get; private set; } = Array.Empty<string>();

    /// <summary>热键面板条目构建委托。框架不预置任何按钮，全部条目由使用方在此注册。</summary>
    public static System.Action<ErosUIHotkeyBuilder>? BuildHotkeys { get; private set; }
    // ViperACR adapter: draw model parameters inside the framework settings window.
    public static System.Action? DrawRotationSettings { get; set; }
    public static System.Action? DrawOpenerSettings { get; set; }
    public static System.Action? DrawBasicSettings { get; set; }
    public static System.Action? DrawDiagnostics { get; set; }

    /// <summary>首次使用时写入的出厂配置 JSON（可选，内容是完整的 ErosUISettings 序列化结果）。不注入则找框架 DLL 内嵌的 Resources/Default{JobTag}.json，仍没有才走代码里的默认值。</summary>
    public static string? DefaultSettingsJson { get; private set; }

    /// <summary>注入本职业的全部环境，由 ErosUIFramework.Configure 转发调用。</summary>
    internal static void Configure(
        string jobTag,
        string jobName,
        IReadOnlyDictionary<string, bool> qtAll,
        Func<string, bool> qtIsMetaKey,
        Func<string, bool, bool> qtIsVisibleInMode,
        Func<string, bool> qtDefault,
        IReadOnlyDictionary<string, (string key, bool invert)[]> qtCascadeRules,
        string[] hotkeyNames,
        System.Action<ErosUIHotkeyBuilder>? buildHotkeys,
        string? author = null,
        string? defaultSettingsJson = null)
    {
        JobTag = jobTag;
        JobName = jobName;
        if (author != null) 作者 = author;
        QtAll = qtAll;
        QtIsMetaKey = qtIsMetaKey;
        QtIsVisibleInMode = qtIsVisibleInMode;
        QtDefault = qtDefault;
        QtCascadeRules = qtCascadeRules;
        HotkeyNames = hotkeyNames;
        BuildHotkeys = buildHotkeys;
        DefaultSettingsJson = defaultSettingsJson;
        Configured = true;
    }
}
