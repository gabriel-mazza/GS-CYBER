using System.Collections.Concurrent;
using Deslocai.Security.Pii;
using Deslocai.Security.Taint;

namespace Deslocai.Security.Remediation;

/// <summary>
/// RISCO CIDADÃO · tradução via IA: transforma o evento técnico de segurança em linguagem humana
/// ("cidadão leigo de 60 anos, em 2 frases"). Só o texto técnico SEM PII vai para a IA.
/// Sem ANTHROPIC_API_KEY (ou se a IA falhar) usa mensagens offline, para o sistema nunca ficar mudo.
/// </summary>
public class CitizenTranslator
{
    private readonly LlmClient _llm;
    private readonly ConcurrentDictionary<string, (string Text, string Source)> _cache = new();
    private DateTime _aiDownUntil = DateTime.MinValue;

    public CitizenTranslator(LlmClient llm) => _llm = llm;
    public bool AiEnabled => _llm.Enabled;

    /// <summary>Descrição técnica do evento (nunca contém dado pessoal real).</summary>
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
                var r = (await Ask(Describe(f), ct), _llm.Label);
                _cache[key] = r;
                return r;
            }
            catch { _aiDownUntil = DateTime.UtcNow.AddSeconds(60); }
        }
        return (Offline(f), AiEnabled ? "offline-rules (IA indisponível)" : "offline-rules");
    }

    private Task<string> Ask(string technical, CancellationToken ct)
    {
        const string system = "Você é o assistente de privacidade do aplicativo de transporte público Deslocai. Responda SOMENTE com o texto final, em português do Brasil, " +
            "tom calmo e acolhedor, sem jargão técnico, sem citar rotas, sistemas ou códigos. Nunca peça dados pessoais.";
        var user = $"Traduza a falha técnica '{technical}' para um cidadão leigo de 60 anos, em 2 frases.";
        return _llm.CompleteAsync(system, user, 1000, json: false, ct);
    }

    /// <summary>Mensagens de reserva, escritas para um leigo, por tipo de dado e estado do fluxo.</summary>
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
