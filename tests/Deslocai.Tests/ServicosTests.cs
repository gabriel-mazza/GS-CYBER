using Deslocai.Api.Application;
using Deslocai.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Deslocai.Tests;

public class LeituraServiceTests
{
    private static LeituraEntradaDto Leitura(int estacaoId, int pessoas, double temp = 25, double umidade = 50) =>
        new() { EstacaoId = estacaoId, TemperaturaC = temp, UmidadePercentual = umidade, PessoasNaPlataforma = pessoas };

    [Fact]
    public async Task Registrar_PlataformaLotada_GeraAlertaDeSuperlotacao()
    {
        using var ctx = new Contexto();
        var estacao = await ctx.CriarEstacaoAsync(capacidade: 1000);

        var resultado = await ctx.LeituraService().RegistrarAsync(Leitura(estacao.Id, 1100));

        var alerta = Assert.Single(resultado.AlertasGerados);
        Assert.Equal(TipoAlerta.Superlotacao, alerta.Tipo);
        Assert.Equal(Severidade.Alta, alerta.Severidade);
        Assert.Equal(110.0, resultado.Leitura.OcupacaoPercentual);
        Assert.True(alerta.Id > 0);
    }

    [Fact]
    public async Task Registrar_LeituraNormal_NaoGeraAlerta()
    {
        using var ctx = new Contexto();
        var estacao = await ctx.CriarEstacaoAsync(capacidade: 1000);

        var resultado = await ctx.LeituraService().RegistrarAsync(Leitura(estacao.Id, 200));

        Assert.Empty(resultado.AlertasGerados);
        Assert.Equal(0, await ctx.Db.Alertas.CountAsync());
    }

    [Fact]
    public async Task Registrar_SegundaLeituraLotada_NaoDuplicaAlerta()
    {
        using var ctx = new Contexto();
        var estacao = await ctx.CriarEstacaoAsync(capacidade: 1000);
        var servico = ctx.LeituraService();

        await servico.RegistrarAsync(Leitura(estacao.Id, 1100));
        var segunda = await servico.RegistrarAsync(Leitura(estacao.Id, 1150));

        Assert.Empty(segunda.AlertasGerados);
        Assert.Equal(1, await ctx.Db.Alertas.CountAsync());
    }

    [Fact]
    public async Task Registrar_QuandoNormaliza_EncerraOAlertaAberto()
    {
        using var ctx = new Contexto();
        var estacao = await ctx.CriarEstacaoAsync(capacidade: 1000);
        var servico = ctx.LeituraService();

        await servico.RegistrarAsync(Leitura(estacao.Id, 1100));
        await servico.RegistrarAsync(Leitura(estacao.Id, 100));

        Assert.Equal(0, await ctx.Db.Alertas.CountAsync(a => a.ResolvidoEm == null));
        Assert.Equal(1, await ctx.Db.Alertas.CountAsync(a => a.ResolvidoEm != null));
    }

    [Fact]
    public async Task Registrar_CalorEPlataformaLotada_GeraDoisAlertas()
    {
        using var ctx = new Contexto();
        var estacao = await ctx.CriarEstacaoAsync(capacidade: 1000);

        var resultado = await ctx.LeituraService()
            .RegistrarAsync(Leitura(estacao.Id, 900, temp: 37, umidade: 90));

        Assert.Equal(2, resultado.AlertasGerados.Count);
        Assert.Contains(resultado.AlertasGerados, a => a.Tipo == TipoAlerta.Superlotacao);
        Assert.Contains(resultado.AlertasGerados, a => a.Tipo == TipoAlerta.CalorExtremo);
    }

