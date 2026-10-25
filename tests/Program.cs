using ViperACR.Core;

var checks = 0;
var failures = new List<string>();
var options = new RotationOptions();
var daily = options with { Mode = CombatMode.Daily };
Snapshot State() => new() {
    Active = true, HasTarget = true, TargetId = 123, Distance = 2, Level = 100,
    BuffTimes = new() { [Buffs.HuntersInstinct] = 35, [Buffs.Swiftscaled] = 35 },
    Cooldowns = new() { [Skills.SerpentsIre] = 60, [Skills.Vicewinder] = 20 },
    Charges = new() { [Skills.Vicewinder] = 0, [Skills.TrueNorth] = 2 }
};
void Check(string name, uint? expected, Decision? actual)
{
    checks++;
    if (expected != actual?.ActionId) failures.Add($"{name}: expected={expected}, actual={actual?.ActionId} ({actual?.Reason})");
}
Decision? G(Snapshot s, RotationOptions? o = null) => ViperEngine.Gcd(s, o ?? options);
Decision? O(Snapshot s, RotationOptions? o = null) => ViperEngine.OffGcd(s, o ?? options);
Snapshot Buff(Snapshot s, uint id, float duration = 35)
{
    var buffs = new Dictionary<uint, float>(s.BuffTimes) { [id] = duration };
    return s with { BuffTimes = buffs };
}
Check("脱战不攻击", null, G(State() with { Active = false }));
Check("无目标不攻击", null, G(State() with { HasTarget = false }));
Check("超出6米不攻击", null, G(State() with { Distance = 21, Coils = 3 }));
Check("远离优先飞蛇", Skills.UncoiledFury, G(State() with { Distance = 6, Coils = 1 }));
Check("远离无资源飞蛇之牙", Skills.WrithingSnap, G(State() with { Distance = 6 }));
Check("有蛇魂但飞蛇之尾未解锁则飞蛇之牙", Skills.WrithingSnap, G(State() with { Distance = 6, Coils = 1, Level = 80 }));
Check("飞蛇之尾锁定时飞蛇之牙", Skills.WrithingSnap, G(State() with { Distance = 6, Coils = 1, Locked = [Skills.UncoiledFury] }));
Check("6米边界无蛇魂飞蛇之牙", Skills.WrithingSnap, G(State() with { Distance = 6 }));
Check("祖灵中无蛇魂远程飞蛇之牙", Skills.WrithingSnap, G(State() with { Distance = 5.5f, Tribute = 3 }));
Check("蛇剑中无蛇魂远程飞蛇之牙", Skills.WrithingSnap, G(State() with { Distance = 5.5f, DreadCombo = 2 }));
Check("可关闭远程止损", null, G(State() with { Distance = 6, Coils = 1 }, options with { Ranged = false }));
Check("起手穿裂", Skills.ReavingFangs, G(State() with { BuffTimes = [] }));
var secondHit = State() with { ComboRemaining = 20,
    Adjusted = new() { [Skills.SteelFangs] = Skills.HuntersSting, [Skills.ReavingFangs] = Skills.SwiftskinsSting } };
Check("起手第二GCD先疾速", Skills.SwiftskinsSting, G(secondHit with { BuffTimes = [] }));
Check("强化咬噬", Skills.SteelFangs, G(Buff(State(), Buffs.HonedSteel)));
Check("强化穿裂", Skills.ReavingFangs, G(Buff(State(), Buffs.HonedReavers)));
foreach (var (buff, action) in new (uint, uint)[] { (Buffs.Flanksting, Skills.FlankstingStrike),
    (Buffs.Flanksbane, Skills.FlanksbaneFang), (Buffs.Hindsting, Skills.HindstingStrike), (Buffs.Hindsbane, Skills.HindsbaneFang) })
{
    var other = action is Skills.FlankstingStrike or Skills.FlanksbaneFang ? Skills.FlanksbaneFang : Skills.HindsbaneFang;
    Check($"强化终结 {action}", action, G(Buff(State() with { ComboRemaining = 20,
        Adjusted = new() { [Skills.SteelFangs] = other, [Skills.ReavingFangs] = action } }, buff)));
}
Check("后续背击强化决定第二段", Skills.SwiftskinsSting, G(Buff(secondHit, Buffs.Hindsting)));
Check("后续侧击强化决定第二段", Skills.HuntersSting, G(Buff(secondHit, Buffs.Flanksting)));
Check("增益快断优先续增益", Skills.HuntersSting, G(Buff(Buff(secondHit, Buffs.Hindsting), Buffs.HuntersInstinct, 2)));
Check("连击快到期不被第二套蛇剑拖断", Skills.SwiftskinsSting, G(secondHit with { ComboRemaining = 5,
    Charges = new() { [Skills.Vicewinder] = 2 } }));
Check("连击快到期不被飞蛇拖断", Skills.SwiftskinsSting, G(secondHit with { ComboRemaining = 3, Coils = 3 }));
Check("双祖灵前先完成即将到期的连击", Skills.SwiftskinsSting, G(Buff(secondHit with {
    ComboRemaining = 20, Gauge = 50 }, Buffs.ReadyToReawaken)));
