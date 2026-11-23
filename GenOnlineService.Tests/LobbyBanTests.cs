using System.Text.Json;
using GenOnlineService.Failover;
using GenOnlineService.Realtime;
using Xunit;

namespace GenOnlineService.Tests
{
	// The hub method only maps these statuses to replies, so the lobby level seam is tested
	public class LobbyBanTests
	{
		private const long Host = 1;
		private const long Guest = 2;

		private static Lobby NewLobby(LobbyLive? template = null)
		{
			LobbyLive live = (template ?? new LobbyLive()) with
			{
				Id = 10,
				Owner = Host,
				State = (int)ELobbyState.GAME_SETUP,
				Type = (int)ELobbyType.CustomGame,
				Slots = Enumerable.Range(0, Lobby.MaxLobbySize).Select(index => new LobbyMemberLive
				{
					SlotIndex = index,
					SlotState = (int)(index < 2 ? EPlayerType.SLOT_PLAYER : EPlayerType.SLOT_OPEN),
					UserId = index == 0 ? Host : index == 1 ? Guest : -1
				}).ToList()
			};
			return Lobby.FromLive(live);
		}

		private static JoinLobbyCommand JoinAs(Lobby lobby, long userID) =>
			new(lobby.LobbyID, userID, EUserSessionType.GameClient, 0, true, null, lobby.Build);

		[Fact]
		public async Task BanRemovesTheMemberAndRefusesTheRejoin()
		{
			Lobby lobby = NewLobby();
			Assert.Equal(LobbyMembershipStatus.Success, await lobby.KickMemberAsync(new KickLobbyMemberCommand(lobby.LobbyID, Host, Guest, Ban: true)));
			Assert.Null(lobby.GetMemberFromUserID(Guest));
			Assert.Equal(LobbyMembershipStatus.Banned, lobby.ValidateJoin(JoinAs(lobby, Guest)));
		}

		[Fact]
		public async Task KickAllowsTheRejoin()
		{
			Lobby lobby = NewLobby();
			Assert.Equal(LobbyMembershipStatus.Success, await lobby.KickMemberAsync(new KickLobbyMemberCommand(lobby.LobbyID, Host, Guest)));
			Assert.Equal(LobbyMembershipStatus.Success, lobby.ValidateJoin(JoinAs(lobby, Guest)));
		}

		[Fact]
		public async Task NonHostCannotBan()
		{
			Lobby lobby = NewLobby();
			Assert.Equal(LobbyMembershipStatus.ActorNotHost, await lobby.KickMemberAsync(new KickLobbyMemberCommand(lobby.LobbyID, Guest, Host, Ban: true)));
			Assert.NotNull(lobby.GetMemberFromUserID(Host));
			Assert.Empty(lobby.BannedUserIDs);
		}

		[Fact]
		public async Task BanSetRoundTripsThroughLiveState()
		{
			Lobby lobby = NewLobby();
			await lobby.KickMemberAsync(new KickLobbyMemberCommand(lobby.LobbyID, Host, Guest, Ban: true));
			LobbyLive live = await lobby.RunExclusiveAsync(() => Task.FromResult(lobby.CaptureLiveLocked()));
			Assert.Equal(new long[] { Guest }, live.BannedUserIds);

			LobbyLive parsed = JsonSerializer.Deserialize<LobbyLive>(LiveJson.Data(live), LiveJson.Options)!;
			Assert.Equal(LobbyMembershipStatus.Banned, Lobby.FromLive(parsed).ValidateJoin(JoinAs(lobby, Guest)));
		}

		[Fact]
		public void OldStateWithoutTheFieldRestoresEmpty()
		{
			string json = LiveJson.Data(new LobbyLive());
			Assert.Contains("banned_user_ids", json);
			LobbyLive old = JsonSerializer.Deserialize<LobbyLive>(json.Replace("\"banned_user_ids\":[],", ""), LiveJson.Options)!;
			Assert.Empty(NewLobby(old).BannedUserIDs);
		}
	}
}
