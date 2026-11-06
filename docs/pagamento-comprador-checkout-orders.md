# Comprador no Checkout Pro via Orders

## Ajuste

A criação da order passa a enviar `payer.email` antes de redirecionar o cliente ao Mercado Pago.

- Com `MercadoPago:ModoTeste=true`, usa `MercadoPago:EmailCompradorTeste`, cujo padrão é `comprador.kiora@example.com`. Não envia o e-mail real do cliente na simulação.
- Com `MercadoPago:ModoTeste=false`, usa o e-mail cadastrado do cliente dono do pedido. A configuração de e-mail fictício não é utilizada em produção.
- O corpo completo continua salvo com a mesma referência e chave de idempotência. Uma repetição após falha de comunicação não altera o comprador nem abre outra cobrança.
- A aprovação continua dependendo de uma consulta autenticada ao provedor e da conferência de aplicação, vendedor, ambiente e valor integral pago.

O teste externo realizado com a credencial de teste confirmou `processed / accredited` e `total_paid_amount=1.00` para a cobrança fictícia de diagnóstico, criada com o e-mail de teste já informado. Isso demonstra um caminho de teste funcional; não prova que o Mercado Pago rejeite universalmente orders sem `payer`.

## Configuração do ambiente

Em **Credenciais → Teste**, confira os identificadores exibidos no bloco da conta de teste:

- `MercadoPago__VendedorId`: User ID da conta de teste.
- `MercadoPago__ApplicationId`: identificador da aplicação correspondente ao ambiente de teste. Pode ser diferente do número da aplicação principal mostrado nos detalhes da integração.
- `MercadoPago__AccessToken`: token copiado com o seletor em Teste.
- `MercadoPago__ModoTeste=true`.

`EmailCompradorTeste` é opcional. Para personalizar a simulação, use `MercadoPago__EmailCompradorTeste` no Render ou `MercadoPago:EmailCompradorTeste` nos User Secrets. Nunca use a senha do vendedor como Access Token.

## Validação após publicar

1. Aguarde o novo deploy terminar no Render.
2. Entre novamente na Kiora caso o reinício encerre a sessão.
3. Crie um novo pedido de teste. Pedidos anteriores mantêm o corpo e a sessão de pagamento já registrados por idempotência.
4. Abra o checkout pelo botão da Kiora, em janela privada e sem login no Mercado Pago.
5. Use o cartão fictício e os dados APRO apresentados no painel de teste.
6. Volte à Kiora e consulte o pagamento. Confirme que o pedido mostra o status aprovado.

Não há alteração de esquema nem necessidade de migration neste ajuste. O redirecionamento continua usando o `checkout_url` recebido na Orders API; não se substitui o domínio manualmente.

## Referências

- [Criar e configurar uma order](https://www.mercadopago.com.br/developers/pt/docs/checkout-pro-orders/create-order)
- [Teste do Checkout Pro via Orders](https://www.mercadopago.com.br/developers/pt/docs/checkout-pro-orders/integration-test-introduction)
- [Credenciais](https://www.mercadopago.com.br/developers/pt/docs/your-integrations/credentials)
