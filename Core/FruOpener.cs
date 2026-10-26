namespace ViperACR.Core;

public static class FruOpener
{
    public const string Name = "100级绝妖星起手";
    public static bool CanStart(int level, RotationOptions options) =>
        level == 100 && options.Mode == CombatMode.HighEnd && StandardOpener.CanStart(options);

    public static IReadOnlyList<OpenerStep> Steps { get; } = Array.AsReadOnly(new OpenerStep[] {
        new(Skills.ReavingFangs), new(Skills.SerpentsIre),
        new(Skills.SwiftskinsSting),
        new(Skills.Vicewinder),
        new(Skills.HuntersCoil), new(Skills.TwinfangBite), new(Skills.TwinbloodBite),
        new(Skills.SwiftskinsCoil), new(Skills.TwinbloodBite), new(Skills.TwinfangBite),
        new(Skills.Reawaken),
        new(Skills.FirstGeneration), new(Skills.FirstLegacy),
        new(Skills.SecondGeneration), new(Skills.SecondLegacy),
        new(Skills.ThirdGeneration), new(Skills.ThirdLegacy),
        new(Skills.FourthGeneration), new(Skills.FourthLegacy),
        new(Skills.Ouroboros),
        new(Skills.HindsbaneFang), new(Skills.DeathRattle)
    });
}
