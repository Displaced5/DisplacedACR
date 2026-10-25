using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using PromeRotation.Data;
using PromeRotation.UI.QuickToggles;

namespace ErosUI;

// ErosUI 风格 QT 面板悬浮窗：
// 由 ErosUIFramework.Install 注册进宿主 WindowSystem，Uninstall 摘除；显隐由设置窗口基础设置页「面板控制」切换。
// 开关清单来自 PR 的 QtRegistry.ResolveVisible（QuickToggles − HiddenQts），
// 状态读写走 PromeSettings.Instance.GetQt/SetQt（与 PR 原生 QT 区实时同步）。
// 按钮按 QtDefinition.Category 分节绘制：同分类一组，组标题 + 网格，组序 = 各组首成员在显示序中的先后；
// 分类为空的组不画标题（全部无分类时布局与旧版平铺完全一致）。
public sealed class ErosUIQtPanelWindow : Window
{
    // 布局参数可调（QT面板页滑块 → ErosUISettings.QtPanel*，每帧读取即时生效）：
    // 基准格 112×34、每行 3、间距 10
    private static int 每行 => Math.Clamp(ErosUISettings.Instance.QtPanelColumns, 1, 6);
    private static float 格宽 => 112f * ErosUISettings.Instance.QtPanelScalePercent / 100f;
    private static float 格高 => 34f * ErosUISettings.Instance.QtPanelScalePercent / 100f;
    private static float 间距 => Math.Clamp(ErosUISettings.Instance.QtPanelSpacing, 0f, 20f);

    // 右键拖拽换位状态（面板级, 一次只拖一格; 左键点击切开关与拖拽完全解耦）：
    // 拖拽源 = defs 平铺索引, 拖拽目标 = 源组内的槽位（拖拽不跨组, 拖到他组 = 落回本组边缘）
    private static int 拖拽源 = -1;
    private static int 拖拽目标;
    private static Vector2 拖拽偏移;
    // 格子视觉位置（按 QT id 索引, 窗口相对坐标, 拖动面板时按钮随窗刚性平移）：
    // 拖拽让位/松手回落时指数阻尼收敛到格位
    private static readonly Dictionary<string, Vector2> 动画格位 = new(StringComparer.Ordinal);
    private static int[] 显示序暂存 = Array.Empty<int>();

    // 一个分类分组：组标题 + 组内条目（defs 平铺索引，组内顺序 = 面板显示顺序）。
    // 布局字段（标题高/内容Y/行数）每帧由 布局分组 填充。
    private sealed class 分类分组
    {
        public string 标题 = string.Empty;
        public readonly List<int> 条目 = new();
        public float 标题高;   // 组标题占用高度（含与首行格子的间距）；空标题 = 0
        public float 内容Y;    // 组网格相对窗口内容区原点的 Y 偏移
        public int 行数;       // 组网格行数（按面板列数换行）
    }

    public ErosUIQtPanelWindow() : base($"{ErosUIJobEnv.作者}{ErosUIJobEnv.JobTag} QT面板",
        ImGuiWindowFlags.NoTitleBar
        | ImGuiWindowFlags.NoCollapse
        | ImGuiWindowFlags.NoScrollbar
        | ImGuiWindowFlags.NoResize      // 去掉右下角缩放 grip 与边缘缩放
        | ImGuiWindowFlags.NoBackground  // ImGui 的 WindowBg/边框完全不画, 背景全由 DrawWindowChrome 接管
        | ImGuiWindowFlags.NoNav)
    {
        RespectCloseHotkey = false;
        AllowBackgroundBlur = false;
    }

