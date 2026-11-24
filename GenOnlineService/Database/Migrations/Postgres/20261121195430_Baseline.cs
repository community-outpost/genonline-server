using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace GenOnlineService.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class Baseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "anticheat_module_reports",
                columns: table => new
                {
                    report_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    match_id = table.Column<long>(type: "bigint", nullable: false),
                    module_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    module_path = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    module_size = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reviewed_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_anticheat_module_reports", x => x.report_id);
                });

            migrationBuilder.CreateTable(
                name: "anticheat_new_account_games",
                columns: table => new
                {
                    report_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    match_id = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reviewed_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_anticheat_new_account_games", x => x.report_id);
                });

            migrationBuilder.CreateTable(
                name: "anticheat_probe_reports",
                columns: table => new
                {
                    report_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    match_id = table.Column<long>(type: "bigint", nullable: false),
                    reason = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reviewed_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_anticheat_probe_reports", x => x.report_id);
                });

            migrationBuilder.CreateTable(
                name: "anticheat_quickmatch_reports",
                columns: table => new
                {
                    report_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    match_id = table.Column<long>(type: "bigint", nullable: false),
                    lobby_id = table.Column<long>(type: "bigint", nullable: false),
                    playlist_id = table.Column<int>(type: "integer", nullable: false),
                    method = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    sides = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reviewed_by = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_anticheat_quickmatch_reports", x => x.report_id);
                });

            migrationBuilder.CreateTable(
                name: "application_settings",
                columns: table => new
                {
                    key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    value = table.Column<string>(type: "jsonb", nullable: false),
                    updated_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_application_settings", x => x.key);
                });

            migrationBuilder.CreateTable(
                name: "audit_log",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    actor_kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    actor_id = table.Column<long>(type: "bigint", nullable: true),
                    actor = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    target_kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    target_id = table.Column<long>(type: "bigint", nullable: true),
                    reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    details = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_log", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "automated_rule_matches",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    rule_id = table.Column<long>(type: "bigint", nullable: false),
                    rule_version = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    trigger = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    matched_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    mode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    actions = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    facts = table.Column<string>(type: "jsonb", nullable: false),
                    review_state = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    reviewed_by = table.Column<long>(type: "bigint", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_automated_rule_matches", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "automated_rule_versions",
                columns: table => new
                {
                    rule_id = table.Column<long>(type: "bigint", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    document = table.Column<string>(type: "jsonb", nullable: false),
                    created_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_automated_rule_versions", x => new { x.rule_id, x.number });
                });

            migrationBuilder.CreateTable(
                name: "automated_rules",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    mode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    current_version = table.Column<int>(type: "integer", nullable: false),
                    archived_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_automated_rules", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "blocks",
                columns: table => new
                {
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    blocked_user_id = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_blocks", x => new { x.user_id, x.blocked_user_id });
                });

            migrationBuilder.CreateTable(
                name: "chat_group_members",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    group_id = table.Column<long>(type: "bigint", nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    joined_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chat_group_members", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "chat_groups",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    owner_id = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chat_groups", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "chat_messages",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    channel_id = table.Column<long>(type: "bigint", nullable: true),
                    lobby_id = table.Column<long>(type: "bigint", nullable: true),
                    match_id = table.Column<long>(type: "bigint", nullable: true),
                    sender_id = table.Column<long>(type: "bigint", nullable: false),
                    recipient_id = table.Column<long>(type: "bigint", nullable: true),
                    group_id = table.Column<long>(type: "bigint", nullable: true),
                    type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    content = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    mentions = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chat_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "chat_read_markers",
                columns: table => new
                {
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    conversation = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    last_read_message_id = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chat_read_markers", x => new { x.user_id, x.conversation });
                });

            migrationBuilder.CreateTable(
                name: "clients",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    client_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    game = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    first_party = table.Column<bool>(type: "boolean", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    min_version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clients", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "connection_reports",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    reporter_id = table.Column<long>(type: "bigint", nullable: false),
                    lobby_id = table.Column<long>(type: "bigint", nullable: false),
                    target_user_id = table.Column<long>(type: "bigint", nullable: false),
                    attempt = table.Column<int>(type: "integer", nullable: false),
                    outcome = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    transport = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ip_version = table.Column<int>(type: "integer", nullable: false),
                    events = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_connection_reports", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "data_protection_keys",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    friendly_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    xml = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_data_protection_keys", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "devices",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    value_hmac = table.Column<byte[]>(type: "bytea", fixedLength: true, maxLength: 32, nullable: false),
                    value_raw = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    first_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_devices", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "erased_accounts",
                columns: table => new
                {
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    erased_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_erased_accounts", x => x.user_id);
                });

            migrationBuilder.CreateTable(
                name: "flag_revisions",
                columns: table => new
                {
                    number = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    document = table.Column<string>(type: "jsonb", nullable: false),
                    rollback_of = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_flag_revisions", x => x.number);
                });

            migrationBuilder.CreateTable(
                name: "flags",
                columns: table => new
                {
                    key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    definition = table.Column<string>(type: "jsonb", nullable: false),
                    archived_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false),
                    updated_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_flags", x => x.key);
                });

            migrationBuilder.CreateTable(
                name: "friend_requests",
                columns: table => new
                {
                    from_user_id = table.Column<long>(type: "bigint", nullable: false),
                    to_user_id = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_friend_requests", x => new { x.from_user_id, x.to_user_id });
                });

            migrationBuilder.CreateTable(
                name: "friendships",
                columns: table => new
                {
                    user_id_low = table.Column<long>(type: "bigint", nullable: false),
                    user_id_high = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_friendships", x => new { x.user_id_low, x.user_id_high });
                });

            migrationBuilder.CreateTable(
                name: "identifier_hashes",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    kind = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    value_hmac = table.Column<byte[]>(type: "bytea", fixedLength: true, maxLength: 32, nullable: false),
                    sanction_id = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_identifier_hashes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "identities",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    extern_uid = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_used_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_identities", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "leaderboard_entries",
                columns: table => new
                {
                    period = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    period_start = table.Column<DateOnly>(type: "date", nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    points = table.Column<int>(type: "integer", nullable: false),
                    wins = table.Column<int>(type: "integer", nullable: false),
                    losses = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_leaderboard_entries", x => new { x.period, x.period_start, x.user_id });
                });

            migrationBuilder.CreateTable(
                name: "lobby_ids",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    next_id = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lobby_ids", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "match_id_workers",
                columns: table => new
                {
                    worker_id = table.Column<int>(type: "integer", nullable: false),
                    holder = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    generation = table.Column<long>(type: "bigint", nullable: false),
                    reserved_until = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_match_id_workers", x => x.worker_id);
                });

            migrationBuilder.CreateTable(
                name: "matches",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false),
                    owner_user_id = table.Column<long>(type: "bigint", nullable: true),
                    name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    lobby_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    roster_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    map_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    map_path = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    is_official_map = table.Column<bool>(type: "boolean", nullable: false),
                    vanilla_teams = table.Column<bool>(type: "boolean", nullable: false),
                    starting_cash = table.Column<long>(type: "bigint", nullable: false),
                    limit_superweapons = table.Column<bool>(type: "boolean", nullable: false),
                    track_stats = table.Column<bool>(type: "boolean", nullable: false),
                    allow_observers = table.Column<bool>(type: "boolean", nullable: false),
                    max_cam_height = table.Column<int>(type: "integer", nullable: false),
                    exe_crc = table.Column<long>(type: "bigint", nullable: false),
                    ini_crc = table.Column<long>(type: "bigint", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    finished_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_matches", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "oauth_applications",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    application_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    client_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    client_secret = table.Column<string>(type: "text", nullable: true),
                    client_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    concurrency_token = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    consent_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    display_name = table.Column<string>(type: "text", nullable: true),
                    display_names = table.Column<string>(type: "text", nullable: true),
                    json_web_key_set = table.Column<string>(type: "text", nullable: true),
                    permissions = table.Column<string>(type: "text", nullable: true),
                    post_logout_redirect_uris = table.Column<string>(type: "text", nullable: true),
                    properties = table.Column<string>(type: "text", nullable: true),
                    redirect_uris = table.Column<string>(type: "text", nullable: true),
                    requirements = table.Column<string>(type: "text", nullable: true),
                    settings = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_oauth_applications", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "oauth_scopes",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    concurrency_token = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    descriptions = table.Column<string>(type: "text", nullable: true),
                    display_name = table.Column<string>(type: "text", nullable: true),
                    display_names = table.Column<string>(type: "text", nullable: true),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    properties = table.Column<string>(type: "text", nullable: true),
                    resources = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_oauth_scopes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "player_network",
                columns: table => new
                {
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    relay_region = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    port_mapping = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    ipv4 = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    ipv6 = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_player_network", x => x.user_id);
                });

            migrationBuilder.CreateTable(
                name: "player_preferences",
                columns: table => new
                {
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    color = table.Column<int>(type: "integer", nullable: true),
                    side = table.Column<int>(type: "integer", nullable: true),
                    map = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    starting_money = table.Column<int>(type: "integer", nullable: true),
                    limit_superweapons = table.Column<bool>(type: "boolean", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_player_preferences", x => x.user_id);
                });

            migrationBuilder.CreateTable(
                name: "player_ratings",
                columns: table => new
                {
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    rating = table.Column<int>(type: "integer", nullable: false),
                    matches_played = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_player_ratings", x => new { x.user_id, x.kind });
                });

            migrationBuilder.CreateTable(
                name: "player_reports",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    reporter_id = table.Column<long>(type: "bigint", nullable: false),
                    reported_user_id = table.Column<long>(type: "bigint", nullable: false),
                    message_id = table.Column<long>(type: "bigint", nullable: true),
                    reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    evidence = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    resolved_by_kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    resolved_by_id = table.Column<long>(type: "bigint", nullable: true),
                    resolution_note = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_player_reports", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "player_stats",
                columns: table => new
                {
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    stats = table.Column<string>(type: "jsonb", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_player_stats", x => x.user_id);
                });

            migrationBuilder.CreateTable(
                name: "realtime_lease",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    holder = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    epoch = table.Column<long>(type: "bigint", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    standby = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    standby_expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    handover_requested_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    handover_target = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    handover_confirmed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_realtime_lease", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    name = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    display_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    position = table.Column<int>(type: "integer", nullable: false),
                    built_in = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.name);
                });

            migrationBuilder.CreateTable(
                name: "sanction_evidence",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    sanction_id = table.Column<long>(type: "bigint", nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    match_id = table.Column<long>(type: "bigint", nullable: false),
                    object_key = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_id = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sanction_evidence", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sanctions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    created_by_kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    created_by_id = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    lifted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    lifted_by_kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    lifted_by_id = table.Column<long>(type: "bigint", nullable: true),
                    lift_reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    report_id = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sanctions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "service_accounts",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    description = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    disabled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_accounts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "session_events",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    result = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    client = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    client_version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    exe_crc = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    country = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "text_rule_exceptions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    term = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    language = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    updated_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_text_rule_exceptions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "text_rule_terms",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    term = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    language = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    severity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    match_mode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    updated_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_text_rule_terms", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "text_rule_workflows",
                columns: table => new
                {
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    definition = table.Column<string>(type: "text", nullable: false),
                    updated_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_text_rule_workflows", x => x.kind);
                });

            migrationBuilder.CreateTable(
                name: "user_recovery_codes",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    code_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    used_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_recovery_codes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user_roles",
                columns: table => new
                {
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    granted_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    granted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_roles", x => new { x.user_id, x.role, x.source });
                });

            migrationBuilder.CreateTable(
                name: "user_username_history",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    username = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name_tag = table.Column<int>(type: "integer", nullable: true),
                    changed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    changed_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_username_history", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    username = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    username_normalized = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name_tag = table.Column<int>(type: "integer", nullable: false),
                    name_key = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    email_normalized = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    email_confirmed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    password_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    security_stamp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    authenticator_key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    state = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    two_factor_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    lockout_end_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    failed_sign_in_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_sign_in_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    renamed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    name_required_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "client_builds",
                columns: table => new
                {
                    client_id = table.Column<long>(type: "bigint", nullable: false),
                    exe_crc = table.Column<long>(type: "bigint", nullable: false),
                    ini_crc = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_builds", x => new { x.client_id, x.exe_crc, x.ini_crc });
                    table.ForeignKey(
                        name: "FK_client_builds_clients_client_id",
                        column: x => x.client_id,
                        principalTable: "clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "match_players",
                columns: table => new
                {
                    match_id = table.Column<long>(type: "bigint", nullable: false),
                    slot = table.Column<short>(type: "smallint", nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: true),
                    team = table.Column<short>(type: "smallint", nullable: true),
                    side = table.Column<int>(type: "integer", nullable: true),
                    color = table.Column<int>(type: "integer", nullable: true),
                    result = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    details = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_match_players", x => new { x.match_id, x.slot });
                    table.ForeignKey(
                        name: "FK_match_players_matches_match_id",
                        column: x => x.match_id,
                        principalTable: "matches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "match_publications",
                columns: table => new
                {
                    match_id = table.Column<long>(type: "bigint", nullable: false),
                    next_attempt_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    published_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    last_error = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_match_publications", x => x.match_id);
                    table.ForeignKey(
                        name: "FK_match_publications_matches_match_id",
                        column: x => x.match_id,
                        principalTable: "matches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "oauth_authorizations",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    application_id = table.Column<string>(type: "text", nullable: true),
                    concurrency_token = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    creation_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    properties = table.Column<string>(type: "text", nullable: true),
                    scopes = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    subject = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_oauth_authorizations", x => x.id);
                    table.ForeignKey(
                        name: "FK_oauth_authorizations_oauth_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "oauth_applications",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "role_permissions",
                columns: table => new
                {
                    role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    permission = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_permissions", x => new { x.role, x.permission });
                    table.ForeignKey(
                        name: "FK_role_permissions_roles_role",
                        column: x => x.role,
                        principalTable: "roles",
                        principalColumn: "name",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "service_account_permissions",
                columns: table => new
                {
                    service_account_id = table.Column<long>(type: "bigint", nullable: false),
                    permission = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    granted_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    granted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_account_permissions", x => new { x.service_account_id, x.permission });
                    table.ForeignKey(
                        name: "FK_service_account_permissions_service_accounts_service_accoun~",
                        column: x => x.service_account_id,
                        principalTable: "service_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "api_tokens",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    owner_user_id = table.Column<long>(type: "bigint", nullable: true),
                    owner_service_id = table.Column<long>(type: "bigint", nullable: true),
                    name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    token_hash = table.Column<byte[]>(type: "bytea", fixedLength: true, maxLength: 32, nullable: false),
                    token_last4 = table.Column<string>(type: "character(4)", fixedLength: true, maxLength: 4, nullable: false),
                    scopes = table.Column<string>(type: "text", nullable: false),
                    allowed_ips = table.Column<string>(type: "text", nullable: true),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_used_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_used_ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    revoked_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_api_tokens", x => x.id);
                    table.CheckConstraint("ck_api_tokens_one_owner", "(owner_user_id IS NULL) <> (owner_service_id IS NULL)");
                    table.ForeignKey(
                        name: "FK_api_tokens_service_accounts_owner_service_id",
                        column: x => x.owner_service_id,
                        principalTable: "service_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_api_tokens_users_owner_user_id",
                        column: x => x.owner_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "name_reservations",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    name = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name_key = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    name_tag = table.Column<int>(type: "integer", nullable: false),
                    held_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_name_reservations", x => x.id);
                    table.ForeignKey(
                        name: "FK_name_reservations_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_agreements",
                columns: table => new
                {
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    document = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    agreed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_agreements", x => new { x.user_id, x.document, x.version });
                    table.ForeignKey(
                        name: "FK_user_agreements_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "game_sessions",
                columns: table => new
                {
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    session_type = table.Column<int>(type: "integer", nullable: false),
                    authorization_id = table.Column<string>(type: "text", nullable: false),
                    client_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    game = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    registered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    exe_crc = table.Column<long>(type: "bigint", nullable: true),
                    ini_crc = table.Column<long>(type: "bigint", nullable: true),
                    anticheat_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_sessions", x => new { x.user_id, x.session_type });
                    table.ForeignKey(
                        name: "FK_game_sessions_oauth_authorizations_authorization_id",
                        column: x => x.authorization_id,
                        principalTable: "oauth_authorizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "oauth_tokens",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    application_id = table.Column<string>(type: "text", nullable: true),
                    authorization_id = table.Column<string>(type: "text", nullable: true),
                    concurrency_token = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    creation_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    expiration_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    payload = table.Column<string>(type: "text", nullable: true),
                    properties = table.Column<string>(type: "text", nullable: true),
                    redemption_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reference_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    subject = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    type = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_oauth_tokens", x => x.id);
                    table.ForeignKey(
                        name: "FK_oauth_tokens_oauth_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "oauth_applications",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_oauth_tokens_oauth_authorizations_authorization_id",
                        column: x => x.authorization_id,
                        principalTable: "oauth_authorizations",
                        principalColumn: "id");
                });

            migrationBuilder.InsertData(
                table: "lobby_ids",
                columns: new[] { "id", "next_id" },
                values: new object[] { 1, 1L });

            migrationBuilder.InsertData(
                table: "match_id_workers",
                columns: new[] { "worker_id", "generation", "holder", "reserved_until" },
                values: new object[,]
                {
                    { 0, 0L, "", 0L },
                    { 1, 0L, "", 0L },
                    { 2, 0L, "", 0L },
                    { 3, 0L, "", 0L },
                    { 4, 0L, "", 0L },
                    { 5, 0L, "", 0L },
                    { 6, 0L, "", 0L },
                    { 7, 0L, "", 0L },
                    { 8, 0L, "", 0L },
                    { 9, 0L, "", 0L },
                    { 10, 0L, "", 0L },
                    { 11, 0L, "", 0L },
                    { 12, 0L, "", 0L },
                    { 13, 0L, "", 0L },
                    { 14, 0L, "", 0L },
                    { 15, 0L, "", 0L },
                    { 16, 0L, "", 0L },
                    { 17, 0L, "", 0L },
                    { 18, 0L, "", 0L },
                    { 19, 0L, "", 0L },
                    { 20, 0L, "", 0L },
                    { 21, 0L, "", 0L },
                    { 22, 0L, "", 0L },
                    { 23, 0L, "", 0L },
                    { 24, 0L, "", 0L },
                    { 25, 0L, "", 0L },
                    { 26, 0L, "", 0L },
                    { 27, 0L, "", 0L },
                    { 28, 0L, "", 0L },
                    { 29, 0L, "", 0L },
                    { 30, 0L, "", 0L },
                    { 31, 0L, "", 0L }
                });

            migrationBuilder.InsertData(
                table: "realtime_lease",
                columns: new[] { "id", "epoch", "expires_at", "handover_confirmed_at", "handover_requested_at", "handover_target", "holder", "standby", "standby_expires_at" },
                values: new object[] { 1, 0L, new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, "", null, null });

            migrationBuilder.CreateIndex(
                name: "ix_anticheat_module_reports_reviewed_at",
                table: "anticheat_module_reports",
                column: "reviewed_at");

            migrationBuilder.CreateIndex(
                name: "ux_anticheat_module_reports_user_match_module",
                table: "anticheat_module_reports",
                columns: new[] { "user_id", "match_id", "module_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_anticheat_new_account_games_reviewed_at",
                table: "anticheat_new_account_games",
                column: "reviewed_at");

            migrationBuilder.CreateIndex(
                name: "ix_anticheat_probe_reports_reviewed_at",
                table: "anticheat_probe_reports",
                column: "reviewed_at");

            migrationBuilder.CreateIndex(
                name: "ix_anticheat_quickmatch_reports_reviewed_at",
                table: "anticheat_quickmatch_reports",
                column: "reviewed_at");

            migrationBuilder.CreateIndex(
                name: "ux_anticheat_quickmatch_reports_user_lobby",
                table: "anticheat_quickmatch_reports",
                columns: new[] { "user_id", "lobby_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_api_tokens_owner_service",
                table: "api_tokens",
                columns: new[] { "owner_service_id", "name" });

            migrationBuilder.CreateIndex(
                name: "ix_api_tokens_owner_user",
                table: "api_tokens",
                columns: new[] { "owner_user_id", "name" });

            migrationBuilder.CreateIndex(
                name: "ux_api_tokens_token_hash",
                table: "api_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_created_at",
                table: "audit_log",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_target",
                table: "audit_log",
                columns: new[] { "target_kind", "target_id" });

            migrationBuilder.CreateIndex(
                name: "ix_automated_rule_matches_review_user",
                table: "automated_rule_matches",
                columns: new[] { "review_state", "user_id" });

            migrationBuilder.CreateIndex(
                name: "ix_automated_rule_matches_rule_matched_at",
                table: "automated_rule_matches",
                columns: new[] { "rule_id", "matched_at" });

            migrationBuilder.CreateIndex(
                name: "ix_automated_rule_matches_rule_user_trigger",
                table: "automated_rule_matches",
                columns: new[] { "rule_id", "user_id", "trigger", "matched_at" });

            migrationBuilder.CreateIndex(
                name: "ix_automated_rule_matches_user",
                table: "automated_rule_matches",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_chat_group_members_user_id",
                table: "chat_group_members",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_chat_group_members_group_user",
                table: "chat_group_members",
                columns: new[] { "group_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_chat_messages_channel",
                table: "chat_messages",
                columns: new[] { "kind", "channel_id", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_chat_messages_created_at",
                table: "chat_messages",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_chat_messages_group",
                table: "chat_messages",
                columns: new[] { "group_id", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_chat_messages_lobby",
                table: "chat_messages",
                columns: new[] { "lobby_id", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_chat_messages_recipient_sender",
                table: "chat_messages",
                columns: new[] { "recipient_id", "sender_id", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_chat_messages_sender_recipient",
                table: "chat_messages",
                columns: new[] { "sender_id", "recipient_id", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_chat_read_markers_updated_at",
                table: "chat_read_markers",
                column: "updated_at");

            migrationBuilder.CreateIndex(
                name: "ux_clients_client_id",
                table: "clients",
                column: "client_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_connection_reports_lobby_id",
                table: "connection_reports",
                column: "lobby_id");

            migrationBuilder.CreateIndex(
                name: "ix_connection_reports_target_updated_at",
                table: "connection_reports",
                columns: new[] { "target_user_id", "updated_at" });

            migrationBuilder.CreateIndex(
                name: "ix_connection_reports_updated_at",
                table: "connection_reports",
                column: "updated_at");

            migrationBuilder.CreateIndex(
                name: "ux_connection_reports_reporter_lobby_target_attempt",
                table: "connection_reports",
                columns: new[] { "reporter_id", "lobby_id", "target_user_id", "attempt" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_devices_kind_value",
                table: "devices",
                columns: new[] { "kind", "value_hmac" });

            migrationBuilder.CreateIndex(
                name: "ix_devices_last_seen_at",
                table: "devices",
                column: "last_seen_at");

            migrationBuilder.CreateIndex(
                name: "ux_devices_user_kind_value",
                table: "devices",
                columns: new[] { "user_id", "kind", "value_hmac" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_friend_requests_to_user_id",
                table: "friend_requests",
                column: "to_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_friendships_user_id_high",
                table: "friendships",
                column: "user_id_high");

            migrationBuilder.CreateIndex(
                name: "ix_game_sessions_authorization_id",
                table: "game_sessions",
                column: "authorization_id");

            migrationBuilder.CreateIndex(
                name: "ix_identifier_hashes_kind_value",
                table: "identifier_hashes",
                columns: new[] { "kind", "value_hmac" });

            migrationBuilder.CreateIndex(
                name: "ix_identifier_hashes_user",
                table: "identifier_hashes",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_identities_provider_extern_uid",
                table: "identities",
                columns: new[] { "provider", "extern_uid" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_identities_user_id_provider",
                table: "identities",
                columns: new[] { "user_id", "provider" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_leaderboard_entries_user_id",
                table: "leaderboard_entries",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_match_players_user_id",
                table: "match_players",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_match_publications_pending",
                table: "match_publications",
                columns: new[] { "published_at", "next_attempt_at" });

            migrationBuilder.CreateIndex(
                name: "ix_matches_finished_at",
                table: "matches",
                columns: new[] { "finished_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_matches_started_at",
                table: "matches",
                column: "started_at");

            migrationBuilder.CreateIndex(
                name: "ix_name_reservations_user_id",
                table: "name_reservations",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_name_reservations_name_key_name_tag",
                table: "name_reservations",
                columns: new[] { "name_key", "name_tag" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_oauth_applications_client_id",
                table: "oauth_applications",
                column: "client_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_oauth_authorizations_application_id_status_subject_type",
                table: "oauth_authorizations",
                columns: new[] { "application_id", "status", "subject", "type" });

            migrationBuilder.CreateIndex(
                name: "ix_oauth_scopes_name",
                table: "oauth_scopes",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_oauth_tokens_application_id_status_subject_type",
                table: "oauth_tokens",
                columns: new[] { "application_id", "status", "subject", "type" });

            migrationBuilder.CreateIndex(
                name: "ix_oauth_tokens_authorization_id",
                table: "oauth_tokens",
                column: "authorization_id");

            migrationBuilder.CreateIndex(
                name: "ix_oauth_tokens_reference_id",
                table: "oauth_tokens",
                column: "reference_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_player_ratings_leaderboard",
                table: "player_ratings",
                columns: new[] { "kind", "rating", "user_id" },
                descending: new[] { false, true, false });

            migrationBuilder.CreateIndex(
                name: "ix_player_reports_reported_user_created_at",
                table: "player_reports",
                columns: new[] { "reported_user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_player_reports_status_created_at",
                table: "player_reports",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ux_player_reports_reporter_message",
                table: "player_reports",
                columns: new[] { "reporter_id", "message_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_roles_position",
                table: "roles",
                column: "position",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sanction_evidence_key",
                table: "sanction_evidence",
                column: "object_key");

            migrationBuilder.CreateIndex(
                name: "ux_sanction_evidence_sanction_key",
                table: "sanction_evidence",
                columns: new[] { "sanction_id", "object_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sanctions_user_type_lifted_expires",
                table: "sanctions",
                columns: new[] { "user_id", "type", "lifted_at", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "ux_service_accounts_name",
                table: "service_accounts",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_session_events_country_user",
                table: "session_events",
                columns: new[] { "country", "user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_session_events_created_at",
                table: "session_events",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_session_events_ip_user",
                table: "session_events",
                columns: new[] { "ip", "user_id" });

            migrationBuilder.CreateIndex(
                name: "ix_session_events_user_created",
                table: "session_events",
                columns: new[] { "user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ux_text_rule_exceptions_language_term",
                table: "text_rule_exceptions",
                columns: new[] { "language", "term" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_text_rule_terms_language_term",
                table: "text_rule_terms",
                columns: new[] { "language", "term" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_recovery_codes_user_id",
                table: "user_recovery_codes",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_username_history_user_id",
                table: "user_username_history",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_created_at",
                table: "users",
                columns: new[] { "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_users_last_sign_in_at",
                table: "users",
                columns: new[] { "last_sign_in_at", "id" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ux_users_email_normalized",
                table: "users",
                column: "email_normalized",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_users_name_key_name_tag",
                table: "users",
                columns: new[] { "name_key", "name_tag" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_users_username_normalized_name_tag",
                table: "users",
                columns: new[] { "username_normalized", "name_tag" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "anticheat_module_reports");

            migrationBuilder.DropTable(
                name: "anticheat_new_account_games");

            migrationBuilder.DropTable(
                name: "anticheat_probe_reports");

            migrationBuilder.DropTable(
                name: "anticheat_quickmatch_reports");

            migrationBuilder.DropTable(
                name: "api_tokens");

            migrationBuilder.DropTable(
                name: "application_settings");

            migrationBuilder.DropTable(
                name: "audit_log");

            migrationBuilder.DropTable(
                name: "automated_rule_matches");

            migrationBuilder.DropTable(
                name: "automated_rule_versions");

            migrationBuilder.DropTable(
                name: "automated_rules");

            migrationBuilder.DropTable(
                name: "blocks");

            migrationBuilder.DropTable(
                name: "chat_group_members");

            migrationBuilder.DropTable(
                name: "chat_groups");

            migrationBuilder.DropTable(
                name: "chat_messages");

            migrationBuilder.DropTable(
                name: "chat_read_markers");

            migrationBuilder.DropTable(
                name: "client_builds");

            migrationBuilder.DropTable(
                name: "connection_reports");

            migrationBuilder.DropTable(
                name: "data_protection_keys");

            migrationBuilder.DropTable(
                name: "devices");

            migrationBuilder.DropTable(
                name: "erased_accounts");

            migrationBuilder.DropTable(
                name: "flag_revisions");

            migrationBuilder.DropTable(
                name: "flags");

            migrationBuilder.DropTable(
                name: "friend_requests");

            migrationBuilder.DropTable(
                name: "friendships");

            migrationBuilder.DropTable(
                name: "game_sessions");

            migrationBuilder.DropTable(
                name: "identifier_hashes");

            migrationBuilder.DropTable(
                name: "identities");

            migrationBuilder.DropTable(
                name: "leaderboard_entries");

            migrationBuilder.DropTable(
                name: "lobby_ids");

            migrationBuilder.DropTable(
                name: "match_id_workers");

            migrationBuilder.DropTable(
                name: "match_players");

            migrationBuilder.DropTable(
                name: "match_publications");

            migrationBuilder.DropTable(
                name: "name_reservations");

            migrationBuilder.DropTable(
                name: "oauth_scopes");

            migrationBuilder.DropTable(
                name: "oauth_tokens");

            migrationBuilder.DropTable(
                name: "player_network");

            migrationBuilder.DropTable(
                name: "player_preferences");

            migrationBuilder.DropTable(
                name: "player_ratings");

            migrationBuilder.DropTable(
                name: "player_reports");

            migrationBuilder.DropTable(
                name: "player_stats");

            migrationBuilder.DropTable(
                name: "realtime_lease");

            migrationBuilder.DropTable(
                name: "role_permissions");

            migrationBuilder.DropTable(
                name: "sanction_evidence");

            migrationBuilder.DropTable(
                name: "sanctions");

            migrationBuilder.DropTable(
                name: "service_account_permissions");

            migrationBuilder.DropTable(
                name: "session_events");

            migrationBuilder.DropTable(
                name: "text_rule_exceptions");

            migrationBuilder.DropTable(
                name: "text_rule_terms");

            migrationBuilder.DropTable(
                name: "text_rule_workflows");

            migrationBuilder.DropTable(
                name: "user_agreements");

            migrationBuilder.DropTable(
                name: "user_recovery_codes");

            migrationBuilder.DropTable(
                name: "user_roles");

            migrationBuilder.DropTable(
                name: "user_username_history");

            migrationBuilder.DropTable(
                name: "clients");

            migrationBuilder.DropTable(
                name: "matches");

            migrationBuilder.DropTable(
                name: "oauth_authorizations");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropTable(
                name: "service_accounts");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "oauth_applications");
        }
    }
}
