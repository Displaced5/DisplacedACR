using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Windowing;
using ECommons.DalamudServices;
using PromeRotation.Data;
using PromeRotation.Extensions;
using PromeRotation.Helpers;
using PromeRotation.Managers;
using PromeRotation.UI.HotKey;

namespace ErosUI;

// ErosUI 风格热键面板：窗口外壳（NoBackground + 窗口绘制列表圆角底（垫牺牲帧防叠窗透出）+
// 外扩裁剪描边 + 每帧按网格精确尺寸），图标/冷却/点击用宿主公开件渲染
// （IHotkey/ActionHotkey/DelegateHotkey + IconHelper/ActionHelper/HotkeyQueueManager）。
public sealed class ErosUIHotkeyPanelWindow : Window
{
    // 格子圆角
    private const float 圆角 = 8f;

    private readonly IReadOnlyList<(string Name, IHotkey Hotkey, uint GameIcon, bool GameIconHQ)> entries;
    private readonly int columns;
    private readonly float tile;
    private readonly float spacing;

    // 右键拖拽换位状态（面板级, 一次只拖一格; 左键点击执行热键与拖拽完全解耦）
    private int 拖拽源 = -1;
    private int 拖拽目标;
    private Vector2 拖拽偏移;
    // 格子视觉位置（按热键名索引, 窗口相对坐标, 拖动面板时按钮随窗刚性平移）：
    // 拖拽让位/松手回落时指数阻尼收敛到格位
    private readonly Dictionary<string, Vector2> 动画格位 = new(StringComparer.Ordinal);
    private int[] 显示序暂存 = Array.Empty<int>();

    // 参数 tile: 按钮边长（已含缩放）
    // 参数 spacing: 按钮间距(px)
    public ErosUIHotkeyPanelWindow(IReadOnlyList<(string Name, IHotkey Hotkey, uint GameIcon, bool GameIconHQ)> entries,
        int columns, float tile, float spacing)
        : base($"{ErosUIJobEnv.作者}{ErosUIJobEnv.JobName}##hotkey",
            ImGuiWindowFlags.NoTitleBar
            | ImGuiWindowFlags.NoCollapse
            | ImGuiWindowFlags.NoScrollbar
            | ImGuiWindowFlags.NoResize
            | ImGuiWindowFlags.NoBackground   // ImGui 的 WindowBg/边框不画，背景全由 DrawWindowChrome 接管
            | ImGuiWindowFlags.NoNav)
    {
        this.entries = entries;
        this.columns = Math.Max(1, columns);
        this.tile = MathF.Max(24f, tile);
        this.spacing = Math.Clamp(spacing, 0f, 20f);
        RespectCloseHotkey = false;
        AllowBackgroundBlur = false;
        IsOpen = true;
    }

