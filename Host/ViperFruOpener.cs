using PromeRotation.Core;
using PromeRotation.Data;
using PromeRotation.Helpers;
using PromeRotation.Managers;
using PromeRotation.Rotation;
using ViperACR.Core;

namespace ViperACR.Host;

public sealed class ViperFruOpener : IOpener
{
    public string OpenerName => FruOpener.Name;
    private readonly bool _skipFirst;
    public bool Started { get; private set; }
    public ViperFruOpener() : this(false) { }
    public ViperFruOpener(bool skipFirst) => _skipFirst = skipFirst;

    public List<PAction> InCombatSequence
    {
        get
        {
            if (RotationManager.GetCurrentRotation() is ViperRotation rotation && !rotation.BindFruOpener(this))
                return [];
            Started = true;
            return FruOpener.Steps.Skip(_skipFirst ? 1 : 0).Select(step => {
                var id = step.ActionId;
                var type = Skills.IsOffGcd(id) ? ActionType.OffGcd : ActionType.Gcd;
                return new PAction(id, type, Skills.IsSelf(id) ? ActionTargetType.Self : ActionTargetType.Target) {
                    RequiresVerification = type == ActionType.Gcd,
                    ExecutionGuard = () => ViperRotation.IsTargetWithinAttackRange(actionId: id)
                };
            }).ToList();
        }
    }

    public void InitializeCountdown(CountDownHandler handler)
    {
        // Slither is submitted by the rotation's guarded 500 ms countdown callback.
    }
}
