using GenOnlineService.Failover;
using GenOnlineService.Realtime;
using Xunit;

namespace GenOnlineService.Tests
{
	public class LobbyEditRulesTests
	{
		private static Lobby NewLobby(ELobbyType type = ELobbyType.CustomGame, bool trackStats = false) => Lobby.FromLive(new LobbyLive
		{
			Id = 10,
			Owner = 1,
			Name = "lobby",
			State = (int)ELobbyState.GAME_SETUP,
			Type = (int)type,
			TrackStats = trackStats,
			Slots = Enumerable.Range(0, Lobby.MaxLobbySize).Select(index => new LobbyMemberLive
			{
				SlotIndex = index,
				SlotState = (int)(index == 0 ? EPlayerType.SLOT_PLAYER : EPlayerType.SLOT_OPEN),
				UserId = index == 0 ? 1 : -1
			}).ToList()
		});

		[Fact]
		public void LobbyRemembersWhetherItTracksStats()
		{
			Assert.False(NewLobby().IsTrackingStats);
			Assert.True(NewLobby(trackStats: true).IsTrackingStats);
		}

		[Fact]
		public void QuickMatchRefusesPasswordAndVisibilityChanges()
		{
			Assert.Equal(ErrorCodes.LobbyNotHost, RealtimeLobbies.RefuseQuickMatchPassword(NewLobby(ELobbyType.QuickMatch))?.Code);
			Assert.Null(RealtimeLobbies.RefuseQuickMatchPassword(NewLobby()));

			UpdateLobbyRequest visibility = new() { Visibility = LobbyVisibility.Friends };
			Assert.Equal(ErrorCodes.LobbyNotHost, RealtimeLobbies.RefuseQuickMatchSettings(NewLobby(ELobbyType.QuickMatch), visibility)?.Code);
			Assert.Null(RealtimeLobbies.RefuseQuickMatchSettings(NewLobby(), visibility));
		}
	}
}
