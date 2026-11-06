# Checkout, endereços e entrega

## Comportamento implementado

- O cliente pode escolher um endereço salvo ou preencher um novo diretamente no checkout.
- A opção **Salvar este endereço na minha conta** vem desmarcada. O endereço é sempre registrado no pedido para permitir a entrega, mas só entra na lista reutilizável quando o cliente marcar essa opção e confirmar a compra.
- O endereço opcional, o pedido e a limpeza do carrinho são gravados na mesma operação. Calcular a entrega não cadastra endereços. Uma falha na gravação não deve esvaziar o carrinho nem cadastrar apenas o endereço.
- A exclusão está disponível no checkout e em Meus endereços, com confirmação. Somente o proprietário pode excluir; a versão do endereço impede apagar silenciosamente uma edição feita em outra aba. Os pedidos anteriores mantêm suas cópias próprias do endereço.
- Os detalhes mostram situação e etapas do pedido, itens e composição histórica de combos, quantidades, preços, descontos registrados, pagamento, endereço, distância estimada, subtotal, entrega e total. As etapas representam o status atual, sem inventar horários de transição ou previsão de chegada.

## Área e tabela aprovadas para o TCC

Saída: **Rua dos Pinheiros, 1234, Pinheiros, São Paulo/SP**.

| Percurso pelas ruas | Taxa |
|---|---:|
| Até 5 km, inclusive | R$ 5,00 |
| Acima de 5 até 10 km | R$ 8,00 |
| Acima de 10 até 15 km | R$ 12,00 |
| Acima de 15 até 20 km | R$ 16,00 |
| Acima de 20 km | Pedido bloqueado |

Essa tabela é fictícia e foi escolhida para o projeto; não representa uma média de mercado. O limite considera a rota de carro com preferência pelo menor percurso, respeitando as vias disponíveis na base do provedor. Não usa raio em linha reta, trânsito ao vivo nem tempo de entrega garantido.

As configurações não secretas ficam na seção `Entrega` de `appsettings.json`. Alterar os valores exige nova cotação; pedidos antigos preservam a taxa da compra.

## Serviços externos e proteção dos valores

O Geoapify localiza rua e número; o openrouteservice/HeiGIT calcula a distância pelas ruas. Ambos são chamados pelo servidor. O ViaCEP continua preenchendo e conferindo CEP, cidade e UF.

- Não utiliza o centro de uma cidade, bairro ou CEP como se fosse a localização do imóvel.
- Confere país, UF, cidade, rua, número, compatibilidade do CEP/bairro e precisão do resultado. Bases geográficas podem conter divergências ou não cobrir um número; nesses casos informa que não foi possível localizar com segurança. Um endereço real pode exigir verificação se o provedor retornar dados diferentes.
- Falha, cota excedida ou indisponibilidade do serviço bloqueiam a confirmação e permitem tentar novamente; não viram entrega grátis.
- Endereços e chaves não são registrados pelos clientes HTTP desses dois serviços.
- Usa cache temporário para reduzir consultas. O cálculo tem limite de 12 tentativas por minuto por usuário, por instância do servidor.
- A cotação é protegida no servidor, vinculada ao cliente, endereço e tabela, e expira em 15 minutos. Alterações de endereço exigem novo cálculo. Valores enviados pelo navegador não determinam o preço.
- Mercado Pago recebe os produtos e uma única linha de taxa de entrega. A soma precisa coincidir com o total do pedido; confirmação parcial não aprova o pagamento.

Os planos e as cotas das contas devem ser acompanhados nos painéis dos provedores. Não há contratação automática de plano pago pelo código.

## Configuração local

As chaves já foram configuradas nos User Secrets do projeto. Não devem ser publicadas no GitHub nem colocadas nos arquivos JavaScript.

Nomes usados pela aplicação:

```text
Entrega:Rotas:ApiKey
Entrega:Geocodificacao:ApiKey
```

Caso precise trocar uma chave no Mac, abra o terminal e execute na pasta do projeto web:

```zsh
cd "/Users/claytonbrito/Desktop/Kiora/KioraRestaurante/KioraRestaurante"
read -s "chave_rotas_kiora?Cole a Basic Key do HeiGIT e pressione Enter: "
printf '\n'
dotnet user-secrets set "Entrega:Rotas:ApiKey" "$chave_rotas_kiora"
unset chave_rotas_kiora

read -s "chave_geoapify_kiora?Cole a API Key do Geoapify e pressione Enter: "
printf '\n'
dotnet user-secrets set "Entrega:Geocodificacao:ApiKey" "$chave_geoapify_kiora"
unset chave_geoapify_kiora
```

Reinicie a aplicação após mudar a configuração. Não é necessário alterar credenciais do Mercado Pago para ativar a entrega.

## Banco de dados e publicação

A migration **20261106222110_AdicionarTaxaEDistanciaEntrega** acrescenta somente duas colunas a `Pedidos`:

- `TaxaEntrega`: decimal, com padrão zero para pedidos anteriores.
- `DistanciaEntregaMetros`: inteiro opcional, vazio nos pedidos anteriores.

Ela não apaga pedidos, clientes nem endereços. É compatível com a versão anterior da aplicação, portanto deve ser aplicada antes de iniciar a versão nova. Não deve ser aplicada uma migration `Down` em produção como tentativa de desfazer uma publicação: isso apagaria as duas colunas e seus dados.

Depois de conferir o destino da conexão e o estado das migrations do banco, a atualização é feita na pasta do projeto web com:

```zsh
dotnet ef database update 20261106222110_AdicionarTaxaEDistanciaEntrega
```

No Render, além da publicação do código, cadastrar estas duas variáveis no serviço da Kiora (o valor de cada uma é a respectiva chave):

```text
Entrega__Rotas__ApiKey
Entrega__Geocodificacao__ApiKey
```

User Secrets do Rider não são enviados automaticamente ao Render. A aplicação deve ser reiniciada/publicada após cadastrar as variáveis. A nova versão depende da migration e dessas configurações para o fluxo de entrega funcionar.

## Validação

Foram preparados testes isolados de limites de distância, taxas, cotação adulterada/expirada, alteração de endereço, propriedade e versão dos endereços, gravação atômica, repetição de confirmação, pagamento com entrega e respostas inválidas dos provedores.

Na consulta real com as chaves locais, o restaurante foi localizado em latitude -23.5681155 / longitude -46.6893574; o percurso até o endereço público do MASP retornou **4.474,7 metros**, correspondendo à faixa de **R$ 5,00**. Isso valida as credenciais e os serviços, mas não garante a cobertura de todos os endereços.

A interface foi conferida em uma aplicação local isolada, com SQLite e dados fictícios: endereço salvo, novo endereço sem cadastro, consulta por CEP, recalcular ao editar, bloqueio acima de 20 km, exclusão com confirmação e finalização de pedido em dinheiro. Também foram conferidos os detalhes em tema claro, escuro e largura de celular. Esse ambiente não utiliza o banco Aiven nem cria cobranças reais.

## Documentação dos provedores

- [Geoapify — Geocoding API](https://apidocs.geoapify.com/docs/geocoding/)
- [Geoapify — painel de projetos](https://myprojects.geoapify.com/)
- [HeiGIT — painel de chaves](https://account.heigit.org/)
- [openrouteservice — Directions](https://giscience.github.io/openrouteservice/v9.10.0/api-reference/endpoints/directions/)
- [Migração do endereço da API para api.heigit.org](https://ask.openrouteservice.org/t/deprecating-api-openrouteservice-org-in-favour-of-api-heigit-org/7912)
