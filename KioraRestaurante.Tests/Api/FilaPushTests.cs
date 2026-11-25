using System.Security.Cryptography;
using System.Text;
using KioraRestaurante.Data;
using KioraRestaurante.Models.Notificacoes;
using KioraRestaurante.Options;
using KioraRestaurante.Services.Notificacoes;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using KioraRestaurante.Models;

namespace KioraRestaurante.Tests;

// Testes da fila persistente de notificações push.
// Utiliza banco temporário e Firebase simulado.
// Não realiza comunicação externa.
public sealed class FilaPushTests
{
    private const string Token =
        "TOKEN_FCM_FICTICIO_FILA_KIORA_123456";

    private static readonly DateTimeOffset Agora =
        new(2026, 11, 17, 15, 0, 0, TimeSpan.Zero);

    // SIMULADOR DO FIREBASE
    private sealed class FirebaseSimulado
        : IEnvioPushService
    {
        public bool Habilitado { get; set; } = true;
        public int Chamadas { get; private set; }
        public string? UltimoToken { get; private set; }
        public Guid? UltimaNotificacao { get; private set; }
        public ResultadoEnvioPush Resultado { get; set; } =
            new(
                SituacaoEnvioPush.AceitoPeloFirebase,
                MensagemId: "firebase-teste-001");
        public Task<ResultadoEnvioPush> EnviarAsync(
            string tokenFcm,
            Guid notificacaoId,
            CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();

            Chamadas++;

            UltimoToken = tokenFcm;

            UltimaNotificacao = notificacaoId;

            return Task.FromResult(Resultado);
        }
    }

    // RELÓGIO CONTROLADO
    private sealed class RelogioTeste : TimeProvider
    {
        public DateTimeOffset Atual { get; set; } = Agora;
        public override DateTimeOffset GetUtcNow()
        {
            return Atual;
        }
    }

