using Deslocai.Api.Application;
using Deslocai.Api.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Deslocai.Api.Controllers;

[ApiController]
[Route("api/relatos")]
[Produces("application/json")]
public class RelatosController : ControllerBase
{
    private readonly IRelatoService _service;

    public RelatosController(IRelatoService service) => _service = service;

    
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RelatoDto>>> Listar(
        [FromQuery] int? estacaoId, [FromQuery] TipoRelato? tipo, CancellationToken ct) =>
        Ok(await _service.ListarAsync(estacaoId, tipo, ct));

    
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RelatoDto>> Criar([FromBody] RelatoEntradaDto dto, CancellationToken ct)
    {
        var criado = await _service.CriarAsync(dto, ct);
        return CreatedAtAction(nameof(Listar), new { estacaoId = criado.EstacaoId }, criado);
    }
}
