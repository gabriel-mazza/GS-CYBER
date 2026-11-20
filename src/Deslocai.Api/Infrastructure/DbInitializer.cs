using Deslocai.Api.Domain;
using Deslocai.Api.Domain.Regras;
using Microsoft.EntityFrameworkCore;

namespace Deslocai.Api.Infrastructure;


public static class DbInitializer
{
    public static async Task InicializarAsync(DeslocaiDbContext db, CancellationToken ct = default)
    {
        if (db.Database.GetMigrations().Any())
            await db.Database.MigrateAsync(ct);
        else
            await db.Database.EnsureCreatedAsync(ct);

        if (await db.Estacoes.AnyAsync(ct)) return;

        
        var se = new Estacao("Sé", "Linha 1-Azul", TipoTransporte.Metro, -23.5505, -46.6333, 3000);
        var luz = new Estacao("Luz", "Linha 7-Rubi", TipoTransporte.Trem, -23.5364, -46.6336, 2500);
        var barraFunda = new Estacao("Barra Funda", "Linha 3-Vermelha", TipoTransporte.Metro, -23.5265, -46.6667, 2800);
        var itaquera = new Estacao("Corinthians-Itaquera", "Linha 3-Vermelha", TipoTransporte.Metro, -23.5453, -46.4733, 3500);
        var consolacao = new Estacao("Consolação", "Linha 2-Verde", TipoTransporte.Metro, -23.5578, -46.6606, 1800);
        var terminal = new Estacao("Terminal Parque Dom Pedro II", "Terminal Central", TipoTransporte.Onibus, -23.5446, -46.6291, 2000);

        db.Estacoes.AddRange(se, luz, barraFunda, itaquera, consolacao, terminal);
        await db.SaveChangesAsync(ct); 

        var elevadorLuz2 = new Equipamento(luz.Id, "ELEV-LUZ-02", TipoEquipamento.Elevador, StatusEquipamento.Inoperante);
        db.Equipamentos.AddRange(
            new Equipamento(se.Id, "ELEV-SE-01", TipoEquipamento.Elevador),
            new Equipamento(se.Id, "ESC-SE-01", TipoEquipamento.EscadaRolante),
            new Equipamento(luz.Id, "ELEV-LUZ-01", TipoEquipamento.Elevador),
            elevadorLuz2,
            new Equipamento(barraFunda.Id, "ELEV-BF-01", TipoEquipamento.Elevador),
            new Equipamento(itaquera.Id, "ELEV-ITQ-01", TipoEquipamento.Elevador),
            new Equipamento(itaquera.Id, "ESC-ITQ-01", TipoEquipamento.EscadaRolante, StatusEquipamento.EmManutencao),
            new Equipamento(consolacao.Id, "ELEV-CON-01", TipoEquipamento.Elevador));
        await db.SaveChangesAsync(ct);

        var agora = DateTime.UtcNow;
        var leituras = new[]
        {
            (Estacao: se, Leitura: new LeituraSensor(se.Id, 33.0, 72, 3300, agora.AddMinutes(-5))),
            (Estacao: luz, Leitura: new LeituraSensor(luz.Id, 29.5, 60, 1200, agora.AddMinutes(-4))),
            (Estacao: barraFunda, Leitura: new LeituraSensor(barraFunda.Id, 37.0, 85, 2400, agora.AddMinutes(-3))),
            (Estacao: itaquera, Leitura: new LeituraSensor(itaquera.Id, 28.0, 55, 900, agora.AddMinutes(-2))),
            (Estacao: consolacao, Leitura: new LeituraSensor(consolacao.Id, 30.0, 65, 700, agora.AddMinutes(-1))),
        };
        db.Leituras.AddRange(leituras.Select(l => l.Leitura));

        
        IRegraAlerta[] regras = { new RegraSuperlotacao(), new RegraCalorExtremo() };
        foreach (var (estacao, leitura) in leituras)
            foreach (var regra in regras)
            {
                var alerta = regra.Avaliar(estacao, leitura);
                if (alerta is not null) db.Alertas.Add(alerta);
            }

        db.Alertas.Add(new Alerta(luz.Id, TipoAlerta.EquipamentoInoperante, Severidade.Alta,
            "Elevador ELEV-LUZ-02 indisponível (Inoperante). Pessoas com deficiência e idosos podem precisar de rota alternativa.",
            elevadorLuz2.Id));

        db.Relatos.Add(new Relato(se.Id, TipoRelato.Superlotacao, PerfilCidadao.Trabalhador,
            "Plataforma sentido Jabaquara muito cheia no horário de pico."));

        await db.SaveChangesAsync(ct);
    }
}
