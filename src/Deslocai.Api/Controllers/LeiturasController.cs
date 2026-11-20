using Deslocai.Api.Application;
using Microsoft.AspNetCore.Mvc;

namespace Deslocai.Api.Controllers;

[ApiController]
[Route("api")]
[Produces("application/json")]
public class LeiturasController : ControllerBase
{
    private readonly ILeituraService _service;

    public LeiturasController(ILeituraService service) => _service = service;

   
    [HttpPost("leituras")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeituraRegistradaDto>> Registrar([FromBody] LeituraEntradaDto dto, CancellationToken ct)
    {
        var resultado = await _service.RegistrarAsync(dto, ct);
        return CreatedAtAction(nameof(Ultimas), new { estacaoId = resultado.Leitura.EstacaoId }, resultado);
    }

    
    [HttpGet("estacoes/{estacaoId:int}/leituras")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<LeituraDto>>> Ultimas(
        int estacaoId, [FromQuery] int quantidade = 10, CancellationToken ct = default) =>
        Ok(await _service.UltimasAsync(estacaoId, quantidade, ct));
}