Check("普通连击剩余充足可被单祖灵保留", Skills.Reawaken, G(secondHit with { ComboRemaining = 25, Gauge = 50 }));
Check("三目标AOE", Skills.ReavingMaw, G(State() with { NearbyEnemies = 3 }));
Check("两目标默认单体", Skills.ReavingFangs, G(State() with { NearbyEnemies = 2 }));
Check("关闭AOE", Skills.ReavingFangs, G(State() with { NearbyEnemies = 4 }, options with { Aoe = false }));
Check("三目标5米AOE", Skills.ReavingMaw, G(State() with { NearbyEnemies = 3, Distance = 4 }));
for (var combo = 1; combo <= 6; combo++)
{
    uint expected = combo switch { 1 => Skills.SwiftskinsCoil, 2 => Skills.SwiftskinsCoil, 3 => Skills.HuntersCoil,
        4 => Skills.SwiftskinsDen, 5 => Skills.SwiftskinsDen, _ => Skills.HuntersDen };
    Check($"蛇剑状态 {combo} 不被祖灵打断", expected, G(Buff(State() with { DreadCombo = combo, Gauge = 100 }, Buffs.ReadyToReawaken)));
}
Check("蛇剑中途目标变多仍完成单体", Skills.SwiftskinsCoil, G(State() with { DreadCombo = 2, NearbyEnemies = 5 }));
Check("蟒剑中途目标变少仍完成AOE", Skills.HuntersDen, G(State() with { DreadCombo = 6, NearbyEnemies = 1 }));
var coilsByPosition = State() with { DreadCombo = 1, NeedsPositional = true };
Check("侧面优先猛袭盘蛇", Skills.HuntersCoil, G(coilsByPosition with { Facing = Facing.Flank }));
Check("背面优先疾速盘蛇", Skills.SwiftskinsCoil, G(coilsByPosition with { Facing = Facing.Rear }));
Check("背面无猛袭仍先疾速", Skills.SwiftskinsCoil, G(coilsByPosition with { Facing = Facing.Rear, BuffTimes = [] }));
Check("侧面优先级高于疾速续期", Skills.HuntersCoil, G(Buff(coilsByPosition with { Facing = Facing.Flank }, Buffs.Swiftscaled, 1)));
Check("背面优先级高于猛袭续期", Skills.SwiftskinsCoil, G(Buff(coilsByPosition with { Facing = Facing.Rear }, Buffs.HuntersInstinct, 1)));
foreach (var facing in Enum.GetValues<Facing>())
{
    Check($"猛袭后换位{facing}仍接疾速", Skills.SwiftskinsCoil, G(coilsByPosition with { DreadCombo = 2, Facing = facing }));
    Check($"疾速后换位{facing}仍接猛袭", Skills.HuntersCoil, G(coilsByPosition with { DreadCombo = 3, Facing = facing }));
}
Check("正面按较短猛袭增益决定", Skills.HuntersCoil, G(Buff(coilsByPosition with { Facing = Facing.Front }, Buffs.HuntersInstinct, 10)));
Check("正面无增益先猛袭", Skills.HuntersCoil, G(coilsByPosition with { Facing = Facing.Front, BuffTimes = [] }));
Check("无身位要求不强制侧击", Skills.SwiftskinsCoil, G(coilsByPosition with { NeedsPositional = false, Facing = Facing.Flank }));
Check("AOE盘蛇不应用单体身位", Skills.SwiftskinsDen, G(coilsByPosition with { DreadCombo = 4, Facing = Facing.Flank }));
for (var level = 90; level <= 100; level++)
{
    var max = level >= 96 ? 5 : 4;
    for (var tribute = max; tribute >= 1; tribute--)
    {
        var expected = Skills.FirstGeneration + (uint)(max - tribute);
        Check($"{level}级祖灵档数 {tribute}", expected, G(State() with { Level = level, Tribute = tribute }));
    }
}
Check("祖灵过渡包不误打第一式", null, G(Buff(State(), Buffs.Reawakened)));
Check("祖灵中远离可飞蛇", Skills.UncoiledFury, G(State() with { Tribute = 3, Coils = 1, Distance = 5.5f }));
Check("灵力90提前消耗", Skills.Reawaken, G(State() with { Gauge = 90 }));
Check("爆发前保留50灵力", Skills.ReavingFangs, G(State() with { Gauge = 50, Cooldowns = new() { [Skills.SerpentsIre] = 15 } }));
Check("非保留窗口消耗50灵力", Skills.Reawaken, G(State() with { Gauge = 50 }));
Check("关闭蛇剑冷却仍可消耗灵力", Skills.Reawaken, G(State() with { Gauge = 50 }, options with { Cooldowns = false }));
Check("关闭爆发不祖灵", Skills.ReavingFangs, G(State() with { Gauge = 100 }, options with { Burst = false }));
Check("增益不足不祖灵", Skills.ReavingFangs, G(Buff(State() with { Gauge = 100 }, Buffs.HuntersInstinct, 4)));
Check("免费祖灵优先于双祖灵增益储备", Skills.Reawaken, G(Buff(Buff(State() with { Gauge = 50 }, Buffs.ReadyToReawaken), Buffs.HuntersInstinct, 20)));
Check("双祖灵准备充分", Skills.Reawaken, G(Buff(State() with { Gauge = 50 }, Buffs.ReadyToReawaken)));
Check("蛇灵气刚用有预备优先祖灵", Skills.Reawaken, G(Buff(State() with { Cooldowns = new() { [Skills.SerpentsIre] = 119.5f } }, Buffs.ReadyToReawaken)));
Check("蛇魂封顶先消耗", Skills.UncoiledFury, G(State() with { Coils = 3, Charges = new() { [Skills.Vicewinder] = 2 } }));
Check("未开启蛇剑不凭空蛇剑", Skills.ReavingFangs, G(State()));
Check("蛇剑充能可用", Skills.Vicewinder, G(State() with { Charges = new() { [Skills.Vicewinder] = 1 } }));
Check("蛇灵气可用", Skills.SerpentsIre, O(State() with { Cooldowns = new() { [Skills.SerpentsIre] = 0 } }));
Check("蛇魂封顶不蛇灵气", null, O(State() with { Coils = 3, Cooldowns = new() { [Skills.SerpentsIre] = 0 } }));
Check("免费祖灵未花不覆盖", null, O(Buff(State() with { Cooldowns = new() { [Skills.SerpentsIre] = 0 } }, Buffs.ReadyToReawaken)));
Check("祖灵中蛇灵气就绪允许安全插入", Skills.SerpentsIre, O(State() with { Tribute = 2, Cooldowns = new() { [Skills.SerpentsIre] = 0 } }));
foreach (var mode in Enum.GetValues<CombatMode>())
{
    var ireDuringReawaken = Buff(State() with { Tribute = 5, Gauge = 40, Coils = 2,
        GcdElapsed = 0.65f, GcdRemaining = 1.22f,
        Cooldowns = new() { [Skills.SerpentsIre] = 0 } }, Buffs.Reawakened);
    var modeOptions = options with { Mode = mode };
    Check($"{mode}日志回归：祖灵内冷却转好不等大蛇牙", Skills.SerpentsIre, O(ireDuringReawaken, modeOptions));
    Check($"{mode}祖灵状态过渡包不额外推迟蛇灵气", Skills.SerpentsIre,
        O(ireDuringReawaken with { Tribute = 0 }, modeOptions));
    Check($"{mode}祖灵内续剑仍优先", Skills.FirstLegacy,
        O(ireDuringReawaken with { TailCombo = 3 }, modeOptions));
    Check($"{mode}祖灵内没有安全窗口仍不强插", null,
        O(ireDuringReawaken with { GcdRemaining = 0.5f }, modeOptions));
    Check($"{mode}祖灵内满魂仍不溢出", null,
        O(ireDuringReawaken with { Coils = 3 }, modeOptions));
    Check($"{mode}祖灵内不覆盖未花的免费祖灵", null,
        O(Buff(ireDuringReawaken, Buffs.ReadyToReawaken), modeOptions));
    Check($"{mode}祖灵内游戏不可用仍等待", null,
        O(ireDuringReawaken with { UnusableOffGcd = [Skills.SerpentsIre] }, modeOptions));
    Check($"{mode}祖灵内冷却未转好仍等待", null,
        O(ireDuringReawaken with { Cooldowns = new() { [Skills.SerpentsIre] = 0.2f } }, modeOptions));
    Check($"{mode}祖灵内蛇灵气QT关闭仍等待", null,
        O(ireDuringReawaken, modeOptions with { SerpentsIre = false }));
    Check($"{mode}祖灵内爆发QT关闭仍等待", null,
        O(ireDuringReawaken, modeOptions with { Burst = false }));
    for (var tribute = 5; tribute >= 1; tribute--)
    {
        var phase = ireDuringReawaken with { Tribute = tribute };
        Check($"{mode}祖灵档{tribute}不阻挡蛇灵气", Skills.SerpentsIre, O(phase, modeOptions));
        Check($"{mode}蛇灵气插入保持祖灵档{tribute}下一战技",
            Skills.FirstGeneration + (uint)(5 - tribute), G(phase, modeOptions));
    }
    for (var tail = 3; tail <= 6; tail++)
        Check($"{mode}祖灵续剑{tail}优先于就绪蛇灵气",
            Skills.FirstLegacy + (uint)(tail - 3), O(ireDuringReawaken with { TailCombo = tail }, modeOptions));
}
Check("祖灵QT关闭不启动祖灵", Skills.ReavingFangs, G(State() with { Gauge = 100 }, options with { Reawaken = false }));
Check("祖灵QT关闭不消费免费祖灵", Skills.ReavingFangs, G(Buff(State(), Buffs.ReadyToReawaken), options with { Reawaken = false }));
Check("祖灵QT关闭仍完成已开祖灵", Skills.ThirdGeneration, G(State() with { Tribute = 3 }, options with { Reawaken = false }));
Check("蛇灵气QT关闭不蛇灵气", null, O(State() with { Cooldowns = new() { [Skills.SerpentsIre] = 0 } }, options with { SerpentsIre = false }));
Check("强碎灵蛇QT关闭仍可蛇灵气", Skills.SerpentsIre, O(State() with { Cooldowns = new() { [Skills.SerpentsIre] = 0 } }, options with { Cooldowns = false }));
Check("祖灵QT关闭不连带禁蛇灵气", Skills.SerpentsIre, O(State() with { Cooldowns = new() { [Skills.SerpentsIre] = 0 } }, options with { Reawaken = false }));
Check("蛇灵气QT关闭仍消耗祖灵资源", Skills.Reawaken, G(State() with { Gauge = 50, Cooldowns = new() { [Skills.SerpentsIre] = 0 } }, options with { SerpentsIre = false }));
Check("关闭蛇灵气不再为它卡强碎灵蛇", Skills.Vicewinder, G(State() with { Charges = new() { [Skills.Vicewinder] = 1 }, Cooldowns = new() { [Skills.SerpentsIre] = 0 } }, options with { SerpentsIre = false }));
Check("爆发总开关仍限制蛇灵气", null, O(State() with { Cooldowns = new() { [Skills.SerpentsIre] = 0 } }, options with { Burst = false }));
Check("关闭两个新QT仍完成双牙", Skills.TwinfangBite, O(Buff(State(), Buffs.HuntersVenom), options with { Reawaken = false, SerpentsIre = false }));
Check("关闭两个新QT仍完成祖灵之蛇", Skills.FirstLegacy, O(State() with { TailCombo = 3 }, options with { Reawaken = false, SerpentsIre = false }));
Check("关闭两个新QT仍按身位接蛇剑", Skills.HuntersCoil, G(coilsByPosition with { Facing = Facing.Flank }, options with { Reawaken = false, SerpentsIre = false }));
var burstSoon = State() with { Gauge = 50, Cooldowns = new() { [Skills.SerpentsIre] = 10 } };
Check("日随50灵力不等蛇灵气", Skills.Reawaken, G(burstSoon, daily));
Check("高难50灵力为爆发保留", Skills.ReavingFangs, G(burstSoon, options));
Check("日随免费祖灵不强求双祖灵增益", Skills.Reawaken,
    G(Buff(Buff(State() with { Gauge = 50 }, Buffs.ReadyToReawaken), Buffs.HuntersInstinct, 18), daily));
