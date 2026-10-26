namespace ViperACR.Core;

public static class TeaOpener
{
    public const string Name = "80级绝亚起手";
    public const float TrueNorthSeconds = 4;
    public static bool CanStart(int level, RotationOptions options) =>
        level == 80 && options.Mode == CombatMode.HighEnd && options.Burst && options.Cooldowns;

    public static IReadOnlyList<OpenerStep> Steps { get; } = Array.AsReadOnly(new OpenerStep[] {
        new(Skills.ReavingFangs),
        new(Skills.SwiftskinsSting), new(0, Potion: true),
        new(Skills.Vicewinder),
        new(Skills.HuntersCoil), new(Skills.TwinfangBite), new(Skills.TwinbloodBite),
        new(Skills.SwiftskinsCoil), new(Skills.TwinbloodBite), new(Skills.TwinfangBite),
        new(Skills.HindstingStrike), new(Skills.DeathRattle),
        new(Skills.SteelFangs),
        new(Skills.HuntersSting),
        new(Skills.Vicewinder),
        new(Skills.SwiftskinsCoil), new(Skills.TwinbloodBite), new(Skills.TwinfangBite),
        new(Skills.HuntersCoil), new(Skills.TwinfangBite), new(Skills.TwinbloodBite)
    });
}
