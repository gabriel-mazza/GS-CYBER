using System.ComponentModel.DataAnnotations;
using Deslocai.Api.Domain;

namespace Deslocai.Api.Application;



public class EstacaoEntradaDto
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string Nome { get; init; } = string.Empty;

    [Required, StringLength(60, MinimumLength = 2)]
    public string Linha { get; init; } = string.Empty;

    [EnumDataType(typeof(TipoTransporte))]
    public TipoTransporte Tipo { get; init; }

    [Range(-90, 90)]
    public double Latitude { get; init; }

    [Range(-180, 180)]
    public double Longitude { get; init; }

    [Range(50, 100000)]
    public int CapacidadeMaxima { get; init; }
}

public class EquipamentoEntradaDto
{
    [Range(1, int.MaxValue)]
    public int EstacaoId { get; init; }

    [Required, StringLength(40, MinimumLength = 2)]
    public string Identificador { get; init; } = string.Empty;

    [EnumDataType(typeof(TipoEquipamento))]
    public TipoEquipamento Tipo { get; init; }
}

public class AlterarStatusDto
{
    [EnumDataType(typeof(StatusEquipamento))]
    public StatusEquipamento Status { get; init; }
}


public class LeituraEntradaDto
{
    [Range(1, int.MaxValue)]
    public int EstacaoId { get; init; }

    [Range(-10, 70)]
    public double TemperaturaC { get; init; }

    [Range(0, 100)]
    public double UmidadePercentual { get; init; }

    [Range(0, 100000)]
    public int PessoasNaPlataforma { get; init; }
}

public class RelatoEntradaDto
{
    [Range(1, int.MaxValue)]
    public int EstacaoId { get; init; }

    [EnumDataType(typeof(TipoRelato))]
    public TipoRelato Tipo { get; init; }

    [EnumDataType(typeof(PerfilCidadao))]
    public PerfilCidadao PerfilRelator { get; init; }

    [Required, StringLength(280, MinimumLength = 5)]
    public string Descricao { get; init; } = string.Empty;
}



public record EstacaoDto(int Id, string Nome, string Linha, TipoTransporte Tipo,
    double Latitude, double Longitude, int CapacidadeMaxima);

public record EstacaoDetalheDto(int Id, string Nome, string Linha, TipoTransporte Tipo,
    double Latitude, double Longitude, int CapacidadeMaxima,
    bool AcessivelParaPcd, int EquipamentosIndisponiveis, int AlertasAtivos,
    double? OcupacaoAtualPercentual, double? TemperaturaAtualC, DateTime? UltimaLeituraEm);

public record EquipamentoDto(int Id, int EstacaoId, string Identificador,
    TipoEquipamento Tipo, StatusEquipamento Status, DateTime AtualizadoEm);

public record LeituraDto(int Id, int EstacaoId, double TemperaturaC, double UmidadePercentual,
    int PessoasNaPlataforma, double OcupacaoPercentual, DateTime RegistradaEm);

public record AlertaDto(int Id, int EstacaoId, int? EquipamentoId, TipoAlerta Tipo,
    Severidade Severidade, string Mensagem, DateTime CriadoEm, DateTime? ResolvidoEm, bool Ativo);

public record LeituraRegistradaDto(LeituraDto Leitura, IReadOnlyList<AlertaDto> AlertasGerados);

public record RelatoDto(int Id, int EstacaoId, TipoRelato Tipo, PerfilCidadao PerfilRelator,
    string Descricao, DateTime CriadoEm);

public record AlertaPorTipoDto(TipoAlerta Tipo, int Quantidade);

public record EstacaoLotadaDto(int EstacaoId, string Nome, double OcupacaoPercentual, DateTime RegistradaEm);


public record ImpactoDto(int TotalEstacoes, int TotalEquipamentos, int EquipamentosIndisponiveis,
    double PercentualEquipamentosOperantes, int EstacoesSemElevadorOperante,
    IReadOnlyList<AlertaPorTipoDto> AlertasAtivosPorTipo,
    IReadOnlyList<EstacaoLotadaDto> EstacoesMaisLotadasUltimas24h,
    double? TemperaturaMediaUltimas24hC, DateTime GeradoEm);
