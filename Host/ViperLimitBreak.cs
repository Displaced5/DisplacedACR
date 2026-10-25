using PromeRotation.Core;
using PromeRotation.Data;
using PromeRotation.Extensions;
using PromeRotation.Helpers;
using PromeRotation.Managers;
using PRCore = PromeRotation.Core.Core;

namespace ViperACR.Host;

internal static class ViperLimitBreak
{
    public const uint IconActionId = 200;

    public static void Queue()
    {
        if (PRCore.Me is not { IsDead: false } me || me.ClassJob.RowId != 41 ||
            GameData.IsInPvPArea() || PRCore.Target is not { IsDead: false, IsTargetable: true } target ||
            !target.CanUseAttackActionOn() || TargetHelper.TargetHasInvincibilityStatus(target)) return;
        var action = CreateAction(LimitBreakHelper.GetLimitBreakActionId(), target.EntityId);
        if (action != null) HotkeyQueueManager.TryEnqueue(action);
    }

    internal static PAction? CreateAction(uint actionId, uint targetId) => actionId == 0 || targetId == 0 ? null :
        new PAction(actionId, ActionType.LimitBreak, ActionTargetType.Target) {
            NetworkTid = targetId,
            ExecutionGuard = () => PRCore.Me is { IsDead: false } me && me.ClassJob.RowId == 41 &&
                !GameData.IsInPvPArea() && PRCore.Target is { IsDead: false, IsTargetable: true } target &&
                target.EntityId == targetId && target.CanUseAttackActionOn() &&
                !TargetHelper.TargetHasInvincibilityStatus(target) &&
                LimitBreakHelper.GetLimitBreakActionId() == actionId && ActionHelper.CanCast(actionId, target)
        };
}
