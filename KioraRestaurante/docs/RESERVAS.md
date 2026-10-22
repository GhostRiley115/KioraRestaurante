# Reservas, fila e recepção — Kiora

## O que foi implementado

Reserva sem login, reserva vinculada à conta quando autenticada, consulta e cancelamento pelo cliente, fila do dia, painel administrativo com atribuição de mesa, chegada, encerramento, cancelamento com motivo e ativação/desativação de mesas. As telas seguem os temas claro e escuro do site, mantendo a identidade vinho e dourado. Há comentários nos pontos principais do código.

O botão Reservar do cabeçalho abre `/Reservas`. O painel da conta tem “Minhas reservas e fila”. Na administração, entre em “Reservas e mesas” (`/ReservasAdmin`).

## Regras escolhidas

- Funcionamento diário das 11h à meia-noite, fuso America/Sao_Paulo.
- Último horário **agendado** de chegada: 23h20, conforme confirmado. A tolerância de 15 minutos também vale nesse horário; uma chegada atrasada não amplia o encerramento à meia-noite.
- Horários a cada 20 minutos, até 30 dias de antecedência.
- Permanência prevista de até 90 minutos. Horários próximos do fechamento terminam à meia-noite. Às 23h20 são 40 minutos; o formulário exige aceite da permanência reduzida.
- Grupos de 1 a 6 pessoas, incluindo crianças. Não há união automática de mesas.
- Sala fictícia inicial: 4 mesas de 2 lugares, 6 de 4 e 2 de 6: **12 mesas e 44 lugares**. Os registros são criados pela migration.
- Até 3 solicitações ativas por identidade; telefone não pode ter reservas sobrepostas nem duas entradas simultâneas na fila. Telefone é informado, não verificado por SMS: isso não constitui uma proteção completa contra abuso.
- A reserva ocupa internamente uma mesa compatível para impedir excesso de reservas. O cliente não escolhe o número; a recepção confirma ou troca por outra mesa livre e compatível na chegada.
- Chegada registrada até 15 minutos antes do horário, nunca antes de abrir. Após a tolerância, a reserva expira.
- O cliente cancela enquanto estiver confirmado, aguardando ou chamado. Após sentar, procura a equipe. O administrador encerra um atendimento e pode cancelar solicitações ainda não atendidas, informando motivo.
- Mesa ocupada não é liberada pelo relógio. A equipe precisa encerrar o atendimento. Mesa ocupada ou comprometida com reserva futura não pode ser desativada.

## Fila e avisos

A fila é para o próprio dia, entre 11h e 23h20. Na implementação, a janela fecha às 23h21 e quem ainda não foi chamado expira. Uma chamada já realizada mantém seu prazo de chegada. A equipe escolhe a mesa e clica em “Chamar próximo grupo compatível”. Entre os grupos que cabem nela, vence quem entrou primeiro. Um grupo de duas pessoas pode ser chamado antes de um grupo de seis quando a mesa tiver dois lugares.

A posição mostrada é a ordem geral de entrada, não uma promessa de atendimento estritamente nessa ordem. Não foi inventado tempo estimado de espera. A chamada permite 10 minutos para chegar. O período previsto da mesa começa na chamada, não no momento em que o cliente efetivamente senta. Reservas próximas impedem que a fila ocupe a mesa por cima delas.

A página de acompanhamento consulta o servidor a cada 15 segundos enquanto visível e mostra a chamada. Pode solicitar permissão para avisos do navegador, quando suportados. **Não é push com o navegador fechado**: o cliente deve acompanhar a página. Push para Android, service worker e provedor de notificações serão uma integração posterior. Cookie identifica o visitante; não envia notificações.

## Ativação do banco

A migration `AdicionarReservasMesasEFila` cria `ControlesSala`, `MesasRestaurante`, `Reservas` e `EventosReserva`, com índices e chaves estrangeiras. A tabela de controle tem uma linha usada para serializar operações da sala e evitar que duas solicitações reservem o mesmo espaço.

Em 21/10/2026, a consulta ao Aiven confirmou que esta migration e as quatro tabelas já existem. A recuperação do módulo preserva o identificador original da migration e não exige recriar essas tabelas. Para um banco novo, com a conexão correta e após revisar as migrations pendentes, execute no Console do Gerenciador de Pacotes, selecionando o projeto KioraRestaurante:

```powershell
Update-Database
```

Ou, na pasta que contém o `.csproj`:

```sh
dotnet ef database update
```

Esse comando aplica todas as migrations pendentes presentes nesta versão do projeto. Faça backup do banco antes de atualizar um ambiente com dados importantes. Não apague tabelas nem migrations existentes para resolver um erro de atualização. Reinicie o site depois da atualização. Não é necessário Cloudinary ou provedor de pagamento para reservar.

## Onde fica cada responsabilidade

- `Models/Reserva.cs`: estados, mesas, reserva e histórico.
- `DTOs/Reserva/ReservaDTOs.cs`: dados de entrada e saída da API.
- `Options/ReservaOptions.cs`: horários e limites; configuração opcional na seção `Reservas` do appsettings. Os padrões já funcionam. Se alterar horários, atualize também os textos explicativos nas views.
- `Services/Reservas/HorariosReserva.cs`: conversão São Paulo/UTC e regras de horário.
- `ReservaService.cs`: disponibilidade, cancelamento, fila, controle de mesas e autorização efetiva no banco.
- `IdentidadeReserva.cs`: cookie protegido do visitante e leitura do usuário autenticado.
- `ExpiracaoReservasService.cs`: verifica vencimentos a cada minuto enquanto o servidor estiver ativo. As operações de escrita também verificam vencimentos; uma aplicação que estava suspensa recupera a manutenção ao voltar.
- `Controllers/ReservasController.cs`: telas do cliente.
- `Controllers/ReservasAdminController.cs`: telas administrativas.
- `Controllers/ReservasApiController.cs`: reutilização das mesmas regras no futuro app.
- `Views/Reservas`, `Views/Admin/Reservas`, `wwwroot/css/reservas.css` e `wwwroot/js/reservas.js`: interface.

