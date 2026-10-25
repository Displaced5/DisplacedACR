using System.Numerics;
using ECommons.DalamudServices;
using PromeRotation.Core;
using PromeRotation.Extensions;
using PromeRotation.Helpers;
using ViperACR.Core;
using PRCore = PromeRotation.Core.Core;
using FFXIVClientStructs.FFXIV.Client.Game;
using NativeActionType = FFXIVClientStructs.FFXIV.Client.Game.ActionType;

namespace ViperACR.Host;

internal static class HostSnapshot
{
    internal static int PartySize() => Svc.Party?.Length ?? 0;

    internal static unsafe bool IsTargetInCombat()
    {
        var target = PRCore.Target;
        return target != null && target.Address != 0 &&
            ((FFXIVClientStructs.FFXIV.Client.Game.Character.BattleChara*)target.Address)->InCombat;
    }
    public static unsafe Snapshot Capture(bool allowEngagedTarget = false)
    {
        var me = PRCore.Me;
        if (me == null || me.IsDead || me.ClassJob.RowId != 41 || GameData.IsInPvPArea() ||
            GameData.IsPlayerOccupied()) return new();
        var target = PRCore.Target;
        var valid = target != null && !target.IsDead && target.IsTargetable && target.CanUseAttackActionOn() &&
                    !TargetHelper.TargetHasInvincibilityStatus(target);
        if (!GameData.IsInCombat() && !(allowEngagedTarget && valid && IsTargetInCombat())) return new();
        var distance = valid ? Math.Max(0, Vector2.Distance(new(me.Position.X, me.Position.Z),
            new(target!.Position.X, target.Position.Z)) - target.HitboxRadius) : float.PositiveInfinity;
        var buffs = Buffs.All.ToDictionary(id => id, id => me.GetStatusLeftTime(id));
        var adjusted = new Dictionary<uint, uint>();
        foreach (var id in new[] { Skills.SteelFangs, Skills.ReavingFangs, Skills.SteelMaw, Skills.ReavingMaw,
                     Skills.Twinfang, Skills.Twinblood, Skills.SerpentsTail })
            adjusted[id] = id.GetAdjustedActionId();
        var locked = new HashSet<uint>();
        var highlighted = new HashSet<uint>();
        var unavailable = new HashSet<uint>();
        var recent = new HashSet<uint>();
        var manager = ActionManager.Instance();
        foreach (var id in Skills.All)
        {
            if (me.Level < Skills.Level(id) || !id.IsActionAvailableByLevelAndQuest()) { locked.Add(id); continue; }
            if (id.IsActionHighlighted()) highlighted.Add(id);
            if (ActionHelper.RecentlyUsed(id, 650)) recent.Add(id);
            // The game checks transformed-action requirements, charges and recast together.
            // PR IsReady/CanCast only inspect adjusted recast and can reject charge actions.
            if (Skills.IsOffGcd(id) && (manager == null ||
                manager->GetActionStatus(NativeActionType.Action, id,
                    (Skills.IsSelf(id) ? me : target)?.GameObjectId ?? 0xE0000000UL) != 0)) unavailable.Add(id);
        }
        var positional = valid && TargetHelper.HasPositionalRequirement(target!);
        var facing = positional ? TargetHelper.GetTargetPositional() switch {
            PromeRotation.Data.Positional.Flank => Facing.Flank,
            PromeRotation.Data.Positional.Rear => Facing.Rear,
            PromeRotation.Data.Positional.Front => Facing.Front, _ => Facing.None } : Facing.None;
        return new Snapshot {
            Active = true, HasTarget = valid, TargetId = valid ? target!.EntityId : 0,
            Level = me.Level, Distance = distance, LongArms = ErosUI.ErosUISettings.Instance.LongArms,
            NearbyEnemies = valid ? (int)TargetHelper.EnemyInRange(ErosUI.ErosUISettings.Instance.LongArms ? 8 : 5) : 0,
            PartySize = PartySize(),
            Coils = JobGaugeHelper.VPR.飞蛇之魂层数, Gauge = JobGaugeHelper.VPR.灵力值,
            Tribute = JobGaugeHelper.VPR.祖灵力档数,
            DreadCombo = (int)JobGaugeHelper.VPR.蛇剑连状态, TailCombo = (int)JobGaugeHelper.VPR.蛇尾术状态,
            GcdRemaining = ActionHelper.GetGcdRemain(), GcdElapsed = ActionHelper.GetGcdElapsed(),
            ComboRemaining = ActionHelper.GetComboLeftTime(),
            HpPercent = me.MaxHp > 0 ? 100f * me.CurrentHp / me.MaxHp : 100,
            TargetHpPercent = valid && target!.MaxHp > 0 ? 100f * target.CurrentHp / target.MaxHp : 100,
            NeedsPositional = positional, Facing = facing, RaidBuff = GameData.IsInPure120(),
            BuffTimes = buffs, Adjusted = adjusted, Locked = locked, Highlighted = highlighted,
            UnusableOffGcd = unavailable, RecentlyUsed = recent,
            Cooldowns = new[] { Skills.Vicewinder, Skills.SerpentsIre, Skills.SecondWind, Skills.Bloodbath }
                .ToDictionary(id => id, id => id.GetActionCooldown()),
            Charges = new[] { Skills.Vicewinder, Skills.TrueNorth }.ToDictionary(id => id, id => id.GetActionCharges())
        };
    }
}
