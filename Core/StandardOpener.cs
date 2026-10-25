namespace ViperACR.Core;

public readonly record struct OpenerStep(uint ActionId, bool Potion = false);

// Standard 17-GCD opener; True North is managed outside the fixed opener.
// Zero denotes the optional potion slot; the host resolves the actual item.
public static class StandardOpener
{
    public const string Name = "蝰蛇标准起手";
    public static bool CanStart(RotationOptions options) =>
        options.Burst && options.Cooldowns && options.Reawaken && options.SerpentsIre;
    public static IReadOnlyList<OpenerStep> Steps { get; } = Array.AsReadOnly(new OpenerStep[] {
        new(Skills.Vicewinder),
        new(Skills.SerpentsIre), new(0, Potion: true),
        new(Skills.SwiftskinsCoil), new(Skills.TwinbloodBite), new(Skills.TwinfangBite),
        new(Skills.HuntersCoil), new(Skills.TwinfangBite), new(Skills.TwinbloodBite),
        new(Skills.Vicewinder),
        new(Skills.HuntersCoil), new(Skills.TwinfangBite), new(Skills.TwinbloodBite),
        new(Skills.SwiftskinsCoil), new(Skills.TwinbloodBite), new(Skills.TwinfangBite),
        new(Skills.Reawaken),
        new(Skills.FirstGeneration), new(Skills.FirstLegacy),
        new(Skills.SecondGeneration), new(Skills.SecondLegacy),
        new(Skills.ThirdGeneration), new(Skills.ThirdLegacy),
        new(Skills.FourthGeneration), new(Skills.FourthLegacy),
        new(Skills.Ouroboros),
        new(Skills.UncoiledFury), new(Skills.UncoiledTwinfang), new(Skills.UncoiledTwinblood),
        new(Skills.UncoiledFury), new(Skills.UncoiledTwinfang), new(Skills.UncoiledTwinblood),
        new(Skills.ReavingFangs), new(Skills.SwiftskinsSting),
        new(Skills.HindsbaneFang), new(Skills.DeathRattle)
    });
}
