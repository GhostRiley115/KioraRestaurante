using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.Json;
using KioraRestaurante.Data;
using KioraRestaurante.DTOs.Notificacoes;
using KioraRestaurante.Models.Enums;
using KioraRestaurante.Models.Notificacoes;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace KioraRestaurante.Services.Notificacoes;

public class NotificacaoService(AppDbContext db, IDataProtectionProvider protecao, TimeProvider relogio)
{
    private readonly IDataProtector _protetor = protecao.CreateProtector("Kiora.Notificacoes.Previa.v1");
    private DateTime Agora => relogio.GetUtcNow().UtcDateTime;
    private sealed record PreviaProtegida(int AutorId, Guid Chave, string Titulo, string Mensagem, PublicoNotificacao Publico,
        int? CupomId, Guid? VersaoCupom, DateTime ExpiraEmUtc);

    private async Task<TipoUsuario> Tipo(int usuarioId, CancellationToken ct)
    {
        var tipo = await db.Usuarios.Where(u => u.Id == usuarioId && u.Ativo).Select(u => (TipoUsuario?)u.Tipo).SingleOrDefaultAsync(ct);
        return tipo ?? throw new UnauthorizedAccessException();
    }
    private async Task ExigirAdministrador(int adminId, CancellationToken ct)
    { if (await Tipo(adminId, ct) != TipoUsuario.Administrador) throw new UnauthorizedAccessException(); }
    private IQueryable<NotificacaoUsuario> Minhas(int usuarioId, TipoUsuario tipo) => db.NotificacoesUsuarios.Where(n => n.UsuarioId == usuarioId
        && (n.Notificacao.Publico == PublicoNotificacao.Todos || n.Notificacao.Publico == PublicoNotificacao.Cliente && tipo == TipoUsuario.Cliente
            || n.Notificacao.Publico == PublicoNotificacao.Administrador && tipo == TipoUsuario.Administrador));

    public async Task<int> NaoLidas(int usuarioId, CancellationToken ct = default)
    { var tipo = await Tipo(usuarioId, ct); return await Minhas(usuarioId, tipo).CountAsync(n => n.LidaEmUtc == null, ct); }

    public async Task<ListaNotificacoesDTO> Listar(int usuarioId, int pagina, CancellationToken ct = default)
    {
        var tipo = await Tipo(usuarioId, ct); var query = Minhas(usuarioId, tipo).AsNoTracking();
        var total = await query.CountAsync(ct); var paginas = Math.Max(1, (total + 19) / 20); pagina = Math.Clamp(pagina, 1, paginas);
        var itens = await query.OrderByDescending(n => n.Notificacao.CriadaEmUtc).ThenBy(n => n.NotificacaoId)
            .Skip((pagina - 1) * 20).Take(20).Select(n => new ItemNotificacaoDTO(n.NotificacaoId, n.Notificacao.Titulo,
                n.Notificacao.Mensagem, n.Notificacao.CriadaEmUtc, n.LidaEmUtc == null,
                n.Notificacao.PedidoId != null || n.Notificacao.ReservaId != null || n.Notificacao.CupomId != null)).ToListAsync(ct);
        return new(itens, pagina, paginas, await query.CountAsync(n => n.LidaEmUtc == null, ct));
    }

