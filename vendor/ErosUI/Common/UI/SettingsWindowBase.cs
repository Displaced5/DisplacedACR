using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;

namespace ErosUI;

// ErosUI 设置窗口基类（Dalamud WindowSystem 托管，多职业共用）。
// 夜间/日间模式 = 同一套侧边栏布局（SettingsWindowBase.Sidebar.cs）+ 深色/米白纯色半透明底色（无模糊）
// （配色分别在 SettingsWindowBase.Night.cs / .Day.cs，行样式见 SidebarSettingRow.cs）。
// 通过末位「主题」标签页切换（基类绘制），偏好存于 ErosUICommonSettings.UIMode。
public abstract partial class SettingsWindowBase : Window
{
    private int currentTab;
    private bool positionDirty;
    private bool wasOpen;

    // 本帧界面模式。PreDraw 时从设置读取并固定，保证一帧内 Push/Pop 样式数量一致（切换下一帧生效）。
    private SettingsUIMode mode;

    private static readonly Vector2 DefaultSize = new(760f, 560f);
    private static readonly Vector2 DefaultPosition = new(100f, 100f);
    private const float MinWindowSize = 200f;

    // 基础全局样式的颜色 Push 数量（Pop 时使用；侧边栏模式在此基础上增量 Push，见 SidebarChromeColorCount）。
    private const int BaseStyleColorCount = 22;

    // Tab 列表
    protected abstract string[] Tabs { get; }

    // 「主题」页标签（固定末位，由基类绘制，子类无需声明）。
    private const string ThemeTabLabel = "主题";

    private string[]? allTabs;
    private string[]? tabsSource;

    // 含末位「主题」页的完整标签列表。子类 Tabs 引用变化时重建并收敛 currentTab，避免索引越界。
    private string[] AllTabs
    {
        get
        {
            var tabs = Tabs;
            if (allTabs == null || !ReferenceEquals(tabs, tabsSource))
            {
                // 按标签名保持当前页（动态页插入/移除导致索引位移时不跳页）
                var currentLabel = allTabs != null && currentTab < allTabs.Length ? allTabs[currentTab] : null;
                tabsSource = tabs;
                allTabs = [.. tabs, ThemeTabLabel];
                var kept = currentLabel != null ? Array.IndexOf(allTabs, currentLabel) : -1;
                currentTab = kept >= 0 ? kept : Math.Min(currentTab, allTabs.Length - 1);
            }
            return allTabs;
        }
    }

    // 绘制当前 Tab 的内容
    protected abstract void DrawTabContent(int tabIndex);

    // 窗口位置/尺寸持久化（可选，返回 null 则不持久化）
    protected virtual WindowLayoutState? Layout => null;

    protected SettingsWindowBase(string title)
        : base(title, ImGuiWindowFlags.NoCollapse)
    {
        // 最小尺寸兜底：防止历史损坏的持久化尺寸或 ImGui 异常把窗口锁死
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(360f, 240f),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
        Size = DefaultSize;
        SizeCondition = ImGuiCond.FirstUseEver;
        Position = DefaultPosition;
        PositionCondition = ImGuiCond.FirstUseEver;
        RespectCloseHotkey = false;
        AllowBackgroundBlur = false;
    }

    public override void PreDraw()
    {

        // 模式在本帧开头固定：Draw 中的切换按钮写设置后，下一帧 PreDraw 才采用新值，
        // 避免同一帧内 Push/Pop 样式数量不一致。
        mode = ErosUICommonSettings.Instance.UIMode;

        Flags &= ~ImGuiWindowFlags.NoTitleBar;

        // 标题栏正常显示; 色彩方案同步写入 SimplePalette
        SimplePalette.ApplyScheme(mode == SettingsUIMode.Day
            ? SimplePalette.UIColorScheme.Light
            : SimplePalette.UIColorScheme.Dark);

        var layout = Layout;
        if (layout is { RememberPosition: true, Loaded: false })
        {
            layout.Loaded = true;

            // 历史数据可能已被垃圾值污染，只接受合理范围内的恢复值
            if (layout.SavedPosition is { } pos && IsSanePosition(pos))
            {
                Position = pos;
                PositionCondition = ImGuiCond.Once;
            }

            if (layout.SavedSize is { } size && IsSaneSize(size))
            {
                Size = size;
                SizeCondition = ImGuiCond.Once;
            }
        }

        PushGlobalStyle();
        base.PreDraw();
    }

    public override void Draw()
    {
        try
        {
            // 纯色半透明背景
            DrawWindowBackground();

            DrawSidebarLayout();

            // 只捕获落在合理范围内的位置/尺寸（防垃圾值）；待用户拖动/缩放结束后一次性写盘
            CaptureLayout();
        }
        catch (Exception ex)
        {
            ImGui.TextColored(new Vector4(1, 0, 0, 1), $"绘制错误: {ex.Message}");
        }
    }

