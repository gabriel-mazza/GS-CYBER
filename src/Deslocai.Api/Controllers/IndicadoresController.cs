using Deslocai.Api.Application;
using Microsoft.AspNetCore.Mvc;

namespace Deslocai.Api.Controllers;

[ApiController]
[Route("api/indicadores")]
[Produces("application/json")]
public class IndicadoresController : ControllerBase
{
    private readonly IIndicadorService _service;

    public IndicadoresController(IIndicadorService service) => _service = service;

    
    [HttpGet("impacto")]
    public async Task<ActionResult<ImpactoDto>> Impacto(CancellationToken ct) =>
        Ok(await _service.ObterImpactoAsync(ct));
}
