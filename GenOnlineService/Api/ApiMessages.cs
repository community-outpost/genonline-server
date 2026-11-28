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

using System.Text.Json.Serialization;

namespace GenOnlineService.Api
{
	using GenOnlineService.Realtime;

	[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
	public sealed class ApiFixtureAttribute : Attribute
	{
		public ApiFixtureAttribute(string name)
		{
			Name = name;
		}

		public string Name { get; }
	}

	public enum SanctionType { Kick, Mute, Ban }

	[ApiFixture("error.response")]
	public sealed record ApiError
	{
		public string Error { get; init; } = "";
		public object Params { get; init; } = Empty.Instance;
	}

	[ApiFixture("post-me-friend_requests.request")]
	public sealed record FriendRequestRequest
	{
		public long UserId { get; init; }
	}

	[ApiFixture("get-me-friends.response")]
	public sealed record FriendsResponse
	{
		public IReadOnlyList<FriendMember> Friends { get; init; } = [];
		public IReadOnlyList<Member> Incoming { get; init; } = [];
		public IReadOnlyList<Member> Outgoing { get; init; } = [];
	}

	[ApiFixture("get-me-blocks.response")]
	public sealed record BlocksResponse
	{
		public IReadOnlyList<Member> Blocked { get; init; } = [];
	}

	[ApiFixture("post-groups.request")]
	public sealed record CreateGroupRequest
	{
		public IReadOnlyList<long> UserIds { get; init; } = [];
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public string? Name { get; init; }
	}

	[ApiFixture("post-groups.response")]
	public sealed record CreateGroupResponse
	{
		public long GroupId { get; init; }
	}

	[ApiFixture("post-groups-members.request")]
	public sealed record AddGroupMembersRequest
	{
		public IReadOnlyList<long> UserIds { get; init; } = [];
	}

	// The name is required, null clears it
	[ApiFixture("patch-groups.request")]
	public sealed record RenameGroupRequest
	{
		public Optional<string?> Name { get; init; }
	}

	[ApiFixture("post-players-sanctions.request")]
	public sealed record CreateSanctionRequest
	{
		public SanctionType? Type { get; init; }
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public long? DurationS { get; init; }
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public string? Reason { get; init; }
	}

	[ApiFixture("post-players-sanctions.response")]
	public sealed record CreateSanctionResponse
	{
		public long SanctionId { get; init; }
	}

	public sealed record HoldEvidenceRequest
	{
		public long? MatchId { get; init; }
	}

	// Screenshots past their 90 days are already gone
	public sealed record HoldEvidenceResponse
	{
		public IReadOnlyList<string> Held { get; init; } = [];
		public int Missing { get; init; }
	}

	// created_by is null when a tool or a sign-in provider created the sanction
	public sealed record SanctionInfo
	{
		public long Id { get; init; }
		public SanctionType Type { get; init; }
		public string? Reason { get; init; }
		public DateTime CreatedAt { get; init; }
		public DateTime? ExpiresAt { get; init; }
		public DateTime? LiftedAt { get; init; }
		public Member? CreatedBy { get; init; }
	}

	[ApiFixture("get-players-sanctions.response")]
	public sealed record SanctionsResponse
	{
		public IReadOnlyList<SanctionInfo> Sanctions { get; init; } = [];
	}

	[ApiFixture("delete-sanctions.request")]
	[ApiFixture("delete-messages.request")]
	public sealed record ReasonRequest
	{
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public string? Reason { get; init; }
	}

	// Exactly one of message_id and user_id
	[ApiFixture("post-reports.request")]
	public sealed record CreateReportRequest
	{
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public string? MessageId { get; init; }
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public long? UserId { get; init; }
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public string? Reason { get; init; }

		// A message report is chat, a player report defaults to other (PlayerReportCategories)
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public string? Category { get; init; }

		// A player report may name a match both players played or a lobby both were in; a message report takes both from the message
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public long? MatchId { get; init; }
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public long? LobbyId { get; init; }
	}

	[ApiFixture("post-reports.response")]
	public sealed record CreateReportResponse
	{
		public long ReportId { get; init; }
	}

	[ApiFixture("post-players-account_support.request")]
	public sealed record AccountSupportRequest
	{
		public string? Reason { get; init; }
	}

	// Shown only in this answer
	[ApiFixture("post-players-password_reset.response")]
	public sealed record PasswordResetResponse
	{
		public string Password { get; init; } = String.Empty;
	}

	public sealed record ResetNameRequest
	{
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public string? Reason { get; init; }
	}

	[ApiFixture("patch-players.request")]
	public sealed record RenamePlayerRequest
	{
		public string? Name { get; init; }
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public string? Reason { get; init; }
	}

	[ApiFixture("patch-me.request")]
	public sealed record UpdateMeRequest
	{
		public string? Name { get; init; }
	}

	[ApiFixture("put-matches-results-me.request")]
	public sealed record MatchResultRequest
	{
		public bool Won { get; init; }
		public bool Desynced { get; init; }
		public int Side { get; init; }
		public int DurationS { get; init; }
		public MatchStats? Stats { get; init; }

		// Absent means true, so clients that never send it still get a stats URL
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public bool? StatsFile { get; init; }
	}

	[ApiFixture("put-matches-results-me.response")]
	public sealed record MatchResultResponse
	{
		public string ScreenshotUrl { get; init; } = "";
		public string ReplayUrl { get; init; } = "";

		// Absent when stats uploads are off
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public string? StatsUrl { get; init; }

		// Form fields to send before the file when the URL is a form target, not a PUT URL
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public IReadOnlyDictionary<string, string>? ScreenshotFields { get; init; }

		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public IReadOnlyDictionary<string, string>? ReplayFields { get; init; }

		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public IReadOnlyDictionary<string, string>? StatsFields { get; init; }

		// When the upload URLs stop working, so the game can end an upload that would be refused anyway
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public DateTime? ExpiresAt { get; init; }
	}

	// scope is everyone, allies or self, and recipient_user_ids are the other players of the match the line was shown to
	[ApiFixture("post-matches-chat.request")]
	public sealed record SendMatchChatRequest
	{
		public string? Text { get; init; }
		public string? Scope { get; init; }
		public IReadOnlyList<long>? RecipientUserIds { get; init; }
	}

	[ApiFixture("post-matches-chat.response")]
	public sealed record SendMatchChatResponse
	{
		public string MessageId { get; init; } = "";
	}

	[ApiFixture("get-matchmaking-playlists.response")]
	public sealed record PlaylistsResponse
	{
		public IReadOnlyList<Playlist> Playlists { get; init; } = [];
	}

	// t_ms counts from the start of the attempt
	public sealed record ConnectionReportEvent
	{
		public string? State { get; init; }
		public long? TMs { get; init; }
	}

	[ApiFixture("post-lobbies-connection_reports.request")]
	public sealed record ConnectionReportRequest
	{
		public long? TargetUserId { get; init; }
		public string? Outcome { get; init; }
		public string? Transport { get; init; }
		public int? IpVersion { get; init; }
		public int? Attempt { get; init; }
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public IReadOnlyList<ConnectionReportEvent>? Events { get; init; }
	}
}
