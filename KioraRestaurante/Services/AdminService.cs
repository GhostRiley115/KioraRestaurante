using System.ComponentModel.DataAnnotations;
using KioraRestaurante.Data;
using KioraRestaurante.DTOs.Admin;
using KioraRestaurante.Models.Enums;
using KioraRestaurante.Services.Interfaces;
using KioraRestaurante.ViewModels.Admin;
using Microsoft.EntityFrameworkCore;
namespace KioraRestaurante.Services;

public class AdminService : IAdminService
{
    private readonly AppDbContext _context;
    public AdminService(AppDbContext context) => _context = context;

    // Também verifica o banco: um cookie antigo não concede acesso a um administrador desativado.
    private async Task ExigirAdministrador(int id)
    {
        if (!await _context.Usuarios.AnyAsync(u => u.Id == id && u.Ativo && u.Tipo == TipoUsuario.Administrador))
            throw new UnauthorizedAccessException("Acesso exclusivo para administradores ativos.");
    }

    public async Task<PainelAdminDTO> Painel(int administradorId)
    {
        await ExigirAdministrador(administradorId);
        return new PainelAdminDTO {
            UsuariosAtivos = await _context.Usuarios.CountAsync(u => u.Ativo),
            UsuariosInativos = await _context.Usuarios.CountAsync(u => !u.Ativo),
            TotalPedidos = await _context.Pedidos.CountAsync(),
            PedidosEmAndamento = await _context.Pedidos.CountAsync(p => p.StatusPedido == StatusPedido.Recebido
                || p.StatusPedido == StatusPedido.EmPreparo || p.StatusPedido == StatusPedido.SaiuParaEntrega)
        };
    }

    public async Task<ResultadoPaginadoDTO<UsuarioAdminDTO>> Usuarios(int administradorId, FiltroUsuariosAdmin filtro)
    {
        await ExigirAdministrador(administradorId);
        Validator.ValidateObject(filtro, new ValidationContext(filtro), true);
        var consulta = _context.Usuarios.AsNoTracking();
        var busca = filtro.Busca?.Trim();
        if (!string.IsNullOrEmpty(busca)) consulta = consulta.Where(u => u.Nome.Contains(busca) || u.Email.Contains(busca));
        if (filtro.Ativo.HasValue) consulta = consulta.Where(u => u.Ativo == filtro.Ativo.Value);
        if (filtro.Tipo.HasValue) consulta = consulta.Where(u => u.Tipo == filtro.Tipo.Value);

        // A projeção seleciona somente os campos necessários, sem senha ou tokens.
        return await Paginar(consulta.OrderBy(u => u.Nome).ThenBy(u => u.Id).Select(u => new UsuarioAdminDTO {
            Id = u.Id, Nome = u.Nome, Email = u.Email, Tipo = u.Tipo, Ativo = u.Ativo,
            QuantidadePedidos = u.Pedidos.Count
        }), filtro.Pagina);
    }

    public async Task<ResultadoPaginadoDTO<PedidoAdminDTO>> Pedidos(int administradorId, FiltroPedidosAdmin filtro)
    {
        await ExigirAdministrador(administradorId);
        Validator.ValidateObject(filtro, new ValidationContext(filtro), true);
        var consulta = _context.Pedidos.AsNoTracking();
        var busca = filtro.Busca?.Trim();
        if (!string.IsNullOrEmpty(busca)) consulta = consulta.Where(p => p.Usuario.Nome.Contains(busca) || p.Usuario.Email.Contains(busca));
        if (filtro.PedidoId.HasValue) consulta = consulta.Where(p => p.Id == filtro.PedidoId.Value);
        if (filtro.UsuarioId.HasValue) consulta = consulta.Where(p => p.UsuarioId == filtro.UsuarioId.Value);
        if (filtro.Status.HasValue) consulta = consulta.Where(p => p.StatusPedido == filtro.Status.Value);
        if (filtro.Pagamento.HasValue) consulta = consulta.Where(p => p.StatusPagamento == filtro.Pagamento.Value);
        if (filtro.Inicio.HasValue) {
            var inicio = HorarioRestaurante.InicioDiaUtc(filtro.Inicio.Value);
            consulta = consulta.Where(p => p.DataPedido >= inicio);
        }
        if (filtro.Fim.HasValue) {
            // O limite exclusivo do dia seguinte inclui todo o último dia escolhido.
            var fim = HorarioRestaurante.InicioDiaUtc(filtro.Fim.Value.AddDays(1));
            consulta = consulta.Where(p => p.DataPedido < fim);
        }
        return await Paginar(consulta.OrderByDescending(p => p.DataPedido).ThenByDescending(p => p.Id).Select(p => new PedidoAdminDTO {
            Id = p.Id, UsuarioId = p.UsuarioId, NomeCliente = p.Usuario.Nome, EmailCliente = p.Usuario.Email,
            DataPedido = p.DataPedido, ValorTotal = p.ValorTotal, StatusPedido = p.StatusPedido,
            StatusPagamento = p.StatusPagamento, FormaPagamento = p.FormaPagamento
        }), filtro.Pagina);
    }