    public async Task MarcarLida(int usuarioId, Guid id, CancellationToken ct = default)
    {
        var tipo = await Tipo(usuarioId, ct);
        if (!await Minhas(usuarioId, tipo).AnyAsync(n => n.NotificacaoId == id, ct)) throw new KeyNotFoundException();
        await Minhas(usuarioId, tipo).Where(n => n.NotificacaoId == id && n.LidaEmUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.LidaEmUtc, (DateTime?)Agora), ct);
    }

    public async Task MarcarTodas(int usuarioId, CancellationToken ct = default)
    {
        var tipo = await Tipo(usuarioId, ct);
        await Minhas(usuarioId, tipo).Where(n => n.LidaEmUtc == null).ExecuteUpdateAsync(s => s.SetProperty(n => n.LidaEmUtc, (DateTime?)Agora), ct);
    }

    public async Task<DestinoNotificacao?> Abrir(int usuarioId, Guid id, CancellationToken ct = default)
    {
        var tipo = await Tipo(usuarioId, ct);
        var vinculo = await Minhas(usuarioId, tipo).AsNoTracking().Include(n => n.Notificacao).ThenInclude(n => n.Reserva)
            .SingleOrDefaultAsync(n => n.NotificacaoId == id, ct) ?? throw new KeyNotFoundException();
        var n = vinculo.Notificacao;
        await MarcarLida(usuarioId, id, ct);
        if (n.PedidoId.HasValue) return tipo == TipoUsuario.Administrador
            ? new("Admin", "DetalhesPedido", n.PedidoId) : new("Pedido", "Detalhes", n.PedidoId);
        if (n.ReservaId.HasValue && n.Reserva != null)
            return tipo == TipoUsuario.Administrador ? new("ReservasAdmin", "Index", Data: HorarioRestaurante.ParaLocal(n.Reserva.InicioUtc ?? n.Reserva.CriadaEmUtc).ToString("yyyy-MM-dd"))
                : new("Reservas", "Detalhes", Codigo: n.Reserva.Codigo);
        if (n.CupomId.HasValue) return new("Cupons", "Detalhes", n.CupomId);
        return null;
    }

    private IQueryable<int> Destinatarios(PublicoNotificacao publico) => db.Usuarios.Where(u => u.Ativo
        && (publico == PublicoNotificacao.Todos || publico == PublicoNotificacao.Cliente && u.Tipo == TipoUsuario.Cliente
            || publico == PublicoNotificacao.Administrador && u.Tipo == TipoUsuario.Administrador)).Select(u => u.Id);

    public async Task<PreviaNotificacao> Preparar(int adminId, CriarNotificacaoDTO dto, CancellationToken ct = default)
    {
        await ExigirAdministrador(adminId, ct); Validator.ValidateObject(dto, new(dto), true);
        if (dto.ChavePublicacao == Guid.Empty || dto.Publico is not PublicoNotificacao.Cliente and not PublicoNotificacao.Todos)
            throw new ValidationException("Confira o público e a mensagem da notificação.");
        string? codigo = null; Guid? versao = null;
        if (dto.CupomId.HasValue)
        {
            var cupom = await db.Cupons.AsNoTracking().SingleOrDefaultAsync(c => c.Id == dto.CupomId && c.Ativo && c.FimEmUtc > Agora, ct)
                ?? throw new ValidationException("O cupom selecionado não está disponível para divulgação.");
            codigo = cupom.Codigo; versao = cupom.Versao;
        }
        dto.Titulo = dto.Titulo.Trim(); dto.Mensagem = dto.Mensagem.Trim();
        var dados = new PreviaProtegida(adminId, dto.ChavePublicacao, dto.Titulo, dto.Mensagem, dto.Publico, dto.CupomId, versao, Agora.AddMinutes(10));
        dto.TokenPrevia = _protetor.Protect(JsonSerializer.Serialize(dados));
        return new(dto, await Destinatarios(dto.Publico).CountAsync(ct), codigo, dados.ExpiraEmUtc);
    }

    public async Task<Guid> Publicar(int adminId, CriarNotificacaoDTO dto, CancellationToken ct = default)
    {
        await ExigirAdministrador(adminId, ct); Validator.ValidateObject(dto, new(dto), true);
        PreviaProtegida? previa;
        try { previa = JsonSerializer.Deserialize<PreviaProtegida>(_protetor.Unprotect(dto.TokenPrevia ?? "")); }
        catch (Exception ex) when (ex is CryptographicException or ArgumentException or JsonException)
        { throw new ValidationException("Confira a prévia da notificação novamente antes de publicar."); }
        if (previa == null || previa.AutorId != adminId || previa.Chave != dto.ChavePublicacao || previa.Titulo != dto.Titulo.Trim()
            || previa.Mensagem != dto.Mensagem.Trim() || previa.Publico != dto.Publico || previa.CupomId != dto.CupomId || previa.ExpiraEmUtc <= Agora)
            throw new ValidationException("A mensagem mudou ou a prévia expirou. Confira novamente antes de publicar.");
        var existente = await db.Notificacoes.AsNoTracking().SingleOrDefaultAsync(n => n.Id == previa.Chave, ct);
        if (existente != null)
        {
            if (existente.AutorId != adminId) throw new UnauthorizedAccessException();
            return existente.Id;
        }
        if (previa.CupomId.HasValue && !await db.Cupons.AnyAsync(c => c.Id == previa.CupomId && c.Versao == previa.VersaoCupom && c.Ativo && c.FimEmUtc > Agora, ct))
            throw new ValidationException("As regras do cupom mudaram. Confira uma nova prévia antes de divulgar.");
        var ids = await Destinatarios(previa.Publico).ToListAsync(ct);
        if (ids.Count == 0) throw new ValidationException("Não há contas ativas no público selecionado.");
        var n = new Notificacao { Id = previa.Chave, AutorId = adminId, Titulo = previa.Titulo, Mensagem = previa.Mensagem,
            Publico = previa.Publico, CupomId = previa.CupomId, Tipo = previa.CupomId.HasValue ? TipoNotificacao.CupomPromocional : TipoNotificacao.Comunicado, CriadaEmUtc = Agora };
        db.Notificacoes.Add(n);
        db.NotificacoesUsuarios.AddRange(ids.Select(id => new NotificacaoUsuario { Notificacao = n, UsuarioId = id }));
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            if (await db.Notificacoes.AnyAsync(x => x.Id == previa.Chave && x.AutorId == adminId, ct)) return previa.Chave;
            throw;
        }
        return n.Id;
    }

    public async Task<List<ComunicadoAdminDTO>> Comunicados(int adminId, CancellationToken ct = default)
    {
        await ExigirAdministrador(adminId, ct);
        return await db.Notificacoes.AsNoTracking().Where(n => n.AutorId != null).OrderByDescending(n => n.CriadaEmUtc).Take(100)
            .Select(n => new ComunicadoAdminDTO(n.Id, n.Titulo, n.CriadaEmUtc, n.Destinatarios.Count,
                n.Destinatarios.Count(d => d.LidaEmUtc != null), n.Cupom != null ? n.Cupom.Codigo : null)).ToListAsync(ct);
    }
}
