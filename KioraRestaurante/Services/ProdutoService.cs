using System.ComponentModel.DataAnnotations;
using KioraRestaurante.Data;
using KioraRestaurante.DTOs.Produto;
using KioraRestaurante.Models;
using KioraRestaurante.Models.Enums;
using KioraRestaurante.Services.Exceptions;
using KioraRestaurante.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using KioraRestaurante.DTOs.Admin;

namespace KioraRestaurante.Services
{
    public class ProdutoService : IProdutoService
    {
        private readonly AppDbContext _context;
        private readonly IImagemProdutoService _imagemService;
        private readonly ILogger<ProdutoService> _logger;

        public ProdutoService(AppDbContext context, IImagemProdutoService imagemService, ILogger<ProdutoService> logger)
        {
            _context = context;
            _imagemService = imagemService;
            _logger = logger;
        }

        // Somente usuário ADM pode criar produtos no cardápio.
        private async Task ExigirAdministrador(int usuarioId)
        {
            var autorizado = await _context.Usuarios.AnyAsync(u =>
                u.Id == usuarioId
                && u.Ativo
                && u.Tipo == TipoUsuario.Administrador);

            if (!autorizado)
            {
                throw new UnauthorizedAccessException();
            }
        }

        // Valida a solicitação de criação de um produto.
        private static void ValidarDados(CriarProdutoRequestDTO dto)
        {
            Validator.ValidateObject(dto, new ValidationContext(dto), validateAllProperties: true);

            // Evita arredondar silenciosamente um preço como 19,999.
            if (decimal.Round(dto.Preco, 2) != dto.Preco)
            {
                throw new RegraProdutoException("O preço deve ter no máximo duas casas decimais.");
            }
        }

        // Só é possível atribuir produto a uma categoria se ela estiver ativa.
        private async Task ExigirCategoriaAtiva(int categoriaId)
        {
            var existe = await _context.Categorias.AnyAsync(c =>
                c.Id == categoriaId && c.Ativa);

            if (!existe)
            {
                throw new RegraProdutoException("A categoria selecionada não existe ou está inativa.");
            }
        }

        // Monta a consulta administrativa sem executá-la ainda.
        // Diferentemente do cardápio, também inclui produtos inativos.
        private IQueryable<ProdutoAdminResponseDTO> ConsultaAdmin()
        {
            return _context.Produtos.AsNoTracking()
                .Select(p => new ProdutoAdminResponseDTO
                {
                    Id = p.Id,
                    Nome = p.Nome,
                    Descricao = p.Descricao,
                    Preco = p.Preco,
                    ImagemUrl = p.Imagem,
                    CategoriaId = p.CategoriaId,
                    NomeCategoria = p.Categoria.Nome,
                    CategoriaAtiva = p.Categoria.Ativa,
                    Ativo = p.Ativo,
                    Disponivel = p.Disponivel
                });
        }

        // Retorna a entidade acompanhada pelo EF para permitir alterações.
        private async Task<Produto> ExigirProduto(int produtoId)
        {
            var produto = await _context.Produtos
                .SingleOrDefaultAsync(p => p.Id == produtoId);

            if (produto == null)
            {
                throw new KeyNotFoundException("Produto não encontrado.");
            }

            return produto;
        }

        // Sobrecarga: mesmo nome do método de criação, mas recebe o DTO de edição.
        private static void ValidarDados(EditarProdutoRequestDTO dto)
        {
            Validator.ValidateObject(dto, new ValidationContext(dto), validateAllProperties: true);

            if (decimal.Round(dto.Preco, 2) != dto.Preco)
            {
                throw new RegraProdutoException(
                    "O preço deve ter no máximo duas casas decimais.");
            }
        }

        public async Task<ResultadoPaginadoDTO<ProdutoAdminResponseDTO>> ListarAdmin(
            int administradorId, FiltroProdutosAdminDTO filtro)
        {
            await ExigirAdministrador(administradorId);

            Validator.ValidateObject(filtro, new ValidationContext(filtro), validateAllProperties: true);

            var consulta = ConsultaAdmin();

            var busca = filtro.Busca?.Trim();

            if (!string.IsNullOrWhiteSpace(busca))
            {
                consulta = consulta.Where(p => p.Nome.Contains(busca));
            }

            if (filtro.Ativo.HasValue)
            {
                consulta = consulta.Where(p => p.Ativo == filtro.Ativo.Value);
            }

            if (filtro.Disponivel.HasValue)
            {
                consulta = consulta.Where(
                    p => p.Disponivel == filtro.Disponivel.Value);
            }

            var resultado = new ResultadoPaginadoDTO<ProdutoAdminResponseDTO>
            {
                Total = await consulta.CountAsync()
            };

            // Se a página solicitada deixou de existir, usa a última disponível.
            resultado.Pagina = Math.Clamp(filtro.Pagina, 1, resultado.TotalPaginas);

            resultado.Itens = await consulta
                .OrderBy(p => p.Nome)
                .ThenBy(p => p.Id)
                .Skip((resultado.Pagina - 1) * resultado.TamanhoPagina)
                .Take(resultado.TamanhoPagina)
                .ToListAsync();

            return resultado;
        }

