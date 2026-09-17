using KioraRestaurante.Data;
using KioraRestaurante.DTOs.Carrinho;
using KioraRestaurante.DTOs.ItemCarrinho;
using KioraRestaurante.Models;
using KioraRestaurante.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace KioraRestaurante.Services
{
    public class CarrinhoService : ICarrinhoService
    {
        //Variaveis globais
        private const int QuantidadeMaxima = 30;
        private const int DiasValidadeVisitante = 7;
        private readonly AppDbContext _context;

        public CarrinhoService(AppDbContext context)
        {
            _context = context;
        }

        //Metodos private serão chamados em métodos principais, são complementares.

        //Quando eu consultar um Carrinho, quero trazer junto os seus itens e, dentro de cada item, o Produto.
        private IQueryable<Carrinho> ConsultaCompleta()
        {
            return _context.Carrinhos
                .Include(c => c.ItensCarrinho)
                .ThenInclude(i => i.Produto);
        }

        //Consulta se o usuário está ativo ou não.
        /*Task -> representa uma operação que, ao terminar, pode devolver um valor ou null,
         async -> permite usar await neste método -> aguarda a conclusão da consulta sem bloquear
            a thread enquanto a operação está pendente e retorna o resultado.*/
        private async Task ValidarUsuario(int usuarioId)
        {
            var habilitado = await _context.Usuarios.AnyAsync(u => u.Id == usuarioId && u.Ativo);
            if (!habilitado)
                throw new InvalidOperationException("Usuário não está habilitado para comprar.");
        }

        /*Verifica se o ID de visitante não é null, depois consulta:
        Um carrinho que tenha o mesmo ID do visitante -> Carrinhos que não possui usuário ->
        O tempo de expiração é valido -> O tempo de expiração é maior que o atual ->
        Retorna um carrinho se for encontrado na consulta.*/
        private async Task<Carrinho?> BuscarVisitante(int? carrinhoId)
        {
            if (carrinhoId == null)
                return null;

            var agora = DateTime.UtcNow;

            return await ConsultaCompleta()
                .FirstOrDefaultAsync(c =>
                    c.Id == carrinhoId.Value
                    && c.Usuario == null
                    && c.ExpiraEmUtc != null
                    && c.ExpiraEmUtc > agora);
        }

        /*Verifica se o objeto AcessoCarrinho possui um ID no usuarioId, se tiver ->
         Valida se o usuário está ativo -> Consulta o carrinho que possui o id do usuário e retorna.

         Se o usuario não tiver um valor de ID então ele é um visitante e o método
         BuscarVisitante é chamado, retornando um carrinho de visitante.*/
        private async Task<Carrinho?> BuscarEntidade(AcessoCarrinho acesso)
        {
            if (acesso.UsuarioId.HasValue)
            {
                await ValidarUsuario(acesso.UsuarioId.Value);
                return await ConsultaCompleta().FirstOrDefaultAsync(c => c.UsuarioId == acesso.UsuarioId.Value);
            }
            return await BuscarVisitante(acesso.CarrinhoVisitanteId);
        }

        /*Busca um carrinho com o ID do visitante ou do usuário,
         se não retornar nada uma exceção é lançada.*/
        private async Task<Carrinho> ExigirCarrinho(AcessoCarrinho acesso)
        {
            var carrinho = await BuscarEntidade(acesso);
            if (carrinho == null)
                throw new KeyNotFoundException("Carrinho não encontrado ou expirado.");
            
            return carrinho;
        }

        private static void ValidarQuantidade(int quantidade)
        {
            if (quantidade < 1 || quantidade > QuantidadeMaxima)
                throw new ArgumentException($"A quantidade deve estar entre 1 e {QuantidadeMaxima} unidades.");
        }
        
        private static void ValidarProduto(Produto produto)
        {
            if (!produto.Ativo || !produto.Disponivel)
                throw new InvalidOperationException("Este produto não está disponível para compra.");
        }

        //É chamado antes de salvar alterações -> renova a versão e ajusta a expiração.
        private static void MarcarAlteracao(Carrinho carrinho)
        {
            //Toda alteração recebe uma nova versão.
            carrinho.Versao = Guid.NewGuid();

            /*Se o carrinho não tem usuário, coloque uma data de expiração. Caso contrário,
             coloque null. Somente o visitante possui prazo de expiração.*/
            carrinho.ExpiraEmUtc = carrinho.UsuarioId == null
                ? DateTime.UtcNow.AddDays(DiasValidadeVisitante)
                : null;
        }

        /*Monta um DTO de itens do carrinho e avisos caso o item esteja indisponível
         ou com quantidade inválida. Assim o usuário fica ciente do erro e decide oque fazer,
         e não simplesmente some com o item do carrinho*/
        private static ItemCarrinhoResponseDTO MontarItem(ItemCarrinho item)
        {
            var disponivel = item.Produto.Ativo && item.Produto.Disponivel;

            /*Cria uma lista de avisos gerais que se inicia null -> a cada verificação,
             se estiver algo errado com o Produto/ItemCarrinho ele adiciona um novo aviso
             para retornar ao usuário no carrinho*/
            string? aviso = null;

            if (!disponivel)
                aviso = "Este produto ficou indisponível. Remova-o para continuar.";
            else if (item.Quantidade < 1 || item.Quantidade > QuantidadeMaxima)
                aviso = $"Ajuste a quantidade para um valor entre 1 e {QuantidadeMaxima}.";

            /*Pega as propriedades do produto de cada item que tem no
            ItemCarrinho do banco e joga para as propriedades do DTO.*/
            return new ItemCarrinhoResponseDTO
            {
                ProdutoId = item.ProdutoId,
                NomeProduto = item.Produto.Nome,
                ImagemUrl = item.Produto.Imagem,
                PrecoUnitario = item.Produto.Preco,
                Quantidade = item.Quantidade,
                Subtotal = item.Quantidade * item.Produto.Preco, /*Multiplica a quantidade de item dentro do ItemCarrinho
                                                                    pelo preço do produto.*/
                DisponivelParaCompra = disponivel,
                Aviso = aviso
            };
        }

        //Pega o carrinho encontrado pelo BuscarVisitante/BuscarEntidade e transforma em um CarrinhoResponseDTO.
        private async Task <CarrinhoResponseDTO> MontarResposta(AcessoCarrinho acesso, Carrinho? carrinho)
        {
            var itens = carrinho?.ItensCarrinho //"?" -> Só acesse ItensCarrinho se carrinho não for null.
                .Select(MontarItem) //Transforma cada item do visitante/usuário em um ItemCarrinhoResponseDTO.
                .ToList()//Faz uma lista com os itens.
                ?? new List<ItemCarrinhoResponseDTO>(); //"??" -> Se a lista for null, use uma lista nova vazia.

            //Só preciso verificar mesclagem pendente se a pessoa estiver logada.
            var mesclagemPendente = false;

            //Se usuário está logado e o carrinho do visitante for diferente de null, temos uma mesclagem pendente.
            if (acesso.UsuarioId.HasValue)
                mesclagemPendente = await BuscarVisitante(acesso.CarrinhoVisitanteId) != null;

            var avisos = new List<string>();
            if (itens.Count == 0)
                avisos.Add("Seu carrinho está vazio.");

            /*Passe por cada item do carrinho. Se aquele item tiver um aviso,
             coloque o aviso na lista geral de avisos.*/
            foreach (var item in itens)
                if (item.Aviso != null)
                    avisos.Add($"{item.NomeProduto}: {item.Aviso}");

            if (mesclagemPendente)
                avisos.Add("Conclua a união com o carrinho de visitante antes de continuar.");

            //Instancia um CarrinhoDTO e joga o id do carrinho do banco dentro dele.
            return new CarrinhoResponseDTO
            {
                CarrinhoId = carrinho?.Id,
                //A lista de itens do DTO vai receber a lista de ItensDTO criado agora.
                Itens = itens,
                //Soma todos os subtotais dos itens e joga o resultado dentro de Total do DTO.
                Total = itens.Sum(item => item.Subtotal),
                //Retorna como "notificação" a quantidade de item total em cima do icone de carrinho.
                QuantidadeTotal = itens.Sum(i => i.Quantidade),
                //Verifica se precisa de login para ir pro checkout.
                RequerLogin = !acesso.UsuarioId.HasValue,
                MesclagemPendente = mesclagemPendente,
                /*Pode continuar pro checkout somente se todas as condições forem satisfeitas:
                Tem pelo menos um item -> Nenhum item possui aviso -> Não possui uma mesclagem pendente.*/
                PodeProsseguir = itens.Count > 0
                && itens.All(i => i.Aviso == null)
                && !mesclagemPendente,
                //Coloca a lista de avisos dentro da resposta
                Avisos = avisos
            };
        }

        //comparar os dois carrinhos e descobrir quais produtos ultrapassam 30 unidades.
        private static List<ConflitoMesclagemDTO> EncontrarConflitos(Carrinho visitante, Carrinho usuario)
        {
            var conflitos = new List<ConflitoMesclagemDTO>();//Essa lista vai guardar produtos cuja quantidade passou de 30

            //Percorre por cada item do visitante.
            foreach (var itemVisitante in visitante.ItensCarrinho)
            {
                //Para cada item do visitante, verifica se o usuário também tem o mesmo produto.
                var itemUsuario = usuario.ItensCarrinho
                    .SingleOrDefault(i => i.ProdutoId == itemVisitante.ProdutoId);

                //Se o usuário tiver esse produto, pegue a quantidade. Se não tiver, considere zero.
                var quantidadeUsuario = itemUsuario?.Quantidade ?? 0;

                //Soma a quantidade do item do usuário com o de visitante
                var soma = quantidadeUsuario + itemVisitante.Quantidade;

                //Se a soma passar do limite máximo -> adiciona um conflito com as informações do item em questão.
                if (soma > QuantidadeMaxima)
                {
                    conflitos.Add(new ConflitoMesclagemDTO
                    {
                        ProdutoId = itemVisitante.ProdutoId,
                        NomeProduto = itemVisitante.Produto.Nome,
                        QuantidadeVisitante = itemVisitante.Quantidade,
                        QuantidadeUsuario = quantidadeUsuario,
                        QuantidadeSomada = soma
                    });
                }
                //Repete o processo com o próximo item de visitante.
            }
            return conflitos;
        }

        //verifica se as escolhas enviadas pelo cliente fazem sentido.
        private static void ValidarAjustes(MesclarCarrinhoRequestDTO dto, List<ConflitoMesclagemDTO> conflitos)
        {
            var ajustes = dto.QuantidadesResolvidas
                          ?? throw new ArgumentException("Informe as quantidades resolvidas.");

            /*Aceitamos ajustes apenas para produtos que realmente
            apresentaram conflito nesta tentativa.*/
            foreach (var ajuste in ajustes)
            {
                //Dentro de ajuste tem algum produto que NÃO tenha nenhum conflito.ProdutoId equivalente?
                if (!conflitos.Any(c => c.ProdutoId == ajuste.Key))
                    throw new ArgumentException("Os conflitos mudaram. Atualize a revisão da mesclagem.");

                //Verifica se o valor escolhido do usuário é valido
                ValidarQuantidade(ajuste.Value);
            }
        }

        private static bool ExistemConflitosPendentes(List<ConflitoMesclagemDTO> conflitos, MesclarCarrinhoRequestDTO dto)
        {
            /*Verifica se tem algum item dentro da lista de conflitos
            que ainda não esteja na lista dos ajustes feitos pelo cliente.*/
            return conflitos.Any(c => !dto.QuantidadesResolvidas.ContainsKey(c.ProdutoId));
        }

        private static void MesclarItens(Carrinho visitante, Carrinho usuario, Dictionary<int, int> ajustes)
        {
            foreach (var itemVisitante in visitante.ItensCarrinho)
            {
                var itemUsuario = usuario.ItensCarrinho
                    .SingleOrDefault(i => i.ProdutoId == itemVisitante.ProdutoId);

                var quantidadeFinal = (itemUsuario?.Quantidade ?? 0) + itemVisitante.Quantidade;

                //Usa a quantidade final escolhida pelo cliente
                if (ajustes.TryGetValue(itemVisitante.ProdutoId, out var quantidadeEscolhida))
                    quantidadeFinal = quantidadeEscolhida;

                //Verifica se a quantidade escolhida pelo cliente é válida.
                ValidarQuantidade(quantidadeFinal);

                if (itemUsuario == null)
                {
                    usuario.ItensCarrinho.Add(new ItemCarrinho
                    {
                        ProdutoId = itemVisitante.ProdutoId,
                        Produto = itemVisitante.Produto,
                        Quantidade = quantidadeFinal
                    });
                }
                else
                {
                    itemUsuario.Quantidade =  quantidadeFinal;
                }
            }
        }

        public async Task<CarrinhoResponseDTO> BuscarCarrinho(AcessoCarrinho acesso)
        {
            var carrinho = await BuscarEntidade(acesso);
            return await MontarResposta(acesso, carrinho);
        }

        public Task<CarrinhoResponseDTO> RevisarCarrinho(AcessoCarrinho acesso)
        {
            /*A revisão utiliza a mesma consulta e as mesmas regras.
            Em uma nova requisição, os produtos são lidos novamente
            do banco, incluindo preço e disponibilidade atuais.
            Vai ser utilizado em PedidoService*/
            return BuscarCarrinho(acesso);
        }

        public async Task<CarrinhoResponseDTO> AdicionarProduto(AcessoCarrinho acesso,
            AdicionarProdutoCarrinhoRequestDTO dto)
        {
            //Valida a quantidade adicionada ao carrinho.
            ValidarQuantidade(dto.Quantidade);
            
            if (dto.ProdutoId <= 0)
                throw new ArgumentException("Informe um produto válido");
            
            //Consulta o banco para encontrar o produto com o ID recebido.
            var produto = await _context.Produtos.SingleOrDefaultAsync(p => p.Id == dto.ProdutoId);
            if(produto == null)
                throw new KeyNotFoundException("Produto não encontrado");
            
            //Verifica se o produto está ativo ou disponível.
            ValidarProduto(produto);
            
            /*Depois que passa de todas as verificações: Quantidade permitida -> Produto válido e disponível
            -> Usuário/visitante existe -> Vamos procurar o carrinho do usuário/visitante para adicionar o produto,
            se não for encontrado o carrinho é porque é a primeira vez do cliente no site ou seu carrinho foi expirado,
            sendo assim, criamos um novo carrinho para ele e atribuimos o ID dele ao carrinho.*/
            var carrinho = await BuscarEntidade(acesso);
            if (carrinho == null)
            {
                carrinho = new Carrinho
                {
                    UsuarioId = acesso.UsuarioId,
                };
                //Solicita que adicione o carrinho no banco porem ainda não salva.
                _context.Carrinhos.Add(carrinho);
            }
            
            /*Verifica se o carrinho já possuí o item selecionado, se sim -> soma a quantidade do carrinho
             com a requisição atual, se não -> Cria um novo item com a quantidade escolhida pelo usuário.*/
            var itemExistente = carrinho.ItensCarrinho.
                SingleOrDefault(i => i.ProdutoId == dto.ProdutoId);

            if (itemExistente == null)
            {
                carrinho.ItensCarrinho.Add(new ItemCarrinho
                {
                    ProdutoId = produto.Id,
                    Produto = produto,
                    Quantidade = dto.Quantidade
                });
            }
            else
            {
                var novaQuantidade = itemExistente.Quantidade + dto.Quantidade;
                ValidarQuantidade(novaQuantidade);
                itemExistente.Quantidade = novaQuantidade;
            }

            //Cria uma versão nova do carrinho.
            MarcarAlteracao(carrinho);
            await _context.SaveChangesAsync();
            /*Transforma uma entidade Carrinho, com seus itens e produtos
            carregados, no DTO que será devolvido ao cliente.*/
            return await MontarResposta(acesso, carrinho);
        }

        public async Task<CarrinhoResponseDTO> AtualizarQuantidade(AcessoCarrinho acesso, int produtoId,
            ItemCarrinhoUpdateDTO dto)
        {
            ValidarQuantidade(dto.Quantidade);
            var carrinho = await ExigirCarrinho(acesso);
            
            var item = carrinho.ItensCarrinho.SingleOrDefault(i => i.ProdutoId == produtoId);

            if (item == null)
                throw new KeyNotFoundException("Produto não encontrado no carrinho.");
            
            ValidarProduto(item.Produto);
            item.Quantidade = dto.Quantidade;
            MarcarAlteracao(carrinho);
            await _context.SaveChangesAsync();
            
            return await MontarResposta(acesso, carrinho);
        }

        public async Task<CarrinhoResponseDTO> RemoverProduto(AcessoCarrinho acesso, int produtoId)
        {
            var carrinho = await ExigirCarrinho(acesso);
            
            var item = carrinho.ItensCarrinho.SingleOrDefault(i => i.ProdutoId == produtoId);
            if (item == null)
                throw new KeyNotFoundException("Produto não encontrado no carrinho.");

            //Remover continua permitido mesmo se o produto estiver indisponível.
            _context.ItensCarrinho.Remove(item);
            carrinho.ItensCarrinho.Remove(item);
            MarcarAlteracao(carrinho);
            await _context.SaveChangesAsync();
            return await MontarResposta(acesso, carrinho);
        }

        public async Task<CarrinhoResponseDTO> EsvaziarCarrinho(AcessoCarrinho acesso)
        {
            var carrinho = await ExigirCarrinho(acesso);
            _context.ItensCarrinho.RemoveRange(carrinho.ItensCarrinho);
            
            carrinho.ItensCarrinho.Clear();

            MarcarAlteracao(carrinho);
            await _context.SaveChangesAsync();
            return await MontarResposta(acesso, carrinho);
        }

        public async Task<MesclagemCarrinhoResponseDTO> MesclarCarrinhos(AcessoCarrinho acesso,
            MesclarCarrinhoRequestDTO dto)
        {
            //APENAS USUÁRIOS LOGADOS PODEM MESCLAR CARRINHO.
            if (!acesso.UsuarioId.HasValue)
                throw new UnauthorizedAccessException();

            //Se é somente possível mesclar após logar -> usuario sempre vai ter um ID.
            var usuarioId = acesso.UsuarioId.Value;
            await ValidarUsuario(usuarioId);

            //Busca o carrinho do visitante e do usuário e salva em variáveis.
            var visitante = await BuscarVisitante(acesso.CarrinhoVisitanteId);
            var usuario = await ConsultaCompleta()
                .SingleOrDefaultAsync(c => c.UsuarioId == usuarioId);

            /*Não existe carrinho visitante. Pode ser um usuário logando direto antes
             de colocar algo no carrinho, carrinho expirado ou uma repetição de uma mesclagem
             já concluída. Não há nada para mesclar.*/
            if (visitante == null)
                return new MesclagemCarrinhoResponseDTO
                {
                    Concluida = true,
                    Carrinho = await MontarResposta(acesso, usuario)
                };

            /*O usuário não tem carrinho: associamos o próprio
             carrinho visitante à conta.*/
            if (usuario == null)
            {
                visitante.UsuarioId = usuarioId;
                MarcarAlteracao(visitante);//Carrinho não expira mais
                await _context.SaveChangesAsync();
                return new MesclagemCarrinhoResponseDTO
                {
                    Concluida = true,
                    Carrinho = await MontarResposta(acesso, visitante)
                };
            }

            //Descobre quais produtos ultrapassam o limite quando os dois carrinhos são somados.
            var conflitos = EncontrarConflitos(visitante, usuario);

            //Verifica se os ajustes enviados pelo cliente são válidos.
            ValidarAjustes(dto, conflitos);

            //Se ainda existir algum conflito sem decisão, retorna os conflitos sem alterar os carrinhos.
            if (ExistemConflitosPendentes(conflitos, dto))
            {
                return new MesclagemCarrinhoResponseDTO
                {
                    Concluida = false,
                    Conflitos = conflitos
                };
            }

            //Agora que todos os conflitos foram resolvidos, realiza a mesclagem dos itens.
            MesclarItens(visitante, usuario, dto.QuantidadesResolvidas);

            /*Produtos indisponíveis são preservados na mesclagem.
            MontarResposta apresentará os avisos correspondentes.*/

            MarcarAlteracao(usuario);

            // Remove o carrinho visitante, que já foi incorporado.
            _context.ItensCarrinho.RemoveRange(visitante.ItensCarrinho);
            _context.Carrinhos.Remove(visitante);

            //A união e a exclusão do carrinho de origem são gravadas juntas.
            await _context.SaveChangesAsync();

            // A partir daqui, só existe o carrinho do usuário.
            var acessoFinal = new AcessoCarrinho
            {
                UsuarioId = usuarioId
            };

            // A partir daqui, só existe o carrinho do usuário.
            return new MesclagemCarrinhoResponseDTO
            {
                Concluida = true,
                Carrinho = await MontarResposta(acessoFinal, usuario)
            };
        }
    }
}