using KioraRestaurante.Models.Salao;
namespace KioraRestaurante.Services.Salao;
public static class TextosSalao
{
    public static string Preparo(SituacaoPreparo s)=>s switch{SituacaoPreparo.NaFila=>"Na fila",SituacaoPreparo.EmPreparo=>"Em preparo",SituacaoPreparo.Pronto=>"Pronto para retirada",SituacaoPreparo.Entregue=>"Entregue à mesa",_=>"Cancelado"};
    public static string Atendimento(SituacaoAtendimento s)=>s switch{SituacaoAtendimento.Aberto=>"Em atendimento",SituacaoAtendimento.AguardandoLimpeza=>"Aguardando limpeza",SituacaoAtendimento.Encerrado=>"Encerrado",_=>"Cancelado"};
    public static string Pagamento(MeioPagamentoPresencial p)=>p switch{MeioPagamentoPresencial.CartaoCredito=>"Cartão de crédito",MeioPagamentoPresencial.CartaoDebito=>"Cartão de débito",MeioPagamentoPresencial.Dinheiro=>"Dinheiro",MeioPagamentoPresencial.Pix=>"Pix",_=>"Outro"};
}
