using Dalamud.Bindings.ImGui;
using ErosUI;
using System.Numerics;
using PromeRotation.Core;
using PromeRotation.Data;
using PromeRotation.Extensions;
using PromeRotation.Helpers;
using PromeRotation.Managers;
using PromeRotation.Rotation;
using PromeRotation.Updaters;
using PromeRotation.UI.HotKey;
using ViperACR.Core;
using PRCore = PromeRotation.Core.Core;

namespace ViperACR.Host;

[RotationMetadata(41u, "Displaced VPR", ReleaseInfo.Author, ReleaseInfo.Version, ContentScope = AcrContentScope.All)]
public sealed class ViperRotation : IRotation, IRotationLifecycle
{
    public string RotationName => "Displaced VPR";
    public uint JobId => 41;
    public static IReadOnlyDictionary<string, bool> QtList { get; } = new Dictionary<string, bool> {
        ["爆发"] = true, ["强碎灵蛇"] = true, ["祖灵降临"] = true, ["蛇灵气"] = true, ["AOE"] = true,
        ["远离止损"] = true, ["自动真北"] = true,
        ["自动治疗"] = false, ["爆发药"] = false
    };
    public static IReadOnlyDictionary<string, Type> Openers { get; } = new Dictionary<string, Type> {
        [StandardOpener.Name] = typeof(ViperStandardOpener),
        [FruOpener.Name] = typeof(ViperFruOpener),
        [DsrOpener.Name] = typeof(ViperDsrOpener),
        [TeaOpener.Name] = typeof(ViperTeaOpener)
    };

    private readonly ViperSettings _settings;
    private readonly ViperEventHandler _events;
    private readonly ViperDiagnostics _diagnostics;
    private readonly CountdownPull _countdownPull = new();
    private readonly CountdownPull _teaTrueNorth = new();
    private readonly PrepullAttack _prepullAttack = new();
    private readonly AbilityWindow _abilityWindow = new();
    private readonly DailyEngagement _dailyEngagement = new();
    private readonly BasicAutomation _basicAutomation = new();
    private ViperStandardOpener? _activeOpener;
    private ViperTeaOpener? _activeTeaOpener;
    private ViperDsrOpener? _activeDsrOpener;
    private ViperFruOpener? _activeFruOpener;
    private long _openerPhaseTime;
    private uint _openerTarget;
    private string _error;
    private string _gcdReason = "等待战斗", _ogcdReason = "等待插入窗口";
    private Snapshot? _cached;
    private long _cacheTime;

