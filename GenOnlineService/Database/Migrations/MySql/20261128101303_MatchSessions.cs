using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GenOnlineService.Migrations.MySql
{
    /// <inheritdoc />
    public partial class MatchSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "machine_id",
                table: "session_events",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "session_event_id",
                table: "match_players",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "machine_id",
                table: "game_sessions",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_session_events_machine_id",
                table: "session_events",
                column: "machine_id");

            migrationBuilder.CreateIndex(
                name: "ix_match_players_session_event_id",
                table: "match_players",
                column: "session_event_id");

            migrationBuilder.CreateIndex(
                name: "IX_game_sessions_machine_id",
                table: "game_sessions",
                column: "machine_id");

            migrationBuilder.AddForeignKey(
                name: "FK_game_sessions_machines_machine_id",
                table: "game_sessions",
                column: "machine_id",
                principalTable: "machines",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_match_players_session_events_session_event_id",
                table: "match_players",
                column: "session_event_id",
                principalTable: "session_events",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_session_events_machines_machine_id",
                table: "session_events",
                column: "machine_id",
                principalTable: "machines",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_game_sessions_machines_machine_id",
                table: "game_sessions");

            migrationBuilder.DropForeignKey(
                name: "FK_match_players_session_events_session_event_id",
                table: "match_players");

            migrationBuilder.DropForeignKey(
                name: "FK_session_events_machines_machine_id",
                table: "session_events");

            migrationBuilder.DropIndex(
                name: "ix_session_events_machine_id",
                table: "session_events");

            migrationBuilder.DropIndex(
                name: "ix_match_players_session_event_id",
                table: "match_players");

            migrationBuilder.DropIndex(
                name: "IX_game_sessions_machine_id",
                table: "game_sessions");

            migrationBuilder.DropColumn(
                name: "machine_id",
                table: "session_events");

            migrationBuilder.DropColumn(
                name: "session_event_id",
                table: "match_players");

            migrationBuilder.DropColumn(
                name: "machine_id",
                table: "game_sessions");
        }
    }
}
