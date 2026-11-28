using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GenOnlineService.Migrations.MySql
{
    /// <inheritdoc />
    public partial class IllegalRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "anticheat_new_account_games");

            migrationBuilder.DropTable(
                name: "anticheat_quickmatch_reports");

            migrationBuilder.CreateTable(
                name: "anticheat_illegal_requests",
                columns: table => new
                {
                    report_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    match_id = table.Column<long>(type: "bigint", nullable: false),
                    lobby_id = table.Column<long>(type: "bigint", nullable: false),
                    request = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    detail = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    state = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    reviewed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    reviewed_by = table.Column<long>(type: "bigint", nullable: true),
                    case_id = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_anticheat_illegal_requests", x => x.report_id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_anticheat_illegal_requests_case",
                table: "anticheat_illegal_requests",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_anticheat_illegal_requests_reviewed_at",
                table: "anticheat_illegal_requests",
                column: "reviewed_at");

            migrationBuilder.CreateIndex(
                name: "ix_anticheat_illegal_requests_user_created",
                table: "anticheat_illegal_requests",
                columns: new[] { "user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ux_anticheat_illegal_requests_user_lobby_request",
                table: "anticheat_illegal_requests",
                columns: new[] { "user_id", "lobby_id", "request" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "anticheat_illegal_requests");

            migrationBuilder.CreateTable(
                name: "anticheat_new_account_games",
                columns: table => new
                {
                    report_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    case_id = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    match_id = table.Column<long>(type: "bigint", nullable: false),
                    reviewed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    reviewed_by = table.Column<long>(type: "bigint", nullable: true),
                    user_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_anticheat_new_account_games", x => x.report_id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "anticheat_quickmatch_reports",
                columns: table => new
                {
                    report_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    case_id = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    lobby_id = table.Column<long>(type: "bigint", nullable: false),
                    match_id = table.Column<long>(type: "bigint", nullable: false),
                    method = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    playlist_id = table.Column<int>(type: "int", nullable: false),
                    reviewed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    reviewed_by = table.Column<long>(type: "bigint", nullable: true),
                    sides = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    user_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_anticheat_quickmatch_reports", x => x.report_id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_anticheat_new_account_games_case",
                table: "anticheat_new_account_games",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_anticheat_new_account_games_reviewed_at",
                table: "anticheat_new_account_games",
                column: "reviewed_at");

            migrationBuilder.CreateIndex(
                name: "ix_anticheat_quickmatch_reports_case",
                table: "anticheat_quickmatch_reports",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_anticheat_quickmatch_reports_reviewed_at",
                table: "anticheat_quickmatch_reports",
                column: "reviewed_at");

            migrationBuilder.CreateIndex(
                name: "ux_anticheat_quickmatch_reports_user_lobby",
                table: "anticheat_quickmatch_reports",
                columns: new[] { "user_id", "lobby_id" },
                unique: true);
        }
    }
}
