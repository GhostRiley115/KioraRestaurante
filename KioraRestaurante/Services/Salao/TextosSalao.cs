using KioraRestaurante.Models.Salao;
namespace KioraRestaurante.Services.Salao;
public static class TextosSalao
{
    public static string ClasseMesa(string s)=>s switch{"Livre"=>"livre","Ocupada"=>"ocupada","Aguardando limpeza"=>"limpeza","Aguardando garçom"=>"chamada",_=>"inativa"};
    public static string Cargo(KioraRestaurante.Models.Enums.TipoUsuario c)=>c switch{KioraRestaurante.Models.Enums.TipoUsuario.Garcom=>"Garçom",KioraRestaurante.Models.Enums.TipoUsuario.Portaria=>"Recepção",KioraRestaurante.Models.Enums.TipoUsuario.Cozinha=>"Cozinha",KioraRestaurante.Models.Enums.TipoUsuario.Gerente=>"Gerente",KioraRestaurante.Models.Enums.TipoUsuario.Administrador=>"Administrador",_=>"Cliente"};
    public static string Preparo(SituacaoPreparo s)=>s switch{SituacaoPreparo.NaFila=>"Na fila",SituacaoPreparo.EmPreparo=>"Em preparo",SituacaoPreparo.Pronto=>"Pronto para retirada",SituacaoPreparo.Entregue=>"Entregue à mesa",_=>"Cancelado"};
    public static string Atendimento(SituacaoAtendimento s)=>s switch{SituacaoAtendimento.Aberto=>"Em atendimento",SituacaoAtendimento.AguardandoLimpeza=>"Aguardando limpeza",SituacaoAtendimento.Encerrado=>"Encerrado",_=>"Cancelado"};
    public static string Pagamento(MeioPagamentoPresencial p)=>p switch{MeioPagamentoPresencial.CartaoCredito=>"Cartão de crédito",MeioPagamentoPresencial.CartaoDebito=>"Cartão de débito",MeioPagamentoPresencial.Dinheiro=>"Dinheiro",MeioPagamentoPresencial.Pix=>"Pix",_=>"Outro"};
}
