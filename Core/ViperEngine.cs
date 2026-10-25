namespace ViperACR.Core;

public static class ViperEngine
{
    public static Decision? Gcd(Snapshot s, RotationOptions o)
    {
        if (!s.Active || !s.HasTarget || !CombatRange.Contains(s.Distance)) return null;
        // Resource and continuation state comes from the game, never from the number of resolver calls.
        if (s.Tribute > 0 || s.Has(Buffs.Reawakened))
        {
            if (s.SkillDistance > 3) return Ranged(s, o);
            if (s.Tribute == 0) return null; // Wait for the gauge packet; never restart at First Generation.
            var step = (s.Level >= 96 ? 5 : 4) - s.Tribute;
            var action = step switch { 0 => Skills.FirstGeneration, 1 => Skills.SecondGeneration,
                2 => Skills.ThirdGeneration, 3 => Skills.FourthGeneration, 4 => Skills.Ouroboros, _ => 0u };
            return Choose(s, action, "按祖灵力档数继续祖灵连击");
        }
        if (s.SkillDistance > 3 && !(UseAoe(s, o) && s.SkillDistance <= 5)) return Ranged(s, o);

        // Complete the active Vicewinder family before starting Reawaken in either mode.
        if (s.DreadCombo != 0)
        {
            var action = s.DreadCombo switch {
                1 => PreferredCoil(s),
                2 => Skills.SwiftskinsCoil, 3 => Skills.HuntersCoil,
                4 => s.Buff(Buffs.HuntersInstinct) < s.Buff(Buffs.Swiftscaled) ? Skills.HuntersDen : Skills.SwiftskinsDen,
                5 => Skills.SwiftskinsDen, 6 => Skills.HuntersDen, _ => 0u };
            // A fresh opener needs damage first, after the basic combo supplied haste.
            if (s.DreadCombo == 4 && !s.Has(Buffs.HuntersInstinct)) action = Skills.HuntersDen;
            return Choose(s, action, "完成蛇剑／蟒剑连击，禁止被祖灵打断");
        }

        var aoe = UseAoe(s, o);
        var ireCooldown = o.Burst && o.SerpentsIre && s.Learned(Skills.SerpentsIre)
            ? s.Cd(Skills.SerpentsIre) : float.PositiveInfinity;
        var leftBasic = s.Adjust(aoe ? Skills.SteelMaw : Skills.SteelFangs);
        var rightBasic = s.Adjust(aoe ? Skills.ReavingMaw : Skills.ReavingFangs);
        var basicPending = new[] { leftBasic, rightBasic }.Any(id =>
            id is >= Skills.HuntersSting and <= Skills.HindsbaneFang or >= Skills.HuntersBite and <= Skills.BloodiedMaw);
        var comboReserve = o.Mode == CombatMode.HighEnd && s.Has(Buffs.ReadyToReawaken) && s.Gauge >= 50
            ? o.DoubleBuffSeconds : o.SingleBuffSeconds;
        // Long resource spenders preserve the basic combo but still consume its 30-second timer.
        if (basicPending && s.ComboRemaining > 0 &&
            (o.Mode == CombatMode.Daily || s.ComboRemaining <= comboReserve))
            return Basic(s, aoe);
        if (o.Mode == CombatMode.Daily && ModePolicy.AllowsTargetBurst(o.Mode, s.TargetHpPercent) &&
            o.Burst && o.Reawaken && (s.Gauge >= 50 || s.Has(Buffs.ReadyToReawaken)) &&
            Choose(s, Skills.Reawaken, "日随：连招完成后优先祖灵降临") is { } dailyReawaken)
            return dailyReawaken;
        if (o.Mode == CombatMode.Daily) return DailyGcd(s, o, aoe);
        var vice = aoe ? Skills.Vicepit : Skills.Vicewinder;
        var viceReady = o.Cooldowns && s.Learned(vice) && s.Charges.GetValueOrDefault(Skills.Vicewinder) >= 1;
        var coilCap = s.Level >= 88 ? 3 : 2;
        var ireSoon = o.Burst && o.SerpentsIre && s.Learned(Skills.SerpentsIre) && s.Cd(Skills.SerpentsIre) <= 3;
        var viceCapping = viceReady && s.Charges.GetValueOrDefault(Skills.Vicewinder) >= 1.9f;
        if (s.Coils >= coilCap && (viceReady || ireSoon))
            return Choose(s, Skills.UncoiledFury, "先消耗飞蛇之魂，避免蛇剑或蛇灵气溢出");

        // Haste first in the opener. Do not spend a slow Vicewinder before Swiftskin's Sting.
        if (!s.Has(Buffs.Swiftscaled)) return Basic(s, aoe);

        var ready = s.Has(Buffs.ReadyToReawaken);
        var doubleBurst = ready && s.Gauge >= 50;
        var buffNeed = doubleBurst ? o.DoubleBuffSeconds : o.SingleBuffSeconds;
        var buffsReady = s.Buff(Buffs.HuntersInstinct) >= buffNeed && s.Buff(Buffs.Swiftscaled) >= buffNeed;
        var preparingBurst = o.Burst && o.Reawaken && (ready || ireCooldown <= 12);

        // Spending gauge at 90 prevents the following 10-point finisher from wasting gauge.
        if (o.Burst && o.Reawaken && s.Learned(Skills.Reawaken) && (ready || s.Gauge >= 50) && buffsReady && s.SkillDistance <= 5)
        {
            var ireAge = s.Cd(Skills.SerpentsIre) > 0 ? 120 - s.Cd(Skills.SerpentsIre) : float.PositiveInfinity;
            var readyToBurst = ready && ireAge >= 2; // One ordinary GCD buffers Serpent's Ire.
            if (readyToBurst || s.Gauge >= 90 || s.RaidBuff || aoe ||
                (!ready && ireCooldown > o.BurstReserveSeconds))
                return Choose(s, Skills.Reawaken, doubleBurst ? "增益充足，启动双祖灵爆发" : "消耗祖灵资源／防止灵力溢出");
        }
        if (viceReady && (viceCapping || !s.Has(Buffs.HuntersInstinct) ||
                (preparingBurst && !buffsReady) || ireCooldown > 12))
            return Choose(s, vice, "使用充能并为后续祖灵续好猛袭与疾速");

        if (s.Coils > o.ReserveCoils && s.Learned(Skills.UncoiledFury) &&
            (s.Coils >= coilCap || aoe || (!preparingBurst && s.Cd(Skills.Vicewinder) < 5)))
            return Choose(s, Skills.UncoiledFury, "消耗多余飞蛇之魂，保留远离资源");
        return Basic(s, aoe);
    }

