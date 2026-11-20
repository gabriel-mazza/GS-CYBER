using Deslocai.Api.Domain;

namespace Deslocai.Api.Application;

public interface IEstacaoService
{
    Task<IReadOnlyList<EstacaoDto>> ListarAsync(TipoTransporte? tipo, string? linha, CancellationToken ct = default);
    Task<EstacaoDetalheDto> ObterAsync(int id, CancellationToken ct = default);
    Task<EstacaoDto> CriarAsync(EstacaoEntradaDto dto, CancellationToken ct = default);
    Task<EstacaoDto> AtualizarAsync(int id, EstacaoEntradaDto dto, CancellationToken ct = default);
    Task RemoverAsync(int id, CancellationToken ct = default);
}

public interface IEquipamentoService
{
    Task<IReadOnlyList<EquipamentoDto>> ListarAsync(int? estacaoId, StatusEquipamento? status, CancellationToken ct = default);
    Task<EquipamentoDto> CriarAsync(EquipamentoEntradaDto dto, CancellationToken ct = default);
    Task<EquipamentoDto> AlterarStatusAsync(int id, StatusEquipamento novoStatus, CancellationToken ct = default);
}

public interface ILeituraService
{
    Task<LeituraRegistradaDto> RegistrarAsync(LeituraEntradaDto dto, CancellationToken ct = default);
    Task<IReadOnlyList<LeituraDto>> UltimasAsync(int estacaoId, int quantidade, CancellationToken ct = default);
}

public interface IAlertaService
{
    Task<IReadOnlyList<AlertaDto>> ListarAsync(int? estacaoId, TipoAlerta? tipo, bool incluirResolvidos, CancellationToken ct = default);
    Task<AlertaDto> ResolverAsync(int id, CancellationToken ct = default);

    
    Task<IReadOnlyList<Alerta>> AvaliarLeituraAsync(Estacao estacao, LeituraSensor leitura, CancellationToken ct = default);

    
    Task SincronizarEquipamentoAsync(Equipamento equipamento, CancellationToken ct = default);
}

public interface IRelatoService
{
    Task<IReadOnlyList<RelatoDto>> ListarAsync(int? estacaoId, TipoRelato? tipo, CancellationToken ct = default);
    Task<RelatoDto> CriarAsync(RelatoEntradaDto dto, CancellationToken ct = default);
}

public interface IIndicadorService
{
    Task<ImpactoDto> ObterImpactoAsync(CancellationToken ct = default);
}
