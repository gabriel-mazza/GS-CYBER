namespace Deslocai.Api.Domain;


public class Estacao : EntidadeBase
{
    public string Nome { get; private set; } = string.Empty;
    public string Linha { get; private set; } = string.Empty;
    public TipoTransporte Tipo { get; private set; }
    public double Latitude { get; private set; }
    public double Longitude { get; private set; }
    public int CapacidadeMaxima { get; private set; }

    public ICollection<Equipamento> Equipamentos { get; } = new List<Equipamento>();
    public ICollection<LeituraSensor> Leituras { get; } = new List<LeituraSensor>();
    public ICollection<Alerta> Alertas { get; } = new List<Alerta>();
    public ICollection<Relato> Relatos { get; } = new List<Relato>();

    protected Estacao() { } 

    public Estacao(string nome, string linha, TipoTransporte tipo, double latitude, double longitude, int capacidadeMaxima)
    {
        Atualizar(nome, linha, tipo, latitude, longitude, capacidadeMaxima);
    }

    
    public void Atualizar(string nome, string linha, TipoTransporte tipo, double latitude, double longitude, int capacidadeMaxima)
    {
        if (string.IsNullOrWhiteSpace(nome)) throw new RegraDeNegocioException("O nome da estação é obrigatório.");
        if (string.IsNullOrWhiteSpace(linha)) throw new RegraDeNegocioException("A linha é obrigatória.");
        if (capacidadeMaxima <= 0) throw new RegraDeNegocioException("A capacidade máxima deve ser maior que zero.");
        if (latitude is < -90 or > 90) throw new RegraDeNegocioException("Latitude inválida.");
        if (longitude is < -180 or > 180) throw new RegraDeNegocioException("Longitude inválida.");

        Nome = nome.Trim();
        Linha = linha.Trim();
        Tipo = tipo;
        Latitude = latitude;
        Longitude = longitude;
        CapacidadeMaxima = capacidadeMaxima;
    }

  
    public double CalcularOcupacaoPercentual(int pessoas) =>
        Math.Round(pessoas * 100.0 / CapacidadeMaxima, 1);
}
