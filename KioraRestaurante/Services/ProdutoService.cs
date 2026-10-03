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
                    EhCombo = p.EhCombo,
                    DescontoPercentual = p.DescontoPercentual,
                    Componentes = p.Componentes.OrderBy(c => c.ProdutoId)
                        .Select(c => new ComboComponenteRequestDTO
                        {
                            ProdutoId = c.ProdutoId,
                            Quantidade = c.Quantidade
                        }).ToList(),
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
                .Include(p => p.Componentes)
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

        // Confere a composição antes de enviar imagem ou alterar o banco.
        private async Task ValidarComposicao(bool ehCombo, List<ComboComponenteRequestDTO>? componentes, int? produtoAtualId = null)
        {
            if (componentes == null)
            {
                throw new RegraProdutoException("A composição do produto não foi informada corretamente.");
            }

            if (!ehCombo)
            {
                if (componentes.Count > 0)
                {
                    throw new RegraProdutoException("Um produto comum não pode receber componentes.");
                }

                return;
            }

            if (componentes.Count == 0)
            {
                throw new RegraProdutoException("O combo precisa ter pelo menos um componente.");
            }

            // Valida os atributos de cada componente.
            // Validar o DTO principal não valida recursivamente essa lista.
            foreach (var componente in componentes)
            {
                if (componente == null)
                {
                    throw new RegraProdutoException(
                        "Existe um componente inválido.");
                }

                Validator.ValidateObject(
                    componente,
                    new ValidationContext(componente),
                    validateAllProperties: true);
            }

            var ids = componentes
                .Select(c => c.ProdutoId)
                .ToList();

            if (ids.Distinct().Count() != ids.Count)
            {
                throw new RegraProdutoException("Não repita o mesmo produto. Altere sua quantidade.");
            }

            if (produtoAtualId.HasValue && ids.Contains(produtoAtualId.Value))
            {
                throw new RegraProdutoException("O combo não pode conter ele mesmo.");
            }

            var produtos = await _context.Produtos
                .AsNoTracking()
                .Where(p => ids.Contains(p.Id))
                .Select(p => new
                {
                    p.Id,
                    p.EhCombo
                }).ToListAsync();

            if (produtos.Count != ids.Count)
            {
                throw new RegraProdutoException("Um ou mais componentes não existem.");
            }

            if (produtos.Any(p => p.EhCombo))
            {
                throw new RegraProdutoException("Não é permitido colocar um combo dentro de outro combo.");
            }

            if (produtoAtualId.HasValue)
            {
                // Impede transformar em combo um produto que já está
                // dentro da composição de outro combo.
                var utilizadoEmOutroCombo = await _context.ComboComponentes
                    .AnyAsync(c => c.ProdutoId == produtoAtualId.Value);

                if (utilizadoEmOutroCombo)
                {
                    throw new RegraProdutoException(
                        "Este produto é componente de outro combo. " +
                        "Remova-o dessa composição antes de transformá-lo em combo.");
                }
            }
        }

        // Aplica uma composição que já passou pelas validações.
        private void AplicarComposicao(
            Produto produto,
            bool ehCombo,
            List<ComboComponenteRequestDTO> componentes)
        {
            produto.EhCombo = ehCombo;

            var idsRecebidos = componentes
                .Select(c => c.ProdutoId)
                .ToHashSet();

            // Remove somente os componentes que deixaram a composição.
            var removidos = produto.Componentes
                .Where(c => !idsRecebidos.Contains(c.ProdutoId))
                .ToList();

            foreach (var removido in removidos)
            {
                _context.ComboComponentes.Remove(removido);
                produto.Componentes.Remove(removido);
            }

            foreach (var recebido in componentes)
            {
                var existente = produto.Componentes
                    .SingleOrDefault(c => c.ProdutoId == recebido.ProdutoId);

                if (existente != null)
                {
                    existente.Quantidade = recebido.Quantidade;
                }
                else
                {
                    produto.Componentes.Add(new ComboComponente
                    {
                        ProdutoId = recebido.ProdutoId,
                        Quantidade = recebido.Quantidade
                    });
                }
            }
        }

        // Lista produtos comuns que podem compor um combo.
        public async Task<List<OpcaoComponenteDTO>> ListarOpcoesComponentes(
            int administradorId,
            int? produtoAtualId = null)
        {
            await ExigirAdministrador(administradorId);

            var consulta = _context.Produtos
                .AsNoTracking()
                .Where(p => !p.EhCombo);

            if (produtoAtualId.HasValue)
            {
                consulta = consulta.Where(p => p.Id != produtoAtualId.Value);
            }

            return await consulta
                .OrderBy(p => p.Nome)
                .Select(p => new OpcaoComponenteDTO
                {
                    Id = p.Id,

                    // Mantém componentes indisponíveis visíveis na edição.
                    Nome = p.Nome +
                           ((!p.Ativo || !p.Disponivel || !p.Categoria.Ativa)
                               ? " — indisponível"
                               : "")
                })
                .ToListAsync();
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

            await ValidarComposicao(dto.EhCombo, dto.Componentes, produtoId);

            // Um preço novo também precisa ser válido com o desconto já existente.
            PrecoProduto.Calcular(dto.Preco, produto.DescontoPercentual);

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
            AplicarComposicao(produto, dto.EhCombo, dto.Componentes);

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

            await ValidarComposicao(dto.EhCombo, dto.Componentes);

            // Produto novo começa sem desconto.
            // O desconto será configurado na tela própria.
            PrecoProduto.Calcular(dto.Preco, 0m);

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

            // Prepara o tipo e os componentes antes de salvar.
            AplicarComposicao(produto, dto.EhCombo, dto.Componentes);

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

        // Filtra o catálogo e depois calcula as informações de venda.
        public async Task<List<ProdutoResponseDTO>> ListarCardapio(
            int? categoriaId = null)
        {
            var consulta = _context.Produtos
                .AsNoTracking()
                .Where(p => p.Ativo && p.Categoria.Ativa);

            // Preserva o filtro de categoria já existente.
            if (categoriaId.HasValue)
            {
                consulta = consulta.Where(
                    p => p.CategoriaId == categoriaId.Value);
            }

            // Carrega os dados necessários para validar também os combos.
            var produtos = await consulta
                .Include(p => p.Categoria)
                .Include(p => p.Componentes)
                .ThenInclude(c => c.Produto)
                .ThenInclude(p => p.Categoria)
                .OrderBy(p => p.Categoria.Nome)
                .ThenBy(p => p.Nome)
                .ToListAsync();

            // Agora os dados estão na memória e podemos chamar
            // nossos métodos C# de cálculo e disponibilidade.
            return produtos.Select(produto => new ProdutoResponseDTO
            {
                Id = produto.Id,
                Nome = produto.Nome,
                Descricao = produto.Descricao,
                ImagemUrl = produto.Imagem,
                CategoriaId = produto.CategoriaId,
                NomeCategoria = produto.Categoria.Nome,

                PrecoOriginal = produto.Preco,
                DescontoPercentual = produto.DescontoPercentual,

                Preco = PrecoProduto.Calcular(
                    produto.Preco,
                    produto.DescontoPercentual),

                EhCombo = produto.EhCombo,

                Disponivel = ProdutoVenda.EstaDisponivel(produto)
            }).ToList();
        }

        public async Task AlterarDesconto(int administradorId, int produtoId, AlterarDescontoRequestDTO dto)
        {
            await ExigirAdministrador(administradorId);

            Validator.ValidateObject(dto, new ValidationContext(dto), validateAllProperties: true);

            var produto = await _context.Produtos
                              .SingleOrDefaultAsync(p => p.Id == produtoId)
                          ?? throw new KeyNotFoundException("Produto não encontrado.");

            // Também verifica casas decimais e o preço final.
            PrecoProduto.Calcular(produto.Preco, dto.Percentual);

            produto.DescontoPercentual = dto.Percentual;

            await _context.SaveChangesAsync();
        }
    }
}
