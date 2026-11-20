using Deslocai.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Deslocai.Api.Infrastructure;


public class Repositorio<T> : IRepositorio<T> where T : EntidadeBase
{
    private readonly DeslocaiDbContext _db;

    public Repositorio(DeslocaiDbContext db) => _db = db;

    public async Task<T?> ObterPorIdAsync(int id, CancellationToken ct = default) =>
        await _db.Set<T>().FindAsync(new object[] { id }, ct);

    public IQueryable<T> Consultar(bool rastrear = false) =>
        rastrear ? _db.Set<T>() : _db.Set<T>().AsNoTracking();

    public async Task AdicionarAsync(T entidade, CancellationToken ct = default) =>
        await _db.Set<T>().AddAsync(entidade, ct);

    public void Remover(T entidade) => _db.Set<T>().Remove(entidade);

    public Task<int> SalvarAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
