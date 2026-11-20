using Deslocai.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Deslocai.Api.Application;

public class EstacaoService : IEstacaoService
{
    private readonly IRepositorio<Estacao> _estacoes;
    private readonly IRepositorio<Equipamento> _equipamentos;
    private readonly IRepositorio<LeituraSensor> _leituras;
    private readonly IRepositorio<Alerta> _alertas;

    public EstacaoService(
        IRepositorio<Estacao> estacoes,
        IRepositorio<Equipamento> equipamentos,
        IRepositorio<LeituraSensor> leituras,
        IRepositorio<Alerta> alertas)
    {
        _estacoes = estacoes;
        _equipamentos = equipamentos;
        _leituras = leituras;
        _alertas = alertas;
    }

    public async Task<IReadOnlyList<EstacaoDto>> ListarAsync(TipoTransporte? tipo, string? linha, CancellationToken ct = default)
    {
        var query = _estacoes.Consultar();

        if (tipo.HasValue)
            query = query.Where(e => e.Tipo == tipo.Value);

        if (!string.IsNullOrWhiteSpace(linha))
            query = query.Where(e => EF.Functions.Like(e.Linha, $"%{linha.Trim()}%"));

        var lista = await query.OrderBy(e => e.Nome).ToListAsync(ct);
        return lista.Select(e => e.ParaDto()).ToList();
    }

    public async Task<EstacaoDetalheDto> ObterAsync(int id, CancellationToken ct = default)
    {
        var estacao = await ObterOuFalharAsync(id, ct);

        var indisponiveis = await _equipamentos.Consultar()
            .CountAsync(e => e.EstacaoId == id && e.Status != StatusEquipamento.Operante, ct);

        var elevadoresIndisponiveis = await _equipamentos.Consultar()
            .CountAsync(e => e.EstacaoId == id
                             && e.Tipo == TipoEquipamento.Elevador
                             && e.Status != StatusEquipamento.Operante, ct);

        var alertasAtivos = await _alertas.Consultar()
            .CountAsync(a => a.EstacaoId == id && a.ResolvidoEm == null, ct);

        var ultima = await _leituras.Consultar()
            .Where(l => l.EstacaoId == id)
            .OrderByDescending(l => l.RegistradaEm)
            .FirstOrDefaultAsync(ct);

        double? ocupacao = ultima is null ? null : estacao.CalcularOcupacaoPercentual(ultima.PessoasNaPlataforma);
        double? temperatura = ultima?.TemperaturaC;

        return new EstacaoDetalheDto(
            estacao.Id, estacao.Nome, estacao.Linha, estacao.Tipo,
            estacao.Latitude, estacao.Longitude, estacao.CapacidadeMaxima,
            AcessivelParaPcd: elevadoresIndisponiveis == 0,
            EquipamentosIndisponiveis: indisponiveis,
            AlertasAtivos: alertasAtivos,
            OcupacaoAtualPercentual: ocupacao,
            TemperaturaAtualC: temperatura,
            UltimaLeituraEm: ultima?.RegistradaEm);
    }

    public async Task<EstacaoDto> CriarAsync(EstacaoEntradaDto dto, CancellationToken ct = default)
    {
        await GarantirNaoDuplicadaAsync(dto.Nome, dto.Linha, ignorarId: null, ct);

        var estacao = new Estacao(dto.Nome, dto.Linha, dto.Tipo, dto.Latitude, dto.Longitude, dto.CapacidadeMaxima);
        await _estacoes.AdicionarAsync(estacao, ct);
        await _estacoes.SalvarAsync(ct);
        return estacao.ParaDto();
    }

    public async Task<EstacaoDto> AtualizarAsync(int id, EstacaoEntradaDto dto, CancellationToken ct = default)
    {
        var estacao = await ObterOuFalharAsync(id, ct);
        await GarantirNaoDuplicadaAsync(dto.Nome, dto.Linha, ignorarId: id, ct);

        estacao.Atualizar(dto.Nome, dto.Linha, dto.Tipo, dto.Latitude, dto.Longitude, dto.CapacidadeMaxima);
        await _estacoes.SalvarAsync(ct);
        return estacao.ParaDto();
    }

    public async Task RemoverAsync(int id, CancellationToken ct = default)
    {
        var estacao = await ObterOuFalharAsync(id, ct);
        _estacoes.Remover(estacao); 
        await _estacoes.SalvarAsync(ct);
    }

    private async Task<Estacao> ObterOuFalharAsync(int id, CancellationToken ct) =>
        await _estacoes.ObterPorIdAsync(id, ct) ?? throw new RecursoNaoEncontradoException(nameof(Estacao), id);

    private async Task GarantirNaoDuplicadaAsync(string nome, string linha, int? ignorarId, CancellationToken ct)
    {
        var n = nome.Trim();
        var l = linha.Trim();
        var existe = await _estacoes.Consultar()
            .AnyAsync(e => e.Nome == n && e.Linha == l && (ignorarId == null || e.Id != ignorarId), ct);

        if (existe)
            throw new ConflitoException($"Já existe a estação '{n}' na linha '{l}'.");
    }
}
