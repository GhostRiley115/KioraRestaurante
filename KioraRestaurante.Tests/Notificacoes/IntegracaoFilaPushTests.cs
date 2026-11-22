using System.Security.Cryptography;
using System.Text;
using KioraRestaurante.Data;
using KioraRestaurante.DTOs.Notificacoes;
using KioraRestaurante.Models;
using KioraRestaurante.Models.Notificacoes;
using KioraRestaurante.Options;
using KioraRestaurante.Services.Notificacoes;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Banco = KioraRestaurante.Tests.CheckoutEntregaTests.Banco;

namespace KioraRestaurante.Tests;

public sealed class IntegracaoFilaPushTests
{
    // MÉTODOS AUXILIARES
    private static FilaPushService CriarFila(
        AppDbContext db,
        TimeProvider relogio)
    {
        return new FilaPushService(
            db,
            relogio,
            Microsoft.Extensions.Options.Options.Create(
                new FirebaseOptions
                {
                    Ativado = true,
                    ProjetoId = "kiora-teste"
                }));
    }
    private static async Task RegistrarDispositivo(
        AppDbContext db,
        Usuario usuario,
        IDataProtectionProvider protecao,
        TimeProvider relogio)
    {
        var token =
            $"TOKEN_FCM_TESTE_KIORA_{usuario.Id}_" +
            Guid.NewGuid().ToString("N");

        var protetor =
            protecao.CreateProtector(
                "Kiora.Push.FcmToken.v1");

        var agora =
            relogio.GetUtcNow().UtcDateTime;

        db.DispositivosPush.Add(
            new DispositivoPush
            {
                TokenHash = Convert.ToHexString(
                    SHA256.HashData(
                        Encoding.UTF8.GetBytes(token))),

                TokenFcm = protetor.Protect(token),

                UsuarioId = usuario.Id,

                VersaoSessaoUsuario =
                    usuario.VersaoSessao,

                Ativo = true,

                Versao = Guid.NewGuid(),

                CriadoEmUtc = agora,

                AtualizadoEmUtc = agora
            });

        await db.SaveChangesAsync();
    }

    private static NotificacaoDispatcher
        CriarDispatcher(Banco b)
    {
        return new NotificacaoDispatcher(
            b.Db,
            b.Cupons,
            b.Relogio,
            CriarFila(b.Db, b.Relogio));
    }

    // TESTE 1 — NOVO PEDIDO E DESTINATÁRIOS

    [Fact]
    public async Task NovoPedidoAlimentaFilaSomenteParaDestinatarios()
    {
        using var b = new Banco();

        var admin =
            await CuponsTests.Administrador(b);

        var outro = new Usuario
        {
            Nome = "Outro cliente",

            Email = "outro-push@example.com",

            SenhaHash = "teste"
        };

        b.Db.Usuarios.Add(outro);

        await b.Db.SaveChangesAsync();

        var protecao =
            new EphemeralDataProtectionProvider();

        await RegistrarDispositivo(
            b.Db,
            b.Cliente,
            protecao,
            b.Relogio);

        await RegistrarDispositivo(
            b.Db,
            admin,
            protecao,
            b.Relogio);

        await RegistrarDispositivo(
            b.Db,
            outro,
            protecao,
            b.Relogio);

        var pedidoId =
            await b.Pedidos.ConfirmarPedido(
                b.Acesso,
                await b.Preparar(false));

        // O pedido gera eventos internos.
        // O dispatcher ainda não os distribuiu.

        Assert.Empty(
            await b.Db.EnviosPush.ToListAsync());

        var dispatcher = CriarDispatcher(b);

        await dispatcher.Processar();

        var envios = await b.Db.EnviosPush
            .AsNoTracking()
            .Include(e => e.Notificacao)
            .ToListAsync();

        Assert.Equal(2, envios.Count);

        var cliente = Assert.Single(
            envios,
            e => e.UsuarioId == b.Cliente.Id);

        var administrador = Assert.Single(
            envios,
            e => e.UsuarioId == admin.Id);

        Assert.Equal(
            TipoNotificacao.PedidoRecebido,
            cliente.Notificacao.Tipo);

        Assert.Equal(
            TipoNotificacao.NovoPedidoAdmin,
            administrador.Notificacao.Tipo);

        Assert.Equal<int?>(
            pedidoId,
            cliente.Notificacao.PedidoId);

        Assert.Equal<int?>(
            pedidoId,
            administrador.Notificacao.PedidoId);

        Assert.DoesNotContain(
            envios,
            e => e.UsuarioId == outro.Id);

        Assert.All(
            envios,
            e => Assert.Equal(
                SituacaoFilaPush.Pendente,
                e.Situacao));

        // Reprocessar não deve duplicar envios.

        await dispatcher.Processar();

        Assert.Equal(
            2,
            await b.Db.EnviosPush.CountAsync());

        Assert.Equal(
            2,
            await b.Db.Notificacoes.CountAsync());
    }

