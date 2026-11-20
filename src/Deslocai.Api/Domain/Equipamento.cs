namespace Deslocai.Api.Domain;


public class Equipamento : EntidadeBase
{
    public int EstacaoId { get; private set; }
    public Estacao? Estacao { get; private set; }
    public string Identificador { get; private set; } = string.Empty;
    public TipoEquipamento Tipo { get; private set; }
    public StatusEquipamento Status { get; private set; } = StatusEquipamento.Operante;
    public DateTime AtualizadoEm { get; private set; } = DateTime.UtcNow;

   
    public bool EstaDisponivel => Status == StatusEquipamento.Operante;

    protected Equipamento() { }

    public Equipamento(int estacaoId, string identificador, TipoEquipamento tipo,
        StatusEquipamento status = StatusEquipamento.Operante)
    {
        if (string.IsNullOrWhiteSpace(identificador))
            throw new RegraDeNegocioException("O identificador do equipamento é obrigatório.");

        EstacaoId = estacaoId;
        Identificador = identificador.Trim();
        Tipo = tipo;
        Status = status;
    }

  
    public bool AlterarStatus(StatusEquipamento novoStatus)
    {
        if (novoStatus == Status) return false;
        Status = novoStatus;
        AtualizadoEm = DateTime.UtcNow;
        return true;
    }
}
