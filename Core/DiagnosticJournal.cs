using System.Globalization;
using System.Text;

namespace ViperACR.Core;

public sealed record DiagnosticEntry(DateTime Time, string Category, string Job, string Subject, string Event, string Detail);

// A bounded, independent history: render/export use snapshots and never hold a game callback lock.
public sealed class DiagnosticJournal
{
    public const int Capacity = 5000;
    private readonly object _gate = new();
    private readonly Queue<DiagnosticEntry> _entries = new();
    public int Count { get { lock (_gate) return _entries.Count; } }
    public void Add(DiagnosticEntry entry)
    {
        lock (_gate)
        {
            while (_entries.Count >= Capacity) _entries.Dequeue();
            _entries.Enqueue(entry);
        }
    }
    public DiagnosticEntry[] Snapshot() { lock (_gate) return _entries.ToArray(); }
    public void Clear() { lock (_gate) _entries.Clear(); }
    public string ToCsv()
    {
        var text = new StringBuilder("时间,分类,职业,对象,事件,详情\r\n");
        foreach (var row in Snapshot().OrderBy(row => row.Time))
            text.AppendLine(string.Join(",", new[] {
                row.Time.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture),
                row.Category, row.Job, row.Subject, row.Event, row.Detail }.Select(CsvCell)));
        return text.ToString();
    }
    private static string CsvCell(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
}

public sealed class DiagnosticStallWatch
{
    private long? _since, _lastWarning;
    public bool Observe(long now, bool stalled)
    {
        if (!stalled) { Reset(); return false; }
        if (_since is null || now < _since) { _since = now; _lastWarning = null; }
        if (now - _since < 4000 || _lastWarning is { } last && now - last < 5000) return false;
        _lastWarning = now;
        return true;
    }
    public void Reset() { _since = null; _lastWarning = null; }
}
