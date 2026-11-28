using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GenOnlineService.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class EacFindings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "anticheat_eac_findings",
                columns: table => new
                {
                    report_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    match_id = table.Column<long>(type: "bigint", nullable: false),
                    lobby_id = table.Column<long>(type: "bigint", nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    self_reported = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reviewed_by = table.Column<long>(type: "bigint", nullable: true),
                    case_id = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_anticheat_eac_findings", x => x.report_id);
                });

            migrationBuilder.CreateTable(
                name: "anticheat_eac_witnesses",
                columns: table => new
                {
                    witness_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    finding_id = table.Column<long>(type: "bigint", nullable: false),
                    reporter_id = table.Column<long>(type: "bigint", nullable: false),
                    action = table.Column<int>(type: "integer", nullable: false),
                    reason_code = table.Column<int>(type: "integer", nullable: false),
                    violation_type = table.Column<int>(type: "integer", nullable: false),
                    details = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_anticheat_eac_witnesses", x => x.witness_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_anticheat_eac_findings_case",
                table: "anticheat_eac_findings",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_anticheat_eac_findings_reviewed_at",
                table: "anticheat_eac_findings",
                column: "reviewed_at");

            migrationBuilder.CreateIndex(
                name: "ux_anticheat_eac_findings_target",
                table: "anticheat_eac_findings",
                columns: new[] { "user_id", "match_id", "lobby_id", "kind", "self_reported" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_anticheat_eac_witnesses_reporter_codes",
                table: "anticheat_eac_witnesses",
                columns: new[] { "finding_id", "reporter_id", "action", "reason_code", "violation_type" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "anticheat_eac_findings");

            migrationBuilder.DropTable(
                name: "anticheat_eac_witnesses");
        }
    }
}
