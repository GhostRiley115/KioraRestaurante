using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KioraRestaurante.Data;
using KioraRestaurante.Models.Salao;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
namespace KioraRestaurante.Services.Salao;

public static class TransacaoSalao
{
    public static async Task<IDbContextTransaction> Iniciar(AppDbContext db,CancellationToken ct)
    {
        var isolamento=db.Database.ProviderName?.Contains("Sqlite",StringComparison.Ordinal)==true?IsolationLevel.Serializable:IsolationLevel.ReadCommitted;
        var tx=await db.Database.BeginTransactionAsync(isolamento,ct);
        try
        {
            // A mesma trava é utilizada por reserva, portaria, garçom e cozinha em todas as instâncias.
            await db.ControlesSala.Where(c=>c.Id==1).ExecuteUpdateAsync(s=>s.SetProperty(c=>c.Id,c=>c.Id),ct);
            if(!await db.ControlesSala.AnyAsync(c=>c.Id==1,ct))throw new ValidationException("A configuração do salão ainda não foi aplicada.");
            return tx;
        }
        catch { await tx.DisposeAsync();throw; }
    }
}
public record ResultadoComando(Guid? Id,Guid? AtendimentoId,string Descricao);
public class ComandoSalaoService(AppDbContext db,TimeProvider relogio,AcessoSalaoService acesso)
{
    public async Task<Guid?> Executar(int usuarioId,string nome,Guid chave,object dados,Func<Task<ResultadoComando>> acao,CancellationToken ct)
    {
        if(chave==Guid.Empty)throw new ValidationException("Atualize o formulário antes de enviar.");
        var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(dados,dados.GetType()))));
        await using var tx=await TransacaoSalao.Iniciar(db,ct);
        try
        {
            await acesso.ConferirAtual(usuarioId,ct);
            var anterior=await db.MovimentosSalao.AsNoTracking().SingleOrDefaultAsync(m=>m.Id==chave,ct);
            if(anterior!=null)
            {
                if(anterior.UsuarioId!=usuarioId||anterior.Acao!=nome||anterior.DadosHash!=hash)throw new ValidationException("Esta chave já foi usada para outra solicitação. Atualize os dados.");
                await tx.CommitAsync(ct);return anterior.ResultadoId;
            }
            var resultado=await acao();
            var responsavel=await db.Usuarios.Where(u=>u.Id==usuarioId).Select(u=>u.Nome).SingleAsync(ct);
            db.MovimentosSalao.Add(new(){Id=chave,UsuarioId=usuarioId,Acao=nome,DadosHash=hash,ResultadoId=resultado.Id,AtendimentoId=resultado.AtendimentoId,
                Descricao=resultado.Descricao,Responsavel=responsavel,CriadoEmUtc=relogio.GetUtcNow().UtcDateTime});
            await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return resultado.Id;
        }
        catch { await tx.RollbackAsync(ct);db.ChangeTracker.Clear();throw; }
    }
}
