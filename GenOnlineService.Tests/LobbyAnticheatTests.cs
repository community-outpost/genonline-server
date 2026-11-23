using GenOnlineService.Realtime;
using System.Text.Json;
using Xunit;

namespace GenOnlineService.Tests
{
	public class LobbyAnticheatTests
	{
		private const EKnownAnticheatID None = EKnownAnticheatID.NONE;
		private const EKnownAnticheatID Go = EKnownAnticheatID.GO_INTEGRATED_AC;
		private const EKnownAnticheatID Eac = EKnownAnticheatID.EASY_ANTICHEAT;

		[Theory]
		[InlineData(None, false, ErrorCodes.LobbyAnticheatRequired)]
		[InlineData(None, true, ErrorCodes.LobbyAnticheatRequired)]
		[InlineData(Go, true, ErrorCodes.LobbyEasyAnticheatUnavailable)]
		[InlineData(Go, false, null)]
		[InlineData(Eac, false, null)]
		[InlineData(Eac, true, null)]
		public void CreateFollowsTheHostAnticheat(EKnownAnticheatID host, bool require, string? expected)
		{
			Assert.Equal(expected, LobbyAnticheat.CreateRefusal(host, require));
		}

		[Theory]
		[InlineData(Eac, Eac, true)]
		[InlineData(Eac, Go, false)]
		[InlineData(Eac, None, false)]
		[InlineData(Go, Go, true)]
		[InlineData(Go, Eac, true)]
		[InlineData(Go, None, false)]
		public void JoinFollowsTheLobbyRequirement(EKnownAnticheatID lobby, EKnownAnticheatID joiner, bool admitted)
		{
			Assert.Equal(admitted, LobbyAnticheat.Admits(lobby, joiner));
		}

		[Theory]
		[InlineData(false, "go_integrated")]
		[InlineData(true, "easy_anticheat")]
		public void WireStringFollowsTheRequirement(bool require, string expected)
		{
			JsonSerializerOptions options = RealtimeJson.Create();
			string json = JsonSerializer.Serialize(new LobbyInfo { Anticheat = RealtimeLobbies.AnticheatOf(LobbyAnticheat.RequirementOf(require)) }, options);
			Assert.Contains("\"anticheat\":\"" + expected + "\"", json);
		}

		[Fact]
		public void CreateRequestReadsRequireAnticheat()
		{
			JsonSerializerOptions options = RealtimeJson.Create();
			Assert.False(JsonSerializer.Deserialize<CreateLobbyRequest>("{}", options)!.RequireAnticheat);
			Assert.True(JsonSerializer.Deserialize<CreateLobbyRequest>("{\"require_anticheat\":true}", options)!.RequireAnticheat);
		}
	}
}
