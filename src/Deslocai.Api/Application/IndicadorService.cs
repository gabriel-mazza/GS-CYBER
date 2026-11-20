using Deslocai.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Deslocai.Api.Application;


public class IndicadorService : IIndicadorService
{
    private readonly IRepositorio<Estacao> _estacoes;
    private readonly IRepositorio<Equipamento> _equipamentos;
    private readonly IRepositorio<Alerta> _alertas;
    private readonly IRepositorio<LeituraSensor> _leituras;

    public IndicadorService(
        IRepositorio<Estacao> estacoes,
        IRepositorio<Equipamento> equipamentos,
        IRepositorio<Alerta> alertas,
        IRepositorio<LeituraSensor> leituras)
    {
        _estacoes = estacoes;
        _equipamentos = equipamentos;
        _alertas = alertas;
        _leituras = leituras;
    }

    public async Task<ImpactoDto> ObterImpactoAsync(CancellationToken ct = default)
    {
        var desde = DateTime.UtcNow.AddHours(-24);

        var totalEstacoes = await _estacoes.Consultar().CountAsync(ct);
        var totalEquipamentos = await _equipamentos.Consultar().CountAsync(ct);
        var indisponiveis = await _equipamentos.Consultar()
            .CountAsync(e => e.Status != StatusEquipamento.Operante, ct);

        
        var estacoesSemElevador = await _equipamentos.Consultar()
            .Where(e => e.Tipo == TipoEquipamento.Elevador && e.Status != StatusEquipamento.Operante)
            .Select(e => e.EstacaoId)
            .Distinct()
            .CountAsync(ct);

        var porTipo = await _alertas.Consultar()
            .Where(a => a.ResolvidoEm == null)
            .GroupBy(a => a.Tipo)
            .Select(g => new { Tipo = g.Key, Quantidade = g.Count() })
            .ToListAsync(ct);

        var leituras = await _leituras.Consultar()
            .Where(l => l.RegistradaEm >= desde)
            .Include(l => l.Estacao)
            .ToListAsync(ct);

       
        var maisLotadas = leituras
            .Where(l => l.Estacao != null)
            .GroupBy(l => l.EstacaoId)
            .Select(g => g.OrderByDescending(l => l.RegistradaEm).First())
            .Select(l => new EstacaoLotadaDto(
                l.EstacaoId,
                l.Estacao!.Nome,
                l.Estacao.CalcularOcupacaoPercentual(l.PessoasNaPlataforma),
                l.RegistradaEm))
            .OrderByDescending(x => x.OcupacaoPercentual)
            .Take(5)
            .ToList();

        double? temperaturaMedia = leituras.Count == 0 ? null : Math.Round(leituras.Average(l => l.TemperaturaC), 1);

        var percentualOperantes = totalEquipamentos == 0
            ? 100.0
            : Math.Round((totalEquipamentos - indisponiveis) * 100.0 / totalEquipamentos, 1);

        return new ImpactoDto(
            totalEstacoes, totalEquipamentos, indisponiveis, percentualOperantes, estacoesSemElevador,
            porTipo.Select(x => new AlertaPorTipoDto(x.Tipo, x.Quantidade)).ToList(),
            maisLotadas, temperaturaMedia, DateTime.UtcNow);
    }
}
