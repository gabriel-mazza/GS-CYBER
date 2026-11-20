using Deslocai.Api.Application;
using Deslocai.Api.Domain;
using Deslocai.Api.Domain.Regras;
using Deslocai.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Deslocai.Tests;


public sealed class Contexto : IDisposable
{
    public DeslocaiDbContext Db { get; }

    public Contexto()
    {
        var opcoes = new DbContextOptionsBuilder<DeslocaiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        Db = new DeslocaiDbContext(opcoes);
    }

    public IRepositorio<T> Repo<T>() where T : EntidadeBase => new Repositorio<T>(Db);

    public AlertaService AlertaService() =>
        new(Repo<Alerta>(), new IRegraAlerta[] { new RegraSuperlotacao(), new RegraCalorExtremo() });

    public EquipamentoService EquipamentoService() =>
        new(Repo<Equipamento>(), Repo<Estacao>(), AlertaService());

    public LeituraService LeituraService() =>
        new(Repo<Estacao>(), Repo<LeituraSensor>(), AlertaService());

    public EstacaoService EstacaoService() =>
        new(Repo<Estacao>(), Repo<Equipamento>(), Repo<LeituraSensor>(), Repo<Alerta>());

    public RelatoService RelatoService() => new(Repo<Relato>(), Repo<Estacao>());

    public IndicadorService IndicadorService() =>
        new(Repo<Estacao>(), Repo<Equipamento>(), Repo<Alerta>(), Repo<LeituraSensor>());

    public async Task<Estacao> CriarEstacaoAsync(string nome = "Sé", int capacidade = 1000)
    {
        var estacao = new Estacao(nome, "Linha 1-Azul", TipoTransporte.Metro, -23.55, -46.63, capacidade);
        Db.Estacoes.Add(estacao);
        await Db.SaveChangesAsync();
        return estacao;
    }

    public void Dispose() => Db.Dispose();
}
