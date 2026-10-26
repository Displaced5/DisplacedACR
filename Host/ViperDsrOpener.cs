using PromeRotation.Core;
using PromeRotation.Data;
using PromeRotation.Helpers;
using PromeRotation.Managers;
using PromeRotation.Rotation;
using ViperACR.Core;

namespace ViperACR.Host;

public sealed class ViperDsrOpener : IOpener
{
    public string OpenerName => DsrOpener.Name;
    private readonly bool _skipFirst;
    private readonly uint _potion;
    private readonly bool _trueNorth;
    public bool Started { get; private set; }
    public ViperDsrOpener() : this(false, 0, true) { }
    public ViperDsrOpener(bool skipFirst, uint potion, bool trueNorth)
    { _skipFirst = skipFirst; _potion = potion; _trueNorth = trueNorth; }

    public List<PAction> InCombatSequence
    {
        get
        {
            if (RotationManager.GetCurrentRotation() is ViperRotation rotation && !rotation.BindDsrOpener(this))
                return [];
            Started = true;
            var actions = new List<PAction>();
            foreach (var step in DsrOpener.Steps.Skip(_skipFirst ? 1 : 0))
            {
                if (step.Potion && _potion == 0 || step.ActionId == Skills.TrueNorth && !_trueNorth) continue;
                var id = step.Potion ? _potion : step.ActionId;
                var type = step.Potion ? ActionType.Item : Skills.IsOffGcd(id) ? ActionType.OffGcd : ActionType.Gcd;
                actions.Add(new PAction(id, type, step.Potion || Skills.IsSelf(id) ? ActionTargetType.Self : ActionTargetType.Target) {
                    RequiresVerification = type == ActionType.Gcd,
                    ExecutionGuard = () => ViperRotation.IsTargetWithinAttackRange(actionId: step.Potion ? Skills.SteelFangs : id)
                });
            }
            return actions;
        }
    }

    public void InitializeCountdown(CountDownHandler handler)
    {
        // Slither is submitted by the rotation's guarded 500 ms countdown callback.
    }
}
