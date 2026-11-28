using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GenOnlineService.Migrations.MySql
{
    /// <inheritdoc />
    public partial class LeaderboardGame : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_leaderboard_entries",
                table: "leaderboard_entries");

            migrationBuilder.AddColumn<string>(
                name: "game",
                table: "leaderboard_entries",
                type: "varchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "zerohour")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddPrimaryKey(
                name: "PK_leaderboard_entries",
                table: "leaderboard_entries",
                columns: new[] { "game", "period", "period_start", "user_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_leaderboard_entries",
                table: "leaderboard_entries");

            migrationBuilder.DropColumn(
                name: "game",
                table: "leaderboard_entries");

            migrationBuilder.AddPrimaryKey(
                name: "PK_leaderboard_entries",
                table: "leaderboard_entries",
                columns: new[] { "period", "period_start", "user_id" });
        }
    }
}
