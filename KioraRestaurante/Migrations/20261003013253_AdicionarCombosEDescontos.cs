using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KioraRestaurante.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarCombosEDescontos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DescontoPercentual",
                table: "Produtos",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "EhCombo",
                table: "Produtos",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Composicao",
                table: "ItensPedido",
                type: "text",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "DescontoPercentual",
                table: "ItensPedido",
                type: "decimal(5,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PrecoOriginalUnitario",
                table: "ItensPedido",
                type: "decimal(10,2)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ComboComponentes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ComboId = table.Column<int>(type: "int", nullable: false),
                    ProdutoId = table.Column<int>(type: "int", nullable: false),
                    Quantidade = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComboComponentes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComboComponentes_Produtos_ComboId",
                        column: x => x.ComboId,
                        principalTable: "Produtos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ComboComponentes_Produtos_ProdutoId",
                        column: x => x.ProdutoId,
                        principalTable: "Produtos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ComboComponentes_ComboId_ProdutoId",
                table: "ComboComponentes",
                columns: new[] { "ComboId", "ProdutoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ComboComponentes_ProdutoId",
                table: "ComboComponentes",
                column: "ProdutoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ComboComponentes");

            migrationBuilder.DropColumn(
                name: "DescontoPercentual",
                table: "Produtos");

            migrationBuilder.DropColumn(
                name: "EhCombo",
                table: "Produtos");

            migrationBuilder.DropColumn(
                name: "Composicao",
                table: "ItensPedido");

            migrationBuilder.DropColumn(
                name: "DescontoPercentual",
                table: "ItensPedido");

            migrationBuilder.DropColumn(
                name: "PrecoOriginalUnitario",
                table: "ItensPedido");
        }
    }
}
