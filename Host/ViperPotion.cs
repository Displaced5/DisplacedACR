using ErosUI;
using PromeRotation.Core;
using PromeRotation.Data;
using PromeRotation.Helpers;
using PromeRotation.Managers;
using PromeRotation.UI.HotKey;
using PRCore = PromeRotation.Core.Core;

namespace ViperACR.Host;

internal static class ViperPotion
{
    public const string Name = "爆发药";
    public const uint IconId = 20709;
    private const long RequestLifetimeMs = 5000;

    public static void Register(ErosUIHotkeyBuilder builder) =>
        builder.Execute(Name, new ExecuteLogic(Queue), gameIconID: IconId);

    public static void Queue()
    {
        if (PRCore.Me is not { IsDead: false } me || me.ClassJob.RowId != 41 ||
            GameData.IsInPvPArea() || GameData.IsPlayerOccupied()) return;
        var potion = GameData.GetBestPotionId();
        if (potion == 0 || ActionHelper.IsItemOnCooldown(potion)) return;
        var action = CreateAction(potion, me.EntityId, Environment.TickCount64);
        if (action != null) ActionQueueManager.Enqueue(action, isHighPriority: true);
    }

    internal static PAction? CreateAction(uint potionId, uint playerId, long requestedAt) =>
        potionId == 0 || playerId == 0 ? null : new PAction(potionId, ActionType.Item, ActionTargetType.Self) {
            // Item IDs retain the host's HQ offset. Recheck at execution to discard stale requests.
            ExecutionGuard = () => Environment.TickCount64 - requestedAt is >= 0 and <= RequestLifetimeMs &&
                PRCore.Me is { IsDead: false } me && me.EntityId == playerId && me.ClassJob.RowId == 41 &&
                !GameData.IsInPvPArea() && !GameData.IsPlayerOccupied() &&
                GameData.GetBestPotionId() == potionId && !ActionHelper.IsItemOnCooldown(potionId)
        };
}
