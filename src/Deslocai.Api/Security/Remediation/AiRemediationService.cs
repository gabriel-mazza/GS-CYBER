using System.Text;
using System.Text.Json;
using Deslocai.Security.Taint;

namespace Deslocai.Security.Remediation;

public record RemediationResult(string Source, string Explanation, string Severity, string Lgpd, string FixedCode, string[] Verification);

/// <summary>
/// Remediação assistida por IA: envia o finding (JÁ MASCARADO, nunca PII real) + trecho de código à API do Claude
/// e recebe explicação, artigo da LGPD e patch em C#. Sem ANTHROPIC_API_KEY, usa um motor de regras offline.
/// </summary>
public class AiRemediationService
{
    private readonly LlmClient _llm;

    public AiRemediationService(LlmClient llm) => _llm = llm;
    public bool AiEnabled => _llm.Enabled;

    public async Task<RemediationResult> RemediateAsync(Finding f, CancellationToken ct = default)
    {
        if (AiEnabled)
        {
            try { return await CallClaude(f, ct); }
            catch (Exception ex) { var fb = Offline(f); return fb with { Source = $"offline-rules (IA indisponível: {ex.GetType().Name})" }; }
        }
        return Offline(f);
    }

    private async Task<RemediationResult> CallClaude(Finding f, CancellationToken ct)
    {
        const string system = "Você é um engenheiro de AppSec especialista em LGPD e C#/.NET. Responda SOMENTE com JSON válido, sem markdown, " +
            "com as chaves: explanation (pt-BR, 2-3 frases), severity (Baixa|Média|Alta|Crítica), lgpd (artigo e motivo), " +
            "fixed_code (trecho C# corrigido, usando SinkGuard/TaintContext.Mask/Pseudonymize e SQL parametrizado), verification (array de 2-3 passos de teste).";
        var user = new StringBuilder()
            .AppendLine($"Tipo: {f.Kind}").AppendLine($"Endpoint: {f.Endpoint}").AppendLine($"Fonte (source): {f.Source}")
            .AppendLine($"Destino (sink): {f.Sink}").AppendLine($"Tipos de PII: {string.Join(", ", f.PiiTypes)}")
            .AppendLine($"Envolve PCD/idoso: {f.VulnerableGroup}").AppendLine($"Evidência (mascarada): {f.EvidenceMasked}")
            .AppendLine($"Código ({f.File}:{f.Line}):").AppendLine(f.CodeSnippet).ToString();

        var text = await _llm.CompleteAsync(system, user.ToString(), 1500, json: true, ct);
        text = text.Trim().Trim('`'); if (text.StartsWith("json", StringComparison.OrdinalIgnoreCase)) text = text[4..];
        using var j = JsonDocument.Parse(text);
        var r = j.RootElement;
        return new RemediationResult(_llm.Label, r.GetProperty("explanation").GetString() ?? "", r.GetProperty("severity").GetString() ?? "",
            r.GetProperty("lgpd").GetString() ?? "", r.GetProperty("fixed_code").GetString() ?? "",
            r.GetProperty("verification").EnumerateArray().Select(x => x.GetString() ?? "").ToArray());
    }

    /// <summary>Motor offline: garante que o protótipo funcione (e seja demonstrável) sem chave de API.</summary>
    public static RemediationResult Offline(Finding f)
    {
        var types = string.Join(", ", f.PiiTypes);
        var sev = f.Kind == "SQL_INJECTION" ? "Crítica" : f.Sink == SinkKind.HttpOutbound || f.VulnerableGroup ? "Crítica" : "Alta";
        return (f.Kind, f.Sink) switch
        {
            ("SQL_INJECTION", _) => new("offline-rules", $"O valor '{f.Source}' vindo da requisição é concatenado no comando SQL, permitindo injeção de SQL.", sev,
                "Art. 46 – medidas de segurança contra acessos não autorizados.",
                "// ANTES: $\"... WHERE cpf = '{dto.Cpf}'\"\n// DEPOIS (parametrizado):\nconst string sql = \"SELECT * FROM Passageiros WHERE Cpf = @cpf\";\nguard.Sql(sql, new { cpf = dto.Cpf });",
                new[] { "Enviar payload com ' OR 1=1 -- e confirmar que nenhum finding SQL_INJECTION é gerado.", "Reexecutar o scan e verificar o fluxo como Mitigado." }),
            (_, SinkKind.Log) => new("offline-rules", $"Dados pessoais ({types}) de '{f.Source}' são gravados em log sem mascaramento, expondo cidadãos a quem acessa logs.", sev,
                "Art. 6º, III (necessidade) e Art. 46 (segurança) da LGPD.",
                "// ANTES: guard.Log($\"Relato: {body}\");\n// DEPOIS:\nguard.Log($\"Relato recebido de {ctx.Mask(dto.Cpf)} na estação {dto.Estacao}\");",
                new[] { "Reenviar a requisição e conferir que o log só tem valores mascarados.", "Rodar o scan: o fluxo deve aparecer como Mitigado." }),
            (_, SinkKind.HttpOutbound) => new("offline-rules", $"Dados pessoais ({types}) são enviados a um serviço externo em texto claro, sem base legal nem minimização.", sev,
                "Art. 7º (base legal), Art. 6º, III (minimização) e Art. 33 (transferência).",
                "// ANTES: guard.HttpOut(url, json com e-mail/CPF);\n// DEPOIS: enviar só identificador pseudonimizado.\nvar id = ctx.Pseudonymize(dto.Cpf);\nguard.HttpOut(url, JsonSerializer.Serialize(new { id, estacao = dto.Estacao }));",
                new[] { "Capturar o payload de saída e validar que não há PII.", "Reexecutar o scan: fluxo Mitigado." }),
            (_, SinkKind.Database) => new("offline-rules", $"Dados pessoais ({types}) vão para o banco em texto claro dentro do comando SQL.", sev,
                "Art. 46 e Art. 6º, VII (segurança).",
                "// Usar parâmetros e criptografia de coluna para PII\nguard.Sql(\"INSERT INTO Relatos(CpfEnc) VALUES (@cpf)\", new { cpf = Encrypt(dto.Cpf) });",
                new[] { "Inspecionar a tabela e confirmar que o CPF está cifrado.", "Reexecutar o scan." }),
            _ => new("offline-rules", $"Fluxo de PII ({types}) sem sanitização até {f.Sink}.", sev, "Art. 46 da LGPD.", "// Aplicar ctx.Mask(...) antes do sink.", new[] { "Reexecutar o scan." }),
        };
    }
}