    public override void PreDraw()
    {
        var day = ErosUICommonSettings.Instance.UIMode == SettingsUIMode.Day;
        SimplePalette.ApplyScheme(day ? SimplePalette.UIColorScheme.Light
                                      : SimplePalette.UIColorScheme.Dark);

        ImGui.PushStyleColor(ImGuiCol.Text, SimplePalette.TextPrimary);
        ImGui.PushStyleColor(ImGuiCol.TextDisabled, SimplePalette.TextDisabled);
        // Border 色同时是按钮边框色（FrameBorderSize>0 时），改动会影响所有按钮描边——勿删
        ImGui.PushStyleColor(ImGuiCol.Border, day ? new Vector4(0f, 0f, 0f, 0.10f)
                                                  : new Vector4(1f, 1f, 1f, 0.10f));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 12f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(14f, 12f));
        // 网格格子全部绝对定位、不吃 ItemSpacing；与格距保持一致仅为「无可用开关」提示等流式控件兜底
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(间距, 间距));
        base.PreDraw();
    }

    public override void Draw()
    {
        IReadOnlyList<QtDefinition> defs;
        try
        {
            defs = QtRegistry.ResolveVisible(
                PromeSettings.Instance.QuickToggles.Keys,
                PromeSettings.Instance.HiddenQts);
        }
        catch
        {
            return;
        }

        // 按用户自定义顺序排列（面板右键拖拽调整; 未调整过 = 注册原序）
        defs = 按自定义顺序排序(defs);

        // 按 Category 分组并计算组布局（列数/标题高/组偏移）
        var groups = 构建分组(defs);
        var cols = 布局分组(groups);

        // 每帧显式设置尺寸，让窗口精确贴合按钮网格。
        // 不要改回 AlwaysAutoResize：自适应尺寸会被窗口状态持久化干扰，
        // 导致窗口比内容宽出一截、左右多出透明边。
        ImGui.SetWindowSize(计算窗口尺寸(groups, cols, defs.Count), ImGuiCond.Always);

        DrawWindowChrome();

        var count = defs.Count;
        if (count == 0)
        {
            ImGui.TextDisabled("无可用开关（在设置的 QT面板页配置显隐）");
            HandleWindowDrag();
            return;
        }

        // 右键拖拽换位: 锁定开关只封拖拽, 点击开关不受影响
        var locked = ErosUISettings.Instance.QtPanelOrderLocked;

        // 拖拽源所在组与组内槽位; 拖拽中途清单变化导致源消失时取消本次拖拽
        var 源组 = -1;
        var 源槽 = -1;
        if (拖拽源 >= 0)
        {
            (源组, 源槽) = 查组内槽位(groups, 拖拽源);
            if (源组 < 0)
            {
                拖拽源 = -1;
                拖拽目标 = -1;
            }
        }

        // 拖拽目标 = 鼠标按源组网格换算的组内槽位（不依赖条目悬停判定, 上移/下移同权）;
        // 组内其它格子按「假设此刻松手」的显示序列实时让位, 指数阻尼滑向新格位
        if (拖拽源 >= 0)
            拖拽目标 = 计算拖拽目标(ImGui.GetIO().MousePos, groups[源组], cols);

        // 逐组绘制（组标题 + 网格）: 静止格子先画, 拖拽源最后画（跟随鼠标, 不被其它格子遮挡）;
        // 动画位置按窗口相对坐标存储, 绘制时加上窗口原点——拖动面板时按钮随窗刚性平移, 不产生追赶动画
        var winPos = ImGui.GetWindowPos();
        var scratch = 显示序暂存;
        for (var gi = 0; gi < groups.Count; gi++)
        {
            var g = groups[gi];
            画分类标题(g, winPos);
            var n = g.条目.Count;
            var 显示序 = VisibleOrderHelper.BuildDisplayOrder(n,
                gi == 源组 ? 源槽 : -1, gi == 源组 ? 拖拽目标 : -1, scratch);
            scratch = 显示序;
            for (var slot = 0; slot < n; slot++)
            {
                var tileIndex = g.条目[显示序[slot]];
                if (tileIndex == 拖拽源) continue;
                画开关(defs[tileIndex], winPos + 取动画格位(defs[tileIndex].Id, 格位偏移(g, slot, cols)), locked, tileIndex);
            }
        }
        显示序暂存 = scratch;

        if (拖拽源 >= 0 && 拖拽源 < count)
        {
            var 源 = defs[拖拽源];
            var pos = ImGui.GetIO().MousePos - 拖拽偏移 - winPos;
            动画格位[源.Id] = pos;   // 记录源格视觉位置（窗口相对）, 松手提交/取消后从该处平滑回落
            画开关(源, winPos + pos, locked, 拖拽源);
        }

        清理动画格位(defs);

        if (拖拽源 >= 0)
        {
            if (ImGui.IsMouseReleased(ImGuiMouseButton.Right))
            {
                // 松手落位: 组内目标槽 → 该槽当前条目在全序中的索引（插入语义与组内让位一致）,
                // MoveQt 内落盘并重建注册序, 让位动画按新格位收敛; 拖到格外/原位 = 取消
                if (!locked && 拖拽目标 >= 0 && 拖拽目标 != 源槽)
                    ErosUISettings.Instance.MoveQt(defs[拖拽源].Id, groups[源组].条目[拖拽目标]);
                拖拽源 = -1;
                拖拽目标 = -1;
            }
            else if (!ImGui.IsMouseDown(ImGuiMouseButton.Right))
            {
                拖拽源 = -1;   // 松手帧面板未绘制（错过释放）, 兜底取消
            }
            // 落点标记: 高亮描边
            else if (拖拽目标 >= 0 && 拖拽目标 != 源槽)
            {
                画目标格高亮(groups[源组], 拖拽目标, cols);
            }
        }

        HandleWindowDrag();
    }

    public override void PostDraw()
    {
        ImGui.PopStyleVar(3);
        ImGui.PopStyleColor(3);
        base.PostDraw();
    }

    // 按 Category 分组（保持显示顺序）：同分类的条目归一组，组序 = 各组首成员出现的先后；
    // 分类为空/纯空白的条目并入无标题组。
    private static List<分类分组> 构建分组(IReadOnlyList<QtDefinition> defs)
    {
        var groups = new List<分类分组>();
        var byLabel = new Dictionary<string, 分类分组>(StringComparer.Ordinal);
        for (var i = 0; i < defs.Count; i++)
        {
            var label = defs[i].Category;
            label = string.IsNullOrWhiteSpace(label) ? string.Empty : label;
            if (!byLabel.TryGetValue(label, out var g))
            {
                g = new 分类分组 { 标题 = label };
                byLabel.Add(label, g);
                groups.Add(g);
            }
            g.条目.Add(i);
        }
        return groups;
    }

    // 填充各组的布局字段并返回面板列数：列数 = min(每行设置, 最大组条目数)，全组共用同一列网格。
    private static int 布局分组(List<分类分组> groups)
    {
        var max = 1;
        foreach (var g in groups) max = Math.Max(max, g.条目.Count);
        var cols = Math.Min(每行, max);

        var textH = ImGui.GetTextLineHeight();
        var y = 0f;
        foreach (var g in groups)
        {
            g.标题高 = g.标题.Length == 0 ? 0f : textH + 间距;
            g.内容Y = y + g.标题高;
            g.行数 = (g.条目.Count + cols - 1) / cols;
            y = g.内容Y + g.行数 * 格高 + (g.行数 - 1) * 间距 + 间距;   // 末尾 间距 = 与下一组的组距
        }
        return cols;
    }

    // 窗口尺寸 = 分组网格 + 组标题 + 内边距，精确贴合、无多余边。
    private static Vector2 计算窗口尺寸(List<分类分组> groups, int cols, int count)
    {
        if (count == 0) return new Vector2(260f, 110f);
        var pad = ImGui.GetStyle().WindowPadding;
        var last = groups[^1];
        var contentH = last.内容Y + last.行数 * 格高 + (last.行数 - 1) * 间距;
        return new Vector2(cols * 格宽 + (cols - 1) * 间距 + pad.X * 2f,
                           contentH + pad.Y * 2f);
    }

    // 查平铺索引所属分组与组内槽位；不在任何组（清单已变化）返回 (-1, -1)。
    private static (int 组, int 槽) 查组内槽位(List<分类分组> groups, int flatIndex)
    {
        for (var gi = 0; gi < groups.Count; gi++)
        {
            var slot = groups[gi].条目.IndexOf(flatIndex);
            if (slot >= 0) return (gi, slot);
        }
        return (-1, -1);
    }

    // 把宿主解析出的 QT 清单按 ErosUISettings.QtOrder 的自定义顺序重排:
    // 顺序表里没有的键按原相对顺序置尾（LINQ OrderBy 稳定排序）。
    private static IReadOnlyList<QtDefinition> 按自定义顺序排序(IReadOnlyList<QtDefinition> defs)
    {
        if (defs.Count < 2) return defs;
        var order = ErosUISettings.Instance.GetOrderedQtKeys();
        var rank = new Dictionary<string, int>(order.Count);
        for (var i = 0; i < order.Count; i++) rank[order[i]] = i;
        return defs.OrderBy(d => rank.TryGetValue(d.Id, out var r) ? r : int.MaxValue).ToList();
    }

    // 拖拽目标 = 鼠标位置按源组网格节距（格宽/格高 + 间距）换算的组内槽位并钳制在组内
    // （拖到他组/组标题 = 落回本组边缘）; 鼠标在窗口外返回 -1（拖出面板 = 取消）。
    private static int 计算拖拽目标(Vector2 鼠标, 分类分组 g, int cols)
    {
        var pos = ImGui.GetWindowPos();
        var size = ImGui.GetWindowSize();
        if (鼠标.X < pos.X || 鼠标.Y < pos.Y || 鼠标.X >= pos.X + size.X || 鼠标.Y >= pos.Y + size.Y)
            return -1;
        var 原点 = pos + ImGui.GetStyle().WindowPadding + new Vector2(0f, g.内容Y);
        var 列 = Math.Clamp((int)((鼠标.X - 原点.X) / (格宽 + 间距)), 0, cols - 1);
        var 行 = Math.Clamp((int)((鼠标.Y - 原点.Y) / (格高 + 间距)), 0, g.行数 - 1);
        return Math.Clamp(行 * cols + 列, 0, g.条目.Count - 1);
    }

    // 拖拽中的目标格高亮描边（Accent 色, 格子矩形外扩 2px; 按源组网格坐标定位, 不依赖条目矩形）。
    private static void 画目标格高亮(分类分组 g, int slot, int cols)
    {
        var min = ImGui.GetWindowPos() + 格位偏移(g, slot, cols);
        ImGui.GetWindowDrawList().AddRect(min - new Vector2(2f), min + new Vector2(格宽, 格高) + new Vector2(2f),
            SimplePalette.ToU32(SimplePalette.Accent), 10f, ImDrawFlags.RoundCornersAll, 2f);
    }

    // 组标题：分类名（次要文字色）+ 右侧细分隔线，画进窗口绘制列表（不占布局、不挡空白处拖窗）。
    private static void 画分类标题(分类分组 g, Vector2 winPos)
    {
        if (g.标题.Length == 0) return;
        var pad = ImGui.GetStyle().WindowPadding;
        var origin = winPos + pad + new Vector2(0f, g.内容Y - g.标题高);
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddText(origin, SimplePalette.ToU32(SimplePalette.TextSecondary), g.标题);

        var right = winPos.X + ImGui.GetWindowSize().X - pad.X;
        var x1 = origin.X + ImGui.CalcTextSize(g.标题).X + 8f;
        if (right - x1 > 12f)
        {
            var day = ErosUICommonSettings.Instance.UIMode == SettingsUIMode.Day;
            var line = day ? new Vector4(0f, 0f, 0f, 0.10f)
                           : new Vector4(1f, 1f, 1f, 0.10f);
            var y = origin.Y + ImGui.GetTextLineHeight() * 0.5f;
            drawList.AddLine(new Vector2(x1, y), new Vector2(right, y), SimplePalette.ToU32(line), 1f);
        }
    }

    // 组内槽位 slot 相对窗口原点的网格偏移（含窗口内边距与组标题/组距偏移，与 画目标格高亮/计算窗口尺寸 同一网格算法）。
    private static Vector2 格位偏移(分类分组 g, int slot, int cols)
        => ImGui.GetStyle().WindowPadding
           + new Vector2((slot % cols) * (格宽 + 间距),
                         g.内容Y + (slot / cols) * (格高 + 间距));

    // 格子视觉位置指数阻尼收敛到目标格位（窗口相对坐标）; 新出现的格子直接落位, 不做飞入动画。
    private static Vector2 取动画格位(string id, Vector2 目标)
    {
        if (!动画格位.TryGetValue(id, out var 当前))
        {
            动画格位[id] = 目标;
            return 目标;
        }

        var next = VisibleOrderHelper.AnimatePosition(当前, 目标, ImGui.GetIO().DeltaTime);
        动画格位[id] = next;
        return next;
    }

    // QT 清单变化后移除失效条目的动画位置记录。
    private static void 清理动画格位(IReadOnlyList<QtDefinition> defs)
    {
        if (动画格位.Count <= defs.Count) return;
        var live = new HashSet<string>(StringComparer.Ordinal);
        foreach (var def in defs) live.Add(def.Id);
        foreach (var id in 动画格位.Keys.Where(id => !live.Contains(id)).ToArray())
            动画格位.Remove(id);
    }

    // 单个开关按钮：绝对定位绘制（格位由让位动画给出）; 起拖判定挂在该格悬停上, 记录抓取偏移供源格跟随鼠标。
    private static void 画开关(QtDefinition def, Vector2 pos, bool locked, int tileIndex)
    {
        var on = PromeSettings.Instance.GetQt(def.Id);

        ImGui.SetCursorScreenPos(pos);
        ImGui.PushID(def.Id);
        if (on)
            ImGui.PushStyleColor(ImGuiCol.Button, SimplePalette.WithAlpha(SimplePalette.Accent, 0.30f));
        else
            ImGui.PushStyleColor(ImGuiCol.Button, SimplePalette.FrameBg);
        ImGui.PushStyleColor(ImGuiCol.Text, on ? SimplePalette.TextPrimary : SimplePalette.TextSecondary);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 8f);

        // 开关状态只靠底色/字色表达——勿加 ☑/☐ 等字形前缀，游戏字体集缺字形会渲染成乱码
        if (ImGui.Button(def.Label, new Vector2(格宽, 格高)))
            PromeSettings.Instance.SetQt(def.Id, !on);

        ImGui.PopStyleVar();
        ImGui.PopStyleColor(2);
        ImGui.PopID();

        // 起拖: 悬停格 + 右键拖动超过阈值 → 该格为源格（右键不触发按钮 Active, 悬停判定全程可用）
        if (!locked && 拖拽源 < 0
            && ImGui.IsItemHovered() && ImGui.IsMouseDragging(ImGuiMouseButton.Right))
        {
            拖拽源 = tileIndex;
            拖拽偏移 = ImGui.GetIO().MousePos - pos;
        }
    }

    // 面板底色与边框：画进窗口自身的绘制列表（先垫占位命令，见 ErosUILayer），
    // 保证两个窗口重叠时背景仍然盖住身后窗口的内容。
    private void DrawWindowChrome()
    {
        var pos = ImGui.GetWindowPos();
        var max = pos + ImGui.GetWindowSize();
        var drawList = ImGui.GetWindowDrawList();
        ErosUILayer.垫牺牲帧(drawList);
        // 覆盖 Begin 压入的内容区内层裁剪, 底色/边框画满全窗口（本窗无标题栏）
        drawList.PushClipRect(pos, max, false);

        var day = ErosUICommonSettings.Instance.UIMode == SettingsUIMode.Day;
        var tint = day ? new Vector4(0.96f, 0.94f, 0.89f, 0.85f)
                       : new Vector4(0.11f, 0.11f, 0.12f, 0.85f);
        var border = day ? new Vector4(0f, 0f, 0f, 0.10f)
                         : new Vector4(1f, 1f, 1f, 0.10f);

        drawList.AddRectFilled(pos + new Vector2(0.5f), max - new Vector2(0.5f),
            SimplePalette.ToU32(tint), 12f, ImDrawFlags.RoundCornersAll);
        drawList.AddRect(pos + new Vector2(1f), max - new Vector2(1f),
            SimplePalette.ToU32(border), 11f, ImDrawFlags.RoundCornersAll, 1f);
        drawList.PopClipRect();
    }

    // 空白处按住左键拖动面板；位置钳制在屏幕工作区内。
    private void HandleWindowDrag()
    {
        if (ImGui.IsWindowHovered() && !ImGui.IsAnyItemHovered()
            && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
        {
            ImGui.SetWindowPos(限制到屏幕内(ImGui.GetWindowPos() + ImGui.GetIO().MouseDelta));
        }
    }

    private static Vector2 限制到屏幕内(Vector2 pos)
    {
        var vp = ImGui.GetMainViewport();
        var size = ImGui.GetWindowSize();
        if (size.X <= 0f || size.Y <= 0f) size = new Vector2(360f, 70f);
        var min = vp.WorkPos + new Vector2(4f);
        var max = vp.WorkPos + vp.WorkSize - size - new Vector2(4f);
        if (max.X < min.X) max.X = min.X;
        if (max.Y < min.Y) max.Y = min.Y;
        return Vector2.Clamp(pos, min, max);
    }
}
