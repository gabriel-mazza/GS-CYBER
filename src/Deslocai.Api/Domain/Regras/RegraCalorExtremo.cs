namespace Deslocai.Api.Domain.Regras;

public class RegraCalorExtremo : IRegraAlerta
{
    public const double LimiteAtencao = 35;

    public TipoAlerta Tipo => TipoAlerta.CalorExtremo;

    
    public static double CalcularSensacao(double temperaturaC, double umidadePercentual) =>
        temperaturaC + Math.Max(0, umidadePercentual - 60) / 10.0;

    public Alerta? Avaliar(Estacao estacao, LeituraSensor leitura)
    {
        var sensacao = CalcularSensacao(leitura.TemperaturaC, leitura.UmidadePercentual);
        if (sensacao < LimiteAtencao) return null;

        var severidade = sensacao switch
        {
            >= 42.0 => Severidade.Critica,
            >= 39.0 => Severidade.Alta,
            _ => Severidade.Media
        };

        var mensagem = $"Calor intenso em {estacao.Nome} (sensação de {sensacao:0.#} °C). " +
                       "Hidrate-se e, se possível, evite esperar na plataforma; idosos e crianças devem buscar áreas ventiladas.";

        return new Alerta(estacao.Id, Tipo, severidade, mensagem);
    }
}
