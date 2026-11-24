using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GenOnlineService.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class LeaseStandbyStartedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "standby_started_at",
                table: "realtime_lease",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "realtime_lease",
                keyColumn: "id",
                keyValue: 1,
                column: "standby_started_at",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "standby_started_at",
                table: "realtime_lease");
        }
    }
}
