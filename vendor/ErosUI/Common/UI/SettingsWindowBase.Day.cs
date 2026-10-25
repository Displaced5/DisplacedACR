using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace ErosUI;

// 日间模式配色：米白半透明纯色底色（无模糊）+ 标题栏/滚动条主色调样式（浅色）。
// 布局在 SettingsWindowBase.Sidebar.cs（与夜间模式共用）。
public abstract partial class SettingsWindowBase
{
    // 日间模式底色：半透明米白（显式 AddRectFilled 填充，不做模糊）。
    private static readonly Vector4 DayBackgroundTint = new(0.96f, 0.94f, 0.89f, 0.85f);

    // 日间模式专属全局样式：标题栏半透明主色、滚动条用暗一档 Accent。
    // Push 数量必须等于 SidebarChromeColorCount，改动后同步该常量。
    private void PushDayChromeColors()
    {
        ImGui.PushStyleColor(ImGuiCol.TitleBg, SimplePalette.WithAlpha(SimplePalette.Primary, 0.30f));
        ImGui.PushStyleColor(ImGuiCol.TitleBgActive, SimplePalette.WithAlpha(SimplePalette.Primary, 0.50f));
        ImGui.PushStyleColor(ImGuiCol.TitleBgCollapsed, SimplePalette.WithAlpha(SimplePalette.Primary, 0.30f));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarBg, new Vector4(1f, 1f, 1f, 0.20f));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrab, SimplePalette.WithAlpha(SimplePalette.Accent, 0.45f));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabHovered, SimplePalette.WithAlpha(SimplePalette.Accent, 0.65f));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabActive, SimplePalette.WithAlpha(SimplePalette.Accent, 0.85f));
    }
}
