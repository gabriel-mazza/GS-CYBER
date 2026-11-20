namespace Deslocai.Api.Domain;


public class Relato : EntidadeBase
{
    public int EstacaoId { get; private set; }
    public Estacao? Estacao { get; private set; }
    public TipoRelato Tipo { get; private set; }
    public PerfilCidadao PerfilRelator { get; private set; }
    public string Descricao { get; private set; } = string.Empty;

    protected Relato() { }

    public Relato(int estacaoId, TipoRelato tipo, PerfilCidadao perfil, string descricao)
    {
        if (string.IsNullOrWhiteSpace(descricao) || descricao.Trim().Length < 5)
            throw new RegraDeNegocioException("A descrição do relato precisa ter ao menos 5 caracteres.");
        if (descricao.Length > 280)
            throw new RegraDeNegocioException("A descrição do relato pode ter no máximo 280 caracteres.");

        EstacaoId = estacaoId;
        Tipo = tipo;
        PerfilRelator = perfil;
        Descricao = descricao.Trim();
    }
}
