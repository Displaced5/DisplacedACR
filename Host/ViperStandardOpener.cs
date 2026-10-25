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
    private readonly uint _potion;
    private readonly StandardOpenerPlan _plan = new();
    public bool Started { get; private set; }
    public bool Complete => _plan.Complete;
    public ViperStandardOpener() : this(false, 0) { }
    public ViperStandardOpener(bool skipFirst, uint potion)
    { _skipFirst = skipFirst; _potion = potion; }

    public List<PAction> InCombatSequence
    {
        get
        {
            // Also bind when PR selects the registered opener or the TXT override directly.
            if (RotationManager.GetCurrentRotation() is ViperRotation rotation && !rotation.BindStandardOpener(this))
                return [];
            Started = true;
            return Convert(_plan.InitialSteps(_skipFirst));
        }
    }

    public List<PAction>? NextPhase(Snapshot state) => _plan.Next(state) is { } steps ? Convert(steps) : null;

    private List<PAction> Convert(IReadOnlyList<OpenerStep> steps)
    {
        var result = new List<PAction>();
        foreach (var step in steps)
        {
            if (step.Potion && _potion == 0) continue;
            var id = step.Potion ? _potion : step.ActionId;
            var type = step.Potion ? ActionType.Item : Skills.IsOffGcd(id) ? ActionType.OffGcd : ActionType.Gcd;
            result.Add(new PAction(id, type, step.Potion || Skills.IsSelf(id) ? ActionTargetType.Self : ActionTargetType.Target) {
                RequiresVerification = type == ActionType.Gcd,
                ExecutionGuard = () => ViperRotation.IsTargetWithinAttackRange(actionId: step.Potion ? Skills.SteelFangs : id)
            });
        }
        return result;
    }

    public void InitializeCountdown(CountDownHandler handler)
    {
        // The integrated ACR owns Slither in OnOutOfBattleUpdate at 500 ms.
        // Do not also queue it here: this PR version ignores countdown action guards.
    }
}
