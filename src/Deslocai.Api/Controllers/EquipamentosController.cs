using Deslocai.Api.Application;
using Deslocai.Api.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Deslocai.Api.Controllers;

[ApiController]
[Route("api/equipamentos")]
[Produces("application/json")]
public class EquipamentosController : ControllerBase
{
    private readonly IEquipamentoService _service;

    public EquipamentosController(IEquipamentoService service) => _service = service;

    
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EquipamentoDto>>> Listar(
        [FromQuery] int? estacaoId, [FromQuery] StatusEquipamento? status, CancellationToken ct) =>
        Ok(await _service.ListarAsync(estacaoId, status, ct));

    
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EquipamentoDto>> Criar([FromBody] EquipamentoEntradaDto dto, CancellationToken ct)
    {
        var criado = await _service.CriarAsync(dto, ct);
        return CreatedAtAction(nameof(Listar), new { estacaoId = criado.EstacaoId }, criado);
    }

    
    [HttpPatch("{id:int}/status")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EquipamentoDto>> AlterarStatus(int id, [FromBody] AlterarStatusDto dto, CancellationToken ct) =>
        Ok(await _service.AlterarStatusAsync(id, dto.Status, ct));
}