    public override void PreDraw()
    {
        var day = ErosUICommonSettings.Instance.UIMode == SettingsUIMode.Day;
        SimplePalette.ApplyScheme(day ? SimplePalette.UIColorScheme.Light
                                      : SimplePalette.UIColorScheme.Dark);

        ImGui.PushStyleColor(ImGuiCol.Text, SimplePalette.TextPrimary);
        ImGui.PushStyleColor(ImGuiCol.Border, day ? new Vector4(0f, 0f, 0f, 0.10f)
                                                  : new Vector4(1f, 1f, 1f, 0.10f));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 12f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(14f, 12f));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(10f, 10f));
        base.PreDraw();
    }

    public override void PostDraw()
    {
        ImGui.PopStyleVar(3);
        ImGui.PopStyleColor(2);
        base.PostDraw();
    }

    public override void Draw()
    {
        var pad = ImGui.GetStyle().WindowPadding;
        var cols = Math.Min(columns, entries.Count);
        var rows = (entries.Count + cols - 1) / cols;
        var size = new Vector2(
            cols * tile + (cols - 1) * spacing + pad.X * 2f,
            rows * tile + (rows - 1) * spacing + pad.Y * 2f);
        // 每帧显式尺寸精确贴合网格（勿改回 AlwaysAutoResize，实测会多出透明边）
        ImGui.SetWindowSize(size, ImGuiCond.Always);

        DrawWindowChrome();

        // 按用户自定义顺序排列（悬浮面板右键拖拽调整; 未调整过 = 注册原序）
        var ordered = 按自定义顺序排序(entries);
        var count = ordered.Count;
        if (count == 0)
        {
            HandleWindowDrag();
            return;
        }

        // 拖拽目标 = 鼠标所在网格坐标换算的格子（不依赖条目悬停判定, 上移/下移同权）;
        // 其它格子按「假设此刻松手」的显示序列实时让位, 指数阻尼滑向新格位
        if (拖拽源 >= 0)
            拖拽目标 = 计算拖拽目标(ImGui.GetIO().MousePos, count);
        var 显示序 = VisibleOrderHelper.BuildDisplayOrder(count, 拖拽源, 拖拽目标, 显示序暂存);
        显示序暂存 = 显示序;

        // 静止格子先画, 拖拽源最后画（跟随鼠标, 不被其它格子遮挡）;
        // 动画位置按窗口相对坐标存储, 绘制时加上窗口原点——拖动面板时按钮随窗刚性平移, 不产生追赶动画
        var drawList = ImGui.GetWindowDrawList();
        var winPos = ImGui.GetWindowPos();
        for (var slot = 0; slot < count; slot++)
        {
            var tileIndex = 显示序[slot];
            if (tileIndex == 拖拽源) continue;
            var rel = 取动画格位(ordered[tileIndex].Name, 格位偏移(slot));
            DrawTile(drawList, ordered[tileIndex], winPos + rel, winPos + rel + new Vector2(tile), tileIndex, floating: false);
        }

        if (拖拽源 >= 0 && 拖拽源 < count)
        {
            var rel = ImGui.GetIO().MousePos - 拖拽偏移 - winPos;
            动画格位[ordered[拖拽源].Name] = rel;   // 记录源格视觉位置（窗口相对）, 松手提交/取消后从该处平滑回落
            DrawTile(drawList, ordered[拖拽源], winPos + rel, winPos + rel + new Vector2(tile), 拖拽源, floating: true);
        }

        清理动画格位(ordered);

        if (拖拽源 >= 0)
        {
            if (ImGui.IsMouseReleased(ImGuiMouseButton.Right))
            {
                // 松手落位（插入语义, MoveHotkey 落盘, 让位动画按新格位收敛）; 拖到格外/原位 = 取消
                if (!ErosUISettings.Instance.HotkeyPanelOrderLocked && 拖拽目标 >= 0 && 拖拽目标 != 拖拽源)
                    ErosUISettings.Instance.MoveHotkey(ordered[拖拽源].Name, 拖拽目标);
                拖拽源 = -1;
                拖拽目标 = -1;
            }
            else if (!ImGui.IsMouseDown(ImGuiMouseButton.Right))
            {
                拖拽源 = -1;   // 松手帧面板未绘制（错过释放）, 兜底取消
            }
            // 落点标记: 高亮描边（让位动画已表达落点）
            else if (拖拽目标 >= 0 && 拖拽目标 != 拖拽源)
            {
                画目标格高亮(拖拽目标);
            }
        }

        HandleWindowDrag();
    }

    // ============================================================
    // === 单个图标按钮（渲染次序：底板 → 图标 → 点击判定 → 状态覆盖层 → 描边） ===
    // ============================================================
    // 参数 floating: 拖拽源格：只画视觉、不参与点击判定与悬停反馈（跟随鼠标画在最上层）。
    private void DrawTile(ImDrawListPtr drawList,
        (string Name, IHotkey Hotkey, uint GameIcon, bool GameIconHQ) entry,
        Vector2 min, Vector2 max, int index, bool floating)
    {
        var name = entry.Name;
        var hk = entry.Hotkey;
        var gameIcon = entry.GameIcon;
        var gameIconHQ = entry.GameIconHQ;

        drawList.AddRectFilled(min, max, SimplePalette.ToU32(SimplePalette.FrameBg), 圆角);

        // 图标来源优先级：游戏内原始图标 id（物品等无动作条目，gameIconHQ=true 取 hq/ 子目录的 HQ 品质）
        // → customIconPath → Action 表动作图标
        var tex = gameIcon != 0
            ? Svc.Texture.GetFromGameIcon(new GameIconLookup(gameIcon, itemHq: gameIconHQ)).GetWrapOrDefault(null)
            : hk.CustomIconPath != null ? IconHelper.GetIconFromPath(hk.CustomIconPath)
            : hk.ActionId.GetActionIcon();
        if (tex != null)
            drawList.AddImageRounded(tex.Handle, min + Vector2.One, max - Vector2.One,
                Vector2.Zero, Vector2.One, SimplePalette.ToU32(Vector4.One), MathF.Max(0f, 圆角 - 1f));

        var hovered = false;
        if (!floating)
        {
            ImGui.SetCursorScreenPos(min);
            ImGui.PushID(name);
            var clicked = ImGui.InvisibleButton("##hk", new Vector2(tile));
            ImGui.PopID();
            if (clicked)
            {
                try { hk.OnClick(); }
                catch (Exception e) { Svc.Log.Error($"[{ErosUIJobEnv.作者}] 热键 {name} 点击失败: {e.Message}"); }
            }
            hovered = ImGui.IsItemHovered();

            // 起拖: 悬停格 + 右键拖动超过阈值 → 该格为源格（右键不触发按钮, 悬停判定全程可用;
            // 锁定排序时不起拖, 点击执行不受影响）; 记录抓取偏移供源格跟随鼠标
            if (!ErosUISettings.Instance.HotkeyPanelOrderLocked && 拖拽源 < 0
                && hovered && ImGui.IsMouseDragging(ImGuiMouseButton.Right))
            {
                拖拽源 = index;
                拖拽偏移 = ImGui.GetIO().MousePos - min;
            }

            // 拖拽进行中抑制其它格子的悬停反馈, 避免让位滑动时高亮/提示跳动
            if (拖拽源 >= 0)
                hovered = false;
        }

        // 切换型逻辑按钮（同步镜头/校准正方向）：激活时底部画强调色横条
        var accent = SimplePalette.Accent;
        if (hk is DelegateHotkey dk && dk.IsActive())
            drawList.AddRectFilled(new Vector2(min.X + 7f, max.Y - 3f), new Vector2(max.X - 7f, max.Y - 1f),
                SimplePalette.ToU32(accent), 99f);

        if (hk.ActionId != 0 && HotkeyQueueManager.IsPending(hk.ActionId))
        {
            // 队列待发：强调色呼吸罩 + 加粗描边
            var pulse = 0.72f + 0.18f * MathF.Sin((float)ImGui.GetTime() * 5.5f);
            drawList.AddRectFilled(min, max,
                SimplePalette.ToU32(SimplePalette.WithAlpha(accent, 0.10f * pulse)), 圆角);
            drawList.AddRect(min, max,
                SimplePalette.ToU32(SimplePalette.WithAlpha(accent, 0.9f)), 圆角, ImDrawFlags.RoundCornersAll, 2.2f);
        }
        else if (hk.ActionId != 0)
        {
            DrawCooldown(drawList, hk, min, max);
        }

        // 描边：平时 BorderStrong（20%），悬停提亮到主文字色 40%
        drawList.AddRect(min, max,
            SimplePalette.ToU32(hovered ? SimplePalette.WithAlpha(SimplePalette.TextPrimary, 0.40f)
                                        : SimplePalette.BorderStrong),
            圆角, ImDrawFlags.RoundCornersAll, 1.2f);

        if (hovered) ImGui.SetTooltip(name);
    }

    // 冷却：顶部暗纱按剩余比例下压 + 居中秒数 + 多充能未满时右下角充能数。
    private static void DrawCooldown(ImDrawListPtr drawList, IHotkey hk, Vector2 min, Vector2 max)
    {
        var cd = ActionHelper.GetActionCooldown(hk.ActionId);
        if (cd <= 0f) return;
        var charges = ActionHelper.GetActionCharges(hk.ActionId);
        var maxCharges = Math.Max(1, ActionHelper.GetMaxCharges(hk.ActionId));
        if (!ActionHelper.IsActionRecharging(cd, charges, maxCharges)) return;

        var recast = ActionHelper.GetActionRecastTime(hk.ActionId);
        if (recast <= 0.001f) return;
        var progress = Math.Clamp(cd / recast, 0f, 1f);

        var veilMax = new Vector2(max.X, min.Y + (max.Y - min.Y) * progress);
        drawList.AddRectFilled(min, veilMax,
            SimplePalette.ToU32(new Vector4(0.1f, 0.1f, 0.1f, 0.6f)), 圆角 - 1f, ImDrawFlags.RoundCornersTop);

        if (cd > 0.05f)
        {
            var text = ((int)MathF.Ceiling(cd)).ToString();
            const float fontScale = 1.1f;
            var fontSize = ImGui.GetFontSize() * fontScale;
            var ts = ImGui.CalcTextSize(text) * fontScale;
            var tp = (min + max) * 0.5f - ts * 0.5f;
            drawList.AddText(ImGui.GetFont(), fontSize, tp + new Vector2(1f, 1f), 4278190080u, text);
            drawList.AddText(ImGui.GetFont(), fontSize, tp, 4294967295u, text);
        }

        if (maxCharges > 1)
        {
            var n = Math.Clamp((int)MathF.Floor(charges + 0.001f), 0, maxCharges);
            var nText = n.ToString();
            var ts = ImGui.CalcTextSize(nText);
            var tp = max - ts - new Vector2(5f, 3f);
            drawList.AddRectFilled(tp - new Vector2(4f, 1f), max - new Vector2(1f),
                SimplePalette.ToU32(new Vector4(0.1f, 0.1f, 0.1f, 0.85f)), 5f);
            drawList.AddText(ImGui.GetFont(), ImGui.GetFontSize(), tp,
                SimplePalette.ToU32(SimplePalette.TextPrimary), nText);
        }
    }

    // ============================================================
    // === 排列顺序与拖拽换位 ===
    // ============================================================
    // 把构建器产出的热键条目按 ErosUISettings.HotkeyOrder 的自定义顺序重排:
    // 顺序表里没有的名字按原相对顺序置尾（LINQ OrderBy 稳定排序）。
    private static List<(string Name, IHotkey Hotkey, uint GameIcon, bool GameIconHQ)> 按自定义顺序排序(
        IReadOnlyList<(string Name, IHotkey Hotkey, uint GameIcon, bool GameIconHQ)> entries)
    {
        if (entries.Count < 2) return entries.ToList();
        var order = ErosUISettings.Instance.GetOrderedHotkeyNames();
        var rank = new Dictionary<string, int>(order.Count);
        for (var i = 0; i < order.Count; i++) rank[order[i]] = i;
        return entries.OrderBy(e => rank.TryGetValue(e.Name, out var r) ? r : int.MaxValue).ToList();
    }

    // 拖拽目标格 = 鼠标位置按网格节距（按钮边长 + 间距）换算行列并钳制在格子范围内;
    // 鼠标在窗口外返回 -1（拖出面板 = 取消）。
    private int 计算拖拽目标(Vector2 鼠标, int 总数)
    {
        var pos = ImGui.GetWindowPos();
        var size = ImGui.GetWindowSize();
        if (鼠标.X < pos.X || 鼠标.Y < pos.Y || 鼠标.X >= pos.X + size.X || 鼠标.Y >= pos.Y + size.Y)
            return -1;
        var 原点 = pos + ImGui.GetStyle().WindowPadding;
        var 列 = Math.Clamp((int)((鼠标.X - 原点.X) / (tile + spacing)), 0, columns - 1);
        var 行 = Math.Clamp((int)((鼠标.Y - 原点.Y) / (tile + spacing)), 0, (总数 + columns - 1) / columns - 1);
        return Math.Clamp(行 * columns + 列, 0, 总数 - 1);
    }

    // 拖拽中的目标格高亮描边（Accent 色, 格子矩形外扩 2px; 按网格坐标定位, 不依赖条目矩形）。
    private void 画目标格高亮(int index)
    {
        var 原点 = ImGui.GetWindowPos() + ImGui.GetStyle().WindowPadding;
        var min = 原点 + new Vector2(index % columns, index / columns) * new Vector2(tile + spacing);
        ImGui.GetWindowDrawList().AddRect(min - new Vector2(2f), min + new Vector2(tile) + new Vector2(2f),
            SimplePalette.ToU32(SimplePalette.Accent), 圆角, ImDrawFlags.RoundCornersAll, 2f);
    }

    // 显示槽位 slot 相对窗口原点的网格偏移（含窗口内边距, 与 画目标格高亮 同一网格算法）。
    private Vector2 格位偏移(int slot)
        => ImGui.GetStyle().WindowPadding
           + new Vector2(slot % columns, slot / columns) * new Vector2(tile + spacing);

    // 格子视觉位置指数阻尼收敛到目标格位（窗口相对坐标）; 新出现的格子直接落位, 不做飞入动画。
    private Vector2 取动画格位(string name, Vector2 目标)
    {
        if (!动画格位.TryGetValue(name, out var 当前))
        {
            动画格位[name] = 目标;
            return 目标;
        }

        var next = VisibleOrderHelper.AnimatePosition(当前, 目标, ImGui.GetIO().DeltaTime);
        动画格位[name] = next;
        return next;
    }

    // 热键清单变化后移除失效条目的动画位置记录。
    private void 清理动画格位(IReadOnlyList<(string Name, IHotkey Hotkey, uint GameIcon, bool GameIconHQ)> entries)
    {
        if (动画格位.Count <= entries.Count) return;
        var live = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries) live.Add(entry.Name);
        foreach (var name in 动画格位.Keys.Where(name => !live.Contains(name)).ToArray())
            动画格位.Remove(name);
    }

    // ============================================================
    // === 外壳与拖动 ===
    // ============================================================
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
