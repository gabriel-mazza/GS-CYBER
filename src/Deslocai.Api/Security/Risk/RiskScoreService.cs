using Deslocai.Security.Taint;

namespace Deslocai.Security.Risk;

public record EndpointRisk(string Endpoint, double Score, string Level, int OpenFlows, int MitigatedFlows, double Raw);
public record RiskReport(double Score, string Level, int OpenFindings, int MitigatedFindings, int ResolvedFindings,
                         IReadOnlyList<EndpointRisk> Endpoints, IReadOnlyList<RiskPoint> Timeline, IReadOnlyDictionary<string, int> ByPiiType);
public record RiskPoint(DateTime At, double Score);


public class RiskScoreService
{
    private readonly FindingStore _store;
    private readonly List<RiskPoint> _timeline = new();
    private readonly object _lock = new();

    public RiskScoreService(FindingStore store)
    {
        _store = store;
        _store.Changed += () => { var s = Compute(_store.Snapshot()).Score; lock (_lock) { _timeline.Add(new RiskPoint(DateTime.UtcNow, s)); if (_timeline.Count > 200) _timeline.RemoveAt(0); } };
    }

    public static double SinkFactor(SinkKind s) => s switch { SinkKind.HttpOutbound => 3.0, SinkKind.HttpResponse => 2.5, SinkKind.File => 2.0, SinkKind.Log => 1.5, SinkKind.Database => 1.0, _ => 1.0 };

    public static double FlowRaw(Finding f)
    {
        if (f.Status != "Open" || f.Sanitized) return 0;
        if (f.Kind == "SQL_INJECTION") return 6 * SinkFactor(f.Sink) * 1.5;
        var weights = f.PiiTypes.Select(Pii.PiiCatalog.Weight).OrderByDescending(w => w).ToList();
        if (weights.Count == 0) return 0;
        double sens = weights[0] + 0.5 * (weights.Count - 1);
        double group = f.VulnerableGroup ? 1.5 : 1.0;                
        double freq = 1 + Math.Min(1.0, Math.Log10(1 + f.Count) / 2); 
        return sens * SinkFactor(f.Sink) * group * freq;
    }

    public static double ToScore(double raw) => Math.Round(100 * (1 - Math.Exp(-raw / 25)), 1);
    public static string Level(double score) => score < 25 ? "Baixo" : score < 50 ? "Moderado" : score < 75 ? "Alto" : "Crítico";

    public RiskReport Compute() => Compute(_store.Snapshot());

    private RiskReport Compute(IReadOnlyList<Finding> all)
    {
        var endpoints = all.GroupBy(f => f.Endpoint).Select(g =>
        {
            double raw = g.Sum(FlowRaw); var s = ToScore(raw);
            return new EndpointRisk(g.Key, s, Level(s), g.Count(f => f.Status == "Open"), g.Count(f => f.Sanitized), raw);
        }).OrderByDescending(e => e.Score).ToList();

        double total = endpoints.Sum(e => e.Raw); var score = ToScore(total);
        var byType = all.Where(f => f.Status == "Open" && !f.Sanitized).SelectMany(f => f.PiiTypes).GroupBy(t => t.ToString()).ToDictionary(g => g.Key, g => g.Count());
        List<RiskPoint> tl; lock (_lock) tl = _timeline.ToList();
        return new RiskReport(score, Level(score), all.Count(f => f.Status == "Open" && !f.Sanitized), all.Count(f => f.Sanitized),
                              all.Count(f => f.Status == "Resolved"), endpoints, tl, byType);
    }

    public void ResetTimeline() { lock (_lock) _timeline.Clear(); }
}
