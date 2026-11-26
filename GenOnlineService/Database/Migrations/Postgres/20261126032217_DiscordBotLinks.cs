using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GenOnlineService.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class DiscordBotLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "discord_bot_links",
                columns: table => new
                {
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    discord_user_id = table.Column<long>(type: "bigint", nullable: false),
                    linked_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    linked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_discord_bot_links", x => x.user_id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_discord_bot_links_discord_user_id",
                table: "discord_bot_links",
                column: "discord_user_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "discord_bot_links");
        }
    }
}
