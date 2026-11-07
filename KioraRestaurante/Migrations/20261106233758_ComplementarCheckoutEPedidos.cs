using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KioraRestaurante.Migrations
{
    /// <inheritdoc />
    public partial class ComplementarCheckoutEPedidos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CanceladoEmUtc",
                table: "Pedidos",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DuracaoPercursoSegundos",
                table: "Pedidos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoCancelamento",
                table: "Pedidos",
                type: "varchar(300)",
                maxLength: 300,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<Guid>(
                name: "OperacaoPendenteId",
                table: "Pedidos",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<int>(
                name: "PreparoMinutos",
                table: "Pedidos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ReembolsoCancelamentoPendente",
                table: "Pedidos",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "RevisaoFinanceira",
                table: "Pedidos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "Versao",
                table: "Pedidos",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<int>(
                name: "RevisaoFinanceira",
                table: "PagamentosOnline",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "OperacoesPedido",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PedidoId = table.Column<int>(type: "int", nullable: false),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    CriadaEmUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ConcluidaEmUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ProcessandoAteUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ConferidaEmUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    DesistenciaSolicitada = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    PagamentoConfirmado = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    EnderecoAnteriorJson = table.Column<string>(type: "text", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NovoEnderecoJson = table.Column<string>(type: "text", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EnderecoSalvarJson = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TaxaAnterior = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    NovaTaxa = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    DistanciaMetros = table.Column<int>(type: "int", nullable: false),
                    DuracaoSegundos = table.Column<int>(type: "int", nullable: false),
                    RevisaoFinanceiraAplicada = table.Column<int>(type: "int", nullable: true),
                    Motivo = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Mensagem = table.Column<string>(type: "varchar(400)", maxLength: 400, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PagamentoAnteriorJson = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Versao = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperacoesPedido", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperacoesPedido_Pedidos_PedidoId",
                        column: x => x.PedidoId,
                        principalTable: "Pedidos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "MovimentosPedido",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    OperacaoPedidoId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    Valor = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    OrderId = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TransacaoId = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReembolsoId = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CheckoutUrl = table.Column<string>(type: "varchar(2048)", maxLength: 2048, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Corpo = table.Column<string>(type: "text", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReembolsosAnterioresJson = table.Column<string>(type: "text", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CriadoEmUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ConfirmadoEmUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Versao = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimentosPedido", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MovimentosPedido_OperacoesPedido_OperacaoPedidoId",
                        column: x => x.OperacaoPedidoId,
                        principalTable: "OperacoesPedido",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_MovimentosPedido_OperacaoPedidoId",
                table: "MovimentosPedido",
                column: "OperacaoPedidoId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimentosPedido_OrderId",
                table: "MovimentosPedido",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OperacoesPedido_Estado_ConferidaEmUtc",
                table: "OperacoesPedido",
                columns: new[] { "Estado", "ConferidaEmUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OperacoesPedido_PedidoId",
                table: "OperacoesPedido",
                column: "PedidoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MovimentosPedido");

            migrationBuilder.DropTable(
                name: "OperacoesPedido");

            migrationBuilder.DropColumn(
                name: "CanceladoEmUtc",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "DuracaoPercursoSegundos",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "MotivoCancelamento",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "OperacaoPendenteId",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "PreparoMinutos",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "ReembolsoCancelamentoPendente",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "RevisaoFinanceira",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "Versao",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "RevisaoFinanceira",
                table: "PagamentosOnline");
        }
    }
}
