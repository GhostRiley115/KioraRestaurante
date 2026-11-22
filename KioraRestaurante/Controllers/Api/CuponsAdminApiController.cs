using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Claims;
using KioraRestaurante.DTOs.Cupons;
using KioraRestaurante.Models.Cupons;
using KioraRestaurante.Models.Enums;
using KioraRestaurante.Services.Cupons;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KioraRestaurante.Controllers.Api;

// Gerenciamento administrativo dos cupons de desconto para o Kiora Mobile.
// Reutiliza as regras comerciais existentes no painel administrativo do site.

[ApiController]
[Route("api/admin/cupons")]
[ResponseCache(
    NoStore = true,
    Location = ResponseCacheLocation.None)]
[Authorize(
    AuthenticationSchemes =
        JwtBearerDefaults.AuthenticationScheme,
    Roles = nameof(TipoUsuario.Administrador))]
public sealed class CuponsAdminApiController
    : ControllerBase
{
    private readonly CupomService _cupons;

    private readonly ILogger<CuponsAdminApiController>
        _logger;

    public CuponsAdminApiController(
        CupomService cupons,
        ILogger<CuponsAdminApiController> logger)
    {
        _cupons = cupons;
        _logger = logger;
    }

    // LISTAR CUPONS
   
    // Lista os 100 cupons mais recentes, incluindo indicadores de utilização.
    // GET /api/admin/cupons
   
    [HttpGet]
    public Task<IActionResult> Listar(
        CancellationToken ct)
    {
        return Executar(async administradorId =>
        {
            var cupons = await _cupons.Listar(
                administradorId,
                ct);

            return Ok(new
            {
                sucesso = true,

                total = cupons.Count,

                totalAtivos =
                    cupons.Count(c => c.Ativo),

                totalInativos =
                    cupons.Count(c => !c.Ativo),

                limiteListagem = 100,

                cupons
            });
        });
    }

    // CONSULTAR CUPOM

    // Retorna os dados completos necessários para visualizar ou editar um cupom.
    // GET /api/admin/cupons/{id}
   
    [HttpGet("{id:int}")]
    public Task<IActionResult> Buscar(
        int id,
        CancellationToken ct)
    {
        return Executar(async administradorId =>
        {
            ValidarId(id);

            var cupom =
                await _cupons.BuscarEdicao(
                    administradorId,
                    id,
                    ct);

            return Ok(new
            {
                sucesso = true,

                fusoHorario =
                    "America/Sao_Paulo",

                cupom
            });
        });
    }

    // CADASTRAR CUPOM
    
    // Cadastra um novo cupom de desconto.
    // POST /api/admin/cupons
    
    [HttpPost]
    public Task<IActionResult> Cadastrar(
        [FromBody] SalvarCupomMobileRequestDTO dto,
        CancellationToken ct)
    {
        return Executar(async administradorId =>
        {
            if (dto.Versao.HasValue &&
                dto.Versao.Value != Guid.Empty)
            {
                throw new ValidationException(
                    "Um novo cupom não deve possuir " +
                    "versão de edição.");
            }

            var dados = ConverterDTO(
                dto,
                id: null,
                versao: Guid.Empty);

            var id = await _cupons.Salvar(
                administradorId,
                dados,
                ct);

            return CreatedAtAction(
                nameof(Buscar),

                new { id },

                new
                {
                    sucesso = true,

                    mensagem =
                        "Cupom cadastrado com sucesso.",

                    cupomId = id,

                    atualizarDetalhes = true
                });
        });
    }

    // EDITAR CUPOM

    // Atualiza as configurações comerciais de um cupom existente.
    // O código original não pode ser alterado.
    // PUT /api/admin/cupons/{id}
   
    [HttpPut("{id:int}")]
    public Task<IActionResult> Editar(
        int id,
        [FromBody] SalvarCupomMobileRequestDTO dto,
        CancellationToken ct)
    {
        return Executar(async administradorId =>
        {
            ValidarId(id);

            var versao =
                ExigirVersao(dto.Versao);

            // Confirma a existência do cupom.
            var atual =
                await _cupons.BuscarEdicao(
                    administradorId,
                    id,
                    ct);

            // Evita sobrescrever configurações alteradas por outro administrador.
            if (atual.Versao != versao)
            {
                throw new ValidationException(
                    "O cupom mudou. Abra a edição " +
                    "novamente.");
            }

            var dados = ConverterDTO(
                dto,
                id,
                versao);

            // O próprio serviço verifica novamente a versão e impede alterações no código existente.
            await _cupons.Salvar(
                administradorId,
                dados,
                ct);

            return Ok(new
            {
                sucesso = true,

                mensagem =
                    "Cupom atualizado com sucesso.",

                cupomId = id,

                atualizarDetalhes = true
            });
        });
    }

    // ATIVAR OU DESATIVAR CUPOM
    
    // Altera a situação de um cupom, preservando suas configurações e histórico.
    // PATCH /api/admin/cupons/{id}/ativo
    
    [HttpPatch("{id:int}/ativo")]
    public Task<IActionResult> AlterarAtivo(
        int id,
        [FromBody] CupomSituacaoMobileRequestDTO dto,
        CancellationToken ct)
    {
        return Executar(async administradorId =>
        {
            ValidarId(id);

            var versao =
                ExigirVersao(dto.Versao);

            if (!dto.Ativo.HasValue)
            {
                throw new ValidationException(
                    "Informe a situação desejada " +
                    "para o cupom.");
            }

            var dados =
                await _cupons.BuscarEdicao(
                    administradorId,
                    id,
                    ct);

            if (dados.Versao != versao)
            {
                throw new ValidationException(
                    "O cupom mudou. Atualize os " +
                    "detalhes antes de continuar.");
            }

            var ativo = dto.Ativo.Value;

            // Evita uma gravação desnecessária quando a situação já é a desejada.
            if (dados.Ativo != ativo)
            {
                dados.Ativo = ativo;

                // Utiliza a versão enviada pelo Mobile.
                dados.Versao = versao;

                await _cupons.Salvar(
                    administradorId,
                    dados,
                    ct);
            }

            return Ok(new
            {
                sucesso = true,

                mensagem = ativo
                    ? "Cupom ativado com sucesso."
                    : "Cupom desativado com sucesso.",

                cupomId = id,

                ativo,

                atualizarDetalhes = true
            });
        });
    }

    // RELATÓRIO DE UTILIZAÇÃO
    
    // Retorna indicadores e histórico de utilização do cupom.
  
    // GET /api/admin/cupons/{id}/relatorio
    // GET /api/admin/cupons/{id}/relatorio?pagina=2
    
    [HttpGet("{id:int}/relatorio")]
    public Task<IActionResult> Relatorio(
        int id,
        CancellationToken ct,
        [FromQuery, Range(1, int.MaxValue)]
        int pagina = 1)
    {
        return Executar(async administradorId =>
        {
            ValidarId(id);

            var relatorio =
                await _cupons.Relatorio(
                    administradorId,
                    id,
                    pagina,
                    ct);

            return Ok(new
            {
                sucesso = true,

                fusoHorario =
                    "America/Sao_Paulo",

                cupom = relatorio.Cupom,

                resumo = new
                {
                    reservados =
                        relatorio.Reservados,

                    confirmados =
                        relatorio.Confirmados,

                    cancelados =
                        relatorio.Cancelados,

                    utilizacoesAtivas =
                        relatorio.Reservados +
                        relatorio.Confirmados,

                    totalRegistros =
                        relatorio.Reservados +
                        relatorio.Confirmados +
                        relatorio.Cancelados,

                    descontosConfirmados =
                        relatorio.DescontosConfirmados
                },

                historico = new
                {
                    pagina =
                        relatorio.Pagina,

                    tamanhoPagina = 50,

                    totalPaginas =
                        relatorio.TotalPaginas,

                    temPaginaAnterior =
                        relatorio.Pagina > 1,

                    temProximaPagina =
                        relatorio.Pagina <
                            relatorio.TotalPaginas,

                    registros = relatorio.Usos
                }
            });
        });
    }

    // CONVERTER CONTRATO JSON
    
    // Converte os dados recebidos pelo Mobile para o DTO comercial existente.
    // Evita depender do ModelBinder monetário utilizado pelos formulários do site. 
    private static SalvarCupomDTO ConverterDTO(
        SalvarCupomMobileRequestDTO dto,
        int? id,
        Guid versao)
    {
        if (!dto.Tipo.HasValue ||
            !Enum.IsDefined(dto.Tipo.Value))
        {
            throw new ValidationException(
                "Informe um tipo de desconto válido.");
        }

        if (!dto.Valor.HasValue ||
            !dto.ValorMinimoProdutos.HasValue)
        {
            throw new ValidationException(
                "Informe os valores do cupom.");
        }

        if (!dto.Ativo.HasValue)
        {
            throw new ValidationException(
                "Informe a situação do cupom.");
        }

        return new SalvarCupomDTO
        {
            Id = id,

            Versao = versao,

            Codigo = dto.Codigo,

            Descricao = dto.Descricao,

            Parceiro = dto.Parceiro,

            Tipo = dto.Tipo.Value,

            Valor = dto.Valor.Value,

            ValorMinimoProdutos =
                dto.ValorMinimoProdutos.Value,

            DescontoMaximo =
                dto.DescontoMaximo,

            InicioLocal = ConverterDataLocal(
                dto.InicioLocal,
                "inicial"),

            FimLocal = ConverterDataLocal(
                dto.FimLocal,
                "final"),

            LimiteTotal =
                dto.LimiteTotal,

            LimitePorCliente =
                dto.LimitePorCliente,

            Ativo = dto.Ativo.Value
        };
    }

    // CONVERSÃO DE DATAS LOCAIS
    private static DateTime ConverterDataLocal(
        string data,
        string campo)
    {
        string[] formatos =
        [
            "yyyy-MM-dd'T'HH:mm",
            "yyyy-MM-dd'T'HH:mm:ss",
            "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF"
        ];

        if (!DateTime.TryParseExact(
                data,
                formatos,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var resultado))
        {
            throw new ValidationException(
                $"Informe a data {campo} no formato " +
                "yyyy-MM-ddTHH:mm:ss, sem indicação " +
                "de fuso horário.");
        }

        // As datas representam o horário local do restaurante. O serviço existente fará a conversão para UTC.

        return DateTime.SpecifyKind(
            resultado,
            DateTimeKind.Unspecified);
    }

    // VALIDAÇÃO DE IDENTIFICADORES
    private static void ValidarId(int id)
    {
        if (id <= 0)
        {
            throw new ValidationException(
                "Informe um ID de cupom válido.");
        }
    }
    private static Guid ExigirVersao(
        Guid? versao)
    {
        if (!versao.HasValue ||
            versao.Value == Guid.Empty)
        {
            throw new ValidationException(
                "Informe a versão atual do cupom.");
        }

        return versao.Value;
    }

    // IDENTIFICAÇÃO DO ADMINISTRADOR
    private bool TentarObterAdministradorId(
        out int administradorId)
    {
        var identificacao = User
            .FindFirst(ClaimTypes.NameIdentifier)
            ?.Value;

        return int.TryParse(
            identificacao,
            out administradorId)
            && administradorId > 0;
    }

    // TRATAMENTO PADRONIZADO
    private async Task<IActionResult> Executar(
        Func<int, Task<IActionResult>> operacao)
    {
        if (!TentarObterAdministradorId(
                out var administradorId))
        {
            return Unauthorized(new
            {
                sucesso = false,

                mensagem =
                    "Não foi possível identificar " +
                    "o administrador."
            });
        }

        try
        {
            return await operacao(
                administradorId);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new
            {
                sucesso = false,

                mensagem =
                    "Cupom não encontrado."
            });
        }
        catch (ValidationException erro)
        {
            return BadRequest(new
            {
                sucesso = false,

                mensagem = erro.Message,

                atualizarDetalhes = true
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new
            {
                sucesso = false,

                mensagem =
                    "O cupom foi alterado durante " +
                    "a operação. Atualize os detalhes " +
                    "antes de tentar novamente.",

                atualizarDetalhes = true
            });
        }
        catch (DbUpdateException erro)
        {
            _logger.LogError(
                "Falha em operação administrativa " +
                "de cupom. Administrador: {AdminId}. " +
                "Tipo: {TipoErro}.",
                administradorId,
                erro.GetType().Name);

            return StatusCode(500, new
            {
                sucesso = false,

                mensagem =
                    "Não foi possível concluir a " +
                    "operação. Confira se o código " +
                    "já existe e consulte os dados " +
                    "antes de tentar novamente.",

                atualizarDetalhes = true
            });
        }
    }
}

