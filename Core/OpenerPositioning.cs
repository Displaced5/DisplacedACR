namespace ViperACR.Core;

public static class OpenerPositioning
{
    public static bool ShouldUseTrueNorth(uint actionId, Snapshot state, bool enabled) =>
        enabled && state.Active && state.HasTarget && state.Charges.GetValueOrDefault(Skills.TrueNorth) >= 1 &&
        !state.Has(Buffs.TrueNorth) && state.CanUse(Skills.TrueNorth) && IsWrong(actionId, state);

    public static bool IsWrong(uint actionId, Snapshot state)
    {
        if (!state.NeedsPositional) return false;
        return actionId switch {
            Skills.HuntersCoil => state.Facing != Facing.Flank,
            Skills.SwiftskinsCoil or Skills.HindsbaneFang => state.Facing != Facing.Rear,
            _ => false
        };
    }
}