    // 内容区（滚动, 填满内容高度）。
    private void DrawContentChild()
    {
        ImGui.BeginChild("##content", new Vector2(0f, 0f), false);
        try
        {
            // 末位「主题」页由基类绘制，其余标签交给子类
            if (currentTab >= Tabs.Length)
                DrawThemeTabContent();
            else
                DrawTabContent(currentTab);
        }
        catch (Exception ex)
        {
            ImGui.TextColored(new Vector4(1, 0, 0, 1), $"绘制错误: {ex.Message}");
        }
        ImGui.EndChild();
    }

    public override void PostDraw()
    {
        // 窗口刚被关闭：把最近一次捕获的脏数据落盘
        if (wasOpen && !IsOpen)
            FlushLayout();

        wasOpen = IsOpen;
        PopGlobalStyle();
        base.PostDraw();
    }

    private void CaptureLayout()
    {
        var layout = Layout;
        if (layout is not { RememberPosition: true }) return;

        var pos = ImGui.GetWindowPos();
        var size = ImGui.GetWindowSize();
        if (!IsSanePosition(pos) || !IsSaneSize(size)) return;

        layout.SavedPosition = pos;
        layout.SavedSize = size;

        // 用户已松手（拖动/缩放结束）才落盘一次，避免拖动过程中频繁写 JSON
        if (positionDirty && !ImGui.IsAnyItemActive() && !ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            positionDirty = false;
            layout.Save?.Invoke();
        }
        else if (ImGui.IsAnyItemActive() || ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            positionDirty = true;
        }
    }

    private void FlushLayout()
    {
        if (!positionDirty) return;
        positionDirty = false;
        Layout?.Save?.Invoke();
    }

    private static bool IsSaneSize(Vector2 v) =>
        v.X >= MinWindowSize && v.Y >= MinWindowSize && v.X < 10000f && v.Y < 10000f;

    private static bool IsSanePosition(Vector2 v) =>
        v.X >= -2000f && v.Y >= -2000f && v.X < 10000f && v.Y < 10000f;

    // 「主题」标签页：最上方主题主色 RGB 调色（见 DrawPrimaryColorSection），
    // 下方界面主题切换（当前主题仅标注不可点）。
    private void DrawThemeTabContent()
    {
        DrawPrimaryColorSection();
        ImGui.Spacing();

        SettingRow.SectionTitle("界面主题");
        ImGui.Spacing();
        DrawThemeOption("夜间", SettingsUIMode.Night, "侧边栏导航 + 深色纯色底");
        DrawThemeOption("日间", SettingsUIMode.Day, "侧边栏导航 + 米白纯色底");
    }

    // 主题主色 RGB 调色（主题页最上方）。
    // 全局唯一一份（Common.json 持久化），日/夜方案都生效，不随职业切换。
    private void DrawPrimaryColorSection()
    {
        var settings = ErosUICommonSettings.Instance;

        SettingRow.SectionTitle("主题色");
        ImGui.Spacing();

        var rgb = SimplePalette.GetPrimaryOverride()
            ?? new Vector3(SimplePalette.Primary.X, SimplePalette.Primary.Y, SimplePalette.Primary.Z);
        var changed = false;

        // 色块：点开 ImGui 调色器直接选色
        if (ImGui.ColorEdit3("##主题色", ref rgb,
                ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.NoLabel | ImGuiColorEditFlags.NoAlpha))
            changed = true;

        // 恢复内置配色：仅在已自定义时显示
        if (SimplePalette.HasPrimaryOverride)
        {
            ImGui.SameLine(0f, 12f);
            if (SettingRow.Button("恢复默认主题色"))
            {
                SimplePalette.SetPrimaryOverride(null);
                settings.PrimaryColorOverride = null;
                settings.Save();
            }
        }

        ImGui.Spacing();

        // R/G/B 0~255 滑杆：与色块双入口，拖动即时换肤（下一帧全窗口样式重算）
        var r = (int)MathF.Round(Math.Clamp(rgb.X, 0f, 1f) * 255f);
        var g = (int)MathF.Round(Math.Clamp(rgb.Y, 0f, 1f) * 255f);
        var b = (int)MathF.Round(Math.Clamp(rgb.Z, 0f, 1f) * 255f);
        ImGui.SetNextItemWidth(260f);
        if (ImGui.SliderInt("R", ref r, 0, 255)) changed = true;
        ImGui.SetNextItemWidth(260f);
        if (ImGui.SliderInt("G", ref g, 0, 255)) changed = true;
        ImGui.SetNextItemWidth(260f);
        if (ImGui.SliderInt("B", ref b, 0, 255)) changed = true;

        if (!changed) return;

        var c = new Vector3(r / 255f, g / 255f, b / 255f);
        SimplePalette.SetPrimaryOverride(c);
        settings.PrimaryColorOverride = c;
        settings.Save();
    }

    private void DrawThemeOption(string label, SettingsUIMode mode, string tooltip)
    {
        if (this.mode == mode)
        {
            SettingRow.Button($"{label}（当前）", tooltip);
            return;
        }
        if (SettingRow.Button(label, tooltip))
        {
            ErosUICommonSettings.Instance.SetUIMode(mode);
        }
    }

