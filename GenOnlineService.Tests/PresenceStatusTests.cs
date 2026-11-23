using System.Text.Json;
using GenOnlineService.Realtime;
using Xunit;

namespace GenOnlineService.Tests
{
	public class PresenceStatusTests
	{
		private static FriendPresenceEvent Live() => new()
		{
			UserId = 7,
			Online = true,
			Clients = [ClientType.Game, ClientType.Launcher],
			Activity = PresenceActivity.Lobby,
			ChannelId = 3
		};

		[Theory]
		[InlineData("online", PresenceStatus.Online)]
		[InlineData("idle", PresenceStatus.Idle)]
		[InlineData("do_not_disturb", PresenceStatus.DoNotDisturb)]
		[InlineData("invisible", PresenceStatus.Invisible)]
		public void EachStatusParsesAndKeepsItsWireString(string wire, PresenceStatus expected)
		{
			Assert.True(RealtimePresence.TryParseStatus(wire, out PresenceStatus status));
			Assert.Equal(expected, status);
			Assert.Equal($"\"{wire}\"", JsonSerializer.Serialize(status, RealtimeJson.Create()));
		}

		[Theory]
		[InlineData(null)]
		[InlineData("")]
		[InlineData("Online")]
		[InlineData("away")]
		[InlineData("0")]
		public void UnknownStatusIsRefused(string? wire) => Assert.False(RealtimePresence.TryParseStatus(wire, out _));

		[Fact]
		public void UserDataDefaultsToOnlineAndHoldsTheChoice()
		{
			SharedUserData user = new(7, new UserSocialContainer(), "n", 1, new PlayerStats(7, 1000, 0, 1000));
			Assert.Equal(PresenceStatus.Online, user.PresenceStatus);
			user.PresenceStatus = PresenceStatus.Idle;
			Assert.Equal(PresenceStatus.Idle, user.PresenceStatus);
		}

		[Theory]
		[InlineData(PresenceStatus.Online)]
		[InlineData(PresenceStatus.Idle)]
		[InlineData(PresenceStatus.DoNotDisturb)]
		public void FriendsSeeTheStatusWhileOnline(PresenceStatus status)
		{
			FriendPresenceEvent seen = RealtimePresence.ForFriends(Live(), status);
			Assert.True(seen.Online);
			Assert.Equal(status, seen.Status);
			Assert.Equal(PresenceActivity.Lobby, seen.Activity);
			Assert.Equal(3, seen.ChannelId);
		}

		[Fact]
		public void InvisibleLooksExactlyLikeOffline()
		{
			FriendPresenceEvent offline = new() { UserId = 7 };
			FriendPresenceEvent seen = RealtimePresence.ForFriends(Live(), PresenceStatus.Invisible);
			Assert.Equal(JsonSerializer.Serialize(offline, RealtimeJson.Create()), JsonSerializer.Serialize(seen, RealtimeJson.Create()));
			Assert.False(seen.Online);
			Assert.Empty(seen.Clients);
		}

		[Fact]
		public void PresenceCarriesStatusOnTheWire()
		{
			string json = JsonSerializer.Serialize(RealtimePresence.ForFriends(Live(), PresenceStatus.DoNotDisturb), RealtimeJson.Create());
			Assert.Contains("\"status\":\"do_not_disturb\"", json);
		}
	}
}
