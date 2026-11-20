using Deslocai.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Deslocai.Api.Application;

public class LeituraService : ILeituraService
{
    private readonly IRepositorio<Estacao> _estacoes;
    private readonly IRepositorio<LeituraSensor> _leituras;
    private readonly IAlertaService _alertas;

    public LeituraService(IRepositorio<Estacao> estacoes, IRepositorio<LeituraSensor> leituras, IAlertaService alertas)
    {
        _estacoes = estacoes;
        _leituras = leituras;
        _alertas = alertas;
    }

    public async Task<LeituraRegistradaDto> RegistrarAsync(LeituraEntradaDto dto, CancellationToken ct = default)
    {
        var estacao = await _estacoes.ObterPorIdAsync(dto.EstacaoId, ct)
                      ?? throw new RecursoNaoEncontradoException(nameof(Estacao), dto.EstacaoId);

        var leitura = new LeituraSensor(estacao.Id, dto.TemperaturaC, dto.UmidadePercentual, dto.PessoasNaPlataforma);
        await _leituras.AdicionarAsync(leitura, ct);

        var gerados = await _alertas.AvaliarLeituraAsync(estacao, leitura, ct);
        await _leituras.SalvarAsync(ct); 

        return new LeituraRegistradaDto(
            leitura.ParaDto(estacao.CapacidadeMaxima),
            gerados.Select(a => a.ParaDto()).ToList());
    }

    public async Task<IReadOnlyList<LeituraDto>> UltimasAsync(int estacaoId, int quantidade, CancellationToken ct = default)
    {
        var estacao = await _estacoes.ObterPorIdAsync(estacaoId, ct)
                      ?? throw new RecursoNaoEncontradoException(nameof(Estacao), estacaoId);

        var limite = Math.Clamp(quantidade, 1, 100);

        var lista = await _leituras.Consultar()
            .Where(l => l.EstacaoId == estacaoId)
            .OrderByDescending(l => l.RegistradaEm)
            .Take(limite)
            .ToListAsync(ct);

        return lista.Select(l => l.ParaDto(estacao.CapacidadeMaxima)).ToList();
    }
}
