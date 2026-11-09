const {test} = require('node:test');
const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');
const path = require('node:path');
const pasta = path.resolve(__dirname, '../../KioraRestaurante/wwwroot/js');
const carregar = (nome, contexto) => vm.runInNewContext(fs.readFileSync(path.join(pasta, nome), 'utf8'), contexto);
const proximoCiclo = () => new Promise(resolve => setImmediate(resolve));

class Campo {
    constructor() { this.value=''; this.disabled=false; this.hidden=false; this.dataset={}; this.eventos={}; this.textContent=''; this.checked=false; this.children=[]; }
    addEventListener(nome, executar) { (this.eventos[nome]??=[]).push(executar); }
    emitir(nome, evento={}) { return Promise.all((this.eventos[nome]??[]).map(fn=>fn(evento))); }
    querySelectorAll() { return []; }
    querySelector() { return null; }
    replaceChildren(...filhos) { this.children=filhos; }
    add(filho) { this.children.push(filho); }
    focus() { this.focado=true; }
}

function checkout({validade,validacao=true}={}) {
    const nomes=['formCheckout','confirmarPedido','endereco-novo','camposNovoEndereco','Dados_NovoEndereco_SalvarNaConta','Dados_NovoEndereco_Apelido','grupoApelidoEndereco','Dados_TokenEntrega','mensagemEntrega','calcularEntrega','taxaEntrega','distanciaEntrega','totalComEntrega','Dados_EnderecoVersao'];
    const elementos=Object.fromEntries(nomes.map(n=>[n,new Campo()]));
    const form=elementos.formCheckout;
    form.dataset={validadeEntrega:validade??'2026-11-08T12:15:00Z',podeProsseguir:'true'};
    elementos.Dados_TokenEntrega.value='cotacao-protegida';
    const agora=Date.parse('2026-11-08T12:00:00Z');
    const contexto={document:{getElementById:n=>elementos[n]??null,addEventListener(){}},window:{addEventListener(){},jQuery:()=>({valid:()=>validacao})},Date:class extends Date {static now(){return agora;}},Intl,console};
    carregar('checkout.js',contexto);
    return elementos;
}

test('Checkout bloqueia cotação vencida sem enviar o pedido e orienta recalcular',async()=>{
    const e=checkout({validade:'2026-11-08T11:59:59Z'});let bloqueou=false;
    await e.formCheckout.emitir('submit',{preventDefault(){bloqueou=true;},submitter:{value:'confirmar'}});
    assert.equal(bloqueou,true);assert.equal(e.Dados_TokenEntrega.value,'');assert.equal(e.confirmarPedido.disabled,true);
    assert.match(e.mensagemEntrega.textContent,/cotação expirou/);assert.equal(e.calcularEntrega.focado,true);
});

test('Checkout com cotação válida mantém envio e bloqueia o botão contra repetição',async()=>{
    const e=checkout();let bloqueou=false;
    await e.formCheckout.emitir('submit',{preventDefault(){bloqueou=true;},submitter:{value:'confirmar'}});
    assert.equal(bloqueou,false);assert.equal(e.Dados_TokenEntrega.value,'cotacao-protegida');assert.equal(e.confirmarPedido.disabled,true);assert.equal(e.confirmarPedido.textContent,'Confirmando…');
});

test('Validação recusada no checkout impede o envio sem deixar botão preso',async()=>{
    const e=checkout({validacao:false});let bloqueou=false;
    await e.formCheckout.emitir('submit',{preventDefault(){bloqueou=true;},submitter:{value:'confirmar'}});
    assert.equal(bloqueou,true);assert.equal(e.confirmarPedido.disabled,false);
});

function horarios(fetch) {
    const elementos=Object.fromEntries(['formReserva','Dados_Data','Dados_Pessoas','Dados_Horario','avisoHorarios'].map(n=>[n,new Campo()]));
    const form=elementos.formReserva;form.dataset.horariosUrl='/Reservas/Horarios';form.querySelector=s=>elementos[s.slice(1)];elementos.Dados_Data.value='2026-11-08';elementos.Dados_Pessoas.value='2';
    const contexto={document:{getElementById:n=>elementos[n]??null},location:{origin:'https://kiora.test'},URL,AbortController,Option:class {constructor(texto,valor){this.text=texto;this.value=valor;}},fetch};
    carregar('reservas.js',contexto);return elementos;
}

test('Horário de uma data antiga não substitui a data nova após ler JSON atrasado',async()=>{
    let resolverAntiga;let consultas=0;
    const e=horarios(async()=>({ok:true,json:()=>++consultas===1?new Promise(resolve=>resolverAntiga=resolve):Promise.resolve([{horario:'20:00',disponivel:true}])}));
    const antiga=e.Dados_Data.emitir('change');await proximoCiclo();
    e.Dados_Data.value='2026-11-09';await e.Dados_Data.emitir('change');
    resolverAntiga([{horario:'12:00',disponivel:true}]);await antiga;
    assert.equal(e.Dados_Horario.disabled,false);assert.deepEqual(e.Dados_Horario.children.map(x=>x.value),['','20:00']);
});

for(const situacao of [5,6])test('Reserva encerrada não mantém aviso de prazo ativo: '+situacao,async()=>{
    const nomes=['acompanharReserva','cancelarReserva','notificarReserva','situacaoReserva','posicaoFila','horarioReserva','mesaReserva','prazoReserva','avisoChamada','estadoConsulta'];
    const e=Object.fromEntries(nomes.map(n=>[n,new Campo()]));const versao=new Campo();e.cancelarReserva.querySelector=()=>versao;
    const contexto={document:{getElementById:n=>e[n]??null,addEventListener(){},hidden:false},window:{},setInterval(){},confirm:()=>true,fetch:async()=>({ok:true,json:async()=>({situacao,situacaoTexto:'Encerrada',expiraEmUtc:'2026-11-08T12:00:00Z',podeCancelar:false,versao:'nova'})}),Intl,Date};
    carregar('reservas.js',contexto);await proximoCiclo();
    assert.equal(e.prazoReserva.hidden,true);assert.equal(e.cancelarReserva.hidden,true);assert.equal(versao.value,'nova');
});

test('Scripts próprios não quebram uma página sem os respectivos componentes',()=>{
    for(const nome of fs.readdirSync(pasta).filter(n=>n.endsWith('.js'))) {
        const prontos=[];
        const contexto={document:{documentElement:{dataset:{}},querySelector:()=>null,querySelectorAll:()=>[],getElementById:()=>null,addEventListener:(nome,fn)=>{if(nome==='DOMContentLoaded')prontos.push(fn);}},window:{location:{href:'https://kiora.test/'},addEventListener(){},matchMedia:()=>({matches:false,addEventListener(){}})},localStorage:{getItem:()=>null},URL,Intl,console};
        assert.doesNotThrow(()=>{carregar(nome,contexto);prontos.forEach(fn=>fn());},nome);
    }
});