    public static Decision? Continuation(Snapshot s)
    {
        if (!s.Active || !s.HasTarget || !CombatRange.Contains(s.Distance)) return null;
        var tail = s.TailCombo switch { 1 => Skills.DeathRattle, 2 => Skills.LastLash,
            3 => Skills.FirstLegacy, 4 => Skills.SecondLegacy, 5 => Skills.ThirdLegacy,
            6 => Skills.FourthLegacy, _ => 0u };
        var hit = Choose(s, tail, "优先执行蛇尾／祖灵之蛇续剑");
        if (hit != null) return hit;
        // Enhancement buffs decide which bite comes first; both buttons can transform at once.
        foreach (var (buff, action) in new (uint, uint)[] {
            (Buffs.PoisedTwinfang, Skills.UncoiledTwinfang), (Buffs.PoisedTwinblood, Skills.UncoiledTwinblood),
            (Buffs.HuntersVenom, Skills.TwinfangBite), (Buffs.SwiftskinsVenom, Skills.TwinbloodBite),
            (Buffs.FellhuntersVenom, Skills.TwinfangThresh), (Buffs.FellskinsVenom, Skills.TwinbloodThresh) })
            if (s.Has(buff) && Choose(s, action, "按强化状态顺序执行双牙／飞蛇续剑") is { } decision) return decision;
        foreach (var slot in new[] { Skills.Twinfang, Skills.Twinblood })
        {
            var action = s.Adjust(slot);
            if (action != slot && action is >= Skills.TwinfangBite and <= Skills.UncoiledTwinblood &&
                Skills.IsOffGcd(action) && s.Highlighted.Contains(action) &&
                Choose(s, action, "执行游戏确认可用的双牙续剑") is { } decision) return decision;
        }
        return null;
    }