    [Fact]
    public async Task Registrar_EstacaoInexistente_LancaNaoEncontrado()
    {
        using var ctx = new Contexto();
        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() =>
            ctx.LeituraService().RegistrarAsync(Leitura(999, 10)));
    }

    [Fact]
    public async Task Ultimas_RetornaMaisRecentesPrimeiroELimitaQuantidade()
    {
        using var ctx = new Contexto();
        var estacao = await ctx.CriarEstacaoAsync();
        var agora = DateTime.UtcNow;
        for (var i = 0; i < 5; i++)
            ctx.Db.Leituras.Add(new LeituraSensor(estacao.Id, 25, 50, 100 + i, agora.AddMinutes(-i)));
        await ctx.Db.SaveChangesAsync();

        var ultimas = await ctx.LeituraService().UltimasAsync(estacao.Id, 3);

        Assert.Equal(3, ultimas.Count);
        Assert.Equal(100, ultimas[0].PessoasNaPlataforma); 
    }
}

public class EquipamentoServiceTests
{
    private static async Task<EquipamentoDto> CriarElevadorAsync(Contexto ctx, int estacaoId, string id = "ELEV-01") =>
        await ctx.EquipamentoService().CriarAsync(new EquipamentoEntradaDto
        {
            EstacaoId = estacaoId,
            Identificador = id,
            Tipo = TipoEquipamento.Elevador
        });

    [Fact]
    public async Task Criar_EstacaoInexistente_LancaNaoEncontrado()
    {
        using var ctx = new Contexto();
        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() => CriarElevadorAsync(ctx, 999));
    }

    [Fact]
    public async Task Criar_IdentificadorDuplicadoNaMesmaEstacao_LancaConflito()
    {
        using var ctx = new Contexto();
        var estacao = await ctx.CriarEstacaoAsync();
        await CriarElevadorAsync(ctx, estacao.Id);

        await Assert.ThrowsAsync<ConflitoException>(() => CriarElevadorAsync(ctx, estacao.Id));
    }

    [Fact]
    public async Task AlterarStatus_ElevadorInoperante_AbreAlertaDeAcessibilidade()
    {
        using var ctx = new Contexto();
        var estacao = await ctx.CriarEstacaoAsync();
        var elevador = await CriarElevadorAsync(ctx, estacao.Id);

        await ctx.EquipamentoService().AlterarStatusAsync(elevador.Id, StatusEquipamento.Inoperante);

        var alerta = await ctx.Db.Alertas.SingleAsync();
        Assert.Equal(TipoAlerta.EquipamentoInoperante, alerta.Tipo);
        Assert.Equal(Severidade.Alta, alerta.Severidade);
        Assert.Equal(elevador.Id, alerta.EquipamentoId);
        Assert.True(alerta.Ativo);
    }

    [Fact]
    public async Task AlterarStatus_VoltaAOperar_EncerraOAlerta()
    {
        using var ctx = new Contexto();
        var estacao = await ctx.CriarEstacaoAsync();
        var elevador = await CriarElevadorAsync(ctx, estacao.Id);
        var servico = ctx.EquipamentoService();

        await servico.AlterarStatusAsync(elevador.Id, StatusEquipamento.Inoperante);
        await servico.AlterarStatusAsync(elevador.Id, StatusEquipamento.Operante);

        Assert.Equal(0, await ctx.Db.Alertas.CountAsync(a => a.ResolvidoEm == null));
    }

    [Fact]
    public async Task AlterarStatus_InoperanteParaManutencao_NaoDuplicaAlerta()
    {
        using var ctx = new Contexto();
        var estacao = await ctx.CriarEstacaoAsync();
        var elevador = await CriarElevadorAsync(ctx, estacao.Id);
        var servico = ctx.EquipamentoService();

        await servico.AlterarStatusAsync(elevador.Id, StatusEquipamento.Inoperante);
        await servico.AlterarStatusAsync(elevador.Id, StatusEquipamento.EmManutencao);

        Assert.Equal(1, await ctx.Db.Alertas.CountAsync());
    }

    [Fact]
    public async Task AlterarStatus_EquipamentoInexistente_LancaNaoEncontrado()
    {
        using var ctx = new Contexto();
        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() =>
            ctx.EquipamentoService().AlterarStatusAsync(999, StatusEquipamento.Inoperante));
    }

    [Fact]
    public async Task Listar_FiltraPorStatus()
    {
        using var ctx = new Contexto();
        var estacao = await ctx.CriarEstacaoAsync();
        var a = await CriarElevadorAsync(ctx, estacao.Id, "ELEV-A");
        await CriarElevadorAsync(ctx, estacao.Id, "ELEV-B");
        await ctx.EquipamentoService().AlterarStatusAsync(a.Id, StatusEquipamento.Inoperante);

        var inoperantes = await ctx.EquipamentoService().ListarAsync(estacao.Id, StatusEquipamento.Inoperante);

        var unico = Assert.Single(inoperantes);
        Assert.Equal("ELEV-A", unico.Identificador);
    }
}