    // TESTE 2 — PUBLICAÇÃO IDEMPOTENTE

    [Fact]
    public async Task PublicarComunicadoNaoDuplicaEnvios()
    {
        using var b = new Banco();

        var admin =
            await CuponsTests.Administrador(b);

        var protecao =
            new EphemeralDataProtectionProvider();

        await RegistrarDispositivo(
            b.Db,
            b.Cliente,
            protecao,
            b.Relogio);

        await RegistrarDispositivo(
            b.Db,
            admin,
            protecao,
            b.Relogio);

        var service = new NotificacaoService(
            b.Db,
            protecao,
            b.Relogio,
            CriarFila(b.Db, b.Relogio));

        var dto = new CriarNotificacaoDTO
        {
            Titulo = "Comunicado de teste",

            Mensagem = "Confira nossas novidades.",

            Publico = PublicoNotificacao.Cliente
        };

        var previa = await service.Preparar(
            admin.Id,
            dto);

        Assert.Equal(
            1,
            previa.Destinatarios);

        // Gerar prévia não pode publicar avisos.

        Assert.Equal(
            0,
            await b.Db.EnviosPush.CountAsync());

        var primeiro = await service.Publicar(
            admin.Id,
            dto);

        var segundo = await service.Publicar(
            admin.Id,
            dto);

        Assert.Equal(primeiro, segundo);

        Assert.Equal(
            1,
            await b.Db.Notificacoes.CountAsync());

        Assert.Equal(
            1,
            await b.Db.NotificacoesUsuarios.CountAsync());

        var envio =
            await b.Db.EnviosPush.SingleAsync();

        Assert.Equal(
            primeiro,
            envio.NotificacaoId);

        Assert.Equal<int?>(
            b.Cliente.Id,
            envio.UsuarioId);

        Assert.Equal(
            SituacaoFilaPush.Pendente,
            envio.Situacao);
    }

    // TESTE 3 — FALHA TRANSACIONAL NO DISPATCHER

