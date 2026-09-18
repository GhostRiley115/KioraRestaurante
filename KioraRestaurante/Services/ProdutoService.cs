using System.ComponentModel.DataAnnotations;
using KioraRestaurante.Data;
using KioraRestaurante.DTOs.Produto;
using KioraRestaurante.Models;
using KioraRestaurante.Models.Enums;
using KioraRestaurante.Services.Exceptions;
using KioraRestaurante.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

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
