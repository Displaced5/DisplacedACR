namespace ViperACR.Core;

public sealed class BasicAutomation
{
    private bool _wasDead;
    private float _previousCountdown;

    public bool ObserveDeath(bool dead, bool enabled)
    {
        var reset = dead && !_wasDead && enabled;
        _wasDead = dead;
        return reset;
    }

    // Resume only at a new timer's edge; a manual Hold during this timer remains respected.
    public bool ObserveCountdown(float remaining, bool enabled, bool holding, bool eligible)
    {
        if (!float.IsFinite(remaining) || remaining <= 0)
        { _previousCountdown = 0; return false; }
        var started = _previousCountdown <= 0 || remaining > _previousCountdown + 0.5f;
        _previousCountdown = remaining;
        return started && enabled && holding && eligible;
    }

    public static bool AllowsCountdownTarget(bool trainingDummy, bool enabled) => !trainingDummy || enabled;
    // BNpcName row 541 identifies training dummies independently of display language.
    public static bool IsTrainingDummy(uint nameId) => nameId == 541;
    public void Reset() { _wasDead = false; _previousCountdown = 0; }
}
