using PromeRotation.Core;
using PromeRotation.Data;
using PromeRotation.Helpers;
using PromeRotation.Managers;
using PromeRotation.Rotation;
using ViperACR.Core;

namespace ViperACR.Host;

public sealed class ViperTeaOpener : IOpener
{
    public string OpenerName => TeaOpener.Name;
    private readonly bool _skipFirst;
    private readonly uint _potion;
    public bool Started { get; private set; }
    public ViperTeaOpener() : this(false, 0) { }
    public ViperTeaOpener(bool skipFirst, uint potion) { _skipFirst = skipFirst; _potion = potion; }

    public List<PAction> InCombatSequence
    {
        get
        {
            if (RotationManager.GetCurrentRotation() is ViperRotation rotation && !rotation.BindTeaOpener(this))
                return [];
            Started = true;
            var actions = new List<PAction>();
            foreach (var step in TeaOpener.Steps.Skip(_skipFirst ? 1 : 0))
            {
                if (step.Potion && _potion == 0) continue;
                var id = step.Potion ? _potion : step.ActionId;
                var type = step.Potion ? ActionType.Item : Skills.IsOffGcd(id) ? ActionType.OffGcd : ActionType.Gcd;
                actions.Add(new PAction(id, type, step.Potion ? ActionTargetType.Self : ActionTargetType.Target) {
                    RequiresVerification = type == ActionType.Gcd,
                    ExecutionGuard = () => ViperRotation.IsTargetWithinAttackRange(actionId: step.Potion ? Skills.SteelFangs : id)
                });
            }
            return actions;
        }
    }

    public void InitializeCountdown(CountDownHandler handler)
    {
        // The rotation owns both countdown actions to avoid duplicate host scheduling.
    }
}
