using Deslocai.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Deslocai.Api.Infrastructure;

public class DeslocaiDbContext : DbContext
{
    public DeslocaiDbContext(DbContextOptions<DeslocaiDbContext> options) : base(options) { }

    public DbSet<Estacao> Estacoes => Set<Estacao>();
    public DbSet<Equipamento> Equipamentos => Set<Equipamento>();
    public DbSet<LeituraSensor> Leituras => Set<LeituraSensor>();
    public DbSet<Alerta> Alertas => Set<Alerta>();
    public DbSet<Relato> Relatos => Set<Relato>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Estacao>(b =>
        {
            b.ToTable("Estacoes");
            b.Property(x => x.Nome).IsRequired().HasMaxLength(100);
            b.Property(x => x.Linha).IsRequired().HasMaxLength(60);
            b.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(20);
            b.HasIndex(x => new { x.Nome, x.Linha }).IsUnique();
        });

        mb.Entity<Equipamento>(b =>
        {
            b.ToTable("Equipamentos");
            b.Property(x => x.Identificador).IsRequired().HasMaxLength(40);
            b.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(20);
            b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            b.HasIndex(x => new { x.EstacaoId, x.Identificador }).IsUnique();
            b.HasOne(x => x.Estacao).WithMany(x => x.Equipamentos)
                .HasForeignKey(x => x.EstacaoId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<LeituraSensor>(b =>
        {
            b.ToTable("Leituras");
            b.HasIndex(x => new { x.EstacaoId, x.RegistradaEm });
            b.HasOne(x => x.Estacao).WithMany(x => x.Leituras)
                .HasForeignKey(x => x.EstacaoId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<Alerta>(b =>
        {
            b.ToTable("Alertas");
            b.Property(x => x.Mensagem).IsRequired().HasMaxLength(400);
            b.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(30);
            
            b.HasIndex(x => new { x.EstacaoId, x.Tipo, x.ResolvidoEm });
            b.HasOne(x => x.Estacao).WithMany(x => x.Alertas)
                .HasForeignKey(x => x.EstacaoId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<Relato>(b =>
        {
            b.ToTable("Relatos");
            b.Property(x => x.Descricao).IsRequired().HasMaxLength(280);
            b.Property(x => x.Tipo).HasConversion<string>().HasMaxLength(30);
            b.Property(x => x.PerfilRelator).HasConversion<string>().HasMaxLength(20);
            b.HasOne(x => x.Estacao).WithMany(x => x.Relatos)
                .HasForeignKey(x => x.EstacaoId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
