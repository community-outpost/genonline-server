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

namespace GenOnlineService.Rest
{
	public sealed record PermissionEntry
	{
		public string Name { get; init; } = "";
		public string Area { get; init; } = "";
		public string Label { get; init; } = "";
		public string Description { get; init; } = "";

		public bool Held { get; init; }
	}

	public sealed record PermissionsResponse
	{
		public IReadOnlyList<PermissionEntry> Permissions { get; init; } = [];
	}

	public sealed record RoleResponse
	{
		public string Name { get; init; } = "";
		public string DisplayName { get; init; } = "";
		public string? Color { get; init; }

		// The name prefix the game shows in brackets before the player name
		public string? Tag { get; init; }
		public string? Description { get; init; }
		public int Position { get; init; }
		public bool BuiltIn { get; init; }

		public bool Locked { get; init; }

		public bool Manageable { get; init; }
		public IReadOnlyList<string> Permissions { get; init; } = [];

		// People who hold it (every source), 0 for everyone, which every player has
		public int MemberCount { get; init; }
	}

	public sealed record RoleListResponse
	{
		public IReadOnlyList<RoleResponse> Roles { get; init; } = [];
	}

	// Create takes every field. Update takes the fields to change: Name cannot change, "" clears Color, Tag and Description, and Position
	// or Above (the role to sit directly above) moves a custom role.
	public sealed record RoleWriteRequest
	{
		public string? Name { get; init; }
		public string? DisplayName { get; init; }
		public string? Color { get; init; }
		public string? Tag { get; init; }
		public string? Description { get; init; }
		public int? Position { get; init; }
		public string? Above { get; init; }
		public IReadOnlyList<string?>? Permissions { get; init; }
	}

	public sealed record RoleMemberResponse
	{
		public long UserId { get; init; }
		public string Name { get; init; } = "";

		// local or upstream:<provider>
		public string Source { get; init; } = "";
		public DateTime GrantedAt { get; init; }
		public string? GrantedBy { get; init; }
	}

	public sealed record RoleMembersResponse
	{
		public int Total { get; init; }
		public IReadOnlyList<RoleMemberResponse> Members { get; init; } = [];
	}

	public static class PermissionCatalog
	{
		public static readonly IReadOnlyList<(string Name, string Area, string Label, string Description)> Entries = new[]
		{
			(Permissions.ModerationKick, "Moderation", "Kick players", "Remove a player from a lobby or the service for now."),
			(Permissions.ModerationMute, "Moderation", "Mute players", "Stop a player from chatting, and lift the mute."),
			(Permissions.ModerationBan, "Moderation", "Ban players", "Ban an account and lift the ban."),
			(Permissions.ModerationRename, "Moderation", "Rename players", "Replace a name that breaks the rules with a placeholder."),
			(Permissions.AppealsReview, "Moderation", "Review appeals", "Read the appeals of players. Deciding one that lifts or shortens a ban or mute also needs the ban or mute permission."),
			(Permissions.AnticheatReview, "Moderation", "Review anti-cheat", "Read the anti-cheat review lists and mark rows reviewed."),
			(Permissions.RulesManage, "Moderation", "Manage automated rules", "Edit, preview and sweep the rules that flag, kick, mute and ban players."),
			(Permissions.TextRulesManage, "Moderation", "Manage text rules", "Change the word lists, exceptions and rules of the name and chat checks."),
			(Permissions.ChatDeleteAny, "Chat", "Delete any message", "Delete a message of any player."),
			(Permissions.ChatMessageAnyone, "Chat", "Message anyone", "Write to a player who blocked or muted direct messages."),
			(Permissions.ChatStaffBadge, "Chat", "Staff badge in chat", "Show the role badge next to the name in chat."),
			(Permissions.ChannelsManage, "Chat", "Manage channels", "Create and change chat channels."),
			(Permissions.BroadcastsManage, "Chat", "Manage broadcasts", "Create and change service broadcasts."),
			(Permissions.AnnouncementsSend, "Chat", "Send announcements", "Send an announcement to every connected player."),
			(Permissions.NamesProtected, "Players", "Protected name", "The name is protected from look-alike names."),
			(Permissions.NamesUnrestricted, "Players", "Any name", "Choose or give any name, also one the reserved word list refuses. The length and character rules still apply."),
			(Permissions.SocialUnblockable, "Players", "Cannot be blocked", "Players cannot block this account."),
			(Permissions.PlayersInspect, "Players", "Inspect players", "Read the sign-ins, devices, addresses and possible duplicate accounts of a player."),
			(Permissions.AccountsSupport, "Players", "Account support", "Reset the password or two-factor sign-in of a player, from the staff pages only."),
			(Permissions.RolesManage, "Access", "Manage roles", "Create and edit roles below your own and give them to players."),
			(Permissions.TokensManage, "Access", "Manage partner tokens", "Create partner accounts and their access tokens."),
			(Permissions.AuditRead, "Server", "Read the audit log", "Read every audited staff action."),
			(Permissions.ServerStats, "Server", "Read server status", "See the live status of the service, lobbies and queues."),
			(Permissions.SettingsManage, "Server", "Manage settings", "Change application settings such as the message of the day."),
			(Permissions.FlagsManage, "Server", "Manage game config", "Edit and publish the config and flags the game reads."),
			(Permissions.PlaylistsManage, "Server", "Manage playlists", "Edit the matchmaking playlists."),
			(Permissions.LaddersManage, "Server", "Manage ladders", "Edit the ladders.")
		};

		public static IReadOnlyList<PermissionEntry> For(IReadOnlySet<string> held) =>
			Entries.Select(e => new PermissionEntry { Name = e.Name, Area = e.Area, Label = e.Label, Description = e.Description, Held = held.Contains(e.Name) }).ToList();
	}
}