    [Fact]
    public async Task FalhaNaFilaReverteDistribuicaoDoEvento()
    {
        using var b = new Banco();

        var protecao =
            new EphemeralDataProtectionProvider();

        await RegistrarDispositivo(
            b.Db,
            b.Cliente,
            protecao,
            b.Relogio);

        await b.Pedidos.ConfirmarPedido(
            b.Acesso,
            await b.Preparar(false));

        var eventoId =
            await b.Db.EventosNotificacao
                .AsNoTracking()
                .Where(e =>
                    e.Tipo ==
                        TipoNotificacao.PedidoRecebido)
                .Select(e => e.Id)
                .SingleAsync();

        // Simula uma falha ao inserir o envio push.

        await b.Db.Database.ExecuteSqlRawAsync(
            """
            CREATE TRIGGER falhar_fila_push
            BEFORE INSERT ON EnviosPush
            BEGIN
                SELECT RAISE(ABORT, 'falha simulada');
            END;
            """);

        var dispatcher = CriarDispatcher(b);

        try
        {
            await Assert.ThrowsAsync<DbUpdateException>(
                () => dispatcher.Distribuir(eventoId));

            Assert.Equal(
                0,
                await b.Db.Notificacoes.CountAsync());

            Assert.Equal(
                0,
                await b.Db.NotificacoesUsuarios
                    .CountAsync());

            Assert.Equal(
                0,
                await b.Db.EnviosPush.CountAsync());

            var evento =
                await b.Db.EventosNotificacao
                    .AsNoTracking()
                    .SingleAsync(e => e.Id == eventoId);

            Assert.Null(evento.ProcessadoEmUtc);

            Assert.Null(evento.ProcessandoAteUtc);
        }
        finally
        {
            await b.Db.Database.ExecuteSqlRawAsync(
                "DROP TRIGGER falhar_fila_push;");
        }

        // Após a recuperação, grava todos os registros.

        await dispatcher.Distribuir(eventoId);

        await dispatcher.Distribuir(eventoId);

        Assert.Equal(
            1,
            await b.Db.Notificacoes.CountAsync());

        Assert.Equal(
            1,
            await b.Db.NotificacoesUsuarios.CountAsync());

        Assert.Equal(
            1,
            await b.Db.EnviosPush.CountAsync());

        var concluido =
            await b.Db.EventosNotificacao
                .AsNoTracking()
                .SingleAsync(e => e.Id == eventoId);

        Assert.NotNull(concluido.ProcessadoEmUtc);
    }

    // TESTE 4 — FALHA NA PUBLICAÇÃO DE COMUNICADO

    [Fact]
    public async Task FalhaNaFilaNaoDeixaComunicadoParcial()
    {
        using var b = new Banco();

        var admin =
            await CuponsTests.Administrador(b);

        var protecao =
            new EphemeralDataProtectionProvider();

        await RegistrarDispositivo(
            b.Db,
            b.Cliente,
            protecao,
            b.Relogio);

        var service = new NotificacaoService(
            b.Db,
            protecao,
            b.Relogio,
            CriarFila(b.Db, b.Relogio));

        var dto = new CriarNotificacaoDTO
        {
            Titulo = "Teste de transação",

            Mensagem = "Publicação controlada.",

            Publico = PublicoNotificacao.Cliente
        };

        await service.Preparar(
            admin.Id,
            dto);

        await b.Db.Database.ExecuteSqlRawAsync(
            """
            CREATE TRIGGER falhar_comunicado_push
            BEFORE INSERT ON EnviosPush
            BEGIN
                SELECT RAISE(ABORT, 'falha simulada');
            END;
            """);

        try
        {
            await Assert.ThrowsAsync<DbUpdateException>(
                () => service.Publicar(admin.Id, dto));

            Assert.Equal(
                0,
                await b.Db.Notificacoes.CountAsync());

            Assert.Equal(
                0,
                await b.Db.NotificacoesUsuarios
                    .CountAsync());

            Assert.Equal(
                0,
                await b.Db.EnviosPush.CountAsync());
        }
        finally
        {
            await b.Db.Database.ExecuteSqlRawAsync(
                "DROP TRIGGER falhar_comunicado_push;");
        }

        var id = await service.Publicar(
            admin.Id,
            dto);

        Assert.Equal(
            id,
            await service.Publicar(admin.Id, dto));

        Assert.Equal(
            1,
            await b.Db.Notificacoes.CountAsync());

        Assert.Equal(
            1,
            await b.Db.NotificacoesUsuarios.CountAsync());

        Assert.Equal(
            1,
            await b.Db.EnviosPush.CountAsync());
    }

