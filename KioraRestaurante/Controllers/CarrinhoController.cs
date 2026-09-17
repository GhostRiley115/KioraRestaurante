using System.Security.Claims;
using KioraRestaurante.DTOs.Carrinho;
using KioraRestaurante.DTOs.ItemCarrinho;
using KioraRestaurante.Services;
using KioraRestaurante.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KioraRestaurante.Controllers;

[ApiController]
[Route("api/carrinho")]
[AutoValidateAntiforgeryToken]
public class CarrinhoController : ControllerBase
{
    private readonly ICarrinhoService _service;
    private readonly CarrinhoCookie _cookie;

    public CarrinhoController(ICarrinhoService service, CarrinhoCookie cookie)
    {
        _service = service;
        _cookie = cookie;
    }

    private AcessoCarrinho ObterAcesso()
    {
        //Por enquanto, considero que não temos usuário logado.
        int? usuarioId = null;

        //Se estiver logado
        if (User.Identity?.IsAuthenticated == true)
        {
            //Guarda o ID do usuário.
            var valor = User.FindFirstValue(ClaimTypes.NameIdentifier);

            //Tenta converter para int, se não conseguir -> excessão
            if(!int.TryParse(valor, out var id) || id <= 0)
                throw new UnauthorizedAccessException();

            usuarioId = id;
        }

        //Monta a ficha de quem está acessando.
        return new AcessoCarrinho
        {
            UsuarioId = usuarioId,
            //Usa o método da classe Cookie para ler o cookie do usuário.
            CarrinhoVisitanteId = _cookie.Ler(HttpContext)
        };
    }

    //Cria uma operação padrão para todas as ações que alteram o carrinho.
    //Uma função que recebe AcessoCarrinho e devolve um Task<CarrinhoResponseDTO>.
    private Task<IActionResult> Alterar(Func<AcessoCarrinho, Task<CarrinhoResponseDTO>> operacao)
    {
        return Executar(async () =>
        {
            var acesso = ObterAcesso();

            //Executa a operação que foi passada, remover, adicionar e etc...
            var resposta = await operacao(acesso);

            //Só criamos ou renovamos o cookie anônimo depois de salvar a alteração com sucesso.
            //Se não for usuário logado e a resposta tiver um ID de carrinho, grave esse ID no cookie.
            if (!acesso.UsuarioId.HasValue && resposta.CarrinhoId.HasValue)
            {
                _cookie.Gravar(HttpContext, resposta.CarrinhoId.Value);
            }
            return Ok(resposta);
        });
    }

    //tratador central de erros.
    private async Task<IActionResult> Executar(Func<Task<IActionResult>> operacao)
    {
        try
        {
            //Executa a operação. Se der tudo certo: retorna resposta. Se der erro: vai para o catch correspondente.
            return await operacao();
        }
        catch (UnauthorizedAccessException)
        {
            //HTTP 401
            return Unauthorized();
        }
        catch (ArgumentException ex)
        {
            //HTTP 400
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            //HTTP 404
            return NotFound(new { message = ex.Message });
        }
        //"Outra operação alterou esse carrinho antes de você salvar sua alteração."
        catch (DbUpdateConcurrencyException)
        {
            //HTTP 409
            return Conflict(new
            {
                mensagem = "O carrinho foi alterado por outra operação." +
                           " Atualize os dados antes de tentar novamente."

            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpGet]
    //Monta a ficha de quem está acessando.
    public Task<IActionResult> Buscar()
    {
        return Executar(async () =>
        {
            var resposta = await _service.BuscarCarrinho(ObterAcesso());

            return Ok(resposta);
        });
    }

    [HttpGet("revisao")]
    //Busca um carrinho
    public Task<IActionResult> Revisar()
    {
        return Executar(async () =>
        {
            var resposta = await _service.RevisarCarrinho(ObterAcesso());

            return Ok(resposta);
        });
    }

    [HttpPost("itens")]
    public Task<IActionResult> Adicionar([FromBody] AdicionarProdutoCarrinhoRequestDTO dto)
    {
        return Alterar(acesso => _service.AdicionarProduto(acesso, dto));
    }

    [HttpPut("itens/{produtoId:int}")]
    public Task<IActionResult> Atualizar(int produtoId, [FromBody] ItemCarrinhoUpdateDTO dto)
    {
        return Alterar(acesso => _service.AtualizarQuantidade(acesso, produtoId, dto));
    }

    [HttpDelete("itens/{produtoId:int}")]
    public Task<IActionResult> Remover(int produtoId)
    {
        return Alterar(acesso => _service.RemoverProduto(acesso, produtoId));
    }

    [HttpDelete("itens")]
    public Task<IActionResult> Esvaziar()
    {
        return Alterar(acesso => _service.EsvaziarCarrinho(acesso));
    }

    [Authorize]
    [HttpPost("mesclagem")]
    public Task<IActionResult> Mesclar([FromBody] MesclarCarrinhoRequestDTO dto)
    {
        return Executar(async () =>
        {
            var resultado = await _service.MesclarCarrinhos(ObterAcesso(), dto);

            if (!resultado.Concluida)
                return Conflict(resultado);

            //Se deu tudo certo na mesclagem, remove o cookie do carrinho temporário.
            _cookie.Remover(HttpContext);

            return Ok(resultado);
        });
    }
}