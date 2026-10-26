using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Lumina;
using Lumina.Data;
using Lumina.Excel.Sheets;

var host = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XIVLauncherCN", "addon", "Hooks", "26-10-21-01");
var plugin = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XIVLauncherCN", "installedPlugins", "PromeRotation", "1.5.10.4");
host = Environment.GetEnvironmentVariable("VIPER_DALAMUD_DIR") ?? host;
plugin = Environment.GetEnvironmentVariable("VIPER_PROME_DIR") ?? plugin;
AssemblyLoadContext.Default.Resolving += (_, name) => {
    var path = Path.Combine(host, name.Name + ".dll");
    if (!File.Exists(path)) path = Path.Combine(plugin, name.Name + ".dll");
    return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
};
if (args.Length >= 2 && args[0] == "--validate-package")
{
    // Use the installed PR's real resolver, assembly loader and registration/package validators.
    // Bypass only the shadow cache directory service, which requires an in-game PluginInterface.
    // Only game-independent opener/handler instances; no rotation, registry commits or game actions.
    try
    {
        var folder = Path.GetFullPath(args[1]).TrimEnd(Path.DirectorySeparatorChar);
        var pr = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(plugin, "PromeRotation.dll"));
        AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(host, "Dalamud.dll"));
        var registry = pr.GetType("PromeRotation.Managers.RotationRegistry", true)!;
        const BindingFlags privateStatic = BindingFlags.NonPublic | BindingFlags.Static;
        object?[] resolve = [folder, null, null];
        if (!(bool)registry.GetMethod("TryResolveMainDll", privateStatic)!.Invoke(null, resolve)!)
            throw new InvalidOperationException((string?)resolve[2]);
        var main = (string)resolve[1]!;
        var loaderType = pr.GetType("PromeRotation.Managers.PromeExAssemblyLoader", true)!;
        var loader = (AssemblyLoadContext)Activator.CreateInstance(loaderType, AssemblyLoadContext.Default, main, folder)!;
        try
        {
            var assembly = (Assembly)loaderType.GetMethod("LoadMainAssembly")!.Invoke(loader, null)!;
            var contract = pr.GetType("PromeRotation.Rotation.IRotation", true)!;
            var registrationType = registry.GetNestedType("RotationRegistration", BindingFlags.Public)!;
            var candidates = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(registrationType))!;
            var build = registry.GetMethod("TryBuildRegistration", privateStatic)!;
            foreach (var type in assembly.GetTypes().Where(t => contract.IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface))
            {
                object?[] buildArgs = [type, null, null, null, true, folder, main, main, assembly, loader, null, null];
                if (!(bool)build.Invoke(null, buildArgs)!) throw new InvalidOperationException((string?)buildArgs[11]);
                candidates.Add(buildArgs[10]);
                var metadata = type.GetCustomAttributesData().Single(a => a.AttributeType.Name == "RotationMetadataAttribute");
                if (!Equals(metadata.ConstructorArguments[1].Value, "Displaced VPR") ||
                    !Equals(metadata.ConstructorArguments[2].Value, "Displaced"))
                    throw new InvalidOperationException("Rotation display name / author mismatch");
                foreach (var name in new[] { "QtList", "Openers" })
                    _ = type.GetProperty(name, BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                var qts = (IReadOnlyDictionary<string, bool>)type.GetProperty("QtList")!.GetValue(null)!;
                if (qts.Keys.Any(key => key.Contains("蝰蛇·", StringComparison.Ordinal)) ||
                    qts.ContainsKey("倒计时蛇行") || qts.ContainsKey("蛇剑冷却") ||
                    !qts.TryGetValue("强碎灵蛇", out var viceEnabled) || !viceEnabled ||
                    !qts.TryGetValue("祖灵降临", out var reawakenEnabled) || !reawakenEnabled ||
                    !qts.TryGetValue("蛇灵气", out var ireEnabled) || !ireEnabled ||
                    qts.ContainsKey("标准起手"))
                    throw new InvalidOperationException("QT names / removed countdown QT / standard opener default contract failed");
                var openers = (IReadOnlyDictionary<string, Type>)type.GetProperty("Openers")!.GetValue(null)!;
                if (!openers.TryGetValue("蝰蛇标准起手", out var registered) || registered.Name != "ViperStandardOpener")
                    throw new InvalidOperationException("Standard opener registration mismatch");
                var lifecycle = pr.GetType("PromeRotation.Rotation.IRotationLifecycle", true)!;
                if (!lifecycle.IsAssignableFrom(type)) throw new InvalidOperationException("UI lifecycle missing");
                type.GetInterfaceMap(lifecycle);
            }
            var ui = assembly.GetType("ErosUI.ErosUIFramework", true)!;
            var qtsForUi = (IReadOnlyDictionary<string, bool>)assembly.GetType("ViperACR.Host.ViperRotation")!.GetProperty("QtList")!.GetValue(null)!;
            var uiDraws = 0;
            var openerDraws = 0;
            ui.GetMethod("Configure")!.Invoke(null, ["VPR", "蝰蛇剑士", qtsForUi,
                (Func<string,bool>)(_ => false), (Func<string,bool,bool>)((_,_) => true),
                (Func<string,bool>)(key => qtsForUi.GetValueOrDefault(key)),
                new Dictionary<string,(string key,bool invert)[]>(), Array.Empty<string>(), null, "Displaced", null,
                (System.Action)(() => uiDraws++), (System.Action)(() => openerDraws++)]);
            var env = assembly.GetType("ErosUI.ErosUIJobEnv", true)!;
            if (!Equals(env.GetProperty("作者")!.GetValue(null), "Displaced") ||
                !Equals(env.GetProperty("JobTag")!.GetValue(null), "VPR")) throw new InvalidOperationException("ErosUI identity mismatch");
            ((System.Action)env.GetProperty("DrawRotationSettings")!.GetValue(null)!)();
            if (uiDraws != 1) throw new InvalidOperationException("ErosUI rotation settings callback mismatch");
            ((System.Action)env.GetProperty("DrawOpenerSettings")!.GetValue(null)!)();
            var settings = Activator.CreateInstance(assembly.GetType("ViperACR.Host.ViperSettings", true)!)!;
            if (openerDraws != 1 || settings.GetType().GetProperty("Opener")!.GetValue(settings)!.ToString() != "Standard")
                throw new InvalidOperationException("Default standard opener / opener UI callback mismatch");
            var trueNorthSetting = settings.GetType().GetProperty("TrueNorthGcdPercent")!;
            if ((int)trueNorthSetting.GetValue(settings)! != 50)
                throw new InvalidOperationException("True North GCD timing must default to 50 percent");
            var legacySettings = JsonSerializer.Deserialize("{}", settings.GetType())!;
            if ((int)trueNorthSetting.GetValue(legacySettings)! != 50)
                throw new InvalidOperationException("Legacy settings must receive the 50 percent default");
            trueNorthSetting.SetValue(settings, 65);
            var timingSettingsJson = JsonSerializer.Serialize(settings, settings.GetType());
            var restoredTimingSettings = JsonSerializer.Deserialize(timingSettingsJson, settings.GetType())!;
            if ((int)trueNorthSetting.GetValue(restoredTimingSettings)! != 65)
                throw new InvalidOperationException("True North GCD timing JSON persistence mismatch");
            Console.WriteLine("True North GCD timing: 50 percent default, legacy settings and 65 percent JSON roundtrip: PASS.");
            var modeSettingsType = assembly.GetType("ErosUI.ErosUISettings", true)!;
            var modeSettings = Activator.CreateInstance(modeSettingsType)!;
            var highEndField = modeSettingsType.GetField("IsHighEnd")!;
            if ((bool)highEndField.GetValue(modeSettings)!)
                throw new InvalidOperationException("New UI settings must default to Daily mode");
            var jsonOptions = new JsonSerializerOptions { IncludeFields = true };
            var longArmsField = modeSettingsType.GetField("LongArms")!;
            if (!(bool)longArmsField.GetValue(modeSettings)!) throw new InvalidOperationException("Long Arms must default On");
            foreach (var enabled in new[] { false, true })
            {
                longArmsField.SetValue(modeSettings, enabled);
                var rangeJson = JsonSerializer.Serialize(modeSettings, modeSettingsType, jsonOptions);
                var rangeRestored = JsonSerializer.Deserialize(rangeJson, modeSettingsType, jsonOptions)!;
                if ((bool)longArmsField.GetValue(rangeRestored)! != enabled)
                    throw new InvalidOperationException("Long Arms UI setting JSON roundtrip mismatch");
            }
            Console.WriteLine("Long Arms: default On; On/Off JSON persistence in basic UI settings: PASS.");
            var dashField = modeSettingsType.GetField("UnlimitedDashDistance")!;
            if ((bool)dashField.GetValue(modeSettings)!) throw new InvalidOperationException("Unlimited dash must default Off");
            foreach (var enabled in new[] { false, true })
            {
                dashField.SetValue(modeSettings, enabled);
                var dashJson = JsonSerializer.Serialize(modeSettings, modeSettingsType, jsonOptions);
                var restoredDash = JsonSerializer.Deserialize(dashJson, modeSettingsType, jsonOptions)!;
                if ((bool)dashField.GetValue(restoredDash)! != enabled)
                    throw new InvalidOperationException("Unlimited dash persistence mismatch");
            }
            Console.WriteLine("Extension settings: Unlimited Dash default Off and On/Off JSON persistence: PASS.");
            var tripleField = modeSettingsType.GetField("TripleWeave")!;
            if (!(bool)tripleField.GetValue(Activator.CreateInstance(modeSettingsType))! ||
                !(bool)tripleField.GetValue(JsonSerializer.Deserialize("{}", modeSettingsType, jsonOptions))!)
                throw new InvalidOperationException("Triple weave must default On for new and legacy settings");
            foreach (var enabled in new[] { false, true })
            {
                tripleField.SetValue(modeSettings, enabled);
                var restored = JsonSerializer.Deserialize(JsonSerializer.Serialize(modeSettings, modeSettingsType, jsonOptions), modeSettingsType, jsonOptions)!;
                if ((bool)tripleField.GetValue(restored)! != enabled)
                    throw new InvalidOperationException("Triple weave setting persistence mismatch");
            }
            Console.WriteLine("Triple weave: default On, legacy default and On/Off JSON persistence: PASS.");
            foreach (var highEnd in new[] { false, true })
            {
                highEndField.SetValue(modeSettings, highEnd);
                var modeJson = JsonSerializer.Serialize(modeSettings, modeSettingsType, jsonOptions);
                var restored = JsonSerializer.Deserialize(modeJson, modeSettingsType, jsonOptions)!;
                if ((bool)highEndField.GetValue(restored)! != highEnd)
                    throw new InvalidOperationException("UI mode JSON persistence mismatch");
            }
            var modeType = assembly.GetType("ViperACR.Core.CombatMode", true)!;
            var openerPolicy = assembly.GetType("ViperACR.Core.ModePolicy", true)!.GetMethod("UsesStandardOpener")!;
            foreach (var highEnd in new[] { false, true })
            foreach (var standardSelected in new[] { false, true })
                if ((bool)openerPolicy.Invoke(null, [Enum.Parse(modeType, highEnd ? "HighEnd" : "Daily"), standardSelected])!
                    != (highEnd && standardSelected))
                    throw new InvalidOperationException("Mode/opener selection policy mismatch");
            if (modeSettingsType.GetMethod("SwitchMode") == null ||
                assembly.GetType("ErosUI.ErosUISettingsUI", true)!.GetMethod("DrawModeSwitchRow") == null)
                throw new InvalidOperationException("Missing shared ErosUI mode switch");
            Console.WriteLine("Daily default, both modes JSON persistence and all four mode/opener policies: PASS.");
            var burstPolicy = assembly.GetType("ViperACR.Core.ModePolicy", true)!.GetMethod("AllowsTargetBurst")!;
            foreach (var highEnd in new[] { false, true })
            foreach (var partySize in new[] { 0, 4, 7, 8 })
            foreach (var hp in new[] { 19.99f, 20f, 100f })
                if ((bool)burstPolicy.Invoke(null, [Enum.Parse(modeType, highEnd ? "HighEnd" : "Daily"), hp, partySize])!
                    != (highEnd || partySize == 8 || hp >= 20))
                    throw new InvalidOperationException("Daily target HP burst policy mismatch");
            if (assembly.GetType("ViperACR.Host.HostSnapshot", true)!.GetMethod("IsTargetInCombat", BindingFlags.NonPublic | BindingFlags.Static) == null)
                throw new InvalidOperationException("Missing target combat state reader");
            var partyReader = assembly.GetType("ViperACR.Host.HostSnapshot", true)!.GetMethod("PartySize", BindingFlags.NonPublic | BindingFlags.Static);
            if (partyReader == null || partyReader.ReturnType != typeof(int))
                throw new InvalidOperationException("Missing live party size reader");
            Console.WriteLine("Daily HP 20 percent boundary / eight-member exemption / HighEnd exemption and host party reader: PASS.");
            var tabs = (string[])assembly.GetType("ErosUI.ErosUISettingsWindow", true)!
                .GetField("tabs", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
            if (tabs.Contains("起手设置") || !tabs.Contains("基础设置"))
                throw new InvalidOperationException("Opener selection belongs in basic settings; separate opener tab must be absent");
            if (!tabs.Contains("拓展功能") || !tabs.Contains("开发诊断") ||
                assembly.GetType("ErosUI.ErosUISettingsUI", true)!.GetMethod("DrawExtensions") == null ||
                env.GetProperty("DrawBasicSettings") == null || env.GetProperty("DrawDiagnostics") == null)
                throw new InvalidOperationException("Missing extension/diagnostics/basic settings UI contract");
            var rotationType = assembly.GetType("ViperACR.Host.ViperRotation", true)!;
            var limitBreak = assembly.GetType("ViperACR.Host.ViperLimitBreak", true)!;
            var createLimitBreak = limitBreak.GetMethod("CreateAction", BindingFlags.NonPublic | BindingFlags.Static)!;
            if (createLimitBreak.Invoke(null, [0u, 123u]) != null || createLimitBreak.Invoke(null, [200u, 0u]) != null)
                throw new InvalidOperationException("Limit break must reject empty gauge/action or missing target");
            foreach (var actionId in new uint[] { 200, 201, 34866 })
            {
                var action = createLimitBreak.Invoke(null, [actionId, 123u])!;
                var actionType = action.GetType();
                if ((uint)actionType.GetField("ActionId")!.GetValue(action)! != actionId ||
                    actionType.GetField("Type")!.GetValue(action)!.ToString() != "LimitBreak" ||
                    actionType.GetField("Target")!.GetValue(action)!.ToString() != "Target" ||
                    (uint)actionType.GetProperty("NetworkTid")!.GetValue(action)! != 123u ||
                    actionType.GetProperty("ExecutionGuard")!.GetValue(action) == null)
                    throw new InvalidOperationException("Limit break action/queue/target/guard contract failed");
            }
            Console.WriteLine("Manual limit break: dynamic action ID, LimitBreak queue type, target binding and guard contract: PASS (no game actions).");
            DiagnosticsValidation.Run(assembly, pr, settings);
            BasicAutomationValidation.Run(assembly, pr);
            var snapshotType = assembly.GetType("ViperACR.Core.Snapshot", true)!;
            var decisionType = assembly.GetType("ViperACR.Core.Decision", true)!;
            var snapshot = Activator.CreateInstance(snapshotType)!;
            var decision = Activator.CreateInstance(decisionType, 34636u, "regression")!;
            var makeAction = rotationType.GetMethod("MakeAction", BindingFlags.NonPublic | BindingFlags.Static)!;
            var dispatched = makeAction.Invoke(null, [snapshot, decision, true, true])!;
            if (dispatched.GetType().GetField("Type")!.GetValue(dispatched)!.ToString() != "Always")
                throw new InvalidOperationException("Dynamic abilities must use host Always queue");
            Console.WriteLine("Opener UI/default, removed standard QT and actual Always action type: PASS.");
            var offsetEngine = pr.GetType("PromeRotation.Helpers.GcdOffsetEngine", true)!;
            var timing = offsetEngine.GetMethod("Decide")!.Invoke(null, [0.2f, false, 300, 10, 17])!;
            if (!(bool)timing.GetType().GetField("Item1")!.GetValue(timing)! ||
                (bool)timing.GetType().GetField("Item2")!.GetValue(timing)!)
                throw new InvalidOperationException("Expected real host queue/use mismatch at 200ms");
            Console.WriteLine("Actual PR GCD decision: at 200ms CanQueue=true and CanUse=false; missed-reset trigger reproduced.");
            foreach (var window in new[] { "ErosUISettingsWindow", "ErosUIQtPanelWindow", "ErosUIHotkeyPanelWindow", "CombatControlWindow" })
                if (assembly.GetType("ErosUI." + window, true)!.BaseType == null) throw new InvalidOperationException("Missing UI window");
            Console.WriteLine("ErosUI embedded, lifecycle interface, VPR/author identity and rotation settings callback: PASS (no windows rendered).");
            // Exercise the actual host event interface without constructing the game-dependent rotation.
            var handlerType = assembly.GetType("ViperACR.Host.ViperEventHandler", true)!;
            var countdownTicks = 0;
            var resetTicks = 0;
            var openerResets = 0;
            var battleUpdates = 0;
            var diagnosticsUpdates = 0;
            var handler = Activator.CreateInstance(handlerType, (System.Action)(() => resetTicks++),
                (System.Action)(() => countdownTicks++), (System.Action)(() => openerResets++))!;
            handlerType.GetProperty("BattleUpdate")!.SetValue(handler, (System.Action)(() => battleUpdates++));
            handlerType.GetProperty("Update")!.SetValue(handler, (System.Action)(() => diagnosticsUpdates++));
            handlerType.GetMethod("OnUpdate")!.Invoke(handler, null);
            handlerType.GetMethod("OnBattleUpdate")!.Invoke(handler, null);
            handlerType.GetMethod("OnOutOfBattleUpdate")!.Invoke(handler, null);
            handlerType.GetMethod("OnTerritoryChanged")!.Invoke(handler, [(ushort)1]);
            handlerType.GetMethod("OnBattleEnded")!.Invoke(handler, null);
            if (countdownTicks != 1 || resetTicks != 2 || openerResets != 2 || battleUpdates != 1 || diagnosticsUpdates != 1)
                throw new InvalidOperationException("Countdown / opener phase / reset wiring failed");
            var openerType = assembly.GetType("ViperACR.Host.ViperStandardOpener", true)!;
            if (!pr.GetType("PromeRotation.Rotation.IOpener", true)!.IsAssignableFrom(openerType))
                throw new InvalidOperationException("IOpener contract mismatch");
            uint[] expected = [34620,34647,34622,34637,34636,34621,34636,34637,34620,34621,34636,34637,
                34622,34637,34636,34626,34627,34640,34628,34641,34629,34642,34630,34643,34631,
                34633,34644,34645,34633,34644,34645,34607,34609,34613,34634];
            foreach (var skipFirst in new[] { false, true })
            foreach (var potion in new uint[] { 0, 999999 })
            foreach (var firstFlank in new[] { false, true })
            foreach (var secondFlank in new[] { false, true })
            {
                var opener = Activator.CreateInstance(openerType, skipFirst, potion)!;
                var steps = ((System.Collections.IEnumerable)openerType.GetProperty("InCombatSequence")!.GetValue(opener)!).Cast<object>().ToList();
                var nextPhase = openerType.GetMethod("NextPhase")!;
                var phaseNumber = 0;
                foreach (var combo in new[] { 1, firstFlank ? 2 : 3, 0, 1, secondFlank ? 2 : 3, 0 })
                {
                    var phaseState = Activator.CreateInstance(snapshotType)!;
                    snapshotType.GetProperty("Active")!.SetValue(phaseState, true);
                    snapshotType.GetProperty("HasTarget")!.SetValue(phaseState, true);
                    snapshotType.GetProperty("DreadCombo")!.SetValue(phaseState, combo);
                    snapshotType.GetProperty("BuffTimes")!.SetValue(phaseState, new Dictionary<uint, float> { [3668] = 35, [3669] = 35 });
                    snapshotType.GetProperty("NeedsPositional")!.SetValue(phaseState, true);
                    var facing = assembly.GetType("ViperACR.Core.Facing", true)!;
                    snapshotType.GetProperty("Facing")!.SetValue(phaseState,
                        Enum.Parse(facing, (phaseNumber < 3 ? firstFlank : secondFlank) ? "Flank" : "Rear"));
                    var phase = nextPhase.Invoke(opener, [phaseState]) as System.Collections.IEnumerable
                        ?? throw new InvalidOperationException("Missing live opener phase");
                    steps.AddRange(phase.Cast<object>());
                    phaseNumber++;
                }
                uint Id(object a) => (uint)a.GetType().GetField("ActionId")!.GetValue(a)!;
                var adaptive = expected.ToArray();
                void CoilBlock(int index, bool hunter)
                {
                    adaptive[index] = hunter ? 34621u : 34622u;
                    adaptive[index + 1] = hunter ? 34636u : 34637u;
                    adaptive[index + 2] = hunter ? 34637u : 34636u;
                }
                CoilBlock(2, firstFlank); CoilBlock(5, !firstFlank);
                CoilBlock(9, secondFlank); CoilBlock(12, !secondFlank);
                var ids = adaptive.Skip(skipFirst ? 1 : 0).ToList();
                if (potion != 0) ids.Insert(skipFirst ? 1 : 2, potion);
                if (steps.Any(step => Id(step) == 7546)) throw new InvalidOperationException("True North must not occur in standard opener");
                if (!steps.Select(Id).SequenceEqual(ids)) throw new InvalidOperationException("Opener sequence/options mismatch");
                foreach (var step in steps)
                {
                    var stepType = step.GetType();
                    var type = stepType.GetField("Type")!.GetValue(step)!.ToString();
                    var verified = (bool)stepType.GetProperty("RequiresVerification")!.GetValue(step)!;
                    if (stepType.GetProperty("ExecutionGuard")!.GetValue(step) == null)
                        throw new InvalidOperationException("Every opener action requires a live attack range guard");
                    if (verified != (type == "Gcd")) throw new InvalidOperationException("GCD verification mismatch");
                    var self = Id(step) == 34647 || Id(step) == 34626 || Id(step) == potion;
                    if (stepType.GetField("Target")!.GetValue(step)!.ToString() != (self ? "Self" : "Target"))
                        throw new InvalidOperationException("Opener target mismatch");
                }
            }
            Console.WriteLine("Phased IOpener: 17 verified GCDs, live flank/rear choice at BOTH Vicewinders, paired bites, optional potion, NO True North, first-hit resume: PASS (16 variants).");
            var teaType = assembly.GetType("ViperACR.Host.ViperTeaOpener", true)!;
            var registeredOpeners = (IReadOnlyDictionary<string, Type>)rotationType.GetProperty("Openers")!.GetValue(null)!;
            if (!registeredOpeners.TryGetValue("80级绝亚起手", out var teaRegistered) || teaRegistered != teaType ||
                !pr.GetType("PromeRotation.Rotation.IOpener", true)!.IsAssignableFrom(teaType))
                throw new InvalidOperationException("Missing level-80 TEA opener registration");
            uint[] teaExpected = [34607,34609,34620,34621,34636,34637,34622,34637,34636,
                34612,34634,34606,34608,34620,34622,34637,34636,34621,34636,34637];
            foreach (var skipFirst in new[] { false, true })
            foreach (var potion in new uint[] { 0, 999999 })
            {
                var tea = Activator.CreateInstance(teaType, skipFirst, potion)!;
                var teaSteps = ((System.Collections.IEnumerable)teaType.GetProperty("InCombatSequence")!.GetValue(tea)!).Cast<object>().ToArray();
                var ids = teaExpected.Skip(skipFirst ? 1 : 0).ToList();
                if (potion != 0) ids.Insert(skipFirst ? 1 : 2, potion);
                uint TeaId(object step) => (uint)step.GetType().GetField("ActionId")!.GetValue(step)!;
                if (!teaSteps.Select(TeaId).SequenceEqual(ids)) throw new InvalidOperationException("TEA opener sequence mismatch");
                var gcdCount = 0;
                foreach (var step in teaSteps)
                {
                    var t = step.GetType();
                    var kind = t.GetField("Type")!.GetValue(step)!.ToString();
                    if (kind == "Gcd") gcdCount++;
                    if ((bool)t.GetProperty("RequiresVerification")!.GetValue(step)! != (kind == "Gcd") ||
                        t.GetProperty("ExecutionGuard")!.GetValue(step) == null ||
                        t.GetField("Target")!.GetValue(step)!.ToString() != (TeaId(step) == potion ? "Self" : "Target") ||
                        (TeaId(step) == potion && kind != "Item"))
                        throw new InvalidOperationException("TEA opener action contract mismatch");
                }
                if (gcdCount != (skipFirst ? 10 : 11)) throw new InvalidOperationException("TEA GCD count mismatch");
            }
            Console.WriteLine("Level-80 TEA IOpener: 11 verified GCDs, fixed coils/bites, Hindsting, potion after GCD2 and first-hit resume: PASS (4 variants).");
            var teaSettings = JsonSerializer.Deserialize("{\"Opener\":2}", settings.GetType())!;
            var restoredTeaSettings = JsonSerializer.Deserialize(JsonSerializer.Serialize(teaSettings, settings.GetType()), settings.GetType())!;
            if (teaSettings.GetType().GetProperty("Opener")!.GetValue(teaSettings)!.ToString() != "Tea80" ||
                restoredTeaSettings.GetType().GetProperty("Opener")!.GetValue(restoredTeaSettings)!.ToString() != "Tea80")
                throw new InvalidOperationException("TEA opener configuration compatibility failed");
            Console.WriteLine("QT names, removed countdown QT and IRotationEventHandler countdown/reset callbacks passed.");
            var dsrType = assembly.GetType("ViperACR.Host.ViperDsrOpener", true)!;
            if (!registeredOpeners.TryGetValue("90级绝龙诗起手", out var dsrRegistered) || dsrRegistered != dsrType ||
                !pr.GetType("PromeRotation.Rotation.IOpener", true)!.IsAssignableFrom(dsrType) ||
                Activator.CreateInstance(dsrType) == null)
                throw new InvalidOperationException("Missing level-90 DSR opener registration/default constructor");
            uint[] dsrExpected = [34607,34647,34609,34620,7546,34621,34636,34637,34622,34637,34636,
                34626,34627,34628,34629,34630,34620,34621,34636,34637,34622,34637,34636,34613,34634,34633];
            foreach (var skipFirst in new[] { false, true })
            foreach (var potion in new uint[] { 0, 999999 })
            foreach (var trueNorth in new[] { false, true })
            {
                var dsr = Activator.CreateInstance(dsrType, skipFirst, potion, trueNorth)!;
                var dsrSteps = ((System.Collections.IEnumerable)dsrType.GetProperty("InCombatSequence")!.GetValue(dsr)!).Cast<object>().ToArray();
                var ids = dsrExpected.Skip(skipFirst ? 1 : 0).Where(id => trueNorth || id != 7546).ToList();
                if (potion != 0) ids.Insert(skipFirst ? 2 : 3, potion);
                uint DsrId(object step) => (uint)step.GetType().GetField("ActionId")!.GetValue(step)!;
                if (!dsrSteps.Select(DsrId).SequenceEqual(ids)) throw new InvalidOperationException("DSR opener sequence mismatch");
                var gcdCount = 0;
                foreach (var step in dsrSteps)
                {
                    var t = step.GetType();
                    var id = DsrId(step);
                    var kind = t.GetField("Type")!.GetValue(step)!.ToString();
                    var expectedKind = id == potion ? "Item" : id is 34647 or 7546 or 34634 or 34636 or 34637 ? "OffGcd" : "Gcd";
                    var self = id == potion || id is 34647 or 34626 or 7546;
                    if (kind == "Gcd") gcdCount++;
                    if (kind != expectedKind ||
                        (bool)t.GetProperty("RequiresVerification")!.GetValue(step)! != (kind == "Gcd") ||
                        t.GetProperty("ExecutionGuard")!.GetValue(step) == null ||
                        t.GetField("Target")!.GetValue(step)!.ToString() != (self ? "Self" : "Target"))
                        throw new InvalidOperationException("DSR opener action contract mismatch");
                }
                if (gcdCount != (skipFirst ? 14 : 15)) throw new InvalidOperationException("DSR GCD count mismatch");
            }
            var dsrSettings = JsonSerializer.Deserialize("{\"Opener\":3}", settings.GetType())!;
            var restoredDsrSettings = JsonSerializer.Deserialize(JsonSerializer.Serialize(dsrSettings, settings.GetType()), settings.GetType())!;
            if (dsrSettings.GetType().GetProperty("Opener")!.GetValue(dsrSettings)!.ToString() != "Dsr90" ||
                restoredDsrSettings.GetType().GetProperty("Opener")!.GetValue(restoredDsrSettings)!.ToString() != "Dsr90")
                throw new InvalidOperationException("DSR opener configuration compatibility failed");
            Console.WriteLine("Level-90 DSR IOpener: 15 verified GCDs, fixed coils/bites, four Generations, correct self targets, potion/True North options, first-hit resume: PASS (8 variants).");
            var fruType = assembly.GetType("ViperACR.Host.ViperFruOpener", true)!;
            if (!registeredOpeners.TryGetValue("100级绝妖星起手", out var fruRegistered) || fruRegistered != fruType ||
                !pr.GetType("PromeRotation.Rotation.IOpener", true)!.IsAssignableFrom(fruType) ||
                Activator.CreateInstance(fruType) == null)
                throw new InvalidOperationException("Missing level-100 FRU opener registration/default constructor");
            uint[] fruExpected = [34607,34647,34609,34620,34621,34636,34637,34622,34637,34636,
                34626,34627,34640,34628,34641,34629,34642,34630,34643,34631,34613,34634];
            foreach (var skipFirst in new[] { false, true })
            {
                var fru = Activator.CreateInstance(fruType, new object[] { skipFirst })!;
                var fruSteps = ((System.Collections.IEnumerable)fruType.GetProperty("InCombatSequence")!.GetValue(fru)!).Cast<object>().ToArray();
                uint FruId(object step) => (uint)step.GetType().GetField("ActionId")!.GetValue(step)!;
                if (!fruSteps.Select(FruId).SequenceEqual(fruExpected.Skip(skipFirst ? 1 : 0)))
                    throw new InvalidOperationException("FRU opener sequence mismatch");
                var gcdCount = 0;
                foreach (var step in fruSteps)
                {
                    var t = step.GetType();
                    var id = FruId(step);
                    var kind = t.GetField("Type")!.GetValue(step)!.ToString();
                    var expectedKind = id is 34647 or 34634 or 34636 or 34637 or >= 34640 and <= 34643 ? "OffGcd" : "Gcd";
                    if (kind == "Gcd") gcdCount++;
                    if (kind != expectedKind ||
                        (bool)t.GetProperty("RequiresVerification")!.GetValue(step)! != (kind == "Gcd") ||
                        t.GetProperty("ExecutionGuard")!.GetValue(step) == null ||
                        t.GetField("Target")!.GetValue(step)!.ToString() != (id is 34647 or 34626 ? "Self" : "Target"))
                        throw new InvalidOperationException("FRU opener action contract mismatch");
                }
                if (gcdCount != (skipFirst ? 11 : 12)) throw new InvalidOperationException("FRU GCD count mismatch");
            }
            var selection = assembly.GetType("ViperACR.Host.OpenerSelection", true)!;
            var labels = (string[])selection.GetField("Labels")!.GetValue(null)!;
            if (!labels.SequenceEqual(new[] { "100级标准起手", "100级绝妖星起手", "90级绝龙诗起手", "80级绝亚起手" }) ||
                !registeredOpeners.Values.Select(t => t.Name).SequenceEqual(new[] { "ViperStandardOpener", "ViperFruOpener", "ViperDsrOpener", "ViperTeaOpener" }))
                throw new InvalidOperationException("Opener display/registration order mismatch");
            foreach (var (saved, index, mode) in new[] { (0, 0, "Standard"), (4, 1, "Fru100"), (3, 2, "Dsr90"), (2, 3, "Tea80"), (1, 0, "Standard"), (99, 0, "Standard") })
            {
                var savedSettings = JsonSerializer.Deserialize("{\"Opener\":" + saved + "}", settings.GetType())!;
                var oldMode = savedSettings.GetType().GetProperty("Opener")!.GetValue(savedSettings)!;
                var actualIndex = (int)selection.GetMethod("ToIndex")!.Invoke(null, [oldMode])!;
                var actualMode = selection.GetMethod("FromIndex")!.Invoke(null, [actualIndex])!;
                savedSettings.GetType().GetProperty("Opener")!.SetValue(savedSettings, actualMode);
                var restored = JsonSerializer.Deserialize(JsonSerializer.Serialize(savedSettings, settings.GetType()), settings.GetType())!;
                if (actualIndex != index || actualMode.ToString() != mode ||
                    restored.GetType().GetProperty("Opener")!.GetValue(restored)!.ToString() != mode)
                    throw new InvalidOperationException("Opener sort changed persisted choice / fallback failed");
            }
            Console.WriteLine("Level-100 FRU IOpener: 12 verified GCDs, fixed coils/bites, paired Legacies, no potion/True North, first-hit resume: PASS (2 variants).");
            Console.WriteLine("Four opener display/registration order and persisted selection compatibility/fallback: PASS (6 cases).");
            registry.GetMethod("ValidateExternalPackage", privateStatic)!.Invoke(null, [Path.GetFileName(folder), candidates, false]);
            Console.WriteLine($"PASS: PR {pr.GetName().Version} accepted package {Path.GetFileName(folder)}; rotations={candidates.Count}");
            foreach (var candidate in candidates)
                Console.WriteLine($"  Job={registrationType.GetProperty("JobId")!.GetValue(candidate)}, Author={registrationType.GetProperty("Author")!.GetValue(candidate)}, Version={registrationType.GetProperty("Version")!.GetValue(candidate)}");
            Console.WriteLine("Actual PR main-DLL resolution, collectible loader, metadata, constructor, static QT/Openers and package validation passed.");
            Console.WriteLine("No registration committed; no rotation instantiated; no game actions sent.");
        }
        finally { loader.Unload(); }
    }
    catch (Exception ex)
    {
        Console.WriteLine("FAIL: " + ex.GetBaseException().Message);
        Environment.ExitCode = 1;
    }
    return;
}
if (args.Length >= 2 && args[0] == "--verify")
{
    var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(args[1]));
    var pr = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(plugin, "PromeRotation.dll"));
    var contract = pr.GetType("PromeRotation.Rotation.IRotation", true)!;
    var rotation = assembly.GetType("ViperACR.Host.ViperRotation", true)!;
    if (!contract.IsAssignableFrom(rotation)) throw new Exception("IRotation contract mismatch");
    var map = rotation.GetInterfaceMap(contract);
    if (rotation.GetConstructor(Type.EmptyTypes) == null) throw new Exception("Missing public default constructor");
    var metadata = rotation.GetCustomAttributesData().Single(x => x.AttributeType.Name == "RotationMetadataAttribute");
    if (!Equals(metadata.ConstructorArguments[0].Value, 41u)) throw new Exception("Incorrect job ID");
    foreach (var name in new[] { "QtList", "Openers" })
        if (rotation.GetProperty(name, BindingFlags.Static | BindingFlags.Public) == null) throw new Exception("Missing " + name);
    var handler = assembly.GetType("ViperACR.Host.ViperEventHandler", true)!;
    handler.GetInterfaceMap(pr.GetType("PromeRotation.Rotation.IRotationEventHandler", true)!);
    Console.WriteLine($"Verified: {assembly.GetName().Name}, Job=41, PR={pr.GetName().Version}");
    Console.WriteLine($"IRotation methods={map.TargetMethods.Length}; public constructor; metadata; QT/Openers; event handler: PASS");
    Console.WriteLine("Metadata-only verification: no plugin was instantiated and no game actions were sent.");
    return;
}
if (args.Length >= 2 && args[0] == "--validate-opener-script")
{
    var pr = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(plugin, "PromeRotation.dll"));
    var script = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(args[1]));
    var type = script.GetType("VPR_100_Standard", true)!;
    if (!pr.GetType("PromeRotation.Rotation.IOpener", true)!.IsAssignableFrom(type)) throw new Exception("Script IOpener mismatch");
    var opener = Activator.CreateInstance(type)!;
    var scriptActions = ((System.Collections.IEnumerable)type.GetProperty("InCombatSequence")!.GetValue(opener)!).Cast<object>().ToArray();
    uint[] expected = [34620,34647,34622,34637,34636,34621,34636,34637,34620,34621,34636,34637,
        34622,34637,34636,34626,34627,34640,34628,34641,34629,34642,34630,34643,34631,
        34633,34644,34645,34633,34644,34645,34607,34609,34613,34634];
    if (!scriptActions.Select(a => (uint)a.GetType().GetField("ActionId")!.GetValue(a)!).SequenceEqual(expected))
        throw new Exception("Standalone script differs from standard opener sequence");
    if (scriptActions.Count(a => (bool)a.GetType().GetProperty("RequiresVerification")!.GetValue(a)!) != 17)
        throw new Exception("Standalone script GCD verification mismatch");
    if (scriptActions.Any(a => a.GetType().GetProperty("ExecutionGuard")!.GetValue(a) == null))
        throw new Exception("Standalone script must guard each action with native/Long Arms range");
    var countdownType = pr.GetType("PromeRotation.Rotation.CountDownHandler", true)!;
    var countdown = Activator.CreateInstance(countdownType)!;
    type.GetMethod("InitializeCountdown")!.Invoke(opener, [countdown]);
    var scheduled = ((System.Collections.IEnumerable)countdownType.GetProperty("ScheduledActions", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(countdown)!).Cast<object>().ToArray();
    if (scheduled.Length != 1 || (int)scheduled[0].GetType().GetField("Item1")!.GetValue(scheduled[0])! != 500)
        throw new Exception("Standalone script countdown must be 500ms");
    Console.WriteLine("Standalone TXT: actual IOpener, standard sequence, 17 verified GCDs and 500ms scheduled Slither: PASS.");
    Console.WriteLine("No countdown factory evaluated; no game actions sent.");
    return;
}
if (args.Length > 0 && args[0] == "--inspect-dummy-names")
{
    using var nameData = new GameData(ClientDataPath(), new LuminaOptions {
        PanicOnSheetChecksumMismatch = true, DefaultExcelLanguage = Language.ChineseSimplified });
    foreach (var row in nameData.Excel.GetSheet<BNpcName>()!.Where(row => row.RowId == 541 || row.Singular.ExtractText().Contains("木人")))
        Console.WriteLine($"{row.RowId}: {row.Singular.ExtractText()}");
    return;
}
if (args.Length > 0 && args[0] == "--limit-break-data")
{
    using var lbData = new GameData(ClientDataPath(), new LuminaOptions {
        PanicOnSheetChecksumMismatch = true, DefaultExcelLanguage = Language.ChineseSimplified });
    foreach (var action in lbData.Excel.GetSheet<Lumina.Excel.Sheets.Action>()!.Where(a => a.RowId is 200 or 201 or 34866))
        Console.WriteLine($"{action.RowId}: {action.Name.ExtractText()} icon={action.Icon} category={action.ActionCategory.RowId} job={action.ClassJob.RowId}");
    return;
}
if (args.Length >= 2 && args[0] == "--export-viper-icons")
{
    // Export client action icons for action identification.
    using var iconData = new GameData(ClientDataPath(), new LuminaOptions {
        PanicOnSheetChecksumMismatch = true, DefaultExcelLanguage = Language.ChineseSimplified });
    var sheet = iconData.Excel.GetSheet<Lumina.Excel.Sheets.Action>()!;
    Directory.CreateDirectory(args[1]);
    foreach (var action in sheet.Where(a => a.RowId is >= 34606 and <= 34647 || a.RowId is 3 or 7546))
    {
        var icon = (uint)action.Icon;
        var tex = iconData.GetFile<Lumina.Data.Files.TexFile>($"ui/icon/{icon / 1000 * 1000:D6}/{icon:D6}.tex")!;
        var pixels = tex.ImageData;
        var width = tex.Header.Width;
        var height = tex.Header.Height;
        using var writer = new BinaryWriter(File.Create(Path.Combine(args[1], $"{action.RowId}.bmp")));
        writer.Write((ushort)0x4d42); writer.Write(54 + pixels.Length); writer.Write(0); writer.Write(54);
        writer.Write(40); writer.Write((int)width); writer.Write(-(int)height); writer.Write((ushort)1);
        writer.Write((ushort)32); writer.Write(0); writer.Write(pixels.Length);
        writer.Write(0); writer.Write(0); writer.Write(0); writer.Write(0); writer.Write(pixels);
        Console.WriteLine($"{action.RowId} {action.Name.ExtractText()} icon={icon}");
    }
    return;
}
var sqpack = Path.GetFullPath(args.Length > 0 ? args[0] : ClientDataPath());
using var data = new GameData(sqpack, new LuminaOptions { PanicOnSheetChecksumMismatch = true, DefaultExcelLanguage = Language.ChineseSimplified });
Console.WriteLine("Languages: " + string.Join(",", Enum.GetNames<Language>()));
var language = Enum.TryParse<Language>("ChineseSimplified", out var chinese) ? chinese : Language.English;
Console.WriteLine($"Repositories={data.Repositories.Count}; Sheets={data.Excel.SheetNames.Count}; Action={data.Excel.SheetNames.Contains("Action")}");
Console.WriteLine($"Action.exh={data.GetFile("exd/action.exh")?.Data.Length}; Language={language} ({(int)language})");
var header = data.GetFile<Lumina.Data.Files.Excel.ExcelHeaderFile>("exd/action.exh")!;
Console.WriteLine("Header languages: " + string.Join(",", header.Languages.Select(x => $"{x}={(int)x}")));
var actions = data.Excel.GetSheet<Lumina.Excel.Sheets.Action>(language, "Action")!;
var statuses = data.Excel.GetSheet<Status>(language, "Status")!;
var result = new {
    GameVersion = File.ReadAllText(Path.Combine(Directory.GetParent(sqpack)!.FullName, "ffxivgame.ver")).Trim(),
    Language = language.ToString(),
    Actions = actions.Where(a => !a.IsPvP && (a.ClassJob.RowId == 41 || a.RowId is >= 34606 and <= 34647 || a.RowId is 7541 or 7542 or 7546 or 7549)).Select(a => new {
        Id = a.RowId, Name = a.Name.ExtractText(), Level = a.ClassJobLevel, Range = a.Range, Radius = a.EffectRange,
        Category = a.ActionCategory.RowId, Recast = a.Recast100ms / 10f, Group = a.CooldownGroup, Charges = a.MaxCharges,
        Self = a.CanTargetSelf, Hostile = a.CanTargetHostile, Combo = a.ActionCombo.RowId, Status = a.StatusGainSelf.RowId,
        PreservesCombo = a.PreservesCombo
    }).ToArray(),
    Statuses = statuses.Where(s => s.RowId is >= 3645 and <= 3675 || s.RowId is >= 3770 and <= 3780 || s.RowId == 1250).Select(s => new { Id = s.RowId, Name = s.Name.ExtractText(), Description = s.Description.ExtractText() }).ToArray()
};
var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
File.WriteAllText(args.Length > 1 ? args[1] : "game-data.json", json);
Console.WriteLine(json);

static string ClientDataPath() => Environment.GetEnvironmentVariable("FFXIV_SQPACK")
    ?? throw new InvalidOperationException("Set FFXIV_SQPACK to the game's sqpack directory, or pass the directory as the first export argument.");