    // SIMULADOR PARA TESTE DE CONCORRÊNCIA
    private sealed class FirebaseControlado
        : IEnvioPushService
    {
        private int _chamadas;
        public bool Habilitado => true;
        public int Chamadas =>
            Volatile.Read(ref _chamadas);
        public TaskCompletionSource<bool> Iniciado
        { get; } = new(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Liberar
        { get; } = new(
                TaskCreationOptions
                    .RunContinuationsAsynchronously);
        public async Task<ResultadoEnvioPush> EnviarAsync(
            string tokenFcm,
            Guid notificacaoId,
            CancellationToken ct = default)
        {
            var chamada =
                Interlocked.Increment(ref _chamadas);

            if (chamada == 1)
            {
                Iniciado.TrySetResult(true);

                await Liberar.Task.WaitAsync(ct);
            }

            return new ResultadoEnvioPush(
                SituacaoEnvioPush.AceitoPeloFirebase,
                MensagemId: "firebase-concorrencia");
        }
    }

    private static ProcessadorPushService
        CriarProcessador(
            IServiceProvider services,
            IEnvioPushService firebase)
    {
        return new ProcessadorPushService(
            services.GetRequiredService<AppDbContext>(),

            firebase,

            services.GetRequiredService<
                IDataProtectionProvider>(),

            services.GetRequiredService<TimeProvider>(),

            NullLogger<ProcessadorPushService>.Instance);
    }

    // TESTE 5 — CONCORRÊNCIA ENTRE PROCESSADORES

    [Fact]
    public async Task DoisProcessadoresNaoEnviamMesmoRegistro()
    {
        await using var host =
            await HostMobile.Criar();

        // Prepara uma notificação e seu envio em um banco temporário compartilhado.

        await using (
            var scope =
                host.App.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<AppDbContext>();

            var relogio = scope.ServiceProvider
                .GetRequiredService<TimeProvider>();

            var protecao = scope.ServiceProvider
                .GetRequiredService<
                    IDataProtectionProvider>();

            var cliente =
                await db.Usuarios.SingleAsync(
                    u => u.Email ==
                        "cliente@mobile.test");

            await RegistrarDispositivo(
                db,
                cliente,
                protecao,
                relogio);

            var notificacao = new Notificacao
            {
                Id = Guid.NewGuid(),

                Tipo = TipoNotificacao.Comunicado,

                Publico = PublicoNotificacao.Cliente,

                Titulo = "Teste concorrente",

                Mensagem = "Notificação fictícia.",

                CriadaEmUtc =
                    relogio.GetUtcNow().UtcDateTime
            };

            db.Notificacoes.Add(notificacao);

            db.NotificacoesUsuarios.Add(
                new NotificacaoUsuario
                {
                    Notificacao = notificacao,

                    UsuarioId = cliente.Id
                });

            var fila = CriarFila(db, relogio);

            Assert.Equal(
                1,
                await fila.Preparar(notificacao));

            await db.SaveChangesAsync();
        }

        var firebase = new FirebaseControlado();

        // Cada processador possui seu próprio contexto de banco de dados.

        await using var scope1 =
            host.App.Services.CreateAsyncScope();

        await using var scope2 =
            host.App.Services.CreateAsyncScope();

        var processador1 = CriarProcessador(
            scope1.ServiceProvider,
            firebase);

        var processador2 = CriarProcessador(
            scope2.ServiceProvider,
            firebase);

        var primeira = processador1.Processar();

        int segunda = -1;

        try
        {
            // Aguarda o primeiro processador assumir o envio e chegar ao Firebase.

            await firebase.Iniciado.Task.WaitAsync(
                TimeSpan.FromSeconds(10));

            // O primeiro envio permanece em curso.
            // O segundo processador não deve assumi-lo.

            segunda = await processador2.Processar();
        }
        finally
        {
            firebase.Liberar.TrySetResult(true);

            await primeira;
        }

        Assert.Equal(0, segunda);

        Assert.Equal(1, await primeira);

        Assert.Equal(1, firebase.Chamadas);

        await using var scopeFinal =
            host.App.Services.CreateAsyncScope();

        var dbFinal = scopeFinal.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var envio = await dbFinal.EnviosPush
            .AsNoTracking()
            .SingleAsync();

        Assert.Equal(
            SituacaoFilaPush.AceitoPeloFirebase,
            envio.Situacao);

        Assert.Equal(1, envio.Tentativas);

        Assert.Equal(
            "firebase-concorrencia",
            envio.MensagemFirebaseId);

        Assert.Null(envio.Concessao);

        Assert.Null(envio.ProcessandoAteUtc);

        Assert.NotNull(envio.FinalizadoEmUtc);
    }
}

