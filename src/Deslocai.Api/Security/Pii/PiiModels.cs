namespace Deslocai.Security.Pii;

public enum PiiType { Cpf, Cnpj, Cartao, Email, Telefone, Rg, Cep, Placa, Nascimento, Geolocalizacao, Bilhete, SaudePcd, Senha, Laudo }


public enum LgpdLevel { Pessoal, Sensivel }

public record PiiMatch(PiiType Type, int Start, int Length, string Value, double Confidence, bool Validated, LgpdLevel Level, int Weight)
{
    public int End => Start + Length;
}

public record PiiScanResult(string Original, string Masked, IReadOnlyList<PiiMatch> Matches)
{
    public bool HasPii => Matches.Count > 0;
}

public static class PiiCatalog
{
    
    public static int Weight(PiiType t) => t switch
    {
        PiiType.SaudePcd => 5, PiiType.Cartao => 5, PiiType.Senha => 5, PiiType.Laudo => 5,
        PiiType.Cpf => 4, PiiType.Rg => 4, PiiType.Geolocalizacao => 4,
        PiiType.Nascimento => 3, PiiType.Bilhete => 3,
        PiiType.Email => 2, PiiType.Telefone => 2, PiiType.Placa => 2, PiiType.Cnpj => 2,
        _ => 1
    };

    public static LgpdLevel Level(PiiType t) => t is PiiType.SaudePcd or PiiType.Laudo ? LgpdLevel.Sensivel : LgpdLevel.Pessoal;
}
