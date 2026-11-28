using Xunit;

namespace GenOnlineService.Tests
{
	public class InMatchReconnectTests
	{
		[Theory]
		[InlineData(ELobbyState.GAME_SETUP, true)]
		[InlineData(ELobbyState.COUNTDOWN, true)]
		[InlineData(ELobbyState.INGAME, true)]
		[InlineData(ELobbyState.COMPLETE, false)]
		[InlineData(ELobbyState.UNKNOWN, false)]
		public void ReconnectResignalsLobbiesAndMatches(ELobbyState state, bool resignals)
		{
			Assert.Equal(resignals, Lobby.ResignalsAfterReconnect(state));
		}

		[Fact]
		public void MatchGraceCoversTheGameDisconnectWindow()
		{
			Assert.Equal(60000, UserSessionSettings.ReconnectGracePeriodFor(ELobbyState.INGAME));
		}

		[Theory]
		[InlineData(null)]
		[InlineData(ELobbyState.GAME_SETUP)]
		[InlineData(ELobbyState.COUNTDOWN)]
		[InlineData(ELobbyState.COMPLETE)]
		public void LobbyGraceStaysUnchanged(ELobbyState? state)
		{
			Assert.Equal(30000, UserSessionSettings.ReconnectGracePeriodFor(state));
		}
	}
}
