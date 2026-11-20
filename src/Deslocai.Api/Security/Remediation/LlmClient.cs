using System.Text;
using System.Text.Json;

namespace Deslocai.Security.Remediation;

/// <summary>
/// Cliente único de LLM. Escolhe o provedor pela variável de ambiente:
///   GEMINI_API_KEY (ou GOOGLE_API_KEY)  → Google Gemini  (plano gratuito no Google AI Studio)
///   ANTHROPIC_API_KEY                   → Claude
/// Sem nenhuma chave, Enabled = false e o sistema usa as mensagens offline.
/// Modelos configuráveis: GEMINI_MODEL (padrão gemini-flash-latest) e ANTHROPIC_MODEL.
/// </summary>
public class LlmClient
{
    private static string? Env(string n) { var v = Environment.GetEnvironmentVariable(n); return string.IsNullOrWhiteSpace(v) ? null : v.Trim(); }

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(45) };
    private readonly string? _gemini = Env("GEMINI_API_KEY") ?? Env("GOOGLE_API_KEY");
    private readonly string? _claude = Env("ANTHROPIC_API_KEY");
    private readonly string _geminiModel = Env("GEMINI_MODEL") ?? "gemini-flash-latest";
    private readonly string _claudeModel = Env("ANTHROPIC_MODEL") ?? "claude-sonnet-5-5";
    private readonly string _geminiBase = (Env("GEMINI_BASE_URL") ?? "https://generativelanguage.googleapis.com").TrimEnd('/');
    private readonly string _claudeBase = (Env("ANTHROPIC_BASE_URL") ?? "https://api.anthropic.com").TrimEnd('/');

    public bool Enabled => _gemini is not null || _claude is not null;
    public string Provider => _gemini is not null ? "Gemini" : _claude is not null ? "Claude" : "offline";
    public string Label => _gemini is not null ? $"gemini ({_geminiModel})" : _claude is not null ? $"claude ({_claudeModel})" : "offline-rules";
    /// <summary>Último erro da chamada à IA (status HTTP e trecho da resposta), para diagnóstico. Nunca contém a chave.</summary>
    public string? LastError { get; private set; }

    public async Task<string> CompleteAsync(string system, string user, int maxTokens, bool json, CancellationToken ct = default)
    {
        try
        {
            var text = _gemini is not null ? await Gemini(system, user, maxTokens, json, ct) : await Claude(system, user, maxTokens, ct);
            if (string.IsNullOrWhiteSpace(text)) throw new InvalidOperationException("resposta vazia da IA");
            LastError = null;
            return text.Trim();
        }
        catch (Exception ex) { LastError = ex.Message.Length > 300 ? ex.Message[..300] : ex.Message; throw; }
    }

    private async Task<string> Gemini(string system, string user, int maxTokens, bool json, CancellationToken ct)
    {
        var gen = new Dictionary<string, object> { ["maxOutputTokens"] = maxTokens, ["temperature"] = 0.4 };
        if (json) gen["responseMimeType"] = "application/json";
        var payload = JsonSerializer.Serialize(new
        {
            systemInstruction = new { parts = new[] { new { text = system } } },
            contents = new[] { new { role = "user", parts = new[] { new { text = user } } } },
            generationConfig = gen,
        });
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{_geminiBase}/v1beta/models/{_geminiModel}:generateContent")
        { Content = new StringContent(payload, Encoding.UTF8, "application/json") };
        req.Headers.Add("x-goog-api-key", _gemini);
        using var resp = await _http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode) throw new HttpRequestException($"Gemini HTTP {(int)resp.StatusCode}: {Short(body)}");
        using var doc = JsonDocument.Parse(body);
        var parts = doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts");
        return string.Concat(parts.EnumerateArray().Where(p => p.TryGetProperty("text", out _)).Select(p => p.GetProperty("text").GetString()));
    }

    private async Task<string> Claude(string system, string user, int maxTokens, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(new { model = _claudeModel, max_tokens = maxTokens, system, messages = new[] { new { role = "user", content = user } } });
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{_claudeBase}/v1/messages") { Content = new StringContent(payload, Encoding.UTF8, "application/json") };
        req.Headers.Add("x-api-key", _claude); req.Headers.Add("anthropic-version", "2023-06-01");
        using var resp = await _http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode) throw new HttpRequestException($"Claude HTTP {(int)resp.StatusCode}: {Short(body)}");
        using var doc = JsonDocument.Parse(body);
        return string.Concat(doc.RootElement.GetProperty("content").EnumerateArray()
            .Where(c => c.GetProperty("type").GetString() == "text").Select(c => c.GetProperty("text").GetString()));
    }

    private static string Short(string s) => s.Length <= 200 ? s : s[..200];
}