Check("高难免费祖灵不等待双祖灵增益", Skills.Reawaken,
    G(Buff(Buff(State() with { Gauge = 50 }, Buffs.ReadyToReawaken), Buffs.HuntersInstinct, 18), options));
var oneCoil = State() with { Coils = 1 };
Check("日随最后一层蛇魂也使用", Skills.UncoiledFury, G(oneCoil, daily));
Check("高难保留一层远离蛇魂", Skills.ReavingFangs, G(oneCoil, options));
Check("日随无视保留蛇魂滑杆", Skills.UncoiledFury, G(oneCoil, daily with { ReserveCoils = 2 }));
Check("日随强碎灵蛇不等蛇灵气窗口", Skills.Vicewinder,
    G(State() with { Charges = new() { [Skills.Vicewinder] = 1 }, Cooldowns = new() { [Skills.SerpentsIre] = 10 } }, daily));
Check("高难窗口内保留强碎灵蛇", Skills.ReavingFangs,
    G(State() with { Charges = new() { [Skills.Vicewinder] = 1 }, Cooldowns = new() { [Skills.SerpentsIre] = 10 } }, options));
Check("日随满蛇魂先防溢出", Skills.UncoiledFury,
    G(State() with { Coils = 3, Charges = new() { [Skills.Vicewinder] = 1 } }, daily));
Check("日随关闭祖灵仍可飞蛇", Skills.UncoiledFury, G(oneCoil with { Gauge = 100 }, daily with { Reawaken = false }));
Check("日随关闭爆发不祖灵", Skills.ReavingFangs, G(State() with { Gauge = 100 }, daily with { Burst = false }));
Check("日随关闭蛇剑QT不主动蛇剑", Skills.ReavingFangs,
    G(State() with { Charges = new() { [Skills.Vicewinder] = 2 } }, daily with { Cooldowns = false }));
Check("日随蛇灵气照常就绪即用", Skills.SerpentsIre, O(State() with { Cooldowns = new() { [Skills.SerpentsIre] = 0 } }, daily));
Check("日随关闭蛇灵气不使用", null, O(State() with { Cooldowns = new() { [Skills.SerpentsIre] = 0 } }, daily with { SerpentsIre = false }));
Check("日随优先打完双牙", Skills.TwinfangBite,
    O(Buff(State() with { Cooldowns = new() { [Skills.SerpentsIre] = 0 } }, Buffs.HuntersVenom), daily));
Check("日随祖灵优先于建立增益", Skills.Reawaken, G(oneCoil with { Gauge = 100, BuffTimes = [] }, daily));
Check("日随祖灵中途继续量谱", Skills.ThirdGeneration, G(State() with { Tribute = 3 }, daily));
Check("日随祖灵等待蛇剑完成", Skills.HuntersCoil, G(State() with { DreadCombo = 3, Gauge = 100 }, daily));
Check("日随远离无魂仍飞蛇之牙", Skills.WrithingSnap, G(State() with { Distance = 6 }, daily));
Check("日随三目标祖灵", Skills.Reawaken, G(State() with { NearbyEnemies = 3, Gauge = 50 }, daily));
Check("日随三目标强碎灵蟒", Skills.Vicepit,
    G(State() with { NearbyEnemies = 3, Charges = new() { [Skills.Vicewinder] = 1 } }, daily));
Check("日随祖灵等待基础连击完成", Skills.SwiftskinsSting,
    G(secondHit with { ComboRemaining = 4, Gauge = 50, Coils = 1 }, daily));
Check("日随不误用未解锁祖灵", Skills.ReavingFangs, G(State() with { Level = 80, Gauge = 50 }, daily));
foreach (var combo in Enumerable.Range(1, 6))
{
    var pending = State() with { DreadCombo = combo, Gauge = 50, Coils = 3,
        Charges = new() { [Skills.Vicewinder] = 2 }, BuffTimes = [] };
    Assert($"日随祖灵不打断蛇剑蟒剑{combo}", G(pending, daily)?.ActionId != Skills.Reawaken);
    Assert($"日随祖灵QT关闭保留蛇剑蟒剑{combo}", G(pending, daily with { Reawaken = false })?.ActionId != Skills.Reawaken);
    Assert($"日随残血保留蛇剑蟒剑{combo}", G(pending with { TargetHpPercent = 19 }, daily)?.ActionId != Skills.Reawaken);
}
Check("日随免费祖灵无需增益优先满蛇魂", Skills.Reawaken,
    G(Buff(State() with { Coils = 3, Gauge = 0, BuffTimes = [], Charges = new() { [Skills.Vicewinder] = 2 } }, Buffs.ReadyToReawaken), daily));
Check("日随祖灵不可用回退蛇魂防溢出", Skills.UncoiledFury,
    G(State() with { Gauge = 50, Coils = 3, Locked = [Skills.Reawaken], Charges = new() { [Skills.Vicewinder] = 2 } }, daily));
Check("日随连招时间充足仍先完成第二段", Skills.SwiftskinsSting,
    G(secondHit with { ComboRemaining = 29, Gauge = 50 }, daily));
Check("日随连招完毕祖灵优先满魂和蛇剑", Skills.Reawaken,
    G(State() with { Gauge = 50, Coils = 3, Charges = new() { [Skills.Vicewinder] = 2 } }, daily));
Check("日随已过期连招不再阻挡祖灵", Skills.Reawaken,
    G(secondHit with { ComboRemaining = 0, Gauge = 50 }, daily));
Check("常规插入不压GCD", null, O(State() with { GcdRemaining = 0.6f, TailCombo = 1 }));
Check("过期窗口仍可补救续剑", Skills.DeathRattle, ViperEngine.Continuation(State() with { GcdRemaining = 0, TailCombo = 1 }));
for (var tail = 1; tail <= 6; tail++)
    Check($"蛇尾／祖灵之蛇续剑 {tail}", tail <= 2 ? Skills.DeathRattle + (uint)(tail - 1) : Skills.FirstLegacy + (uint)(tail - 3), O(State() with { TailCombo = tail }));
foreach (var (buff, action) in new (uint, uint)[] {
    (Buffs.HuntersVenom, Skills.TwinfangBite), (Buffs.SwiftskinsVenom, Skills.TwinbloodBite),
    (Buffs.FellhuntersVenom, Skills.TwinfangThresh), (Buffs.FellskinsVenom, Skills.TwinbloodThresh),
    (Buffs.PoisedTwinfang, Skills.UncoiledTwinfang), (Buffs.PoisedTwinblood, Skills.UncoiledTwinblood) })
    Check($"双牙强化顺序 {action}", action, O(Buff(State(), buff)));
