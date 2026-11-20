using Deslocai.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Deslocai.Api.Application;

public class EquipamentoService : IEquipamentoService
{
    private readonly IRepositorio<Equipamento> _equipamentos;
    private readonly IRepositorio<Estacao> _estacoes;
    private readonly IAlertaService _alertas;

    public EquipamentoService(IRepositorio<Equipamento> equipamentos, IRepositorio<Estacao> estacoes, IAlertaService alertas)
    {
        _equipamentos = equipamentos;
        _estacoes = estacoes;
        _alertas = alertas;
    }

    public async Task<IReadOnlyList<EquipamentoDto>> ListarAsync(int? estacaoId, StatusEquipamento? status, CancellationToken ct = default)
    {
        var query = _equipamentos.Consultar();

        if (estacaoId.HasValue) query = query.Where(e => e.EstacaoId == estacaoId.Value);
        if (status.HasValue) query = query.Where(e => e.Status == status.Value);

        var lista = await query.OrderBy(e => e.EstacaoId).ThenBy(e => e.Identificador).ToListAsync(ct);
        return lista.Select(e => e.ParaDto()).ToList();
    }

    public async Task<EquipamentoDto> CriarAsync(EquipamentoEntradaDto dto, CancellationToken ct = default)
    {
        var estacaoExiste = await _estacoes.Consultar().AnyAsync(e => e.Id == dto.EstacaoId, ct);
        if (!estacaoExiste)
            throw new RecursoNaoEncontradoException(nameof(Estacao), dto.EstacaoId);

        var identificador = dto.Identificador.Trim();
        var duplicado = await _equipamentos.Consultar()
            .AnyAsync(e => e.EstacaoId == dto.EstacaoId && e.Identificador == identificador, ct);
        if (duplicado)
            throw new ConflitoException($"Já existe o equipamento '{identificador}' nesta estação.");

        var equipamento = new Equipamento(dto.EstacaoId, identificador, dto.Tipo);
        await _equipamentos.AdicionarAsync(equipamento, ct);
        await _equipamentos.SalvarAsync(ct);
        return equipamento.ParaDto();
    }

    public async Task<EquipamentoDto> AlterarStatusAsync(int id, StatusEquipamento novoStatus, CancellationToken ct = default)
    {
        var equipamento = await _equipamentos.ObterPorIdAsync(id, ct)
                          ?? throw new RecursoNaoEncontradoException(nameof(Equipamento), id);

        if (equipamento.AlterarStatus(novoStatus))
            await _alertas.SincronizarEquipamentoAsync(equipamento, ct); 

        await _equipamentos.SalvarAsync(ct); 
        return equipamento.ParaDto();
    }
}
