using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using System.Text;
using Dalamud.Bindings.ImGui;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ErosUI;
using PromeRotation;
using PromeRotation.Core;
using PromeRotation.Data;
using PromeRotation.Extensions;
using PromeRotation.Helpers;
using PromeRotation.LogSystem;
using PromeRotation.Managers;
using ViperACR.Core;
using PRCore = PromeRotation.Core.Core;
using StatusSheet = Lumina.Excel.Sheets.Status;

namespace ViperACR.Host;

internal sealed class ViperDiagnostics(ViperSettings settings) : IDisposable
{
    internal readonly DiagnosticJournal Journal = new();
    private readonly DiagnosticStallWatch _stall = new();
    private readonly ConcurrentQueue<LogSystemEvent> _pending = new();
    private readonly object _eventGate = new();
    private readonly Dictionary<string, bool> _qts = new();
    private readonly Dictionary<(bool status, uint id), string> _names = new();
    private IDisposable? _subscription;
    private int _pendingCount, _dropped;
    private uint _playerId;
    private uint? _targetId;
    private long _nextSample, _nextSubscribe;
    private bool _countdown, _entered;
    private bool _acceptEvents;
    private string _message = "", _lastDecision = "";
    private bool _showRecords;
    public static string ExportDirectory => Path.Combine(Path.GetDirectoryName(ViperSettings.FilePath)!, "Diagnostics");

    public void Enter()
    {
        if (_entered) return;
        _entered = true;
        Svc.PluginInterface.UiBuilder.Draw += DrawTargetLine;
    }

    public void Dispose()
    {
        _entered = false;
        Svc.PluginInterface.UiBuilder.Draw -= DrawTargetLine;
        Stop();
    }

    private void Stop()
    {
        lock (_eventGate)
        {
            _acceptEvents = false;
            while (_pending.TryDequeue(out _)) Interlocked.Decrement(ref _pendingCount);
        }
        try { Interlocked.Exchange(ref _subscription, null)?.Dispose(); }
        catch (Exception ex) { _message = "诊断订阅释放失败：" + ex.Message; }
        _stall.Reset(); _qts.Clear(); _targetId = null; _countdown = false;
        _lastDecision = ""; _nextSubscribe = 0;
    }

    // Network callbacks only queue immutable event data. Game objects, Excel sheets and UI stay on host callbacks.
    private void Receive(LogSystemEvent value)
    {
        lock (_eventGate)
        {
        if (!_acceptEvents) return;
        var player = Volatile.Read(ref _playerId);
        var keep = value switch {
            LogSystemActionEffectEvent e => player != 0 && e.SourceId == player,
            LogSystemCastStartEvent e => player != 0 && e.SourceId == player,
            LogSystemCancelCastEvent e => player != 0 && e.SourceId == player,
            LogSystemInterruptCastEvent e => player != 0 && e.SourceId == player,
            LogSystemStatusAddEvent e => player != 0 && e.TargetId == player,
            LogSystemStatusRemoveEvent e => player != 0 && e.TargetId == player,
            LogSystemBattleStartedEvent or LogSystemBattleEndedEvent or LogSystemTerritoryEvent or LogSystemChangeMapEvent => true,
            _ => false
        };
        if (!keep) return;
        if (Interlocked.Increment(ref _pendingCount) > 1024)
        { Interlocked.Decrement(ref _pendingCount); Interlocked.Increment(ref _dropped); return; }
        _pending.Enqueue(value);
        }
    }