Check("已用续剑不重复发", null, O(State() with { TailCombo = 1, RecentlyUsed = [Skills.DeathRattle] }));
Check("服务器判定不可用续剑不发", null, O(State() with { TailCombo = 1, UnusableOffGcd = [Skills.DeathRattle] }));
Check("续剑优先于蛇灵气", Skills.DeathRattle, O(State() with { TailCombo = 1, Cooldowns = new() { [Skills.SerpentsIre] = 0 } }));
var flank = Buff(secondHit with { Adjusted = new() { [Skills.SteelFangs] = Skills.FlankstingStrike },
    NeedsPositional = true, Facing = Facing.Rear, GcdRemaining = 1, GcdElapsed = 1.5f }, Buffs.Flanksting);
Check("身位不符真北", Skills.TrueNorth, O(flank));
Check("身位正确不真北", null, O(flank with { Facing = Facing.Flank }));
Check("无身位要求不真北", null, O(flank with { NeedsPositional = false }));
Check("已有真北不重复", null, O(Buff(flank, Buffs.TrueNorth)));
Check("可关闭自动真北", null, O(flank, options with { AutoTrueNorth = false }));
foreach (var mode in new[] { CombatMode.Daily, CombatMode.HighEnd })
{
    var timingOptions = options with { Mode = mode, TrueNorthGcdPercent = 50 };
    Check($"{mode}真北50%之前等待", null,
        O(flank with { GcdElapsed = 1.24f, GcdRemaining = 1.26f }, timingOptions));
    Check($"{mode}真北50%边界使用", Skills.TrueNorth,
        O(flank with { GcdElapsed = 1.25f, GcdRemaining = 1.25f }, timingOptions));
    Check($"{mode}短GCD按比例等待", null,
        O(flank with { GcdElapsed = 0.8f, GcdRemaining = 0.9f }, timingOptions));
    Check($"{mode}短GCD50%可使用", Skills.TrueNorth,
        O(flank with { GcdElapsed = 0.85f, GcdRemaining = 0.85f }, timingOptions));
    Check($"{mode}可调为20%提前使用", Skills.TrueNorth,
        O(flank with { GcdElapsed = 0.5f, GcdRemaining = 2 }, timingOptions with { TrueNorthGcdPercent = 20 }));
    Check($"{mode}60%仍未达到80%", null,
        O(flank, timingOptions with { TrueNorthGcdPercent = 80 }));
    Check($"{mode}80%不能绕过安全插入余量", null,
        O(flank with { GcdElapsed = 2, GcdRemaining = 0.5f }, timingOptions with { TrueNorthGcdPercent = 80 }));
    Check($"{mode}真北无充能", null,
        O(flank with { Charges = new() { [Skills.TrueNorth] = 0 } }, timingOptions));
    Check($"{mode}0%仍需实际插入窗口", Skills.TrueNorth,
        O(flank with { GcdElapsed = 0, GcdRemaining = 2.5f }, timingOptions with { TrueNorthGcdPercent = 0 }));
    Check($"{mode}100%安全窗口内不触发", null,
        O(flank, timingOptions with { TrueNorthGcdPercent = 100 }));
    foreach (var invalidElapsed in new[] { -1f, float.NaN, float.PositiveInfinity })
        Check($"{mode}无效GCD计时{invalidElapsed}不真北", null,
            O(flank with { GcdElapsed = invalidElapsed }, timingOptions with { TrueNorthGcdPercent = 0 }));
}
Check("默认不治疗", null, O(State() with { HpPercent = 20 }));
Check("低血内丹", Skills.SecondWind, O(State() with { HpPercent = 20 }, options with { Healing = true }));
Check("内丹冷却转浴血", Skills.Bloodbath, O(State() with { HpPercent = 20,
    Cooldowns = new() { [Skills.SecondWind] = 40, [Skills.SerpentsIre] = 50 } }, options with { Healing = true }));
// Cross-product checks catch invalid action families, levels, ranges and gauge consumption.
foreach (var mode in new[] { CombatMode.Daily, CombatMode.HighEnd })
foreach (var level in new[] { 1, 10, 30, 50, 65, 80, 82, 86, 90, 92, 96, 100 })
foreach (var distance in new[] { 2f, 4f, 15f, 25f })
foreach (var gauge in new[] { 0, 40, 50, 90, 100 })
foreach (var coils in new[] { 0, 1, 3 })
foreach (var enemies in new[] { 1, 3 })
{
    var s = State() with { Level = level, Distance = distance, Gauge = gauge, Coils = coils, NearbyEnemies = enemies };
    var action = G(s, options with { Mode = mode });
    checks++;
    if (action is { } a && (!s.Learned(a.ActionId) || !s.CanReach(a.ActionId) ||
        a.ActionId == Skills.Reawaken && gauge < 50 || a.ActionId == Skills.UncoiledFury && coils == 0))
        failures.Add($"无效动作：mode={mode},level={level},distance={distance},gauge={gauge},coils={coils},action={a.ActionId}");
}
// Countdown sequences cover cancellation, missed/late frames, disabled gates and repeated ticks.
var pull = new CountdownPull();
void Pull(string name, float remaining, bool eligible, bool expected)
{
    checks++;
    var actual = pull.TrySubmit(remaining, eligible);
    if (actual != expected) failures.Add($"{name}: expected={expected}, actual={actual}");
}
Pull("无倒计时", 0, true, false);
Pull("倒计时十秒等待", 10, true, false);
Pull("倒计时500ms前等待", 0.501f, true, false);
Pull("倒计时500ms蛇行", 0.5f, true, true);
Pull("相同帧不重复蛇行", 0.5f, true, false);
Pull("最后100ms不重复蛇行", 0.1f, true, false);
Pull("倒计时结束", 0, true, false);
Pull("新的短倒计时可以蛇行", 0.4f, true, true);
Pull("小幅计时抖动不重复", 0.45f, true, false);
Pull("取消倒计时", -1, true, false);
Pull("新倒计时条件未满足", 0.5f, false, false);
Pull("最后半秒条件恢复可蛇行", 0.4f, true, true);
Pull("切换目标不会再蛇行", 0.3f, true, false);
Pull("直接重新开启倒计时", 10, true, false);
Pull("重开后允许蛇行", 0.4f, true, true);
Pull("倒计时异常值", float.NaN, true, false);
Pull("无穷倒计时", float.PositiveInfinity, true, false);
Pull("异常恢复后新倒计时", 0.5f, true, true);
pull.Reset();
Pull("切区重置后等待", 5, true, false);
Pull("关闭ACR不蛇行", 0.5f, false, false);
Pull("始终无可用目标不蛇行", 0.1f, false, false);
Pull("跳帧跳过整个窗口不补发", 0, true, false);
pull.Reset();
Pull("第一次读到最后半秒可以蛇行", 0.4f, true, true);
Pull("蛇行后关闭开关", 0.3f, false, false);
Pull("蛇行后再开开关仍不重复", 0.2f, true, false);
void Assert(string name, bool condition) { checks++; if (!condition) failures.Add(name); }
uint[] standardActions = [34620,34647,0,34622,34637,34636,34621,34636,34637,
    34620,34621,34636,34637,34622,34637,34636,34626,34627,34640,34628,34641,
    34629,34642,34630,34643,34631,34633,34644,34645,34633,34644,34645,34607,34609,34613,34634];
