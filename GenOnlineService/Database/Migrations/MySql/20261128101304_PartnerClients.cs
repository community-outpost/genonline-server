using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GenOnlineService.Migrations.MySql
{
    /// <inheritdoc />
    public partial class PartnerClients : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "partner_id",
                table: "clients",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_clients_partner_id",
                table: "clients",
                column: "partner_id");

            migrationBuilder.AddForeignKey(
                name: "FK_clients_service_accounts_partner_id",
                table: "clients",
                column: "partner_id",
                principalTable: "service_accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_clients_service_accounts_partner_id",
                table: "clients");

            migrationBuilder.DropIndex(
                name: "ix_clients_partner_id",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "partner_id",
                table: "clients");
        }
    }
}
