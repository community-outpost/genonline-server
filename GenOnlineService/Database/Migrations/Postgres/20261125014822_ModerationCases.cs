using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GenOnlineService.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class ModerationCases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "case_id",
                table: "player_reports",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "category",
                table: "player_reports",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "chat");

            // a report of a player predates the categories, so it is about something other than one message
            migrationBuilder.Sql("UPDATE player_reports SET category = 'other' WHERE message_id IS NULL;");

            migrationBuilder.AddColumn<long>(
                name: "lobby_id",
                table: "player_reports",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "match_id",
                table: "player_reports",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "case_id",
                table: "automated_rule_matches",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "case_id",
                table: "anticheat_quickmatch_reports",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "case_id",
                table: "anticheat_probe_reports",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "case_id",
                table: "anticheat_new_account_games",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "case_id",
                table: "anticheat_module_reports",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "moderation_cases",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    opened_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    claimed_by_id = table.Column<long>(type: "bigint", nullable: true),
                    claimed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    closed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    closed_by_id = table.Column<long>(type: "bigint", nullable: true),
                    outcome = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    note = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    sanction_id = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_moderation_cases", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_player_reports_case",
                table: "player_reports",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_player_reports_match",
                table: "player_reports",
                column: "match_id");

            migrationBuilder.CreateIndex(
                name: "ix_automated_rule_matches_case",
                table: "automated_rule_matches",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_anticheat_quickmatch_reports_case",
                table: "anticheat_quickmatch_reports",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_anticheat_probe_reports_case",
                table: "anticheat_probe_reports",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_anticheat_new_account_games_case",
                table: "anticheat_new_account_games",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_anticheat_module_reports_case",
                table: "anticheat_module_reports",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_moderation_cases_status_closed_at",
                table: "moderation_cases",
                columns: new[] { "status", "closed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_moderation_cases_user_status",
                table: "moderation_cases",
                columns: new[] { "user_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "moderation_cases");

            migrationBuilder.DropIndex(
                name: "ix_player_reports_case",
                table: "player_reports");

            migrationBuilder.DropIndex(
                name: "ix_player_reports_match",
                table: "player_reports");

            migrationBuilder.DropIndex(
                name: "ix_automated_rule_matches_case",
                table: "automated_rule_matches");

            migrationBuilder.DropIndex(
                name: "ix_anticheat_quickmatch_reports_case",
                table: "anticheat_quickmatch_reports");

            migrationBuilder.DropIndex(
                name: "ix_anticheat_probe_reports_case",
                table: "anticheat_probe_reports");

            migrationBuilder.DropIndex(
                name: "ix_anticheat_new_account_games_case",
                table: "anticheat_new_account_games");

            migrationBuilder.DropIndex(
                name: "ix_anticheat_module_reports_case",
                table: "anticheat_module_reports");

            migrationBuilder.DropColumn(
                name: "case_id",
                table: "player_reports");

            migrationBuilder.DropColumn(
                name: "category",
                table: "player_reports");

            migrationBuilder.DropColumn(
                name: "lobby_id",
                table: "player_reports");

            migrationBuilder.DropColumn(
                name: "match_id",
                table: "player_reports");

            migrationBuilder.DropColumn(
                name: "case_id",
                table: "automated_rule_matches");

            migrationBuilder.DropColumn(
                name: "case_id",
                table: "anticheat_quickmatch_reports");

            migrationBuilder.DropColumn(
                name: "case_id",
                table: "anticheat_probe_reports");

            migrationBuilder.DropColumn(
                name: "case_id",
                table: "anticheat_new_account_games");

            migrationBuilder.DropColumn(
                name: "case_id",
                table: "anticheat_module_reports");
        }
    }
}
