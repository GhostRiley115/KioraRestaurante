using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KioraRestaurante.Services.Notificacoes;

// Executa automaticamente o processamento da fila persistente de notificações push.
public sealed class PushBackgroundService(
    IServiceScopeFactory scopeFactory,
    IEnvioPushService firebase,
    ILogger<PushBackgroundService> logger
) : BackgroundService
{
    private static readonly TimeSpan IntervaloNormal =
        TimeSpan.FromSeconds(15);

    private static readonly TimeSpan IntervaloComEnvios =
        TimeSpan.FromSeconds(2);

    private static readonly TimeSpan IntervaloAposFalha =
        TimeSpan.FromSeconds(30);

    // INICIAR PROCESSAMENTO AUTOMÁTICO
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        // Quando o Firebase estiver desativado, não acessa o banco nem inicia o processamento.

        if (!firebase.Habilitado)
        {
            logger.LogInformation(
                "Processamento automático de notificações " +
                "push desativado por configuração.");

            return;
        }

        logger.LogInformation(
            "Processamento automático de notificações " +
            "push iniciado.");

        try
        {
            await Task.Delay(
                IntervaloNormal,
                stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                var intervalo = IntervaloNormal;

                try
                {
                    // Cria um novo escopo a cada ciclo.
                    // O AppDbContext não permanece aberto durante toda a execução do serviço.

                    await using var scope =
                        scopeFactory.CreateAsyncScope();

                    var processador =
                        scope.ServiceProvider
                            .GetRequiredService<
                                ProcessadorPushService>();

                    var processados =
                        await processador.Processar(
                            stoppingToken);

                    if (processados > 0)
                    {
                        logger.LogDebug(
                            "Fila push: {Quantidade} " +
                            "registros processados.",
                            processados);

                        intervalo = IntervaloComEnvios;
                    }
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception erro)
                {
                    // Uma falha no processamento não deve interromper o funcionamento geral do site.
                    // Não registra tokens ou credenciais.

                    logger.LogError(
                        "Falha no ciclo de processamento " +
                        "da fila push. Tipo: {TipoErro}.",
                        erro.GetType().Name);

                    intervalo = IntervaloAposFalha;
                }

                await Task.Delay(
                    intervalo,
                    stoppingToken);
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            // Encerramento normal da aplicação.
        }
        finally
        {
            logger.LogInformation(
                "Processamento automático de " +
                "notificações push encerrado.");
        }
    }
}
