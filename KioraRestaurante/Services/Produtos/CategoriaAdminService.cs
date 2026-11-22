using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Text;
using KioraRestaurante.Data;
using KioraRestaurante.Models;
using KioraRestaurante.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace KioraRestaurante.Services.Produtos;

// Gerenciamento administrativo das categorias do cardápio do Kiora Restaurante.
// Preserva os relacionamentos com produtos e utiliza desativação lógica.
public sealed class CategoriaAdminService
{
    private readonly AppDbContext _context;

    public CategoriaAdminService(
        AppDbContext context)
    {
        _context = context;
    }

    // AUTORIZAÇÃO ADMINISTRATIVA 
    private async Task ExigirAdministrador(
        int administradorId,
        CancellationToken ct)
    {
        var autorizado =
            await _context.Usuarios.AnyAsync(
                u => u.Id == administradorId
                    && u.Ativo
                    && u.Tipo ==
                        TipoUsuario.Administrador,
                ct);

        if (!autorizado)
        {
            throw new UnauthorizedAccessException();
        }
    }

    // CONSULTA ADMINISTRATIVA
 
    private IQueryable<CategoriaAdminResponseDTO>
        ConsultaAdmin()
    {
        return _context.Categorias
            .AsNoTracking()
            .Select(c =>
                new CategoriaAdminResponseDTO
                {
                    Id = c.Id,

                    Nome = c.Nome,

                    Ativa = c.Ativa,

                    TotalProdutos =
                        c.Produtos.Count(),

                    ProdutosAtivos =
                        c.Produtos.Count(p =>
                            p.Ativo),

                    ProdutosInativos =
                        c.Produtos.Count(p =>
                            !p.Ativo),

                    ProdutosDisponiveis =
                        c.Produtos.Count(p =>
                            c.Ativa &&
                            p.Ativo &&
                            p.Disponivel),

                    PodeDesativar =
                        c.Ativa &&
                        !c.Produtos.Any(p =>
                            p.Ativo),

                    PodeReativar =
                        !c.Ativa
                });
    }

    // LISTAR CATEGORIAS

    // Lista categorias ativas e inativas.
    // Permite filtrar pela situação.
    
    public async Task<List<CategoriaAdminResponseDTO>>
        Listar(
            int administradorId,
            bool? ativa = null,
            CancellationToken ct = default)
    {
        await ExigirAdministrador(
            administradorId,
            ct);

        var consulta = ConsultaAdmin();

        if (ativa.HasValue)
        {
            consulta = consulta.Where(c =>
                c.Ativa == ativa.Value);
        }

        return await consulta
            .OrderByDescending(c => c.Ativa)
            .ThenBy(c => c.Nome)
            .ThenBy(c => c.Id)
            .ToListAsync(ct);
    }

    // BUSCAR CATEGORIA

    public async Task<CategoriaAdminResponseDTO>
        Buscar(
            int administradorId,
            int categoriaId,
            CancellationToken ct = default)
    {
        await ExigirAdministrador(
            administradorId,
            ct);

        return await BuscarResposta(
            categoriaId,
            ct);
    }

    private async Task<CategoriaAdminResponseDTO>
        BuscarResposta(
            int categoriaId,
            CancellationToken ct)
    {
        var categoria = await ConsultaAdmin()
            .SingleOrDefaultAsync(
                c => c.Id == categoriaId,
                ct);

        if (categoria == null)
        {
            throw new KeyNotFoundException(
                "Categoria não encontrada.");
        }

        return categoria;
    }

    private async Task<Categoria>
        ExigirCategoria(
            int categoriaId,
            CancellationToken ct)
    {
        var categoria =
            await _context.Categorias
                .SingleOrDefaultAsync(
                    c => c.Id == categoriaId,
                    ct);

        if (categoria == null)
        {
            throw new KeyNotFoundException(
                "Categoria não encontrada.");
        }

        return categoria;
    }

    // VALIDAR NOME
    private static string ValidarNome(
        string? nome)
    {
        if (string.IsNullOrWhiteSpace(nome))
        {
            throw new ValidationException(
                "Informe o nome da categoria.");
        }

        var nomeNormalizado =
            nome.Trim()
                .Normalize(NormalizationForm.FormC);

        if (nomeNormalizado.Length > 50)
        {
            throw new ValidationException(
                "O nome da categoria deve possuir " +
                "até 50 caracteres.");
        }

        if (nomeNormalizado.Any(char.IsControl))
        {
            throw new ValidationException(
                "O nome da categoria contém " +
                "caracteres inválidos.");
        }

        return nomeNormalizado;
    }

