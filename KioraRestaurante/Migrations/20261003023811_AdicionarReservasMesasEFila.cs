using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace KioraRestaurante.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarReservasMesasEFila : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ControlesSala",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ControlesSala", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "MesasRestaurante",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Nome = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Capacidade = table.Column<int>(type: "int", nullable: false),
                    Ativa = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MesasRestaurante", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Reservas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Codigo = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ChaveSolicitacao = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UsuarioId = table.Column<int>(type: "int", nullable: true),
                    VisitanteHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Nome = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Telefone = table.Column<string>(type: "varchar(11)", maxLength: 11, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Pessoas = table.Column<int>(type: "int", nullable: false),
                    Tipo = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Situacao = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MesaId = table.Column<int>(type: "int", nullable: true),
                    CriadaEmUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    InicioUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    FimUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ExpiraEmUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ChegadaEmUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    EncerradaEmUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Versao = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reservas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Reservas_MesasRestaurante_MesaId",
                        column: x => x.MesaId,
                        principalTable: "MesasRestaurante",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reservas_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "EventosReserva",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ReservaId = table.Column<int>(type: "int", nullable: false),
                    AdministradorId = table.Column<int>(type: "int", nullable: true),
                    DataUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Descricao = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventosReserva", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventosReserva_Reservas_ReservaId",
                        column: x => x.ReservaId,
                        principalTable: "Reservas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "ControlesSala",
                column: "Id",
                value: 1);

            migrationBuilder.InsertData(
                table: "MesasRestaurante",
                columns: new[] { "Id", "Ativa", "Capacidade", "Nome" },
                values: new object[,]
                {
                    { 1, true, 2, "Mesa 01" },
                    { 2, true, 2, "Mesa 02" },
                    { 3, true, 2, "Mesa 03" },
                    { 4, true, 2, "Mesa 04" },
                    { 5, true, 4, "Mesa 05" },
                    { 6, true, 4, "Mesa 06" },
                    { 7, true, 4, "Mesa 07" },
                    { 8, true, 4, "Mesa 08" },
                    { 9, true, 4, "Mesa 09" },
                    { 10, true, 4, "Mesa 10" },
                    { 11, true, 6, "Mesa 11" },
                    { 12, true, 6, "Mesa 12" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_EventosReserva_ReservaId",
                table: "EventosReserva",
                column: "ReservaId");

            migrationBuilder.CreateIndex(
                name: "IX_Reservas_ChaveSolicitacao",
                table: "Reservas",
                column: "ChaveSolicitacao",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reservas_Codigo",
                table: "Reservas",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reservas_MesaId_InicioUtc",
                table: "Reservas",
                columns: new[] { "MesaId", "InicioUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Reservas_Situacao_ExpiraEmUtc",
                table: "Reservas",
                columns: new[] { "Situacao", "ExpiraEmUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Reservas_UsuarioId",
                table: "Reservas",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Reservas_VisitanteHash",
                table: "Reservas",
                column: "VisitanteHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ControlesSala");

            migrationBuilder.DropTable(
                name: "EventosReserva");

            migrationBuilder.DropTable(
                name: "Reservas");

            migrationBuilder.DropTable(
                name: "MesasRestaurante");
        }
    }
}