    public ViperRotation()
    {
        LegacyConfiguration.Migrate();
        ErosUIFramework.Configure("VPR", "蝰蛇剑士", QtList, _ => false, (_, _) => true,
            key => QtList.GetValueOrDefault(key), new Dictionary<string, (string key, bool invert)[]>(),
            ["蛇行", "真北", "内丹", "浴血", "牵制", "冲刺", "极限技"],
            b => {
                b.Fixed("蛇行", Skills.Slither, ActionType.OffGcd, ActionTargetType.Target,
                    () => IsTargetWithinAttackRange(actionId: Skills.Slither));
                b.Fixed("真北", Skills.TrueNorth, ActionType.OffGcd, ActionTargetType.Self);
                b.Fixed("内丹", Skills.SecondWind, ActionType.OffGcd, ActionTargetType.Self);
                b.Fixed("浴血", Skills.Bloodbath, ActionType.OffGcd, ActionTargetType.Self);
                b.Fixed("牵制", 7549, ActionType.OffGcd, ActionTargetType.Target);
                b.Fixed("冲刺", 3, ActionType.OffGcd, ActionTargetType.Self);
                b.Execute("极限技", new ExecuteLogic(ViperLimitBreak.Queue), ViperLimitBreak.IconActionId);
            }, author: ReleaseInfo.Author, drawRotationSettings: DrawRotationSettings, drawOpenerSettings: DrawOpenerSettings);
        _settings = ViperSettings.Load(out _error);
        _diagnostics = new ViperDiagnostics(_settings);
        ErosUIJobEnv.DrawBasicSettings = DrawBasicSettings;
        ErosUIJobEnv.DrawDiagnostics = DrawDiagnostics;
        _events = new ViperEventHandler(Reset, UpdateOutOfCombat,
            () => { _prepullAttack.Reset(); _dailyEngagement.Reset(); PromeSettings.Instance.OpenerHasBeenExecuted = false; });
        _events.BattleUpdate = UpdateStandardOpener;
        _events.Update = Update;
        foreach (var (name, value) in QtList) PromeSettings.Instance.AddQt(name, value);
    }
    public void OnEnterAcr()
    {
        _basicAutomation.Reset();
        try { ErosUIFramework.Install(); _diagnostics.Enter(); }
        catch (Exception ex)
        {
            _error = "ErosUI 初始化失败：" + ex.Message;
            ErosUIFramework.Uninstall();
        }
    }
    public void OnExitAcr()
    {
        _basicAutomation.Reset();
        _diagnostics.Dispose();
        _prepullAttack.Reset(); _countdownPull.Reset();
        _abilityWindow.Reset();
        _dailyEngagement.Reset();
        _activeOpener = null;
        _activeTeaOpener = null; _teaTrueNorth.Reset();
        _activeDsrOpener = null;
        _activeFruOpener = null;
        ErosUIFramework.Uninstall();
        ErosUIJobEnv.DrawRotationSettings = null;
        ErosUIJobEnv.DrawOpenerSettings = null;
        ErosUIJobEnv.DrawBasicSettings = null;
        ErosUIJobEnv.DrawDiagnostics = null;
    }
    private RotationOptions Options() => new() {
        Mode = CurrentMode,
        Burst = Qt("爆发"), Cooldowns = Qt("强碎灵蛇"),
        Reawaken = Qt("祖灵降临"), SerpentsIre = Qt("蛇灵气"), Aoe = Qt("AOE"),
        Ranged = Qt("远离止损"), AutoTrueNorth = Qt("自动真北"),
        Healing = Qt("自动治疗"), Potion = Qt("爆发药"),
        AoeThreshold = _settings.AoeThreshold, ReserveCoils = _settings.ReserveCoils,
        BurstReserveSeconds = _settings.BurstReserveSeconds, WeaveSafetySeconds = _settings.WeaveSafetySeconds,
        TrueNorthGcdPercent = _settings.TrueNorthGcdPercent
    };
    private static CombatMode CurrentMode => ErosUISettings.Instance.IsHighEnd ? CombatMode.HighEnd : CombatMode.Daily;
    private bool UsesStandardOpener => ModePolicy.UsesStandardOpener(CurrentMode, _settings.Opener == OpenerMode.Standard);
    private bool UsesTeaOpener => CurrentMode == CombatMode.HighEnd && _settings.Opener == OpenerMode.Tea80;
    private bool UsesDsrOpener => CurrentMode == CombatMode.HighEnd && _settings.Opener == OpenerMode.Dsr90;
    private bool UsesFruOpener => CurrentMode == CombatMode.HighEnd && _settings.Opener == OpenerMode.Fru100;
    private static bool Qt(string key) => PromeSettings.Instance.GetQt(key);
    internal static bool IsTargetWithinAttackRange(uint expectedTarget = 0, uint actionId = Skills.SteelFangs)
    {
        var me = PRCore.Me;
        var target = PRCore.Target;
        return me is { IsDead: false } && target is { IsDead: false, IsTargetable: true } &&
            (expectedTarget == 0 || target.EntityId == expectedTarget) &&
            (actionId == Skills.Slither ? CombatRange.CanDash(Math.Max(0,
                Vector2.Distance(new(me.Position.X, me.Position.Z), new(target.Position.X, target.Position.Z)) - target.HitboxRadius),
                ErosUISettings.Instance.LongArms, ErosUISettings.Instance.UnlimitedDashDistance) :
            Skills.Range(actionId) == 0 ||
                CombatRange.EffectiveDistance(Math.Max(0, Vector2.Distance(new(me.Position.X, me.Position.Z),
                    new(target.Position.X, target.Position.Z)) - target.HitboxRadius), ErosUISettings.Instance.LongArms)
                    <= Skills.Range(actionId));
    }
    private Snapshot Read()
    {
        var now = Environment.TickCount64;
        if (_cached != null && now - _cacheTime < 50 && _cached.LongArms == ErosUISettings.Instance.LongArms) return _cached;
        _cached = HostSnapshot.Capture(); _cacheTime = now;
        return _cached;
    }
    private void Reset() { _cached = null; _countdownPull.Reset(); _teaTrueNorth.Reset(); _abilityWindow.Reset(); _activeOpener = null; _activeTeaOpener = null; _activeDsrOpener = null; _activeFruOpener = null; _gcdReason = "等待战斗"; _ogcdReason = "等待插入窗口"; }

    private void Update()
    {
        UpdateBasicAutomation();
        _diagnostics.Update(Read, _gcdReason, _ogcdReason, _activeOpener is { Started: true } || _activeTeaOpener is { Started: true } || _activeDsrOpener is { Started: true } || _activeFruOpener is { Started: true });
    }

    private void UpdateBasicAutomation()
    {
        if (!ReferenceEquals(RotationManager.GetCurrentRotation(), this) || PRCore.Me is not { } me || me.ClassJob.RowId != 41)
        { _basicAutomation.Reset(); return; }
        var ui = ErosUISettings.Instance;
        if (_basicAutomation.ObserveDeath(me.IsDead, ui.DeathAutoResetQt))
            ui.RestoreQtSnapshot(ui.IsHighEnd);
        var eligible = !me.IsDead && !GameData.IsInCombat() && !GameData.IsInPvPArea() &&
            !GameData.IsPlayerOccupied() && PRCore.Target is { IsDead: false, IsTargetable: true } target &&
            target.CanUseAttackActionOn() && !TargetHelper.TargetHasInvincibilityStatus(target);
        if (_basicAutomation.ObserveCountdown(GameData.GetCountdown(), ui.OpenerAutoReleaseHold,
                PromeSettings.Instance.EnableAcr == AcrState.Hold, eligible))
            PromeSettings.Instance.EnableAcr = AcrState.On;
    }