    // VERIFICAR DUPLICIDADE
    private async Task ExigirNomeDisponivel(
        string nome,
        int? categoriaIdIgnorada,
        CancellationToken ct)
    {
        var consulta = _context.Categorias
            .AsNoTracking();

        if (categoriaIdIgnorada.HasValue)
        {
            consulta = consulta.Where(c =>
                c.Id != categoriaIdIgnorada.Value);
        }

        // Inclui categorias inativas para impedir que a reativação futura crie duplicidades.

        var nomes = await consulta
            .Select(c => c.Nome)
            .ToListAsync(ct);

        var existe = nomes.Any(nomeExistente =>
            string.Equals(
                nomeExistente.Trim()
                    .Normalize(
                        NormalizationForm.FormC),
                nome,
                StringComparison.OrdinalIgnoreCase));

        if (existe)
        {
            throw new ValidationException(
                "Já existe uma categoria com " +
                "esse nome. Consulte também " +
                "as categorias inativas.");
        }
    }

    // CADASTRAR CATEGORIA
    
    public async Task<CategoriaAdminResponseDTO>
        Cadastrar(
            int administradorId,
            string? nome,
            CancellationToken ct = default)
    {
        await ExigirAdministrador(
            administradorId,
            ct);

        var nomeValidado =
            ValidarNome(nome);

        // Mantém a verificação e a gravação dentro da mesma transação.

        await using var transacao =
            await _context.Database
                .BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    ct);

        await ExigirNomeDisponivel(
            nomeValidado,
            categoriaIdIgnorada: null,
            ct: ct);

        var categoria = new Categoria
        {
            Nome = nomeValidado,

            Ativa = true
        };

        _context.Categorias.Add(categoria);

        await _context.SaveChangesAsync(ct);

        var resposta = await BuscarResposta(
            categoria.Id,
            ct);

        await transacao.CommitAsync(ct);

        return resposta;
    }

    // EDITAR CATEGORIA

    public async Task<CategoriaAdminResponseDTO>
        Editar(
            int administradorId,
            int categoriaId,
            string? nome,
            CancellationToken ct = default)
    {
        await ExigirAdministrador(
            administradorId,
            ct);

        var nomeValidado =
            ValidarNome(nome);

        await using var transacao =
            await _context.Database
                .BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    ct);

        var categoria =
            await ExigirCategoria(
                categoriaId,
                ct);

        await ExigirNomeDisponivel(
            nomeValidado,
            categoriaId,
            ct);

        // Altera somente o nome.
        // O ID e os produtos associados permanecem os mesmos.

        categoria.Nome = nomeValidado;

        await _context.SaveChangesAsync(ct);

        var resposta = await BuscarResposta(
            categoriaId,
            ct);

        await transacao.CommitAsync(ct);

        return resposta;
    }

    // ATIVAR OU DESATIVAR CATEGORIA

    public async Task<CategoriaAdminResponseDTO>
        AlterarAtiva(
            int administradorId,
            int categoriaId,
            bool ativa,
            CancellationToken ct = default)
    {
        await ExigirAdministrador(
            administradorId,
            ct);

        await using var transacao =
            await _context.Database
                .BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    ct);

        var categoria =
            await ExigirCategoria(
                categoriaId,
                ct);

        // Solicitações repetidas não precisam produzir uma nova alteração.

        if (categoria.Ativa == ativa)
        {
            var respostaAtual =
                await BuscarResposta(
                    categoriaId,
                    ct);

            await transacao.CommitAsync(ct);

            return respostaAtual;
        }

        // Impede desativar uma categoria que ainda possui produtos ativos.

        if (!ativa)
        {
            var possuiProdutosAtivos =
                await _context.Produtos.AnyAsync(
                    p => p.CategoriaId == categoriaId
                        && p.Ativo,
                    ct);

            if (possuiProdutosAtivos)
            {
                throw new ValidationException(
                    "Esta categoria possui produtos " +
                    "ativos vinculados. Transfira ou " +
                    "desative esses produtos antes " +
                    "de desativar a categoria.");
            }
        }
        else
        {
            // Confere novamente o nome antes de reativar.

            await ExigirNomeDisponivel(
                categoria.Nome.Trim()
                    .Normalize(
                        NormalizationForm.FormC),
                categoriaId,
                ct);
        }

        categoria.Ativa = ativa;

        await _context.SaveChangesAsync(ct);

        var resposta =
            await BuscarResposta(
                categoriaId,
                ct);

        await transacao.CommitAsync(ct);

        return resposta;
    }
}

// CONTRATO ADMINISTRATIVO DA CATEGORIA
public sealed class CategoriaAdminResponseDTO
{
    public int Id { get; set; }

    public string Nome { get; set; } = "";

    public bool Ativa { get; set; }

    public int TotalProdutos { get; set; }

    public int ProdutosAtivos { get; set; }

    public int ProdutosInativos { get; set; }

    public int ProdutosDisponiveis { get; set; }

    public bool PodeDesativar { get; set; }

    public bool PodeReativar { get; set; }
}
