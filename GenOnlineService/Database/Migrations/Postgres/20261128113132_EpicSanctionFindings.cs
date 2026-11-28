using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GenOnlineService.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class EpicSanctionFindings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_anticheat_eac_findings_target",
                table: "anticheat_eac_findings");

            migrationBuilder.AddColumn<DateTime>(
                name: "expires_at",
                table: "anticheat_eac_findings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "placed_at",
                table: "anticheat_eac_findings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reference_id",
                table: "anticheat_eac_findings",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "sanction_action",
                table: "anticheat_eac_findings",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source",
                table: "anticheat_eac_findings",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "client");

            migrationBuilder.CreateIndex(
                name: "ux_anticheat_eac_findings_target",
                table: "anticheat_eac_findings",
                columns: new[] { "user_id", "match_id", "lobby_id", "kind", "self_reported", "reference_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_anticheat_eac_findings_target",
                table: "anticheat_eac_findings");

            migrationBuilder.DropColumn(
                name: "expires_at",
                table: "anticheat_eac_findings");

            migrationBuilder.DropColumn(
                name: "placed_at",
                table: "anticheat_eac_findings");

            migrationBuilder.DropColumn(
                name: "reference_id",
                table: "anticheat_eac_findings");

            migrationBuilder.DropColumn(
                name: "sanction_action",
                table: "anticheat_eac_findings");

            migrationBuilder.DropColumn(
                name: "source",
                table: "anticheat_eac_findings");

            migrationBuilder.CreateIndex(
                name: "ux_anticheat_eac_findings_target",
                table: "anticheat_eac_findings",
                columns: new[] { "user_id", "match_id", "lobby_id", "kind", "self_reported" },
                unique: true);
        }
    }
}
