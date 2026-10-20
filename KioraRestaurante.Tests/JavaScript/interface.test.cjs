const {test} = require('node:test');
const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');
const path = require('node:path');
const scripts = path.resolve(__dirname, '../../KioraRestaurante/wwwroot/js');
const tick = () => new Promise(resolve => setImmediate(resolve));

class Elemento {
    constructor() { this.dataset={}; this.style={}; this.handlers={}; this.children=[]; this.selectores={}; this.disabled=false; this.hidden=false; this.textContent=''; this.value=''; this.classList={contains:()=>false,add(){},remove(){},toggle(){}}; }
    addEventListener(tipo,fn,options={}) {
        const anterior=this.handlers[tipo];
        this.handlers[tipo]=(...args)=>{
            anterior?.(...args);
            if(options.once)this.handlers[tipo]=anterior;
            return fn(...args);
        };
    }
    querySelector(s) { return this.selectores[s] ??= new Elemento(); }
    querySelectorAll() { return []; }
    setAttribute() {}
    append(...items) { this.children.push(...items); }
    replaceChildren(...items) { this.children=items; }
    focus() {}
    reset() {}
    reportValidity() { return true; }
}
function ambiente(ids, fetch) {
    const elementos=Object.fromEntries(ids.map(id=>[id,new Elemento()]));
    const destinos=[];
    const ctx={
        document:{getElementById:id=>elementos[id]||null,querySelectorAll:()=>[],querySelector:()=>null,
            createElement:()=>new Elemento(),addEventListener(){},dispatchEvent(){}},
        window:{location:{href:'https://localhost/',assign:url=>destinos.push(url),reload:()=>destinos.push('reload')},
            history:{replaceState(){}},matchMedia:()=>({matches:true})},
        bootstrap:{Offcanvas:{getOrCreateInstance:()=>({show(){},hide(){}})},Modal:{getOrCreateInstance:()=>({show(){},hide(){}}),getInstance:()=>null}},
        fetch,URL,Intl,AbortController,console:{log(){},error(){}},setTimeout:()=>0,clearTimeout(){},
        FormData:class { constructor(form){this.dados={...form?.dados};} append(k,v){this.dados[k]=v;} get(k){return this.dados[k]??null;} set(k,v){this.dados[k]=v;} },
        CustomEvent:class {},requestAnimationFrame:fn=>fn()
    };
    return {ctx,elementos,destinos};
}
function carregar(a,nome) { vm.runInNewContext(fs.readFileSync(path.join(scripts,nome),'utf8'),a.ctx); }
function resposta(dados,status=200) { return {ok:status<400,status,redirected:false,json:async()=>dados,text:async()=>JSON.stringify(dados)}; }

test('Desativação bloqueia clique repetido e navega depois do logout',async()=>{
    let liberar,chamadas=0;
    const a=ambiente(['btnConfirmarDesativacao','modalDesativarConta','mensagemDesativarConta'],()=>{chamadas++;return new Promise(r=>liberar=r);});
    const btn=a.elementos.btnConfirmarDesativacao;btn.textContent='Desativar';
    carregar(a,'conta.js');
    const primeira=btn.handlers.click();await btn.handlers.click();
    assert.equal(chamadas,1);assert.equal(btn.disabled,true);
    liberar(resposta({sucesso:true,mensagem:'Conta desativada',redirecionarPara:'/'}));await primeira;
    assert.deepEqual(a.destinos,['/']);assert.equal(btn.disabled,true);
});
for (const falha of ['http','rede']) test('Desativação permite nova tentativa após erro '+falha,async()=>{
    const a=ambiente(['btnConfirmarDesativacao','modalDesativarConta','mensagemDesativarConta'],async()=>{
        if(falha==='rede')throw new Error('sem rede');return resposta({sucesso:false,mensagem:'Tente novamente'},400);
    });
    const btn=a.elementos.btnConfirmarDesativacao;btn.textContent='Desativar';carregar(a,'conta.js');
    await btn.handlers.click();assert.equal(btn.disabled,false);assert.equal(btn.textContent,'Desativar');
    assert.equal(a.destinos.length,0);assert.ok(a.elementos.mensagemDesativarConta.textContent);
});
test('Login usa o destino validado devolvido pelo servidor',async()=>{
    const a=ambiente(['formLogin','mensagemLogin'],async()=>resposta({sucesso:true,redirecionarPara:'/Pedido/Checkout'}));
    carregar(a,'conta.js');await a.elementos.formLogin.handlers.submit({preventDefault(){}});
    assert.deepEqual(a.destinos,['/Pedido/Checkout']);
});