public class EstacaoServiceTests
{
    private static EstacaoEntradaDto NovaEstacao(string nome = "Luz", string linha = "Linha 7-Rubi") => new()
    {
        Nome = nome,
        Linha = linha,
        Tipo = TipoTransporte.Trem,
        Latitude = -23.53,
        Longitude = -46.63,
        CapacidadeMaxima = 2500
    };

    [Fact]
    public async Task Criar_Duplicada_LancaConflito()
    {
        using var ctx = new Contexto();
        var servico = ctx.EstacaoService();
        await servico.CriarAsync(NovaEstacao());

        await Assert.ThrowsAsync<ConflitoException>(() => servico.CriarAsync(NovaEstacao()));
    }

    [Fact]
    public async Task Listar_FiltraPorTipoELinha()
    {
        using var ctx = new Contexto();
        var servico = ctx.EstacaoService();
        await servico.CriarAsync(NovaEstacao("Luz", "Linha 7-Rubi"));
        await ctx.CriarEstacaoAsync("Sé"); 

        var trens = await servico.ListarAsync(TipoTransporte.Trem, null);
        var porLinha = await servico.ListarAsync(null, "azul");

        Assert.Equal("Luz", Assert.Single(trens).Nome);
        Assert.Equal("Sé", Assert.Single(porLinha).Nome); 
    }

    [Fact]
    public async Task Obter_ComElevadorParado_NaoEAcessivelParaPcd()
    {
        using var ctx = new Contexto();
        var estacao = await ctx.CriarEstacaoAsync();
        var elevador = await ctx.EquipamentoService().CriarAsync(new EquipamentoEntradaDto
        {
            EstacaoId = estacao.Id,
            Identificador = "ELEV-01",
            Tipo = TipoEquipamento.Elevador
        });

        var antes = await ctx.EstacaoService().ObterAsync(estacao.Id);
        await ctx.EquipamentoService().AlterarStatusAsync(elevador.Id, StatusEquipamento.Inoperante);
        var depois = await ctx.EstacaoService().ObterAsync(estacao.Id);

        Assert.True(antes.AcessivelParaPcd);
        Assert.False(depois.AcessivelParaPcd);
        Assert.Equal(1, depois.EquipamentosIndisponiveis);
        Assert.Equal(1, depois.AlertasAtivos);
    }

    [Fact]
    public async Task Obter_InformaOcupacaoDaUltimaLeitura()
    {
        using var ctx = new Contexto();
        var estacao = await ctx.CriarEstacaoAsync(capacidade: 1000);
        await ctx.LeituraService().RegistrarAsync(new LeituraEntradaDto
        {
            EstacaoId = estacao.Id, TemperaturaC = 30, UmidadePercentual = 60, PessoasNaPlataforma = 500
        });

        var detalhe = await ctx.EstacaoService().ObterAsync(estacao.Id);

        Assert.Equal(50.0, detalhe.OcupacaoAtualPercentual);
        Assert.Equal(30.0, detalhe.TemperaturaAtualC);
    }

    [Fact]
    public async Task Atualizar_EstacaoInexistente_LancaNaoEncontrado()
    {
        using var ctx = new Contexto();
        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() =>
            ctx.EstacaoService().AtualizarAsync(999, NovaEstacao()));
    }

    [Fact]
    public async Task Remover_ExcluiAEstacao()
    {
        using var ctx = new Contexto();
        var estacao = await ctx.CriarEstacaoAsync();

        await ctx.EstacaoService().RemoverAsync(estacao.Id);

        Assert.Equal(0, await ctx.Db.Estacoes.CountAsync());
    }
}

