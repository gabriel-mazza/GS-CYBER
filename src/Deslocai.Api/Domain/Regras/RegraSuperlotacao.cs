namespace Deslocai.Api.Domain.Regras;

public class RegraSuperlotacao : IRegraAlerta
{
    public const double LimiteAtencao = 80;

    public TipoAlerta Tipo => TipoAlerta.Superlotacao;

    public Alerta? Avaliar(Estacao estacao, LeituraSensor leitura)
    {
        var ocupacao = estacao.CalcularOcupacaoPercentual(leitura.PessoasNaPlataforma);
        if (ocupacao < LimiteAtencao) return null;

        var severidade = ocupacao switch
        {
            >= 120.0 => Severidade.Critica,
            >= 100.0 => Severidade.Alta,
            _ => Severidade.Media
        };

        var mensagem = $"Plataforma de {estacao.Nome} com {ocupacao:0.#}% da capacidade. " +
                       "Considere aguardar o próximo veículo ou usar outra estação.";

        return new Alerta(estacao.Id, Tipo, severidade, mensagem);
    }
}
