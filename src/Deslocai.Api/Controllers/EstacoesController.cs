using Deslocai.Api.Application;
using Deslocai.Api.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Deslocai.Api.Controllers;

[ApiController]
[Route("api/estacoes")]
[Produces("application/json")]
public class EstacoesController : ControllerBase
{
    private readonly IEstacaoService _service;

    public EstacoesController(IEstacaoService service) => _service = service;

    
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EstacaoDto>>> Listar(
        [FromQuery] TipoTransporte? tipo, [FromQuery] string? linha, CancellationToken ct) =>
        Ok(await _service.ListarAsync(tipo, linha, ct));

    
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EstacaoDetalheDto>> Obter(int id, CancellationToken ct) =>
        Ok(await _service.ObterAsync(id, ct));

   
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EstacaoDto>> Criar([FromBody] EstacaoEntradaDto dto, CancellationToken ct)
    {
        var criada = await _service.CriarAsync(dto, ct);
        return CreatedAtAction(nameof(Obter), new { id = criada.Id }, criada);
    }

    
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EstacaoDto>> Atualizar(int id, [FromBody] EstacaoEntradaDto dto, CancellationToken ct) =>
        Ok(await _service.AtualizarAsync(id, dto, ct));

    
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remover(int id, CancellationToken ct)
    {
        await _service.RemoverAsync(id, ct);
        return NoContent();
    }
}
