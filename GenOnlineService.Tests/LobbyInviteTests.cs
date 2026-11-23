using GenOnlineService.Failover;
using GenOnlineService.Realtime;
using Xunit;

namespace GenOnlineService.Tests
{
	// Invites are checked at the lobby and social seams, the hub method only maps the answers to replies
	public class LobbyInviteTests
	{
		private const long Host = 1;
		private const long Guest = 2;
		private const long Friend = 3;

		private static Lobby NewLobby(bool passworded = true, ELobbyState state = ELobbyState.GAME_SETUP, bool easyAnticheat = false)
		{
			LobbyLive live = new LobbyLive
			{
				Id = 10,
				Owner = Host,
				State = (int)state,
				Type = (int)ELobbyType.CustomGame,
				Passworded = passworded,
				Password = passworded ? "secret" : "",
				Slots = Enumerable.Range(0, Lobby.MaxLobbySize).Select(index => new LobbyMemberLive
				{
					SlotIndex = index,
					SlotState = (int)(index < 2 ? EPlayerType.SLOT_PLAYER : EPlayerType.SLOT_OPEN),
					UserId = index == 0 ? Host : index == 1 ? Guest : -1
				}).ToList()
			};
			return Lobby.FromLive(easyAnticheat ? live with { Build = new LiveBuild { AnticheatId = (int)EKnownAnticheatID.EASY_ANTICHEAT } } : live);
		}

		private static JoinLobbyCommand JoinAs(Lobby lobby, long userID, string? password = null, GameBuild? build = null) =>
			new(lobby.LobbyID, userID, EUserSessionType.GameClient, 0, true, password, build ?? lobby.Build);

		[Fact]
		public async Task InvitedUserSkipsThePassword()
		{
			Lobby lobby = NewLobby();
			Assert.Null(await lobby.InviteAsync(Host, Friend));
			Assert.Equal(LobbyMembershipStatus.Success, lobby.ValidateJoin(JoinAs(lobby, Friend)));
		}

		[Fact]
		public void UninvitedUserStillNeedsThePassword()
		{
			Lobby lobby = NewLobby();
			Assert.Equal(LobbyMembershipStatus.PasswordMismatch, lobby.ValidateJoin(JoinAs(lobby, Friend)));
			Assert.Equal(LobbyMembershipStatus.Success, lobby.ValidateJoin(JoinAs(lobby, Friend, "secret")));
		}

		[Fact]
		public async Task ExpiredInviteNoLongerSkips()
		{
			Lobby lobby = NewLobby();
			await lobby.InviteAsync(Host, Friend);
			lobby.Invites[Friend] = lobby.Invites[Friend] with { Expires = DateTime.UtcNow.AddSeconds(-1) };
			Assert.Equal(LobbyMembershipStatus.PasswordMismatch, lobby.ValidateJoin(JoinAs(lobby, Friend)));
		}

		[Fact]
		public async Task BanRemovesTheInviteAndRefusesTheJoin()
		{
			Lobby lobby = NewLobby();
			lobby.Invites[Guest] = new LobbyInviteEntry("x", DateTime.UtcNow.AddMinutes(5));
			await lobby.KickMemberAsync(new KickLobbyMemberCommand(lobby.LobbyID, Host, Guest, Ban: true));
			Assert.False(lobby.Invites.ContainsKey(Guest));
			Assert.Equal(LobbyMembershipStatus.Banned, lobby.ValidateJoin(JoinAs(lobby, Guest)));
		}

		[Fact]
		public async Task InviteDoesNotSkipTheBuildOrAnticheatChecks()
		{
			Lobby lobby = NewLobby(easyAnticheat: true);
			await lobby.InviteAsync(Host, Friend);
			Assert.Equal(LobbyMembershipStatus.AnticheatMismatch, lobby.ValidateJoin(JoinAs(lobby, Friend, build: lobby.Build with { AnticheatID = EKnownAnticheatID.GO_INTEGRATED_AC })));
			Assert.Equal(LobbyMembershipStatus.BuildMismatch, lobby.ValidateJoin(JoinAs(lobby, Friend, build: lobby.Build with { ExeCRC = lobby.Build.ExeCRC + 1 })));
		}

		[Fact]
		public async Task InviteRefusedOutsideSetupForAMemberTargetAndANonMember()
		{
			Assert.Equal(ErrorCodes.LobbyNotInSetup, await NewLobby(state: ELobbyState.INGAME).InviteAsync(Host, Friend));
			Assert.Equal(ErrorCodes.LobbyAlreadyMember, await NewLobby().InviteAsync(Host, Guest));
			Assert.Equal(ErrorCodes.LobbyNotMember, await NewLobby().InviteAsync(Friend, 4));
		}

		[Fact]
		public async Task RepeatInviteIsRefusedUntilTheResendWindowPasses()
		{
			Lobby lobby = NewLobby();
			Assert.Null(await lobby.InviteAsync(Host, Friend));
			Assert.Equal(ErrorCodes.LobbyInvitePending, await lobby.InviteAsync(Host, Friend));
			lobby.Invites[Friend] = lobby.Invites[Friend] with { Expires = DateTime.UtcNow.AddMinutes(1) };
			Assert.Null(await lobby.InviteAsync(Host, Friend));
			Assert.True(lobby.Invites[Friend].Expires > DateTime.UtcNow.AddMinutes(9));
		}

		[Fact]
		public void InviteeMustBeAFriendWithNoBlock()
		{
			Assert.Null(GroupActions.CheckInvitees([], [Friend], [Friend]));
			Assert.Equal(ErrorCodes.ChatNotFriends, GroupActions.CheckInvitees([], [], [Friend])?.Code);
			Assert.Equal(ErrorCodes.SocialBlocked, GroupActions.CheckInvitees([Friend], [Friend], [Friend])?.Code);
		}

		[Fact]
		public async Task APendingInviteRoundTripsThroughLiveStateAndAnExpiredOneIsDropped()
		{
			Lobby lobby = NewLobby();
			Assert.Null(await lobby.InviteAsync(Host, Friend));
			Assert.Null(await lobby.InviteAsync(Host, 4));
			lobby.Invites[4] = lobby.Invites[4] with { Expires = DateTime.UtcNow.AddSeconds(-1) };

			LobbyLive live = await lobby.RunExclusiveAsync(() => Task.FromResult(lobby.CaptureLiveLocked()));
			Assert.Equal(new long[] { Friend }, live.Invites.Select(invite => invite.UserId));

			LobbyLive parsed = System.Text.Json.JsonSerializer.Deserialize<LobbyLive>(LiveJson.Data(live), LiveJson.Options)!;
			Lobby restored = Lobby.FromLive(parsed);
			Assert.Equal(lobby.Invites[Friend].Id, restored.Invites[Friend].Id);
			Assert.Equal(LobbyMembershipStatus.Success, restored.ValidateJoin(JoinAs(restored, Friend)));
			Assert.Equal(LobbyMembershipStatus.PasswordMismatch, restored.ValidateJoin(JoinAs(restored, 4)));
		}

		[Fact]
		public async Task OldStateWithoutInvitesRestoresNone()
		{
			Lobby lobby = NewLobby();
			string json = LiveJson.Data(await lobby.RunExclusiveAsync(() => Task.FromResult(lobby.CaptureLiveLocked())));
			Assert.Contains("\"invites\":[]", json);
			LobbyLive old = System.Text.Json.JsonSerializer.Deserialize<LobbyLive>(json.Replace("\"invites\":[],", "").Replace(",\"invites\":[]", ""), LiveJson.Options)!;
			Assert.Empty(Lobby.FromLive(old).Invites);
		}
	}
}
