using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace ErosUI;

// 夜间模式配色：深色半透明纯色底色（无模糊）+ 标题栏/滚动条主色调样式。
// 布局在 SettingsWindowBase.Sidebar.cs（与日间模式共用）。
public abstract partial class SettingsWindowBase
{
    // 夜间模式底色：半透明深灰（显式 AddRectFilled 填充，不做模糊）。
    private static readonly Vector4 NightBackgroundTint = new(0.11f, 0.11f, 0.12f, 0.85f);

    // 夜间模式专属全局样式：标题栏与滚动条跟随主题主色。
    // Push 数量必须等于 SidebarChromeColorCount，改动后同步该常量。
    private void PushNightChromeColors()
    {
        var primary = SimplePalette.Primary;
        ImGui.PushStyleColor(ImGuiCol.TitleBg, new Vector4(primary.X * 0.22f, primary.Y * 0.22f, primary.Z * 0.22f, 1f));
        ImGui.PushStyleColor(ImGuiCol.TitleBgActive, new Vector4(primary.X * 0.45f, primary.Y * 0.45f, primary.Z * 0.45f, 1f));
        ImGui.PushStyleColor(ImGuiCol.TitleBgCollapsed, new Vector4(primary.X * 0.22f, primary.Y * 0.22f, primary.Z * 0.22f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarBg, new Vector4(0f, 0f, 0f, 0.20f));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrab, SimplePalette.WithAlpha(primary, 0.45f));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabHovered, SimplePalette.WithAlpha(primary, 0.65f));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabActive, SimplePalette.WithAlpha(primary, 0.85f));
    }
}
