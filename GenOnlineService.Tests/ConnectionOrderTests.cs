using System.Text.Json;
using GenOnlineService.Failover;
using GenOnlineService.Realtime;
using Xunit;

namespace GenOnlineService.Tests
{
	public class ConnectionOrderTests
	{
		private static (string, bool)[] Candidates(PeerStepValues values) => values.Candidates.Select(c => (c.Transport, c.Relay)).ToArray();

		[Fact]
		public void SharedPairRacesGnsDirectAndThePlugin()
		{
			PeerStepValues values = ConnectionOrder.ValuesOf(ConnectionOrder.First, "eos", null);

			Assert.Equal(new[] { ("gns", false), ("eos", false) }, Candidates(values));
			Assert.Equal("gns", values.Transport);
			Assert.False(values.Relay);
			Assert.Equal(10, values.TimeoutS);
		}

		[Fact]
		public void PairWithoutSharedPluginStartsOnGnsDirect()
		{
			PeerStepValues values = ConnectionOrder.ValuesOf(ConnectionOrder.First, null, null);

			Assert.Equal(new[] { ("gns", false) }, Candidates(values));
			Assert.Equal(6, values.TimeoutS);
		}

		[Fact]
		public void RaceOrdersThePluginFirstWhenItWonLastTime()
		{
			PeerStepValues values = ConnectionOrder.ValuesOf(ConnectionOrder.First, "eos", "eos");

			Assert.Equal(new[] { ("eos", false), ("gns", false) }, Candidates(values));
			Assert.Equal("eos", values.Transport);
		}

		[Theory]
		[InlineData(true)]
		[InlineData(false)]
		public void FailedFirstStepEndsOnTurnAlone(bool shared)
		{
			PeerPairState next = ConnectionOrder.OnRequest(ConnectionOrder.First, 1, shared)!.Value;
			PeerStepValues values = ConnectionOrder.ValuesOf(next, shared ? "eos" : null, "eos");

			Assert.Equal(ConnectionOrder.RelayStep, next.Step);
			Assert.Equal(new[] { ("gns", true) }, Candidates(values));
		}

		[Fact]
		public void PlainRequestRestartsTheSameStep()
		{
			PeerPairState next = ConnectionOrder.OnRequest(ConnectionOrder.First, null, true)!.Value;

			Assert.Equal(new PeerPairState(2, ConnectionOrder.RaceStep), next);
		}

		[Fact]
		public void PlainRequestSoonAfterARestartIsIgnored()
		{
			PeerPairs pairs = new();
			pairs.Restart(1, 2, false, true, 10_000);

			Assert.Null(pairs.Request(2, 1, null, true, 11_999));
			Assert.NotNull(pairs.Request(1, 2, null, true, 12_000));
			Assert.Null(pairs.Request(2, 1, null, true, 12_500));
		}

		[Fact]
		public void FailedAttemptIsNeverIgnoredForBeingSoon()
		{
			PeerPairs pairs = new();
			PeerPairState current = pairs.Restart(1, 2, false, true, 10_000);

			Assert.Equal(ConnectionOrder.RelayStep, pairs.Request(1, 2, current.Attempt, true, 10_001)!.Value.Step);
		}

		[Fact]
		public void WinnerIsStoredPerPairAndForgottenWithAMember()
		{
			PeerPairs pairs = new();
			pairs.SetWinner(2, 1, "eos");

			Assert.Equal("eos", pairs.Winner(1, 2));
			pairs.Forget(1);
			Assert.Null(pairs.Winner(1, 2));
		}

		private static Lobby TwoMemberLobby() => Lobby.FromLive(new LobbyLive
		{
			Id = 10,
			Owner = 1,
			State = (int)ELobbyState.GAME_SETUP,
			Type = (int)ELobbyType.CustomGame,
			Slots = Enumerable.Range(0, Lobby.MaxLobbySize).Select(index => new LobbyMemberLive
			{
				SlotIndex = index,
				SlotState = (int)(index < 2 ? EPlayerType.SLOT_PLAYER : EPlayerType.SLOT_OPEN),
				UserId = index < 2 ? index + 1 : -1
			}).ToList()
		});

		[Fact]
		public async Task ReportPeerTransportRecordsTheCallersLinkAsConnected()
		{
			Lobby lobby = TwoMemberLobby();
			await lobby.ReportPeerTransportAsync(1, 2, "gns");
			await lobby.ReportPeerTransportAsync(2, 1, "gns");

			LobbyLive live = await lobby.RunExclusiveAsync(() => Task.FromResult(lobby.CaptureLiveLocked()));
			Assert.Equal(new[] { new long[] { 1, 2 }, new long[] { 2, 1 } }, live.ReportedConnections);
		}

		[Fact]
		public async Task WinnerRoundTripsThroughLiveState()
		{
			Lobby lobby = TwoMemberLobby();
			await lobby.ReportPeerTransportAsync(2, 1, "eos");

			LobbyLive live = await lobby.RunExclusiveAsync(() => Task.FromResult(lobby.CaptureLiveLocked()));
			Lobby restored = Lobby.FromLive(JsonSerializer.Deserialize<LobbyLive>(LiveJson.Data(live), LiveJson.Options)!);
			LobbyLive again = await restored.RunExclusiveAsync(() => Task.FromResult(restored.CaptureLiveLocked()));

			PeerPairLive pair = Assert.Single(again.PeerPairs);
			Assert.Equal((1L, 2L, "eos"), (pair.Low, pair.High, pair.Winner));
		}
	}
}