    public static Decision? OffGcd(Snapshot s, RotationOptions o)
    {
        if (!s.Active || !s.HasTarget || !CombatRange.Contains(s.Distance)) return null;
        if (s.GcdRemaining < o.WeaveSafetySeconds) return null;
        if (Continuation(s) is { } continuation) return continuation;
        var cap = s.Level >= 88 ? 3 : 2;
        if (o.Burst && o.SerpentsIre && ModePolicy.AllowsTargetBurst(o.Mode, s.TargetHpPercent) &&
            s.SkillDistance <= 20 && s.Coils < cap && !s.Has(Buffs.ReadyToReawaken) &&
            s.Tribute == 0 && !s.Has(Buffs.Reawakened) && s.Cd(Skills.SerpentsIre) <= 0.05f &&
            Choose(s, Skills.SerpentsIre, "蛇灵气按冷却使用，保留免费祖灵且不溢出蛇魂") is { } ire) return ire;

        if (o.AutoTrueNorth && !s.Has(Buffs.TrueNorth) && s.NeedsPositional &&
            s.Charges.GetValueOrDefault(Skills.TrueNorth) >= 1)
        {
            var next = Gcd(s, o)?.ActionId ?? 0;
            var needed = next is Skills.FlankstingStrike or Skills.FlanksbaneFang or Skills.HuntersCoil ? Facing.Flank
                : next is Skills.HindstingStrike or Skills.HindsbaneFang or Skills.SwiftskinsCoil ? Facing.Rear : Facing.None;
            // Use the actual recast progress so haste and short GCDs share the same percentage.
            if (needed != Facing.None && needed != s.Facing &&
                s.GcdProgressPercent >= Math.Clamp(o.TrueNorthGcdPercent, 0, 100))
                return Choose(s, Skills.TrueNorth, "GCD进度达到设定百分比，下一个战技身位不符，使用真北");
        }
        if (o.Healing && s.HpPercent <= 45 && s.Cd(Skills.SecondWind) <= 0.05f &&
            Choose(s, Skills.SecondWind, "低血量内丹") is { } heal) return heal;
        if (o.Healing && s.HpPercent <= 70 && s.Cd(Skills.Bloodbath) <= 0.05f &&
            Choose(s, Skills.Bloodbath, "低血量浴血") is { } bloodbath) return bloodbath;
        return null;
    }

    public static uint PreferredCoil(Snapshot s)
    {
        if (s.NeedsPositional && s.Facing == Facing.Flank) return Skills.HuntersCoil;
        if (s.NeedsPositional && s.Facing == Facing.Rear) return Skills.SwiftskinsCoil;
        if (!s.Has(Buffs.HuntersInstinct)) return Skills.HuntersCoil;
        return s.Buff(Buffs.HuntersInstinct) < s.Buff(Buffs.Swiftscaled)
            ? Skills.HuntersCoil : Skills.SwiftskinsCoil;
    }

    private static Decision? DailyGcd(Snapshot s, RotationOptions o, bool aoe)
    {
        var vice = aoe ? Skills.Vicepit : Skills.Vicewinder;
        var viceReady = o.Cooldowns && s.Learned(vice) && s.Charges.GetValueOrDefault(Skills.Vicewinder) >= 1;
        var allowBurst = ModePolicy.AllowsTargetBurst(o.Mode, s.TargetHpPercent);
        var ireReady = allowBurst && o.Burst && o.SerpentsIre && s.Learned(Skills.SerpentsIre) && s.Cd(Skills.SerpentsIre) <= 0.05f;
        var cap = s.Level >= 88 ? 3 : 2;
        if (s.Coils >= cap && (viceReady || ireReady) &&
            Choose(s, Skills.UncoiledFury, "日随：先消耗飞蛇之魂，避免资源溢出") is { } overflow) return overflow;
        if (!s.Has(Buffs.Swiftscaled)) return Basic(s, aoe);

        if (viceReady && Choose(s, vice, "日随：强碎灵蛇／强碎灵蟒充能就绪即用") is { } coil) return coil;
        if (s.Coils > 0 && Choose(s, Skills.UncoiledFury, "日随：消耗可用飞蛇之魂，不保留远离资源") is { } fury) return fury;
        return Basic(s, aoe);
    }

