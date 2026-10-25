namespace ViperACR.Core;

// Each phase is requested only after the preceding host command has drained.
// Coil phases use live gauge/position rather than position at opener construction.
public sealed class StandardOpenerPlan
{
    private int _phase;
    private bool _usedPreparedReawaken;
    public bool Complete => _phase >= 6;

    public IReadOnlyList<OpenerStep> InitialSteps(bool skipFirst) =>
        StandardOpener.Steps.Skip(skipFirst ? 1 : 0).Take(skipFirst ? 2 : 3).ToArray();

    public IReadOnlyList<OpenerStep>? Next(Snapshot state)
    {
        if (!state.Active || !state.HasTarget || !CombatRange.Contains(state.Distance) || Complete) return null;
        if (state.Tribute > 0 || state.Has(Buffs.Reawakened)) return null;
        // At a completed combo boundary, spend the free Reawaken before starting another Vicewinder.
        if (!_usedPreparedReawaken && _phase == 2 && state.DreadCombo == 0 && state.Tribute == 0 &&
            !state.Has(Buffs.Reawakened) && state.Has(Buffs.ReadyToReawaken) && state.CanUse(Skills.Reawaken))
        {
            _usedPreparedReawaken = true;
            return StandardOpener.Steps.Skip(16).Take(10).ToArray();
        }
        IReadOnlyList<OpenerStep>? next = _phase switch {
            0 or 3 when state.DreadCombo == 1 => Coil(ViperEngine.PreferredCoil(state)),
            1 or 4 when state.DreadCombo == 2 => Coil(Skills.SwiftskinsCoil),
            1 or 4 when state.DreadCombo == 3 => Coil(Skills.HuntersCoil),
            2 when state.DreadCombo == 0 => [new(Skills.Vicewinder)],
            5 when state.DreadCombo == 0 => StandardOpener.Steps.Skip(_usedPreparedReawaken ? 26 : 16).ToArray(),
            _ => null
        };
        if (next != null) _phase++;
        return next;
    }

    private static OpenerStep[] Coil(uint skill) => skill == Skills.HuntersCoil
        ? [new(skill), new(Skills.TwinfangBite), new(Skills.TwinbloodBite)]
        : [new(skill), new(Skills.TwinbloodBite), new(Skills.TwinfangBite)];
}
