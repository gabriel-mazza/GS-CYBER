namespace Deslocai.Api.Domain;


public class Alerta : EntidadeBase
{
    public int EstacaoId { get; private set; }
    public Estacao? Estacao { get; private set; }
    public int? EquipamentoId { get; private set; }
    public TipoAlerta Tipo { get; private set; }
    public Severidade Severidade { get; private set; }
    public string Mensagem { get; private set; } = string.Empty;
    public DateTime? ResolvidoEm { get; private set; }

   
    public bool Ativo => ResolvidoEm is null;

    protected Alerta() { }

    public Alerta(int estacaoId, TipoAlerta tipo, Severidade severidade, string mensagem, int? equipamentoId = null)
    {
        if (string.IsNullOrWhiteSpace(mensagem)) throw new RegraDeNegocioException("A mensagem do alerta é obrigatória.");

        EstacaoId = estacaoId;
        Tipo = tipo;
        Severidade = severidade;
        Mensagem = mensagem;
        EquipamentoId = equipamentoId;
    }

    public void Resolver()
    {
        if (!Ativo) throw new RegraDeNegocioException("O alerta já foi resolvido.");
        ResolvidoEm = DateTime.UtcNow;
    }
}
