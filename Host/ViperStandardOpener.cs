using PromeRotation.Core;
using PromeRotation.Data;
using PromeRotation.Helpers;
using PromeRotation.Managers;
using PromeRotation.Rotation;
using ViperACR.Core;

namespace ViperACR.Host;

public sealed class ViperStandardOpener : IOpener
{
    public string OpenerName => StandardOpener.Name;
    private readonly bool _skipFirst;
    private readonly StandardOpenerPlan _plan = new();
    public bool Started { get; private set; }
    public bool Complete => _plan.Complete;

    public ViperStandardOpener() : this(false) { }
    public ViperStandardOpener(bool skipFirst) => _skipFirst = skipFirst;

    public List<PAction> InCombatSequence
    {
        get
        {
            if (RotationManager.GetCurrentRotation() is ViperRotation rotation && !rotation.BindStandardOpener(this))
                return [];
            Started = true;
            var actions = new List<PAction>();
            foreach (var step in _plan.InitialSteps(_skipFirst))
                actions.Add(Convert(step));
            return actions;
        }
    }

    public List<PAction>? NextPhase(Snapshot state, bool autoTrueNorth)
    {
        if (_plan.Next(state, autoTrueNorth) is not { } steps) return null;
        return steps.Select((step, index) => Convert(step,
            step.ActionId == Skills.TrueNorth ? steps[index + 1].ActionId : 0)).ToList();
    }

    private static PAction Convert(OpenerStep step, uint positionalAction = 0)
    {
        var id = step.ActionId;
        if (id == Skills.TrueNorth)
            return new PAction(id, ActionType.OffGcd, ActionTargetType.Self) {
                ExecutionGuard = () => ViperRotation.ShouldUseOpenerTrueNorth(positionalAction)
            };
        var type = Skills.IsOffGcd(id) ? ActionType.OffGcd : ActionType.Gcd;
        return new PAction(id, type, Skills.IsSelf(id) ? ActionTargetType.Self : ActionTargetType.Target) {
            RequiresVerification = type == ActionType.Gcd,
            ExecutionGuard = () => ViperRotation.IsTargetWithinAttackRange(actionId: id)
        };
    }

    public void InitializeCountdown(CountDownHandler handler)
    {
        // Slither is submitted by the rotation's guarded 500 ms countdown callback.
    }
}
