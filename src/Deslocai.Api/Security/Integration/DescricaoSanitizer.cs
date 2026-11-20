using Deslocai.Api.Application;
using Deslocai.Security.Pii;
using Deslocai.Security.Taint;

namespace Deslocai.Security.Integration;


public class DescricaoSanitizer : IDescricaoSanitizer
{
    private const int LimiteDescricao = 280;
    private readonly PiiDetector _detector;
    private readonly FindingStore _store;
    private readonly IHttpContextAccessor _http;
    private readonly IConfiguration _config;

    public DescricaoSanitizer(PiiDetector detector, FindingStore store, IHttpContextAccessor http, IConfiguration config)
    { _detector = detector; _store = store; _http = http; _config = config; }

    public string Sanitizar(string descricao)
    {
        var scan = _detector.Scan(descricao);
        var pii = scan.Matches.Where(m => m.Type != PiiType.SaudePcd).ToList();
        if (pii.Count == 0) return descricao;

        bool mascarar = _config.GetValue("Seguranca:MascararPiiEmRelatos", true);
        var masked = PiiDetector.Mask(descricao, pii);
        var req = _http.HttpContext?.Request;
        var endpoint = req is null ? "POST /api/relatos" : $"{req.Method} {req.Path}";

        _store.Record(new Finding
        {
            Key = $"{endpoint}|body.descricao|Database|RelatoService.cs|{(mascarar ? "safe" : "leak")}",
            Kind = "PII_LEAK", Endpoint = endpoint, Source = "body.descricao", Sink = SinkKind.Database,
            PiiTypes = pii.Select(m => m.Type).Distinct().ToList(),
            VulnerableGroup = scan.Matches.Any(m => m.Type == PiiType.SaudePcd),
            EvidenceMasked = masked.Length <= 220 ? masked : masked[..220] + "…",
            File = "RelatoService.cs", Line = 38, Member = "CriarAsync",
            CodeSnippet = "var relato = new Relato(dto.EstacaoId, dto.Tipo, dto.PerfilRelator, descricao);\nawait _relatos.AdicionarAsync(relato, ct);",
            Sanitized = mascarar, Status = mascarar ? "Mitigated" : "Open",
        });

        if (!mascarar) return descricao;
        return masked.Length > LimiteDescricao ? masked[..LimiteDescricao] : masked;
    }
}
