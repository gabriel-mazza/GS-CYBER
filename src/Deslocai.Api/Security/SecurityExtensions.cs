using Deslocai.Security.Demo;
using Deslocai.Security.Pii;
using Deslocai.Security.Remediation;
using Deslocai.Security.Risk;
using Deslocai.Security.Taint;

namespace Deslocai.Security;

/// <summary>Ponto único de integração do módulo de Cybersecurity com qualquer app ASP.NET Core.</summary>
public static class SecurityExtensions
{
    public static IServiceCollection AddDeslocaSecurity(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddSingleton<PiiDetector>();
        services.AddSingleton<FindingStore>();
        services.AddSingleton<RiskScoreService>();
        services.AddSingleton<SecurityMode>();
        services.AddSingleton<CitizenTranslator>();
        services.AddSingleton<LlmClient>();
        services.AddSingleton<AiRemediationService>();
        services.AddScoped<TaintContext>();
        services.AddScoped<SinkGuard>();
        return services;
    }

    /// <summary>Registra o middleware de taint (sources) e os endpoints de auditoria/dashboard/demo.</summary>
    public static WebApplication UseDeslocaSecurity(this WebApplication app)
    {
        app.UseStaticFiles();                       // serve wwwroot (dashboard)
        app.UseMiddleware<TaintMiddleware>();

        // Motor de PII (público): detecta, classifica e mascara
        app.MapPost("/api/pii/scan", (ScanRequest req, PiiDetector d) =>
        {
            var r = d.Scan(req.Text);
            return new
            {
                masked = r.Masked, hasPii = r.HasPii,
                matches = r.Matches.Select(m => new { type = m.Type.ToString(), value = PiiDetector.MaskValue(m.Type, m.Value), m.Start, m.Length, m.Confidence, m.Validated, lgpd = m.Level.ToString(), m.Weight })
            };
        });

        // Auditoria / dashboard
        app.MapGet("/api/findings", (FindingStore s) => s.Snapshot().OrderByDescending(f => f.LastSeen).Select(f => new
        {
            f.Id, f.Kind, f.Endpoint, f.Source, sink = f.Sink.ToString(), piiTypes = f.PiiTypes.Select(t => t.ToString()), f.VulnerableGroup,
            f.EvidenceMasked, f.File, f.Line, f.CodeSnippet, f.Sanitized, f.Status, f.Count, f.FirstSeen, f.LastSeen, f.Remediation,
            flowRisk = RiskScoreService.FlowRaw(f)
        }));
        app.MapGet("/api/risk", (RiskScoreService r) => r.Compute());
        app.MapGet("/api/ai-status", (LlmClient llm) => new { aiEnabled = llm.Enabled, provider = llm.Provider, label = llm.Label, lastError = llm.LastError });
        app.MapPost("/api/remediation/{id}", async (string id, FindingStore s, AiRemediationService ai, CancellationToken ct) =>
        {
            var f = s.Get(id); if (f is null) return Results.NotFound();
            var res = await ai.RemediateAsync(f, ct);
            s.SetRemediation(id, System.Text.Json.JsonSerializer.Serialize(res));
            return Results.Ok(res);
        });
        app.MapPost("/api/findings/{id}/resolve", (string id, FindingStore s) => s.Resolve(id) ? Results.Ok() : Results.NotFound());
        // Modo do Taint: Auditoria (registra) ou Bloqueio (impede o vazamento)
        app.MapGet("/api/mode", (SecurityMode m) => new { modo = m.Name });
        app.MapPost("/api/mode/{modo}", (string modo, SecurityMode m) =>
        {
            if (!modo.Equals("auditoria", StringComparison.OrdinalIgnoreCase) && !modo.Equals("bloqueio", StringComparison.OrdinalIgnoreCase)) return Results.BadRequest();
            m.Block = modo.Equals("bloqueio", StringComparison.OrdinalIgnoreCase);
            return Results.Ok(new { modo = m.Name });
        });

        // Risco Cidadão: farol + mensagem do evento mais relevante traduzida por IA para leigos
        app.MapGet("/api/citizen", async (FindingStore s, RiskScoreService r, CitizenTranslator t, CancellationToken ct) =>
        {
            var all = s.Snapshot(); var risk = r.Compute();
            var open = all.Where(f => f.Status == "Open").OrderByDescending(RiskScoreService.FlowRaw).FirstOrDefault();
            var f = open
                ?? all.Where(x => x.Status == "Blocked").OrderByDescending(x => x.LastSeen).FirstOrDefault()
                ?? all.Where(x => x.Status == "Mitigated").OrderByDescending(x => x.LastSeen).FirstOrDefault()
                ?? all.Where(x => x.Status == "Resolved").OrderByDescending(x => x.LastSeen).FirstOrDefault();
            if (f is null)
                return Results.Ok(new { farol = "verde", score = risk.Score, level = risk.Level, status = "Nenhum", source = "-", technical = "Nenhum evento de segurança registrado.", message = "Tudo tranquilo por aqui: nenhum problema de segurança foi encontrado." });
            var (text, source) = await t.TranslateAsync(f, ct);
            var farol = open is not null ? (risk.Score < 25 ? "verde" : risk.Score < 60 ? "amarelo" : "vermelho")
                      : all.Any(x => x.Status == "Blocked") ? "amarelo" : "verde";
            return Results.Ok(new { farol, score = risk.Score, level = risk.Level, status = f.Status, source, technical = CitizenTranslator.Describe(f), message = text });
        });
        
        app.MapPost("/api/reset", (FindingStore s, RiskScoreService r) => { s.Reset(); r.ResetTimeline(); return Results.Ok(); });

        DemoEndpoints.Map(app);                     // /api/v1/relatos (vulnerável) e /api/v2/relatos (corrigido)
        return app;
    }
}

public record ScanRequest(string Text);
