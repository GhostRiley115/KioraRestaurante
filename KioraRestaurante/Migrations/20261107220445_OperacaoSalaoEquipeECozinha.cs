using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KioraRestaurante.Migrations
{
    /// <inheritdoc />
    public partial class OperacaoSalaoEquipeECozinha : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(name: "VersaoFuncionario",table: "Usuarios",type: "char(36)",nullable: false,
                defaultValue: Guid.Empty,collation: "ascii_general_ci");
            migrationBuilder.Sql("UPDATE Usuarios SET VersaoFuncionario=UUID();");
            migrationBuilder.AddColumn<bool>(
                name: "ConvitePendente",
                table: "Usuarios",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TelefoneFuncionario",
                table: "Usuarios",
                type: "varchar(11)",
                maxLength: 11,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<Guid>(
                name: "AtendimentoId",
                table: "Notificacoes",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "Versao",
                table: "MesasRestaurante",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<DateTime>(
                name: "IniciadoEmUtc",
                table: "ItensPedido",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProntoEmUtc",
                table: "ItensPedido",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SituacaoPreparo",
                table: "ItensPedido",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "VersaoPreparo",
                table: "ItensPedido",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.CreateTable(
                name: "AtendimentosMesa",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    NomeMesa = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MesaId = table.Column<int>(type: "int", nullable: false),
                    MesaOcupadaId = table.Column<int>(type: "int", nullable: true),
                    ReservaId = table.Column<int>(type: "int", nullable: true),
                    GarcomId = table.Column<int>(type: "int", nullable: false),
                    NomeGrupo = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Pessoas = table.Column<int>(type: "int", nullable: false),
                    Situacao = table.Column<int>(type: "int", nullable: false),
                    AbertoEmUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    FechadoEmUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LiberadoEmUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    FechadoPorId = table.Column<int>(type: "int", nullable: true),
                    GarcomFechamento = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SubtotalFechamento = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    TotalFechamento = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    TipoDesconto = table.Column<int>(type: "int", nullable: true),
                    ValorDescontoInformado = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    MotivoDesconto = table.Column<string>(type: "varchar(400)", maxLength: 400, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DescontoPorId = table.Column<int>(type: "int", nullable: true),
                    Versao = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AtendimentosMesa", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AtendimentosMesa_MesasRestaurante_MesaId",
                        column: x => x.MesaId,
                        principalTable: "MesasRestaurante",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AtendimentosMesa_MesasRestaurante_MesaOcupadaId",
                        column: x => x.MesaOcupadaId,
                        principalTable: "MesasRestaurante",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AtendimentosMesa_Reservas_ReservaId",
                        column: x => x.ReservaId,
                        principalTable: "Reservas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AtendimentosMesa_Usuarios_GarcomId",
                        column: x => x.GarcomId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ConvitesFuncionarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    CriadoPorId = table.Column<int>(type: "int", nullable: false),
                    TokenHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    VersaoSessao = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CriadoEmUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ExpiraEmUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UsadoEmUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConvitesFuncionarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConvitesFuncionarios_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ItensComanda",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    AtendimentoId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    RodadaId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ProdutoId = table.Column<int>(type: "int", nullable: false),
                    NomeProduto = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Quantidade = table.Column<int>(type: "int", nullable: false),
                    PrecoUnitario = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Observacao = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Composicao = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ImagemUrl = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ImagemPublicId = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Situacao = table.Column<int>(type: "int", nullable: false),
                    EnviadoEmUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    IniciadoEmUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ProntoEmUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    EntregueEmUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CanceladoEmUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    MotivoCancelamento = table.Column<string>(type: "varchar(400)", maxLength: 400, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SubstituiItemId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    Versao = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItensComanda", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItensComanda_AtendimentosMesa_AtendimentoId",
                        column: x => x.AtendimentoId,
                        principalTable: "AtendimentosMesa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ItensComanda_Produtos_ProdutoId",
                        column: x => x.ProdutoId,
                        principalTable: "Produtos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "MovimentosSalao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    AtendimentoId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    ResultadoId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    Acao = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DadosHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Responsavel = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Descricao = table.Column<string>(type: "varchar(1500)", maxLength: 1500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CriadoEmUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimentosSalao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MovimentosSalao_AtendimentosMesa_AtendimentoId",
                        column: x => x.AtendimentoId,
                        principalTable: "AtendimentosMesa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimentosSalao_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PagamentosPresenciais",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    AtendimentoId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Meio = table.Column<int>(type: "int", nullable: false),
                    Valor = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Referencia = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PagamentosPresenciais", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PagamentosPresenciais_AtendimentosMesa_AtendimentoId",
                        column: x => x.AtendimentoId,
                        principalTable: "AtendimentosMesa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "MesasRestaurante",
                keyColumn: "Id",
                keyValue: 1,
                column: "Versao",
                value: new Guid("00000000-0000-4000-8000-000000000001"));

            migrationBuilder.UpdateData(
                table: "MesasRestaurante",
                keyColumn: "Id",
                keyValue: 2,
                column: "Versao",
                value: new Guid("00000000-0000-4000-8000-000000000002"));

            migrationBuilder.UpdateData(
                table: "MesasRestaurante",
                keyColumn: "Id",
                keyValue: 3,
                column: "Versao",
                value: new Guid("00000000-0000-4000-8000-000000000003"));

            migrationBuilder.UpdateData(
                table: "MesasRestaurante",
                keyColumn: "Id",
                keyValue: 4,
                column: "Versao",
                value: new Guid("00000000-0000-4000-8000-000000000004"));

            migrationBuilder.UpdateData(
                table: "MesasRestaurante",
                keyColumn: "Id",
                keyValue: 5,
                column: "Versao",
                value: new Guid("00000000-0000-4000-8000-000000000005"));

            migrationBuilder.UpdateData(
                table: "MesasRestaurante",
                keyColumn: "Id",
                keyValue: 6,
                column: "Versao",
                value: new Guid("00000000-0000-4000-8000-000000000006"));

            migrationBuilder.UpdateData(
                table: "MesasRestaurante",
                keyColumn: "Id",
                keyValue: 7,
                column: "Versao",
                value: new Guid("00000000-0000-4000-8000-000000000007"));

            migrationBuilder.UpdateData(
                table: "MesasRestaurante",
                keyColumn: "Id",
                keyValue: 8,
                column: "Versao",
                value: new Guid("00000000-0000-4000-8000-000000000008"));

            migrationBuilder.UpdateData(
                table: "MesasRestaurante",
                keyColumn: "Id",
                keyValue: 9,
                column: "Versao",
                value: new Guid("00000000-0000-4000-8000-000000000009"));

            migrationBuilder.UpdateData(
                table: "MesasRestaurante",
                keyColumn: "Id",
                keyValue: 10,
                column: "Versao",
                value: new Guid("00000000-0000-4000-8000-000000000010"));

            migrationBuilder.UpdateData(
                table: "MesasRestaurante",
                keyColumn: "Id",
                keyValue: 11,
                column: "Versao",
                value: new Guid("00000000-0000-4000-8000-000000000011"));

            migrationBuilder.UpdateData(
                table: "MesasRestaurante",
                keyColumn: "Id",
                keyValue: 12,
                column: "Versao",
                value: new Guid("00000000-0000-4000-8000-000000000012"));

            migrationBuilder.CreateIndex(
                name: "IX_Notificacoes_AtendimentoId",
                table: "Notificacoes",
                column: "AtendimentoId");

            migrationBuilder.CreateIndex(
                name: "IX_AtendimentosMesa_FechadoEmUtc",
                table: "AtendimentosMesa",
                column: "FechadoEmUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AtendimentosMesa_GarcomId",
                table: "AtendimentosMesa",
                column: "GarcomId");

            migrationBuilder.CreateIndex(
                name: "IX_AtendimentosMesa_MesaId",
                table: "AtendimentosMesa",
                column: "MesaId");

            migrationBuilder.CreateIndex(
                name: "IX_AtendimentosMesa_MesaOcupadaId",
                table: "AtendimentosMesa",
                column: "MesaOcupadaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AtendimentosMesa_ReservaId",
                table: "AtendimentosMesa",
                column: "ReservaId");

            migrationBuilder.CreateIndex(
                name: "IX_ConvitesFuncionarios_TokenHash",
                table: "ConvitesFuncionarios",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConvitesFuncionarios_UsuarioId",
                table: "ConvitesFuncionarios",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_ItensComanda_AtendimentoId",
                table: "ItensComanda",
                column: "AtendimentoId");

            migrationBuilder.CreateIndex(
                name: "IX_ItensComanda_ProdutoId",
                table: "ItensComanda",
                column: "ProdutoId");

            migrationBuilder.CreateIndex(
                name: "IX_ItensComanda_Situacao_EnviadoEmUtc",
                table: "ItensComanda",
                columns: new[] { "Situacao", "EnviadoEmUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MovimentosSalao_AtendimentoId",
                table: "MovimentosSalao",
                column: "AtendimentoId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimentosSalao_UsuarioId",
                table: "MovimentosSalao",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_PagamentosPresenciais_AtendimentoId",
                table: "PagamentosPresenciais",
                column: "AtendimentoId");

            migrationBuilder.AddForeignKey(
                name: "FK_Notificacoes_AtendimentosMesa_AtendimentoId",
                table: "Notificacoes",
                column: "AtendimentoId",
                principalTable: "AtendimentosMesa",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            // Pedidos antigos mantêm seu andamento; não voltam à fila como se tivessem acabado de chegar.
            migrationBuilder.Sql("""
                UPDATE ItensPedido i INNER JOIN Pedidos p ON p.Id=i.PedidoId
                SET i.SituacaoPreparo=CASE WHEN p.StatusPedido IN ('SaiuParaEntrega','Entregue') THEN 2
                    WHEN p.StatusPedido='Cancelado' THEN 4 WHEN p.StatusPedido='EmPreparo' THEN 1 ELSE 0 END,
                    i.VersaoPreparo=UUID();
                UPDATE MesasRestaurante SET Versao=UUID();
                UPDATE Reservas SET Situacao='AguardandoChegada', MesaId=NULL, FimUtc=NULL, Versao=UUID()
                WHERE Situacao='Confirmada';
                """);
            // Atendimentos já ocupados ficam bloqueados para o mesmo grupo, sem inventar vendas passadas.
            migrationBuilder.Sql("""
                INSERT INTO AtendimentosMesa
                    (Id,NomeMesa,MesaId,MesaOcupadaId,ReservaId,GarcomId,NomeGrupo,Pessoas,Situacao,AbertoEmUtc,
                     SubtotalFechamento,TotalFechamento,ValorDescontoInformado,Versao)
                SELECT UUID(),m.Nome,r.MesaId,r.MesaId,r.Id,
                    (SELECT MIN(Id) FROM Usuarios WHERE Tipo='Administrador' AND Ativo=1),
                    r.Nome,r.Pessoas,0,COALESCE(r.ChegadaEmUtc,r.CriadaEmUtc),0,0,0,UUID()
                FROM Reservas r INNER JOIN MesasRestaurante m ON m.Id=r.MesaId
                WHERE r.Situacao='EmAtendimento';
                UPDATE Reservas SET FimUtc=NULL WHERE Situacao='EmAtendimento';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name:"VersaoFuncionario",table:"Usuarios");
            migrationBuilder.DropForeignKey(
                name: "FK_Notificacoes_AtendimentosMesa_AtendimentoId",
                table: "Notificacoes");

            migrationBuilder.DropTable(
                name: "ConvitesFuncionarios");

            migrationBuilder.DropTable(
                name: "ItensComanda");

            migrationBuilder.DropTable(
                name: "MovimentosSalao");

            migrationBuilder.DropTable(
                name: "PagamentosPresenciais");

            migrationBuilder.DropTable(
                name: "AtendimentosMesa");

            migrationBuilder.DropIndex(
                name: "IX_Notificacoes_AtendimentoId",
                table: "Notificacoes");

            migrationBuilder.DropColumn(
                name: "ConvitePendente",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "TelefoneFuncionario",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "AtendimentoId",
                table: "Notificacoes");

            migrationBuilder.DropColumn(
                name: "Versao",
                table: "MesasRestaurante");

            migrationBuilder.DropColumn(
                name: "IniciadoEmUtc",
                table: "ItensPedido");

            migrationBuilder.DropColumn(
                name: "ProntoEmUtc",
                table: "ItensPedido");

            migrationBuilder.DropColumn(
                name: "SituacaoPreparo",
                table: "ItensPedido");

            migrationBuilder.DropColumn(
                name: "VersaoPreparo",
                table: "ItensPedido");
        }
    }
}