    public void Update(Func<Snapshot> read, string gcdReason, string abilityReason, bool fixedOpener)
    {
        if (!_entered) return;
        if (!settings.CombatLogRecordEnabled) { if (_subscription != null) Stop(); return; }
        try
        {
            Volatile.Write(ref _playerId, PRCore.Me?.EntityId ?? 0);
            var now = Environment.TickCount64;
            if (_subscription == null && now >= _nextSubscribe)
            {
                _nextSubscribe = now + 5000;
                var source = Plugin.Instance?.LogSystem?.Events;
                if (source != null)
                {
                    lock (_eventGate) _acceptEvents = true;
                    _subscription = source.Subscribe(Receive);
                    Add("诊断", "记录器", "开始", $"Displaced VPR {ReleaseInfo.Version} | 区域 {Svc.ClientState.TerritoryType}");
                }
            }
            for (var i = 0; i < 256 && _pending.TryDequeue(out var value); i++)
            { Interlocked.Decrement(ref _pendingCount); Record(value); }
            var dropped = Interlocked.Exchange(ref _dropped, 0);
            if (dropped > 0) Add("诊断", "事件队列", "溢出", $"丢弃 {dropped} 条，避免事件积压");
            if (now < _nextSample) return;
            _nextSample = now + 200;
            var target = PRCore.Target;
            var id = target?.EntityId ?? 0;
            if (_targetId != id)
            {
                _targetId = id;
                Add("目标", TargetText(), id == 0 ? "取消目标" : "切换", target == null ? "" : $"HP {target.CurrentHp}/{target.MaxHp}");
            }
            var countdown = GameData.GetCountdown();
            if (_countdown != (countdown > 0))
            { _countdown = countdown > 0; Add("战斗", "倒计时", _countdown ? "开始" : "结束/取消", $"{countdown:F2}s"); }
            foreach (var key in ViperRotation.QtList.Keys)
            {
                var enabled = PromeSettings.Instance.GetQt(key);
                if (!_qts.TryGetValue(key, out var previous) || previous != enabled)
                    Add("QT", key, _qts.ContainsKey(key) ? "切换" : "初始", enabled ? "开启" : "关闭");
                _qts[key] = enabled;
            }
            var state = read();
            var decision = $"{(ErosUISettings.Instance.IsHighEnd ? "高难" : "日随")} | 固定起手 {fixedOpener} | GCD {gcdReason} | 能力技 {abilityReason}";
            if (decision != _lastDecision)
            { _lastDecision = decision; Add("决策", "循环", "变化", decision + " | " + ResourceText(state)); }
            var eligible = state.Active && state.HasTarget && PromeSettings.Instance.EnableAcr == PromeRotation.Data.AcrState.On &&
                !fixedOpener && PRCore.Me is { IsDead: false, IsCasting: false } &&
                state.GcdRemaining <= 0.05f && ActionHelper.GetAnimationLock() <= 0.05f;
            if (_stall.Observe(now, eligible))
                Add("停顿", "GCD", "就绪后持续4秒未推进", decision + " | " + ResourceText(state) + " | " + QueueText());
        }
        catch (Exception ex)
        {
            if (_subscription == null) { lock (_eventGate) _acceptEvents = false; }
            _message = "诊断采样失败：" + ex.Message;
        }
    }

    private void Record(LogSystemEvent value)
    {
        switch (value)
        {
            case LogSystemActionEffectEvent e:
                Add("技能", Name(e.ActionId), "命中", $"{e.TargetName} ({e.TargetId:X}) | 序列 {e.GlobalSequence} | 目标索引 {e.TargetIndex}", e.Timestamp); break;
            case LogSystemCastStartEvent e:
                Add("读条", Name(e.ActionId), "开始", $"{e.CastTime:F2}s | 可打断 {e.CanInterrupt != 0}", e.Timestamp); break;
            case LogSystemCancelCastEvent e: Add("读条", Name(e.ActionId), "取消", "", e.Timestamp); break;
            case LogSystemInterruptCastEvent e: Add("读条", Name(e.ActionId), "打断", "", e.Timestamp); break;
            case LogSystemStatusAddEvent e:
                Add("状态", Name(e.StatusId, true), e.IsRefresh ? "刷新" : "获得", $"层数 {e.StackCount} | {e.Duration:F2}s", e.Timestamp); break;
            case LogSystemStatusRemoveEvent e:
                Add("状态", Name(e.StatusId, true), "移除", $"层数 {e.StackCount}", e.Timestamp); break;
            case LogSystemBattleStartedEvent e: Add("战斗", "", "开始", "", e.Timestamp); break;
            case LogSystemBattleEndedEvent e: Add("战斗", "", "结束", "", e.Timestamp); break;
            case LogSystemTerritoryEvent e: Add("区域", e.TerritoryName, "切换", $"ID {e.TerritoryId}", e.Timestamp); break;
            case LogSystemChangeMapEvent e: Add("区域", e.PlaceName, "地图变化", $"{e.RegionName} / {e.SubName} | ID {e.MapId}", e.Timestamp); break;
        }
    }
    private void Add(string category, string subject, string kind, string detail, DateTime? time = null) =>
        Journal.Add(new(time ?? DateTime.Now, category, "蝰蛇", subject, kind, detail));
    private string Name(uint id, bool status = false)
    {
        if (_names.TryGetValue((status, id), out var cached)) return cached;
        string name;
        try { name = status ? Svc.Data.GetExcelSheet<StatusSheet>().GetRow(id).Name.ToString() : ExcelActionHelper.GetActionName(id, false); }
        catch { name = ""; }
        return _names[(status, id)] = $"{name} ({id})";
    }
    private static string TargetText() => PRCore.Target is { } t ? $"{t.Name} ({t.EntityId:X})" : "无目标";
    internal static string ResourceText(Snapshot s) =>
        $"灵力 {s.Gauge} | 蛇魂 {s.Coils} | 祖灵力 {s.Tribute} | 蛇剑 {s.DreadCombo} | 蛇尾 {s.TailCombo} | GCD {s.GcdElapsed:F2}/{s.GcdRemaining:F2}s | 距离 {s.Distance:F2}m" +
        $" | 队伍人数 {s.PartySize} | 蛇灵气CD {s.Cd(Skills.SerpentsIre):F2}s | 免费祖灵 {s.Buff(Buffs.ReadyToReawaken):F2}s | 祖灵状态 {s.Has(Buffs.Reawakened)} | 蛇灵气可用 {s.CanUse(Skills.SerpentsIre)}";
    private static string QueueText() => string.Join(" | ", ActionQueueManager.GetQueueStatus().Select(x =>
        $"{x.Key}: {x.Value.Count} [{string.Join("; ", x.Value.Take(2))}]"));

