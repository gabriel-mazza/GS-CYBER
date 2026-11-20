using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Deslocai.Security.Pii;
using Deslocai.Security.Taint;

namespace Deslocai.Security.Remediation;


public class CitizenTranslator
{
    private readonly HttpClient _http;
    private readonly string? _key = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
    private readonly string _model = Environment.GetEnvironmentVariable("ANTHROPIC_MODEL") ?? "claude-sonnet-5-5";
    private readonly ConcurrentDictionary<string, (string Text, string Source)> _cache = new();
    private DateTime _aiDownUntil = DateTime.MinValue;

    public CitizenTranslator(HttpClient http) { _http = http; _http.Timeout = TimeSpan.FromSeconds(8); }
    public bool AiEnabled => !string.IsNullOrWhiteSpace(_key);

    
    public static string Describe(Finding f)
    {
        var tipos = f.PiiTypes.Count > 0 ? string.Join(", ", f.PiiTypes) : "entrada não confiável";
        return f.Kind == "SQL_INJECTION"
            ? $"Tentativa de injeção de SQL {Estado(f.Status)} no endpoint {f.Endpoint}"
            : $"Vazamento de PII ({tipos}) {Estado(f.Status)}: origem {f.Source}, destino {f.Sink}, endpoint {f.Endpoint}";
    }

    private static string Estado(string status) => status switch
    {
        "Blocked" => "bloqueado", "Mitigated" => "evitado por mascaramento", "Resolved" => "corrigido", _ => "detectado e ainda não bloqueado",
    };

    public async Task<(string Text, string Source)> TranslateAsync(Finding f, CancellationToken ct = default)
    {
        var key = $"{f.Kind}|{f.Status}|{f.Sink}|{string.Join(',', f.PiiTypes.OrderBy(t => t))}";
        if (_cache.TryGetValue(key, out var hit)) return hit;

        if (AiEnabled && DateTime.UtcNow >= _aiDownUntil)
        {
            try
            {
                var r = (await CallClaude(Describe(f), ct), $"claude ({_model})");
                _cache[key] = r;
                return r;
            }
            catch { _aiDownUntil = DateTime.UtcNow.AddSeconds(60); }
        }
        return (Offline(f), AiEnabled ? "offline-rules (IA indisponível)" : "offline-rules");
    }

    private async Task<string> CallClaude(string technical, CancellationToken ct)
    {
        const string system = "Você é o assistente de privacidade do aplicativo de transporte público Deslocai. Responda SOMENTE com o texto final, em português do Brasil, " +
            "tom calmo e acolhedor, sem jargão técnico, sem citar rotas, sistemas ou códigos. Nunca peça dados pessoais.";
        var user = $"Traduza a falha técnica '{technical}' para um cidadão leigo de 60 anos, em 2 frases.";
        var payload = JsonSerializer.Serialize(new { model = _model, max_tokens = 200, system, messages = new[] { new { role = "user", content = user } } });
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages") { Content = new StringContent(payload, Encoding.UTF8, "application/json") };
        req.Headers.Add("x-api-key", _key); req.Headers.Add("anthropic-version", "2023-06-01");
        using var resp = await _http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        var text = string.Concat(doc.RootElement.GetProperty("content").EnumerateArray()
            .Where(c => c.GetProperty("type").GetString() == "text").Select(c => c.GetProperty("text").GetString())).Trim();
        if (text.Length == 0) throw new InvalidOperationException("resposta vazia");
        return text;
    }

    
    public static string Offline(Finding f)
    {
        if (f.Kind == "SQL_INJECTION")
            return f.Status switch
            {
                "Blocked" => "Barramos uma tentativa de mexer indevidamente no nosso sistema para proteger os seus dados. Você não precisa fazer nada.",
                "Resolved" => "Corrigimos uma falha que poderia permitir acesso indevido ao sistema. Agora está tudo seguro.",
                _ => "Encontramos uma falha que poderia permitir acesso indevido ao sistema. A nossa equipe já foi avisada e está corrigindo.",
            };
        var s = Subject(f.PiiTypes);
        return f.Status switch
        {
            "Blocked" => $"Alguém tentou usar {s} de forma insegura, mas o nosso sistema bloqueou a ação para proteger a sua privacidade. Você não precisa fazer nada.",
            "Mitigated" => $"Protegemos {s} escondendo parte das informações antes de guardá-las. Está tudo certo e você não precisa fazer nada.",
            "Resolved" => $"Corrigimos uma falha que deixava {s} sem a devida proteção. Agora está tudo seguro.",
            _ => $"Encontramos uma falha que deixava {s} sem a devida proteção. A nossa equipe já foi avisada e está corrigindo para manter você seguro.",
        };
    }

    private static string Subject(IEnumerable<PiiType> types)
    {
        var top = types.OrderByDescending(PiiCatalog.Weight).FirstOrDefault();
        return top switch
        {
            PiiType.Senha => "a sua senha",
            PiiType.Cartao => "os dados do seu cartão",
            PiiType.Laudo or PiiType.SaudePcd => "informações sobre a sua saúde",
            PiiType.Cpf or PiiType.Rg or PiiType.Cnpj => "os seus documentos",
            PiiType.Geolocalizacao => "o lugar onde você está",
            PiiType.Email or PiiType.Telefone => "os seus dados de contato",
            _ => "os seus dados pessoais",
        };
    }
}