Assert("标准起手36步序列逐项一致", StandardOpener.Steps.Select(s => s.ActionId).SequenceEqual(standardActions));
Assert("日随忽略标准选择直接动态循环", !ModePolicy.UsesStandardOpener(CombatMode.Daily, true));
Assert("日随动态选择直接动态循环", !ModePolicy.UsesStandardOpener(CombatMode.Daily, false));
Assert("高难标准选择标准起手", ModePolicy.UsesStandardOpener(CombatMode.HighEnd, true));
Assert("高难动态选择不提交标准起手", !ModePolicy.UsesStandardOpener(CombatMode.HighEnd, false));
Assert("长臂猿增加3米", CombatRange.Bonus == 3);
foreach (var arms in new[] { false, true })
{
    Assert($"蛇行关闭无限距离{arms}原生边界", CombatRange.CanDash(arms ? 23 : 20, arms, false));
    Assert($"蛇行关闭无限距离{arms}拒绝超距", !CombatRange.CanDash(arms ? 23.01f : 20.01f, arms, false));
    Assert($"蛇行无限距离{arms}允许远目标预判", CombatRange.CanDash(1000, arms, true));
    Assert($"蛇行无限距离{arms}拒绝NaN", !CombatRange.CanDash(float.NaN, arms, true));
    Assert($"蛇行无限距离{arms}拒绝Infinity", !CombatRange.CanDash(float.PositiveInfinity, arms, true));
    Assert($"蛇行无限距离{arms}拒绝负距离", !CombatRange.CanDash(-1, arms, true));
}
foreach (var longArms in new[] { false, true })
foreach (var mode in new[] { CombatMode.Daily, CombatMode.HighEnd })
{
    var rangeOptions = options with { Mode = mode };
    var edge = longArms ? 6f : 3f;
    var near = State() with { Distance = edge, LongArms = longArms };
    Check($"{mode}/{longArms}近战边界", Skills.ReavingFangs, G(near, rangeOptions));
    Check($"{mode}/{longArms}近战超距转远程", Skills.WrithingSnap, G(near with { Distance = edge + 0.01f }, rangeOptions));
    Check($"{mode}/{longArms}祖灵近战边界", Skills.FirstGeneration, G(near with { Tribute = 5 }, rangeOptions));
    Check($"{mode}/{longArms}盘蛇近战边界", Skills.HuntersCoil,
        G(near with { DreadCombo = 1, NeedsPositional = true, Facing = Facing.Flank }, rangeOptions));
    var aoeEdge = longArms ? 8f : 5f;
    Check($"{mode}/{longArms}AOE边界", Skills.ReavingMaw,
        G(near with { Distance = aoeEdge, NearbyEnemies = 3 }, rangeOptions));
    Check($"{mode}/{longArms}AOE超距", Skills.WrithingSnap,
        G(near with { Distance = aoeEdge + 0.01f, NearbyEnemies = 3 }, rangeOptions));
    var rangedEdge = longArms ? 23f : 20f;
    Check($"{mode}/{longArms}远程边界", Skills.UncoiledFury,
        G(near with { Distance = rangedEdge, Coils = 1 }, rangeOptions));
    Check($"{mode}/{longArms}远程超距", null,
        G(near with { Distance = rangedEdge + 0.01f, Coils = 1 }, rangeOptions));
    Check($"{mode}/{longArms}飞蛇续剑边界", Skills.UncoiledTwinfang,
        ViperEngine.Continuation(Buff(near with { Distance = rangedEdge }, Buffs.PoisedTwinfang)));
    Check($"{mode}/{longArms}飞蛇续剑超距", null,
        ViperEngine.Continuation(Buff(near with { Distance = rangedEdge + 0.01f }, Buffs.PoisedTwinfang)));
    foreach (var invalidDistance in new[] { float.PositiveInfinity, float.NaN })
    {
        Check($"{mode}/{longArms}无效距离GCD", null, G(near with { Distance = invalidDistance }, rangeOptions));
        Check($"{mode}/{longArms}无效距离能力技", null, O(near with { Distance = invalidDistance }, rangeOptions));
    }
}
Check("6米边界飞蛇之尾可用", Skills.UncoiledFury, G(State() with { Distance = 6, Coils = 1 }));
Check("6米边界飞蛇续剑可用", Skills.UncoiledTwinfang,
    ViperEngine.Continuation(Buff(State() with { Distance = 6 }, Buffs.PoisedTwinfang)));
Assert("上限不扩大近战原生3米", !(State() with { Distance = 4 }).CanReach(Skills.SteelFangs));
foreach (var hp in new[] { 0f, 1f, 19f, 19.99f })
{
    var low = State() with { TargetHpPercent = hp, Gauge = 100,
        Cooldowns = new() { [Skills.SerpentsIre] = 0 } };
    Check($"日随目标{hp}%不启动祖灵", Skills.ReavingFangs, G(low, daily));
    Check($"日随目标{hp}%不使用蛇灵气", null, O(low, daily));
    Check($"日随目标{hp}%免费祖灵仍禁止", Skills.ReavingFangs, G(Buff(low, Buffs.ReadyToReawaken), daily));
    Check($"日随目标{hp}%已开祖灵继续", Skills.ThirdGeneration, G(low with { Tribute = 3 }, daily));
}
Check("日随20%边界允许祖灵", Skills.Reawaken, G(State() with { TargetHpPercent = 20, Gauge = 50 }, daily));
Check("日随20%边界允许蛇灵气", Skills.SerpentsIre,
    O(State() with { TargetHpPercent = 20, Cooldowns = new() { [Skills.SerpentsIre] = 0 } }, daily));
Check("高难残血仍可祖灵", Skills.Reawaken, G(State() with { TargetHpPercent = 1, Gauge = 90 }));
Check("高难残血仍可蛇灵气", Skills.SerpentsIre,
    O(State() with { TargetHpPercent = 1, Cooldowns = new() { [Skills.SerpentsIre] = 0 } }));
foreach (var size in new[] { 0, 1, 4, 7, 8, 9, 24 })
foreach (var hp in new[] { 0.1f, 19.99f, 20f, 100f })
{
    var allowed = size == 8 || hp >= 20;
    var partyState = State() with { PartySize = size, TargetHpPercent = hp, Gauge = 50,
        Cooldowns = new() { [Skills.SerpentsIre] = 0 } };
    Check($"日随人数{size}血量{hp}祖灵策略", allowed ? Skills.Reawaken : Skills.ReavingFangs, G(partyState, daily));
    Check($"日随人数{size}血量{hp}预备祖灵策略", allowed ? Skills.Reawaken : Skills.ReavingFangs,
        G(Buff(partyState, Buffs.ReadyToReawaken), daily));
    Check($"日随人数{size}血量{hp}蛇灵气策略", allowed ? Skills.SerpentsIre : null, O(partyState, daily));
    Check($"高难人数{size}血量{hp}预备祖灵无血量限制", Skills.Reawaken,
        G(Buff(partyState, Buffs.ReadyToReawaken), options));
}
foreach (var mode in Enum.GetValues<CombatMode>())
{
    var modeOptions = options with { Mode = mode };
    var prepared = Buff(State() with { Gauge = 0, Coils = 3, BuffTimes = [],
        Charges = new() { [Skills.Vicewinder] = 2 }, Cooldowns = new() { [Skills.SerpentsIre] = 119.8f } }, Buffs.ReadyToReawaken);
    Check($"{mode}预备优先于满魂蛇剑增益与蛇灵气缓冲", Skills.Reawaken, G(prepared, modeOptions));
    Check($"{mode}预备祖灵QT关闭不使用", Skills.UncoiledFury, G(prepared, modeOptions with { Reawaken = false }));
    Check($"{mode}预备爆发QT关闭不使用", Skills.UncoiledFury, G(prepared, modeOptions with { Burst = false }));
    Check($"{mode}预备不打断当前祖灵", Skills.ThirdGeneration, G(prepared with { Tribute = 3 }, modeOptions));
    for (var combo = 1; combo <= 6; combo++)
        Assert($"{mode}预备不打断蛇剑蟒剑{combo}", G(prepared with { DreadCombo = combo }, modeOptions)?.ActionId != Skills.Reawaken);
}
Check("日随残血仍打强碎灵蛇", Skills.Vicewinder,
    G(State() with { TargetHpPercent = 1, Gauge = 100, Charges = new() { [Skills.Vicewinder] = 1 } }, daily));
Check("日随残血仍打飞蛇", Skills.UncoiledFury,
    G(State() with { TargetHpPercent = 1, Gauge = 100, Coils = 1 }, daily));