function carrinho(versao,quantidade=1) { return {versao,itens:[{produtoId:1,nomeProduto:'Combo',quantidade,precoUnitario:10,subtotal:10*quantidade,disponivelParaCompra:true,composicao:'2 × <b>Componente</b>'}],quantidadeTotal:quantidade,total:10*quantidade,podeProsseguir:true,mesclagemPendente:false}; }
function ambienteCarrinho(fetch) {
    const a=ambiente(['carrinhoConfiguracao','painelCarrinho','itensCarrinho','modeloItemCarrinho','carrinhoVazio','totalCarrinho','quantidadeCarrinho','abrirCarrinho','avisosCarrinho','estadoCarrinho','avisoCarrinho','revisarCarrinho','esvaziarCarrinho','atualizarCarrinho','formMesclagem','conflitosCarrinho'],fetch);
    a.elementos.carrinhoConfiguracao.dataset={api:'/api/carrinho',raiz:'/',checkout:'/Pedido/Checkout'};
    a.elementos.modeloItemCarrinho.content={cloneNode:()=>new Elemento()};
    carregar(a,'carrinho.js');return a;
}
function clicarItem(a,acao='aumentar') {
    const linha={dataset:{produtoId:'1'}};
    const botao={dataset:{acao},disabled:false,closest:()=>linha};
    return a.elementos.itensCarrinho.handlers.click({target:{closest:()=>botao}});
}
test('Conflito atualiza a tela sem repetir alteração e próxima tentativa usa nova versão',async()=>{
    const chamadas=[];let gets=0,puts=0;
    const a=ambienteCarrinho(async(url,options)=>{
        chamadas.push({url,...options});
        if(options.method==='GET')return resposta(carrinho(++gets===1?'v1':'v2',gets===1?1:5));
        if(++puts===1)return resposta({codigo:'carrinho_desatualizado',mensagem:'Atualize'},409);
        return resposta(carrinho('v3',6));
    });
    await tick();await clicarItem(a);
    assert.equal(puts,1);assert.equal(gets,2);assert.equal(a.elementos.quantidadeCarrinho.textContent,5);
    assert.match(a.elementos.avisoCarrinho.textContent,/outra aba/);
    assert.deepEqual(JSON.parse(chamadas.find(c=>c.method==='PUT').body),{quantidade:2,versaoEsperada:'v1'});
    await clicarItem(a);assert.equal(puts,2);
    assert.deepEqual(JSON.parse(chamadas.filter(c=>c.method==='PUT')[1].body),{quantidade:6,versaoEsperada:'v2'});
    const item=a.elementos.itensCarrinho.children[0];
    assert.equal(item.querySelector('.kc-item-composicao').textContent,'Composição por combo: 2 × <b>Componente</b>');
});
test('Remover e esvaziar enviam a versão exibida',async()=>{
    const chamadas=[];
    const a=ambienteCarrinho(async(url,options)=>{chamadas.push({url,...options});return resposta(carrinho('v1'));});
    await tick();await clicarItem(a,'remover');
    a.elementos.esvaziarCarrinho.handlers.click();await tick();
    const deletes=chamadas.filter(c=>c.method==='DELETE');
    assert.equal(deletes.length,2);
    for(const req of deletes)assert.deepEqual(JSON.parse(req.body),{versaoEsperada:'v1'});
});

