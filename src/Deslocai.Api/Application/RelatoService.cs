using Deslocai.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Deslocai.Api.Application;

public class RelatoService : IRelatoService
{
    private readonly IRepositorio<Relato> _relatos;
    private readonly IRepositorio<Estacao> _estacoes;
    private readonly IDescricaoSanitizer? _sanitizer;

    public RelatoService(IRepositorio<Relato> relatos, IRepositorio<Estacao> estacoes, IDescricaoSanitizer? sanitizer = null)
    {
        _relatos = relatos;
        _estacoes = estacoes;
        _sanitizer = sanitizer;
    }

    public async Task<IReadOnlyList<RelatoDto>> ListarAsync(int? estacaoId, TipoRelato? tipo, CancellationToken ct = default)
    {
        var query = _relatos.Consultar();

        if (estacaoId.HasValue) query = query.Where(r => r.EstacaoId == estacaoId.Value);
        if (tipo.HasValue) query = query.Where(r => r.Tipo == tipo.Value);

        var lista = await query.OrderByDescending(r => r.CriadoEm).Take(100).ToListAsync(ct);
        return lista.Select(r => r.ParaDto()).ToList();
    }

    public async Task<RelatoDto> CriarAsync(RelatoEntradaDto dto, CancellationToken ct = default)
    {
        var estacaoExiste = await _estacoes.Consultar().AnyAsync(e => e.Id == dto.EstacaoId, ct);
        if (!estacaoExiste)
            throw new RecursoNaoEncontradoException(nameof(Estacao), dto.EstacaoId);

        
        var descricao = _sanitizer?.Sanitizar(dto.Descricao) ?? dto.Descricao;
        var relato = new Relato(dto.EstacaoId, dto.Tipo, dto.PerfilRelator, descricao);
        await _relatos.AdicionarAsync(relato, ct);
        await _relatos.SalvarAsync(ct);
        return relato.ParaDto();
    }
}
