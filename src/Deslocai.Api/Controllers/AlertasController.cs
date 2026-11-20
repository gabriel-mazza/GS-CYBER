using Deslocai.Api.Application;
using Deslocai.Api.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Deslocai.Api.Controllers;

[ApiController]
[Route("api/alertas")]
[Produces("application/json")]
public class AlertasController : ControllerBase
{
    private readonly IAlertaService _service;

    public AlertasController(IAlertaService service) => _service = service;

    
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AlertaDto>>> Listar(
        [FromQuery] int? estacaoId, [FromQuery] TipoAlerta? tipo,
        [FromQuery] bool incluirResolvidos = false, CancellationToken ct = default) =>
        Ok(await _service.ListarAsync(estacaoId, tipo, incluirResolvidos, ct));

    
    [HttpPatch("{id:int}/resolver")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AlertaDto>> Resolver(int id, CancellationToken ct) =>
        Ok(await _service.ResolverAsync(id, ct));
}
