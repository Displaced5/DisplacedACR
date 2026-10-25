namespace ViperACR.Core;

// Action IDs, targeting and unlock levels for the CN client.
public static class Skills
{
    public const uint SteelFangs = 34606, ReavingFangs = 34607,
        HuntersSting = 34608, SwiftskinsSting = 34609,
        FlankstingStrike = 34610, FlanksbaneFang = 34611,
        HindstingStrike = 34612, HindsbaneFang = 34613,
        SteelMaw = 34614, ReavingMaw = 34615,
        HuntersBite = 34616, SwiftskinsBite = 34617,
        JaggedMaw = 34618, BloodiedMaw = 34619,
        Vicewinder = 34620, HuntersCoil = 34621, SwiftskinsCoil = 34622,
        Vicepit = 34623, HuntersDen = 34624, SwiftskinsDen = 34625,
        Reawaken = 34626, FirstGeneration = 34627, SecondGeneration = 34628,
        ThirdGeneration = 34629, FourthGeneration = 34630, Ouroboros = 34631,
        WrithingSnap = 34632, UncoiledFury = 34633,
        DeathRattle = 34634, LastLash = 34635,
        TwinfangBite = 34636, TwinbloodBite = 34637,
        TwinfangThresh = 34638, TwinbloodThresh = 34639,
        FirstLegacy = 34640, SecondLegacy = 34641,
        ThirdLegacy = 34642, FourthLegacy = 34643,
        UncoiledTwinfang = 34644, UncoiledTwinblood = 34645,
        Slither = 34646, SerpentsIre = 34647, SerpentsTail = 35920, Twinfang = 35921, Twinblood = 35922,
        SecondWind = 7541, Bloodbath = 7542, TrueNorth = 7546;

    public static readonly uint[] All = [
        .. Enumerable.Range(34606, 41).Select(x => (uint)x), SerpentsIre,
        SerpentsTail, Twinfang, Twinblood, SecondWind, Bloodbath, TrueNorth
    ];
    public static bool IsSelf(uint id) => id is SteelMaw or ReavingMaw or HuntersBite or
        SwiftskinsBite or JaggedMaw or BloodiedMaw or Vicepit or HuntersDen or SwiftskinsDen or
        Reawaken or LastLash or TwinfangThresh or TwinbloodThresh or SerpentsIre or
        SecondWind or Bloodbath or TrueNorth;
    public static bool IsOffGcd(uint id) => id is >= DeathRattle and <= UncoiledTwinblood or
        Slither or SerpentsIre or SecondWind or Bloodbath or TrueNorth;
    public static float Range(uint id) => IsSelf(id) ? (id is SerpentsIre or SecondWind or Bloodbath or TrueNorth ? 0 : 5)
        : id is Slither or WrithingSnap or UncoiledFury or UncoiledTwinfang or UncoiledTwinblood ? 20
        : IsOffGcd(id) ? 5 : 3;
    public static int Level(uint id) => id switch
    {
        SteelFangs => 1, HuntersSting => 5, ReavingFangs => 10, WrithingSnap => 15,
        SwiftskinsSting => 20, SteelMaw => 25,
        >= FlankstingStrike and <= HindsbaneFang => 30, ReavingMaw => 35,
        HuntersBite or Slither => 40, SwiftskinsBite => 45, JaggedMaw or BloodiedMaw => 50,
        DeathRattle or SerpentsTail => 55, LastLash => 60,
        Vicewinder or HuntersCoil or SwiftskinsCoil => 65,
        Vicepit or HuntersDen or SwiftskinsDen => 70,
        TwinfangBite or TwinbloodBite or Twinfang or Twinblood => 75,
        TwinfangThresh or TwinbloodThresh => 80,
        UncoiledFury => 82, SerpentsIre => 86,
        >= Reawaken and <= FourthGeneration => 90,
        UncoiledTwinfang or UncoiledTwinblood => 92,
        Ouroboros => 96, >= FirstLegacy and <= FourthLegacy => 100,
        SecondWind => 8, Bloodbath => 12, TrueNorth => 50, _ => 101
    };
}

public static class Buffs
{
    public const uint Flanksting = 3645, Flanksbane = 3646, Hindsting = 3647, Hindsbane = 3648,
        Jagged = 3649, Bloodied = 3650,
        HuntersVenom = 3657, SwiftskinsVenom = 3658, FellhuntersVenom = 3659, FellskinsVenom = 3660,
        PoisedTwinfang = 3665, PoisedTwinblood = 3666,
        HuntersInstinct = 3668, Swiftscaled = 3669, Reawakened = 3670, ReadyToReawaken = 3671,
        HonedSteel = 3672, HonedReavers = 3772, TrueNorth = 1250;
    public static readonly uint[] All = [Flanksting, Flanksbane, Hindsting, Hindsbane, Jagged, Bloodied,
        HuntersVenom, SwiftskinsVenom, FellhuntersVenom, FellskinsVenom, PoisedTwinfang, PoisedTwinblood,
        HuntersInstinct, Swiftscaled, Reawakened, ReadyToReawaken, HonedSteel, HonedReavers, TrueNorth];
}
