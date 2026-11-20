namespace Deslocai.Api.Domain;


public abstract class EntidadeBase
{
    public int Id { get; protected set; }
    public DateTime CriadoEm { get; protected set; } = DateTime.UtcNow;
}