    private static async Task<ResultadoPaginadoDTO<T>> Paginar<T>(IQueryable<T> consulta, int pagina)
    {
        var resultado = new ResultadoPaginadoDTO<T> { Total = await consulta.CountAsync() };
        resultado.Pagina = Math.Clamp(pagina, 1, resultado.TotalPaginas);
        resultado.Itens = await consulta.Skip((resultado.Pagina - 1) * resultado.TamanhoPagina)
            .Take(resultado.TamanhoPagina).ToListAsync();
        return resultado;
    }

    public async Task<PedidoDetalhesAdminDTO?> DetalhesPedido(int administradorId, int pedidoId)
    {
        await ExigirAdministrador(administradorId);
        return await _context.Pedidos.AsNoTracking().Where(p => p.Id == pedidoId).Select(p => new PedidoDetalhesAdminDTO {
            Id = p.Id, UsuarioId = p.UsuarioId, NomeCliente = p.Usuario.Nome, EmailCliente = p.Usuario.Email,
            ClienteAtivo = p.Usuario.Ativo, DataPedido = p.DataPedido, ValorTotal = p.ValorTotal,
            StatusPedido = p.StatusPedido, StatusPagamento = p.StatusPagamento, FormaPagamento = p.FormaPagamento,
            Itens = p.ItensPedido.OrderBy(i => i.Id).Select(i => new ItemPedidoAdminDTO {
                Nome = i.NomeProduto, Quantidade = i.Quantidade, PrecoUnitario = i.PrecoUnitario, Observacao = i.Observacao
            }).ToList(),
            Endereco = p.EnderecoEntrega == null ? null : new EnderecoPedidoAdminDTO {
                Logradouro = p.EnderecoEntrega.Logradouro, Numero = p.EnderecoEntrega.Numero,
                Bairro = p.EnderecoEntrega.Bairro, Cidade = p.EnderecoEntrega.Cidade, Uf = p.EnderecoEntrega.Uf,
                Cep = p.EnderecoEntrega.Cep, Complemento = p.EnderecoEntrega.Complemento, Referencia = p.EnderecoEntrega.Referencia
            }
        }).SingleOrDefaultAsync();
    }

    public async Task AlterarSituacaoUsuario(int administradorId, int usuarioId, bool ativo)
    {
        await ExigirAdministrador(administradorId);
        if (administradorId == usuarioId) throw new ValidationException("Você não pode alterar a situação da própria conta.");
        var usuario = await _context.Usuarios.AsNoTracking().Where(u => u.Id == usuarioId)
            .Select(u => new { u.Tipo }).SingleOrDefaultAsync();
        if (usuario == null) throw new KeyNotFoundException("Usuário não encontrado.");
        if (usuario.Tipo != TipoUsuario.Cliente) throw new ValidationException("Contas administrativas estão protegidas nesta tela.");

        // Altera somente a situação; preserva os pedidos e impede modificar um administrador.
        // Limpa tokens antigos de recuperação, que não devem sobreviver à mudança de situação.
        var alterados = await _context.Usuarios.Where(u => u.Id == usuarioId && u.Tipo == TipoUsuario.Cliente)
            .ExecuteUpdateAsync(set => set.SetProperty(u => u.Ativo, ativo)
                .SetProperty(u => u.TokenRecuperacaoSenha, (string?)null)
                .SetProperty(u => u.ExpiracaoTokenRecuperacaoSenha, (DateTime?)null));
        if (alterados == 0) throw new ValidationException("A conta mudou. Atualize a listagem antes de tentar novamente.");
    }
}
