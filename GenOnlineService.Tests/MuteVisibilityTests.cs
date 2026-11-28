using System.Text.Json;
using GenOnlineService.Identity;
using GenOnlineService.Realtime;
using GenOnlineService.Rest;
using Xunit;

namespace GenOnlineService.Tests;

public class MuteVisibilityTests
{
	private static readonly DateTime s_until = new(2026, 12, 1, 8, 30, 0, DateTimeKind.Utc);

	private static string Json(object value) => JsonSerializer.Serialize(value, RealtimeJson.Create());

	[Fact]
	public void MeShowsTheMuteEndWhileMuted()
	{
		Assert.Contains("\"muted_until\":\"2026-12-01T08:30:00Z\"", Json(new MeResponse { UserId = 1, MutedUntil = s_until }));
	}

	[Fact]
	public void MeOmitsMutedUntilWhenNotMuted()
	{
		Assert.DoesNotContain("muted_until", Json(new MeResponse { UserId = 1 }));
	}

	[Fact]
	public void LiftedMuteIsAnUnmuteNotice()
	{
		Assert.Equal("{\"action\":\"unmute\"}", Json(new ModerationNoticeEvent { Action = ModerationAction.Unmute }));
	}

	[Fact]
	public void MuteNoticeKeepsItsShape()
	{
		Assert.Equal("{\"action\":\"mute\",\"expires_at\":\"2026-12-01T08:30:00Z\"}", Json(new ModerationNoticeEvent { Action = ModerationAction.Mute, ExpiresAt = s_until }));
	}

	[Fact]
	public void SlotShowsMutedUntilOnlyWhenMuted()
	{
		Assert.DoesNotContain("muted_until", Json(new Slot { Index = 1 }));
		Assert.Contains("\"muted_until\":\"2026-12-01T08:30:00Z\"", Json(new Slot { Index = 1, MutedUntil = s_until }));
	}

	[Fact]
	public void MemberMuteEventCarriesTheEndOrNone()
	{
		Assert.Equal("{\"lobby_id\":4,\"user_id\":9,\"muted_until\":\"2026-12-01T08:30:00Z\"}", Json(new LobbyMemberMuteEvent { LobbyId = 4, UserId = 9, MutedUntil = s_until }));
		Assert.Equal("{\"lobby_id\":4,\"user_id\":9}", Json(new LobbyMemberMuteEvent { LobbyId = 4, UserId = 9 }));
	}

	[Fact]
	public void TheLongestActiveMuteOfEachPlayerCounts()
	{
		Dictionary<long, DateTime> mutes = AccessControl.MutesOf(
		[
			new Sanction { UserID = 1, ExpiresAt = s_until },
			new Sanction { UserID = 1, ExpiresAt = s_until.AddHours(2) },
			new Sanction { UserID = 2, ExpiresAt = s_until }
		]);
		Assert.Equal(s_until.AddHours(2), mutes[1]);
		Assert.Equal(s_until, mutes[2]);
		Assert.Equal(DateTimeKind.Utc, mutes[2].Kind);
	}

	[Fact]
	public void AMuteWithoutAnEndReadsAsTheLatestMoment()
	{
		Assert.Equal(DateTime.MaxValue, AccessControl.MutesOf([new Sanction { UserID = 3 }])[3]);
		Assert.Equal(DateTime.MaxValue, AccessControl.MutedUntilOf(new Sanction()));
	}
}
