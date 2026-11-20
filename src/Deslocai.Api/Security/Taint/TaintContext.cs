using System.Text.RegularExpressions;
using Deslocai.Security.Pii;

namespace Deslocai.Security.Taint;


public class TaintContext
{
    
    private static readonly Regex SecretKey = new(@"(?:^|[._\[])(?:senha|password|passwd|pwd|token|secret|segredo|api[_-]?key)(?:\]|\b)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));

    private readonly PiiDetector _detector;
    public TaintContext(PiiDetector detector) => _detector = detector;

    public List<TaintEntry> Entries { get; } = new();
    public HashSet<string> Sanitized { get; } = new();   
    public bool HasVulnerableGroup => Entries.Any(e => e.VulnerableGroup);

    
    public void AddSource(string source, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        bool secret = SecretKey.IsMatch(source);
        if (value.Length < (secret ? 3 : 4)) return;

        if (secret) { Entries.Add(new TaintEntry(source, value, new[] { PiiType.Senha }, false, true)); return; }

        var scan = _detector.Scan(value);
        var types = scan.Matches.Select(m => m.Type).Distinct().ToList();
        bool vuln = types.Contains(PiiType.SaudePcd) || types.Contains(PiiType.Laudo);
        Entries.Add(new TaintEntry(source, value, types, vuln, true));
       
        if (scan.Matches.Count > 0 && scan.Matches.Any(m => m.Value != value))
            foreach (var m in scan.Matches.Where(m => m.Type != PiiType.SaudePcd && m.Value != value))
                Entries.Add(new TaintEntry(source, m.Value, new[] { m.Type }, vuln, false));
    }

    
    public string Redact(string? text)
    {
        if (string.IsNullOrEmpty(text)) return text ?? "";
        var masked = _detector.Scan(text).Masked;
        foreach (var e in Entries.Where(e => e.PiiTypes.Contains(PiiType.Senha)))
            masked = masked.Replace(e.Value, PiiDetector.MaskValue(PiiType.Senha, e.Value), StringComparison.Ordinal);
        return masked;
    }

    
    public string Mask(string? value)
    {
        if (value is null) return "";
        var masked = Redact(value);
        foreach (var e in Entries.Where(e => e.PiiTypes.Count > 0 && value.Contains(e.Value, StringComparison.Ordinal)))
            Sanitized.Add(e.Source);
        return masked;
    }

   
    public string Pseudonymize(string? value, string salt = "deslocai")
    {
        if (value is null) return "";
        foreach (var e in Entries.Where(e => e.Value == value)) Sanitized.Add(e.Source);
        var h = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(salt + value));
        return "ps_" + Convert.ToHexString(h)[..16].ToLowerInvariant();
    }
}
