namespace Deslocai.Security.Taint;

public class FindingStore
{
    private readonly object _lock = new();
    private readonly List<Finding> _findings = new();
    private readonly List<(DateTime At, string Label)> _events = new();
    public event Action? Changed;

    public IReadOnlyList<Finding> Snapshot() { lock (_lock) return _findings.Select(Clone).ToList(); }

    
    public Finding Record(Finding f)
    {
        lock (_lock)
        {
            var existing = _findings.FirstOrDefault(x => x.Key == f.Key);
            if (existing is null) { _findings.Add(f); existing = f; }
            else
            {
                existing.Count++;
                existing.LastSeen = DateTime.UtcNow;
                existing.EvidenceMasked = f.EvidenceMasked;
                if (!f.Sanitized && existing.Status == "Resolved") existing.Status = "Open";
            }
            _events.Add((DateTime.UtcNow, f.Sanitized ? "mitigated" : "leak"));
            Changed?.Invoke();
            return existing;
        }
    }

    public Finding? Get(string id) { lock (_lock) return _findings.FirstOrDefault(f => f.Id == id); }

    public bool Resolve(string id)
    {
        lock (_lock)
        {
            var f = _findings.FirstOrDefault(x => x.Id == id);
            if (f is null) return false;
            f.Status = "Resolved";
            Changed?.Invoke();
            return true;
        }
    }

    public void SetRemediation(string id, string text) { lock (_lock) { var f = _findings.FirstOrDefault(x => x.Id == id); if (f != null) f.Remediation = text; } }

    public void Reset() { lock (_lock) { _findings.Clear(); _events.Clear(); Changed?.Invoke(); } }

    private static Finding Clone(Finding f) => f.Copy();
}
