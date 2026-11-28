using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GenOnlineService.Migrations.MySql
{
    /// <inheritdoc />
    public partial class DeviceMachines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "last_ip",
                table: "devices");

            migrationBuilder.CreateTable(
                name: "machines",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    machine_guid_id = table.Column<long>(type: "bigint", nullable: true),
                    mac_id = table.Column<long>(type: "bigint", nullable: true),
                    volume_serial_id = table.Column<long>(type: "bigint", nullable: true),
                    identifiers_key = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    first_seen_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    last_seen_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_machines", x => x.id);
                    table.ForeignKey(
                        name: "FK_machines_devices_mac_id",
                        column: x => x.mac_id,
                        principalTable: "devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_machines_devices_machine_guid_id",
                        column: x => x.machine_guid_id,
                        principalTable: "devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_machines_devices_volume_serial_id",
                        column: x => x.volume_serial_id,
                        principalTable: "devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "machine_addresses",
                columns: table => new
                {
                    machine_id = table.Column<long>(type: "bigint", nullable: false),
                    ip = table.Column<string>(type: "varchar(45)", maxLength: 45, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    first_seen_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    last_seen_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    sign_ins = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_machine_addresses", x => new { x.machine_id, x.ip });
                    table.ForeignKey(
                        name: "FK_machine_addresses_machines_machine_id",
                        column: x => x.machine_id,
                        principalTable: "machines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_machine_addresses_last_seen_at",
                table: "machine_addresses",
                column: "last_seen_at");

            migrationBuilder.CreateIndex(
                name: "ix_machines_last_seen_at",
                table: "machines",
                column: "last_seen_at");

            migrationBuilder.CreateIndex(
                name: "IX_machines_mac_id",
                table: "machines",
                column: "mac_id");

            migrationBuilder.CreateIndex(
                name: "IX_machines_machine_guid_id",
                table: "machines",
                column: "machine_guid_id");

            migrationBuilder.CreateIndex(
                name: "IX_machines_volume_serial_id",
                table: "machines",
                column: "volume_serial_id");

            migrationBuilder.CreateIndex(
                name: "ux_machines_user_identifiers",
                table: "machines",
                columns: new[] { "user_id", "identifiers_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "machine_addresses");

            migrationBuilder.DropTable(
                name: "machines");

            migrationBuilder.AddColumn<string>(
                name: "last_ip",
                table: "devices",
                type: "varchar(45)",
                maxLength: 45,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }
    }
}