    private static bool AllowsCountdownTarget() => BasicAutomation.AllowsCountdownTarget(
        PRCore.Target is Dalamud.Game.ClientState.Objects.Types.IBattleNpc npc && BasicAutomation.IsTrainingDummy(npc.NameId),
        ErosUISettings.Instance.OpenerAutoPull);

    private void UpdateStandardOpener()
    {
        if (_activeOpener is not { Started: true, Complete: false }) return;
        if (!GameData.IsInCombat() || PRCore.Me is not { IsDead: false } ||
            PRCore.Target is not { IsDead: false, IsTargetable: true } target || target.EntityId != _openerTarget)
        { _activeOpener = null; return; }
        if (PromeSettings.Instance.EnableAcr != AcrState.On || GameData.IsPlayerOccupied())
        { _openerPhaseTime = Environment.TickCount64; return; }
        if (ActionQueueManager.HasActionsInQueue() || ActionUpdaterRouter.HasActiveCommand())
        { _openerPhaseTime = Environment.TickCount64; return; }
        if (!IsTargetWithinAttackRange(_openerTarget, Skills.UncoiledFury))
        { _openerPhaseTime = Environment.TickCount64; return; }
        // Refresh after the preceding verified hit; don't select using an earlier cached gauge.
        _cached = null;
        if (_activeOpener.NextPhase(Read()) is { } phase)
        {
            ActionQueueManager.Enqueue(phase);
            _openerPhaseTime = Environment.TickCount64;
        }
        else if (Environment.TickCount64 - _openerPhaseTime > 5000)
        {
            _activeOpener = null;
            _gcdReason = "起手量谱不符，已回到动态循环";
        }
    }

    private bool StandardOpenerPending()
    {
        if (_activeFruOpener is { Started: true })
        {
            if (ActionQueueManager.HasActionsInQueue() || ActionUpdaterRouter.HasActiveCommand()) return true;
            _activeFruOpener = null;
        }
        if (_activeDsrOpener is { Started: true })
        {
            if (ActionQueueManager.HasActionsInQueue() || ActionUpdaterRouter.HasActiveCommand()) return true;
            _activeDsrOpener = null;
        }
        if (_activeTeaOpener is { Started: true })
        {
            if (ActionQueueManager.HasActionsInQueue() || ActionUpdaterRouter.HasActiveCommand()) return true;
            _activeTeaOpener = null;
        }
        if (_activeOpener is { Complete: true } && !ActionQueueManager.HasActionsInQueue() &&
            !ActionUpdaterRouter.HasActiveCommand()) _activeOpener = null;
        UpdateStandardOpener();
        return _activeOpener is { Started: true, Complete: false } ||
            _activeOpener is { Started: true } &&
            (ActionQueueManager.HasActionsInQueue() || ActionUpdaterRouter.HasActiveCommand());
    }

    private void UpdateOutOfCombat()
    {
        UpdateCountdownPull();
        UpdateDailyEngagement();
    }

    private void UpdateDailyEngagement()
    {
        if (CurrentMode != CombatMode.Daily || GameData.IsInCombat() ||
            PromeSettings.Instance.EnableAcr != AcrState.On ||
            !ReferenceEquals(RotationManager.GetCurrentRotation(), this) ||
            GameData.GetCountdown() > 0 || _prepullAttack.HasPending ||
            ActionQueueManager.HasActionsInQueue() || ActionUpdaterRouter.HasActiveCommand()) return;
        var state = HostSnapshot.Capture(allowEngagedTarget: true);
        var decision = ViperEngine.Gcd(state, Options());
        if (decision == null || PRCore.Target is not { } target || target.EntityId != state.TargetId) return;
        var action = decision.ActionId;
        var eligible = state.Active && state.HasTarget && IsTargetWithinAttackRange(state.TargetId, action) &&
            ActionHelper.GetAnimationLock() <= 0.05f &&
            ActionHelper.GetGcdRemain() <= 0.05f && action.IsActionAvailableByLevelAndQuest() &&
            ActionHelper.IsReady(action) && ActionHelper.CanCast(action, Skills.IsSelf(action) ? PRCore.Me : target);
        if (!_dailyEngagement.TrySubmit(Environment.TickCount64, CurrentMode, GameData.IsInCombat(),
            HostSnapshot.IsTargetInCombat(), GameData.GetCountdown(), eligible, action)) return;
        // PR's normal decision loop may stop while the player is out of combat.
        // Submit one ready GCD here after checking the selected enemy's real combat flag.
        ActionUpdater.UseAction(new PAction(action, ActionType.Gcd,
            Skills.IsSelf(action) ? ActionTargetType.Self : ActionTargetType.Target) {
            NetworkTid = Skills.IsSelf(action) ? 0u : state.TargetId
        });
        _gcdReason = "日随：目标已进入战斗，自动接战；" + decision.Reason;
        _cached = null;
    }

