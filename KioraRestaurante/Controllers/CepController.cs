using System.ComponentModel.DataAnnotations;
using KioraRestaurante.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KioraRestaurante.Controllers;

[Authorize]
[ApiController]
[Route("api/cep")]
public class CepController : ControllerBase
{
    private readonly CepService _cepService;

    public CepController(CepService cepService)
    {
        _cepService = cepService;
    }

    [HttpGet("{cep}")]
    public async Task<IActionResult> Consultar(string cep)
    {
        try
        {
            var endereco = await _cepService.Consultar(cep);

            _cepService.ExigirAreaAtendida(endereco.Uf, endereco.Ibge);

            return Ok(endereco);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { mensagem = ex.Message });
        }
    }
}