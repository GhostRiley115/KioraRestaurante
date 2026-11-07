using System.ComponentModel.DataAnnotations;
using KioraRestaurante.Data;
using KioraRestaurante.DTOs.Salao;
using KioraRestaurante.Models;
using KioraRestaurante.Models.Enums;
using KioraRestaurante.Models.Salao;
using KioraRestaurante.Services.Reservas;
using Microsoft.EntityFrameworkCore;
namespace KioraRestaurante.Services.Salao;

public partial class ComandaService(AppDbContext db,AcessoSalaoService acesso,ComandoSalaoService comandos,HorariosReserva horarios,TimeProvider relogio,AvisosSalaoService avisos)
{
    private DateTime Agora=>relogio.GetUtcNow().UtcDateTime;
    private static void Validar(object dto)=>Validator.ValidateObject(dto,new(dto),true);
    private static void ConferirVersao(Guid atual,Guid enviada)
    { if(atual!=enviada)throw new ValidationException("Os dados mudaram em outro dispositivo. Atualize a tela antes de continuar."); }
    private static void SomenteAberta(AtendimentoMesa a)
    { if(a.Situacao!=SituacaoAtendimento.Aberto)throw new ValidationException("Esta comanda já foi fechada. O histórico permanece disponível."); }
    private static void Dono(Usuario usuario,AtendimentoMesa a)
    { if(!AcessoSalaoService.Gestor(usuario.Tipo)&&a.GarcomId!=usuario.Id)throw new UnauthorizedAccessException(); }
    private async Task<AtendimentoMesa> Buscar(Guid id,CancellationToken ct) => await db.AtendimentosMesa.Include(a=>a.Itens).Include(a=>a.Garcom)
        .Include(a=>a.Reserva).SingleOrDefaultAsync(a=>a.Id==id,ct) ?? throw new KeyNotFoundException();
    public static decimal Subtotal(AtendimentoMesa a)=>a.Itens.Where(i=>i.Situacao!=SituacaoPreparo.Cancelado).Sum(i=>i.Subtotal);
    public static decimal Desconto(AtendimentoMesa a)
    {
        var subtotal=Subtotal(a);
        return Math.Min(subtotal,decimal.Round(a.TipoDesconto==TipoDescontoPresencial.Percentual?subtotal*a.ValorDescontoInformado/100m:a.ValorDescontoInformado,2,MidpointRounding.AwayFromZero));
    }
    private static void Alterada(AtendimentoMesa a)=>a.Versao=Guid.NewGuid();
    private void SalaAberta()
    {
        var local=HorariosReserva.Local(Agora);var minutos=local.Hour*60+local.Minute;
        if(minutos<horarios.Regras.AberturaMinutos||minutos>=horarios.Regras.FechamentoMinutos)throw new ValidationException("O restaurante está fora do horário de novas acomodações.");
    }
    private async Task<Produto> ProdutoVendaAtual(int id,CancellationToken ct)
    {
        var produto=await db.Produtos.Include(p=>p.Categoria).Include(p=>p.Componentes).ThenInclude(c=>c.Produto).ThenInclude(p=>p.Categoria)
            .SingleOrDefaultAsync(p=>p.Id==id,ct);
        if(produto==null||!ProdutoVenda.EstaDisponivel(produto))throw new ValidationException("O produto está indisponível. Atualize o cardápio.");
        return produto;
    }
    private ItemComanda Item(AtendimentoMesa a,Produto p,ItemComandaNovoDTO dto,Guid rodada)
    {
        Validar(dto);
        return new(){AtendimentoId=a.Id,ProdutoId=p.Id,RodadaId=rodada,NomeProduto=p.Nome,Quantidade=dto.Quantidade,
            PrecoUnitario=PrecoProduto.Calcular(p.Preco,p.DescontoPercentual),Observacao=string.IsNullOrWhiteSpace(dto.Observacao)?null:dto.Observacao.Trim(),
            Composicao=ProdutoVenda.DescreverComposicao(p),ImagemUrl=p.Imagem,ImagemPublicId=p.ImagemPublicId,EnviadoEmUtc=Agora};
    }
    private static void ConferirTotal(AtendimentoMesa a)
    { if(Subtotal(a)>99999999.99m)throw new ValidationException("O total da comanda ultrapassa o limite permitido."); }