    public void Draw(Snapshot state, string gcdReason, string abilityReason)
    {
        SettingRow.SectionTitle("开发诊断");
        ImGui.TextWrapped("循环配置：" + ViperSettings.FilePath);
        ImGui.TextWrapped("界面配置：" + ErosUISettings.FilePath);
        var line = settings.ShowTargetLine;
        if (ImGui.Checkbox("显示目标连线", ref line)) { settings.ShowTargetLine = line; Save(); }
        var color = new Vector3(settings.TargetLineRed, settings.TargetLineGreen, settings.TargetLineBlue);
        if (ImGui.ColorEdit3("连线颜色", ref color))
        { settings.TargetLineRed = color.X; settings.TargetLineGreen = color.Y; settings.TargetLineBlue = color.Z; Save(); }
        var thickness = settings.TargetLineThickness;
        if (ImGui.SliderFloat("连线粗细", ref thickness, 1, 10, "%.1f")) { settings.TargetLineThickness = thickness; Save(); }
        var record = settings.CombatLogRecordEnabled;
        if (ImGui.Checkbox("记录战斗日志", ref record))
        { settings.CombatLogRecordEnabled = record; if (!record) Stop(); Save(); }
        ImGui.SameLine();
        if (ImGui.Button(_showRecords ? "收起战斗记录" : "查看战斗记录")) _showRecords = !_showRecords;
        ImGui.TextUnformatted($"记录状态：{(record ? _subscription != null ? "记录中" : "等待 PR 日志服务" : "已关闭")} | {Journal.Count}/{DiagnosticJournal.Capacity} 条");
        if (ImGui.Button("导出 CSV")) Export();
        ImGui.SameLine();
        if (ImGui.Button("打开日志目录"))
        {
            try { Directory.CreateDirectory(ExportDirectory); Process.Start(new ProcessStartInfo(ExportDirectory) { UseShellExecute = true }); }
            catch (Exception ex) { _message = "打开失败：" + ex.Message; }
        }
        ImGui.SameLine();
        if (ImGui.Button("清空记录")) { Journal.Clear(); _message = "记录已清空"; }
        if (_message.Length > 0) ImGui.TextWrapped(_message);
        ImGui.Separator();
        var me = PRCore.Me;
        ImGui.TextUnformatted(me == null ? "玩家未就绪" : $"等级 {me.Level} | 职业 {me.ClassJob.RowId} | 战斗 {GameData.IsInCombat()} | ACR {PromeSettings.Instance.EnableAcr}");
        ImGui.TextUnformatted($"模式：{(ErosUISettings.Instance.IsHighEnd ? "高难" : "日随")} | 动画锁 {ActionHelper.GetAnimationLock():F3}s | 倒计时 {GameData.GetCountdown():F2}s");
        ImGui.TextUnformatted($"目标：{TargetText()} | 有效快照 {state.Active} | 可攻击目标 {state.HasTarget}");
        ImGui.TextWrapped(ResourceText(state));
        ImGui.TextUnformatted($"目标血量 {state.TargetHpPercent:F1}% | 身位 {state.Facing} | 身位要求 {state.NeedsPositional} | 敌人数 {state.NearbyEnemies}");
        ImGui.TextUnformatted($"猛袭 {state.Buff(Buffs.HuntersInstinct):F1}s | 疾速 {state.Buff(Buffs.Swiftscaled):F1}s | 长臂猿 {ErosUISettings.Instance.LongArms} | 突进距离无限制 {ErosUISettings.Instance.UnlimitedDashDistance}");
        ImGui.TextWrapped("GCD：" + gcdReason); ImGui.TextWrapped("能力技：" + abilityReason);
        ImGui.TextWrapped("队列：" + QueueText());
        DrawSolvers("GCD 求解器", RotationManager.GcdSolverStatus);
        DrawSolvers("能力技求解器", RotationManager.OffGcdSolverStatus);
        DrawSolvers("Always 求解器", RotationManager.AlwaysSolverStatus);
        if (ImGui.CollapsingHeader("能力技可用状态"))
        {
            if (!state.Active) ImGui.TextUnformatted("当前无有效战斗快照；战斗开始后显示技能可用状态。");
            else
            foreach (var id in Skills.All.Where(Skills.IsOffGcd))
                ImGui.TextWrapped($"{Name(id)} | {(!state.Learned(id) ? "未解锁" : state.UnusableOffGcd.Contains(id) ? "游戏判定不可用" : "游戏判定可用")} | 状态码 {NativeStatus(id)} | 最近使用 {state.RecentlyUsed.Contains(id)} | 高亮 {state.Highlighted.Contains(id)} | CD {id.GetActionCooldown():F1}s");
        }
        if (_showRecords) DrawRecords();
    }
    private static void DrawSolvers(string label, IEnumerable<SolverStatus> statuses)
    {
        if (!ImGui.CollapsingHeader(label)) return;
        foreach (var status in statuses) ImGui.TextWrapped($"{status.Name} | {(status.Success ? "通过" : "等待")} | {status.Message}");
    }
    private static unsafe uint NativeStatus(uint id)
    {
        var manager = FFXIVClientStructs.FFXIV.Client.Game.ActionManager.Instance();
        return manager == null ? uint.MaxValue : manager->GetActionStatus(
            FFXIVClientStructs.FFXIV.Client.Game.ActionType.Action, id,
            (Skills.IsSelf(id) ? PRCore.Me : PRCore.Target)?.GameObjectId ?? 0xE0000000UL);
    }
    private void DrawRecords()
    {
        if (!ImGui.BeginTable("DisplacedDiagnosticRecords", 6, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg |
                ImGuiTableFlags.Resizable | ImGuiTableFlags.ScrollY, new Vector2(0, 320))) return;
        ImGui.TableSetupScrollFreeze(0, 1);
        foreach (var label in new[] { "时间", "分类", "职业", "对象", "事件", "详情" }) ImGui.TableSetupColumn(label);
        ImGui.TableHeadersRow();
        foreach (var entry in Journal.Snapshot().Reverse().Take(500))
        {
            ImGui.TableNextRow();
            foreach (var value in new[] { entry.Time.ToString("HH:mm:ss.fff"), entry.Category, entry.Job, entry.Subject, entry.Event, entry.Detail })
            { ImGui.TableNextColumn(); ImGui.TextWrapped(value); }
        }
        ImGui.EndTable();
    }
    private void Save()
    { try { settings.Save(); _message = ""; } catch (Exception ex) { _message = "保存失败：" + ex.Message; } }
    private void Export()
    {
        try
        {
            Directory.CreateDirectory(ExportDirectory);
            var path = Path.Combine(ExportDirectory, $"Displaced-Diagnostics-{DateTime.Now:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}.csv");
            File.WriteAllText(path, Journal.ToCsv(), new UTF8Encoding(true));
            _message = $"已导出 {Journal.Count} 条：{path}";
        }
        catch (Exception ex) { _message = "导出失败：" + ex.Message; }
    }
    private void DrawTargetLine()
    {
        if (!_entered || !settings.ShowTargetLine || PRCore.Me is not { } me || PRCore.Target is not { } target) return;
        if (!Svc.GameGui.WorldToScreen(me.Position, out var from) || !Svc.GameGui.WorldToScreen(target.Position, out var to)) return;
        ImGui.GetBackgroundDrawList().AddLine(from, to, ImGui.ColorConvertFloat4ToU32(new Vector4(
            settings.TargetLineRed, settings.TargetLineGreen, settings.TargetLineBlue, 1)), settings.TargetLineThickness);
    }
}
