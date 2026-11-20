using Deslocai.Api.Domain;
using Deslocai.Api.Domain.Regras;

namespace Deslocai.Tests;

public class EstacaoTests
{
    private static Estacao CriarEstacao(int capacidade = 1000) =>
        new("Sé", "Linha 1-Azul", TipoTransporte.Metro, -23.55, -46.63, capacidade);

    [Fact]
    public void CalcularOcupacao_RetornaPercentualDaCapacidade()
    {
        var estacao = CriarEstacao(2000);
        Assert.Equal(55.0, estacao.CalcularOcupacaoPercentual(1100));
    }

    [Fact]
    public void Construtor_CapacidadeZero_LancaRegraDeNegocio() =>
        Assert.Throws<RegraDeNegocioException>(() => CriarEstacao(0));

    [Fact]
    public void Construtor_NomeVazio_LancaRegraDeNegocio() =>
        Assert.Throws<RegraDeNegocioException>(() =>
            new Estacao("  ", "Linha 1", TipoTransporte.Trem, 0, 0, 100));

    [Fact]
    public void Atualizar_AplicaOsNovosValores()
    {
        var estacao = CriarEstacao();
        estacao.Atualizar("Luz", "Linha 7-Rubi", TipoTransporte.Trem, -23.53, -46.63, 2500);

        Assert.Equal("Luz", estacao.Nome);
        Assert.Equal(TipoTransporte.Trem, estacao.Tipo);
        Assert.Equal(2500, estacao.CapacidadeMaxima);
    }
}

public class EquipamentoTests
{
    [Fact]
    public void AlterarStatus_QuandoMuda_RetornaTrueEAtualizaDisponibilidade()
    {
        var elevador = new Equipamento(1, "ELEV-01", TipoEquipamento.Elevador);
        Assert.True(elevador.EstaDisponivel);

        var mudou = elevador.AlterarStatus(StatusEquipamento.Inoperante);

        Assert.True(mudou);
        Assert.False(elevador.EstaDisponivel);
    }

    [Fact]
    public void AlterarStatus_MesmoStatus_RetornaFalse()
    {
        var elevador = new Equipamento(1, "ELEV-01", TipoEquipamento.Elevador);
        Assert.False(elevador.AlterarStatus(StatusEquipamento.Operante));
    }
}

public class LeituraSensorTests
{
    [Theory]
    [InlineData(200, 50, 10)]  
    [InlineData(25, 150, 10)]   
    [InlineData(25, 50, -1)]   
    public void Construtor_ValoresInvalidos_LancaRegraDeNegocio(double temp, double umidade, int pessoas) =>
        Assert.Throws<RegraDeNegocioException>(() => new LeituraSensor(1, temp, umidade, pessoas));
}

public class AlertaTests
{
    [Fact]
    public void Resolver_DuasVezes_LancaRegraDeNegocio()
    {
        var alerta = new Alerta(1, TipoAlerta.Superlotacao, Severidade.Alta, "Plataforma lotada");
        alerta.Resolver();

        Assert.False(alerta.Ativo);
        Assert.Throws<RegraDeNegocioException>(() => alerta.Resolver());
    }
}

public class RegraSuperlotacaoTests
{
    private static Estacao CriarEstacao() =>
        new("Sé", "Linha 1-Azul", TipoTransporte.Metro, -23.55, -46.63, 1000);

    [Fact]
    public void Avaliar_AbaixoDoLimite_NaoGeraAlerta()
    {
        var leitura = new LeituraSensor(1, 25, 50, 799);
        Assert.Null(new RegraSuperlotacao().Avaliar(CriarEstacao(), leitura));
    }

    [Theory]
    [InlineData(800, Severidade.Media)]
    [InlineData(1000, Severidade.Alta)]
    [InlineData(1200, Severidade.Critica)]
    public void Avaliar_AcimaDoLimite_GeraAlertaComSeveridadeProporcional(int pessoas, Severidade esperada)
    {
        var leitura = new LeituraSensor(1, 25, 50, pessoas);

        var alerta = new RegraSuperlotacao().Avaliar(CriarEstacao(), leitura);

        Assert.NotNull(alerta);
        Assert.Equal(TipoAlerta.Superlotacao, alerta!.Tipo);
        Assert.Equal(esperada, alerta.Severidade);
    }
}

public class RegraCalorExtremoTests
{
    private static Estacao CriarEstacao() =>
        new("Barra Funda", "Linha 3-Vermelha", TipoTransporte.Metro, -23.52, -46.66, 1000);

    [Fact]
    public void CalcularSensacao_SomaUmGrauACadaDezPontosDeUmidadeAcimaDe60() =>
        Assert.Equal(40.0, RegraCalorExtremo.CalcularSensacao(37, 90), precision: 5);

    [Fact]
    public void Avaliar_TemperaturaAmena_NaoGeraAlerta()
    {
        var leitura = new LeituraSensor(1, 25, 50, 10);
        Assert.Null(new RegraCalorExtremo().Avaliar(CriarEstacao(), leitura));
    }

    [Theory]
    [InlineData(36, 50, Severidade.Media)]    
    [InlineData(37, 90, Severidade.Alta)]    
    [InlineData(40, 100, Severidade.Critica)] 
    public void Avaliar_CalorIntenso_GeraAlerta(double temp, double umidade, Severidade esperada)
    {
        var leitura = new LeituraSensor(1, temp, umidade, 10);

        var alerta = new RegraCalorExtremo().Avaliar(CriarEstacao(), leitura);

        Assert.NotNull(alerta);
        Assert.Equal(TipoAlerta.CalorExtremo, alerta!.Tipo);
        Assert.Equal(esperada, alerta.Severidade);
    }
}
