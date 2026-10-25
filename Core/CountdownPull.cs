namespace ViperACR.Core;

// One submission per countdown. Observe even while disabled so cancellation/restarts reset it.
public sealed class CountdownPull
{
    public const int TriggerMilliseconds = 500;
    public const float TriggerSeconds = TriggerMilliseconds / 1000f;
    private float _previous;
    private bool _submitted;

    public void Reset() { _previous = 0; _submitted = false; }

    public bool TrySubmit(float remaining, bool eligible)
    {
        if (!float.IsFinite(remaining) || remaining <= 0) { Reset(); return false; }
        // A timer restarted without a zero frame is a new countdown as well.
        if (remaining > _previous + 0.5f) _submitted = false;
        _previous = remaining;
        if (_submitted || remaining > TriggerSeconds || !eligible) return false;
        _submitted = true;
        return true;
    }
}
