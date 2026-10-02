using System.ComponentModel.DataAnnotations;
using KioraRestaurante.Data;
using KioraRestaurante.DTOs.Endereco;
using KioraRestaurante.Models;
using Microsoft.EntityFrameworkCore;

namespace KioraRestaurante.Services;

public class EnderecoUsuarioService
{
    private readonly AppDbContext _context;
    private readonly CepService _cepService;

    public EnderecoUsuarioService(
        AppDbContext context,
        CepService cepService)
    {
        _context = context;
        _cepService = cepService;
    }

    private async Task ExigirUsuario(int usuarioId)
    {
        var ativo = await _context.Usuarios
            .AnyAsync(u => u.Id == usuarioId && u.Ativo);

        if (!ativo)
        {
            throw new UnauthorizedAccessException();
        }
    }

    public async Task<List<EnderecoUsuario>> Listar(int usuarioId)
    {
        await ExigirUsuario(usuarioId);

        return await _context.EnderecosUsuario
            .AsNoTracking()
            .Where(e => e.UsuarioId == usuarioId)
            .OrderBy(e => e.Apelido)
            .ThenBy(e => e.Id)
            .ToListAsync();
    }

    public async Task<EnderecoUsuario?> Buscar(int usuarioId, int id)
    {
        await ExigirUsuario(usuarioId);

        return await _context.EnderecosUsuario
            .AsNoTracking()
            .SingleOrDefaultAsync(
                e => e.Id == id && e.UsuarioId == usuarioId);
    }

    public async Task Salvar(int usuarioId, int? id, SalvarEnderecoRequestDTO dto)
    {
        await ExigirUsuario(usuarioId);

        Validator.ValidateObject(dto, new ValidationContext(dto), validateAllProperties: true);

        EnderecoUsuario endereco;

        if (id.HasValue)
        {
            endereco = await _context.EnderecosUsuario
                .SingleOrDefaultAsync(
                    e => e.Id == id.Value && e.UsuarioId == usuarioId)
                ?? throw new KeyNotFoundException();

            if (dto.Versao != endereco.Versao)
            {
                throw new ValidationException("Esse endereço foi alterado. Abra a edição novamente.");
            }
        }
        else
        {
            endereco = new EnderecoUsuario
            {
                UsuarioId = usuarioId
            };
        }

        // O servidor verifica o CEP mesmo que o JavaScript já tenha consultado.
        var cep = await _cepService.Consultar(dto.Cep);

        _cepService.ExigirAreaAtendida(cep.Uf, cep.Ibge);

        endereco.Apelido = dto.Apelido.Trim();
        endereco.Cep = cep.Cep;

        // Se o CEP não trouxer rua ou bairro, usamos o preenchimento manual.
        endereco.Logradouro = string.IsNullOrWhiteSpace(cep.Logradouro)
            ? dto.Logradouro.Trim()
            : cep.Logradouro.Trim();

        endereco.Bairro = string.IsNullOrWhiteSpace(cep.Bairro)
            ? dto.Bairro.Trim()
            : cep.Bairro.Trim();

        endereco.Numero = dto.Numero.Trim();
        endereco.Cidade = cep.Cidade;
        endereco.Uf = cep.Uf;
        endereco.MunicipioIbge = cep.Ibge;
        endereco.Complemento = dto.Complemento?.Trim();
        endereco.Referencia = dto.Referencia?.Trim();

        // Uma edição gera uma nova versão.
        endereco.Versao = Guid.NewGuid();

        if (!id.HasValue)
        {
            _context.EnderecosUsuario.Add(endereco);
        }

        await _context.SaveChangesAsync();
    }

    public async Task<EnderecoEntrega> CriarCopiaParaPedido(int usuarioId, int enderecoId, Guid versao)
    {
        var endereco = await Buscar(usuarioId, enderecoId)
            ?? throw new ValidationException("Escolha um endereço válido da sua conta.");

        if (endereco.Versao != versao)
        {
            throw new ValidationException(
                "O endereço foi editado. Atualize a revisão e confira o endereço.");
        }

        // Reconfere a política atual de entrega, mesmo para endereços antigos.
        _cepService.ExigirAreaAtendida(endereco.Uf, endereco.MunicipioIbge);

        // Uma entidade NOVA: não reutiliza nem vincula o endereço editável.
        return new EnderecoEntrega
        {
            Cep = endereco.Cep,
            Logradouro = endereco.Logradouro,
            Numero = endereco.Numero,
            Bairro = endereco.Bairro,
            Cidade = endereco.Cidade,
            Uf = endereco.Uf,
            Complemento = endereco.Complemento,
            Referencia = endereco.Referencia
        };
    }
}