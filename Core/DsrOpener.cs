namespace ViperACR.Core;

public static class DsrOpener
{
    public const string Name = "90级绝龙诗起手";
    public static bool CanStart(int level, RotationOptions options) =>
        level == 90 && options.Mode == CombatMode.HighEnd && StandardOpener.CanStart(options);

    public static IReadOnlyList<OpenerStep> Steps { get; } = Array.AsReadOnly(new OpenerStep[] {
        new(Skills.ReavingFangs), new(Skills.SerpentsIre),
        new(Skills.SwiftskinsSting), new(0, Potion: true),
        new(Skills.Vicewinder), new(Skills.TrueNorth),
        new(Skills.HuntersCoil), new(Skills.TwinfangBite), new(Skills.TwinbloodBite),
        new(Skills.SwiftskinsCoil), new(Skills.TwinbloodBite), new(Skills.TwinfangBite),
        new(Skills.Reawaken),
        new(Skills.FirstGeneration), new(Skills.SecondGeneration),
        new(Skills.ThirdGeneration), new(Skills.FourthGeneration),
        new(Skills.Vicewinder),
        new(Skills.HuntersCoil), new(Skills.TwinfangBite), new(Skills.TwinbloodBite),
        new(Skills.SwiftskinsCoil), new(Skills.TwinbloodBite), new(Skills.TwinfangBite),
        new(Skills.HindsbaneFang), new(Skills.DeathRattle),
        new(Skills.UncoiledFury)
    });
}
