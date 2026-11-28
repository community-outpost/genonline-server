using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GenOnlineService.Migrations.MySql
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
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    match_id = table.Column<long>(type: "bigint", nullable: false),
                    lobby_id = table.Column<long>(type: "bigint", nullable: false),
                    kind = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    self_reported = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    reviewed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    reviewed_by = table.Column<long>(type: "bigint", nullable: true),
                    case_id = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_anticheat_eac_findings", x => x.report_id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "anticheat_eac_witnesses",
                columns: table => new
                {
                    witness_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    finding_id = table.Column<long>(type: "bigint", nullable: false),
                    reporter_id = table.Column<long>(type: "bigint", nullable: false),
                    action = table.Column<int>(type: "int", nullable: false),
                    reason_code = table.Column<int>(type: "int", nullable: false),
                    violation_type = table.Column<int>(type: "int", nullable: false),
                    details = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_anticheat_eac_witnesses", x => x.witness_id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

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
