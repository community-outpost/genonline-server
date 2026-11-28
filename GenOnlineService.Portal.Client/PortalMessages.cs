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
		public const string Overview = "api/v1/admin/overview";
		public const string Appeals = "api/v1/appeals";
		public const string Cases = "api/v1/admin/cases";
		public const string AdminPlayers = "api/v1/admin/players";
		public const string Players = "api/v1/players";
		public const string PlayerSearch = "api/v1/players/search";
		public const string Sanctions = "api/v1/sanctions";
		public const string Matches = "api/v1/matches";
		public const string AuditEvents = "api/v1/audit_events";
		public const string ServiceAccounts = "api/v1/service_accounts";
		public const string MatchSearch = "api/v1/matches/search";
		public const string Settings = "api/v1/application/settings";
		public const string Rooms = "api/v1/admin/rooms";
		public const string Announcements = "api/v1/announcements";
		public const string Messages = "api/v1/messages";
		public const string ChatLogs = "api/v1/admin/chat";
		public const string StaffRealtime = "api/v1/admin/realtime";
		public const string StaffLobbies = "api/v1/admin/lobbies";
		public const string Lobbies = "api/v1/lobbies";
		public const string TextRules = "api/v1/text_rules";
		public const string StaffInvestigate = "api/v1/admin/investigate";
		public const string Flags = "api/v1/flags";
		public const string Playlists = "api/v1/admin/playlists";
		public const string FlagRevisions = "api/v1/flags/revisions";
		public const string Rules = "api/v1/rules";
		public const string RuleMatches = "api/v1/rules/matches";
		public const string Roles = "api/v1/roles";
		public const string Permissions = "api/v1/permissions";
		public const string SignIn = "session/sign_in";
		public const string SignOut = "session/sign_out";

		public const string AccountPage = "/profile";
		public const string LogoPath = "/portal/logo.svg";
		public const string AdminPages = "/admin";
		public const string OverviewPage = AdminPages;
		public const string AppealsPage = "/admin/appeals";
		public const string CasesPage = "/admin/cases";
		public const string PlayersPage = "/admin/players";
		public const string MatchesPage = "/admin/matches";
		public const string AuditPage = "/admin/audit";
		public const string PartnersPage = "/admin/partners";
		public const string ApiDocs = "/api/docs";
		public const string SettingsPage = "/admin/settings";
		public const string AnnouncementsPage = "/admin/announcements";
		public const string LivePage = "/admin/live";
		public const string LobbiesPage = "/admin/lobbies";
		public const string TextRulesPage = "/admin/text-rules";
		public const string InvestigatePage = "/admin/investigate";
		public const string ChatLogsPage = "/admin/chat";
		public const string ConfigPage = "/admin/config";
		public const string PlaylistsPage = "/admin/playlists";
		public const string RulesPage = "/admin/rules";
		public const string RuleMatchesPage = "/admin/rules/matches";
		public const string RolesPage = "/admin/roles";
		public const string RoomsPage = "/admin/rooms";

		public static string PlayerPage(long id) => PlayersPage + "/" + id;

		public static string PlayerCasePage(long userID) => PlayersPage + "/" + userID + "/case";
		public static string DecidedCasePage(long caseID) => CasesPage + "/" + caseID;

		public static string RulePage(long id) => RulesPage + "/" + id;

		public static string RolePage(string name) => RolesPage + "/" + Uri.EscapeDataString(name);

		public static string RuleMatchesOf(long id) => RuleMatchesPage + "?rule=" + id.ToString(System.Globalization.CultureInfo.InvariantCulture);

		public static string PlayerTab(long id, string tab) => PlayerPage(id) + "?tab=" + tab;

		public static string PlayersSearch(string term) => PlayersPage + "?q=" + Uri.EscapeDataString(term);

		public static string MatchPage(long id) => MatchesPage + "/" + id;

		public static string PartnerPage(long id) => PartnersPage + "/" + id;

		public static string LobbyPage(long id) => LobbiesPage + "/" + id;

		public static string MatchesOf(long player) => MatchesPage + "?player=" + player.ToString(System.Globalization.CultureInfo.InvariantCulture);

		public static string AuditOf(long target) => AuditPage + "?target=" + target;

		public static string ChatLogsOf(string conversation) => ChatLogsPage + "?conversation=" + Uri.EscapeDataString(conversation);

		public static string InvestigateOf(string kind, string value) => InvestigatePage + "?kind=" + Uri.EscapeDataString(kind) + "&value=" + Uri.EscapeDataString(value);

		public static string InvestigateDevice(string kind, string key) => InvestigatePage + "?kind=" + Uri.EscapeDataString(kind) + "&key=" + Uri.EscapeDataString(key);
	}

	public static class PortalPermissions
	{
		public const string Kick = "moderation.kick";
		public const string Mute = "moderation.mute";
		public const string Ban = "moderation.ban";
		public const string Rename = "moderation.rename";
		public const string AnticheatReview = "anticheat.review";
		public const string AppealsReview = "appeals.review";
		public const string PlayersInspect = "players.inspect";
		public const string RolesManage = "roles.manage";
		public const string AuditRead = "audit.read";
		public const string TokensManage = "tokens.manage";
		public const string ChatDeleteAny = "chat.delete_any";
		public const string ChatLogs = "chat.logs";
		public const string AnnouncementsSend = "announcements.send";
		public const string SettingsManage = "settings.manage";
		public const string ServerStats = "server.stats";
		public const string AccountsSupport = "accounts.support";
		public const string TextRulesManage = "textrules.manage";
		public const string FlagsManage = "flags.manage";
		public const string PlaylistsManage = "playlists.manage";
		public const string RulesManage = "rules.manage";
		public const string ChannelsManage = "channels.manage";
	}

	public static class PortalPartnerPermissions
	{
		public static readonly IReadOnlyList<string> Grantable = ["lobbies.read", "match_history.read"];

		public static readonly IReadOnlyList<string> Privileged = ["ops.database", "users.revoke"];

		public static bool IsPrivileged(string permission) => Privileged.Contains(permission);
	}

	public static class PortalAuditActions
	{
		// An action in words for the filter and the log; an action without words shows as stored
		public static string LabelOf(string action) => action switch
		{
			"role_grant" => "Role granted",
			"appeal_open" => "Appeal opened",
			"appeal_decide" => "Appeal decided",
			"role_create" => "Role created",
			"role_update" => "Role changed",
			"role_delete" => "Role deleted",
			"role_revoke" => "Role revoked",
			"kick" => "Kick",
			"mute" => "Mute",
			"unmute" => "Mute lifted",
			"ban" => "Ban",
			"unban" => "Ban lifted",
			"rename" => "Rename",
			"name_reset" => "Name reset",
			"message_delete" => "Message deleted",
			"message_lookup" => "Message looked up",
			"chat_log_view" => "Chat log read",
			"staff_message" => "Staff message",
			"report_view" => "Report viewed",
			"report_close" => "Report closed",
			"player_inspect" => "Personal data read",
			"device_reveal" => "Device values revealed",
			"connection_lookup" => "Connections looked up",
			"anticheat_review" => "Anti-cheat row reviewed",
			"announcement" => "Announcement",
			"setting_update" => "Setting changed",
			"room_create" => "Chat room added",
			"room_update" => "Chat room changed",
			"token_mint" => "Token created",
			"token_rotate" => "Token rotated",
			"token_revoke" => "Token revoked",
			"sa_create" => "Service account created",
			"sa_grant" => "Service account granted",
			"sa_revoke" => "Service account revoked",
			"sa_disable" => "Service account disabled",
			"sa_enable" => "Service account enabled",
			"password_reset" => "Password reset",
			"two_factor_reset" => "Two-factor reset",
			"revoke_sessions" => "Signed out everywhere",
			"revoke_client_sessions" => "Client sessions revoked",
			"client_add" => "Client added",
			"sa_update" => "Service account changed",
			"text_term_add" => "Word added",
			"text_term_update" => "Word changed",
			"text_term_remove" => "Word removed",
			"text_exception_add" => "Exception added",
			"text_exception_remove" => "Exception removed",
			"text_workflow_update" => "Text rule changed",
			"text_rules_seed" => "Word lists imported",
			"investigate_search" => "Investigation search",
			"flag_create" => "Config field added",
			"flag_update" => "Config field changed",
			"flag_archive" => "Config field archived",
			"flag_restore" => "Config field restored",
			"flags_publish" => "Game config published",
			"flags_rollback" => "Game config rolled back",
			"flags_import" => "Game config imported",
			"flags_reset" => "Game config draft reset",
			"playlist_create" => "Playlist added",
			"playlist_update" => "Playlist changed",
			"playlist_delete" => "Playlist deleted",
			"playlist_map_change" => "Playlist maps changed",
			"rule_create" => "Automated rule added",
			"rule_version" => "Automated rule saved",
			"rule_update" => "Automated rule changed",
			"rule_archive" => "Automated rule archived",
			"rule_restore" => "Automated rule restored",
			"rule_breaker" => "Automated rule fell back to Flag",
			"rule_match_review" => "Rule match reviewed",
			"rule_preview" => "Automated rule previewed",
			"rule_sweep_start" => "Automated rule sweep started",
			"rule_sweep_cancel" => "Automated rule sweep cancelled",
			_ => action
		};

		public static readonly IReadOnlyList<string> All =
		[
			"appeal_open", "appeal_decide", "role_grant", "role_revoke", "role_create", "role_update", "role_delete", "kick", "mute", "unmute", "ban", "unban", "rename", "name_reset",
			"message_delete", "message_lookup", "chat_log_view", "staff_message", "report_view", "report_close", "player_inspect",
			"device_reveal", "connection_lookup", "anticheat_review", "announcement", "setting_update", "room_create", "room_update", "token_mint", "token_rotate",
			"token_revoke", "sa_create", "sa_update", "sa_grant", "sa_revoke", "sa_disable", "sa_enable", "password_reset",
			"two_factor_reset", "revoke_sessions", "revoke_client_sessions", "client_add", "text_term_add", "text_term_update",
			"text_term_remove", "text_exception_add", "text_exception_remove", "text_workflow_update", "text_rules_seed", "investigate_search", "flag_create", "flag_update", "flag_archive", "flag_restore", "flags_publish", "flags_rollback", "flags_import", "flags_reset",
			"playlist_create", "playlist_update", "playlist_delete", "playlist_map_change",
			"rule_create", "rule_version", "rule_update", "rule_archive", "rule_restore", "rule_breaker", "rule_match_review", "rule_preview", "rule_sweep_start", "rule_sweep_cancel"
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

	public sealed record PortalQueue
	{
		public string Kind { get; init; } = "";
		public int Waiting { get; init; }
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

	// the anti-cheat and rule kinds of Waiting are left out for staff without anticheat.review
	public sealed record PortalOverview
	{
		public int PlayersOnline { get; init; }
		public int LobbiesOpen { get; init; }
		public int MatchesToday { get; init; }
		public int PlayersToReview { get; init; }
		public IReadOnlyList<PortalQueue> Waiting { get; init; } = [];
		public IReadOnlyList<PortalCaseRow> NextCases { get; init; } = [];
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

	public sealed record PortalAppeal
	{
		public long Id { get; init; }
		public long SanctionId { get; init; }
		public PortalMember Player { get; init; } = new PortalMember();
		public string Status { get; init; } = "";
		public string Text { get; init; } = "";
		public DateTime CreatedAt { get; init; }
		public DateTime? DecidedAt { get; init; }
		public string? DecisionReason { get; init; }
		public PortalMember? DecidedBy { get; init; }
		public string SanctionType { get; init; } = "";
		public string? SanctionReason { get; init; }
		public DateTime SanctionCreatedAt { get; init; }
		public DateTime? SanctionExpiresAt { get; init; }
		public DateTime? SanctionLiftedAt { get; init; }
	}

	public sealed record PortalAppealList
	{
		public IReadOnlyList<PortalAppeal> Appeals { get; init; } = [];
	}

	public sealed record PortalAppealDecision
	{
		public string? Decision { get; init; }
		public string? Reason { get; init; }
		public DateTime? ExpiresAt { get; init; }
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

		public string? CreatedByKind { get; init; }
		public string? CreatedByName { get; init; }
		public bool Active { get; init; }

		public string? LiftedByKind { get; init; }
		public PortalMember? LiftedBy { get; init; }
		public string? LiftReason { get; init; }

		[JsonIgnore]
		public string? LifterName => LiftedBy?.FullName ?? (LiftedByKind == null ? null : PortalText.ActorKind(LiftedByKind));
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
		public IReadOnlyList<PortalGameRating> Ratings { get; init; } = [];
		public IReadOnlyList<PortalSanction> Sanctions { get; init; } = [];

		// Every role row with its source: "local", or "upstream:<provider>" for a role that follows that provider and cannot be changed here
		public IReadOnlyList<PortalRoleGrant> RoleGrants { get; init; } = [];

		// The roles the caller may grant or revoke on this player: roles.manage and the rank rule, empty otherwise
		public IReadOnlyList<string> AssignableRoles { get; init; } = [];

		// The Discord account the bot treats as this player, for its staff commands only (never a sign-in), and both calls need roles.manage
		public string? DiscordBotUserId { get; init; }
		public bool CanLinkDiscordBot { get; init; }

		// False where the sign-in provider owns the names
		public bool CanRename { get; init; }

		public IReadOnlyList<PortalAgreement> Agreements { get; init; } = [];

		[JsonIgnore]
		public string FullName => Tag == null ? Name : Name + "#" + Tag;
	}

	public sealed record PortalGameRating
	{
		public string Game { get; init; } = "";
		public int EloRating { get; init; }
		public int MonthlyEloRating { get; init; }
		public int EloMatches { get; init; }
	}

	// A document in force and the latest version of it the player agreed to (null: not agreed)
	public sealed record PortalAgreement
	{
		public string Document { get; init; } = "";
		public string Version { get; init; } = "";
		public string? AgreedVersion { get; init; }
		public DateTime? AgreedAt { get; init; }
		public bool Current { get; init; }
	}

	public sealed record PortalRoleDefinition
	{
		public string Name { get; init; } = "";
		public string DisplayName { get; init; } = "";
		public string? Color { get; init; }
		public string? Tag { get; init; }
		public string? Description { get; init; }
		public int Position { get; init; }
		public bool BuiltIn { get; init; }

		// Owner: nobody edits it in the service
		public bool Locked { get; init; }

		// The caller may edit or delete it: it sits below their own highest role
		public bool Manageable { get; init; }
		public IReadOnlyList<string> Permissions { get; init; } = [];
		public int MemberCount { get; init; }
	}

	public sealed record PortalRoleList
	{
		public IReadOnlyList<PortalRoleDefinition> Roles { get; init; } = [];
	}

	public sealed record PortalPermissionEntry
	{
		public string Name { get; init; } = "";
		public string Area { get; init; } = "";
		public string Label { get; init; } = "";
		public string Description { get; init; } = "";
		public bool Held { get; init; }
	}

	public sealed record PortalPermissionList
	{
		public IReadOnlyList<PortalPermissionEntry> Permissions { get; init; } = [];
	}

	public sealed record PortalRoleMember
	{
		public long UserId { get; init; }
		public string Name { get; init; } = "";

		public string Source { get; init; } = "";
		public DateTime GrantedAt { get; init; }
		public string? GrantedBy { get; init; }

		[JsonIgnore]
		public bool Local => Source == "local";
	}

	public sealed record PortalRoleMembers
	{
		public int Total { get; init; }
		public IReadOnlyList<PortalRoleMember> Members { get; init; } = [];
	}

	// A new role takes every field, a change only the fields to change; "" clears the color, the tag or the description
	public sealed record PortalRoleWrite
	{
		public string? Name { get; init; }
		public string? DisplayName { get; init; }
		public string? Color { get; init; }
		public string? Tag { get; init; }
		public string? Description { get; init; }
		public int? Position { get; init; }

		public string? Above { get; init; }
		public IReadOnlyList<string>? Permissions { get; init; }
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

		// The whole keyed hash, the investigation search finds the other accounts of the device with it
		public string Key { get; init; } = "";

		// A MAC address: its vendor in the IEEE registry, or "locally administered / virtual"; null when unknown
		public string? Vendor { get; init; }

		public bool HasValue { get; init; }

		// The value the client sent, only with ?raw=true (audited as device_reveal); null otherwise or when never kept
		public string? Value { get; init; }
	}

	// The identifiers one sign-in sent together, by device key, with every address the machine signed in from
	public sealed record PortalMachine
	{
		public IReadOnlyList<string> Keys { get; init; } = [];
		public DateTime FirstSeenAt { get; init; }
		public DateTime LastSeenAt { get; init; }
		public IReadOnlyList<PortalMachineAddress> Addresses { get; init; } = [];
	}

	public sealed record PortalMachineAddress
	{
		public string Ip { get; init; } = "";
		public string? Country { get; init; }
		public DateTime FirstSeenAt { get; init; }
		public DateTime LastSeenAt { get; init; }
		public int SignIns { get; init; }
	}

	public sealed record PortalAddress
	{
		public string Ip { get; init; } = "";
		public string? Country { get; init; }
		public DateTime FirstSeenAt { get; init; }
		public DateTime LastSeenAt { get; init; }
		public int Events { get; init; }

		public string? City { get; init; }
		public long? Asn { get; init; }
		public string? NetworkOwner { get; init; }
	}

	public sealed record PortalAddressFacts
	{
		public string? Country { get; init; }
		public string? City { get; init; }
		public long? Asn { get; init; }
		public string? NetworkOwner { get; init; }
	}

	// An account that used the searched identifier; events counts its sessions on the addresses, null for devices
	public sealed record PortalInvestigationAccount
	{
		public long Id { get; init; }
		public string Name { get; init; } = "";
		public string? Tag { get; init; }
		public string State { get; init; } = "";
		public DateTime FirstSeenAt { get; init; }
		public DateTime LastSeenAt { get; init; }
		public int? Events { get; init; }

		[JsonIgnore]
		public string FullName => Tag == null ? Name : Name + "#" + Tag;
	}

	public sealed record PortalInvestigation
	{
		public static readonly IReadOnlyList<(string Kind, string Label, string Hint)> Kinds =
		[
			("mac", "MAC address", "AA:BB:CC:DD:EE:FF, with colons, dashes or nothing"),
			("machine_guid", "Machine id", "The Windows machine GUID"),
			("volume_serial", "Volume serial", "1234-ABCD, as the vol command shows it"),
			("fingerprint", "Device fingerprint", "The fingerprint the player page shows, or more of the key"),
			("ip", "IP address", "IPv4 or IPv6"),
			("ip_range", "IP range", "CIDR, at most a /16 for IPv4 and a /48 for IPv6"),
			("country", "Country", "Two letter code, such as NL")
		];

		public static string LabelOf(string kind) => Kinds.FirstOrDefault(k => k.Kind == kind).Label ?? kind;

		public string Kind { get; init; } = "";
		public string Value { get; init; } = "";
		public string? Vendor { get; init; }
		public PortalAddressFacts? Network { get; init; }
		public string? RawValue { get; init; }
		public IReadOnlyList<PortalInvestigationAccount> Accounts { get; init; } = [];

		[JsonIgnore]
		public bool HasNext { get; init; }
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
		public IReadOnlyList<PortalMachine> Machines { get; init; } = [];
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

	public sealed record PortalPasswordReset
	{
		public string Password { get; init; } = "";
	}

	public sealed record PortalRenameRequest
	{
		public string? Name { get; init; }
		public string? Reason { get; init; }
	}

	public sealed record PortalSettings
	{
		public string? Motd { get; init; }
	}

	public sealed record PortalRoom
	{
		public long Id { get; init; }
		public string Name { get; init; } = "";
		public long? ParentId { get; init; }
		public bool IsDefault { get; init; }
		public int Position { get; init; }
		public bool Enabled { get; init; }
		public bool ShowAllMatches { get; init; }
	}

	// Depth first, the siblings in their order, the disabled rooms included
	public sealed record PortalRoomList
	{
		public IReadOnlyList<PortalRoom> Rooms { get; init; } = [];
	}

	public sealed record PortalAnnouncementRequest
	{
		public string? Message { get; init; }
	}

	public sealed record PortalAnnouncementResult
	{
		public int DeliveredTo { get; init; }
	}

	public sealed record PortalMatchFile
	{
		public string Name { get; init; } = "";
		public string Type { get; init; } = "";
		public string? Url { get; init; }
		public string? Status { get; init; }
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
		public string? Game { get; init; }
		public PortalMatchMap Map { get; init; } = new PortalMatchMap();
		public PortalMatchBuild Build { get; init; } = new PortalMatchBuild();
		public DateTime StartedAt { get; init; }
		public DateTime? FinishedAt { get; init; }
		public IReadOnlyList<PortalMatchSlot> Slots { get; init; } = [];
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
		public PortalAuditActor? Target { get; init; }
		public string? Reason { get; init; }
		public string Source { get; init; } = "";
		public JsonElement? Details { get; init; }
		public DateTime CreatedAt { get; init; }
	}

	public sealed record PortalAuditQuery
	{
		public string? Actor { get; init; }
		public string? Target { get; init; }
		public string? Action { get; init; }
		public string? From { get; init; }
		public string? To { get; init; }

		public bool IsFiltered => this != new PortalAuditQuery();

		private static string? Clean(string? text) => String.IsNullOrWhiteSpace(text) ? null : text.Trim();

		// A day that is not yyyy-MM-dd is left out, an unknown action too
		private static string? Day(string? text) => DateTime.TryParseExact(text?.Trim(), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _) ? text!.Trim() : null;

		public static PortalAuditQuery FromAddress(string? actor, string? target, string? action, string? from, string? to) => new PortalAuditQuery
		{
			Actor = Clean(actor),
			Target = Clean(target),
			Action = action != null && PortalAuditActions.All.Contains(action) ? action : null,
			From = Day(from),
			To = Day(to)
		};

		public IReadOnlyDictionary<string, object?> ToAddress() => new Dictionary<string, object?>
		{
			["actor"] = Actor,
			["target"] = Target,
			["action"] = Action,
			["from"] = From,
			["to"] = To
		};

		public string ToApiQuery()
		{
			var parts = new List<string>();
			foreach ((string name, string? value) in new[] { ("actor", Actor), ("target", Target), ("action", Action), ("from", From), ("to", To) })
			{
				if (value != null)
				{
					parts.Add(name + "=" + Uri.EscapeDataString(value));
				}
			}
			return String.Join("&", parts);
		}
	}

	public sealed record PortalChatLogSender
	{
		public long Id { get; init; }
		public string Name { get; init; } = "";
		public string? Tag { get; init; }
	}

	// A deleted line keeps its text for staff, like the cases do
	public sealed record PortalChatLogLine
	{
		public long Id { get; init; }
		public string Conversation { get; init; } = "";
		public PortalChatLogSender Sender { get; init; } = new PortalChatLogSender();

		// Match lines only: everyone, allies or self
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public string? Scope { get; init; }
		public string Text { get; init; } = "";
		public DateTime CreatedAt { get; init; }
		public bool Deleted { get; init; }
	}

	public sealed record PortalChatLogPage
	{
		public IReadOnlyList<PortalChatLogLine> Messages { get; init; } = [];
	}

	public sealed record PortalAuditPage
	{
		public IReadOnlyList<PortalAuditEvent> AuditEvents { get; init; } = [];
	}

	public sealed record PortalError
	{
		public string Error { get; init; } = "";

		public JsonElement? Params { get; init; }

		public string? Field => Params is JsonElement details && details.ValueKind == JsonValueKind.Object && details.TryGetProperty("field", out JsonElement named) && named.ValueKind == JsonValueKind.String ? named.GetString() : null;
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

	public sealed record PortalPartner
	{
		public long Id { get; init; }
		public string Name { get; init; } = "";
		public string? Description { get; init; }
		public DateTime CreatedAt { get; init; }
		public DateTime? DisabledAt { get; init; }
		public IReadOnlyList<string> Permissions { get; init; } = [];
		public int ActiveTokens { get; init; }
		public DateTime? LastTokenUsedAt { get; init; }
	}

	public sealed record PortalPartnerPage
	{
		public IReadOnlyList<PortalPartner> ServiceAccounts { get; init; } = [];
	}

	// A token of a partner, never its secret; revoked_at in the future is the end of a rotation overlap
	public sealed record PortalPartnerToken
	{
		public long Id { get; init; }
		public string Name { get; init; } = "";
		public IReadOnlyList<string> Scopes { get; init; } = [];
		public DateTime CreatedAt { get; init; }
		public DateTime ExpiresAt { get; init; }
		public DateTime? LastUsedAt { get; init; }
		public string? LastUsedIp { get; init; }
		public DateTime? RevokedAt { get; init; }
		public string TokenLast4 { get; init; } = "";
		public IReadOnlyList<string> AllowedIps { get; init; } = [];

		public bool IsActive(DateTime utcNow) => ExpiresAt > utcNow && (RevokedAt == null || RevokedAt > utcNow);
	}

	public sealed record PortalPartnerTokenPage
	{
		public IReadOnlyList<PortalPartnerToken> PersonalAccessTokens { get; init; } = [];
	}

	// The create and rotate answers, the only place the gopat- secret is ever shown
	public sealed record PortalCreatedToken
	{
		public long Id { get; init; }
		public string Name { get; init; } = "";
		public DateTime ExpiresAt { get; init; }
		public string Token { get; init; } = "";
	}

	public sealed record PortalCreatePartnerRequest
	{
		public string? Name { get; init; }
		public string? Description { get; init; }
	}

	public sealed record PortalGrantChanges
	{
		public IReadOnlyList<string>? Add { get; init; }
		public IReadOnlyList<string>? Remove { get; init; }
	}

	// Each part is optional; the null ones are left out of the body, an empty description clears it
	public sealed record PortalUpdatePartnerRequest
	{
		public string? Description { get; init; }
		public PortalGrantChanges? Grants { get; init; }
		public bool? Disabled { get; init; }
	}

	// expires_at is a day (midnight UTC); allowed_ips holds addresses or CIDR ranges
	public sealed record PortalCreateTokenRequest
	{
		public string Name { get; init; } = "";
		public IReadOnlyList<string> Scopes { get; init; } = [];
		public string ExpiresAt { get; init; } = "";
		public IReadOnlyList<string>? AllowedIps { get; init; }
	}

	public static class PortalQueries
	{
		public static string Join(IEnumerable<KeyValuePair<string, object?>> arguments) =>
			String.Join('&', arguments.Where(a => a.Value != null).Select(a => a.Key + "=" + Uri.EscapeDataString(a.Value!.ToString()!)));
	}

	public sealed record PortalRealtime
	{
		public PortalRealtimeCopy Copy { get; init; } = new PortalRealtimeCopy();
		public PortalRealtimeLease? Lease { get; init; }
		public int PlayersOnline { get; init; }
		public int LobbiesOpen { get; init; }
		public IReadOnlyList<PortalRealtimeQueue> Queues { get; init; } = [];
	}

	public sealed record PortalRealtimeCopy
	{
		public string Id { get; init; } = "";
		public bool Serving { get; init; }
		public bool Failover { get; init; }
	}

	// Holder and standby are null for none, expires_in_ms is null once the lease expired
	public sealed record PortalRealtimeLease
	{
		public string? Holder { get; init; }
		public long Epoch { get; init; }
		public long? ExpiresInMs { get; init; }
		public string? Standby { get; init; }
		public bool HandoverRequested { get; init; }
	}

	public sealed record PortalRealtimeQueue
	{
		public int PlaylistId { get; init; }
		public string Name { get; init; } = "";
		public int Queued { get; init; }
	}

	public sealed record PortalLobbyList
	{
		public IReadOnlyList<PortalLobby> Lobbies { get; init; } = [];
	}

	public sealed record PortalLobby
	{
		public long Id { get; init; }
		public string Name { get; init; } = "";
		public long HostId { get; init; }
		public string Type { get; init; } = "";
		public string State { get; init; } = "";
		public string Visibility { get; init; } = "";
		public bool HasPassword { get; init; }
		public PortalMatchMap Map { get; init; } = new PortalMatchMap();
		public string Region { get; init; } = "";
		public long? MatchId { get; init; }
		public DateTime CreatedAt { get; init; }
		public IReadOnlyList<PortalSlot> Slots { get; init; } = [];

		[JsonIgnore]
		public IEnumerable<PortalSlot> Players => Slots.Where(s => s.Member != null);

		[JsonIgnore]
		public int Seats => Slots.Count(s => s.State != "closed");
	}

	public sealed record PortalSlot
	{
		public int Index { get; init; }
		public string State { get; init; } = "";
		public PortalMember? Member { get; init; }
		public bool Ready { get; init; }
	}

	// a report never carries an address
	public sealed record PortalConnectionReport
	{
		public long Id { get; init; }
		public long LobbyId { get; init; }
		public PortalMember Reporter { get; init; } = new PortalMember();
		public PortalMember Target { get; init; } = new PortalMember();
		public int Attempt { get; init; }
		public string Outcome { get; init; } = "";
		public string Transport { get; init; } = "";
		public int IpVersion { get; init; }
		public IReadOnlyList<PortalConnectionEvent> Events { get; init; } = [];
		public DateTime CreatedAt { get; init; }
		public DateTime UpdatedAt { get; init; }

		[JsonIgnore]
		public string Timeline => String.Join(", ", Events.Select(e => $"{e.State}@{e.TMs}ms"));
	}

	public sealed record PortalConnectionEvent
	{
		public string State { get; init; } = "";
		public long TMs { get; init; }
	}

	public sealed record PortalConnectionReports
	{
		public IReadOnlyList<PortalConnectionReport> Reports { get; init; } = [];
	}

	public sealed record PortalTextTerm
	{
		public long Id { get; init; }
		public string Term { get; init; } = "";
		public string Language { get; init; } = "";
		public string Category { get; init; } = "";
		public string Severity { get; init; } = "";
		public string? Level { get; init; }
		public string Match { get; init; } = "";
		public string Source { get; init; } = "";
		public string UpdatedBy { get; init; } = "";
		public DateTime UpdatedAt { get; init; }
	}

	public sealed record PortalTextTerms
	{
		public int Total { get; init; }
		public IReadOnlyList<PortalTextTerm> Terms { get; init; } = [];
	}

	public sealed record PortalTextTermRequest
	{
		public string? Term { get; init; }
		public string? Language { get; init; }
		public string? Category { get; init; }
		public string? Severity { get; init; }
		public string? Level { get; init; }
		public string? Match { get; init; }
	}

	public sealed record PortalTextException
	{
		public long Id { get; init; }
		public string Term { get; init; } = "";
		public string Language { get; init; } = "";
		public string Source { get; init; } = "";
		public string UpdatedBy { get; init; } = "";
		public DateTime UpdatedAt { get; init; }
	}

	public sealed record PortalTextExceptions
	{
		public IReadOnlyList<PortalTextException> Exceptions { get; init; } = [];
	}

	public sealed record PortalTextRule
	{
		public string Name { get; init; } = "";
		public string Expression { get; init; } = "";
		public string Outcome { get; init; } = "";
		public bool Enabled { get; init; } = true;
		public string? Message { get; init; }
	}

	public sealed record PortalTextWorkflowDefinition
	{
		public IReadOnlyList<PortalTextRule> Rules { get; init; } = [];
	}

	public sealed record PortalTextWorkflow
	{
		public string Kind { get; init; } = "";
		public PortalTextWorkflowDefinition Definition { get; init; } = new PortalTextWorkflowDefinition();
		public IReadOnlyList<string> Outcomes { get; init; } = [];
		public IReadOnlyList<string> Fields { get; init; } = [];
		public string UpdatedBy { get; init; } = "";
		public DateTime? UpdatedAt { get; init; }
	}

	public sealed record PortalTextWorkflows
	{
		public IReadOnlyList<PortalTextWorkflow> Workflows { get; init; } = [];
	}

	public sealed record PortalTextTestReading
	{
		public string Name { get; init; } = "";
		public string Text { get; init; } = "";
	}

	public sealed record PortalTextTestHit
	{
		public string Term { get; init; } = "";
		public string Language { get; init; } = "";
		public string Category { get; init; } = "";
		public string Severity { get; init; } = "";
		public string? Level { get; init; }
		public string Reading { get; init; } = "";
		public string Text { get; init; } = "";
	}

	public sealed record PortalTextTest
	{
		public string Kind { get; init; } = "";
		public string Outcome { get; init; } = "";
		public string? Rule { get; init; }
		public IReadOnlyList<PortalTextTestReading> Readings { get; init; } = [];
		public IReadOnlyList<PortalTextTestHit> Hits { get; init; } = [];
	}

	public static class PortalTextChoices
	{
		public static readonly IReadOnlyList<(string Value, string Title)> Kinds = [("display_name", "Player name"), ("lobby_name", "Lobby name"), ("chat", "Chat message")];
		public static readonly IReadOnlyList<(string Value, string Title)> Languages = [("en", "English"), ("ar", "Arabic"), ("de", "German"), ("ru", "Russian")];
		public static readonly IReadOnlyList<string> Categories = ["slur", "sexual", "profanity", "insult", "impersonation", "reserved"];
		public static readonly IReadOnlyList<string> Severities = ["block", "flag"];
		public static readonly IReadOnlyList<string> Levels = ["extreme", "standard", "strict"];
		public static readonly IReadOnlyList<string> Matches = ["word", "anywhere", "literal"];

		public static string KindTitle(string kind) => Kinds.FirstOrDefault(k => k.Value == kind).Title ?? kind;

		// The stored outcome "mask" is the filter list: it changes nothing in the stored or sent chat
		public static string OutcomeTitle(string outcome) => outcome == "mask" ? "filter for players who turn on the chat filter" : outcome;
	}

	public sealed record PortalReasonError
	{
		public string Error { get; init; } = "";
		public PortalReasonParams? Params { get; init; }
	}

	public sealed record PortalReasonParams
	{
		public string? Reason { get; init; }
	}

	public sealed record PortalFlag
	{
		public string Key { get; init; } = "";
		public string Scope { get; init; } = "client";
		public string Label { get; init; } = "";
		public string? Description { get; init; }
		public string Group { get; init; } = "";

		public string Type { get; init; } = "string";
		public string? Unit { get; init; }
		public double? Min { get; init; }
		public double? Max { get; init; }
		public IReadOnlyList<PortalFlagChoice>? Choices { get; init; }

		// false leaves the field out of the game's answer, the game then keeps its built-in default
		public bool Enabled { get; init; } = true;
		public JsonElement Value { get; init; }
		public IReadOnlyList<PortalFlagRule> Rules { get; init; } = [];

		// flagd targeting written by hand, edited through export and import only
		public JsonElement? Custom { get; init; }
	}

	public sealed record PortalFlagChoice
	{
		public JsonElement Value { get; init; }
		public string Label { get; init; } = "";
	}

	public sealed record PortalFlagRule
	{
		public string Kind { get; init; } = "";
		public IReadOnlyList<string>? Values { get; init; }
		public int? Percent { get; init; }
		public JsonElement Value { get; init; }
	}

	public sealed record PortalDraftFlag
	{
		public PortalFlag Flag { get; init; } = new();
		public bool Archived { get; init; }

		public int Version { get; init; }

		// service code reads it, so it cannot be archived
		public bool UsedByService { get; init; }

		public string Status { get; init; } = "";
		public string UpdatedBy { get; init; } = "";
		public DateTime UpdatedAt { get; init; }
	}

	public sealed record PortalPlaylistMap
	{
		public long Id { get; init; }

		// the index the game sends, null while the map is disabled
		public int? Index { get; init; }

		public string Name { get; init; } = "";
		public string Path { get; init; } = "";
		public bool Custom { get; init; }
		public int MaxPlayers { get; init; }
		public bool Enabled { get; init; }
	}

	public sealed record PortalPlaylist
	{
		public int Id { get; init; }
		public string Name { get; init; } = "";
		public bool Enabled { get; init; }
		public int MinPlayers { get; init; }
		public int DesiredPlayers { get; init; }
		public int MinSelectedMaps { get; init; }
		public bool AllowTeams { get; init; }

		// -1 means no teams
		public int TeamSize { get; init; } = -1;

		public bool AllowArmySelection { get; init; }
		public int GracePeriodS { get; init; }
		public int Version { get; init; }
		public string UpdatedBy { get; init; } = "";
		public DateTime UpdatedAt { get; init; }

		// whether the queue offers it right now
		public bool Offered { get; init; }

		public IReadOnlyList<PortalPlaylistMap> Maps { get; init; } = [];
	}

	public sealed record PortalPlaylistRating
	{
		public int BaseRating { get; init; }
		public int KFactor { get; init; }
		public int ExpansionStep { get; init; }
		public int ExpansionStepHigh { get; init; }
		public int ExpansionIntervalS { get; init; }
		public int HighEloThreshold { get; init; }
	}

	public sealed record PortalPlaylistList
	{
		public PortalPlaylistRating Rating { get; init; } = new();
		public IReadOnlyList<PortalPlaylist> Playlists { get; init; } = [];
	}

	public sealed record PortalFlagDraft
	{
		// the live revision, 0 before the first publish
		public int Revision { get; init; }

		public string DraftEtag { get; init; } = "";
		public IReadOnlyList<PortalDraftFlag> Flags { get; init; } = [];
	}

	public sealed record PortalFlagChange
	{
		public string Key { get; init; } = "";
		public string Label { get; init; } = "";

		public string Change { get; init; } = "";
		public IReadOnlyList<string> Fields { get; init; } = [];
		public PortalFlag? Before { get; init; }
		public PortalFlag? After { get; init; }
	}

	public sealed record PortalFlagChanges
	{
		public int? From { get; init; }
		public int? To { get; init; }
		public IReadOnlyList<PortalFlagChange> Changes { get; init; } = [];
	}

	public sealed record PortalFlagRevision
	{
		public int Number { get; init; }
		public DateTime CreatedAt { get; init; }
		public string CreatedBy { get; init; } = "";
		public string? Comment { get; init; }
		public int? RollbackOf { get; init; }
		public bool Live { get; init; }

		public JsonElement? Document { get; init; }
	}

	public sealed record PortalFlagComment
	{
		public string? Comment { get; init; }
	}

	public static class PortalCaseKinds
	{
		public const string Report = "report";
		public const string RuleMatch = "rule_match";

		public static readonly IReadOnlyList<string> All = ["report", "modules", "probes", "devices", "quickmatch_rules", "new_accounts", "rule_match"];

		public static string Title(string kind) => kind switch
		{
			Report => "Player reports",
			RuleMatch => "Automated rules",
			_ => PortalReviewList.Find(kind)?.Title ?? kind
		};

		public static string ShortTitle(string kind) => kind switch
		{
			Report => "Reports",
			RuleMatch => "Rules",
			_ => PortalReviewList.Find(kind)?.ShortTitle ?? kind
		};

		public static string Icon(string kind) => kind switch
		{
			Report => "flag",
			RuleMatch => "workflow",
			"quickmatch_rules" => "swords",
			_ => PortalReviewList.IconOf(kind)
		};

		// anti-cheat and rule signals need anticheat.review, reports any staff permission
		public static bool IsAntiCheat(string kind) => kind != Report;
	}

	public static class PortalReportCategories
	{
		public static string Title(string? category) => category switch
		{
			"chat" => "Chat",
			"cheating" => "Cheating",
			"griefing" => "Griefing",
			"name" => "Offensive name",
			"harassment" => "Harassment",
			_ => "Other"
		};
	}

	public sealed record PortalCaseRow
	{
		public PortalMember Player { get; init; } = new PortalMember();
		public DateTime AccountCreatedAt { get; init; }
		public bool Banned { get; init; }
		public bool Muted { get; init; }
		public int PriorCases { get; init; }
		public IReadOnlyDictionary<string, int> Waiting { get; init; } = new Dictionary<string, int>();
		public DateTime? FirstAt { get; init; }
		public DateTime? LastAt { get; init; }
		public PortalMember? ClaimedBy { get; init; }
		public DateTime? ClaimedAt { get; init; }
		public int Priority { get; init; }

		[JsonIgnore]
		public int WaitingTotal => Waiting.Values.Sum();
	}

	public sealed record PortalCaseList
	{
		public IReadOnlyList<PortalCaseRow> Cases { get; init; } = [];
		public int Total { get; init; }
	}

	public sealed record PortalCaseSummary
	{
		public long Id { get; init; }
		public string Status { get; init; } = "";
		public DateTime OpenedAt { get; init; }
		public PortalMember? ClaimedBy { get; init; }
		public DateTime? ClaimedAt { get; init; }
		public DateTime? ClosedAt { get; init; }
		public PortalMember? ClosedBy { get; init; }
		public string? Outcome { get; init; }
		public string? Note { get; init; }
		public long? SanctionId { get; init; }
	}

	public sealed record PortalCaseMessage
	{
		public long Id { get; init; }
		public PortalMember Author { get; init; } = new PortalMember();
		public string Content { get; init; } = "";
		public DateTime At { get; init; }
		public bool Deleted { get; init; }
		public string Part { get; init; } = "";
	}

	public sealed record PortalCaseSignal
	{
		public string Kind { get; init; } = "";
		public long Id { get; init; }
		public DateTime? At { get; init; }
		public long? MatchId { get; init; }
		public long? LobbyId { get; init; }
		public PortalMember? Reporter { get; init; }
		public string? Category { get; init; }
		public string? MessageId { get; init; }
		public IReadOnlyList<PortalCaseMessage>? Conversation { get; init; }
		public bool? AfterPending { get; init; }
		public string? Reason { get; init; }
		public string? ModuleName { get; init; }
		public string? ModulePath { get; init; }
		public long? ModuleSize { get; init; }
		public int? PlaylistId { get; init; }
		public string? Method { get; init; }
		public IReadOnlyList<int>? Sides { get; init; }
		public long? RuleId { get; init; }
		public string? RuleName { get; init; }
		public int? RuleVersion { get; init; }
		public string? Trigger { get; init; }
		public string? RuleOutcome { get; init; }
		public string? Actions { get; init; }
		public System.Text.Json.JsonElement? Facts { get; init; }

		[JsonIgnore]
		public string SideList => Sides == null ? "" : String.Join(", ", Sides.Select(PortalText.Side));
	}

	public sealed record PortalCaseSanction
	{
		public long Id { get; init; }
		public string Type { get; init; } = "";
		public DateTime? ExpiresAt { get; init; }
		public string? Reason { get; init; }
	}

	public sealed record PortalCaseStanding
	{
		public DateTime AccountCreatedAt { get; init; }
		public DateTime? LastSignInAt { get; init; }
		public IReadOnlyList<PortalCaseSanction> ActiveSanctions { get; init; } = [];
		public int Sanctions { get; init; }
		public int OpenAppeals { get; init; }
		public IReadOnlyList<PortalCaseSummary> PriorCases { get; init; } = [];
	}

	public sealed record PortalCase
	{
		public PortalMember Player { get; init; } = new PortalMember();
		public PortalCaseStanding Standing { get; init; } = new PortalCaseStanding();
		public PortalCaseSummary? Case { get; init; }
		public IReadOnlyList<PortalCaseSignal> Signals { get; init; } = [];
		public IReadOnlyList<PortalMatch> Matches { get; init; } = [];
	}

	public sealed record PortalCaseSignalRef
	{
		public string Kind { get; init; } = "";
		public long Id { get; init; }
	}

	public sealed record PortalCloseCase
	{
		public IReadOnlyList<PortalCaseSignalRef> Signals { get; init; } = [];
		public string Outcome { get; init; } = "no_action";
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public PortalSanctionRequest? Sanction { get; init; }
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public string? Note { get; init; }
	}

	public sealed record PortalCaseClosed
	{
		public long CaseId { get; init; }
		public int Decided { get; init; }
		public long? SanctionId { get; init; }
	}
}
