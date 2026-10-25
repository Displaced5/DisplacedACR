using System.Numerics;
using Dalamud.Bindings.ImGui;
using PromeRotation.Helpers;
using PromeRotation.Data;

namespace ErosUI;

// 设置面板 UI（多职业共用）。
// QT面板页经 ErosUIJobEnv 读当前职业 QT 表；
// 主题 为多职业通用内容（ErosUICommonSettings）。
public static class ErosUISettingsUI
{
    // ============================================================
    // === 模式切换行（基础设置 / QT面板 各页共用） ===
    // ============================================================

    // 模式切换行：模式色「当前模式：xx」+ 同排胶囊切换按钮。
    public static void DrawModeSwitchRow()
    {
        var h = ErosUISettings.Instance.IsHighEnd;

        ImGui.AlignTextToFramePadding();   // 文字与 28 高胶囊垂直居中
        ImGui.PushStyleColor(ImGuiCol.Text,
            h ? SimplePalette.ModeHighEndText : SimplePalette.ModeDailyText);
        ImGui.Text($"当前模式：{(h ? "高难" : "日随")}");
        ImGui.PopStyleColor();

        ImGui.SameLine(0f, 10f);
        var clicked = ModeCapsule(h ? "切换日随" : "切换高难");
        if (clicked)
        {
            HintHelper.ShowToast2($"切换至{(h ? "日随" : "高难")}模式", 2, HintHelper.HintType.Info);
            ErosUISettings.Instance.SwitchMode(!h);
        }
    }

