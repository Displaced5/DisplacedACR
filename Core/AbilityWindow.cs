namespace ViperACR.Core;

// Track actual game recast transitions; a queue-window submission may skip GCD remaining == 0.
public sealed class AbilityWindow
{
    private float _lastElapsed, _lastRemaining;
    private bool _observed;
    private int _used;
    private long _lastSubmit = long.MinValue;
    private readonly Dictionary<uint, long> _recent = [];

    public void Observe(float elapsed, float remaining)
    {
        if (!_observed || elapsed + 0.05f < _lastElapsed || remaining > _lastRemaining + 0.1f) _used = 0;
        _observed = true; _lastElapsed = elapsed; _lastRemaining = remaining;
    }
    public static int Maximum(bool tripleWeave, int hostMaximum) =>
        tripleWeave ? 3 : Math.Clamp(hostMaximum, 0, 2);
    public bool CanSubmit(uint action, long now, int maximum) =>
        _used < Math.Clamp(maximum, 0, 3) &&
        (_lastSubmit == long.MinValue || now - _lastSubmit >= 600) &&
        (!_recent.TryGetValue(action, out var last) || now - last >= 650);
    public void Record(uint action, long now) { _used++; _lastSubmit = now; _recent[action] = now; }
    public void Reset() { _observed = false; _used = 0; _lastSubmit = long.MinValue; _recent.Clear(); }
}