    private void DrawWindowBackground()
    {
        // 仅主视口生效；夜间/日间用各自常量纯色半透明底色
        if (ImGui.GetWindowViewport().ID != ImGui.GetMainViewport().ID) return;

        // 底色画进窗口自身的绘制列表，先垫占位命令防止背景模糊顶掉首条绘制（见 ErosUILayer），
        // 保证两个窗口重叠时背景仍然盖住身后窗口的内容
        var min = ImGui.GetWindowPos() + new Vector2(0.5f);
        var max = min + ImGui.GetWindowSize() - new Vector2(1f);

        // 圆角必须与 PreDraw 压入的 WindowRounding(12f) 完全一致，否则底色的角与窗口边框错位
        var rounding = 12f;
        var tint = mode switch
        {
            SettingsUIMode.Night => NightBackgroundTint,
            SettingsUIMode.Day => DayBackgroundTint,
            _ => NightBackgroundTint,
        };
        var drawList = ImGui.GetWindowDrawList();
        ErosUILayer.垫牺牲帧(drawList);
        // 背景画满全窗口但让出标题栏: Begin 给窗口 draw list 压的是内容区内层裁剪（减内边距）,
        // 不覆盖会把底色四条边各裁掉一条; 标题栏区不铺底色, 保持原生标题栏可读
        drawList.PushClipRect(new Vector2(ImGui.GetWindowPos().X, ImGui.GetWindowPos().Y + ImGui.GetFrameHeight()),
            ImGui.GetWindowPos() + ImGui.GetWindowSize(), false);
        drawList.AddRectFilled(min, max, SimplePalette.ToU32(tint), rounding, ImDrawFlags.RoundCornersAll);
        drawList.PopClipRect();
    }

    private void PushGlobalStyle()
    {
        // 窗口 + 内容区 child 都透明，背景统一由 DrawWindowBackground 自绘（夜间/日间纯色）
        ImGui.PushStyleColor(ImGuiCol.WindowBg, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.Border, SimplePalette.Border);

        // 文本
        ImGui.PushStyleColor(ImGuiCol.Text, SimplePalette.TextPrimary);
        ImGui.PushStyleColor(ImGuiCol.TextDisabled, SimplePalette.TextDisabled);

        // 按钮（侧边栏导航用，其余控件由主题色着色）
        ImGui.PushStyleColor(ImGuiCol.Button, SimplePalette.FrameBg);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, SimplePalette.FrameBgHovered);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, SimplePalette.FrameBgActive);

        // FrameBg（Checkbox/Radio/Input/Drag 背景）
        ImGui.PushStyleColor(ImGuiCol.FrameBg, SimplePalette.FrameBg);
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, SimplePalette.FrameBgHovered);
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, SimplePalette.FrameBgActive);

        // 强调色：Checkbox 勾选、Drag grab、Slider grab（Accent 在浅色方案下自动暗一档）
        ImGui.PushStyleColor(ImGuiCol.CheckMark, SimplePalette.Accent);
        ImGui.PushStyleColor(ImGuiCol.SliderGrab, SimplePalette.Accent);
        ImGui.PushStyleColor(ImGuiCol.SliderGrabActive, SimplePalette.PrimaryActive);

        // Header（Combo 下拉项）
        ImGui.PushStyleColor(ImGuiCol.Header, SimplePalette.FrameBgHovered);
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, SimplePalette.FrameBgActive);
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, SimplePalette.FrameBgActive);

        // Popup（Combo 下拉等; 随方案取色, 硬编码深色会让日间模式深字画黑底不可读）
        ImGui.PushStyleColor(ImGuiCol.PopupBg, SimplePalette.PopupBg);

        // Separator
        ImGui.PushStyleColor(ImGuiCol.Separator, SimplePalette.Border);

        // Resize grip
        ImGui.PushStyleColor(ImGuiCol.ResizeGrip, SimplePalette.WithAlpha(SimplePalette.Primary, 0.2f));
        ImGui.PushStyleColor(ImGuiCol.ResizeGripHovered, SimplePalette.WithAlpha(SimplePalette.Primary, 0.4f));
        ImGui.PushStyleColor(ImGuiCol.ResizeGripActive, SimplePalette.Primary);

        // 侧边栏模式增量样式（标题栏/滚动条）：夜间/日间实现在 .Night.cs / .Day.cs
        if (mode == SettingsUIMode.Night) PushNightChromeColors();
        else if (mode == SettingsUIMode.Day) PushDayChromeColors();

        // 变量
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 12f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 6f);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, 6f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(16f, 12f));
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(8f, 4f));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8f, 6f));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemInnerSpacing, new Vector2(6f, 4f));
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarSize, 10f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 1f);
    }

    private void PopGlobalStyle()
    {
        ImGui.PopStyleVar(9);
        ImGui.PopStyleColor(BaseStyleColorCount + SidebarChromeColorCount);
    }

    // 窗口位置/尺寸持久化状态，由子类持有。
    public sealed class WindowLayoutState
    {
        public bool Loaded;
        public bool RememberPosition = true;
        public Vector2? SavedPosition;
        public Vector2? SavedSize;

        // 保存回调；子类在构造完成后赋值。
        public System.Action? Save;
    }
}
