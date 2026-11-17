/*
**    GeneralsOnline Game Services - Backend Services for Command & Conquer Generals Online: Zero Hour
**    Copyright (C) 2025  GeneralsOnline Development Team
**
**    This program is free software: you can redistribute it and/or modify
**    it under the terms of the GNU Affero General Public License as
**    published by the Free Software Foundation, either version 3 of the
**    License, or (at your option) any later version.
**
**    This program is distributed in the hope that it will be useful,
**    but WITHOUT ANY WARRANTY; without even the implied warranty of
**    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
**    GNU Affero General Public License for more details.
**
**    You should have received a copy of the GNU Affero General Public License
**    along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

using System.Text.Json;
using System.Text.Json.Serialization;

namespace GenOnlineService.Portal.Client
{
	public static class PortalJson
	{
		public static readonly JsonSerializerOptions Options = new JsonSerializerOptions
		{
			PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
		};
	}

	public static class PortalRoutes
	{
		public const string Overview = "api/v1/staff/overview";
		public const string Reports = "api/v1/reports";
		public const string ReportSearch = "api/v1/reports/search";
		public const string Players = "api/v1/players";
		public const string PlayerSearch = "api/v1/players/search";
		public const string Sanctions = "api/v1/sanctions";
		public const string Matches = "api/v1/matches";
		public const string AntiCheat = "api/v1/anticheat_reports";
		public const string AuditEvents = "api/v1/audit_events";
		public const string MatchSearch = "api/v1/matches/search";
		public const string SignIn = "users/sign_in";
		public const string SignOut = "users/sign_out";

		public const string AccountPage = "/profile";
		// the logo of the sign-in pages (PortalLook.LogoPath on the host)
		public const string LogoPath = "/portal/logo.png";
		public const string AdminPages = "/admin";
		public const string OverviewPage = AdminPages;
		public const string ReportsPage = "/admin/reports";
		public const string PlayersPage = "/admin/players";
		public const string MatchesPage = "/admin/matches";
		public const string AntiCheatPage = "/admin/anticheat";
		public const string AuditPage = "/admin/audit";

		public static string PlayerPage(long id) => PlayersPage + "/" + id;

		public static string PlayersSearch(string term) => PlayersPage + "?q=" + Uri.EscapeDataString(term);

		public static string MatchPage(long id) => MatchesPage + "/" + id;

		public static string MatchesOf(long player) => MatchesPage + "?player=" + player.ToString(System.Globalization.CultureInfo.InvariantCulture);

		public static string AntiCheatList(string kind) => AntiCheatPage + "/" + kind;

		public static string AuditOf(long target) => AuditPage + "?target=" + target;
	}

	public static class PortalPermissions
	{
		public const string Kick = "moderation.kick";
		public const string Mute = "moderation.mute";
		public const string Ban = "moderation.ban";
		public const string Rename = "moderation.rename";
		public const string AnticheatReview = "anticheat.review";
		public const string PlayersInspect = "players.inspect";
		public const string RolesManage = "roles.manage";
		public const string AuditRead = "audit.read";
	}

	public static class PortalAuditActions
	{
		public static readonly IReadOnlyList<string> All =
		[
			"role_grant", "role_revoke", "kick", "mute", "unmute", "ban", "unban", "rename", "name_reset",
			"message_delete", "message_lookup", "staff_message", "report_view", "report_close", "player_inspect",
			"connection_lookup", "anticheat_review", "announcement", "setting_update", "token_mint", "token_rotate",
			"token_revoke", "sa_create", "sa_grant", "sa_revoke", "sa_disable", "sa_enable", "password_reset",
			"two_factor_reset", "revoke_sessions", "revoke_client_sessions", "client_add"
		];
	}

	public sealed record PortalReviewList(string Kind, string Title, string ShortTitle, string Description)
	{
		public static readonly IReadOnlyList<PortalReviewList> All =
		[
			new PortalReviewList("new_accounts", "New accounts in quickmatch", "New accounts", "New accounts that played a quickmatch game."),
			new PortalReviewList("modules", "Unrecognized modules", "Modules", "Modules a game client loaded that are not on the allow list."),
			new PortalReviewList("probes", "Suspicious probes", "Probes", "Missing or malformed answers to an anti-cheat probe inside a match."),
			new PortalReviewList("devices", "Suspicious device ids", "Device ids", "Sign-ins with a device value that lacks the marker of a genuine client."),
			new PortalReviewList("quickmatch_rules", "Illegal quick match actions", "Quick match", "Quick match requests a genuine client never sends, such as choosing an army in a random army playlist.")
		];

		public static PortalReviewList? Find(string? kind) => All.FirstOrDefault(list => list.Kind == kind);

		public static string IconOf(string kind) => kind switch
		{
			"new_accounts" => "user-plus",
			"modules" => "puzzle",
			"probes" => "radar",
			_ => "fingerprint-pattern"
		};
	}

	// The unreviewed rows of one review list on the dashboard
	public sealed record PortalQueue
	{
		public string Kind { get; init; } = "";
		public int Unreviewed { get; init; }
	}

	public sealed record PortalOverviewSanction
	{
		public long Id { get; init; }
		public string Type { get; init; } = "";
		public PortalMember Player { get; init; } = new PortalMember();
		public string? Reason { get; init; }
		public DateTime CreatedAt { get; init; }
		public DateTime? ExpiresAt { get; init; }
		public DateTime? LiftedAt { get; init; }
		public PortalMember? CreatedBy { get; init; }
		public bool Active { get; init; }
	}

	// The staff dashboard: live counts, the queues, and the newest reports and sanctions. The anti-cheat numbers are
	// null for staff without anticheat.review
	public sealed record PortalOverview
	{
		public int PlayersOnline { get; init; }
		public int LobbiesOpen { get; init; }
		public int MatchesToday { get; init; }
		public int OpenReports { get; init; }
		public int? UnreviewedAnticheat { get; init; }
		public IReadOnlyList<PortalQueue>? Anticheat { get; init; }
		public IReadOnlyList<PortalReport> RecentReports { get; init; } = [];
		public IReadOnlyList<PortalOverviewSanction> RecentSanctions { get; init; } = [];
	}

	public sealed record PortalSessionInfo
	{
		public long UserId { get; init; }
		public string DisplayName { get; init; } = "";
		public string? Tag { get; init; }
		public IReadOnlyList<string> Roles { get; init; } = [];
		public IReadOnlyList<string> Permissions { get; init; } = [];
		public bool Staff { get; init; }
		// from the page's own csrf meta tags, not from the session JSON
		[JsonIgnore]
		public string CsrfHeader { get; init; } = "";
		[JsonIgnore]
		public string CsrfToken { get; init; } = "";

		[JsonIgnore]
		public string FullName => Tag == null ? DisplayName : DisplayName + "#" + Tag;

		public bool Can(string permission) => Permissions.Contains(permission);
	}

	public sealed record PortalMember
	{
		public long Id { get; init; }
		public string Name { get; init; } = "";
		public string? Tag { get; init; }

		[JsonIgnore]
		public string FullName => Tag == null ? Name : Name + "#" + Tag;
	}

	// One row of the staff queue, the fields of /api/v1/reports
	public sealed record PortalReport
	{
		public long Id { get; init; }
		public PortalMember Reporter { get; init; } = new PortalMember();
		public PortalMember Reported { get; init; } = new PortalMember();
		public string? MessageId { get; init; }
		public string? Reason { get; init; }
		public string Status { get; init; } = "";
		public DateTime CreatedAt { get; init; }
		public DateTime? ResolvedAt { get; init; }
		public string? ResolutionNote { get; init; }

		// Flat values for the grid columns
		[JsonIgnore]
		public string ReporterName => Reporter.FullName;
		[JsonIgnore]
		public string ReportedName => Reported.FullName;
	}

	// One page of the queue and the size of the whole filtered queue
	public sealed record PortalReportPage
	{
		public IReadOnlyList<PortalReport> Reports { get; init; } = [];
		public int Total { get; init; }
	}

	public sealed record PortalResolveRequest
	{
		public string? Status { get; init; }
		public string? ResolutionNote { get; init; }
	}

	public sealed record PortalPlayerRow
	{
		public long Id { get; init; }
		public string Name { get; init; } = "";
		public string? Tag { get; init; }
		public string State { get; init; } = "";
		public DateTime CreatedAt { get; init; }
		public DateTime? LastSignInAt { get; init; }
		public bool Online { get; init; }
		public bool Banned { get; init; }
		public bool Muted { get; init; }
		public IReadOnlyList<string> Roles { get; init; } = [];
		public bool Flagged { get; init; }
		public int Matches { get; init; }
		public int? Rating { get; init; }

		[JsonIgnore]
		public string FullName => Tag == null ? Name : Name + "#" + Tag;
	}

	public sealed record PortalPlayerPage
	{
		public IReadOnlyList<PortalPlayerRow> Players { get; init; } = [];
		public int Total { get; init; }
	}

	public sealed record PortalPlayerSession
	{
		public string Kind { get; init; } = "";
		public string ClientId { get; init; } = "";
		public DateTime RegisteredAt { get; init; }
	}

	public sealed record PortalSanction
	{
		public long Id { get; init; }
		public string Type { get; init; } = "";
		public string? Reason { get; init; }
		public DateTime CreatedAt { get; init; }
		public DateTime? ExpiresAt { get; init; }
		public DateTime? LiftedAt { get; init; }
		public PortalMember? CreatedBy { get; init; }
		public bool Active { get; init; }
	}

	public sealed record PortalPlayer
	{
		public long Id { get; init; }
		public string Name { get; init; } = "";
		public string? Tag { get; init; }
		public string State { get; init; } = "";
		public DateTime CreatedAt { get; init; }
		public DateTime? LastSignInAt { get; init; }
		public IReadOnlyList<string> Roles { get; init; } = [];
		public bool Online { get; init; }
		public long? LobbyId { get; init; }
		public IReadOnlyList<PortalPlayerSession> Sessions { get; init; } = [];
		public int EloRating { get; init; }
		public int MonthlyEloRating { get; init; }
		public int EloMatches { get; init; }
		public IReadOnlyList<PortalSanction> Sanctions { get; init; } = [];

		// Every role row with its source: "local", or "upstream:<provider>" for a role that follows that provider and cannot be changed here
		public IReadOnlyList<PortalRoleGrant> RoleGrants { get; init; } = [];

		// The roles the caller may grant or revoke on this player: roles.manage and the rank rule, empty otherwise
		public IReadOnlyList<string> AssignableRoles { get; init; } = [];

		// False where the sign-in provider owns the names
		public bool CanRename { get; init; }

		[JsonIgnore]
		public string FullName => Tag == null ? Name : Name + "#" + Tag;
	}

	public sealed record PortalRoleGrant
	{
		public string Role { get; init; } = "";
		public string Source { get; init; } = "";

		[JsonIgnore]
		public bool Local => Source == "local";

		[JsonIgnore]
		public string Provider => Source.StartsWith("upstream:", StringComparison.Ordinal) ? Source.Substring("upstream:".Length) : Source;
	}

	public sealed record PortalIdentity
	{
		public string Provider { get; init; } = "";
		public string ExternalId { get; init; } = "";
		public DateTime LinkedAt { get; init; }
		public DateTime? LastUsedAt { get; init; }

		public string? ProfileUrl { get; init; }
	}

	public sealed record PortalDevice
	{
		public string Kind { get; init; } = "";
		public string Fingerprint { get; init; } = "";
		public DateTime FirstSeenAt { get; init; }
		public DateTime LastSeenAt { get; init; }
		public string? LastIp { get; init; }
	}

	public sealed record PortalAddress
	{
		public string Ip { get; init; } = "";
		public string? Country { get; init; }
		public DateTime FirstSeenAt { get; init; }
		public DateTime LastSeenAt { get; init; }
		public int Events { get; init; }
	}

	public sealed record PortalDuplicate
	{
		public long Id { get; init; }
		public string Name { get; init; } = "";
		public string? Tag { get; init; }
		public string State { get; init; } = "";
		public string Match { get; init; } = "";
		public int Shared { get; init; }
		public DateTime LastSeenAt { get; init; }

		[JsonIgnore]
		public string FullName => Tag == null ? Name : Name + "#" + Tag;
	}

	public sealed record PortalInspection
	{
		public string? LastIp { get; init; }
		public string? LastCountry { get; init; }
		public DateTime? LastSeenAt { get; init; }
		public IReadOnlyList<PortalIdentity> Identities { get; init; } = [];
		public IReadOnlyList<PortalDevice> Devices { get; init; } = [];
		public IReadOnlyList<PortalAddress> Addresses { get; init; } = [];
		public IReadOnlyList<PortalDuplicate> Duplicates { get; init; } = [];
	}

	public sealed record PortalSanctionRequest
	{
		public string? Type { get; init; }
		public long? DurationS { get; init; }
		public string? Reason { get; init; }
	}

	public sealed record PortalReasonRequest
	{
		public string? Reason { get; init; }
	}

	public sealed record PortalRenameRequest
	{
		public string? Name { get; init; }
		public string? Reason { get; init; }
	}

	public sealed record PortalMatchFile
	{
		public string Name { get; init; } = "";
		public string Type { get; init; } = "";
	}

	public sealed record PortalMatchSlot
	{
		public string State { get; init; } = "";
		public long? UserId { get; init; }
		public string Name { get; init; } = "";
		public string? Tag { get; init; }
		public int Side { get; init; }
		public int? Team { get; init; }
		public bool Won { get; init; }
		public bool Desynced { get; init; }
		public long? DurationS { get; init; }
		public IReadOnlyList<PortalMatchFile> Files { get; init; } = [];

		[JsonIgnore]
		public string FullName => Tag == null ? Name : Name + "#" + Tag;
	}

	public sealed record PortalMatchMap
	{
		public string Name { get; init; } = "";
		public bool Official { get; init; }
	}

	public sealed record PortalMatchBuild
	{
		public long ExeCrc { get; init; }
		public long IniCrc { get; init; }
	}

	public sealed record PortalMatch
	{
		public long Id { get; init; }
		public long? OwnerId { get; init; }
		public string Name { get; init; } = "";
		public string Type { get; init; } = "";
		public string RosterType { get; init; } = "";
		public PortalMatchMap Map { get; init; } = new PortalMatchMap();
		public PortalMatchBuild Build { get; init; } = new PortalMatchBuild();
		public DateTime StartedAt { get; init; }
		public DateTime? FinishedAt { get; init; }
		public IReadOnlyList<PortalMatchSlot> Slots { get; init; } = [];
	}

	// One row of a review list, the fields of /api/v1/anticheat_reports/:kind
	public sealed record PortalAntiCheatReport
	{
		public long Id { get; init; }
		public PortalMember Player { get; init; } = new PortalMember();
		public long? MatchId { get; init; }
		public string? ModuleName { get; init; }
		public string? ModulePath { get; init; }
		public long? ModuleSize { get; init; }
		public string? Reason { get; init; }
		public long? LobbyId { get; init; }
		public int? PlaylistId { get; init; }
		public string? Method { get; init; }
		public IReadOnlyList<int>? Sides { get; init; }
		public DateTime? ReportedAt { get; init; }
		public DateTime? ReviewedAt { get; init; }

		public string SideList => Sides == null ? "" : String.Join(", ", Sides);
	}

	public sealed record PortalAntiCheatPage
	{
		public IReadOnlyList<PortalAntiCheatReport> Reports { get; init; } = [];
		public int Total { get; init; }
	}

	public sealed record PortalReviewRequest
	{
		public bool Reviewed { get; init; }
	}

	public sealed record PortalAuditActor
	{
		public long Id { get; init; }
		public string? DisplayName { get; init; }
	}

	public sealed record PortalAuditEvent
	{
		public long Id { get; init; }
		public string Action { get; init; } = "";
		public string ActorKind { get; init; } = "";
		public string ActorName { get; init; } = "";
		public PortalAuditActor? Actor { get; init; }
		public string TargetKind { get; init; } = "";
		public long? TargetId { get; init; }
		public string? Reason { get; init; }
		public string Source { get; init; } = "";
		public JsonElement? Details { get; init; }
		public DateTime CreatedAt { get; init; }
	}

	public sealed record PortalAuditPage
	{
		public IReadOnlyList<PortalAuditEvent> AuditEvents { get; init; } = [];
	}

	public sealed record PortalError
	{
		public string Error { get; init; } = "";
	}

	public sealed record PortalPlayerQuery
	{
		public const string Online = "online";
		public const string Banned = "banned";
		public const string Muted = "muted";
		public const string Staff = "staff";
		public const string Flagged = "flagged";

		public static readonly string[] AllFilters = [Online, Banned, Muted, Staff, Flagged];

		public string? Search { get; init; }
		public IReadOnlyList<string> Filters { get; init; } = [];
		public string? SignedInWithin { get; init; }
		public string? OrderBy { get; init; }
		public string? Sort { get; init; }

		public bool IsFiltered => Filters.Count > 0 || SignedInWithin != null;

		// Unknown values in a typed or old address are dropped, so the list still opens
		// A direction with a search but no order is left out: the host keeps a search's own order and refuses that pair
		public static PortalPlayerQuery FromAddress(string? q, string? filter, string? signedInWithin, string? orderBy, string? sort) => new PortalPlayerQuery
		{
			Search = String.IsNullOrWhiteSpace(q) ? null : q.Trim(),
			Filters = (filter ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(AllFilters.Contains).Distinct().ToList(),
			SignedInWithin = signedInWithin is "24h" or "7d" or "30d" ? signedInWithin : null,
			OrderBy = orderBy is "last_sign_in_at" or "created_at" or "name" or "matches" ? orderBy : null,
			Sort = sort is "asc" or "desc" && (String.IsNullOrWhiteSpace(q) || orderBy is "last_sign_in_at" or "created_at" or "name" or "matches") ? sort : null
		};

		public Dictionary<string, object?> ToAddress() => new Dictionary<string, object?>
		{
			["q"] = Search,
			["filter"] = Filters.Count == 0 ? null : String.Join(',', Filters),
			["signed_in_within"] = SignedInWithin,
			["order_by"] = OrderBy,
			["sort"] = Search != null && OrderBy == null ? null : Sort
		};

		public string ToApiQuery() => PortalQueries.Join(new Dictionary<string, object?>(ToAddress().Where(a => a.Key != "q")) { ["query"] = Search });
	}

	public sealed record PortalMatchRowPlayer
	{
		public long UserId { get; init; }
		public string Name { get; init; } = "";
		public string? Tag { get; init; }
		public int? Team { get; init; }
		public bool Won { get; init; }
		public bool Desynced { get; init; }

		[JsonIgnore]
		public string FullName => Tag == null ? Name : Name + "#" + Tag;
	}

	public sealed record PortalMatchRow
	{
		public long Id { get; init; }
		public string Name { get; init; } = "";
		public string? Type { get; init; }
		public PortalMatchMap Map { get; init; } = new PortalMatchMap();
		public DateTime StartedAt { get; init; }
		public DateTime? FinishedAt { get; init; }
		public long? DurationS { get; init; }
		public IReadOnlyList<PortalMatchRowPlayer> Players { get; init; } = [];
		public IReadOnlyList<string> Flags { get; init; } = [];

		// What the match came to for one of its players: won, lost when another player won, no result when nobody did,
		// in progress while it runs
		public string ResultFor(long userID) =>
			FinishedAt == null ? "in progress"
			: Players.Any(p => p.UserId == userID && p.Won) ? "won"
			: Players.Any(p => p.Won) ? "lost"
			: "no result";
	}

	// Total is null when the host stopped counting (past 10,000 rows); HasNext says whether another page follows
	public sealed record PortalMatchPage
	{
		public IReadOnlyList<PortalMatchRow> Matches { get; init; } = [];
		public int? Total { get; init; }
		public bool HasNext { get; init; }
	}

	public sealed record PortalMatchQuery
	{
		public string? Type { get; init; }
		public string? Map { get; init; }
		public string? Player { get; init; }
		// yyyy-MM-dd, UTC; To includes its day
		public string? From { get; init; }
		public string? To { get; init; }
		public bool Flagged { get; init; }

		public bool IsFiltered => Type != null || Map != null || Player != null || From != null || To != null || Flagged;

		public static PortalMatchQuery FromAddress(string? type, string? map, string? player, string? from, string? to, string? flagged) => new PortalMatchQuery
		{
			Type = type is "quick_match" or "custom" ? type : null,
			Map = String.IsNullOrWhiteSpace(map) ? null : map.Trim(),
			Player = String.IsNullOrWhiteSpace(player) ? null : player.Trim(),
			From = Day(from),
			To = Day(to),
			Flagged = flagged == "true"
		};

		public Dictionary<string, object?> ToAddress() => new Dictionary<string, object?>
		{
			["type"] = Type,
			["map"] = Map,
			["player"] = Player,
			["from"] = From,
			["to"] = To,
			["flagged"] = Flagged ? "true" : null
		};

		public string ToApiQuery() => PortalQueries.Join(ToAddress());

		public static PortalMatchQuery OfPlayer(long id) => new PortalMatchQuery { Player = id.ToString(System.Globalization.CultureInfo.InvariantCulture) };

		private static string? Day(string? text) =>
			DateTime.TryParseExact(text, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _) ? text : null;
	}

	public static class PortalQueries
	{
		public static string Join(IEnumerable<KeyValuePair<string, object?>> arguments) =>
			String.Join('&', arguments.Where(a => a.Value != null).Select(a => a.Key + "=" + Uri.EscapeDataString(a.Value!.ToString()!)));
	}
}
