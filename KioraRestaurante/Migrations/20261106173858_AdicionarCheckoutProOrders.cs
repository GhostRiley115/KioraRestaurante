using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KioraRestaurante.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarCheckoutProOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PagamentosOnline",
                columns: table => new
                {
                    PedidoId = table.Column<int>(type: "int", nullable: false),
                    Referencia = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    OrderId = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CheckoutUrl = table.Column<string>(type: "varchar(2048)", maxLength: 2048, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Valor = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    CorpoCriacao = table.Column<string>(type: "text", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CriadoEmUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AtualizacaoExternaUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ConferidoEmUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    SituacaoExterna = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RevisaoNecessaria = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Versao = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PagamentosOnline", x => x.PedidoId);
                    table.ForeignKey(
                        name: "FK_PagamentosOnline_Pedidos_PedidoId",
                        column: x => x.PedidoId,
                        principalTable: "Pedidos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_PagamentosOnline_ConferidoEmUtc",
                table: "PagamentosOnline",
                column: "ConferidoEmUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PagamentosOnline_OrderId",
                table: "PagamentosOnline",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PagamentosOnline_Referencia",
                table: "PagamentosOnline",
                column: "Referencia",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PagamentosOnline");
        }
    }
}