    // 模式胶囊按钮：透明底 + 悬停淡染，宽随文字、高 28；文字用主文字色（与其他按钮一致）。
    private static bool ModeCapsule(string label)
    {
        var style = ImGui.GetStyle();
        var w = ImGui.CalcTextSize(label).X + style.FramePadding.X * 2f;
        ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, SimplePalette.FrameBgHovered);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, SimplePalette.FrameBgActive);
        ImGui.PushStyleColor(ImGuiCol.Text, SimplePalette.TextPrimary);
        var clicked = ImGui.Button(label, new Vector2(w, 28f));
        ImGui.PopStyleColor(4);
        return clicked;
    }

    // 界面滑杆行：原生带标签滑杆（宽随调用方）。返回是否刚松手（提交信号, 供调用方落盘）。
    private static bool 布局滑杆Int(string label, ref int value, int min, int max, float width)
    {
        ImGui.SetNextItemWidth(width);
        ImGui.SliderInt(label, ref value, min, max);
        return ImGui.IsItemDeactivatedAfterEdit();
    }

    // ============================================================
    // 独立控制 QT 面板与热键面板悬浮窗的显隐（基础设置页「面板控制」分节）
    private static void DrawPanelsVisibilityButton()
    {
        var qtVisible = ErosUIFramework.QtPanelVisible;
        if (SettingRow.Button(qtVisible ? "隐藏 QT 面板" : "显示 QT 面板",
                "显示/隐藏 QT 开关悬浮面板"))
            ErosUIFramework.SetQtPanelVisible(!qtVisible);

        var hotkeyVisible = ErosUIFramework.HotkeyPanelVisible;
        ImGui.SameLine(0f, 12f);
        if (SettingRow.Button(hotkeyVisible ? "隐藏 Hotkey 面板" : "显示 Hotkey 面板",
                "显示/隐藏热键按钮悬浮面板"))
            ErosUIFramework.SetHotkeyPanelVisible(!hotkeyVisible);
    }

    // ============================================================
    public static void DrawGeneral()
    {
        Hdr("面板控制");
        DrawPanelsVisibilityButton();

        Hdr("基础设置");
        DrawModeSwitchRow();
        var resetQt = ErosUISettings.Instance.DeathAutoResetQt;
        if (SettingRow.Checkbox("死亡自动重置QT", ref resetQt))
        {
            ErosUISettings.Instance.DeathAutoResetQt = resetQt;
            ErosUISettings.Instance.Save();
        }
        ErosUIJobEnv.DrawBasicSettings?.Invoke();
    }

    public static void DrawExtensions()
    {
        Hdr("拓展功能");
        var longArms = ErosUISettings.Instance.LongArms;
        if (ImGui.Checkbox("长臂猿", ref longArms))
        {
            ErosUISettings.Instance.LongArms = longArms;
            ErosUISettings.Instance.Save();
        }
        var unlimitedDash = ErosUISettings.Instance.UnlimitedDashDistance;
        if (ImGui.Checkbox("突进距离无限制", ref unlimitedDash))
        {
            ErosUISettings.Instance.UnlimitedDashDistance = unlimitedDash;
            ErosUISettings.Instance.Save();
        }
    }

    // ============================================================
    // === QT面板页：顶部按钮在「QT显隐 / QT默认值」两个子视图间切换 ===
    // ============================================================

    // QT面板页子视图（显隐管理 / 默认值管理）；会话内记住上次选择，不写入配置文件。
    private enum QtSubview
    {
        显隐,
        默认值,
    }

    private static QtSubview _qtSubview = QtSubview.显隐;

    // QT面板页：顶部切换按钮行 + 按所选子视图绘制显隐管理 / 默认值管理。
    public static void DrawQtPanel()
    {
        QtSubviewButton("QT显隐", QtSubview.显隐);
        ImGui.SameLine(0f, 8f);
        QtSubviewButton("QT默认值", QtSubview.默认值);

        ImGui.Separator();

        if (_qtSubview == QtSubview.显隐) DrawQtManage();
        else DrawDefaultsManage();
    }

    // 子视图切换按钮：激活项实底高亮 + 主文字色，未激活透明底 + 次级文字色；宽随文字、高 28。
    private static void QtSubviewButton(string label, QtSubview view)
    {
        var isActive = _qtSubview == view;
        if (isActive)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, SimplePalette.NavActiveBg);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, SimplePalette.WithAlpha(SimplePalette.Accent, 0.25f));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, SimplePalette.WithAlpha(SimplePalette.Accent, 0.35f));
            ImGui.PushStyleColor(ImGuiCol.Text, SimplePalette.NavActiveText);
        }
        else
        {
            ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, SimplePalette.FrameBgHovered);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, SimplePalette.FrameBgActive);
            ImGui.PushStyleColor(ImGuiCol.Text, SimplePalette.TextSecondary);
        }

        var style = ImGui.GetStyle();
        var w = ImGui.CalcTextSize(label).X + style.FramePadding.X * 2f;
        if (ImGui.Button(label, new Vector2(w, 28f)))
            _qtSubview = view;

        ImGui.PopStyleColor(4);
    }

    // ============================================================
    private static void DrawQtManage()
    {
        Hdr("QT 按钮显隐管理");
        ImGui.TextWrapped("勾选 = 在 QT 面板显示该开关按钮；取消勾选 = 从 QT 面板隐藏。");
        ImGui.TextWrapped("显隐配置按日随/高难各存一套，改动只作用于当前模式；切换模式自动套用该模式的保存记录。");
        ImGui.TextWrapped("模式专属开关只在所属模式的列表中显示（如日随专属开关不会出现在高难列表）。");
        ImGui.TextWrapped("只控制显隐，不改变开关的当前状态；隐藏的开关在循环逻辑中仍按当前状态生效。");
        DrawModeSwitchRow();

        // QT 面板布局调整（同 Hotkey 页滑块模式; 面板每帧按设置重排, 改动松手即存即生效）
        Hdr("QT 面板");
        ImGui.TextWrapped("QT 悬浮面板：布局改动松手后自动保存并即时生效。");
        var s = ErosUISettings.Instance;
        var sliderWidth = 360f;

        int cols = s.QtPanelColumns;
        var colsSaved = 布局滑杆Int("每行数量", ref cols, 1, 6, sliderWidth);
        s.QtPanelColumns = cols;          // 拖动中只改内存值, 面板实时跟随
        bool save = colsSaved;            // 松手才落盘

        int spacing = s.QtPanelSpacing;
        save |= 布局滑杆Int("间隔(px)", ref spacing, 0, 20, sliderWidth);
        s.QtPanelSpacing = spacing;

        int scale = s.QtPanelScalePercent;
        save |= 布局滑杆Int("缩放(%)", ref scale, 50, 200, sliderWidth);
        s.QtPanelScalePercent = scale;

        if (save) s.Save();

        // QT 排序模块: 标题 + 排序说明 + 锁定开关
        Hdr("QT 排序");
        ImGui.TextWrapped("QT 面板按钮的排列顺序在悬浮面板上按住鼠标右键拖动调整（同一分类组内换位），改动即时生效并与本页顺序同步。");
        var locked = s.QtPanelOrderLocked;
        if (SettingRow.Checkbox("锁定QT排序", ref locked, "开启后 QT 悬浮面板按钮不可右键拖拽换位（防战斗误拖）"))
        {
            s.QtPanelOrderLocked = locked;
            s.Save();
        }

        ImGui.Separator();   // 排序模块与下方显隐列表的分界线

        // 全部 QT 平铺一列: 显隐勾选（排列顺序在悬浮面板右键拖动调整, 各页同序）
        DrawQtVisibleList();
    }

    // QT 按钮显隐勾选列表（顺序 = ErosUISettings.QtOrder 自定义序, 与悬浮面板一致; 只绑显隐, 不绑开关状态）。
    // 模式专属开关在另一模式的面板不会注册, 勾选了也不显示, 直接不列出（与 QT面板页默认值子视图同口径）。
    private static void DrawQtVisibleList()
    {
        foreach (var key in ErosUISettings.Instance.GetOrderedQtKeys())
        {
            if (ErosUIJobEnv.QtIsMetaKey(key)) continue;
            if (!ErosUIJobEnv.QtIsVisibleInMode(key, ErosUISettings.Instance.IsHighEnd)) continue;

            var v = ErosUISettings.Instance.IsQtVisible(key);
            if (SettingRow.Checkbox(key, ref v))
                ErosUISettings.Instance.SetQtVisible(key, v);
        }
    }

    // ============================================================
    public static void DrawHotkey()
    {
        var s = ErosUISettings.Instance;

        Hdr("热键面板");
        ImGui.TextWrapped("手动热键悬浮窗：布局改动松手后或显隐勾选后自动重建面板并保存。");
        bool rebuild = false;

        // 三个滑块统一为 360px，避免滑块过长或过短
        var sliderWidth = 360f;

        int columns = s.HotkeyColumns;
        rebuild |= 布局滑杆Int("每行数量", ref columns, 1, 12, sliderWidth);
        s.HotkeyColumns = columns;                      // 拖动中只改内存值
        // 松手才重建，避免拖动中每帧建销面板窗口

        int spacing = s.HotkeySpacing;
        rebuild |= 布局滑杆Int("间隔(px)", ref spacing, 0, 20, sliderWidth);
        s.HotkeySpacing = spacing;

        int scale = s.HotkeyScalePercent;
        rebuild |= 布局滑杆Int("缩放(%)", ref scale, 50, 200, sliderWidth);
        s.HotkeyScalePercent = scale;

        // 热键排序模块: 标题 + 排序说明 + 锁定开关（与 QT 排序模块同一套交互）
        Hdr("热键排序");
        ImGui.TextWrapped("热键面板按钮的排列顺序在悬浮面板上按住鼠标右键拖动调整，改动即时生效并与本页顺序同步。");
        var locked = s.HotkeyPanelOrderLocked;
        if (SettingRow.Checkbox("锁定热键排序", ref locked, "开启后热键悬浮面板按钮不可右键拖拽换位（防战斗误拖）"))
        {
            s.HotkeyPanelOrderLocked = locked;
            s.Save();
        }

        ImGui.Separator();

        ImGui.TextWrapped("勾选 = 在热键面板显示该按钮；取消勾选 = 从热键面板隐藏。");
        // 全部热键平铺一列: 显隐勾选（排列顺序在悬浮面板右键拖动调整, 各页同序）
        foreach (var name in s.GetOrderedHotkeyNames())
        {
            bool shown = !s.HiddenHotkeys.Contains(name);
            if (SettingRow.Checkbox(name, ref shown))
            {
                if (shown) s.HiddenHotkeys.Remove(name);
                else if (!s.HiddenHotkeys.Contains(name)) s.HiddenHotkeys.Add(name);
                s.Save();
                rebuild = true;
            }
        }

        if (rebuild)
        {
            s.Save();
            ErosUIHotkeyUI.Rebuild();
        }
    }

    private static void DrawDefaultsManage()
    {
        Hdr("默认值管理");
        ImGui.TextWrapped("勾选框直接修改当前模式的 QT 默认值，改动即时生效并自动保存；切换模式自动切换到对应模式的默认值。");
        DrawModeSwitchRow();   // 切到哪个模式就显示哪个模式的默认值

        ImGui.Separator();

        // 当前模式的默认值平铺一列（单列跟随当前模式; 顺序 = 自定义排序, 与悬浮面板一致）
        var s = ErosUISettings.Instance;
        var dict = s.GetCurrentModeDefaults();
        foreach (var key in s.GetOrderedQtKeys())
        {
            if (ErosUIJobEnv.QtIsMetaKey(key)) continue;
            if (!ErosUIJobEnv.QtIsVisibleInMode(key, s.IsHighEnd)) continue;   // 当前模式下隐藏的 QT 不显示

            var v = dict.TryGetValue(key, out var dv) ? dv : ErosUIJobEnv.QtDefault(key);
            if (SettingRow.Checkbox(key, ref v))
            {
                dict[key] = v;
                APIHelper.设置QT(key, v);   // 显示的就是当前模式: 立即同步 QT 面板实际状态
                s.Save();
            }
        }

        ImGui.Separator();

        if (SettingRow.Button("从当前QT导入"))
        {
            s.SaveQtSnapshot(s.IsHighEnd);
            s.Save();
            HintHelper.ShowToast2("已用 QT 面板当前状态覆盖本模式默认值", 3, HintHelper.HintType.Info);
        }
    }

    // 分节标题（主题色圆点 + 主文字 + 尾随细线）
    private static void Hdr(string t) => SettingRow.SectionTitle(t);
}