test('Revisão de visitante guarda o checkout como destino do login',async()=>{
    const a=ambienteCarrinho(async()=>resposta({...carrinho('v1'),requerLogin:true}));
    a.elementos.modalLogin=new Elemento();a.elementos.loginReturnUrl=new Elemento();
    await tick();a.elementos.revisarCarrinho.handlers.click();await tick();
    assert.equal(a.elementos.loginReturnUrl.value,'/Pedido/Checkout');
    assert.equal(a.destinos.length,0);
});
for (const concluida of [false,true]) test('Checkout só recarrega após mesclagem concluída: '+concluida,async()=>{
    const a=ambienteCarrinho(async(url,opcoes)=>opcoes.method==='GET'
        ? resposta({...carrinho('v1'),mesclagemPendente:true})
        : resposta({concluida,carrinho:carrinho('v2'),conflitos:[{produtoId:1,nomeProduto:'Combo',quantidadeVisitante:20,quantidadeUsuario:20,quantidadeSomada:40}]},concluida?200:409));
    a.elementos.formCheckout=new Elemento();
    await tick();
    assert.deepEqual(a.destinos,concluida?['reload']:[]);
    assert.equal(a.elementos.formMesclagem.hidden,concluida);
});

function ambienteRecuperacao(fetch) {
    const ids=['modalEsqueciSenha','modalLogin','etapaEmailRecuperacao','etapaCodigoRecuperacao','etapaRedefinirSenha','voltarEmailRecuperacao','formEsqueciSenha','formValidarCodigo','formRedefinirSenha','mensagemEsqueciSenha','mensagemValidarCodigo','mensagemRedefinirSenha','esqueciSenhaEmail','emailCodigoRecuperacao','emailRedefinicao','codigoRecuperacao','codigoRedefinicao','novaSenhaRecuperacao','confirmarNovaSenhaRecuperacao'];
    const a=ambiente(ids,fetch);const e=a.elementos;const timers=[];
    e.etapaEmailRecuperacao.style.display='block';e.etapaCodigoRecuperacao.style.display='none';e.etapaRedefinirSenha.style.display='none';
    const campos={
        formEsqueciSenha:{Email:'esqueciSenhaEmail'},
        formValidarCodigo:{Email:'emailCodigoRecuperacao',Codigo:'codigoRecuperacao'},
        formRedefinirSenha:{Email:'emailRedefinicao',Codigo:'codigoRedefinicao',NovaSenha:'novaSenhaRecuperacao',ConfirmarNovaSenha:'confirmarNovaSenhaRecuperacao'}
    };
    for(const [form,mapa] of Object.entries(campos)) {
        Object.defineProperty(e[form],'dados',{get:()=>Object.fromEntries(Object.entries(mapa).map(([nome,id])=>[nome,e[id].value]))});
        e[form].action='/Conta/'+form;
        e[form].reset=()=>Object.values(mapa).forEach(id=>e[id].value='');
        e[form].querySelectorAll=()=>Object.values(mapa).map(id=>e[id]);
    }
    const controles=Object.values(campos).flatMap(m=>Object.values(m).map(id=>e[id]));
    e.modalEsqueciSenha.querySelectorAll=()=>controles;
    a.ctx.setTimeout=fn=>{timers.push(fn);return timers.length;};
    a.ctx.clearTimeout=id=>{if(id)timers[id-1]=null;};
    a.ctx.bootstrap.Modal.getInstance=()=>({hide(){e.modalEsqueciSenha.handlers['hidden.bs.modal']?.();}});
    a.ctx.bootstrap.Modal.getOrCreateInstance=()=>({show(){a.destinos.push('modalLogin');}});
    a.temporizadores=timers;
    carregar(a,'conta.js');
    e.esqueciSenhaEmail.value='cliente@example.test';
    return a;
}
const enviar=form=>form.handlers.submit({preventDefault(){}});

