using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Deslocai.Security.Pii;

namespace Deslocai.Security.Taint;


public class SinkGuard
{
    private readonly TaintContext _ctx;
    private readonly FindingStore _store;
    private readonly IHttpContextAccessor _http;
    private readonly PiiDetector _detector;
    private readonly SecurityMode _mode;
    private readonly ILogger<SinkGuard> _logger;

    public SinkGuard(TaintContext ctx, FindingStore store, IHttpContextAccessor http, PiiDetector detector, SecurityMode mode, ILogger<SinkGuard> logger)
    { _ctx = ctx; _store = store; _http = http; _detector = detector; _mode = mode; _logger = logger; }

    
    public void Log(string message, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0, [CallerMemberName] string member = "")
    {
        var leaks = Inspect(SinkKind.Log, message, file, line, member);
        _logger.LogInformation("{Mensagem}", leaks.Count > 0 && _mode.Block ? _ctx.Redact(message) : message);
    }

    
    public void Sql(string commandText, object? parameters = null, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0, [CallerMemberName] string member = "")
        => Enforce(Inspect(SinkKind.Database, commandText, file, line, member));

    public void HttpOut(string url, string body, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0, [CallerMemberName] string member = "")
        => Enforce(Inspect(SinkKind.HttpOutbound, url + " " + body, file, line, member));

    public void WriteFile(string path, string content, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0, [CallerMemberName] string member = "")
        => Enforce(Inspect(SinkKind.File, content, file, line, member));

   
    public string Response(string body)
    {
        var leaks = Inspect(SinkKind.HttpResponse, body, "Resposta HTTP", 0, "Response");
        return leaks.Count > 0 && _mode.Block ? _ctx.Redact(body) : body;
    }

    private void Enforce(List<Finding> leaks)
    {
        if (_mode.Block && leaks.Count > 0) throw new TaintBlockedException(leaks[0]);
    }

    private List<Finding> Inspect(SinkKind sink, string payload, string file, int line, string member)
    {
        var ctx = _http.HttpContext;
        var endpoint = ctx is null ? "(n/a)" : $"{ctx.Request.Method} {ctx.Request.Path}";
        var reported = new HashSet<string>();
        var leaks = new List<Finding>();
        var unescaped = Unescape(payload);

        foreach (var e in _ctx.Entries)
        {
            bool isPii = e.PiiTypes.Count > 0;
            bool leaked = e.Variants().Any(v => payload.Contains(v, StringComparison.Ordinal) || unescaped.Contains(v, StringComparison.Ordinal));

            if (leaked && (isPii || sink == SinkKind.Database))
            {
                if (!reported.Add(e.Source + "|leak")) continue;
                bool sqli = !isPii && sink == SinkKind.Database;
                leaks.Add(Store(endpoint, e, sink, payload, file, line, member, sanitized: _mode.Block, blocked: _mode.Block, kind: sqli ? "SQL_INJECTION" : "PII_LEAK"));
            }
            else if (!leaked && isPii && e.IsWholeValue && _ctx.Sanitized.Contains(e.Source))
            {
                if (!reported.Add(e.Source + "|safe")) continue;
                Store(endpoint, e, sink, payload, file, line, member, sanitized: true, blocked: false, kind: "PII_LEAK");
            }
        }
        return leaks;
    }

    private Finding Store(string endpoint, TaintEntry e, SinkKind sink, string payload, string file, int line, string member, bool sanitized, bool blocked, string kind)
    {
        var key = $"{endpoint}|{e.Source}|{sink}|{Path.GetFileName(file)}:{line}|{(blocked ? "blocked" : sanitized ? "safe" : kind)}";
        return _store.Record(new Finding
        {
            Key = key, Kind = kind, Endpoint = endpoint, Source = e.Source, Sink = sink,
            PiiTypes = e.PiiTypes.ToList(), VulnerableGroup = e.VulnerableGroup || _ctx.HasVulnerableGroup && e.PiiTypes.Count > 0,
            EvidenceMasked = Trim(_ctx.Redact(payload)), 
            File = Path.GetFileName(file), Line = line, Member = member,
            CodeSnippet = sink == SinkKind.HttpResponse ? "(corpo da resposta HTTP)" : Snippet(file, line),
            Sanitized = sanitized, Status = blocked ? "Blocked" : sanitized ? "Mitigated" : "Open",
        });
    }

    private static string Unescape(string s)
    {
        if (s.IndexOf('\\') < 0) return s;
        try { return Regex.Unescape(s); } catch { return s; }
    }

    private static string Trim(string s) => s.Length <= 220 ? s : s[..220] + "…";

    private static string Snippet(string file, int line)
    {
        try
        {
            if (!System.IO.File.Exists(file)) return "";
            var lines = System.IO.File.ReadAllLines(file);
            int from = Math.Max(0, line - 3), to = Math.Min(lines.Length, line + 2);
            return string.Join("\n", lines[from..to]);
        }
        catch { return ""; }
    }
}
