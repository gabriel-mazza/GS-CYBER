using Deslocai.Security.Pii;

namespace Deslocai.Security.Taint;

public enum SinkKind { Log, Database, HttpOutbound, File, HttpResponse }

public class Finding
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Key { get; set; } = "";
    public string Kind { get; set; } = "PII_LEAK";            
    public string Endpoint { get; set; } = "";
    public string Source { get; set; } = "";                  
    public SinkKind Sink { get; set; }
    public List<PiiType> PiiTypes { get; set; } = new();
    public bool VulnerableGroup { get; set; }                
    public string EvidenceMasked { get; set; } = "";
    public string File { get; set; } = "";
    public int Line { get; set; }
    public string Member { get; set; } = "";
    public string CodeSnippet { get; set; } = "";
    public bool Sanitized { get; set; }                       
    public string Status { get; set; } = "Open";              
    public int Count { get; set; } = 1;
    public DateTime FirstSeen { get; set; } = DateTime.UtcNow;
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    public string? Remediation { get; set; }
    public Finding Copy() => (Finding)MemberwiseClone();
}

public record TaintEntry(string Source, string Value, IReadOnlyList<PiiType> PiiTypes, bool VulnerableGroup, bool IsWholeValue)
{
    public IEnumerable<string> Variants()
    {
        yield return Value;
        var digits = Validators.Digits(Value);
        if (PiiTypes.Count > 0 && digits.Length >= 8 && digits != Value) yield return digits;
        if (PiiTypes.Contains(PiiType.Email)) yield return Value.ToLowerInvariant();
    }
}


public class TaintBlockedException : Exception
{
    public Finding Finding { get; }
    public TaintBlockedException(Finding f)
        : base($"Vazamento de PII bloqueado: {string.Join(",", f.PiiTypes)} em {f.Sink} (endpoint {f.Endpoint})") => Finding = f;
}


public class SecurityMode
{
    public SecurityMode(IConfiguration config) => Block = string.Equals(config["Seguranca:ModoTaint"], "Bloqueio", StringComparison.OrdinalIgnoreCase);
    public volatile bool Block;
    public string Name => Block ? "Bloqueio" : "Auditoria";
}
