using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KioraRestaurante.Migrations
{
    /// <inheritdoc />
    public partial class CancelamentoAdministrativo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AdministradorSolicitanteId",
                table: "OperacoesPedido",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdministradorSolicitanteNome",
                table: "OperacoesPedido",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdministradorSolicitanteId",
                table: "OperacoesPedido");

            migrationBuilder.DropColumn(
                name: "AdministradorSolicitanteNome",
                table: "OperacoesPedido");
        }
    }
}
