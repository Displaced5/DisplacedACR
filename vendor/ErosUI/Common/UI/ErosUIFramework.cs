using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;

namespace ErosUI;

// ErosUI 框架门面：使用方的唯一入口。Configure 注入职业环境，
// Install/Uninstall 一站式装卸（窗口注册、热键面板、QT 注册、设置落盘全部内含），
// 必须严格成对地放在 OnEnterAcr/OnExitAcr 里。
public static class ErosUIFramework
{
    private static CombatControlWindow? control;
    private static ErosUISettingsWindow? settings;
    private static ErosUIQtPanelWindow? qtPanel;

    /// <summary>注入本职业的全部环境。在 Rotation 构造函数最前面调用。</summary>
    public static void Configure(
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
        string? defaultSettingsJson = null,
        System.Action? drawRotationSettings = null,
        System.Action? drawOpenerSettings = null)
    {
        ErosUIJobEnv.Configure(jobTag, jobName, qtAll, qtIsMetaKey, qtIsVisibleInMode,
            qtDefault, qtCascadeRules, hotkeyNames, buildHotkeys, author,
            defaultSettingsJson);
        ErosUIJobEnv.DrawRotationSettings = drawRotationSettings;
        ErosUIJobEnv.DrawOpenerSettings = drawOpenerSettings;
    }

    /// <summary>注册全部窗口并完成初始化：构建热键面板、注册 QT、压制宿主自带面板。幂等，重复调用会先卸载再注册。OnEnterAcr 调用。</summary>
    public static void Install()
    {
        Uninstall();
        var ws = PromeRotation.Plugin.Instance?.WindowSystem;
        if (ws == null)
        {
            Svc.Log.Info($"[{ErosUIJobEnv.作者}] 无法访问宿主 WindowSystem，UI 未注册");
            return;
        }

        control = new CombatControlWindow(
            $"{ErosUIJobEnv.作者}{ErosUIJobEnv.JobTag} 控制",
            ToggleSettings,
            () => 限制存档位置(ErosUISettings.Instance.控制条位置),
            pos =>
            {
                ErosUISettings.Instance.控制条位置 = pos;
                ErosUISettings.Instance.Save();
            });
        settings = new ErosUISettingsWindow();
        ws.AddWindow(control);
        ws.AddWindow(settings);
        qtPanel = new ErosUIQtPanelWindow();
        ws.AddWindow(qtPanel);
        qtPanel.IsOpen = true;   // 默认打开

        HidePrPanels(force: true);   // 切职业瞬间宿主会重弹自带面板，强制压制一次
        ErosUIHotkeyUI.Rebuild();
        APIHelper.重建QT可见性();
    }

    /// <summary>卸载全部窗口与热键面板，并把两份设置落盘。OnExitAcr 调用，与 Install 严格成对。</summary>
    public static void Uninstall()
    {
        ErosUIHotkeyUI.Uninstall();
        var ws = PromeRotation.Plugin.Instance?.WindowSystem;
        if (ws == null) { control = null; settings = null; qtPanel = null; SaveSettings(); return; }

        if (control != null)
        {
            try { ws.RemoveWindow(control); } catch (ArgumentException) { }
            control = null;
        }
        if (settings != null)
        {
            try { ws.RemoveWindow(settings); } catch (ArgumentException) { }
            settings = null;
        }
        if (qtPanel != null)
        {
            try { ws.RemoveWindow(qtPanel); } catch (ArgumentException) { }
            qtPanel = null;
        }
        SaveSettings();
    }

    /// <summary>把通用设置与当前职业设置立即写入磁盘。</summary>
    public static void SaveSettings()
    {
        ErosUISettings.Instance.Save();
        ErosUICommonSettings.Instance.Save();
    }

    /// <summary>写入 QT 开关状态，并按联动表把关联的键一并写入。</summary>
    public static void 设置QT(string qtKey, bool 值) => APIHelper.设置QT(qtKey, 值);

    /// <summary>清空后按当前职业与当前模式重新注册 QT，并同步显隐配置到宿主。切换模式后调用。</summary>
    public static void 重建QT可见性() => APIHelper.重建QT可见性();

    /// <summary>切换 QT 悬浮面板的显示/隐藏。</summary>
    public static void ToggleQtPanel()
    {
        if (qtPanel == null) return;
        qtPanel.IsOpen = !qtPanel.IsOpen;
    }

    /// <summary>QT 悬浮面板当前是否可见。</summary>
    public static bool QtPanelVisible => qtPanel?.IsOpen ?? false;

    /// <summary>显示/隐藏 QT 悬浮面板。只切换显隐，不重建窗口。</summary>
    public static void SetQtPanelVisible(bool visible)
    {
        if (qtPanel != null) qtPanel.IsOpen = visible;
    }

    /// <summary>热键悬浮面板当前是否可见。</summary>
    public static bool HotkeyPanelVisible => ErosUIHotkeyUI.PanelVisible;

    /// <summary>显示/隐藏热键悬浮面板。只切换显隐，不重建窗口。</summary>
    public static void SetHotkeyPanelVisible(bool visible) => ErosUIHotkeyUI.SetPanelVisible(visible);

    /// <summary>打开设置窗口。</summary>
    public static void OpenSettings()
    {
        if (settings != null) settings.IsOpen = true;
    }

    /// <summary>切换设置窗口的打开/关闭（战斗控制条「设置」按钮使用）。</summary>
    public static void ToggleSettings()
    {
        if (settings == null) return;
        settings.IsOpen ^= true;
    }

    /// <summary>给宿主 DrawSettings 回调用的入口：绘制一个打开设置窗口的按钮。</summary>
    public static void DrawSettingsEntry()
    {
        if (ImGui.Button($"打开 {ErosUIJobEnv.作者} 设置窗口"))
            OpenSettings();
    }

    // ============================================================
    // === 宿主本体面板压制 ===
    // ============================================================
    private static long _上次HidePrPanels;

    /// <summary>关闭宿主自带的 QT 面板窗口，防止它与本框架的面板同时出现。</summary>
    /// <remarks>
    /// 只能在 UI 线程调用（由控制条每帧触发，内部 1 秒节流）。
    /// 不要挂到 Framework.Update：那会在渲染线程之外改窗口状态，与渲染竞争后
    /// 会导致整个游戏的 ImGui 交互假死。force 参数绕过节流立即执行，供切职业时
    /// 抑制宿主重新弹出的面板。
    /// </remarks>
    public static void HidePrPanels(bool force = false)
    {
        var now = Environment.TickCount64;
        if (!force && now - _上次HidePrPanels < 1000) return;
        _上次HidePrPanels = now;
        try { PromeRotation.Plugin.Instance?.CloseQtWindow(); } catch { /* 宿主未就绪 */ }
    }

    // 存档位置钳制在屏幕工作区内，防止读取越界存档或切换分辨率后窗口跑出屏幕。
    private static Vector2 限制存档位置(Vector2? saved)
    {
        if (saved is not Vector2 v) return new Vector2(40f, 220f);
        var vp = ImGui.GetMainViewport();
        var min = vp.WorkPos + new Vector2(4f);
        var max = vp.WorkPos + vp.WorkSize - new Vector2(364f, 74f);
        if (max.X < min.X) max.X = min.X;
        if (max.Y < min.Y) max.Y = min.Y;
        return new Vector2(Math.Clamp(v.X, min.X, max.X), Math.Clamp(v.Y, min.Y, max.Y));
    }
}