Check("日随切换高血目标恢复祖灵", Skills.Reawaken,
    G(State() with { TargetHpPercent = 20.01f, Gauge = 50 }, daily));
Check("日随残血蛇尾续剑不受限制", Skills.DeathRattle,
    O(State() with { TargetHpPercent = 1, TailCombo = 1 }, daily));
var engage = new DailyEngagement();
Assert("日随目标未战斗不接战", !engage.TrySubmit(1000, CombatMode.Daily, false, false, 0, true, Skills.SteelFangs));
Assert("高难不自动接战", !engage.TrySubmit(1000, CombatMode.HighEnd, false, true, 0, true, Skills.SteelFangs));
Assert("倒计时中不抢先接战", !engage.TrySubmit(1000, CombatMode.Daily, false, true, 5, true, Skills.SteelFangs));
Assert("ACR关闭目标失效锁未解除等不接战", !engage.TrySubmit(1000, CombatMode.Daily, false, true, 0, false, Skills.SteelFangs));
Assert("玩家战斗中交给常规循环", !engage.TrySubmit(1000, CombatMode.Daily, true, true, 0, true, Skills.SteelFangs));
Assert("无技能时不接战", !engage.TrySubmit(1000, CombatMode.Daily, false, true, 0, true, 0));
Assert("无效倒计时不接战", !engage.TrySubmit(1000, CombatMode.Daily, false, true, float.NaN, true, Skills.SteelFangs));
Assert("目标已开战日随可接战", engage.TrySubmit(1000, CombatMode.Daily, false, true, 0, true, Skills.SteelFangs));
Assert("接战重试节流", !engage.TrySubmit(1999, CombatMode.Daily, false, true, 0, true, Skills.SteelFangs));
Assert("未进入战斗一秒后可重试", engage.TrySubmit(2000, CombatMode.Daily, false, true, 0, true, Skills.SteelFangs));
engage.Reset();
Assert("切区重置接战节流", engage.TrySubmit(2001, CombatMode.Daily, false, true, 0, true, Skills.SteelFangs));
foreach (var burstEnabled in new[] { false, true })
foreach (var viceEnabled in new[] { false, true })
foreach (var reawakenEnabled in new[] { false, true })
foreach (var ireEnabled in new[] { false, true })
    Assert($"固定起手尊重四开关 {burstEnabled}/{viceEnabled}/{reawakenEnabled}/{ireEnabled}",
        StandardOpener.CanStart(options with { Burst = burstEnabled, Cooldowns = viceEnabled,
            Reawaken = reawakenEnabled, SerpentsIre = ireEnabled }) ==
        (burstEnabled && viceEnabled && reawakenEnabled && ireEnabled));