    private static bool UseAoe(Snapshot s, RotationOptions o) => o.Aoe && s.NearbyEnemies >= o.AoeThreshold && s.Learned(Skills.SteelMaw);
    private static Decision? Ranged(Snapshot s, RotationOptions o)
    {
        if (!o.Ranged || s.SkillDistance > 20) return null;
        if (s.Coils > 0 && Choose(s, Skills.UncoiledFury, "远离时用飞蛇之尾止损") is { } fury) return fury;
        return Choose(s, Skills.WrithingSnap, "飞蛇之尾不可用，使用飞蛇之牙止损");
    }

    private static Decision? Basic(Snapshot s, bool aoe)
    {
        var left = s.Adjust(aoe ? Skills.SteelMaw : Skills.SteelFangs);
        var right = s.Adjust(aoe ? Skills.ReavingMaw : Skills.ReavingFangs);
        if (s.ComboRemaining > 0)
        {
            var candidates = new[] { left, right };
            foreach (var (buff, action) in new (uint, uint)[] { (Buffs.Flanksting, Skills.FlankstingStrike),
                (Buffs.Flanksbane, Skills.FlanksbaneFang), (Buffs.Hindsting, Skills.HindstingStrike),
                (Buffs.Hindsbane, Skills.HindsbaneFang), (Buffs.Jagged, Skills.JaggedMaw), (Buffs.Bloodied, Skills.BloodiedMaw) })
                if (s.Has(buff) && candidates.Contains(action)) return Choose(s, action, "使用强化后的齿剑／牙剑终结技");
            var hunter = aoe ? Skills.HuntersBite : Skills.HuntersSting;
            var swift = aoe ? Skills.SwiftskinsBite : Skills.SwiftskinsSting;
            if (candidates.Contains(hunter) || candidates.Contains(swift))
            {
                // Finisher buffs guide the second hit unless upkeep is about to expire.
                var preferSwift = s.Buff(Buffs.Swiftscaled) <= s.Buff(Buffs.HuntersInstinct);
                if (s.Buff(Buffs.HuntersInstinct) > 5 && s.Buff(Buffs.Swiftscaled) > 5 && !aoe)
                {
                    if (s.Has(Buffs.Flanksting) || s.Has(Buffs.Flanksbane)) preferSwift = false;
                    if (s.Has(Buffs.Hindsting) || s.Has(Buffs.Hindsbane)) preferSwift = true;
                }
                return Choose(s, preferSwift && s.Learned(swift) ? swift : hunter, "续较短的增益并保持强化终结技顺序");
            }
            foreach (var action in candidates)
                if (s.Highlighted.Contains(action) && s.CanUse(action)) return new(action, "跟随游戏高亮完成连击");
            if (candidates.Any(x => x is >= Skills.FlankstingStrike and <= Skills.HindsbaneFang or Skills.JaggedMaw or Skills.BloodiedMaw))
                return Choose(s, left, "无强化状态时完成当前连击");
        }
        var opener = s.Has(Buffs.HonedSteel) ? (aoe ? Skills.SteelMaw : Skills.SteelFangs)
            : (aoe ? Skills.ReavingMaw : Skills.ReavingFangs);
        if (!s.Learned(opener)) opener = aoe ? Skills.SteelMaw : Skills.SteelFangs;
        return Choose(s, opener, "交替使用咬噬与穿裂，建立基础连击");
    }

    private static Decision? Choose(Snapshot s, uint action, string reason) => action != 0 && s.CanUse(action) ? new(action, reason) : null;
}
