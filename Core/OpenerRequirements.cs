namespace ViperACR.Core;

public static class OpenerRequirements
{
    public static bool CanStart(RotationOptions options) =>
        options.Burst && options.Cooldowns && options.Reawaken && options.SerpentsIre;
}
