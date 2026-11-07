using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KioraRestaurante.Migrations
{
    /// <inheritdoc />
    public partial class NotificacoesCuponsEFotosHistoricas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CodigoCupom",
                table: "Pedidos",
                type: "varchar(40)",
                maxLength: 40,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "DescontoCupom",
                table: "Pedidos",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ImagemPublicId",
                table: "ItensPedido",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ImagemUrl",
                table: "ItensPedido",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            // Para compras anteriores, registra a foto disponível no catálogo na implantação.
            // Uma imagem histórica já substituída antes desta versão não pode ser recuperada daqui.
            migrationBuilder.Sql("""
                UPDATE ItensPedido i INNER JOIN Produtos p ON p.Id = i.ProdutoId
                SET i.ImagemUrl = NULLIF(p.Imagem, ''), i.ImagemPublicId = p.ImagemPublicId
                WHERE i.ImagemUrl IS NULL AND i.ImagemPublicId IS NULL;
                """);

            migrationBuilder.CreateTable(
                name: "Cupons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Codigo = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Descricao = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Parceiro = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Valor = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    ValorMinimoProdutos = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    DescontoMaximo = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    InicioEmUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    FimEmUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LimiteTotal = table.Column<int>(type: "int", nullable: true),
                    LimitePorCliente = table.Column<int>(type: "int", nullable: false),
                    UtilizacoesAtivas = table.Column<int>(type: "int", nullable: false),
                    Ativo = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CriadoEmUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CriadoPorId = table.Column<int>(type: "int", nullable: false),
                    Versao = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cupons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cupons_Usuarios_CriadoPorId",
                        column: x => x.CriadoPorId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "EventosNotificacao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    PedidoId = table.Column<int>(type: "int", nullable: true),
                    ReservaId = table.Column<int>(type: "int", nullable: true),
                    CriadoEmUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ProcessadoEmUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ProcessandoAteUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Concessao = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventosNotificacao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventosNotificacao_Pedidos_PedidoId",
                        column: x => x.PedidoId,
                        principalTable: "Pedidos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EventosNotificacao_Reservas_ReservaId",
                        column: x => x.ReservaId,
                        principalTable: "Reservas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Notificacoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Publico = table.Column<int>(type: "int", nullable: false),
                    Titulo = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Mensagem = table.Column<string>(type: "varchar(1500)", maxLength: 1500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CriadaEmUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AutorId = table.Column<int>(type: "int", nullable: true),
                    PedidoId = table.Column<int>(type: "int", nullable: true),
                    ReservaId = table.Column<int>(type: "int", nullable: true),
                    CupomId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notificacoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notificacoes_Cupons_CupomId",
                        column: x => x.CupomId,
                        principalTable: "Cupons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Notificacoes_Pedidos_PedidoId",
                        column: x => x.PedidoId,
                        principalTable: "Pedidos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Notificacoes_Reservas_ReservaId",
                        column: x => x.ReservaId,
                        principalTable: "Reservas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Notificacoes_Usuarios_AutorId",
                        column: x => x.AutorId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "UsosCupom",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CupomId = table.Column<int>(type: "int", nullable: false),
                    PedidoId = table.Column<int>(type: "int", nullable: false),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Parceiro = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Desconto = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    Situacao = table.Column<int>(type: "int", nullable: false),
                    ReservadoEmUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ConfirmadoEmUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CanceladoEmUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Versao = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsosCupom", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UsosCupom_Cupons_CupomId",
                        column: x => x.CupomId,
                        principalTable: "Cupons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UsosCupom_Pedidos_PedidoId",
                        column: x => x.PedidoId,
                        principalTable: "Pedidos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UsosCupom_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "NotificacoesUsuarios",
                columns: table => new
                {
                    NotificacaoId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    LidaEmUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificacoesUsuarios", x => new { x.NotificacaoId, x.UsuarioId });
                    table.ForeignKey(
                        name: "FK_NotificacoesUsuarios_Notificacoes_NotificacaoId",
                        column: x => x.NotificacaoId,
                        principalTable: "Notificacoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NotificacoesUsuarios_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Cupons_Codigo",
                table: "Cupons",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cupons_CriadoPorId",
                table: "Cupons",
                column: "CriadoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_EventosNotificacao_PedidoId",
                table: "EventosNotificacao",
                column: "PedidoId");

            migrationBuilder.CreateIndex(
                name: "IX_EventosNotificacao_ProcessadoEmUtc_CriadoEmUtc",
                table: "EventosNotificacao",
                columns: new[] { "ProcessadoEmUtc", "CriadoEmUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_EventosNotificacao_ReservaId",
                table: "EventosNotificacao",
                column: "ReservaId");

            migrationBuilder.CreateIndex(
                name: "IX_Notificacoes_AutorId",
                table: "Notificacoes",
                column: "AutorId");

            migrationBuilder.CreateIndex(
                name: "IX_Notificacoes_CupomId",
                table: "Notificacoes",
                column: "CupomId");

            migrationBuilder.CreateIndex(
                name: "IX_Notificacoes_PedidoId",
                table: "Notificacoes",
                column: "PedidoId");

            migrationBuilder.CreateIndex(
                name: "IX_Notificacoes_ReservaId",
                table: "Notificacoes",
                column: "ReservaId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificacoesUsuarios_UsuarioId_LidaEmUtc",
                table: "NotificacoesUsuarios",
                columns: new[] { "UsuarioId", "LidaEmUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_UsosCupom_CupomId_UsuarioId_Situacao",
                table: "UsosCupom",
                columns: new[] { "CupomId", "UsuarioId", "Situacao" });

            migrationBuilder.CreateIndex(
                name: "IX_UsosCupom_PedidoId",
                table: "UsosCupom",
                column: "PedidoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UsosCupom_UsuarioId",
                table: "UsosCupom",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventosNotificacao");

            migrationBuilder.DropTable(
                name: "NotificacoesUsuarios");

            migrationBuilder.DropTable(
                name: "UsosCupom");

            migrationBuilder.DropTable(
                name: "Notificacoes");

            migrationBuilder.DropTable(
                name: "Cupons");

            migrationBuilder.DropColumn(
                name: "CodigoCupom",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "DescontoCupom",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "ImagemPublicId",
                table: "ItensPedido");

            migrationBuilder.DropColumn(
                name: "ImagemUrl",
                table: "ItensPedido");
        }
    }
}
