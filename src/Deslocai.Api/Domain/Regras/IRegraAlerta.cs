namespace Deslocai.Api.Domain.Regras;


public interface IRegraAlerta
{
    TipoAlerta Tipo { get; }

    
    Alerta? Avaliar(Estacao estacao, LeituraSensor leitura);
}