    private void UpdateCountdownPull()
    {
        var remaining = GameData.GetCountdown();
        var me = PRCore.Me;
        var target = PRCore.Target;
        var trueNorthEligible = UsesTeaOpener && TeaOpener.CanStart(me?.Level ?? 0, Options()) &&
            remaining > CountdownPull.TriggerSeconds && remaining <= TeaOpener.TrueNorthSeconds &&
            AllowsCountdownTarget() && PromeSettings.Instance.EnableAcr == AcrState.On &&
            ReferenceEquals(RotationManager.GetCurrentRotation(), this) &&
            me is { IsDead: false } && me.ClassJob.RowId == 41 &&
            !GameData.IsInCombat() && !GameData.IsInPvPArea() && !GameData.IsPlayerOccupied() &&
            target is { IsDead: false, IsTargetable: true } && target.CanUseAttackActionOn() &&
            !TargetHelper.TargetHasInvincibilityStatus(target) && Qt("自动真北") &&
            me.GetStatusLeftTime(Buffs.TrueNorth) <= 0 && Skills.TrueNorth.GetActionCharges() > 0 &&
            ActionHelper.CanCast(Skills.TrueNorth, me) && ActionHelper.GetAnimationLock() <= 0.05f;
        if (_teaTrueNorth.TrySubmit(remaining, trueNorthEligible, TeaOpener.TrueNorthSeconds))
        {
            ActionUpdater.UseAction(new PAction(Skills.TrueNorth, ActionType.OffGcd, ActionTargetType.Self));
            _ogcdReason = "绝亚起手倒数4秒真北";
        }
        var eligible = remaining > 0 && remaining <= CountdownPull.TriggerSeconds &&
            AllowsCountdownTarget() &&
            PromeSettings.Instance.EnableAcr == AcrState.On &&
            ReferenceEquals(RotationManager.GetCurrentRotation(), this) &&
            me is { IsDead: false, Level: >= 40 } && me.ClassJob.RowId == 41 &&
            !GameData.IsInCombat() && !GameData.IsInPvPArea() && !GameData.IsPlayerOccupied() &&
            target is { IsDead: false, IsTargetable: true } && target.CanUseAttackActionOn() &&
            !TargetHelper.TargetHasInvincibilityStatus(target) &&
            IsTargetWithinAttackRange(target.EntityId, Skills.Slither) &&
            Skills.Slither.IsActionAvailableByLevelAndQuest() && ActionHelper.IsReady(Skills.Slither) &&
            Skills.Slither.GetActionCharges() > 0 && ActionHelper.CanCast(Skills.Slither, target) &&
            ActionHelper.GetAnimationLock() <= 0.05f;
        if (_countdownPull.TrySubmit(remaining, eligible))
        {
            // PR countdown actions ignore ExecutionGuard, so submit in this guarded callback.
            ActionUpdater.UseAction(new PAction(Skills.Slither, ActionType.OffGcd, ActionTargetType.Target) {
                NetworkTid = target!.EntityId
            });
            _prepullAttack.Arm(target.EntityId, Environment.TickCount64, remaining);
            _ogcdReason = "倒计时蛇行（剩余 500ms 内，每次倒计时一次）";
        }
        if (!_prepullAttack.HasPending) return;
        // This callback continues before combat: wait only for the game's lock/range, never for combat.
        var allowed = PromeSettings.Instance.EnableAcr == AcrState.On &&
            AllowsCountdownTarget() &&
            ReferenceEquals(RotationManager.GetCurrentRotation(), this) &&
            me is { IsDead: false } && me.ClassJob.RowId == 41 && !GameData.IsInPvPArea() &&
            target is { IsDead: false, IsTargetable: true } && target.CanUseAttackActionOn() &&
            !TargetHelper.TargetHasInvincibilityStatus(target);
        var first = GetOpener() is ViperStandardOpener ? Skills.Vicewinder : me is { Level: >= 10 } ? Skills.ReavingFangs : Skills.SteelFangs;
        var ready = allowed && !GameData.IsPlayerOccupied() &&
            ActionHelper.GetAnimationLock() <= 0.05f && ActionHelper.GetGcdRemain() <= 0.05f &&
            IsTargetWithinAttackRange(target!.EntityId, first) &&
            first.IsActionAvailableByLevelAndQuest() && ActionHelper.IsReady(first) && ActionHelper.CanCast(first, target);
        if (_prepullAttack.TrySubmit(Environment.TickCount64, remaining, target?.EntityId ?? 0, allowed, ready, first))
        {
            ActionUpdater.UseAction(new PAction(first, ActionType.Gcd, ActionTargetType.Target) { NetworkTid = target!.EntityId });
            _gcdReason = $"蛇行后自动首击 {first}（不等待战斗状态）";
        }
    }

