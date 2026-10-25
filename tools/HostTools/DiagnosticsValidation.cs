using System.Collections;
using System.Reflection;
using System.Text.Json;

internal static class DiagnosticsValidation
{
    public static void Run(Assembly assembly, Assembly pr, object settings)
    {
        const BindingFlags hidden = BindingFlags.NonPublic | BindingFlags.Instance;
        var settingsType = settings.GetType();
        foreach (var name in new[] { "CombatLogRecordEnabled", "ShowTargetLine" })
        {
            var property = settingsType.GetProperty(name)!;
            if ((bool)property.GetValue(settings)!) throw new Exception($"{name} must default Off");
            foreach (var enabled in new[] { true, false })
            {
                property.SetValue(settings, enabled);
                var restored = JsonSerializer.Deserialize(JsonSerializer.Serialize(settings, settingsType), settingsType)!;
                if ((bool)property.GetValue(restored)! != enabled) throw new Exception($"{name} persistence failed");
            }
        }
        settingsType.GetProperty("CombatLogRecordEnabled")!.SetValue(settings, true);
        var type = assembly.GetType("ViperACR.Host.ViperDiagnostics", true)!;
        var recorder = Activator.CreateInstance(type, settings)!;
        void Field(string name, object value) => type.GetField(name, hidden)!.SetValue(recorder, value);
        int Pending() => (int)type.GetField("_pendingCount", hidden)!.GetValue(recorder)!;
        void Receive(object value) => type.GetMethod("Receive", hidden)!.Invoke(recorder, [value]);
        object Event(string name, params (string name, object value)[] properties)
        {
            var eventType = pr.GetType("PromeRotation.LogSystem.LogSystem" + name + "Event", true)!;
            var value = Activator.CreateInstance(eventType)!;
            foreach (var property in properties) eventType.GetProperty(property.name)!.SetValue(value, property.value);
            return value;
        }
        Field("_playerId", 123u); Field("_acceptEvents", true);
        var ownAction = Event("ActionEffect", ("SourceId", 123UL), ("ActionId", 34636u), ("TargetId", 456UL));
        Receive(ownAction);
        Receive(Event("ActionEffect", ("SourceId", 999UL)));
        Receive(Event("StatusAdd", ("TargetId", 999UL)));
        if (Pending() != 1) throw new Exception("Diagnostics must exclude other players' actions/statuses");
        foreach (var value in new[] {
            Event("CastStart", ("SourceId", 123UL), ("ActionId", (ushort)34636)),
            Event("CancelCast", ("SourceId", 123UL)), Event("InterruptCast", ("SourceId", 123UL)),
            Event("StatusAdd", ("TargetId", 123UL), ("StatusId", 3668u), ("IsRefresh", true)),
            Event("StatusRemove", ("TargetId", 123UL), ("StatusId", 3668u)),
            Event("BattleStarted"), Event("BattleEnded"), Event("Territory"), Event("ChangeMap") }) Receive(value);
        if (Pending() != 10) throw new Exception("Missing one of ten actual host event contracts");
        var pending = ((IEnumerable)type.GetField("_pending", hidden)!.GetValue(recorder)!).Cast<object>().ToArray();
        foreach (var value in pending) type.GetMethod("Record", hidden)!.Invoke(recorder, [value]);
        var journal = type.GetField("Journal", hidden)!.GetValue(recorder)!;
        if ((int)journal.GetType().GetProperty("Count")!.GetValue(journal)! != 10)
            throw new Exception("Real PR event conversion lost records");
        var csv = (string)journal.GetType().GetMethod("ToCsv")!.Invoke(journal, null)!;
        if (!csv.Contains("刷新") || !csv.Contains("命中") || !csv.Contains("34636"))
            throw new Exception("Real PR event payload conversion failed");
        for (var i = 0; i < 1100; i++) Receive(ownAction);
        if (Pending() != 1024 || (int)type.GetField("_dropped", hidden)!.GetValue(recorder)! != 86)
            throw new Exception("Network event ingress is not bounded");
        var subscription = new Subscription();
        Field("_subscription", subscription);
        type.GetMethod("Stop", hidden)!.Invoke(recorder, null);
        Receive(ownAction);
        if (!subscription.Disposed || Pending() != 0) throw new Exception("Stop must dispose and reject late callbacks");
        Console.WriteLine("Diagnostics: ten actual PR event types, own-player filtering, CSV payloads, bounded ingress, subscription disposal/late callbacks and setting persistence: PASS (no game or UI services invoked).");
    }
    private sealed class Subscription : IDisposable
    {
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
    }
}