        public async Task<ProdutoAdminResponseDTO?> BuscarAdmin(int administradorId, int produtoId)
        {
            await ExigirAdministrador(administradorId);

            return await ConsultaAdmin()
                .SingleOrDefaultAsync(p => p.Id == produtoId);
        }

        public async Task Editar(int administradorId, int produtoId, EditarProdutoRequestDTO dto, IFormFile? foto)
        {
            await ExigirAdministrador(administradorId);

            ValidarDados(dto);

            var produto = await ExigirProduto(produtoId);

            await ExigirCategoriaAtiva(dto.CategoriaId);

            // Sem arquivo novo, preservamos a imagem que já está no produto.
            ImagemProdutoResultado? novaImagem = null;

            if (foto != null)
            {
                novaImagem = await _imagemService.Enviar(foto);
            }

            produto.Nome = dto.Nome.Trim();
            produto.Descricao = dto.Descricao.Trim();
            produto.Preco = dto.Preco;
            produto.CategoriaId = dto.CategoriaId;

            if (novaImagem != null)
            {
                produto.Imagem = novaImagem.Url;
                produto.ImagemPublicId = novaImagem.PublicId;
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                if (novaImagem != null)
                {
                    // O upload e o banco são operações em sistemas diferentes.
                    _logger.LogError(
                        "Falha ao salvar edição do produto {ProdutoId}. " +
                        "Confira a imagem enviada {PublicId} antes de repetir.",
                        produtoId,
                        novaImagem.PublicId);
                }

                throw;
            }
        }

        //Método para criação de produtos. Primeiro valida e depois transforma em um DTO.
        public async Task<int> Criar(int administradorId, CriarProdutoRequestDTO dto, IFormFile foto)
        {
            // As verificações acontecem antes de enviar a foto.
            await ExigirAdministrador(administradorId);
            ValidarDados(dto);
            await ExigirCategoriaAtiva(dto.CategoriaId);

            var imagem = await _imagemService.Enviar(foto);

            var produto = new Produto
            {
                Nome = dto.Nome.Trim(),
                Descricao = dto.Descricao.Trim(),
                Preco = dto.Preco,
                CategoriaId = dto.CategoriaId,
                Disponivel = dto.Disponivel,
                Ativo = true,
                Imagem = imagem.Url,
                ImagemPublicId = imagem.PublicId
            };

            _context.Produtos.Add(produto);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // A imagem pode ter sido enviada mesmo que a gravação falhe.
                _logger.LogError(
                    "Falha ao salvar produto associado à imagem {PublicId}. " +
                    "Confira o banco antes de remover a imagem ou repetir o cadastro.",
                    imagem.PublicId);

                throw;
            }

            return produto.Id;
        }

        public async Task AlterarAtivo(int administradorId, int produtoId, bool ativo)
        {
            await ExigirAdministrador(administradorId);

            var produto = await ExigirProduto(produtoId);

            produto.Ativo = ativo;

            // Desativar também interrompe a venda.
            // Reativar não libera a venda automaticamente.
            if (!ativo)
            {
                produto.Disponivel = false;
            }

            await _context.SaveChangesAsync();
        }

        public async Task AlterarDisponibilidade(int administradorId, int produtoId, bool disponivel)
        {
            await ExigirAdministrador(administradorId);

            var produto = await ExigirProduto(produtoId);

            // As restrições só são necessárias ao liberar a venda.
            if (disponivel)
            {
                if (!produto.Ativo)
                {
                    throw new RegraProdutoException(
                        "Reative o produto antes de disponibilizá-lo.");
                }

                await ExigirCategoriaAtiva(produto.CategoriaId);
            }

            produto.Disponivel = disponivel;

            await _context.SaveChangesAsync();
        }

        //Vai filtrar e trazer só a categoria selecionada pelo usuario
        public async Task<List<ProdutoResponseDTO>> ListarCardapio(int? categoriaId = null)
        {
            var consulta = _context.Produtos
                .AsNoTracking()
                .Where(p => p.Ativo && p.Categoria.Ativa);

            if (categoriaId.HasValue)
            {
                consulta = consulta.Where(p => p.CategoriaId == categoriaId.Value);
            }

            return await consulta
                .OrderBy(p => p.Categoria.Nome)
                .ThenBy(p => p.Nome)
                .Select(p => new ProdutoResponseDTO
                {
                    Id = p.Id,
                    Nome = p.Nome,
                    Descricao = p.Descricao,
                    Preco = p.Preco,
                    ImagemUrl = p.Imagem,
                    Disponivel = p.Disponivel,
                    CategoriaId = p.CategoriaId,
                    NomeCategoria = p.Categoria.Nome
                })
                .ToListAsync();
        }
    }
}
