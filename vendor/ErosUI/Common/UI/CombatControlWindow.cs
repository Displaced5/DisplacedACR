using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using PromeRotation.Data;

namespace ErosUI;

// ACR 战斗控制悬浮条（Dalamud WindowSystem 托管）。
// 仅一个状态主按钮 + 主动攻击开关 + 设置按钮，无标题栏；纯色半透明底。
// 主按钮：左键 运行⇄关闭，右键 运行⇄停手；主动攻击为独立开关。
// 任意空白处按住左键拖动位置（短击空白不触发任何状态切换）。
// 夜间/日间模式（实心状态按钮 + 竖分隔线 + 按钮化主动攻击）见 CombatControlWindow.Modern.cs。
public sealed partial class CombatControlWindow : Window
{
    private static readonly Vector2 DefaultPosition = new(40f, 220f);

    private readonly System.Action toggleSettings;
    private readonly Func<Vector2>? getSavedPosition;
    private readonly System.Action<Vector2>? savePosition;
    private bool persistPending;

    public CombatControlWindow(
        string title,
        System.Action toggleSettings,
        Func<Vector2>? getSavedPosition = null,
        System.Action<Vector2>? savePosition = null)
        : base(title,
            ImGuiWindowFlags.NoDecoration
            | ImGuiWindowFlags.AlwaysAutoResize
            | ImGuiWindowFlags.NoFocusOnAppearing
            | ImGuiWindowFlags.NoNav)
    {
        this.toggleSettings = toggleSettings;
        this.getSavedPosition = getSavedPosition;
        this.savePosition = savePosition;

        Position = getSavedPosition?.Invoke() ?? DefaultPosition;
        PositionCondition = ImGuiCond.FirstUseEver;
        RespectCloseHotkey = false;
        AllowBackgroundBlur = false;
        IsOpen = true;
    }

