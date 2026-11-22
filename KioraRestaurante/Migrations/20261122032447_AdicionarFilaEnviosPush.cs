using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KioraRestaurante.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarFilaEnviosPush : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EnviosPush",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    NotificacaoId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    DispositivoId = table.Column<long>(type: "bigint", nullable: false),
                    UsuarioId = table.Column<int>(type: "int", nullable: true),
                    VisitanteHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    VersaoSessaoUsuario = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    VersaoDispositivo = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Situacao = table.Column<int>(type: "int", nullable: false),
                    Tentativas = table.Column<int>(type: "int", nullable: false),
                    CriadoEmUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ProximaTentativaEmUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ExpiraEmUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UltimaTentativaEmUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    FinalizadoEmUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ProcessandoAteUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Concessao = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    Versao = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UltimoCodigoErro = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MensagemFirebaseId = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnviosPush", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnviosPush_DispositivosPush_DispositivoId",
                        column: x => x.DispositivoId,
                        principalTable: "DispositivosPush",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EnviosPush_Notificacoes_NotificacaoId",
                        column: x => x.NotificacaoId,
                        principalTable: "Notificacoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EnviosPush_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_EnviosPush_DispositivoId",
                table: "EnviosPush",
                column: "DispositivoId");

            migrationBuilder.CreateIndex(
                name: "IX_EnviosPush_NotificacaoId_DispositivoId",
                table: "EnviosPush",
                columns: new[] { "NotificacaoId", "DispositivoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EnviosPush_ProcessandoAteUtc",
                table: "EnviosPush",
                column: "ProcessandoAteUtc");

            migrationBuilder.CreateIndex(
                name: "IX_EnviosPush_Situacao_ExpiraEmUtc",
                table: "EnviosPush",
                columns: new[] { "Situacao", "ExpiraEmUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_EnviosPush_Situacao_ProximaTentativaEmUtc",
                table: "EnviosPush",
                columns: new[] { "Situacao", "ProximaTentativaEmUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_EnviosPush_UsuarioId_CriadoEmUtc",
                table: "EnviosPush",
                columns: new[] { "UsuarioId", "CriadoEmUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EnviosPush");
        }
    }
}
