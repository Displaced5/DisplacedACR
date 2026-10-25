using System.Reflection;
using System.Text.Json;

internal static class BasicAutomationValidation
{
    public static void Run(Assembly assembly, Assembly pr)
    {
        var uiType = assembly.GetType("ErosUI.ErosUISettings", true)!;
        var settings = Activator.CreateInstance(uiType)!;
        var options = new JsonSerializerOptions { IncludeFields = true };
        foreach (var name in new[] { "DeathAutoResetQt", "OpenerAutoPull", "OpenerAutoReleaseHold" })
        {
            var field = uiType.GetField(name)!;
            if (!(bool)field.GetValue(settings)!) throw new Exception($"{name} must default On");
            var legacy = JsonSerializer.Deserialize("{}", uiType, options)!;
            if (!(bool)field.GetValue(legacy)!) throw new Exception($"{name} missing legacy default");
            foreach (var enabled in new[] { false, true })
            {
                field.SetValue(settings, enabled);
                var restored = JsonSerializer.Deserialize(JsonSerializer.Serialize(settings, uiType, options), uiType, options)!;
                if ((bool)field.GetValue(restored)! != enabled) throw new Exception($"{name} persistence mismatch");
            }
        }
        var prSettingsType = pr.GetType("PromeRotation.Data.PromeSettings", true)!;
        var prSettings = prSettingsType.GetProperty("Instance")!.GetValue(null)!;
        var live = (Dictionary<string, bool>)prSettingsType.GetProperty("QuickToggles")!.GetValue(prSettings)!;
        var hidden = (HashSet<string>)prSettingsType.GetProperty("HiddenQts")!.GetValue(prSettings)!;
        var before = new Dictionary<string, bool>(live);
        var hiddenBefore = hidden.ToArray();
        try
        {
            var defaults = (IReadOnlyDictionary<string, bool>)assembly.GetType("ViperACR.Host.ViperRotation", true)!.GetProperty("QtList")!.GetValue(null)!;
            foreach (var (key, value) in defaults) live[key] = !value;
            live["OtherJobQt"] = true;
            hidden.Add("爆发");
            var restore = uiType.GetMethod("RestoreQtSnapshot")!;
            restore.Invoke(settings, [false]);
            if (defaults.Any(pair => live[pair.Key] != pair.Value)) throw new Exception("Death reset factory QT defaults failed");
            var daily = (Dictionary<string, bool>)uiType.GetField("QtDailyDefaults")!.GetValue(settings)!;
            var high = (Dictionary<string, bool>)uiType.GetField("QtHighEndDefaults")!.GetValue(settings)!;
            daily["爆发"] = false; daily["自动治疗"] = true;
            high["爆发"] = true; high["蛇灵气"] = false;
            foreach (var isHigh in new[] { false, true })
            {
                foreach (var key in defaults.Keys) live[key] = !defaults[key];
                restore.Invoke(settings, [isHigh]);
                var saved = isHigh ? high : daily;
                if (defaults.Any(pair => live[pair.Key] != saved.GetValueOrDefault(pair.Key, pair.Value)))
                    throw new Exception("Death reset must restore saved current-mode QT defaults with factory fallback");
                if (!live["OtherJobQt"] || !hidden.Contains("爆发"))
                    throw new Exception("Death QT reset changed unrelated QT or visibility");
            }
        }
        finally
        {
            live.Clear(); foreach (var pair in before) live[pair.Key] = pair.Value;
            hidden.Clear(); foreach (var key in hiddenBefore) hidden.Add(key);
        }
        Console.WriteLine("Basic automation: three default-On/legacy/On-Off JSON settings; real PR QT reset to Daily/HighEnd saved defaults, factory fallback and hidden/unrelated QT preservation: PASS (no game services).");
    }
}
