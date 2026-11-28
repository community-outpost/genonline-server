using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GenOnlineService.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class MatchChat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "recipients",
                table: "chat_messages",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "scope",
                table: "chat_messages",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_chat_messages_match",
                table: "chat_messages",
                columns: new[] { "match_id", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_chat_messages_match",
                table: "chat_messages");

            migrationBuilder.DropColumn(
                name: "recipients",
                table: "chat_messages");

            migrationBuilder.DropColumn(
                name: "scope",
                table: "chat_messages");
        }
    }
}
