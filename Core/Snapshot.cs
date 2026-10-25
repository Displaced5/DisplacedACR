namespace ViperACR.Core;

public enum Facing { None, Front, Flank, Rear }
public enum CombatMode { Daily, HighEnd }

public static class ModePolicy
{
    public static bool UsesStandardOpener(CombatMode mode, bool standardSelected) =>
        mode == CombatMode.HighEnd && standardSelected;
    public static bool AllowsTargetBurst(CombatMode mode, float targetHpPercent, int partySize) =>
        mode != CombatMode.Daily || partySize == 8 || targetHpPercent >= 20;
}

public sealed record RotationOptions
{
    public CombatMode Mode { get; init; } = CombatMode.HighEnd;
    public bool Burst { get; init; } = true;
    public bool Cooldowns { get; init; } = true;
    public bool Reawaken { get; init; } = true;
    public bool SerpentsIre { get; init; } = true;
    public bool Aoe { get; init; } = true;
    public bool Ranged { get; init; } = true;
    public bool AutoTrueNorth { get; init; } = true;
    public int TrueNorthGcdPercent { get; init; } = 50;
    public bool Healing { get; init; }
    public bool Potion { get; init; }
    public int AoeThreshold { get; init; } = 3;
    public int ReserveCoils { get; init; } = 1;
    public float BurstReserveSeconds { get; init; } = 45;
    public float SingleBuffSeconds { get; init; } = 14;
    public float DoubleBuffSeconds { get; init; } = 26;
    public float WeaveSafetySeconds { get; init; } = 0.75f;
}

public sealed record Snapshot
{
    public bool Active { get; init; }
    public int Level { get; init; } = 100;
    public bool HasTarget { get; init; }
    public uint TargetId { get; init; }
    public float Distance { get; init; }
    public bool LongArms { get; init; }
    public float SkillDistance => CombatRange.EffectiveDistance(Distance, LongArms);
    public int NearbyEnemies { get; init; } = 1;
    public int PartySize { get; init; }
    public int Coils { get; init; }
    public int Gauge { get; init; }
    public int Tribute { get; init; }
    public int DreadCombo { get; init; }
    public int TailCombo { get; init; }
    public float GcdRemaining { get; init; } = 2;
    public float GcdElapsed { get; init; }
    public float GcdProgressPercent => float.IsFinite(GcdElapsed) && float.IsFinite(GcdRemaining) &&
        GcdElapsed >= 0 && GcdRemaining >= 0 && GcdElapsed + GcdRemaining > 0
            ? 100f * (GcdElapsed / (GcdElapsed + GcdRemaining)) : float.NaN;
    public float ComboRemaining { get; init; }
    public Facing Facing { get; init; }
    public bool NeedsPositional { get; init; }
    public bool RaidBuff { get; init; }
    public float HpPercent { get; init; } = 100;
    public float TargetHpPercent { get; init; } = 100;
    public Dictionary<uint, float> BuffTimes { get; init; } = [];
    public Dictionary<uint, uint> Adjusted { get; init; } = [];
    public Dictionary<uint, float> Cooldowns { get; init; } = [];
    public Dictionary<uint, float> Charges { get; init; } = [];
    public HashSet<uint> Highlighted { get; init; } = [];
    public HashSet<uint> Locked { get; init; } = [];
    public HashSet<uint> UnusableOffGcd { get; init; } = [];
    public HashSet<uint> RecentlyUsed { get; init; } = [];
    public float Buff(uint id) => BuffTimes.GetValueOrDefault(id);
    public bool Has(uint id) => Buff(id) > 0;
    public float Cd(uint id) => Cooldowns.GetValueOrDefault(id);
    public uint Adjust(uint id) => Adjusted.GetValueOrDefault(id, id);
    public bool Learned(uint id) => Level >= Skills.Level(id) && !Locked.Contains(id);
    public bool CanReach(uint id) => CombatRange.Contains(Distance) &&
        (Skills.Range(id) == 0 || SkillDistance <= Skills.Range(id));
    public bool CanUse(uint id) => Learned(id) && CanReach(id) && !RecentlyUsed.Contains(id) &&
        (!Skills.IsOffGcd(id) || !UnusableOffGcd.Contains(id));
}

public sealed record Decision(uint ActionId, string Reason);
