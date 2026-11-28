using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GenOnlineService.Migrations.MySql
{
    /// <inheritdoc />
    public partial class PlayerRatingGame : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_player_ratings",
                table: "player_ratings");

            migrationBuilder.DropIndex(
                name: "ix_player_ratings_leaderboard",
                table: "player_ratings");

            migrationBuilder.AddColumn<string>(
                name: "game",
                table: "player_ratings",
                type: "varchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "zerohour")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddPrimaryKey(
                name: "PK_player_ratings",
                table: "player_ratings",
                columns: new[] { "user_id", "game", "kind" });

            migrationBuilder.CreateIndex(
                name: "ix_player_ratings_leaderboard",
                table: "player_ratings",
                columns: new[] { "game", "kind", "rating", "user_id" },
                descending: new[] { false, false, true, false });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_player_ratings",
                table: "player_ratings");

            migrationBuilder.DropIndex(
                name: "ix_player_ratings_leaderboard",
                table: "player_ratings");

            migrationBuilder.DropColumn(
                name: "game",
                table: "player_ratings");

            migrationBuilder.AddPrimaryKey(
                name: "PK_player_ratings",
                table: "player_ratings",
                columns: new[] { "user_id", "kind" });

            migrationBuilder.CreateIndex(
                name: "ix_player_ratings_leaderboard",
                table: "player_ratings",
                columns: new[] { "kind", "rating", "user_id" },
                descending: new[] { false, true, false });
        }
    }
}