Assert("蛇行阈值严格500ms", CountdownPull.TriggerMilliseconds == 500 && CountdownPull.TriggerSeconds == 0.5f);
Assert("标准起手17个GCD", StandardOpener.Steps.Count(s => !s.Potion && !Skills.IsOffGcd(s.ActionId)) == 17);
Assert("药只有一个可选位置", StandardOpener.Steps.Count(s => s.Potion) == 1 && StandardOpener.Steps[2].Potion);
Assert("标准起手不使用真北", StandardOpener.Steps.All(s => s.ActionId != Skills.TrueNorth));
Assert("起手不重复蛇行", StandardOpener.Steps.All(s => s.ActionId != Skills.Slither));
Assert("首击强碎灵蛇", StandardOpener.Steps[0].ActionId == Skills.Vicewinder);
Assert("结束背裂与蛇尾击", StandardOpener.Steps[^2].ActionId == Skills.HindsbaneFang && StandardOpener.Steps[^1].ActionId == Skills.DeathRattle);
Assert("两个强碎灵蛇", StandardOpener.Steps.Count(s => s.ActionId == Skills.Vicewinder) == 2);
Assert("两个飞蛇之尾", StandardOpener.Steps.Count(s => s.ActionId == Skills.UncoiledFury) == 2);
var approach = new PrepullAttack();
Assert("未蛇行不自动开怪", !approach.TrySubmit(1000, 0.3f, 123, true, true, Skills.Vicewinder));
approach.Arm(123, 1000, 0.5f);
Assert("蛇行后等待实际动画锁", !approach.TrySubmit(1100, 0.4f, 123, true, false, Skills.Vicewinder));
Assert("动画锁结束无需战斗状态即可首击", approach.TrySubmit(1600, 0, 123, true, true, Skills.Vicewinder));
Assert("首击最多提交一次", !approach.TrySubmit(1650, 0, 123, true, true, Skills.Vicewinder));
Assert("战斗事件可识别刚提交的首击", approach.IsRecentFirstHit(1700, 123, Skills.Vicewinder));
Assert("其他目标不继承首击", !approach.IsRecentFirstHit(1700, 456, Skills.Vicewinder));
Assert("不同技能不误跳首击", !approach.IsRecentFirstHit(1700, 123, Skills.ReavingFangs));
Assert("旧首击不影响以后战斗", !approach.IsRecentFirstHit(5000, 123, Skills.Vicewinder));
approach.Arm(123, 1000, 0.5f);
Assert("中途关闭ACR取消首击", !approach.TrySubmit(1300, 0.2f, 123, false, true, Skills.Vicewinder));
Assert("重新开启不补发取消的首击", !approach.TrySubmit(1400, 0.1f, 123, true, true, Skills.Vicewinder));
approach.Arm(123, 1000, 0.5f);
Assert("倒计时提前取消则取消攻击", !approach.TrySubmit(1100, 0, 123, true, true, Skills.Vicewinder));
Assert("取消后不补发", !approach.TrySubmit(1600, 0, 123, true, true, Skills.Vicewinder));
approach.Arm(123, 1000, 0.5f);
Assert("更换目标取消攻击", !approach.TrySubmit(1300, 0.2f, 456, true, true, Skills.Vicewinder));
approach.Arm(123, 1000, 0.5f);
Assert("无法接近超时取消攻击", !approach.TrySubmit(3001, 0, 123, true, true, Skills.Vicewinder));
approach.Arm(123, 1000, 0.5f);
Assert("重新开始长倒计时取消旧攻击", !approach.TrySubmit(1200, 10, 123, true, true, Skills.Vicewinder));
approach.Arm(123, 1000, 0.5f);
approach.Reset();
Assert("切区卸载清理待执行攻击", !approach.HasPending && !approach.TrySubmit(1600, 0, 123, true, true, Skills.Vicewinder));
approach.Arm(0, 1000, 0.5f);
Assert("无目标不能启用接近攻击", !approach.HasPending);
approach.Arm(123, 1000, float.NaN);
Assert("无效倒计时不启用攻击", !approach.HasPending);
approach.Arm(123, 1000, 0.5f);
Assert("标准起手关闭可使用基础首击", approach.TrySubmit(1600, 0, 123, true, true, Skills.ReavingFangs));
Assert("基础首击不会误判强碎灵蛇已用", !approach.IsRecentFirstHit(1700, 123, Skills.Vicewinder));
foreach (var firstFacing in new[] { Facing.Flank, Facing.Rear })
foreach (var secondFacing in new[] { Facing.Flank, Facing.Rear })
{
    var plan = new StandardOpenerPlan();
    Assert("起手初段不提前固化盘蛇", plan.InitialSteps(false).Select(s => s.ActionId)
        .SequenceEqual(new uint[] { Skills.Vicewinder, Skills.SerpentsIre, 0 }));
    Assert("已打首发只省略强碎灵蛇", plan.InitialSteps(true).Select(s => s.ActionId)
        .SequenceEqual(new uint[] { Skills.SerpentsIre, 0 }));
    Assert("等待初次强碎灵蛇量谱", plan.Next(State()) == null);
    var first = firstFacing == Facing.Flank ? Skills.HuntersCoil : Skills.SwiftskinsCoil;
    var firstState = State() with { DreadCombo = 1, NeedsPositional = true, Facing = firstFacing };
    var phase = plan.Next(firstState)!;
    Assert($"标准起手第一套实时{firstFacing}", phase[0].ActionId == first);
    Assert("第一招双牙与所选盘蛇一致", phase.Skip(1).Select(s => s.ActionId).SequenceEqual(
        first == Skills.HuntersCoil ? new uint[] { Skills.TwinfangBite, Skills.TwinbloodBite }
            : new uint[] { Skills.TwinbloodBite, Skills.TwinfangBite }));
    Assert("未收到第一盘蛇量谱不推进", plan.Next(firstState) == null);
    var remaining = first == Skills.HuntersCoil ? Skills.SwiftskinsCoil : Skills.HuntersCoil;
    var second = plan.Next(firstState with { DreadCombo = first == Skills.HuntersCoil ? 2 : 3,
        Facing = firstFacing == Facing.Flank ? Facing.Rear : Facing.Flank })!;
    Assert("换位不重复第一招", second[0].ActionId == remaining);
    Assert("第一套完成后才打第二次强碎灵蛇", plan.Next(State())![0].ActionId == Skills.Vicewinder);
    var nextState = firstState with { Facing = secondFacing };
    var next = plan.Next(nextState)!;
    var nextFirst = secondFacing == Facing.Flank ? Skills.HuntersCoil : Skills.SwiftskinsCoil;
    Assert($"第二次强碎灵蛇独立读取{secondFacing}", next[0].ActionId == nextFirst);
    var nextLast = plan.Next(nextState with { DreadCombo = nextFirst == Skills.HuntersCoil ? 2 : 3 })!;
    Assert("第二套仍完成另一招", nextLast[0].ActionId != nextFirst);
    var tail = plan.Next(State())!;
    Assert("自适应后祖灵与飞蛇收尾不变", tail.Select(s => s.ActionId)
        .SequenceEqual(StandardOpener.Steps.Skip(16).Select(s => s.ActionId)));
    Assert("标准起手阶段完成不再重复", plan.Complete && plan.Next(State()) == null);
}
var invalidPlan = new StandardOpenerPlan();
foreach (var facing in new[] { Facing.Flank, Facing.Rear })
{
    var preparedPlan = new StandardOpenerPlan();
    var actions = preparedPlan.InitialSteps(false).Select(x => x.ActionId).ToList();
    var firstCoil = State() with { DreadCombo = 1, NeedsPositional = true, Facing = facing };
    actions.AddRange(preparedPlan.Next(firstCoil)!.Select(x => x.ActionId));
    actions.AddRange(preparedPlan.Next(firstCoil with { DreadCombo = facing == Facing.Flank ? 2 : 3 })!.Select(x => x.ActionId));
    var freeState = Buff(State() with { Gauge = 20 }, Buffs.ReadyToReawaken);
    Assert($"标准起手{facing}预备超出范围不提交祖灵",
        preparedPlan.Next(freeState with { Distance = 6 })![0].ActionId == Skills.Vicewinder);
    // Use a fresh plan for the available free-Reawaken branch.
    preparedPlan = new StandardOpenerPlan();
    preparedPlan.Next(firstCoil);
    preparedPlan.Next(firstCoil with { DreadCombo = facing == Facing.Flank ? 2 : 3 });
    var freeSteps = preparedPlan.Next(freeState)!;
    Assert($"标准起手{facing}第一套结束预备先于第二蛇剑", freeSteps[0].ActionId == Skills.Reawaken &&
        freeSteps.Select(x => x.ActionId).SequenceEqual(StandardOpener.Steps.Skip(16).Take(10).Select(x => x.ActionId)));
    actions.AddRange(freeSteps.Select(x => x.ActionId));
    Assert($"标准起手{facing}祖灵进行中不重新蛇剑",
        preparedPlan.Next(freeState with { Tribute = 3, DreadCombo = 9 }) == null);
    Assert($"标准起手{facing}祖灵最后一档蛇剑状态归零仍等待",
        preparedPlan.Next(freeState with { Tribute = 1, DreadCombo = 0 }) == null);
    Assert($"标准起手{facing}祖灵状态过渡包仍等待",
        preparedPlan.Next(Buff(State(), Buffs.Reawakened)) == null);
    var afterFree = preparedPlan.Next(State())!;
    Assert($"标准起手{facing}祖灵结束接第二蛇剑", afterFree.Single().ActionId == Skills.Vicewinder);
    actions.AddRange(afterFree.Select(x => x.ActionId));
    actions.AddRange(preparedPlan.Next(firstCoil)!.Select(x => x.ActionId));
    actions.AddRange(preparedPlan.Next(firstCoil with { DreadCombo = facing == Facing.Flank ? 2 : 3 })!.Select(x => x.ActionId));
    var finalSteps = preparedPlan.Next(State())!;
    actions.AddRange(finalSteps.Select(x => x.ActionId));
    Assert($"标准起手{facing}收尾不重复祖灵且完整", preparedPlan.Complete &&
        finalSteps.Select(x => x.ActionId).SequenceEqual(StandardOpener.Steps.Skip(26).Select(x => x.ActionId)));
    Assert($"标准起手{facing}仍17GCD且只使用一次祖灵",
        actions.Count(x => x != 0 && !Skills.IsOffGcd(x)) == 17 && actions.Count(x => x == Skills.Reawaken) == 1);
}
Assert("失去目标不推进标准起手", invalidPlan.Next(State() with { HasTarget = false, DreadCombo = 1 }) == null);
Assert("脱战不推进标准起手", invalidPlan.Next(State() with { Active = false, DreadCombo = 1 }) == null);
var abilityWindow = new AbilityWindow();
long tick = 1000;
// Reproduce PR's missed reset: every GCD is queued at 0.2s, so <=0.1s is never observed.
// Two abilities each GCD must still be available for more than two minutes.
for (var cycle = 0; cycle < 100; cycle++)
{
    abilityWindow.Observe(0.6f, 1.9f);
    Assert($"队列GCD {cycle} 第一插恢复", abilityWindow.CanSubmit(Skills.TwinfangBite, tick, 2));
    abilityWindow.Record(Skills.TwinfangBite, tick);
    Assert($"队列GCD {cycle} 不重复提交同招", !abilityWindow.CanSubmit(Skills.TwinfangBite, tick + 100, 2));
    abilityWindow.Observe(1.25f, 1.25f);
    Assert($"队列GCD {cycle} 第二插可用", abilityWindow.CanSubmit(Skills.TwinbloodBite, tick + 650, 2));
    abilityWindow.Record(Skills.TwinbloodBite, tick + 650);
    abilityWindow.Observe(2.3f, 0.2f);
    Assert($"队列GCD {cycle} 两插上限", !abilityWindow.CanSubmit(Skills.TrueNorth, tick + 1300, 2));
    tick += 2500;
}
abilityWindow.Reset();
abilityWindow.Observe(0.6f, 1.1f);
Assert("宿主零插上限不被绕过", !abilityWindow.CanSubmit(Skills.DeathRattle, tick, 0));
Assert("单插上限首插可用", abilityWindow.CanSubmit(Skills.DeathRattle, tick, 1));
abilityWindow.Record(Skills.DeathRattle, tick);
Assert("单插上限拒绝二插", !abilityWindow.CanSubmit(Skills.SerpentsIre, tick + 700, 1));
abilityWindow.Observe(0.1f, 1.6f);
Assert("1.7秒祖灵GCD切换复位", abilityWindow.CanSubmit(Skills.FirstLegacy, tick + 800, 1));
abilityWindow.Record(Skills.FirstLegacy, tick + 800);
abilityWindow.Reset();
Assert("脱战清空节流状态", abilityWindow.CanSubmit(Skills.FirstLegacy, tick + 801, 2));
var basicAutomation = new BasicAutomation();
Assert("开启三插覆盖宿主两插上限", AbilityWindow.Maximum(true, 2) == 3);
Assert("关闭三插遵循宿主零或单插", AbilityWindow.Maximum(false, 0) == 0 && AbilityWindow.Maximum(false, 1) == 1);
Assert("关闭三插最多两插", AbilityWindow.Maximum(false, 3) == 2);
var tripleWindow = new AbilityWindow();
tripleWindow.Observe(0.5f, 2);
tripleWindow.Record(Skills.TwinfangBite, 1000);
tripleWindow.Record(Skills.TwinbloodBite, 1600);
Assert("两次双牙后两插模式禁止真北", !tripleWindow.CanSubmit(Skills.TrueNorth, 2200, AbilityWindow.Maximum(false, 2)));
Assert("两次双牙后三插模式允许真北", tripleWindow.CanSubmit(Skills.TrueNorth, 2200, AbilityWindow.Maximum(true, 2)));
Assert("三插仍遵守600ms间隔", !tripleWindow.CanSubmit(Skills.TrueNorth, 2199, 3));
tripleWindow.Record(Skills.TrueNorth, 2200);
Assert("三插后拒绝第四插", !tripleWindow.CanSubmit(Skills.SerpentsIre, 2800, 3));
tripleWindow.Observe(0.1f, 2.4f);
Assert("新GCD恢复三插次数", tripleWindow.CanSubmit(Skills.SerpentsIre, 2800, 3));
Assert("存活不重置QT", !basicAutomation.ObserveDeath(false, true));
Assert("死亡第一次重置QT", basicAutomation.ObserveDeath(true, true));
Assert("持续死亡不反复覆盖QT", !basicAutomation.ObserveDeath(true, true));
Assert("复活后再次死亡重置QT", !basicAutomation.ObserveDeath(false, true) && basicAutomation.ObserveDeath(true, true));
basicAutomation.Reset();
Assert("关闭死亡重置则不执行", !basicAutomation.ObserveDeath(true, false));
Assert("死亡中打开设置不补重置", !basicAutomation.ObserveDeath(true, true));
Assert("关闭后下次死亡可重置", !basicAutomation.ObserveDeath(false, true) && basicAutomation.ObserveDeath(true, true));
basicAutomation.Reset();
Assert("无倒计时不解除停手", !basicAutomation.ObserveCountdown(0, true, true, true));
Assert("倒计时开始解除停手", basicAutomation.ObserveCountdown(15, true, true, true));
Assert("同一倒计时手动停手不被覆盖", !basicAutomation.ObserveCountdown(14, true, true, true));
Assert("倒计时正常递减不重复解除", !basicAutomation.ObserveCountdown(5, true, true, true));
Assert("无零帧重启倒计时可解除", basicAutomation.ObserveCountdown(15, true, true, true));
Assert("取消倒计时不解除", !basicAutomation.ObserveCountdown(0, true, true, true));
Assert("关闭自动解除则保持停手", !basicAutomation.ObserveCountdown(15, false, true, true));
Assert("倒计时中打开不立即覆盖停手", !basicAutomation.ObserveCountdown(14, true, true, true));
basicAutomation.Reset();
Assert("非停手状态不自动打开", !basicAutomation.ObserveCountdown(15, true, false, true));
basicAutomation.Reset();
Assert("无效角色目标战斗条件不解除", !basicAutomation.ObserveCountdown(15, true, true, false));
Assert("条件晚于倒计时开始满足不补解除", !basicAutomation.ObserveCountdown(14, true, true, true));
Assert("非法倒计时不解除", !basicAutomation.ObserveCountdown(float.NaN, true, true, true) &&
    !basicAutomation.ObserveCountdown(float.PositiveInfinity, true, true, true));