public class RelatoServiceTests
{
    [Fact]
    public async Task Criar_EstacaoInexistente_LancaNaoEncontrado()
    {
        using var ctx = new Contexto();
        await Assert.ThrowsAsync<RecursoNaoEncontradoException>(() =>
            ctx.RelatoService().CriarAsync(new RelatoEntradaDto
            {
                EstacaoId = 999, Tipo = TipoRelato.Calor, PerfilRelator = PerfilCidadao.Idoso, Descricao = "Muito calor na plataforma"
            }));
    }

    [Fact]
    public async Task Criar_E_Listar_FiltraPorTipo()
    {
        using var ctx = new Contexto();
        var estacao = await ctx.CriarEstacaoAsync();
        var servico = ctx.RelatoService();

        await servico.CriarAsync(new RelatoEntradaDto
        {
            EstacaoId = estacao.Id, Tipo = TipoRelato.Calor, PerfilRelator = PerfilCidadao.Idoso, Descricao = "Muito calor na plataforma"
        });
        await servico.CriarAsync(new RelatoEntradaDto
        {
            EstacaoId = estacao.Id, Tipo = TipoRelato.Superlotacao, PerfilRelator = PerfilCidadao.Trabalhador, Descricao = "Trem lotado no pico"
        });

        var calor = await servico.ListarAsync(estacao.Id, TipoRelato.Calor);

        Assert.Equal(PerfilCidadao.Idoso, Assert.Single(calor).PerfilRelator);
    }
}

public class IndicadorServiceTests
{
    [Fact]
    public async Task ObterImpacto_CalculaDisponibilidadeEEstacoesSemElevador()
    {
        using var ctx = new Contexto();
        var estacao = await ctx.CriarEstacaoAsync(capacidade: 1000);
        var equipamentos = ctx.EquipamentoService();

        var elevador = await equipamentos.CriarAsync(new EquipamentoEntradaDto
        {
            EstacaoId = estacao.Id, Identificador = "ELEV-01", Tipo = TipoEquipamento.Elevador
        });
        await equipamentos.CriarAsync(new EquipamentoEntradaDto
        {
            EstacaoId = estacao.Id, Identificador = "ESC-01", Tipo = TipoEquipamento.EscadaRolante
        });
        await equipamentos.AlterarStatusAsync(elevador.Id, StatusEquipamento.Inoperante);
        await ctx.LeituraService().RegistrarAsync(new LeituraEntradaDto
        {
            EstacaoId = estacao.Id, TemperaturaC = 30, UmidadePercentual = 60, PessoasNaPlataforma = 900
        });

        var impacto = await ctx.IndicadorService().ObterImpactoAsync();

        Assert.Equal(1, impacto.TotalEstacoes);
        Assert.Equal(2, impacto.TotalEquipamentos);
        Assert.Equal(1, impacto.EquipamentosIndisponiveis);
        Assert.Equal(50.0, impacto.PercentualEquipamentosOperantes);
        Assert.Equal(1, impacto.EstacoesSemElevadorOperante);
        Assert.Contains(impacto.AlertasAtivosPorTipo, x => x.Tipo == TipoAlerta.EquipamentoInoperante && x.Quantidade == 1);
        Assert.Contains(impacto.AlertasAtivosPorTipo, x => x.Tipo == TipoAlerta.Superlotacao && x.Quantidade == 1);
        Assert.Equal(90.0, Assert.Single(impacto.EstacoesMaisLotadasUltimas24h).OcupacaoPercentual);
    }

    [Fact]
    public async Task ObterImpacto_SemDados_RetornaZerosSemErro()
    {
        using var ctx = new Contexto();

        var impacto = await ctx.IndicadorService().ObterImpactoAsync();

        Assert.Equal(0, impacto.TotalEstacoes);
        Assert.Equal(100.0, impacto.PercentualEquipamentosOperantes);
        Assert.Null(impacto.TemperaturaMediaUltimas24hC);
        Assert.Empty(impacto.EstacoesMaisLotadasUltimas24h);
    }
}