    public override void PreDraw()
    {
        // 本窗口独立使用 SimplePalette 颜色，需自行同步色彩方案（设置窗口可能未打开）
        SimplePalette.ApplyScheme(ErosUICommonSettings.Instance.UIMode == SettingsUIMode.Day
            ? SimplePalette.UIColorScheme.Light
            : SimplePalette.UIColorScheme.Dark);

        // 透明背景：底色直接画在 background drawList，不经 WindowBg
        ImGui.PushStyleColor(ImGuiCol.WindowBg, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.Border, SimplePalette.Border);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 10f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(10f, 8f));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 1f);
        // UI 线程节流压制 PR 本体面板（1 秒一次，防止自动弹回）
        ErosUIFramework.HidePrPanels();
        // 纵向间距置零：拖动区与按钮行之间不留缝，等高的 6px 空隙挪到按钮行下方
        // （Draw 末尾的 Dummy），否则顶部 padding8+拖动区6+间距6=20px、底部只有 8px，视觉上偏上
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8f, 0f));
        base.PreDraw();
    }

    public override void Draw()
    {
        // 拖动改位先于本帧全部可见绘制: 帧内 SetWindowPos 会平移后续绘制坐标,
        // 先画背景再改位会让底板停在旧位、按钮落在新位, 拖动时按钮相对底板晃动
        DrawDragZone();

        // 夜间/日间模式：实心按钮 + 竖分隔线（CombatControlWindow.Modern.cs）
        DrawBackground();
        DrawModernBar();

        // 顶部 6px 拖动区会让上侧留白比下侧多 6px；在按钮行下补同高占位，使按钮行上下居中
        ImGui.Dummy(new Vector2(0f, 6f));
    }

    public override void PostDraw()
    {
        ImGui.PopStyleVar(4);
        ImGui.PopStyleColor(2);
        base.PostDraw();
    }

    private void DrawBackground()
    {
        if (ImGui.GetWindowViewport().ID != ImGui.GetMainViewport().ID) return;

        // 底色画到视口背景层即可（本窗口是 NoDecoration 小窗，不参与背景模糊，
        // 无需像其他窗口那样垫占位命令）。矩形整体内缩半像素对齐像素中心，
        // 保证圆角与半透明描边的抗锯齿正常。
        var min = ImGui.GetWindowPos() + new Vector2(0.5f);
        var max = min + ImGui.GetWindowSize() - new Vector2(1f);
        // 圆角必须与 PreDraw 压入的 WindowRounding(10f) 完全一致，否则模糊/底色的角与窗口边框错位
        var rounding = 10f;
        var drawList = ImGui.GetBackgroundDrawList(ImGui.GetWindowViewport());

        drawList.AddRectFilled(min, max, SimplePalette.ToU32(ModernBarTint(ErosUICommonSettings.Instance.UIMode)), rounding, ImDrawFlags.RoundCornersAll);
    }

    // 顶部 6px 拖动区：短击空白不切换状态，仅负责拖动；松手后写盘一次。
    // 宽度用固定 RowWidth：取 ContentRegionAvail 会随窗口宽度自馈，AlwaysAutoResize 收不回去
    private void DrawDragZone()
    {
        ImGui.InvisibleButton("##cc_drag", new Vector2(RowWidth, 6f));
        if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
        {
            ImGui.SetWindowPos(限制到屏幕内(ImGui.GetWindowPos() + ImGui.GetIO().MouseDelta));
            persistPending = true;
        }
        if (ImGui.IsItemHovered())
            ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeAll);

        // 拖动结束（松手）后落盘一次，避免拖动过程频繁写 JSON
        if (persistPending && !ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            persistPending = false;
            savePosition?.Invoke(ImGui.GetWindowPos());
        }
    }

    // 把窗口位置限制在屏幕工作区内（含 4px 边距）；窗口尺寸未知时按全屏处理。
    private Vector2 限制到屏幕内(Vector2 pos)
    {
        var vp = ImGui.GetMainViewport();
        var size = ImGui.GetWindowSize();
        if (size.X <= 0f || size.Y <= 0f) size = new Vector2(360f, 70f);   // 首帧兜底尺寸

        var margin = 4f;
        var minX = vp.WorkPos.X + margin;
        var minY = vp.WorkPos.Y + margin;
        var maxX = vp.WorkPos.X + vp.WorkSize.X - size.X - margin;
        var maxY = vp.WorkPos.Y + vp.WorkSize.Y - size.Y - margin;
        if (maxX < minX) maxX = minX;
        if (maxY < minY) maxY = minY;

        return new Vector2(Math.Clamp(pos.X, minX, maxX), Math.Clamp(pos.Y, minY, maxY));
    }

    private void DrawSettingsButton()
    {
        var drawList = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        var size = new Vector2(SettingsButtonWidth, ModernButtonHeight);
        var max = pos + size;

        ImGui.InvisibleButton("##cc_settings", size);
        var hovered = ImGui.IsItemHovered();
        var clicked = ImGui.IsItemClicked(ImGuiMouseButton.Left);

        // 按钮圆角 6（明显小于悬浮条本身的 10）
        var rounding = ModernButtonRounding;
        drawList.AddRectFilled(pos, max,
            SimplePalette.ToU32(hovered ? SimplePalette.FrameBgActive : SimplePalette.FrameBg), rounding);
        drawList.AddRect(pos, max,
            SimplePalette.ToU32(hovered ? SimplePalette.BorderStrong : SimplePalette.Border), rounding,
            ImDrawFlags.RoundCornersAll, 1.2f);

        var label = "设置";
        var textSize = ImGui.CalcTextSize(label);
        drawList.AddText(pos + (size - textSize) * 0.5f,
            SimplePalette.ToU32(SimplePalette.TextPrimary), label);

        if (hovered)
            ImGui.SetTooltip("打开 / 关闭 ACR 设置");
        if (clicked)
            toggleSettings();
    }
}
