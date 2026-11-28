using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GenOnlineService.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class MatchHistoryCursorIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A failed index build leaves these columns committed, so a rerun of the migration has to accept them
            migrationBuilder.Sql("ALTER TABLE matches ADD COLUMN IF NOT EXISTS desynced boolean NOT NULL DEFAULT FALSE;");
            migrationBuilder.Sql("ALTER TABLE matches ADD COLUMN IF NOT EXISTS team_size smallint NULL;");
            migrationBuilder.Sql("ALTER TABLE match_players ADD COLUMN IF NOT EXISTS finished_at timestamp with time zone NULL;");

            // Each batch of 10000 matches commits on its own, so the large tables are never locked for the whole backfill
            migrationBuilder.Sql(@"
DO $$
DECLARE
    lo bigint := -9223372036854775808;
    hi bigint;
BEGIN
    LOOP
        SELECT max(id) INTO hi FROM (SELECT id FROM matches WHERE id > lo ORDER BY id LIMIT 10000) batch;
        EXIT WHEN hi IS NULL;

        UPDATE matches m SET team_size = t.n
        FROM (
            SELECT match_id, max(c)::smallint AS n
            FROM (
                SELECT match_id, count(*) AS c FROM match_players
                WHERE match_id > lo AND match_id <= hi AND (side IS NULL OR side <> -2)
                GROUP BY match_id, CASE WHEN team IS NULL OR team < 0 THEN -1 - slot ELSE team END
            ) teams
            GROUP BY match_id
        ) t
        WHERE m.id = t.match_id AND m.finished_at IS NOT NULL;

        UPDATE matches m SET desynced = true
        WHERE m.id > lo AND m.id <= hi
            AND EXISTS (SELECT 1 FROM match_players p WHERE p.match_id = m.id AND p.details @> '{""desynced"":true}');

        UPDATE match_players p SET finished_at = m.finished_at
        FROM matches m
        WHERE m.id = p.match_id AND p.match_id > lo AND p.match_id <= hi AND m.finished_at IS NOT NULL;

        COMMIT;
        lo := hi;
    END LOOP;
END $$;", suppressTransaction: true);

            // A failed concurrent build leaves an invalid index that IF NOT EXISTS would keep, so a rerun drops it and builds again
            migrationBuilder.Sql(@"
DO $$
DECLARE
    stale text;
BEGIN
    FOR stale IN
        SELECT c.relname FROM pg_index i
        JOIN pg_class c ON c.oid = i.indexrelid
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE NOT i.indisvalid AND n.nspname = current_schema()
            AND c.relname IN ('ix_matches_map_finished', 'ix_matches_type_finished', 'ix_matches_rated_finished', 'ix_matches_team_size_finished',
                'ix_matches_desync_finished', 'ix_match_players_user_match', 'ix_match_players_user_finished')
    LOOP
        EXECUTE format('DROP INDEX %I', stale);
    END LOOP;
END $$;", suppressTransaction: true);

            migrationBuilder.Sql("CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_matches_map_finished ON matches (map_name, finished_at, id);", suppressTransaction: true);
            migrationBuilder.Sql("CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_matches_type_finished ON matches (lobby_type, finished_at, id);", suppressTransaction: true);
            migrationBuilder.Sql("CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_matches_rated_finished ON matches (track_stats, finished_at, id);", suppressTransaction: true);
            migrationBuilder.Sql("CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_matches_team_size_finished ON matches (team_size, finished_at, id);", suppressTransaction: true);
            migrationBuilder.Sql("CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_matches_desync_finished ON matches (desynced, finished_at, id) WHERE desynced;", suppressTransaction: true);
            migrationBuilder.Sql("CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_match_players_user_match ON match_players (user_id, match_id);", suppressTransaction: true);
            migrationBuilder.Sql("CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_match_players_user_finished ON match_players (user_id, finished_at, match_id);", suppressTransaction: true);
            migrationBuilder.Sql("DROP INDEX CONCURRENTLY IF EXISTS ix_match_players_user_id;", suppressTransaction: true);
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
