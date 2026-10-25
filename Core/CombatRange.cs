namespace ViperACR.Core;

public static class CombatRange
{
    public const float Bonus = 3;
    public static bool Contains(float distance) => float.IsFinite(distance) && distance >= 0;
    public static float EffectiveDistance(float distance, bool longArms) =>
        Contains(distance) ? Math.Max(0, distance - (longArms ? Bonus : 0)) : float.PositiveInfinity;
    public static bool CanDash(float distance, bool longArms, bool unlimitedDash) =>
        Contains(distance) && (unlimitedDash || EffectiveDistance(distance, longArms) <= Skills.Range(Skills.Slither));
}