    // MÉTODOS AUXILIARES
    private static FilaPushService CriarFila(
        AppDbContext db,
        TimeProvider relogio,
        bool ativado = true)
    {
        var options =
            Microsoft.Extensions.Options.Options.Create(
                new FirebaseOptions
                {
                    Ativado = ativado,

                    ProjetoId = "kiora-teste"
                });

        return new FilaPushService(
            db,
            relogio,
            options);
    }
    private static ProcessadorPushService CriarProcessador(
        IServiceProvider services,
        FirebaseSimulado firebase,
        RelogioTeste relogio)
    {
        return new ProcessadorPushService(
            services.GetRequiredService<AppDbContext>(),
            firebase,
            services.GetRequiredService<
                IDataProtectionProvider>(),
            relogio,
            NullLogger<ProcessadorPushService>.Instance);
    }
    private static async Task<Guid> PrepararDados(
        HostMobile host,
        RelogioTeste relogio,
        bool ativado = true,
        bool dispositivoAtivo = true,
        bool sessaoValida = true,
        bool notificaçãoExpirada = false,
        bool destinatarioAutorizado = true)
    {
        await using var scope =
            host.App.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var usuario =
            await db.Usuarios.SingleAsync(
                u => u.Email == "cliente@mobile.test");

        var outroUsuario =
            await db.Usuarios.SingleAsync(
                u => u.Email == "outro@mobile.test");

        var protecao =
            scope.ServiceProvider
                .GetRequiredService<
                    IDataProtectionProvider>();

        var protetor =
            protecao.CreateProtector(
                "Kiora.Push.FcmToken.v1");

        var hash = Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(Token)));

        var dispositivo = new DispositivoPush
        {
            TokenHash = hash,

            TokenFcm = protetor.Protect(Token),

            UsuarioId = usuario.Id,

            VersaoSessaoUsuario =
                sessaoValida
                    ? usuario.VersaoSessao
                    : Guid.NewGuid(),

            Ativo = dispositivoAtivo,

            Versao = Guid.NewGuid(),

            CriadoEmUtc =
                relogio.Atual.UtcDateTime,

            AtualizadoEmUtc =
                relogio.Atual.UtcDateTime
        };

        db.DispositivosPush.Add(dispositivo);

        await db.SaveChangesAsync();

        var notificacao = new Notificacao
        {
            Id = Guid.NewGuid(),

            Tipo = TipoNotificacao.PedidoRecebido,

            Publico = PublicoNotificacao.Cliente,

            Titulo = "Notificação de teste",

            Mensagem = "Mensagem fictícia.",

            CriadaEmUtc = notificaçãoExpirada
                ? relogio.Atual.UtcDateTime.AddHours(-25)
                : relogio.Atual.UtcDateTime
        };

        db.Notificacoes.Add(notificacao);

        db.NotificacoesUsuarios.Add(
            new NotificacaoUsuario
            {
                Notificacao = notificacao,

                UsuarioId = destinatarioAutorizado
                    ? usuario.Id
                    : outroUsuario.Id
            });

        var fila = CriarFila(
            db,
            relogio,
            ativado);

        var quantidade =
            await fila.Preparar(notificacao);

        // Uma segunda preparação não pode duplicar um envio pendente no mesmo contexto.

        var repeticao =
            await fila.Preparar(notificacao);

        Assert.Equal(0, repeticao);

        var esperado =
            ativado &&
            dispositivoAtivo &&
            sessaoValida &&
            !notificaçãoExpirada &&
            destinatarioAutorizado
                ? 1
                : 0;

        Assert.Equal(
            esperado,
            quantidade);

        await db.SaveChangesAsync();

        return notificacao.Id;
    }
    private static async Task<EnvioPush> LerEnvio(
        HostMobile host)
    {
        await using var scope =
            host.App.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        return await db.EnviosPush
            .AsNoTracking()
            .SingleAsync();
    }
    private static async Task<int> Processar(
        HostMobile host,
        FirebaseSimulado firebase,
        RelogioTeste relogio)
    {
        await using var scope =
            host.App.Services.CreateAsyncScope();

        var processador = CriarProcessador(
            scope.ServiceProvider,
            firebase,
            relogio);

        return await processador.Processar();
    }

    // TESTE 1 — CRIAÇÃO SEM DUPLICIDADE

    [Fact]
    public async Task FilaCriaSomenteUmEnvioPorDispositivo()
    {
        await using var host =
            await HostMobile.Criar();

        var relogio = new RelogioTeste();

        var notificacaoId =
            await PrepararDados(host, relogio);

        var envio = await LerEnvio(host);

        Assert.Equal(
            notificacaoId,
            envio.NotificacaoId);

        Assert.Equal(
            SituacaoFilaPush.Pendente,
            envio.Situacao);

        Assert.Equal(0, envio.Tentativas);

        Assert.NotNull(envio.UsuarioId);

        Assert.NotNull(envio.VersaoSessaoUsuario);

        Assert.NotEqual(
            Guid.Empty,
            envio.VersaoDispositivo);

        Assert.Equal(
            Agora.UtcDateTime.AddHours(24),
            envio.ExpiraEmUtc);

        Assert.Null(envio.Concessao);

        Assert.Null(envio.FinalizadoEmUtc);
    }

    // TESTE 2 — BLOQUEIOS NA INCLUSÃO

    [Theory]
    [InlineData(false, true, true, false, true)]
    [InlineData(true, false, true, false, true)]
    [InlineData(true, true, false, false, true)]
    [InlineData(true, true, true, true, true)]
    [InlineData(true, true, true, false, false)]
    public async Task FilaNaoIncluiDispositivosInelegiveis(
        bool ativado,
        bool dispositivoAtivo,
        bool sessaoValida,
        bool notificacaoExpirada,
        bool destinatarioAutorizado)
    {
        await using var host =
            await HostMobile.Criar();

        var relogio = new RelogioTeste();

        await PrepararDados(
            host,
            relogio,
            ativado,
            dispositivoAtivo,
            sessaoValida,
            notificacaoExpirada,
            destinatarioAutorizado);

        await using var scope =
            host.App.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        Assert.Equal(
            0,
            await db.EnviosPush.CountAsync());

        // A notificação interna continua existindo.

        Assert.Equal(
            1,
            await db.Notificacoes.CountAsync());

        Assert.Equal(
            1,
            await db.NotificacoesUsuarios.CountAsync());
    }

    // TESTE 3 — ENVIO ACEITO PELO FIREBASE

    [Fact]
    public async Task EnvioAceitoNaoEProcessadoNovamente()
    {
        await using var host =
            await HostMobile.Criar();

        var relogio = new RelogioTeste();

        var notificacaoId =
            await PrepararDados(host, relogio);

        var firebase = new FirebaseSimulado();

        Assert.Equal(
            1,
            await Processar(
                host,
                firebase,
                relogio));

        var envio = await LerEnvio(host);

        Assert.Equal(
            SituacaoFilaPush.AceitoPeloFirebase,
            envio.Situacao);

        Assert.Equal(1, envio.Tentativas);

        Assert.Equal(1, firebase.Chamadas);

        Assert.Equal(
            Token,
            firebase.UltimoToken);

        Assert.Equal<Guid?>(
            notificacaoId,
            firebase.UltimaNotificacao);

        Assert.Equal(
            "firebase-teste-001",
            envio.MensagemFirebaseId);

        Assert.NotNull(envio.FinalizadoEmUtc);

        Assert.Null(envio.Concessao);

        Assert.Null(envio.ProcessandoAteUtc);

        // O envio finalizado não deve ser repetido.

        Assert.Equal(
            0,
            await Processar(
                host,
                firebase,
                relogio));

        Assert.Equal(1, firebase.Chamadas);
    }

    // TESTE 4 — FALHA TEMPORÁRIA E NOVA TENTATIVA

    [Fact]
    public async Task FalhaTemporariaAgendaNovaTentativa()
    {
        await using var host =
            await HostMobile.Criar();

        var relogio = new RelogioTeste();

        await PrepararDados(host, relogio);

        var firebase = new FirebaseSimulado
        {
            Resultado = new ResultadoEnvioPush(
                SituacaoEnvioPush.FalhaTemporaria,
                CodigoErro: "UNAVAILABLE")
        };

        await Processar(
            host,
            firebase,
            relogio);

        var pendente = await LerEnvio(host);

        Assert.Equal(
            SituacaoFilaPush.Pendente,
            pendente.Situacao);

        Assert.Equal(1, pendente.Tentativas);

        Assert.Equal(
            "UNAVAILABLE",
            pendente.UltimoCodigoErro);

        Assert.InRange(
            pendente.ProximaTentativaEmUtc,
            Agora.UtcDateTime.AddMinutes(1),
            Agora.UtcDateTime
                .AddMinutes(1)
                .AddSeconds(15));

        Assert.Null(pendente.FinalizadoEmUtc);

        // Ainda não chegou o momento da nova tentativa.

        Assert.Equal(
            0,
            await Processar(
                host,
                firebase,
                relogio));

        Assert.Equal(1, firebase.Chamadas);

        // Avança o relógio sem espera real.

        relogio.Atual = Agora.AddMinutes(2);

        firebase.Resultado = new ResultadoEnvioPush(
            SituacaoEnvioPush.AceitoPeloFirebase,
            MensagemId: "firebase-reenvio");

        await Processar(
            host,
            firebase,
            relogio);

        var concluido = await LerEnvio(host);

        Assert.Equal(
            SituacaoFilaPush.AceitoPeloFirebase,
            concluido.Situacao);

        Assert.Equal(2, concluido.Tentativas);

        Assert.Equal(2, firebase.Chamadas);

        Assert.Null(concluido.UltimoCodigoErro);
    }

    // TESTE 5 — LIMITE DE TENTATIVAS

    [Fact]
    public async Task SeisFalhasEncerramProcessamento()
    {
        await using var host =
            await HostMobile.Criar();

        var relogio = new RelogioTeste();

        await PrepararDados(host, relogio);

        var firebase = new FirebaseSimulado
        {
            Resultado = new ResultadoEnvioPush(
                SituacaoEnvioPush.FalhaTemporaria,
                CodigoErro: "UNAVAILABLE")
        };

        for (var i = 0; i < 6; i++)
        {
            await Processar(
                host,
                firebase,
                relogio);

            var atual = await LerEnvio(host);

            Assert.Equal(
                i + 1,
                atual.Tentativas);

            relogio.Atual = new DateTimeOffset(
                DateTime.SpecifyKind(
                    atual.ProximaTentativaEmUtc,
                    DateTimeKind.Utc))
                .AddSeconds(1);
        }

        var envio = await LerEnvio(host);

        Assert.Equal(
            SituacaoFilaPush.FalhaDefinitiva,
            envio.Situacao);

        Assert.Equal(6, envio.Tentativas);

        Assert.Equal(6, firebase.Chamadas);

        Assert.NotNull(envio.FinalizadoEmUtc);

        Assert.Equal(
            0,
            await Processar(
                host,
                firebase,
                relogio));
    }

    // TESTE 6 — TOKEN INVÁLIDO

    [Fact]
    public async Task TokenInvalidoDesativaDispositivo()
    {
        await using var host =
            await HostMobile.Criar();

        var relogio = new RelogioTeste();

        await PrepararDados(host, relogio);

        var firebase = new FirebaseSimulado
        {
            Resultado = new ResultadoEnvioPush(
                SituacaoEnvioPush.TokenInvalido,
                CodigoErro: "UNREGISTERED")
        };

        await Processar(
            host,
            firebase,
            relogio);

        var envio = await LerEnvio(host);

        Assert.Equal(
            SituacaoFilaPush.TokenInvalido,
            envio.Situacao);

        Assert.Equal(1, envio.Tentativas);

        await using var scope =
            host.App.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var dispositivo =
            await db.DispositivosPush.SingleAsync();

        Assert.False(dispositivo.Ativo);

        Assert.NotNull(
            dispositivo.DesativadoEmUtc);

        Assert.NotEqual(
            envio.VersaoDispositivo,
            dispositivo.Versao);
    }

    // TESTE 7 — SEGURANÇA ANTES DO ENVIO

    [Theory]
    [InlineData("sessao")]
    [InlineData("dispositivo")]
    [InlineData("versao")]
    [InlineData("conta")]
    [InlineData("destinatario")]
    public async Task AlteracaoDeSegurancaDescartaEnvio(
        string alteracao)
    {
        await using var host =
            await HostMobile.Criar();

        var relogio = new RelogioTeste();

        await PrepararDados(host, relogio);

        await using (
            var scope =
                host.App.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<AppDbContext>();

            var dispositivo =
                await db.DispositivosPush.SingleAsync();

            var usuario =
                await db.Usuarios.SingleAsync(
                    u => u.Id ==
                        dispositivo.UsuarioId);

            switch (alteracao)
            {
                case "sessao":

                    usuario.VersaoSessao =
                        Guid.NewGuid();

                    break;

                case "dispositivo":

                    dispositivo.Ativo = false;

                    dispositivo.Versao =
                        Guid.NewGuid();

                    break;

                case "versao":

                    dispositivo.Versao =
                        Guid.NewGuid();

                    break;

                case "conta":

                    usuario.Ativo = false;

                    break;

                case "destinatario":

                    var destinatario =
                        await db.NotificacoesUsuarios
                            .SingleAsync();

                    db.NotificacoesUsuarios.Remove(
                        destinatario);

                    break;
            }

            await db.SaveChangesAsync();
        }

        var firebase = new FirebaseSimulado();

        await Processar(
            host,
            firebase,
            relogio);

        var envio = await LerEnvio(host);

        Assert.Equal(
            SituacaoFilaPush.Descartado,
            envio.Situacao);

        Assert.Equal(
            "DESTINATARIO_NAO_AUTORIZADO",
            envio.UltimoCodigoErro);

        Assert.Equal(0, firebase.Chamadas);

        Assert.Equal(0, envio.Tentativas);
    }

    // TESTE 8 — EXPIRAÇÃO

    [Fact]
    public async Task NotificacaoExpiradaNaoEEnviada()
    {
        await using var host =
            await HostMobile.Criar();

        var relogio = new RelogioTeste();

        await PrepararDados(host, relogio);

        relogio.Atual = Agora.AddHours(25);

        var firebase = new FirebaseSimulado();

        await Processar(
            host,
            firebase,
            relogio);

        var envio = await LerEnvio(host);

        Assert.Equal(
            SituacaoFilaPush.Expirado,
            envio.Situacao);

        Assert.Equal(0, firebase.Chamadas);

        Assert.NotNull(envio.FinalizadoEmUtc);
    }

    // TESTE 9 — ERRO DE CONFIGURAÇÃO

    [Fact]
    public async Task ErroConfiguracaoNaoConsomeTentativas()
    {
        await using var host =
            await HostMobile.Criar();

        var relogio = new RelogioTeste();

        await PrepararDados(host, relogio);

        var firebase = new FirebaseSimulado
        {
            Resultado = new ResultadoEnvioPush(
                SituacaoEnvioPush.ErroConfiguracao,
                CodigoErro: "FIREBASE_CONFIGURACAO")
        };

        await Processar(
            host,
            firebase,
            relogio);

        var envio = await LerEnvio(host);

        Assert.Equal(
            SituacaoFilaPush.Pendente,
            envio.Situacao);

        Assert.Equal(0, envio.Tentativas);

        Assert.Equal(
            Agora.UtcDateTime.AddHours(1),
            envio.ProximaTentativaEmUtc);

        Assert.Null(envio.Concessao);

        Assert.Equal(1, firebase.Chamadas);
    }

    // TESTE 10 — FIREBASE DESATIVADO

    [Fact]
    public async Task FirebaseDesativadoNaoProcessaFila()
    {
        await using var host =
            await HostMobile.Criar();

        var relogio = new RelogioTeste();

        await PrepararDados(host, relogio);

        var firebase = new FirebaseSimulado
        {
            Habilitado = false
        };

        var original = await LerEnvio(host);

        Assert.Equal(
            0,
            await Processar(
                host,
                firebase,
                relogio));

        var atual = await LerEnvio(host);

        Assert.Equal(
            original.Versao,
            atual.Versao);

        Assert.Equal(
            SituacaoFilaPush.Pendente,
            atual.Situacao);

        Assert.Equal(0, firebase.Chamadas);
    }

    // TESTE 11 — RECUPERAR CONCESSÃO EXPIRADA

    [Fact]
    public async Task ProcessamentoInterrompidoPodeSerRetomado()
    {
        await using var host =
            await HostMobile.Criar();

        var relogio = new RelogioTeste();

        await PrepararDados(host, relogio);

        await using (
            var scope =
                host.App.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<AppDbContext>();

            var envio =
                await db.EnviosPush.SingleAsync();

            envio.Concessao = Guid.NewGuid();

            envio.ProcessandoAteUtc =
                Agora.UtcDateTime.AddMinutes(3);

            envio.Versao = Guid.NewGuid();

            await db.SaveChangesAsync();
        }

        var firebase = new FirebaseSimulado();

        // Outra instância ainda possui a concessão.

        Assert.Equal(
            0,
            await Processar(
                host,
                firebase,
                relogio));

        Assert.Equal(0, firebase.Chamadas);

        // Simula uma interrupção do servidor e o vencimento da concessão.

        relogio.Atual = Agora.AddMinutes(4);

        Assert.Equal(
            1,
            await Processar(
                host,
                firebase,
                relogio));

        var recuperado = await LerEnvio(host);

        Assert.Equal(
            SituacaoFilaPush.AceitoPeloFirebase,
            recuperado.Situacao);

        Assert.Equal(1, firebase.Chamadas);

        Assert.Null(recuperado.Concessao);

        Assert.Null(recuperado.ProcessandoAteUtc);
    }

    // TESTE 12 — FALHA DE DESCRIPTOGRAFIA

    [Fact]
    public async Task FalhaDeProtecaoNaoDesativaDispositivo()
    {
        await using var host =
            await HostMobile.Criar();

        var relogio = new RelogioTeste();

        await PrepararDados(host, relogio);

        await using (
            var scope =
                host.App.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<AppDbContext>();

            var dispositivo =
                await db.DispositivosPush.SingleAsync();

            dispositivo.TokenFcm =
                "TOKEN_PROTEGIDO_INDECRIPTAVEL";

            await db.SaveChangesAsync();
        }

        var firebase = new FirebaseSimulado();

        await Processar(
            host,
            firebase,
            relogio);

        var envio = await LerEnvio(host);

        Assert.Equal(
            SituacaoFilaPush.Pendente,
            envio.Situacao);

        Assert.Equal(
            "TOKEN_PROTECAO_INDISPONIVEL",
            envio.UltimoCodigoErro);

        Assert.Equal(0, envio.Tentativas);

        Assert.Equal(0, firebase.Chamadas);

        await using var scopeFinal =
            host.App.Services.CreateAsyncScope();

        var dbFinal = scopeFinal.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var atual =
            await dbFinal.DispositivosPush.SingleAsync();

        Assert.True(atual.Ativo);
    }


    [Fact]
    public async Task VisitanteRecebePushDaPropriaReserva()
    {
        await using var host = await HostMobile.Criar();
        var relogio = new RelogioTeste();

        await using var scope =
            host.App.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var protecao = scope.ServiceProvider
            .GetRequiredService<IDataProtectionProvider>();

        var protetor = protecao.CreateProtector(
            "Kiora.Push.FcmToken.v1");

        var visitanteHash = Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes("visitante-teste")));

        // Reserva feita por visitante sem conta.
        var reserva = new Reserva
        {
            UsuarioId = null,
            VisitanteHash = visitanteHash,
            Nome = "Visitante Teste",
            Telefone = "11999999999",
            Pessoas = 2,
            Tipo = TipoReserva.Agendada,
            Situacao = SituacaoReserva.Confirmada,
            ChaveSolicitacao = Guid.NewGuid(),
            CriadaEmUtc = relogio.Atual.UtcDateTime,
            ExpiraEmUtc = relogio.Atual.UtcDateTime.AddHours(5)
        };

        db.Reservas.Add(reserva);
        await db.SaveChangesAsync();

        // Dispositivo vinculado ao mesmo visitante.
        var dispositivo = new DispositivoPush
        {
            TokenHash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(Token))),
            TokenFcm = protetor.Protect(Token),
            UsuarioId = null,
            VisitanteHash = visitanteHash,
            VersaoSessaoUsuario = null,
            Ativo = true,
            Versao = Guid.NewGuid()
        };

        db.DispositivosPush.Add(dispositivo);
        await db.SaveChangesAsync();

        var notificacao = new Notificacao
        {
            Id = Guid.NewGuid(),
            Tipo = TipoNotificacao.ReservaAtualizada,
            Publico = PublicoNotificacao.Cliente,
            ReservaId = reserva.Id,
            Titulo = "Reserva atualizada",
            Mensagem = "Confira sua reserva.",
            CriadaEmUtc = relogio.Atual.UtcDateTime
        };

        db.Notificacoes.Add(notificacao);

        var fila = CriarFila(db, relogio);

        Assert.Equal(1, await fila.Preparar(notificacao));
        Assert.Equal(0, await fila.Preparar(notificacao));

        await db.SaveChangesAsync();

        var envio = await db.EnviosPush
            .AsNoTracking()
            .SingleAsync();

        Assert.Null(envio.UsuarioId);
        Assert.Equal(visitanteHash, envio.VisitanteHash);
        Assert.Equal(dispositivo.Id, envio.DispositivoId);

        // Simula o processamento e envio.
        var firebase = new FirebaseSimulado();

        Assert.Equal(
            1,
            await Processar(host, firebase, relogio));

        Assert.Equal(1, firebase.Chamadas);
        Assert.Equal(Token, firebase.UltimoToken);

        var envioFinal = await LerEnvio(host);

        Assert.Equal(
            SituacaoFilaPush.AceitoPeloFirebase,
            envioFinal.Situacao);
    }

    [Fact]
    public async Task VisitanteNaoRecebePushAposTrocaDeIdentidade()
    {
        await using var host = await HostMobile.Criar();
        var relogio = new RelogioTeste();

        await using var scope =
            host.App.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var protecao = scope.ServiceProvider
            .GetRequiredService<IDataProtectionProvider>();

        var protetor = protecao.CreateProtector(
            "Kiora.Push.FcmToken.v1");

        var visitanteHash = Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes("visitante-teste")));

        // Reserva feita por visitante sem conta.
        var reserva = new Reserva
        {
            UsuarioId = null,
            VisitanteHash = visitanteHash,
            Nome = "Visitante Teste",
            Telefone = "11999999999",
            Pessoas = 2,
            Tipo = TipoReserva.Agendada,
            Situacao = SituacaoReserva.Confirmada,
            ChaveSolicitacao = Guid.NewGuid(),
            CriadaEmUtc = relogio.Atual.UtcDateTime,
            ExpiraEmUtc = relogio.Atual.UtcDateTime.AddHours(5)
        };

        db.Reservas.Add(reserva);
        await db.SaveChangesAsync();

        // Dispositivo vinculado ao mesmo visitante.
        var dispositivo = new DispositivoPush
        {
            TokenHash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(Token))),
            TokenFcm = protetor.Protect(Token),
            UsuarioId = null,
            VisitanteHash = visitanteHash,
            VersaoSessaoUsuario = null,
            Ativo = true,
            Versao = Guid.NewGuid()
        };

        db.DispositivosPush.Add(dispositivo);
        await db.SaveChangesAsync();

        var notificacao = new Notificacao
        {
            Id = Guid.NewGuid(),
            Tipo = TipoNotificacao.ReservaAtualizada,
            Publico = PublicoNotificacao.Cliente,
            ReservaId = reserva.Id,
            Titulo = "Reserva atualizada",
            Mensagem = "Confira sua reserva.",
            CriadaEmUtc = relogio.Atual.UtcDateTime
        };

        db.Notificacoes.Add(notificacao);

        var fila = CriarFila(db, relogio);

        Assert.Equal(1, await fila.Preparar(notificacao));
        Assert.Equal(0, await fila.Preparar(notificacao));

        await db.SaveChangesAsync();

        // Simula a troca de identidade do dispositivo
        // depois que a notificação entrou na fila.
        dispositivo.VisitanteHash = Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes("outro-visitante")));

        await db.SaveChangesAsync();

        var envio = await db.EnviosPush
            .AsNoTracking()
            .SingleAsync();

        Assert.Null(envio.UsuarioId);
        Assert.Equal(visitanteHash, envio.VisitanteHash);
        Assert.Equal(dispositivo.Id, envio.DispositivoId);


        var firebase = new FirebaseSimulado();

        Assert.Equal(
            1,
            await Processar(host, firebase, relogio));

        // O Firebase não deve receber nenhuma notificação.
        Assert.Equal(0, firebase.Chamadas);

        // O envio deve ser descartado por segurança.
        var envioFinal = await LerEnvio(host);

        Assert.Equal(
            SituacaoFilaPush.Descartado,
            envioFinal.Situacao);

        Assert.Equal(
            "DESTINATARIO_NAO_AUTORIZADO",
            envioFinal.UltimoCodigoErro);

    }

}