    public PAction? NextGcd()
    {
        if (StandardOpenerPending()) return null;
        var state = Read();
        var result = ViperEngine.Gcd(state, Options());
        _gcdReason = result?.Reason ?? "无可用 GCD／目标不在范围";
        return MakeAction(state, result, false);
    }
    public PAction? NextOffGcd()
    {
        if (StandardOpenerPending()) return null;
        var state = Read(); var options = Options();
        var result = ViperEngine.OffGcd(state, options);
        _ogcdReason = result?.Reason ?? "无需插入技能";
        // Abilities are submitted only by NextAlways; PR 1.5.10.4 can retain its oGCD
        // count across queued GCDs and stop dequeuing the regular oGCD queue.
        if (result != null) return null;
        // User opts in explicitly. Complete damage continuations before spending a weave on an item.
        if (options.Potion && options.Burst && state.Active && state.HasTarget && state.SkillDistance <= 3 &&
            state.GcdRemaining >= 1.2f && state.Has(Buffs.HuntersInstinct) && state.Has(Buffs.Swiftscaled) &&
            (state.Has(Buffs.ReadyToReawaken) || state.RaidBuff) && state.Tribute == 0)
        {
            var potion = GameData.GetBestPotionId();
            if (potion != 0 && !ActionHelper.IsItemOnCooldown(potion))
            {
                _ogcdReason = "爆发药（QT 已开启）";
                return new PAction(potion, ActionType.Item, ActionTargetType.Self) {
                    ExecutionGuard = () => GameData.IsInCombat() && Qt("爆发药") && IsTargetWithinAttackRange(state.TargetId)
                };
            }
        }
        return null;
    }
    public PAction? NextAlways()
    {
        if (StandardOpenerPending()) return null;
        var state = Read();
        if (!state.Active || !state.HasTarget || ActionHelper.GetAnimationLock() > 0) return null;
        _abilityWindow.Observe(ActionHelper.GetGcdElapsed(), state.GcdRemaining);
        var options = Options();
        var result = ViperEngine.OffGcd(state, options) ?? ViperEngine.Continuation(state);
        if (result == null || !_abilityWindow.CanSubmit(result.ActionId, Environment.TickCount64,
                AbilityWindow.Maximum(ErosUISettings.Instance.TripleWeave, PromeSettings.Instance.Hacks.MaxOgcdsPerGcd))) return null;
        // Always is an actual queue type checked by this host. BypassOffGcdWindow is not read.
        var action = MakeAction(state, result, true, true);
        _abilityWindow.Record(result.ActionId, Environment.TickCount64);
        _ogcdReason = result.Reason;
        return action;
    }
    private static PAction? MakeAction(Snapshot state, Decision? decision, bool offGcd, bool rescue = false)
    {
        if (decision == null) return null;
        var id = decision.ActionId;
        return new PAction(id, rescue ? ActionType.Always : offGcd ? ActionType.OffGcd : ActionType.Gcd,
            Skills.IsSelf(id) ? ActionTargetType.Self : ActionTargetType.Target) {
            NetworkTid = Skills.IsSelf(id) ? 0u : state.TargetId,
            BypassOffGcdWindow = rescue,
            ExecutionGuard = () => PRCore.Me is { IsDead: false } me && me.ClassJob.RowId == 41 &&
                GameData.IsInCombat() && !GameData.IsInPvPArea() && PRCore.Target is { } target &&
                target.EntityId == state.TargetId && !target.IsDead && target.IsTargetable && target.CanUseAttackActionOn() &&
                IsTargetWithinAttackRange(state.TargetId, id) &&
                (id is not (Skills.SerpentsIre or Skills.Reawaken) ||
                    ModePolicy.AllowsTargetBurst(CurrentMode, target.MaxHp > 0 ? 100f * target.CurrentHp / target.MaxHp : 100,
                        HostSnapshot.PartySize()))
        };
    }
    public IOpener? GetOpener()
    {
        if (UsesFruOpener) return GetFruOpener();
        if (UsesDsrOpener) return GetDsrOpener();
        if (UsesTeaOpener) return GetTeaOpener();
        var me = PRCore.Me;
        var target = PRCore.Target;
        if (PromeSettings.Instance.EnableAcr != AcrState.On || !UsesStandardOpener || !IsTargetWithinAttackRange() ||
            !StandardOpener.CanStart(Options()) ||
            me is not { IsDead: false, Level: >= 100 } ||
            me.ClassJob.RowId != 41 || GameData.IsInPvPArea() ||
            target is not { IsDead: false, IsTargetable: true } || !target.CanUseAttackActionOn() ||
            TargetHelper.TargetHasInvincibilityStatus(target)) return null;
        // A carried-over free Reawaken belongs to the dynamic priority, not another opener Ire.
        if (me.GetStatusLeftTime(Buffs.ReadyToReawaken) > 0) return null;
        // Combat begins after the triggering hit. Avoid duplicating a manually used first Vicewinder.
        var firstUsed = (int)JobGaugeHelper.VPR.蛇剑连状态 == 1 ||
            _prepullAttack.IsRecentFirstHit(Environment.TickCount64, target.EntityId, Skills.Vicewinder);
        if (JobGaugeHelper.VPR.祖灵力档数 != 0 ||
            !firstUsed && (int)JobGaugeHelper.VPR.蛇剑连状态 != 0 ||
            Skills.Vicewinder.GetActionCharges() < (firstUsed ? 1 : 2) ||
            !ActionHelper.IsReady(Skills.SerpentsIre) || !Skills.SerpentsIre.IsActionAvailableByLevelAndQuest())
            return null;
        var potion = Qt("爆发药") ? GameData.GetBestPotionId() : 0;
        if (potion != 0 && ActionHelper.IsItemOnCooldown(potion)) potion = 0;
        var opener = new ViperStandardOpener(firstUsed, potion);
        return opener;
    }

