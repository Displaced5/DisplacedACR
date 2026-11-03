namespace ViperACR.Core;

public sealed class StandardOpenerPlan
{
    private int _phase;
    public bool Complete => _phase >= 4;

    public IReadOnlyList<OpenerStep> InitialSteps(bool skipFirst) =>
        StandardOpener.Steps.Skip(skipFirst ? 1 : 0).Take(skipFirst ? 3 : 4).ToArray();

    public IReadOnlyList<OpenerStep>? Next(Snapshot state, bool autoTrueNorth)
    {
        if (!state.Active || !state.HasTarget || !CombatRange.Contains(state.Distance) || Complete ||
            state.Tribute > 0 || state.Has(Buffs.Reawakened)) return null;

        var coil = _phase switch {
            0 when state.DreadCombo == 1 => Skills.HuntersCoil,
            1 when state.DreadCombo == 2 => Skills.SwiftskinsCoil,
            _ => 0u
        };
        if (coil != 0)
        {
            _phase++;
            var steps = new List<OpenerStep>();
            if (OpenerPositioning.ShouldUseTrueNorth(coil, state, autoTrueNorth))
                steps.Add(new(Skills.TrueNorth));
            steps.Add(new(coil));
            steps.AddRange(coil == Skills.HuntersCoil
                ? [new(Skills.TwinfangBite), new(Skills.TwinbloodBite)]
                : [new(Skills.TwinbloodBite), new(Skills.TwinfangBite)]);
            return steps;
        }

        if (_phase == 2 && state.DreadCombo == 0)
        {
            _phase++;
            return StandardOpener.Steps.Skip(10).Take(10).ToArray();
        }
        if (_phase == 3 && state.DreadCombo == 0)
        {
            _phase++;
            var steps = new List<OpenerStep>();
            if (OpenerPositioning.ShouldUseTrueNorth(Skills.HindsbaneFang, state, autoTrueNorth))
                steps.Add(new(Skills.TrueNorth));
            steps.AddRange(StandardOpener.Steps.Skip(20));
            return steps;
        }
        return null;
    }
}
