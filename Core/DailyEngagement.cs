namespace ViperACR.Core;

// Only a selected enemy already fighting may bring the player into combat.
public sealed class DailyEngagement
{
    private long _lastAttempt = long.MinValue;
    public bool TrySubmit(long now, CombatMode mode, bool playerInCombat, bool targetInCombat,
        float countdown, bool eligible, uint action)
    {
        if (mode != CombatMode.Daily || playerInCombat || !targetInCombat ||
            !float.IsFinite(countdown) || countdown > 0 || !eligible || action == 0 ||
            _lastAttempt != long.MinValue && now - _lastAttempt < 1000) return false;
        _lastAttempt = now;
        return true;
    }
    public void Reset() => _lastAttempt = long.MinValue;
}
