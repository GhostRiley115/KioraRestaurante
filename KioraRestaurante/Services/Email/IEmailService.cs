namespace KioraRestaurante.Services.Email
{
    public interface IEmailService
    {
        Task<bool> EnviarCodigoRecuperacaoSenhaAsync(
            string emailDestino,
            string codigo);
    }
}
