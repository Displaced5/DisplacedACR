
namespace ErosUI;

// ErosUI 设置窗口（SettingsWindowBase 子类，多职业共用同一窗口类）。
// 标题按职业环境注入（"{作者} {JobName}设置"），页签结构各职业一致：
// 基础设置 / Hotkey / QT面板，末位「主题」页由基类提供（夜间/日间）。
// 「主题」页为多职业通用内容（ErosUICommonSettings），其余页读当前职业数据。
public sealed class ErosUISettingsWindow : SettingsWindowBase
{

    public ErosUISettingsWindow() : base($"{ErosUIJobEnv.作者} {ErosUIJobEnv.JobName}设置")
    {
        // 三项 Allow 全关 → Dalamud 不再往标题栏注入「窗口设置」齿轮，
        // 右上角只剩关闭键（ImGui 自绘，不受影响）
        AllowPinning = false;
        AllowClickthrough = false;
        AllowBackgroundBlur = false;
    }

    // 页签固定, 末位「主题」页由基类追加
    private static readonly string[] tabs = ["基础设置", "Hotkey", "QT面板", "拓展功能", "循环设置", "开发诊断"];

    protected override string[] Tabs => tabs;

    protected override void DrawTabContent(int tabIndex)
    {
        switch (tabIndex)
        {
            case 0: ErosUISettingsUI.DrawGeneral(); break;
            case 1: ErosUISettingsUI.DrawHotkey(); break;
            case 2: ErosUISettingsUI.DrawQtPanel(); break;
            case 3: ErosUISettingsUI.DrawExtensions(); break;
            case 4: ErosUIJobEnv.DrawRotationSettings?.Invoke(); break;
            case 5: ErosUIJobEnv.DrawDiagnostics?.Invoke(); break;
        }
    }
}
