using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KioraRestaurante.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarChaveConfirmacaoPedido : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ChaveConfirmacao",
                table: "Pedidos",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_UsuarioId_ChaveConfirmacao",
                table: "Pedidos",
                columns: new[] { "UsuarioId", "ChaveConfirmacao" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Pedidos_UsuarioId_ChaveConfirmacao",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "ChaveConfirmacao",
                table: "Pedidos");
        }
    }
}
