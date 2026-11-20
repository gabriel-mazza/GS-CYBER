namespace Deslocai.Api.Domain;


public class LeituraSensor : EntidadeBase
{
    public int EstacaoId { get; private set; }
    public Estacao? Estacao { get; private set; }
    public double TemperaturaC { get; private set; }
    public double UmidadePercentual { get; private set; }
    public int PessoasNaPlataforma { get; private set; }
    public DateTime RegistradaEm { get; private set; }

    protected LeituraSensor() { }

    public LeituraSensor(int estacaoId, double temperaturaC, double umidadePercentual,
        int pessoasNaPlataforma, DateTime? registradaEm = null)
    {
        if (temperaturaC is < -10 or > 70) throw new RegraDeNegocioException("Temperatura fora da faixa plausível (-10 a 70 °C).");
        if (umidadePercentual is < 0 or > 100) throw new RegraDeNegocioException("Umidade deve estar entre 0 e 100%.");
        if (pessoasNaPlataforma < 0) throw new RegraDeNegocioException("A contagem de pessoas não pode ser negativa.");

        EstacaoId = estacaoId;
        TemperaturaC = temperaturaC;
        UmidadePercentual = umidadePercentual;
        PessoasNaPlataforma = pessoasNaPlataforma;
        RegistradaEm = registradaEm ?? DateTime.UtcNow;
    }
}
