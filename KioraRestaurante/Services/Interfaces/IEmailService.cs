namespace KioraRestaurante.Services.Interfaces
{
    public interface IEmailService
    {
        Task EnviarCodigoRecuperacaoSenhaAsync(
            string emailDestino,
            string codigo);
    }
}