    private ViperTeaOpener? GetTeaOpener()
    {
        var me = PRCore.Me;
        var target = PRCore.Target;
        if (PromeSettings.Instance.EnableAcr != AcrState.On || !UsesTeaOpener ||
            me is not { IsDead: false } || me.ClassJob.RowId != 41 || !TeaOpener.CanStart(me.Level, Options()) ||
            GameData.IsInPvPArea() || !IsTargetWithinAttackRange() ||
            target is not { IsDead: false, IsTargetable: true } || !target.CanUseAttackActionOn() ||
            TargetHelper.TargetHasInvincibilityStatus(target) || JobGaugeHelper.VPR.祖灵力档数 != 0 ||
            (int)JobGaugeHelper.VPR.蛇剑连状态 != 0 || Skills.Vicewinder.GetActionCharges() < 2 ||
            !Skills.Vicewinder.IsActionAvailableByLevelAndQuest()) return null;
        var firstUsed = _prepullAttack.IsRecentFirstHit(Environment.TickCount64, target.EntityId, Skills.ReavingFangs) ||
            ActionHelper.RecentlyUsed(Skills.ReavingFangs, 2500) && ActionHelper.GetComboLeftTime() > 0 &&
            Skills.ReavingFangs.GetAdjustedActionId() == Skills.SwiftskinsSting;
        var potion = Qt("爆发药") ? GameData.GetBestPotionId() : 0;
        if (potion != 0 && ActionHelper.IsItemOnCooldown(potion)) potion = 0;
        return new ViperTeaOpener(firstUsed, potion);
    }

    internal bool BindTeaOpener(ViperTeaOpener opener)
    {
        if (PromeSettings.Instance.EnableAcr != AcrState.On || !UsesTeaOpener ||
            PRCore.Me is not { IsDead: false } me || me.ClassJob.RowId != 41 ||
            !TeaOpener.CanStart(me.Level, Options()) || !GameData.IsInCombat() || GameData.IsInPvPArea() ||
            !IsTargetWithinAttackRange() || PRCore.Target is not { IsDead: false, IsTargetable: true } target ||
            !target.CanUseAttackActionOn() || TargetHelper.TargetHasInvincibilityStatus(target)) return false;
        _activeTeaOpener = opener;
        return true;
    }

    private ViperDsrOpener? GetDsrOpener()
    {
        var me = PRCore.Me;
        var target = PRCore.Target;
        if (PromeSettings.Instance.EnableAcr != AcrState.On || !UsesDsrOpener ||
            me is not { IsDead: false } || me.ClassJob.RowId != 41 || !DsrOpener.CanStart(me.Level, Options()) ||
            GameData.IsInPvPArea() || !IsTargetWithinAttackRange() ||
            target is not { IsDead: false, IsTargetable: true } || !target.CanUseAttackActionOn() ||
            TargetHelper.TargetHasInvincibilityStatus(target) || JobGaugeHelper.VPR.祖灵力档数 != 0 ||
            (int)JobGaugeHelper.VPR.蛇剑连状态 != 0 || Skills.Vicewinder.GetActionCharges() < 2 ||
            me.GetStatusLeftTime(Buffs.ReadyToReawaken) > 0 ||
            !Skills.Vicewinder.IsActionAvailableByLevelAndQuest() || !Skills.Reawaken.IsActionAvailableByLevelAndQuest() ||
            !Skills.SerpentsIre.IsActionAvailableByLevelAndQuest() || !ActionHelper.IsReady(Skills.SerpentsIre)) return null;
        var firstUsed = _prepullAttack.IsRecentFirstHit(Environment.TickCount64, target.EntityId, Skills.ReavingFangs) ||
            ActionHelper.RecentlyUsed(Skills.ReavingFangs, 2500) && ActionHelper.GetComboLeftTime() > 0 &&
            Skills.ReavingFangs.GetAdjustedActionId() == Skills.SwiftskinsSting;
        var potion = Qt("爆发药") ? GameData.GetBestPotionId() : 0;
        if (potion != 0 && ActionHelper.IsItemOnCooldown(potion)) potion = 0;
        return new ViperDsrOpener(firstUsed, potion, Qt("自动真北") && Skills.TrueNorth.GetActionCharges() > 0);
    }

    internal bool BindDsrOpener(ViperDsrOpener opener)
    {
        if (PromeSettings.Instance.EnableAcr != AcrState.On || !UsesDsrOpener ||
            PRCore.Me is not { IsDead: false } me || me.ClassJob.RowId != 41 ||
            !DsrOpener.CanStart(me.Level, Options()) || !GameData.IsInCombat() || GameData.IsInPvPArea() ||
            !IsTargetWithinAttackRange() || PRCore.Target is not { IsDead: false, IsTargetable: true } target ||
            !target.CanUseAttackActionOn() || TargetHelper.TargetHasInvincibilityStatus(target)) return false;
        _activeDsrOpener = opener;
        return true;
    }