test('Recuperação percorre as três etapas com a mesma conta e limpa ao concluir',async()=>{
    const pedidos=[];
    const a=ambienteRecuperacao(async(url,opcoes)=>{pedidos.push({...opcoes.body.dados});return resposta({sucesso:true,mensagem:'OK'});});
    const e=a.elementos;
    await enviar(e.formEsqueciSenha);
    assert.equal(e.etapaEmailRecuperacao.style.display,'none');assert.equal(e.etapaCodigoRecuperacao.style.display,'block');
    assert.equal(e.emailCodigoRecuperacao.value,'cliente@example.test');
    e.codigoRecuperacao.value='123456';await enviar(e.formValidarCodigo);
    assert.equal(e.etapaCodigoRecuperacao.style.display,'none');assert.equal(e.etapaRedefinirSenha.style.display,'block');
    assert.equal(e.codigoRedefinicao.value,'123456');
    e.novaSenhaRecuperacao.value=e.confirmarNovaSenhaRecuperacao.value='NovaSenha123!';await enviar(e.formRedefinirSenha);
    assert.equal(pedidos.length,3);assert.ok(pedidos.every(p=>p.Email==='cliente@example.test'));
    assert.equal(pedidos[2].Codigo,'123456');
    a.temporizadores.filter(Boolean).forEach(fn=>fn());
    assert.deepEqual(a.destinos,['modalLogin']);assert.equal(e.codigoRedefinicao.value,'');
    assert.equal(e.etapaEmailRecuperacao.style.display,'block');assert.equal(e.novaSenhaRecuperacao.disabled,false);
});

test('Fechar recuperação cancela a interface pendente e ignora resposta antiga',async()=>{
    let liberar;let chamadas=0;
    const a=ambienteRecuperacao(()=>{chamadas++;return new Promise(r=>liberar=r);});const e=a.elementos;
    const primeiro=enviar(e.formEsqueciSenha);await enviar(e.formEsqueciSenha);
    assert.equal(chamadas,1);assert.equal(e.esqueciSenhaEmail.disabled,true);
    e.modalEsqueciSenha.handlers['hidden.bs.modal']();
    liberar(resposta({sucesso:true,mensagem:'OK'}));await primeiro;
    assert.equal(e.etapaCodigoRecuperacao.style.display,'none');assert.equal(e.emailCodigoRecuperacao.value,'');
    assert.equal(e.esqueciSenhaEmail.disabled,false);assert.equal(e.esqueciSenhaEmail.value,'');
});

test('Recuperação informa limite sem avançar e libera formulário para tentar depois',async()=>{
    const a=ambienteRecuperacao(async()=>resposta({sucesso:false,mensagem:'Aguarde 600 segundos'},429));const e=a.elementos;
    await enviar(e.formEsqueciSenha);
    assert.equal(e.etapaCodigoRecuperacao.style.display,'none');assert.match(e.mensagemEsqueciSenha.textContent,/Aguarde/);
    assert.equal(e.esqueciSenhaEmail.disabled,false);
});

test('Solicitar novo código volta ao email e apaga código e senha anteriores',async()=>{
    const a=ambienteRecuperacao(async()=>resposta({sucesso:true,mensagem:'OK'}));const e=a.elementos;
    await enviar(e.formEsqueciSenha);e.codigoRecuperacao.value='123456';await enviar(e.formValidarCodigo);
    e.novaSenhaRecuperacao.value='SenhaDigitada123!';e.voltarEmailRecuperacao.handlers.click();
    assert.equal(e.esqueciSenhaEmail.value,'cliente@example.test');assert.equal(e.codigoRedefinicao.value,'');
    assert.equal(e.novaSenhaRecuperacao.value,'');assert.equal(e.etapaEmailRecuperacao.style.display,'block');
});
