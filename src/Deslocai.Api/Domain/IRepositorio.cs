namespace Deslocai.Api.Domain;


public interface IRepositorio<T> where T : EntidadeBase
{
    Task<T?> ObterPorIdAsync(int id, CancellationToken ct = default);

   
    IQueryable<T> Consultar(bool rastrear = false);

    Task AdicionarAsync(T entidade, CancellationToken ct = default);
    void Remover(T entidade);
    Task<int> SalvarAsync(CancellationToken ct = default);
}
