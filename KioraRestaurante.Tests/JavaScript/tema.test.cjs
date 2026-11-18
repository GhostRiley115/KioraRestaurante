const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
const script = fs.readFileSync(path.join(__dirname, '../../KioraRestaurante/wwwroot/js/tema.js'), 'utf8');
function ambiente({salva = null, escuro = false, bloqueado = false} = {}) {
    const eventos = {}, janela = {}, media = {}, controle = { value: '', addEventListener: (nome, fn) => { controle[nome] = fn; } };
    const root = { dataset: {} };
    const sistema = { matches: escuro, addEventListener: (nome, fn) => { media[nome] = fn; } };
    const storage = { getItem() { if(bloqueado) throw Error(); return salva; }, setItem(chave, valor) { if(bloqueado) throw Error(); salva = valor; } };
    vm.runInNewContext(script, { document: { documentElement: root, querySelectorAll: () => [controle], addEventListener: (nome, fn) => { eventos[nome] = fn; } }, window: { matchMedia: () => sistema, addEventListener: (nome, fn) => { janela[nome] = fn; } }, localStorage: storage });
    eventos.DOMContentLoaded();
    return {root, controle, sistema, media, janela, salva: () => salva};
}
test('Sistema escolhe claro ou escuro sem preferência salva', () => {
    assert.equal(ambiente().root.dataset.tema, 'claro');
    assert.equal(ambiente({escuro:true}).root.dataset.bsTheme, 'dark');
});
test('Preferência explícita prevalece sobre mudança no dispositivo', () => {
    const a=ambiente({salva:'claro',escuro:true}); a.media.change();
    assert.equal(a.root.dataset.tema,'claro');
});
test('Escolha é persistida e voltar ao sistema acompanha o dispositivo', () => {
    const a=ambiente();a.controle.value='escuro';a.controle.change();
    assert.equal(a.salva(),'escuro');assert.equal(a.root.dataset.tema,'escuro');
    a.controle.value='sistema';a.controle.change();assert.equal(a.root.dataset.tema,'claro');
    a.sistema.matches=true;a.media.change();assert.equal(a.root.dataset.tema,'escuro');
});
test('Falha de armazenamento não impede escolher um tema', () => {
    const a=ambiente({bloqueado:true});a.controle.value='escuro';a.controle.change();
    assert.equal(a.root.dataset.tema,'escuro');
});
test('Preferência desconhecida usa sistema e mudanças entre abas sincronizam', () => {
    const a=ambiente({salva:'invalido'});assert.equal(a.controle.value,'sistema');
    a.janela.storage({key:'kiora.tema',newValue:'escuro'});assert.equal(a.root.dataset.tema,'escuro');
    a.janela.storage({key:null,newValue:null});assert.equal(a.root.dataset.tema,'claro');
});