`ChaveSolicitacao` evita uma segunda reserva ao reenviar o mesmo formulário. `Versao` evita cancelar/alterar uma situação antiga depois que outra pessoa já a modificou. A transação com `FOR UPDATE` protege a decisão de disponibilidade. Não se deve retirar essa trava nem modificar reservas diretamente em outro serviço sem respeitar o mesmo controle.

## Visitante, conta e app Android

O visitante é identificado por um segredo aleatório em cookie HttpOnly protegido por Data Protection; no banco fica somente o hash desse segredo. Limpar cookies ou usar outro navegador perde o acesso à reserva de visitante. Não há recuperação por telefone. A reserva feita com login fica vinculada ao ID da conta e aparece em outros dispositivos autenticados nessa conta. Fazer login depois de uma reserva anônima não transfere automaticamente sua propriedade para a conta.

Preserve as chaves de Data Protection no servidor e compartilhe-as entre instâncias; isso também é necessário para manter os cookies reconhecíveis após reinicializações.

O app pode usar:

| Método e rota | Finalidade |
|---|---|
| GET `/api/reservas/sessao` | Obter chave do visitante, token CSRF e limites de data |
| GET `/api/reservas/horarios?data=2026-10-10&pessoas=2` | Consultar horários |
| POST `/api/reservas` | Criar reserva |
| POST `/api/reservas/fila` | Entrar na fila |
| GET `/api/reservas/minhas` | Consultar solicitações próprias |
| GET `/api/reservas/{codigo}` | Acompanhar solicitação própria |
| POST `/api/reservas/cancelar` | Cancelar com código e versão |
| GET `/api/reservas/admin?data=2026-10-10` | Painel administrativo |
| POST `/api/reservas/admin/operar` | Chegada, encerramento ou cancelamento administrativo |
| POST `/api/reservas/admin/chamar/{mesaId}` | Chamar grupo compatível |

A autenticação atual continua por cookies. O Android deve preservar cookies de autenticação e antifalsificação e enviar `X-CSRF-TOKEN` nas escritas. Para visitante, pode guardar com segurança a `chaveVisitante` recebida e enviar `X-Reserva-Chave`. Essa chave é um segredo de acesso: não colocar em URLs nem logs. Ela não substitui login de administrador. A API administrativa exige a role Administrador e confere se a conta ainda está ativa. Papéis específicos de recepção/funcionário ainda não foram acrescentados.

Exemplo do corpo para criar reserva (troque data e UUID por valores válidos):

```json
{
  "chaveSolicitacao": "96dfbb21-905d-440c-b309-c2512274b6f1",
  "nome": "Cliente exemplo",
  "telefone": "11999999999",
  "pessoas": 2,
  "data": "2026-10-10",
  "horario": "19:00",
  "aceitaPermanenciaReduzida": false
}
```

Mantenha a mesma chave ao repetir a mesma confirmação por falha de rede; use outra para uma nova reserva. Datas e horas das respostas usam UTC com `Z`; converta para exibição no app. Os enums são números conforme `Models/Reserva.cs`. Cancelamento recebe `codigo` e `versao` da última consulta. Operação administrativa recebe esses campos, `acao` (`chegada`, `encerrar` ou `cancelar`), `mesaId` para chegada e `motivo` para cancelamento. Consulte novamente após conflitos (HTTP 409).

## Como testar pela interface

1. Atualize o banco e abra `/Reservas` sem login.
2. Reserve um horário futuro e confira o acompanhamento e “Minhas reservas”.
3. Cancele e confira se o horário recuperou a vaga.
4. Em outro navegador/perfil, entre como administrador e abra Reservas e mesas.
5. Durante o funcionamento, coloque um visitante na fila; na administração, chame um grupo para uma mesa compatível.
6. Mantenha o acompanhamento do visitante aberto: a chamada aparece em até cerca de 15 segundos. Registre a chegada e, depois, encerre o atendimento.
7. Reserve estando logado e consulte com a mesma conta em outro navegador.
8. Teste o horário 23h20: o formulário precisa aceitar a permanência reduzida.

A validação da recuperação em 21/10/2026 incluiu criação de banco MySQL vazio com todas as migrations, criação e cancelamento de reserva, reenvio sem duplicação, autorização por proprietário e administrador, proteção CSRF, fila, chamada, chegada e encerramento. Três requisições simultâneas para duas mesas de seis lugares resultaram em duas confirmações e um conflito, usando a trava real `FOR UPDATE`. Tudo foi executado em banco temporário local, sem gravar reservas no Aiven.

Os testes em `ReservasConfiguracaoTests.cs` verificam horários, fechamento, o cálculo da permanência usando o mesmo instante e a coerência do snapshot com o modelo atual. O fluxo da fila foi corrigido: duas leituras consecutivas do relógio reduziam artificialmente os 90 minutos e exigiam aceite mesmo longe do fechamento.