    private ViperFruOpener? GetFruOpener()
    {
        var me = PRCore.Me;
        var target = PRCore.Target;
        if (PromeSettings.Instance.EnableAcr != AcrState.On || !UsesFruOpener ||
            me is not { IsDead: false } || me.ClassJob.RowId != 41 || !FruOpener.CanStart(me.Level, Options()) ||
            GameData.IsInPvPArea() || !IsTargetWithinAttackRange() ||
            target is not { IsDead: false, IsTargetable: true } || !target.CanUseAttackActionOn() ||
            TargetHelper.TargetHasInvincibilityStatus(target) || JobGaugeHelper.VPR.祖灵力档数 != 0 ||
            (int)JobGaugeHelper.VPR.蛇剑连状态 != 0 || Skills.Vicewinder.GetActionCharges() < 1 ||
            me.GetStatusLeftTime(Buffs.ReadyToReawaken) > 0 ||
            !Skills.Vicewinder.IsActionAvailableByLevelAndQuest() || !Skills.Reawaken.IsActionAvailableByLevelAndQuest() ||
            !Skills.SerpentsIre.IsActionAvailableByLevelAndQuest() || !ActionHelper.IsReady(Skills.SerpentsIre)) return null;
        var firstUsed = _prepullAttack.IsRecentFirstHit(Environment.TickCount64, target.EntityId, Skills.ReavingFangs) ||
            ActionHelper.RecentlyUsed(Skills.ReavingFangs, 2500) && ActionHelper.GetComboLeftTime() > 0 &&
            Skills.ReavingFangs.GetAdjustedActionId() == Skills.SwiftskinsSting;
        return new ViperFruOpener(firstUsed);
    }

    internal bool BindFruOpener(ViperFruOpener opener)
    {
        if (PromeSettings.Instance.EnableAcr != AcrState.On || !UsesFruOpener ||
            PRCore.Me is not { IsDead: false } me || me.ClassJob.RowId != 41 ||
            !FruOpener.CanStart(me.Level, Options()) || !GameData.IsInCombat() || GameData.IsInPvPArea() ||
            !IsTargetWithinAttackRange() || PRCore.Target is not { IsDead: false, IsTargetable: true } target ||
            !target.CanUseAttackActionOn() || TargetHelper.TargetHasInvincibilityStatus(target)) return false;
        _activeFruOpener = opener;
        return true;
    }

