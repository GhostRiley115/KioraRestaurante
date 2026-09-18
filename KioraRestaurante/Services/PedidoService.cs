using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;

using KioraRestaurante.Data;
using KioraRestaurante.DTOs.Pedido;
using KioraRestaurante.Models;
using KioraRestaurante.Models.Enums;
using KioraRestaurante.Services.Interfaces;
using KioraRestaurante.Services.Exceptions;

namespace KioraRestaurante.Services;

public class PedidoService : IPedidoService
{
    private readonly AppDbContext _context;
    private readonly ICarrinhoService _carrinhoService;
    // Proteger a informação da revisão
    private readonly IDataProtector _protetor;

    public PedidoService(AppDbContext context, ICarrinhoService carrinhoService, IDataProtectionProvider provider)
    {
        _context = context;
        _carrinhoService = carrinhoService;
        _protetor = provider.CreateProtector("Kiora.Checkout.v1");
    }

    // Pede outra confirmação caso o valor dos itens mude
    private sealed class RevisaoProtegida
    {
        public int UsuarioId { get; set; }
        public int CarrinhoId { get; set; }
        public Guid VersaoCarrinho { get; set; }
        public Guid ChaveConfirmacao { get; set; }
        public DateTime ExpiraEmUtc { get; set; }
        public string Itens { get; set; } =  string.Empty;
    }

    private static string RepresentarItens(Carrinho carrinho)
    {
        var itens = carrinho.ItensCarrinho
            .OrderBy(i => i.ProdutoId)
            .Select(i => new
            {
                i.ProdutoId,
                i.Quantidade,
                Nome = i.Produto.Nome,
                Preco = i.Produto.Preco,
                i.Produto.Ativo,
                i.Produto.Disponivel
            });

        return JsonSerializer.Serialize(itens);
    }

    private RevisaoProtegida LerRevisao(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            throw new RegraPedidoException("Atualize a revisão antes de confirmar.");
        }

