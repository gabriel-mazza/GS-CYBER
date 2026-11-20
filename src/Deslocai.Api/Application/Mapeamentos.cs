using Deslocai.Api.Domain;

namespace Deslocai.Api.Application;


public static class Mapeamentos
{
    public static EstacaoDto ParaDto(this Estacao e) =>
        new(e.Id, e.Nome, e.Linha, e.Tipo, e.Latitude, e.Longitude, e.CapacidadeMaxima);

    public static EquipamentoDto ParaDto(this Equipamento e) =>
        new(e.Id, e.EstacaoId, e.Identificador, e.Tipo, e.Status, e.AtualizadoEm);

    public static LeituraDto ParaDto(this LeituraSensor l, int capacidadeEstacao) =>
        new(l.Id, l.EstacaoId, l.TemperaturaC, l.UmidadePercentual, l.PessoasNaPlataforma,
            Math.Round(l.PessoasNaPlataforma * 100.0 / capacidadeEstacao, 1), l.RegistradaEm);

    public static AlertaDto ParaDto(this Alerta a) =>
        new(a.Id, a.EstacaoId, a.EquipamentoId, a.Tipo, a.Severidade, a.Mensagem,
            a.CriadoEm, a.ResolvidoEm, a.Ativo);

    public static RelatoDto ParaDto(this Relato r) =>
        new(r.Id, r.EstacaoId, r.Tipo, r.PerfilRelator, r.Descricao, r.CriadoEm);
}