Assert("倒计时清空后可重新开始", basicAutomation.ObserveCountdown(10, true, true, true));
Assert("木人配置关闭禁止倒计时出招", !BasicAutomation.AllowsCountdownTarget(true, false));
Assert("木人配置开启允许倒计时出招", BasicAutomation.AllowsCountdownTarget(true, true));
Assert("普通敌人不受木人开关影响", BasicAutomation.AllowsCountdownTarget(false, false) && BasicAutomation.AllowsCountdownTarget(false, true));
Assert("木人按名称ID识别", BasicAutomation.IsTrainingDummy(541) && !BasicAutomation.IsTrainingDummy(0) && !BasicAutomation.IsTrainingDummy(13078));
var journal = new DiagnosticJournal();
var diagnosticTime = new DateTime(2026, 10, 25, 19, 0, 0);
for (var i = 0; i < 5100; i++) journal.Add(new(diagnosticTime.AddMilliseconds(i), "技能", "蝰蛇", $"技能{i}", "命中", "详情"));
Assert("诊断记录最多5000条", journal.Count == 5000);
Assert("诊断记录淘汰最旧条目", journal.Snapshot()[0].Subject == "技能100");
var history = journal.Snapshot();
journal.Clear();
Assert("诊断快照不受清空影响", history.Length == 5000 && journal.Count == 0);
journal.Add(new(diagnosticTime.AddSeconds(1), "状态", "蝰蛇", "含,逗号", "刷新", "含\"引号\"\r\n换行"));
journal.Add(new(diagnosticTime, "战斗", "蝰蛇", "开始", "开始", ""));
var csv = journal.ToCsv();
Assert("CSV中文标题与时间排序", csv.StartsWith("时间,分类,职业,对象,事件,详情\r\n\"2026-10-25 19:00:00.000\""));
Assert("CSV特殊字段无损转义", csv.Contains("\"含,逗号\"") && csv.Contains("\"含\"\"引号\"\"\r\n换行\""));
var stallWatch = new DiagnosticStallWatch();
Assert("停顿未满4秒不告警", !stallWatch.Observe(1000, true) && !stallWatch.Observe(4999, true));
Assert("停顿4秒首次告警", stallWatch.Observe(5000, true));
Assert("停顿告警5秒节流", !stallWatch.Observe(9999, true) && stallWatch.Observe(10000, true));
Assert("退出停顿重置计时", !stallWatch.Observe(10001, false) && !stallWatch.Observe(20000, true));
Assert("重进停顿重新等待4秒", !stallWatch.Observe(23999, true) && stallWatch.Observe(24000, true));
Assert("停顿计时回退安全", !stallWatch.Observe(1, true) && !stallWatch.Observe(4000, true) && stallWatch.Observe(4001, true));
var migrationRoot = Path.Combine(Path.GetTempPath(), "Displaced-Migration-" + Guid.NewGuid().ToString("N"));
try
{
    var source = Path.Combine(migrationRoot, "legacy");
    var destination = Path.Combine(migrationRoot, "current");
    ConfigurationMigration.CopyMissingJson(source, destination);
    Assert("旧配置不存在时不创建新目录", !Directory.Exists(destination));
    Directory.CreateDirectory(source);
    Directory.CreateDirectory(destination);
    File.WriteAllText(Path.Combine(source, "VPR.json"), "{\"mode\":\"legacy\"}");
    File.WriteAllText(Path.Combine(destination, "VPR.json"), "{\"mode\":\"current\"}");
    File.WriteAllText(Path.Combine(source, "Common.json"), "{\"theme\":2}");
    File.WriteAllText(Path.Combine(source, "ignored.dll"), "not a setting");
    ConfigurationMigration.CopyMissingJson(source, destination);
    Assert("已有新作者配置不覆盖", File.ReadAllText(Path.Combine(destination, "VPR.json")) == "{\"mode\":\"current\"}");
    Assert("中断迁移后补齐缺少的主题设置", File.ReadAllText(Path.Combine(destination, "Common.json")) == "{\"theme\":2}");
    Assert("配置迁移不携带旧程序集", !File.Exists(Path.Combine(destination, "ignored.dll")));
    ConfigurationMigration.CopyMissingJson(source, destination);
    Assert("重复迁移保持新配置与旧源文件", File.Exists(Path.Combine(source, "Common.json")) &&
        File.ReadAllText(Path.Combine(destination, "VPR.json")) == "{\"mode\":\"current\"}");
}
finally { Directory.Delete(migrationRoot, recursive: true); }
Console.WriteLine($"Checks: {checks}; Failures: {failures.Count}");
foreach (var failure in failures) Console.WriteLine("FAIL " + failure);
return failures.Count == 0 ? 0 : 1;