    public async Task<Guid> Acomodar(int usuarioId,AcomodarMesaDTO dto,CancellationToken ct=default)
    {
        var u=await acesso.Exigir(usuarioId,ct,TipoUsuario.Garcom,TipoUsuario.Gerente);Validar(dto);
        return (await comandos.Executar(usuarioId,"acomodar",dto.ChaveSolicitacao,dto,async()=>
        {
            SalaAberta();
            var mesa=await db.MesasRestaurante.SingleOrDefaultAsync(m=>m.Id==dto.MesaId&&m.Ativa,ct)??throw new ValidationException("Mesa indisponível.");
            ConferirVersao(mesa.Versao,dto.Versao);
            if(await db.AtendimentosMesa.AnyAsync(a=>a.MesaOcupadaId==mesa.Id,ct))throw new ValidationException("A mesa está ocupada ou aguardando limpeza.");
            Reserva? reserva=null;
            if(dto.ReservaCodigo.HasValue)
            {
                reserva=await db.Reservas.SingleOrDefaultAsync(r=>r.Codigo==dto.ReservaCodigo,ct)??throw new KeyNotFoundException();
                if(reserva.Situacao is not (SituacaoReserva.Aguardando or SituacaoReserva.Chamada)||reserva.ChegadaEmUtc==null)
                    throw new ValidationException("A portaria precisa registrar a chegada antes de acomodar o grupo.");
                if(reserva.Situacao==SituacaoReserva.Chamada&&reserva.ExpiraEmUtc<=Agora)throw new ValidationException("A chamada expirou. A portaria precisa chamar novamente.");
                if(reserva.Situacao==SituacaoReserva.Chamada&&reserva.MesaId!=mesa.Id)throw new ValidationException("O grupo foi chamado para outra mesa.");
            }
            var ignorarReservaId=reserva?.Id??0;
            if(await db.Reservas.AnyAsync(r=>r.MesaId==mesa.Id&&r.Situacao==SituacaoReserva.Chamada&&r.ExpiraEmUtc>Agora&&r.Id!=ignorarReservaId,ct))
                throw new ValidationException("Esta mesa está aguardando a acomodação de outro grupo chamado pela portaria.");
            var pessoas=reserva?.Pessoas??dto.Pessoas;
            if(pessoas>mesa.Capacidade)throw new ValidationException("A mesa não comporta o grupo.");
            var garcomId=dto.GarcomId??usuarioId;
            if(garcomId!=usuarioId&&!AcessoSalaoService.Gestor(u.Tipo))throw new UnauthorizedAccessException();
            var garcom=await acesso.Exigir(garcomId,ct,TipoUsuario.Garcom,TipoUsuario.Gerente);
            var atendimento=new AtendimentoMesa{MesaId=mesa.Id,NomeMesa=mesa.Nome,MesaOcupadaId=mesa.Id,GarcomId=garcom.Id,
                NomeGrupo=reserva?.Nome??(string.IsNullOrWhiteSpace(dto.NomeGrupo)?"Cliente presencial":dto.NomeGrupo.Trim()),Pessoas=pessoas,ReservaId=reserva?.Id,AbertoEmUtc=Agora};
            db.AtendimentosMesa.Add(atendimento);mesa.Versao=Guid.NewGuid();
            if(reserva!=null)
            {
                reserva.Situacao=SituacaoReserva.EmAtendimento;reserva.MesaId=mesa.Id;reserva.FimUtc=null;reserva.Versao=Guid.NewGuid();
                reserva.Eventos.Add(new(){AdministradorId=usuarioId,DataUtc=Agora,Descricao=$"Grupo acomodado por {u.Nome} na {mesa.Nome}. Sem limite de permanência."});
            }
            return new(atendimento.Id,atendimento.Id,$"Comanda aberta na {mesa.Nome}, {pessoas} pessoa(s); responsável: {garcom.Nome}.");
        },ct))!.Value;
    }

