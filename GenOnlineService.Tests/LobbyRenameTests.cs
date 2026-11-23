using System.Text.Json;
using GenOnlineService.Failover;
using GenOnlineService.Realtime;
using Xunit;

namespace GenOnlineService.Tests
{
	public class LobbyRenameTests
	{
		private static Lobby NewLobby(ELobbyType type = ELobbyType.CustomGame) => Lobby.FromLive(new LobbyLive
		{
			Id = 10,
			Owner = 1,
			Name = "old name",
			State = (int)ELobbyState.GAME_SETUP,
			Type = (int)type,
			Slots = Enumerable.Range(0, Lobby.MaxLobbySize).Select(index => new LobbyMemberLive
			{
				SlotIndex = index,
				SlotState = (int)(index == 0 ? EPlayerType.SLOT_PLAYER : EPlayerType.SLOT_OPEN),
				UserId = index == 0 ? 1 : -1
			}).ToList()
		});

		[Theory]
		[InlineData("new name", null)]
		[InlineData("", null)]
		[InlineData("bad\0name", ErrorCodes.InvalidArgument)]
		public void NameCheckIsTheCreateCheck(string name, string? code)
		{
			Assert.Equal(code, RealtimeHub.LobbyNameFailure(name)?.Code);
		}

		[Fact]
		public void PlayerTextIsCappedAt140Characters()
		{
			Assert.Equal(140, RealtimeHub.LobbyNameMaxLength);
			Assert.Equal(140, RealtimeHub.ChatMaxLength);
			Assert.Equal(140, RealtimeHub.BuildLimits().ChatMaxLength);
		}

		[Fact]
		public void NameLongerThanTheLimitIsInvalid()
		{
			Assert.Null(RealtimeHub.LobbyNameFailure(new string('a', RealtimeHub.LobbyNameMaxLength)));
			Failure? failure = RealtimeHub.LobbyNameFailure(new string('a', RealtimeHub.LobbyNameMaxLength + 1));
			Assert.Equal(ErrorCodes.InvalidArgument, failure?.Code);
			Assert.Contains("name", JsonSerializer.Serialize(failure?.Params));
		}

		[Fact]
		public void RenameShowsInTheStateAndTheSummary()
		{
			Lobby lobby = NewLobby();
			lobby.UpdateName("new name");
			Assert.Equal("new name", RealtimeLobbies.StateOf(lobby).Name);
			Assert.Equal("new name", RealtimeLobbies.SummaryOf(lobby, null).Name);
		}

		[Fact]
		public async Task RenameRoundTripsThroughLiveState()
		{
			Lobby lobby = NewLobby();
			lobby.UpdateName("new name");
			LobbyLive live = await lobby.RunExclusiveAsync(() => Task.FromResult(lobby.CaptureLiveLocked()));
			LobbyLive parsed = JsonSerializer.Deserialize<LobbyLive>(LiveJson.Data(live), LiveJson.Options)!;
			Assert.Equal("new name", Lobby.FromLive(parsed).Name);
		}

		[Fact]
		public void QuickMatchLobbyCannotBeRenamed()
		{
			UpdateLobbyRequest rename = new UpdateLobbyRequest { Name = "new name" };
			Assert.Equal(ErrorCodes.LobbyNotHost, RealtimeLobbies.RefuseQuickMatchSettings(NewLobby(ELobbyType.QuickMatch), rename)?.Code);
			Assert.Null(RealtimeLobbies.RefuseQuickMatchSettings(NewLobby(), rename));
		}
	}
}
