using System.Numerics;
using Dalamud.Bindings.ImGui;
using PromeRotation.Data;

namespace ErosUI;

// 战斗控制悬浮条（夜间/日间模式）：实心状态按钮（绿/黄/红 + 白色方块标记）
// + 1px 竖分隔线 + 按钮化主动攻击（开启时实心红）+ 深色/白色设置按钮。
// 状态按钮左键 运行⇄关闭、右键 运行⇄停手；主动攻击切换 AutoPull。
// 两模式差异只有纯色半透明底色，其余颜色随 SimplePalette 方案自动切换。
public sealed partial class CombatControlWindow
{
    // 夜间模式悬浮条底色：半透明深灰（显式 AddRectFilled 填充，不做模糊）。
    private static readonly Vector4 NightBarTint = new(0.11f, 0.11f, 0.12f, 0.85f);

    // 日间模式悬浮条底色：半透明米白（显式 AddRectFilled 填充，不做模糊）。
    private static readonly Vector4 DayBarTint = new(0.96f, 0.94f, 0.89f, 0.85f);

    // 实心状态按钮上的文字与标记色（彩色底上固定用白）。
    private static readonly Vector4 SolidButtonText = new(1f, 1f, 1f, 1f);

    private const float ModernButtonHeight = 34f;

    // 悬浮条按钮圆角：必须明显小于悬浮条本身的 10f（圆角分层）。
    private const float ModernButtonRounding = 6f;

    private const float StateButtonWidth = 118f;
    private const float AutoPullButtonWidth = 96f;
    private const float SettingsButtonWidth = 56f;

    // 分隔线占位步进（按钮间 SameLine 间距）。
    private const float DividerAdvance = 12f;

    // 按钮行总宽 = 状态 + 分隔 + 主动攻击 + 分隔 + 设置。
    // 顶部拖动条必须用这个固定宽度，不要改成取可用区域宽度：
    // 可用宽度会随窗口变宽形成自反馈，而自动尺寸的窗口只会变大不会变小，
    // 按钮减少后背景就再也收不回去。
    private const float RowWidth = StateButtonWidth + DividerAdvance + AutoPullButtonWidth + DividerAdvance + SettingsButtonWidth;

    // 按界面模式取悬浮条底色。
    private static Vector4 ModernBarTint(SettingsUIMode mode) => mode switch
    {
        SettingsUIMode.Day => DayBarTint,
        _ => NightBarTint,
    };

    private void DrawModernBar()
    {
        DrawModernStateButton();
        DrawModernDivider();
        DrawModernAutoPullButton();
        DrawModernDivider();
        DrawSettingsButton();
    }

    private void DrawModernStateButton()
    {
        var state = PromeSettings.Instance.EnableAcr;
        var (label, color) = state switch
        {
            AcrState.On => ("运行中", SimplePalette.StateRunning),
            AcrState.Hold => ("停手中", SimplePalette.StateHold),
            _ => ("关闭中", SimplePalette.StateOff),
        };

        var drawList = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        var size = new Vector2(StateButtonWidth, ModernButtonHeight);
        var max = pos + size;

        ImGui.InvisibleButton("##cc_state", size);
        var hovered = ImGui.IsItemHovered();
        var leftClick = ImGui.IsItemClicked(ImGuiMouseButton.Left);
        var rightClick = ImGui.IsItemClicked(ImGuiMouseButton.Right);

        // 实心状态色，hover 微提亮
        drawList.AddRectFilled(pos, max,
            SimplePalette.ToU32(SimplePalette.WithAlpha(color, hovered ? 1f : 0.88f)), ModernButtonRounding);

        // 白色小方块 + 白字，整体居中
        const float markSize = 7f;
        var textSize = ImGui.CalcTextSize(label);
        var groupWidth = markSize + 6f + textSize.X;
        var groupX = pos.X + (size.X - groupWidth) * 0.5f;
        var centerY = pos.Y + size.Y * 0.5f;
        var white = SimplePalette.ToU32(SolidButtonText);
        drawList.AddRectFilled(
            new Vector2(groupX, centerY - markSize * 0.5f),
            new Vector2(groupX + markSize, centerY + markSize * 0.5f),
            white, 1.5f);
        drawList.AddText(
            new Vector2(groupX + markSize + 6f, pos.Y + (size.Y - textSize.Y) * 0.5f), white, label);

        if (hovered)
            ImGui.SetTooltip("左键：运行 / 关闭\n右键：运行 / 停手\n拖动顶部细条移动位置");

        if (leftClick)
            PromeSettings.Instance.EnableAcr = state == AcrState.Off ? AcrState.On : AcrState.Off;
        else if (rightClick)
            PromeSettings.Instance.EnableAcr = state == AcrState.On ? AcrState.Hold : AcrState.On;
    }

    private void DrawModernAutoPullButton()
    {
        var on = PromeSettings.Instance.AutoPull;
        var drawList = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        var size = new Vector2(AutoPullButtonWidth, ModernButtonHeight);
        var max = pos + size;

        ImGui.InvisibleButton("##cc_autopull", size);
        var hovered = ImGui.IsItemHovered();
        var clicked = ImGui.IsItemClicked(ImGuiMouseButton.Left);

        if (on)
        {
            // 开启：实心红（复用状态红色）
            drawList.AddRectFilled(pos, max,
                SimplePalette.ToU32(SimplePalette.WithAlpha(SimplePalette.StateOff, hovered ? 1f : 0.90f)), ModernButtonRounding);
        }
        else
        {
            // 关闭：与设置按钮同形的普通格子（颜色随方案）
            drawList.AddRectFilled(pos, max,
                SimplePalette.ToU32(hovered ? SimplePalette.FrameBgActive : SimplePalette.FrameBg), ModernButtonRounding);
            drawList.AddRect(pos, max, SimplePalette.ToU32(SimplePalette.Border), ModernButtonRounding,
                ImDrawFlags.RoundCornersAll, 1f);
        }

        const string label = "主动攻击";
        var textSize = ImGui.CalcTextSize(label);
        var textColor = on ? SolidButtonText : SimplePalette.TextSecondary;
        drawList.AddText(pos + (size - textSize) * 0.5f, SimplePalette.ToU32(textColor), label);

        if (hovered)
            ImGui.SetTooltip("非战斗状态下自动寻找目标并开始攻击");
        if (clicked)
            PromeSettings.Instance.AutoPull = !on;
    }

    // 12px 间距中央画 1px 竖线（高 20px，相对 34px 按钮行垂直居中）。
    private static void DrawModernDivider()
    {
        ImGui.SameLine(0f, 0f);
        var pos = ImGui.GetCursorScreenPos();
        var x = pos.X + 6f;
        ImGui.GetWindowDrawList().AddLine(
            new Vector2(x, pos.Y + 7f),
            new Vector2(x, pos.Y + 27f),
            SimplePalette.ToU32(SimplePalette.Border));
        ImGui.SameLine(0f, DividerAdvance);
    }
}