    public async Task Adicionar(int usuarioId,AdicionarRodadaDTO dto,CancellationToken ct=default)
    {
        var u=await acesso.Exigir(usuarioId,ct,TipoUsuario.Garcom,TipoUsuario.Gerente);Validar(dto);
        if(dto.Itens==null||dto.Itens.Count is <1 or >30)throw new ValidationException("Envie entre 1 e 30 itens por rodada.");
        foreach(var item in dto.Itens)Validar(item);
        await comandos.Executar(usuarioId,"adicionar-rodada",dto.ChaveSolicitacao,dto,async()=>
        {
            var a=await Buscar(dto.AtendimentoId,ct);Dono(u,a);SomenteAberta(a);ConferirVersao(a.Versao,dto.Versao);
            foreach(var novo in dto.Itens)
            {
                var item=Item(a,await ProdutoVendaAtual(novo.ProdutoId,ct),novo,dto.ChaveSolicitacao);
                a.Itens.Add(item);db.ItensComanda.Add(item);
            }
            ConferirTotal(a);Alterada(a);
            return new(a.Id,a.Id,$"Rodada enviada à cozinha: {string.Join("; ",dto.Itens.Select(i=>$"produto #{i.ProdutoId}, {i.Quantidade} un."))}");
        },ct);
    }

    public async Task Corrigir(int usuarioId,CorrigirItemDTO dto,CancellationToken ct=default)
    {
        var u=await acesso.Exigir(usuarioId,ct,TipoUsuario.Garcom,TipoUsuario.Gerente);Validar(dto);
        await comandos.Executar(usuarioId,"corrigir-item",dto.ChaveSolicitacao,dto,async()=>
        {
            var a=await Buscar(dto.AtendimentoId,ct);Dono(u,a);SomenteAberta(a);ConferirVersao(a.Versao,dto.Versao);
            var antigo=a.Itens.SingleOrDefault(i=>i.Id==dto.ItemId)??throw new KeyNotFoundException();
            if(antigo.Situacao==SituacaoPreparo.Cancelado)throw new ValidationException("O item já foi cancelado ou substituído.");
            if(antigo.Situacao!=SituacaoPreparo.NaFila&&!AcessoSalaoService.Gestor(u.Tipo))throw new ValidationException("O preparo já começou. Peça ao gerente para corrigir com o motivo registrado.");
            var situacaoAnterior=antigo.Situacao;
            antigo.Situacao=SituacaoPreparo.Cancelado;antigo.CanceladoEmUtc=Agora;antigo.MotivoCancelamento=dto.Motivo.Trim();antigo.Versao=Guid.NewGuid();
            if(dto.Quantidade>0)
            {
                var produtoId=dto.ProdutoId??antigo.ProdutoId;
                var novo=produtoId==antigo.ProdutoId
                    ?new ItemComanda{AtendimentoId=a.Id,ProdutoId=antigo.ProdutoId,NomeProduto=antigo.NomeProduto,PrecoUnitario=antigo.PrecoUnitario,Composicao=antigo.Composicao,
                        ImagemUrl=antigo.ImagemUrl,ImagemPublicId=antigo.ImagemPublicId,EnviadoEmUtc=Agora,RodadaId=dto.ChaveSolicitacao}
                    :Item(a,await ProdutoVendaAtual(produtoId,ct),new(){ProdutoId=produtoId,Quantidade=dto.Quantidade,Observacao=dto.Observacao},dto.ChaveSolicitacao);
                novo.Quantidade=dto.Quantidade;novo.Observacao=string.IsNullOrWhiteSpace(dto.Observacao)?null:dto.Observacao.Trim();novo.SubstituiItemId=antigo.Id;
                a.Itens.Add(novo);db.ItensComanda.Add(novo);
            }
            await avisos.Correcao(a,antigo,dto.Motivo.Trim(),ct);
            ConferirTotal(a);Alterada(a);
            return new(a.Id,a.Id,$"Correção de {antigo.Quantidade} × {antigo.NomeProduto} (estava {situacaoAnterior}); nova quantidade {dto.Quantidade}. Motivo: {dto.Motivo.Trim()}.");
        },ct);
    }
    public async Task Entregar(int usuarioId,EntregarItemDTO dto,CancellationToken ct=default)
    {
        var u=await acesso.Exigir(usuarioId,ct,TipoUsuario.Garcom,TipoUsuario.Gerente);
        await comandos.Executar(usuarioId,"entregar-item",dto.ChaveSolicitacao,dto,async()=>
        {
            var a=await Buscar(dto.AtendimentoId,ct);Dono(u,a);SomenteAberta(a);ConferirVersao(a.Versao,dto.Versao);
            var item=a.Itens.SingleOrDefault(i=>i.Id==dto.ItemId)??throw new KeyNotFoundException();
            if(item.Situacao!=SituacaoPreparo.Pronto)throw new ValidationException("Somente um item pronto pode ser registrado como entregue.");
            item.Situacao=SituacaoPreparo.Entregue;item.EntregueEmUtc=Agora;item.Versao=Guid.NewGuid();Alterada(a);
            return new(a.Id,a.Id,$"Entregue à mesa: {item.Quantidade} × {item.NomeProduto}.");
        },ct);
    }
    public async Task Transferir(int usuarioId,TransferirMesaDTO dto,CancellationToken ct=default)
    {
        await acesso.Exigir(usuarioId,ct,TipoUsuario.Gerente);
        await comandos.Executar(usuarioId,"transferir-mesa",dto.ChaveSolicitacao,dto,async()=>
        {
            var a=await Buscar(dto.AtendimentoId,ct);SomenteAberta(a);ConferirVersao(a.Versao,dto.Versao);
            var garcom=await acesso.Exigir(dto.GarcomId,ct,TipoUsuario.Garcom,TipoUsuario.Gerente);
            var anterior=a.Garcom.Nome;a.GarcomId=garcom.Id;Alterada(a);await avisos.Transferencia(a,ct);
            return new(a.Id,a.Id,$"Responsável alterado de {anterior} para {garcom.Nome}.");
        },ct);
    }
    public async Task Descontar(int usuarioId,DescontoComandaDTO dto,CancellationToken ct=default)
    {
        await acesso.Exigir(usuarioId,ct,TipoUsuario.Gerente);Validar(dto);
        if(dto.Valor<0||dto.Valor>99999999.99m||decimal.Round(dto.Valor,2)!=dto.Valor||dto.Tipo==TipoDescontoPresencial.Percentual&&dto.Valor>100)
            throw new ValidationException("Informe um desconto válido em reais ou entre 0 e 100%.");
        await comandos.Executar(usuarioId,"desconto",dto.ChaveSolicitacao,dto,async()=>
        {
            var a=await Buscar(dto.AtendimentoId,ct);SomenteAberta(a);ConferirVersao(a.Versao,dto.Versao);
            if(dto.Tipo==TipoDescontoPresencial.Valor&&dto.Valor>Subtotal(a))throw new ValidationException("O desconto não pode ultrapassar o subtotal.");
            a.TipoDesconto=dto.Tipo;a.ValorDescontoInformado=dto.Valor;a.DescontoPorId=usuarioId;a.MotivoDesconto=dto.Motivo.Trim();Alterada(a);
            return new(a.Id,a.Id,$"Desconto atualizado: {dto.Valor} ({dto.Tipo}). Motivo: {dto.Motivo.Trim()}.");
        },ct);
    }
    public async Task Fechar(int usuarioId,FecharComandaDTO dto,CancellationToken ct=default)
    {
        var u=await acesso.Exigir(usuarioId,ct,TipoUsuario.Garcom,TipoUsuario.Gerente);Validar(dto);
        if(dto.Pagamentos==null||dto.Pagamentos.Count>20)throw new ValidationException("Confira os recebimentos da conta.");
        foreach(var p in dto.Pagamentos){Validar(p);if(p.Valor<=0||p.Valor>99999999.99m||decimal.Round(p.Valor,2)!=p.Valor)throw new ValidationException("Informe valores de recebimento positivos com até duas casas decimais.");}
        await comandos.Executar(usuarioId,"fechar-comanda",dto.ChaveSolicitacao,dto,async()=>
        {
            var a=await Buscar(dto.AtendimentoId,ct);Dono(u,a);SomenteAberta(a);ConferirVersao(a.Versao,dto.Versao);
            if(a.Itens.Any(i=>i.Situacao is SituacaoPreparo.NaFila or SituacaoPreparo.EmPreparo or SituacaoPreparo.Pronto))throw new ValidationException("Entregue ou corrija os itens pendentes antes de fechar a conta.");
            var subtotal=Subtotal(a);var desconto=Desconto(a);var total=subtotal-desconto;
            if(!dto.PagamentoExternoConferido)throw new ValidationException("Confirme que o pagamento externo foi conferido.");
            if(dto.Pagamentos.Sum(p=>p.Valor)!=total)throw new ValidationException("A soma dos recebimentos deve ser igual ao total da conta.");
            foreach(var p in dto.Pagamentos)db.PagamentosPresenciais.Add(new(){AtendimentoId=a.Id,Meio=p.Meio,Valor=p.Valor,Referencia=p.Referencia?.Trim()});
            a.SubtotalFechamento=subtotal;a.TotalFechamento=total;a.FechadoEmUtc=Agora;a.FechadoPorId=usuarioId;a.GarcomFechamento=a.Garcom.Nome;
            a.Situacao=SituacaoAtendimento.AguardandoLimpeza;Alterada(a);
            return new(a.Id,a.Id,$"Comanda fechada. Subtotal {subtotal:F2}; desconto {desconto:F2}; total {total:F2}. Recebimento externo conferido. Mesa aguardando limpeza.");
        },ct);
    }
    public async Task Liberar(int usuarioId,ComandaComandoDTO dto,CancellationToken ct=default)
    {
        var u=await acesso.Exigir(usuarioId,ct,TipoUsuario.Garcom,TipoUsuario.Gerente);
        await comandos.Executar(usuarioId,"liberar-mesa",dto.ChaveSolicitacao,dto,async()=>
        {
            var a=await Buscar(dto.AtendimentoId,ct);Dono(u,a);ConferirVersao(a.Versao,dto.Versao);
            if(a.Situacao!=SituacaoAtendimento.AguardandoLimpeza)throw new ValidationException("Feche a conta e conclua a limpeza antes de liberar a mesa.");
            a.Situacao=SituacaoAtendimento.Encerrado;a.MesaOcupadaId=null;a.LiberadoEmUtc=Agora;Alterada(a);
            var mesa=await db.MesasRestaurante.SingleAsync(m=>m.Id==a.MesaId,ct);mesa.Versao=Guid.NewGuid();
            if(a.Reserva!=null){a.Reserva.Situacao=SituacaoReserva.Concluida;a.Reserva.EncerradaEmUtc=Agora;a.Reserva.Versao=Guid.NewGuid();
                a.Reserva.Eventos.Add(new(){AdministradorId=usuarioId,DataUtc=Agora,Descricao="Atendimento encerrado; mesa limpa e liberada pela equipe do salão."});}
            return new(a.Id,a.Id,"Mesa limpa e liberada para novo grupo.");
        },ct);
    }
}
