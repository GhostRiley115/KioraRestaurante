namespace KioraRestaurante.Services.Interfaces
{
    public interface IEmailService
    {
        Task<bool> EnviarCodigoRecuperacaoSenhaAsync(
            string emailDestino,
            string codigo);
    }
}