    internal bool BindStandardOpener(ViperStandardOpener opener)
    {
        if (PromeSettings.Instance.EnableAcr != AcrState.On || !UsesStandardOpener || !IsTargetWithinAttackRange() || !StandardOpener.CanStart(Options()) ||
            !GameData.IsInCombat() || PRCore.Target is not { IsDead: false, IsTargetable: true } target) return false;
        _activeOpener = opener;
        _openerTarget = target.EntityId;
        _openerPhaseTime = Environment.TickCount64;
        return true;
    }
    public IRotationEventHandler GetEventHandler() => _events;
    public void UpdateDebugStatus()
    {
        var state = Read(); var options = Options();
        var gcd = ViperEngine.Gcd(state, options); var ogcd = ViperEngine.OffGcd(state, options);
        RotationManager.GcdSolverStatus.Clear(); RotationManager.OffGcdSolverStatus.Clear();
        RotationManager.AlwaysSolverStatus.Clear();
        RotationManager.GcdSolverStatus.Add(new SolverStatus { Name = "蝰蛇 GCD", Success = gcd != null,
            Message = gcd is null ? "等待可攻击目标、范围或职业资源" : $"{gcd.ActionId}：{gcd.Reason}" });
        RotationManager.OffGcdSolverStatus.Add(new SolverStatus { Name = "蝰蛇 oGCD", Success = ogcd != null,
            Message = ogcd is null ? "等待安全插入窗口／触发状态" : $"{ogcd.ActionId}：{ogcd.Reason}" });
        var rescue = ViperEngine.Continuation(state);
        RotationManager.AlwaysSolverStatus.Add(new SolverStatus { Name = "续剑补救", Success = rescue != null,
            Message = rescue?.Reason ?? "无待执行续剑" });
    }
    public void DrawQTs()
    {
        foreach (var name in QtList.Keys)
        {
            var enabled = Qt(name);
            if (ImGui.Checkbox(name, ref enabled)) PromeSettings.Instance.SetQt(name, enabled);
        }
    }
    public void DrawSettings()
    {
        ErosUIFramework.DrawSettingsEntry();
        DrawBasicSettings();
        DrawRotationSettings();
    }
    private void DrawBasicSettings()
    {
        SettingRow.SectionTitle("起手选择");
        var selected = OpenerSelection.ToIndex(_settings.Opener);
        ImGui.BeginDisabled(CurrentMode != CombatMode.HighEnd);
        if (SettingRow.Combo("起手方案", ref selected, OpenerSelection.Labels))
        {
            _settings.Opener = OpenerSelection.FromIndex(selected);
            try { _settings.Save(); _error = ""; } catch (Exception ex) { _error = ex.Message; }
        }
        ImGui.EndDisabled();
        var potion = Qt("爆发药");
        if (SettingRow.Checkbox("是否使用爆发药", ref potion)) PromeSettings.Instance.SetQt("爆发药", potion);
        var autoPull = ErosUISettings.Instance.OpenerAutoPull;
        if (SettingRow.Checkbox("木人倒计时自动开怪", ref autoPull))
        {
            ErosUISettings.Instance.OpenerAutoPull = autoPull;
            ErosUISettings.Instance.Save();
        }
        var releaseHold = ErosUISettings.Instance.OpenerAutoReleaseHold;
        if (SettingRow.Checkbox("自动关闭停手", ref releaseHold))
        {
            ErosUISettings.Instance.OpenerAutoReleaseHold = releaseHold;
            ErosUISettings.Instance.Save();
        }
        if (_error.Length > 0) ImGui.TextWrapped(_error);
    }
    private void DrawOpenerSettings()
    {
        ErosUISettingsUI.DrawModeSwitchRow();
        var highEnd = CurrentMode == CombatMode.HighEnd;
        if (!highEnd) ImGui.TextWrapped("日随不使用固定起手，直接按实时循环出招；目标已开战时自动接战。下方选择仅影响高难模式。");
        var selected = OpenerSelection.ToIndex(_settings.Opener);
        ImGui.BeginDisabled(!highEnd);
        if (ImGui.Combo("高难起手方式", ref selected, OpenerSelection.Labels, OpenerSelection.Labels.Length))
        {
            _settings.Opener = OpenerSelection.FromIndex(selected);
            try { _settings.Save(); _error = ""; } catch (Exception ex) { _error = ex.Message; }
        }
        ImGui.EndDisabled();
        ImGui.TextWrapped("高难模式按此处选择起手，之后使用标准循环，规划祖灵爆发、灵力与蛇魂。默认100级标准起手。");
        ImGui.TextWrapped("倒计时蛇行固定为500ms，随ACR总开关启停；起手方式在下次开怪时生效。");
        if (_error.Length > 0) ImGui.TextWrapped(_error);
    }
    private void DrawRotationSettings()
    {
        ImGui.TextUnformatted($"Displaced VPR {ReleaseInfo.Version} | PromeRotation / 国服 API 15");
        ErosUISettingsUI.DrawModeSwitchRow();
        var highEnd = CurrentMode == CombatMode.HighEnd;
        ImGui.TextWrapped(highEnd ? "高难模式：按基础设置中的起手方案开怪，后续标准循环保留爆发规划。" :
            "日随模式：直接按实时循环有资源就用，选中目标已开战时自动接战；非8人小队目标低于20%不使用蛇灵气、不启动祖灵，已开启的祖灵仍完成。");
        SettingRow.SectionTitle("循环参数");
        var threshold = _settings.AoeThreshold;
        if (ImGui.SliderInt("AOE 目标数", ref threshold, 2, 8)) _settings.AoeThreshold = threshold;
        var reserve = _settings.ReserveCoils;
        ImGui.BeginDisabled(!highEnd);
        if (ImGui.SliderInt("保留飞蛇之魂", ref reserve, 0, 2)) _settings.ReserveCoils = reserve;
        var burstReserve = _settings.BurstReserveSeconds;
        if (ImGui.SliderFloat("爆发前保留灵力（秒）", ref burstReserve, 15, 60)) _settings.BurstReserveSeconds = burstReserve;
        ImGui.EndDisabled();
        var weave = _settings.WeaveSafetySeconds;
        if (ImGui.SliderFloat("插入所需剩余时间（秒）", ref weave, 0.65f, 1.2f)) _settings.WeaveSafetySeconds = weave;
        var trueNorthPercent = _settings.TrueNorthGcdPercent;
        if (ImGui.SliderInt("自动真北触发时机", ref trueNorthPercent, 0, 100, "%d%%"))
            _settings.TrueNorthGcdPercent = trueNorthPercent;
        if (ImGui.Button("保存设置")) { try { _settings.Save(); _error = ""; } catch (Exception ex) { _error = ex.Message; } }
        if (_error.Length > 0) ImGui.TextWrapped(_error);
    }
    private void DrawDiagnostics()
    {
        _diagnostics.Draw(Read(), _gcdReason, _ogcdReason);
    }
}

internal sealed class ViperEventHandler(Action reset, Action countdownUpdate, Action resetOpener) : IRotationEventHandler
{
    public Action? BattleUpdate { get; set; }
    public Action? Update { get; set; }
    public void OnUpdate() => Update?.Invoke();
    public void OnOutOfBattleUpdate() => countdownUpdate();
    public void OnBattleStarted() => reset();
    public void OnBattleUpdate() => BattleUpdate?.Invoke();
    public void OnNoTarget() => reset();
    public void OnBattleEnded() { reset(); resetOpener(); }
    public void OnTerritoryChanged(ushort territoryId) { reset(); resetOpener(); }
}
