using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KioraRestaurante.Migrations
{
    /// <inheritdoc />
    public partial class HistoricoDescontosSalao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DescontoPercentual",
                table: "ItensComanda",
                type: "decimal(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PrecoOriginalUnitario",
                table: "ItensComanda",
                type: "decimal(10,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DescontoPercentual",
                table: "ItensComanda");

            migrationBuilder.DropColumn(
                name: "PrecoOriginalUnitario",
                table: "ItensComanda");
        }
    }
}
