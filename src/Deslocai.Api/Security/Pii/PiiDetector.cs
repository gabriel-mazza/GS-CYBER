using System.Text;
using System.Text.RegularExpressions;

namespace Deslocai.Security.Pii;


public class PiiDetector
{
    private record Rule(PiiType Type, Regex Regex, Func<string, bool>? Validate, double BaseConfidence, int Priority, int ValueGroup = 0);

    private const RegexOptions Opt = RegexOptions.Compiled | RegexOptions.CultureInvariant;
    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(250); 

    private static Regex R(string p, RegexOptions extra = RegexOptions.None) => new(p, Opt | extra, Timeout);

    private static readonly Rule[] Rules =
    {
        new(PiiType.Cnpj,   R(@"(?<![\d./-])\d{2}\.?\d{3}\.?\d{3}/?\d{4}-?\d{2}(?![\d])"), Validators.IsValidCnpj, 0.99, 1),
        new(PiiType.Cartao, R(@"(?<!\d)\d{4}[ -]?\d{4}[ -]?\d{4}[ -]?\d{1,7}(?!\d)"),        Validators.IsValidLuhn, 0.97, 2),
        new(PiiType.Cpf,    R(@"(?<![\d./-])\d{3}\.?\d{3}\.?\d{3}-?\d{2}(?![\d])"),           Validators.IsValidCpf, 0.99, 3),
        new(PiiType.Email,  R(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9\-]+(?:\.[A-Za-z0-9\-]+)*\.[A-Za-z]{2,}"), null, 0.98, 4),
        new(PiiType.Rg,     R(@"\bR\.?G\.?[:\s]*(\d{1,2}\.?\d{3}\.?\d{3}-?[0-9Xx])", RegexOptions.IgnoreCase), null, 0.85, 5, 1),
        new(PiiType.Nascimento, R(@"(?:nasc(?:imento)?|data\s+de\s+nasc\w*|dn)[:\s]*(\d{2}[/\-.]\d{2}[/\-.]\d{4})", RegexOptions.IgnoreCase), Validators.IsPlausibleDate, 0.9, 5, 1),
        new(PiiType.Bilhete, R(@"(?:bilhete(?:\s+[uú]nico)?|cart[aã]o\s+(?:bom|sptrans))[:\s#nº]*(\d{8,16})", RegexOptions.IgnoreCase), null, 0.85, 5, 1),
        new(PiiType.Geolocalizacao, R(@"-?\d{1,2}\.\d{4,}\s*,\s*-?\d{1,3}\.\d{4,}"), null, 0.9, 6),
        new(PiiType.Telefone, R(@"(?<![\d])(?:\+?55[\s-]?)?\(?\d{2}\)?[\s-]?9?\d{4}[\s-]?\d{4}(?![\d])"), null, 0.75, 7),
        new(PiiType.Placa,  R(@"\b[A-Z]{3}-?\d[A-Z0-9]\d{2}\b"), null, 0.7, 8),
        new(PiiType.Cep,    R(@"(?<![\d])\d{5}-\d{3}(?![\d])"), null, 0.7, 9),
        new(PiiType.Laudo, R(@"\bCID[-\s]?(?:10)?[-\s:]*[A-TV-Z]\d{2}(?:\.\d{1,2})?\b", RegexOptions.IgnoreCase), null, 0.9, 6),
        new(PiiType.SaudePcd, R(@"\b(?:pcd|cadeirante|defici[eê]ncia\s+(?:f[ií]sica|visual|auditiva|intelectual)|mobilidade\s+reduzida|idos[oa]|autis\w+|cego|surdo|laudo|diagn[oó]stico|prontu[aá]rio)\b", RegexOptions.IgnoreCase), null, 0.8, 10),
    };

    public PiiScanResult Scan(string? text)
    {
        if (string.IsNullOrEmpty(text)) return new PiiScanResult(text ?? "", text ?? "", Array.Empty<PiiMatch>());

        var candidates = new List<(PiiMatch Match, int Priority)>();
        foreach (var rule in Rules)
        {
            try
            {
                foreach (Match m in rule.Regex.Matches(text))
                {
                    var g = rule.ValueGroup > 0 ? m.Groups[rule.ValueGroup] : m.Groups[0];
                    if (!g.Success) continue;
                    bool validated = rule.Validate is not null;
                    if (validated && !rule.Validate!(g.Value)) continue;
                    candidates.Add((new PiiMatch(rule.Type, g.Index, g.Length, g.Value, rule.BaseConfidence, validated,
                        PiiCatalog.Level(rule.Type), PiiCatalog.Weight(rule.Type)), rule.Priority));
                }
            }
            catch (RegexMatchTimeoutException) {  }
        }

        
        var chosen = new List<PiiMatch>();
        foreach (var (match, _) in candidates.OrderBy(c => c.Priority).ThenByDescending(c => c.Match.Length))
            if (!chosen.Any(c => match.Start < c.End && c.Start < match.End)) chosen.Add(match);

        var ordered = chosen.OrderBy(m => m.Start).ToList();
        return new PiiScanResult(text, Mask(text, ordered), ordered);
    }

    public static string Mask(string text, IEnumerable<PiiMatch> matches)
    {
        var sb = new StringBuilder();
        int pos = 0;
        foreach (var m in matches.OrderBy(m => m.Start))
        {
            sb.Append(text, pos, m.Start - pos).Append(MaskValue(m.Type, m.Value));
            pos = m.End;
        }
        return sb.Append(text, pos, text.Length - pos).ToString();
    }

    public static string MaskValue(PiiType type, string v)
    {
        var d = Validators.Digits(v);
        string Last(int n) => d.Length >= n ? d[^n..] : "";
        return type switch
        {
            PiiType.Cpf => $"***.***.***-{Last(2)}",
            PiiType.Cnpj => $"**.***.***/****-{Last(2)}",
            PiiType.Cartao => $"**** **** **** {Last(4)}",
            PiiType.Telefone => $"(**) *****-{Last(4)}",
            PiiType.Email => v.IndexOf('@') is var i and > 0 ? $"{v[0]}***{v[i..]}" : "[EMAIL]",
            PiiType.Placa => "***-****",
            PiiType.Cep => "*****-***",
            PiiType.Rg => "**.***.***-*",
            PiiType.Nascimento => "**/**/****",
            PiiType.Geolocalizacao => "[LOCALIZACAO]",
            PiiType.Bilhete => $"********{Last(3)}",
            PiiType.Senha => "[SENHA]",
            PiiType.Laudo => "[CID]",
            _ => "[DADO_SENSIVEL]",
        };
    }
}
