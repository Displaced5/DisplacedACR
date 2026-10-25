namespace ViperACR.Core;

// A short, target-bound handoff from Slither to a single attack, independent of combat state.
public sealed class PrepullAttack
{
    public uint TargetId { get; private set; }
    public uint SubmittedAction { get; private set; }
    private long _expectedEnd, _expires, _submittedAt;
    private bool _armed;
    public bool HasPending => _armed;

    public void Reset() { TargetId = SubmittedAction = 0; _armed = false; }
    public void Arm(uint targetId, long now, float countdown)
    {
        Reset();
        if (targetId == 0 || !float.IsFinite(countdown) || countdown <= 0 || countdown > 0.5f) return;
        TargetId = targetId; _expectedEnd = now + (long)(countdown * 1000);
        _expires = now + 2000; _armed = true;
    }
    public bool TrySubmit(long now, float countdown, uint currentTarget, bool allowed, bool ready, uint actionId)
    {
        if (!_armed) return false;
        // A disappearing timer well before its expected end means cancellation.
        if (!allowed || !float.IsFinite(countdown) || countdown > 0.75f || now > _expires ||
            currentTarget != TargetId || countdown <= 0 && now < _expectedEnd - 50)
        { Reset(); return false; }
        if (!ready || actionId == 0) return false;
        _armed = false; SubmittedAction = actionId; _submittedAt = now;
        return true;
    }
    public bool IsRecentFirstHit(long now, uint targetId, uint actionId) => SubmittedAction == actionId &&
        TargetId == targetId && now - _submittedAt is >= 0 and <= 2500;
}
