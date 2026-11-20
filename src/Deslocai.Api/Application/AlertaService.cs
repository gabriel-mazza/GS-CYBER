using Deslocai.Api.Domain;
using Deslocai.Api.Domain.Regras;
using Microsoft.EntityFrameworkCore;

namespace Deslocai.Api.Application;

public class AlertaService : IAlertaService
{
    private readonly IRepositorio<Alerta> _alertas;
    private readonly IEnumerable<IRegraAlerta> _regras; 

    public AlertaService(IRepositorio<Alerta> alertas, IEnumerable<IRegraAlerta> regras)
    {
        _alertas = alertas;
        _regras = regras;
    }

    public async Task<IReadOnlyList<AlertaDto>> ListarAsync(int? estacaoId, TipoAlerta? tipo, bool incluirResolvidos, CancellationToken ct = default)
    {
        var query = _alertas.Consultar();

        if (estacaoId.HasValue) query = query.Where(a => a.EstacaoId == estacaoId.Value);
        if (tipo.HasValue) query = query.Where(a => a.Tipo == tipo.Value);
        if (!incluirResolvidos) query = query.Where(a => a.ResolvidoEm == null);

        var lista = await query
            .OrderByDescending(a => a.Severidade)
            .ThenByDescending(a => a.CriadoEm)
            .ToListAsync(ct);

        return lista.Select(a => a.ParaDto()).ToList();
    }

    public async Task<AlertaDto> ResolverAsync(int id, CancellationToken ct = default)
    {
        var alerta = await _alertas.ObterPorIdAsync(id, ct)
                     ?? throw new RecursoNaoEncontradoException(nameof(Alerta), id);

        alerta.Resolver();
        await _alertas.SalvarAsync(ct);
        return alerta.ParaDto();
    }

    public async Task<IReadOnlyList<Alerta>> AvaliarLeituraAsync(Estacao estacao, LeituraSensor leitura, CancellationToken ct = default)
    {
        var gerados = new List<Alerta>();

        foreach (var regra in _regras) 
        {
            var tipo = regra.Tipo;
            var abertos = await _alertas.Consultar(rastrear: true)
                .Where(a => a.EstacaoId == estacao.Id && a.Tipo == tipo && a.ResolvidoEm == null)
                .ToListAsync(ct);

            var novo = regra.Avaliar(estacao, leitura);

            if (novo is null)
            {
               
                foreach (var aberto in abertos) aberto.Resolver();
                continue;
            }

            if (abertos.Count > 0) continue; 

            await _alertas.AdicionarAsync(novo, ct);
            gerados.Add(novo);
        }

        return gerados;
    }

    public async Task SincronizarEquipamentoAsync(Equipamento equipamento, CancellationToken ct = default)
    {
        var abertos = await _alertas.Consultar(rastrear: true)
            .Where(a => a.EquipamentoId == equipamento.Id && a.ResolvidoEm == null)
            .ToListAsync(ct);

        if (equipamento.EstaDisponivel)
        {
            foreach (var aberto in abertos) aberto.Resolver();
            return;
        }

        if (abertos.Count > 0) return;

        var severidade = equipamento.Tipo == TipoEquipamento.Elevador ? Severidade.Alta : Severidade.Media;
        var mensagem = $"{equipamento.Tipo} {equipamento.Identificador} indisponível ({equipamento.Status}). " +
                       "Pessoas com deficiência e idosos podem precisar de rota alternativa.";

        await _alertas.AdicionarAsync(
            new Alerta(equipamento.EstacaoId, TipoAlerta.EquipamentoInoperante, severidade, mensagem, equipamento.Id), ct);
    }
}