// CONTRATO JSON DE CADASTRO E EDIÇÃO
public sealed class SalvarCupomMobileRequestDTO
{
    public Guid? Versao { get; set; }

    [Required(
        ErrorMessage =
            "Informe o código do cupom.")]
    [StringLength(40)]
    public string Codigo { get; set; } = "";

    [Required(
        ErrorMessage =
            "Informe a descrição do cupom.")]
    [StringLength(200)]
    public string Descricao { get; set; } = "";

    [StringLength(100)]
    public string? Parceiro { get; set; }

    [Required(
        ErrorMessage =
            "Informe o tipo de desconto.")]
    [EnumDataType(typeof(TipoDescontoCupom))]
    public TipoDescontoCupom? Tipo { get; set; }

    [Required(
        ErrorMessage =
            "Informe o valor do desconto.")]
    public decimal? Valor { get; set; }

    [Required(
        ErrorMessage =
            "Informe o valor mínimo dos produtos.")]
    public decimal? ValorMinimoProdutos
    {
        get;
        set;
    }

    public decimal? DescontoMaximo
    {
        get;
        set;
    }

    [Required(
        ErrorMessage =
            "Informe o início da validade.")]
    public string InicioLocal { get; set; } = "";

    [Required(
        ErrorMessage =
            "Informe o fim da validade.")]
    public string FimLocal { get; set; } = "";

    [Range(1, 1000000000)]
    public int? LimiteTotal { get; set; }

    [Range(1, 1000000)]
    public int? LimitePorCliente
    {
        get;
        set;
    } = 1;

    [Required(
        ErrorMessage =
            "Informe a situação do cupom.")]
    public bool? Ativo { get; set; }
}

// CONTRATO JSON DE ALTERAÇÃO DA SITUAÇÃO
public sealed class CupomSituacaoMobileRequestDTO
{
    [Required(
        ErrorMessage =
            "Informe a versão atual do cupom.")]
    public Guid? Versao { get; set; }

    [Required(
        ErrorMessage =
            "Informe a situação do cupom.")]
    public bool? Ativo { get; set; }
}
