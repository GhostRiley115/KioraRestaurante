using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KioraRestaurante.Migrations
{
    /// <inheritdoc />
    public partial class FortalecerSegurancaDispositivosPush : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "Versao",
                table: "DispositivosPush",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "VersaoSessaoUsuario",
                table: "DispositivosPush",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Versao",
                table: "DispositivosPush");

            migrationBuilder.DropColumn(
                name: "VersaoSessaoUsuario",
                table: "DispositivosPush");
        }
    }
}
