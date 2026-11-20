using Deslocai.Security.Taint;

namespace Deslocai.Security.Demo;

public record RelatoPcd(string Nome, string Cpf, string Email, string Telefone, string Estacao, string Descricao);


public static class DemoEndpoints
{
    public static void Map(WebApplication app)
    {
        
        app.MapPost("/api/v1/relatos", (RelatoPcd dto, SinkGuard guard) =>
        {
            guard.Log($"Novo relato recebido: {dto}");
            guard.Sql($"INSERT INTO Relatos(Cpf, Estacao, Descricao) VALUES ('{dto.Cpf}', '{dto.Estacao}', '{dto.Descricao}')");
            guard.HttpOut("https://webhook.parceiro.exemplo/notificar", $"{{\"email\":\"{dto.Email}\",\"cpf\":\"{dto.Cpf}\"}}");
            
            return Results.Accepted(value: new { status = "recebido", versao = "v1-vulneravel", recebido = dto });
        });

        
        app.MapPost("/api/v2/relatos", (RelatoPcd dto, SinkGuard guard, TaintContext ctx) =>
        {
            guard.Log($"Novo relato de {ctx.Mask(dto.Cpf)} ({ctx.Mask(dto.Email)}) na estação {dto.Estacao}");
            guard.Sql("INSERT INTO Relatos(Cpf, Estacao, Descricao) VALUES (@cpf, @estacao, @descricao)", new { dto.Cpf, dto.Estacao, dto.Descricao });
            var id = ctx.Pseudonymize(dto.Cpf);
            ctx.Pseudonymize(dto.Email);
            guard.HttpOut("https://webhook.parceiro.exemplo/notificar", $"{{\"id\":\"{id}\",\"estacao\":\"{dto.Estacao}\"}}");
            return Results.Accepted(value: new { status = "recebido", versao = "v2-corrigido" });
        });
    }
}
