using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using KioraRestaurante.Data;
using KioraRestaurante.DTOs.Carrinho;
using KioraRestaurante.DTOs.Cupons;
using KioraRestaurante.Models;
using KioraRestaurante.Models.Cupons;
using KioraRestaurante.Models.Enums;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace KioraRestaurante.Services.Cupons;

public class CupomService(AppDbContext db, IDataProtectionProvider protecao, TimeProvider relogio)
{
    private readonly IDataProtector _protetor = protecao.CreateProtector("Kiora.Cupom.Checkout.v1");
    private DateTime Agora => relogio.GetUtcNow().UtcDateTime;
    private IsolationLevel Isolamento => db.Database.ProviderName?.Contains("Sqlite", StringComparison.Ordinal) == true
        ? IsolationLevel.Serializable : IsolationLevel.ReadCommitted;
    private sealed record Cotacao(int UsuarioId, int CupomId, string Codigo, string? Parceiro, Guid Versao,
        decimal Desconto, string ProdutosHash, DateTime ExpiraEmUtc);
    public static string Normalizar(string? codigo) => (codigo ?? "").Trim().ToUpperInvariant();
    private static string Hash(CarrinhoResponseDTO resumo) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        JsonSerializer.Serialize(resumo.Itens.OrderBy(i => i.ProdutoId).Select(i => new { i.ProdutoId, i.Quantidade, i.PrecoUnitario, i.Composicao })))));

    private async Task ExigirUsuario(int id, TipoUsuario tipo, CancellationToken ct = default)
    {
        if (!await db.Usuarios.AnyAsync(u => u.Id == id && u.Ativo && u.Tipo == tipo, ct)) throw new UnauthorizedAccessException();
    }
    public Task ExigirAdministrador(int id, CancellationToken ct = default) => ExigirUsuario(id, TipoUsuario.Administrador, ct);

    public static decimal CalcularDesconto(Cupom cupom, decimal subtotal)
    {
        if (subtotal <= 0 || subtotal < cupom.ValorMinimoProdutos) throw new ValidationException("O pedido não atingiu o valor mínimo de produtos deste cupom.");
        var desconto = cupom.Tipo == TipoDescontoCupom.Percentual ? subtotal * cupom.Valor / 100m : cupom.Valor;
        if (cupom.DescontoMaximo.HasValue) desconto = Math.Min(desconto, cupom.DescontoMaximo.Value);
        desconto = decimal.Round(Math.Min(subtotal, desconto), 2, MidpointRounding.AwayFromZero);
        if (desconto <= 0) throw new ValidationException("Este cupom não gera desconto para o carrinho atual.");
        return desconto;
    }

    private void ExigirValido(Cupom? c)
    {
        if (c == null || !c.Ativo || c.InicioEmUtc > Agora || c.FimEmUtc <= Agora)
            throw new ValidationException("Cupom inválido, indisponível ou fora da validade.");
    }

    public async Task<CupomAplicado> Cotar(int usuarioId, string? codigo, CarrinhoResponseDTO resumo, CancellationToken ct = default)
    {
        await ExigirUsuario(usuarioId, TipoUsuario.Cliente, ct);
        if (!resumo.PodeProsseguir || resumo.Itens.Count == 0) throw new ValidationException("Confira os produtos do carrinho antes de aplicar o cupom.");
        var normalizado = Normalizar(codigo);
        if (!Regex.IsMatch(normalizado, "^[A-Z0-9_-]{3,40}$")) throw new ValidationException("Informe um código de cupom válido.");
        var id = await db.Cupons.Where(c => c.Codigo == normalizado).Select(c => (int?)c.Id).SingleOrDefaultAsync(ct);
        if (!id.HasValue) throw new ValidationException("Cupom inválido, indisponível ou fora da validade.");
        await Reconciliar(id.Value, ct);
        var cupom = await db.Cupons.AsNoTracking().SingleAsync(c => c.Id == id, ct);
        ExigirValido(cupom);
        if (cupom.LimiteTotal.HasValue && cupom.UtilizacoesAtivas >= cupom.LimiteTotal.Value)
            throw new ValidationException("Este cupom já atingiu o limite de utilizações disponíveis.");
        if (cupom.LimitePorCliente.HasValue && await db.UsosCupom.CountAsync(u => u.CupomId == cupom.Id && u.UsuarioId == usuarioId && u.Situacao != SituacaoUsoCupom.Cancelado, ct) >= cupom.LimitePorCliente.Value)
            throw new ValidationException("Você já atingiu o limite de uso deste cupom.");
        var dados = new Cotacao(usuarioId, cupom.Id, cupom.Codigo, cupom.Parceiro, cupom.Versao,
            CalcularDesconto(cupom, resumo.Total), Hash(resumo), Agora.AddMinutes(15));
        return Aplicado(dados, _protetor.Protect(JsonSerializer.Serialize(dados)));
    }

    private static CupomAplicado Aplicado(Cotacao c, string token) => new(c.CupomId, c.Codigo, c.Parceiro, c.Versao, c.Desconto, c.ProdutosHash, token, c.ExpiraEmUtc);

    public async Task<CupomAplicado> Conferir(int usuarioId, string? codigo, string? token, CarrinhoResponseDTO resumo, CancellationToken ct = default)
    {
        await ExigirUsuario(usuarioId, TipoUsuario.Cliente, ct);
        Cotacao? dados;
        try { dados = JsonSerializer.Deserialize<Cotacao>(_protetor.Unprotect(token ?? "")); }
        catch (Exception ex) when (ex is CryptographicException or JsonException or ArgumentException)
        { throw new ValidationException("Aplique novamente o cupom antes de confirmar o pedido."); }
        if (dados == null || dados.UsuarioId != usuarioId || dados.Codigo != Normalizar(codigo)
            || dados.ExpiraEmUtc <= Agora || dados.ProdutosHash != Hash(resumo))
            throw new ValidationException("O carrinho ou o cupom mudou. Aplique o cupom novamente.");
        var cupom = await db.Cupons.AsNoTracking().SingleOrDefaultAsync(c => c.Id == dados.CupomId, ct);
        ExigirValido(cupom);
        if (cupom!.Versao != dados.Versao || CalcularDesconto(cupom, resumo.Total) != dados.Desconto)
            throw new ValidationException("As regras do cupom mudaram. Aplique o cupom novamente.");
        return Aplicado(dados, token!);
    }

    // O chamador mantém uma transação que também gravará pedido, uso e limpeza do carrinho.
    public async Task Reservar(Pedido pedido, CupomAplicado aplicado, CancellationToken ct = default)
    {
        if (db.Database.CurrentTransaction == null) throw new InvalidOperationException("A reserva de cupom exige transação.");
        var agora = Agora;
        if (aplicado.ExpiraEmUtc <= agora) throw new ValidationException("O cupom expirou. Aplique novamente antes de confirmar.");
        var n = await db.Cupons.Where(c => c.Id == aplicado.CupomId && c.Versao == aplicado.Versao && c.Ativo
                && c.InicioEmUtc <= agora && c.FimEmUtc > agora && c.UtilizacoesAtivas < int.MaxValue)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.UtilizacoesAtivas, c => c.UtilizacoesAtivas + 1), ct);
        if (n != 1) throw new ValidationException("O cupom mudou ou ficou indisponível. Confira a revisão.");
        var c = await db.Cupons.FindAsync([aplicado.CupomId], ct) ?? throw new ValidationException("Cupom indisponível.");
        await db.Entry(c).ReloadAsync(ct);
        await ReconciliarComTrava(c, 1, ct);
        if (c.LimiteTotal.HasValue && c.UtilizacoesAtivas > c.LimiteTotal.Value)
            throw new ValidationException("A última utilização deste cupom já foi reservada. Remova o cupom ou tente outro.");
        if (c.LimitePorCliente.HasValue && await db.UsosCupom.CountAsync(u => u.CupomId == c.Id && u.UsuarioId == pedido.UsuarioId && u.Situacao != SituacaoUsoCupom.Cancelado, ct) >= c.LimitePorCliente.Value)
            throw new ValidationException("Você já atingiu o limite de uso deste cupom.");
        var uso = new UsoCupom { CupomId = c.Id, Pedido = pedido, UsuarioId = pedido.UsuarioId, Codigo = aplicado.Codigo,
            Parceiro = aplicado.Parceiro, Desconto = aplicado.Desconto, ReservadoEmUtc = agora };
        pedido.UsoCupom = uso; db.UsosCupom.Add(uso);
    }

    public async Task ReconciliarPedido(int pedidoId, CancellationToken ct = default)
    {
        var id = await db.UsosCupom.Where(u => u.PedidoId == pedidoId).Select(u => (int?)u.CupomId).SingleOrDefaultAsync(ct);
        if (id.HasValue) await Reconciliar(id.Value, ct);
    }

    public async Task Reconciliar(int cupomId, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(Isolamento, ct);
        // A atualização mantém a linha bloqueada até concluir a contagem, em todas as instâncias do site.
        await db.Cupons.Where(c => c.Id == cupomId).ExecuteUpdateAsync(s => s.SetProperty(c => c.UtilizacoesAtivas, c => c.UtilizacoesAtivas), ct);
        var cupom = await db.Cupons.FindAsync([cupomId], ct);
        if (cupom != null) { await db.Entry(cupom).ReloadAsync(ct); await ReconciliarComTrava(cupom, 0, ct); }
        await tx.CommitAsync(ct);
    }

    private async Task ReconciliarComTrava(Cupom cupom, int reservaEmAndamento, CancellationToken ct)
    {
        var usos = await db.UsosCupom.Where(u => u.CupomId == cupom.Id && u.Situacao != SituacaoUsoCupom.Cancelado
            && (u.Pedido.StatusPedido == StatusPedido.Cancelado || u.Situacao == SituacaoUsoCupom.Reservado
                && (u.Pedido.FormaPagamento == FormaPagamento.Dinheiro && u.Pedido.StatusPedido == StatusPedido.Entregue
                    || u.Pedido.FormaPagamento != FormaPagamento.Dinheiro && u.Pedido.StatusPagamento == StatusPagamento.Aprovado))).Select(u => new { Uso = u, Cancelado = u.Pedido.StatusPedido == StatusPedido.Cancelado }).ToListAsync(ct);
        foreach (var registro in usos)
        {
            var uso = registro.Uso; await db.Entry(uso).ReloadAsync(ct);
            if (registro.Cancelado) { uso.Situacao = SituacaoUsoCupom.Cancelado; uso.CanceladoEmUtc = Agora; }
            else { uso.Situacao = SituacaoUsoCupom.Confirmado; uso.ConfirmadoEmUtc ??= Agora; }
            uso.Versao = Guid.NewGuid();
        }
        if (usos.Count > 0) await db.SaveChangesAsync(ct);
        cupom.UtilizacoesAtivas = checked(await db.UsosCupom.CountAsync(u => u.CupomId == cupom.Id && u.Situacao != SituacaoUsoCupom.Cancelado, ct) + reservaEmAndamento);
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> Salvar(int adminId, SalvarCupomDTO dto, CancellationToken ct = default)
    {
        await ExigirAdministrador(adminId, ct); Validator.ValidateObject(dto, new(dto), true);
        var codigo = Normalizar(dto.Codigo);
        if (!Regex.IsMatch(codigo, "^[A-Z0-9_-]{3,40}$")) throw new ValidationException("Use de 3 a 40 letras sem acento, números, hífen ou sublinhado no código.");
        if (dto.Valor <= 0 || dto.Valor > 99999999.99m || decimal.Round(dto.Valor, 2) != dto.Valor
            || dto.Tipo == TipoDescontoCupom.Percentual && dto.Valor > 100
            || dto.ValorMinimoProdutos is < 0 or > 99999999.99m || decimal.Round(dto.ValorMinimoProdutos, 2) != dto.ValorMinimoProdutos
            || dto.DescontoMaximo.HasValue && (dto.DescontoMaximo <= 0 || dto.DescontoMaximo > 99999999.99m || decimal.Round(dto.DescontoMaximo.Value, 2) != dto.DescontoMaximo))
            throw new ValidationException("Confira os valores do cupom. Percentual deve ser de até 100%; valores em reais devem ter até duas casas decimais.");
        if (dto.InicioLocal.Year is < 2020 or > 2099 || dto.FimLocal.Year is < 2020 or > 2099 || dto.FimLocal <= dto.InicioLocal)
            throw new ValidationException("A validade final deve ser posterior ao início.");
        var fuso = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        var inicio = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(dto.InicioLocal, DateTimeKind.Unspecified), fuso);
        var fim = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(dto.FimLocal, DateTimeKind.Unspecified), fuso);
        if (await db.Cupons.AnyAsync(c => c.Codigo == codigo && c.Id != dto.Id, ct)) throw new ValidationException("Já existe um cupom com esse código.");
        await using var tx = await db.Database.BeginTransactionAsync(Isolamento, ct);
        Cupom cupom;
        if (dto.Id.HasValue)
        {
            var versao = Guid.NewGuid();
            var n = await db.Cupons.Where(c => c.Id == dto.Id && c.Versao == dto.Versao).ExecuteUpdateAsync(s => s.SetProperty(c => c.Versao, versao), ct);
            if (n != 1) throw new ValidationException("O cupom mudou. Abra a edição novamente.");
            cupom = (await db.Cupons.FindAsync([dto.Id.Value], ct))!; await db.Entry(cupom).ReloadAsync(ct);
            if (cupom.Codigo != codigo) throw new ValidationException("O código de um cupom existente não pode ser trocado. Crie outro cupom para usar um novo código.");
            await ReconciliarComTrava(cupom, 0, ct);
            if (dto.LimiteTotal.HasValue && dto.LimiteTotal < cupom.UtilizacoesAtivas) throw new ValidationException("O limite total não pode ser menor que os usos ativos já registrados.");
        }
        else { cupom = new() { Codigo = codigo, CriadoPorId = adminId, CriadoEmUtc = Agora }; db.Cupons.Add(cupom); }
        cupom.Descricao = dto.Descricao.Trim(); cupom.Parceiro = string.IsNullOrWhiteSpace(dto.Parceiro) ? null : dto.Parceiro.Trim();
        cupom.Tipo = dto.Tipo; cupom.Valor = dto.Valor; cupom.ValorMinimoProdutos = dto.ValorMinimoProdutos; cupom.DescontoMaximo = dto.DescontoMaximo;
        cupom.InicioEmUtc = inicio; cupom.FimEmUtc = fim; cupom.LimiteTotal = dto.LimiteTotal; cupom.LimitePorCliente = dto.LimitePorCliente;
        cupom.Ativo = dto.Ativo;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return cupom.Id;
    }

    public async Task<SalvarCupomDTO> BuscarEdicao(int adminId, int id, CancellationToken ct = default)
    {
        await ExigirAdministrador(adminId, ct);
        var c = await db.Cupons.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, ct) ?? throw new KeyNotFoundException();
        return new() { Id = c.Id, Versao = c.Versao, Codigo = c.Codigo, Descricao = c.Descricao, Parceiro = c.Parceiro, Tipo = c.Tipo,
            Valor = c.Valor, ValorMinimoProdutos = c.ValorMinimoProdutos, DescontoMaximo = c.DescontoMaximo,
            InicioLocal = HorarioRestaurante.ParaLocal(c.InicioEmUtc), FimLocal = HorarioRestaurante.ParaLocal(c.FimEmUtc),
            LimiteTotal = c.LimiteTotal, LimitePorCliente = c.LimitePorCliente, Ativo = c.Ativo };
    }

    public async Task<List<CupomResumoAdminDTO>> Listar(int adminId, CancellationToken ct = default)
    {
        await ExigirAdministrador(adminId, ct);
        var recentes = await db.Cupons.AsNoTracking().OrderByDescending(c => c.CriadoEmUtc).Take(100).Select(c => c.Id).ToListAsync(ct);
        // Somente cupons com mudança de uso precisam de uma transação de reconciliação nesta listagem.
        var pendentes = await db.UsosCupom.Where(u => recentes.Contains(u.CupomId) && u.Situacao != SituacaoUsoCupom.Cancelado
            && (u.Pedido.StatusPedido == StatusPedido.Cancelado || u.Situacao == SituacaoUsoCupom.Reservado
                && (u.Pedido.FormaPagamento == FormaPagamento.Dinheiro && u.Pedido.StatusPedido == StatusPedido.Entregue
                    || u.Pedido.FormaPagamento != FormaPagamento.Dinheiro && u.Pedido.StatusPagamento == StatusPagamento.Aprovado)))
            .Select(u => u.CupomId).Distinct().ToListAsync(ct);
        foreach (var id in pendentes) await Reconciliar(id, ct);
        var resultados = await db.Cupons.AsNoTracking().OrderByDescending(c => c.CriadoEmUtc).Take(100).Select(c => new CupomResumoAdminDTO
        {
            Id = c.Id, Codigo = c.Codigo, Descricao = c.Descricao, Parceiro = c.Parceiro, Tipo = c.Tipo, Valor = c.Valor,
            Ativo = c.Ativo, FimEmUtc = c.FimEmUtc, LimiteTotal = c.LimiteTotal, LimitePorCliente = c.LimitePorCliente,
            Reservas = c.Usos.Count(u => u.Situacao == SituacaoUsoCupom.Reservado), Confirmados = c.Usos.Count(u => u.Situacao == SituacaoUsoCupom.Confirmado),
            Cancelados = c.Usos.Count(u => u.Situacao == SituacaoUsoCupom.Cancelado), DescontoConfirmado = 0
        }).ToListAsync(ct);
        var ids = resultados.Select(c => c.Id).ToList();
        var descontos = await db.UsosCupom.AsNoTracking().Where(u => ids.Contains(u.CupomId) && u.Situacao == SituacaoUsoCupom.Confirmado)
            .Select(u => new { u.CupomId, u.Desconto }).ToListAsync(ct);
        foreach (var item in resultados) item.DescontoConfirmado = descontos.Where(u => u.CupomId == item.Id).Sum(u => u.Desconto);
        return resultados;
    }

    public async Task<RelatorioCupomDTO> Relatorio(int adminId, int id, int pagina, CancellationToken ct = default)
    {
        var cupom = await BuscarEdicao(adminId, id, ct);
        await Reconciliar(id, ct);
        var query = db.UsosCupom.AsNoTracking().Where(u => u.CupomId == id);
        var total = await query.CountAsync(ct); var paginas = Math.Max(1, (total + 49) / 50); pagina = Math.Clamp(pagina, 1, paginas);
        var totais = await query.Select(u => new { u.Situacao, u.Desconto }).ToListAsync(ct);
        var usos = await query.OrderByDescending(u => u.ReservadoEmUtc).ThenBy(u => u.Id).Skip((pagina - 1) * 50).Take(50)
            .Select(u => new UsoCupomRelatorio(u.PedidoId, u.UsuarioId, u.Parceiro, u.Desconto, u.Situacao, u.ReservadoEmUtc, u.ConfirmadoEmUtc, u.CanceladoEmUtc)).ToListAsync(ct);
        return new(cupom, totais.Count(u => u.Situacao == SituacaoUsoCupom.Reservado), totais.Count(u => u.Situacao == SituacaoUsoCupom.Confirmado),
            totais.Count(u => u.Situacao == SituacaoUsoCupom.Cancelado), totais.Where(u => u.Situacao == SituacaoUsoCupom.Confirmado).Sum(u => u.Desconto), usos, pagina, paginas);
    }
}
