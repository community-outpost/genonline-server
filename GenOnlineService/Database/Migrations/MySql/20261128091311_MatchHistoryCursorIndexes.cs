using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GenOnlineService.Migrations.MySql
{
    /// <inheritdoc />
    public partial class MatchHistoryCursorIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "desynced",
                table: "matches",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<short>(
                name: "team_size",
                table: "matches",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "finished_at",
                table: "match_players",
                type: "datetime(6)",
                nullable: true);

            // Each batch of 10000 matches commits on its own, so the large tables are never locked for the whole backfill
            migrationBuilder.Sql(@"
CREATE PROCEDURE genonline_backfill_match_history()
BEGIN
    DECLARE lo bigint DEFAULT -9223372036854775808;
    DECLARE hi bigint;
    backfill: LOOP
        SELECT max(id) INTO hi FROM (SELECT id FROM matches WHERE id > lo ORDER BY id LIMIT 10000) batch;
        IF hi IS NULL THEN
            LEAVE backfill;
        END IF;

        UPDATE matches m
        JOIN (
            SELECT match_id, max(c) AS n
            FROM (
                SELECT match_id, count(*) AS c FROM match_players
                WHERE match_id > lo AND match_id <= hi AND (side IS NULL OR side <> -2)
                GROUP BY match_id, CASE WHEN team IS NULL OR team < 0 THEN -1 - slot ELSE team END
            ) teams
            GROUP BY match_id
        ) t ON t.match_id = m.id
        SET m.team_size = t.n
        WHERE m.finished_at IS NOT NULL;

        UPDATE matches m SET m.desynced = 1
        WHERE m.id > lo AND m.id <= hi
            AND EXISTS (SELECT 1 FROM match_players p WHERE p.match_id = m.id AND p.details IS NOT NULL AND JSON_CONTAINS(p.details, '{""desynced"":true}'));

        UPDATE match_players p
        JOIN matches m ON m.id = p.match_id
        SET p.finished_at = m.finished_at
        WHERE p.match_id > lo AND p.match_id <= hi AND m.finished_at IS NOT NULL;

        COMMIT;
        SET lo = hi;
    END LOOP backfill;
END", suppressTransaction: true);
            migrationBuilder.Sql("CALL genonline_backfill_match_history();", suppressTransaction: true);
            migrationBuilder.Sql("DROP PROCEDURE genonline_backfill_match_history;", suppressTransaction: true);

            migrationBuilder.Sql("ALTER TABLE matches ADD INDEX ix_matches_map_finished (map_name, finished_at, id), ALGORITHM=INPLACE, LOCK=NONE;", suppressTransaction: true);
            migrationBuilder.Sql("ALTER TABLE matches ADD INDEX ix_matches_type_finished (lobby_type, finished_at, id), ALGORITHM=INPLACE, LOCK=NONE;", suppressTransaction: true);
            migrationBuilder.Sql("ALTER TABLE matches ADD INDEX ix_matches_rated_finished (track_stats, finished_at, id), ALGORITHM=INPLACE, LOCK=NONE;", suppressTransaction: true);
            migrationBuilder.Sql("ALTER TABLE matches ADD INDEX ix_matches_team_size_finished (team_size, finished_at, id), ALGORITHM=INPLACE, LOCK=NONE;", suppressTransaction: true);
            migrationBuilder.Sql("ALTER TABLE matches ADD INDEX ix_matches_desync_finished (desynced, finished_at, id), ALGORITHM=INPLACE, LOCK=NONE;", suppressTransaction: true);
            migrationBuilder.Sql("ALTER TABLE match_players ADD INDEX ix_match_players_user_match (user_id, match_id), ALGORITHM=INPLACE, LOCK=NONE;", suppressTransaction: true);
            migrationBuilder.Sql("ALTER TABLE match_players ADD INDEX ix_match_players_user_finished (user_id, finished_at, match_id), ALGORITHM=INPLACE, LOCK=NONE;", suppressTransaction: true);
            migrationBuilder.Sql("ALTER TABLE match_players DROP INDEX ix_match_players_user_id, ALGORITHM=INPLACE, LOCK=NONE;", suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_matches_desync_finished",
                table: "matches");

            migrationBuilder.DropIndex(
                name: "ix_matches_map_finished",
                table: "matches");

            migrationBuilder.DropIndex(
                name: "ix_matches_rated_finished",
                table: "matches");

            migrationBuilder.DropIndex(
                name: "ix_matches_team_size_finished",
                table: "matches");

            migrationBuilder.DropIndex(
                name: "ix_matches_type_finished",
                table: "matches");

            migrationBuilder.DropIndex(
                name: "ix_match_players_user_finished",
                table: "match_players");

            migrationBuilder.DropIndex(
                name: "ix_match_players_user_match",
                table: "match_players");

            migrationBuilder.DropColumn(
                name: "desynced",
                table: "matches");

            migrationBuilder.DropColumn(
                name: "team_size",
                table: "matches");

            migrationBuilder.DropColumn(
                name: "finished_at",
                table: "match_players");

            migrationBuilder.CreateIndex(
                name: "ix_match_players_user_id",
                table: "match_players",
                column: "user_id");
        }
    }
}
