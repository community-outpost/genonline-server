using System.Text.Json;
using GenOnlineService.Failover;
using GenOnlineService.Realtime;
using Xunit;

namespace GenOnlineService.Tests
{
	public class LobbySettingsEditTests
	{
		// the host in slot 0, two more players in slots 1 and 2 on the given sides
		private static Lobby NewLobby(ELobbyType type = ELobbyType.CustomGame, bool allowObservers = true, int joinability = 0, ushort cameraHeight = 310, int side1 = 0, int side2 = 0) => Lobby.FromLive(new LobbyLive
		{
			Id = 10,
			Owner = 1,
			Name = "lobby",
			State = (int)ELobbyState.GAME_SETUP,
			Type = (int)type,
			AllowObservers = allowObservers,
			Joinability = joinability,
			MaxCameraHeight = cameraHeight,
			Slots = Enumerable.Range(0, Lobby.MaxLobbySize).Select(index => new LobbyMemberLive
			{
				SlotIndex = index,
				SlotState = (int)(index <= 2 ? EPlayerType.SLOT_PLAYER : EPlayerType.SLOT_OPEN),
				UserId = index <= 2 ? index + 1 : -1,
				Side = index == 1 ? side1 : index == 2 ? side2 : 0
			}).ToList()
		});

		[Fact]
		public void AQuickMatchLobbyKeepsThePlaylistsArmiesAndObservers()
		{
			foreach (UpdateLobbyRequest request in new[]
			{
				new UpdateLobbyRequest { Settings = new UpdateLobbySettings { VanillaTeams = true } },
				new UpdateLobbyRequest { Settings = new UpdateLobbySettings { AllowObservers = false } }
			})
			{
				Assert.Equal(ErrorCodes.LobbyNotHost, RealtimeLobbies.RefuseQuickMatchSettings(NewLobby(ELobbyType.QuickMatch), request)?.Code);
				Assert.Null(RealtimeLobbies.RefuseQuickMatchSettings(NewLobby(), request));
			}
			// the camera height stays the host's to change, as before
			Assert.Null(RealtimeLobbies.RefuseQuickMatchSettings(NewLobby(ELobbyType.QuickMatch), new UpdateLobbyRequest { Settings = new UpdateLobbySettings { MaxCameraHeight = 400 } }));
		}

		[Fact]
		public void ClosingTheObserverSeatSendsItsPlayersToRandom()
		{
			Lobby lobby = NewLobby(side1: ClientFeatures.ObserverSide, side2: 3);
			lobby.UpdateAllowObservers(false);

			Assert.False(lobby.AllowObservers);
			Assert.Equal(-1, lobby.GetMemberFromUserID(2)!.Side);
			Assert.Equal(3, lobby.GetMemberFromUserID(3)!.Side);
			Assert.False(RealtimeLobbies.StateOf(lobby).Settings.AllowObservers);
		}

		[Fact]
		public void OpeningTheObserverSeatMovesNobody()
		{
			Lobby lobby = NewLobby(allowObservers: false, side1: 2);
			lobby.UpdateAllowObservers(true);
			lobby.UpdateVanillaTeams(true);

			Assert.True(lobby.AllowObservers);
			Assert.Equal(2, lobby.GetMemberFromUserID(2)!.Side);
			Assert.True(RealtimeLobbies.StateOf(lobby).Settings.VanillaTeams);
			Assert.True(RealtimeLobbies.SummaryOf(lobby, null).Settings.VanillaTeams);
		}

		[Fact]
		public void TheSummaryCarriesTheVisibilityAndCameraHeightOfTheState()
		{
			Lobby lobby = NewLobby(joinability: (int)ELobbyJoinability.FriendsOnly, cameraHeight: 450);
			LobbySummary summary = RealtimeLobbies.SummaryOf(lobby, null);
			Assert.Equal(RealtimeLobbies.StateOf(lobby).Visibility, summary.Visibility);
			Assert.Equal(LobbyVisibility.Friends, summary.Visibility);
			Assert.Equal(450, summary.Settings.MaxCameraHeight);

			lobby.UpdateMaxCameraHeight(600);
			string json = JsonSerializer.Serialize(RealtimeLobbies.SummaryOf(lobby, null), RealtimeJson.Create());
			Assert.Contains("\"visibility\":\"friends\"", json);
			Assert.Contains("\"max_camera_height\":600", json);
		}
	}
}
