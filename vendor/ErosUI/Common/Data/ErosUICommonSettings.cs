using System.Text.Json;
using System.Text.Json.Serialization;
using System.Numerics;
using ECommons.DalamudServices;
using ECommons.Logging;

namespace ErosUI;

// 多职业通用设置（单例，JSON 持久化到 Common.json，全部职业共用一份）。
// 只收录框架层通用内容：界面主题（UIMode）与主题主色覆盖。
// 面板布局等职业专属配置在 ErosUISettings（按职业分文件）。
public class ErosUICommonSettings
{
    private static ErosUICommonSettings? instance;
    public static ErosUICommonSettings Instance => instance ??= Load();

    // === 主题 ===
    // UI 主题模式（夜间/日间，全部 ErosUI 窗口与面板共用）; 代码回退默认 = 夜间。
    // JSON 键固定为旧名 UiMode，避免升级后老配置文件的主题偏好丢失。
    [JsonPropertyName("UiMode")]
    public SettingsUIMode UIMode = SettingsUIMode.Night;

    // 切换 UI 主题并落盘
    public void SetUIMode(SettingsUIMode mode)
    {
        UIMode = mode;
        Save();
    }

    // 自定义主题主色（「主题」页调色写入; RGB 0~1）。
    // null = 用内置默认配色。加载时经 Normalize 下发 SimplePalette。
    public Vector3? PrimaryColorOverride;

    // ============================================================
    // === JSON 持久化（路径与 ErosUISettings 同目录: Settings\ACRConfig\<作者>\Common.json） ===
    // ============================================================
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        IncludeFields = true,
    };

    public static string FilePath
    {
        get
        {
            var dir = ErosUISettings.SettingsDirectory;
            if (!System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);
            return System.IO.Path.Combine(dir, "Common.json");
        }
    }

    public static ErosUICommonSettings Load()
    {
        try
        {
            if (System.IO.File.Exists(FilePath))
            {
                var json = System.IO.File.ReadAllText(FilePath);
                var s = JsonSerializer.Deserialize<ErosUICommonSettings>(json, JsonOptions);
                if (s != null) return Normalize(s);
            }
            else
            {
                // 首次使用: 按代码默认值初始化并立即落盘
                var s = Normalize(new ErosUICommonSettings());
                s.Save();
                PluginLog.Log($"[{ErosUIJobEnv.作者}] 通用设置已初始化: {FilePath}");
                return s;
            }
        }
        catch (Exception e) { PluginLog.Error($"[{ErosUIJobEnv.作者}] 通用设置加载失败: {e.Message}"); }
        return new ErosUICommonSettings();
    }



    private static ErosUICommonSettings Normalize(ErosUICommonSettings s)
    {
        // UIMode 的值来自配置文件, 手改 JSON 或历史配置残留已移除的主题档可能出现未定义值, 越界一律回落夜间
        if (s.UIMode is not (SettingsUIMode.Night or SettingsUIMode.Day))
            s.UIMode = SettingsUIMode.Night;
        if (s.PrimaryColorOverride is { } pc)
            s.PrimaryColorOverride = new(Math.Clamp(pc.X, 0f, 1f), Math.Clamp(pc.Y, 0f, 1f), Math.Clamp(pc.Z, 0f, 1f));
        SimplePalette.SetPrimaryOverride(s.PrimaryColorOverride);
        return s;
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(this, JsonOptions);
            var dir = System.IO.Path.GetDirectoryName(FilePath);
            if (dir != null && !System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);
            System.IO.File.WriteAllText(FilePath, json);
        }
        catch { /* 写失败静默 */ }
    }
}