        try
        {
            var json = _protetor.Unprotect(token);

            return JsonSerializer.Deserialize<RevisaoProtegida>(json) ?? throw new RegraPedidoException
                ("Não foi possível recuperar a revisão.");
        }
        catch (CryptographicException)
        {
            throw new RegraPedidoException("A revisão não é mais válida. Confira os dados novamente.");
        }
        catch (JsonException)
        {
            throw new RegraPedidoException("A revisão não é mais válida. Confira os dados novamente.");
        }
    }

    private async Task<int> ExigirUsuario(AcessoCarrinho acesso)
    {
        if (!acesso.UsuarioId.HasValue)
        {
            throw new RegraPedidoException("Entre na sua conta para confirmar o pedido.");
        }

        var usuarioId = acesso.UsuarioId.Value;
        var ativo = await _context.Usuarios
            .AnyAsync(u => u.Id == usuarioId && u.Ativo);

        if (!ativo)
        {
            throw new RegraPedidoException("Sua conta não está habilitada para realizar pedidos.");
        }
        return usuarioId;
    }

    private Task<Carrinho?> BuscarCarrinho(int usuarioId)
    {
        return _context.Carrinhos
            .Include(c => c.ItensCarrinho)
            .ThenInclude(i => i.Produto)
            .SingleOrDefaultAsync(c => c.UsuarioId == usuarioId);
    }

    private Task<int?> BuscarConfirmacao(int usuarioId, Guid chave)
    {
        // AsNoTracking consultas em que não vamos alterar os objetos retornados.
        return _context.Pedidos.AsNoTracking()
            .Where(p => p.UsuarioId == usuarioId && p.ChaveConfirmacao == chave)
            .Select(p => (int?)p.Id)
            .SingleOrDefaultAsync();
    }

    public async Task<CheckoutPedidoResponseDTO> PrepararCheckout(AcessoCarrinho acesso)
    {
        var usuarioId = await ExigirUsuario(acesso);
        var resumo = await _carrinhoService.RevisarCarrinho(acesso);

        var resposta = new CheckoutPedidoResponseDTO()
        {
            Carrinho = resumo
        };

        /*
         * Carrinho vazio, produto indisponível ou mesclagem pendente:
         * mostramos os avisos, mas não liberamos a confirmação.
         */
        if (!resumo.PodeProsseguir)
            return resposta;

        var carrinho = await BuscarCarrinho(usuarioId);

        if (carrinho == null)
        {
            throw new RegraPedidoException("Carrinho não encontrado.");
        }

        var revisao = new RevisaoProtegida
        {
            UsuarioId = usuarioId,
            CarrinhoId = carrinho.Id,
            VersaoCarrinho = carrinho.Versao,
            ChaveConfirmacao = Guid.NewGuid(),
            ExpiraEmUtc = DateTime.UtcNow.AddMinutes(30),
            Itens = RepresentarItens(carrinho)
        };
        resposta.TokenRevisao = _protetor.Protect(JsonSerializer.Serialize(revisao));

        return resposta;
    }

    public async Task<int> ConfirmarPedido(AcessoCarrinho acesso, CriarPedidoRequestDTO dto)
    {
        var usuarioId = await ExigirUsuario(acesso);

        var revisao = LerRevisao(dto.TokenRevisao);

        if (revisao.UsuarioId != usuarioId)
        {
            throw new RegraPedidoException("Esta revisão pertence a outra sessão de usuário.");
        }

        //Se essa confirmação já criou um pedido, devolvemos o mesmo ID.
        var pedidoExistente = await BuscarConfirmacao(usuarioId, revisao.ChaveConfirmacao);

        if (pedidoExistente.HasValue)
            return pedidoExistente.Value;

        if (revisao.ExpiraEmUtc <= DateTime.UtcNow)
        {
            throw new RegraPedidoException("A revisão expirou. Confira os valores e confirme novamente.");
        }

        /*
         * Valida os atributos do DTO também dentro do serviço.
         * Assim, a regra não depende apenas do controller.
         */
        Validator.ValidateObject(dto, new ValidationContext(dto), true);

        var resumoAtual = await _carrinhoService.RevisarCarrinho(acesso);
        if (!resumoAtual.PodeProsseguir)
        {
            throw new RegraPedidoException(string.Join(" ", resumoAtual.Avisos));
        }
        var carrinho = await BuscarCarrinho(usuarioId);

        if (carrinho == null || carrinho.ItensCarrinho.Count == 0)
        {
            throw new RegraPedidoException("Seu carrinho está vazio.");
        }

        ValidarRevisaoAtual(carrinho, revisao);
        ValidarItensParaPedido(carrinho);

        var pedido = new Pedido
        {
            UsuarioId = usuarioId,
            ChaveConfirmacao = revisao.ChaveConfirmacao,
            DataPedido = DateTime.UtcNow,
            FormaPagamento = dto.FormaPagamento!.Value,
            StatusPedido = StatusPedido.Recebido,
            StatusPagamento = StatusPagamento.Pendente,

            EnderecoEntrega = CriarEndereco(dto)
        };

        CopiarItensParaPedido(carrinho, pedido);

        return await SalvarPedidoELimparCarrinho(pedido, carrinho);
    }


    // Confere se a revisão ainda representa o mesmo carrinho, versão e produtos.
    private static void ValidarRevisaoAtual(Carrinho carrinho, RevisaoProtegida revisao)
    {
        if (carrinho.Id != revisao.CarrinhoId || carrinho.Versao != revisao.VersaoCarrinho ||
            RepresentarItens(carrinho) != revisao.Itens)
        {
            throw new RegraPedidoException(
                "O carrinho ou os produtos mudaram. Confira a revisão atualizada antes de confirmar.");
        }

    }

    // Valida cada item antes de transformá-lo em parte de um pedido.
    private static void ValidarItensParaPedido(Carrinho carrinho)
    {
        foreach (var item in carrinho.ItensCarrinho)
        {
            if (item.Quantidade < 1 || item.Quantidade > 30)
            {
                throw new RegraPedidoException("Existe uma quantidade inválida no carrinho.");
            }

            if (!item.Produto.Ativo || !item.Produto.Disponivel)
            {
                throw new RegraPedidoException($"{item.Produto.Nome} não está disponível.");
            }

            if (item.Produto.Preco <= 0)
            {
                throw new RegraPedidoException($"{item.Produto.Nome} está com preço inválido.");
            }
        }

    }

    // Monta o endereço do pedido com os dados já validados do formulário.
    private static EnderecoEntrega CriarEndereco(CriarPedidoRequestDTO dto)
    {
        return new EnderecoEntrega
        {
            Cep = dto.Cep.Trim(),
            Logradouro = dto.Logradouro.Trim(),
            Numero = dto.Numero.Trim(),
            Bairro = dto.Bairro.Trim(),
            Cidade = dto.Cidade.Trim(),
            Uf = dto.Uf.Trim(),
            Complemento = dto.Complemento?.Trim(),
            Referencia = dto.Referencia?.Trim(),
        };
    }

    // Copia os dados da compra para preservar o histórico e calcula o total.
    private static void CopiarItensParaPedido(Carrinho carrinho, Pedido pedido)
    {
        foreach (var item in carrinho.ItensCarrinho)
        {
            pedido.ItensPedido.Add(new ItemPedido
            {
                ProdutoId = item.ProdutoId,
                NomeProduto = item.Produto.Nome,
                PrecoUnitario = item.Produto.Preco,
                Quantidade = item.Quantidade
            });
        }

        pedido.ValorTotal = pedido.ItensPedido.Sum(i => i.Subtotal);

        if (pedido.ValorTotal > 99999999.99m)
        {
            throw new RegraPedidoException(
                "O valor do pedido ultrapassa o limite permitido.");
        }

    }

    // Salva a compra e a limpeza do carrinho na mesma operação.
    private async Task<int> SalvarPedidoELimparCarrinho(Pedido pedido, Carrinho carrinho)
    {
        _context.Pedidos.Add(pedido);

        _context.ItensCarrinho.RemoveRange(carrinho.ItensCarrinho);
        carrinho.ItensCarrinho.Clear();

        // Participa da proteção contra alterações simultâneas.
        carrinho.Versao = Guid.NewGuid();

        try
        {
             // Pedido, itens, endereço e limpeza do carrinho são gravados juntos.
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            /*
             * Outra requisição pode ter confirmado a mesma compra.
             * Descarta o estado local que falhou e consulta o banco.
             */
            _context.ChangeTracker.Clear();

            var repetido = await BuscarConfirmacao(pedido.UsuarioId, pedido.ChaveConfirmacao!.Value);

            if(repetido.HasValue)
                return repetido.Value;

            // Não era uma repetição concluída: mantém o erro original.
            throw;
        }
        return pedido.Id;
    }

    public Task<Pedido?> BuscarPedido(int usuarioId, int pedidoId)
    {
        return _context.Pedidos
            .AsNoTracking()
            .Include(p => p.EnderecoEntrega)
            .Include(p => p.ItensPedido)
            .SingleOrDefaultAsync(p =>
                p.Id == pedidoId
                && p.UsuarioId == usuarioId
                && p.Usuario.Ativo);
    }

    public Task<List<Pedido>> ListarPedidos(int usuarioId)
    {
        return _context.Pedidos
            .AsNoTracking()
            .Where(p =>
                p.UsuarioId == usuarioId
                && p.Usuario.Ativo)
            .OrderByDescending(p => p.DataPedido)
            .Take(50)
            .ToListAsync();
    }
}