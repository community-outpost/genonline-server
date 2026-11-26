using System.Reflection;
using GenOnlineService.Failover;
using GenOnlineService.Realtime;
using Xunit;

namespace GenOnlineService.Tests
{
	public class LobbyHostMigrationTests
	{
		private const long Host = 1;
		private const long Guest = 2;
		private const long Third = 3;

		private sealed class ManualClock : TimeProvider
		{
			public DateTimeOffset Now = DateTimeOffset.UnixEpoch;
			public override DateTimeOffset GetUtcNow() => Now;
		}

		// Records every event a member receives with the receiving user
		public class Recorder : DispatchProxy
		{
			public long UserID;
			public List<(long UserID, object Event)> Log = null!;

			protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
			{
				Log.Add((UserID, args![0]!));
				return Task.CompletedTask;
			}

			public static IRealtimeClient For(long userID, List<(long UserID, object Event)> log)
			{
				IRealtimeClient client = Create<IRealtimeClient, Recorder>();
				Recorder recorder = (Recorder)(object)client;
				recorder.UserID = userID;
				recorder.Log = log;
				return client;
			}
		}

		private readonly ManualClock m_clock = new();
		private readonly List<(long UserID, object Event)> m_events = new();

		// Slot 0 holds the host, 1 the guest, 2 the third player, 3 a brutal AI, 5 is closed and the rest are open
		private Lobby NewLobby(ELobbyState state = ELobbyState.GAME_SETUP, ELobbyType type = ELobbyType.CustomGame, long[]? joinOrder = null)
		{
			EPlayerType[] states = { EPlayerType.SLOT_PLAYER, EPlayerType.SLOT_PLAYER, EPlayerType.SLOT_PLAYER, EPlayerType.SLOT_BRUTAL_AI, EPlayerType.SLOT_OPEN, EPlayerType.SLOT_CLOSED, EPlayerType.SLOT_OPEN, EPlayerType.SLOT_OPEN };
			long[] order = joinOrder ?? new long[] { 1, 2, 3 };
			LobbyLive live = new LobbyLive
			{
				Id = 10,
				Owner = Host,
				State = (int)state,
				Type = (int)type,
				StartPhase = state == ELobbyState.COUNTDOWN ? "countdown" : "none",
				CountdownEndsAtMs = state == ELobbyState.COUNTDOWN ? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 60000 : null,
				Slots = Enumerable.Range(0, Lobby.MaxLobbySize).Select(index => new LobbyMemberLive
				{
					SlotIndex = index,
					SlotState = (int)states[index],
					UserId = index < 3 ? index + 1 : -1,
					Side = 10 + index,
					Color = index,
					Team = index,
					StartPosition = index,
					JoinSequence = index < 3 ? order[index] : 0
				}).ToList()
			};
			Lobby lobby = Lobby.FromLive(live);
			lobby.Clock = m_clock;
			lobby.GameClientOf = member => Recorder.For(member.UserID, m_events);
			return lobby;
		}

		[Fact]
		public async Task HostMigrationPicksTheEarliestJoinerWhateverTheSlot()
		{
			Lobby lobby = NewLobby(joinOrder: new long[] { 1, 5, 4 });
			await lobby.RemoveMemberAsync(Host);
			Assert.Equal(Third, lobby.Owner);
			Assert.Equal(0, lobby.GetMemberFromUserID(Third)!.SlotIndex);
			Assert.Equal(EPlayerType.SLOT_OPEN, lobby.Members[2].SlotState);
			Assert.Equal(1, lobby.GetMemberFromUserID(Guest)!.SlotIndex);
		}

		[Fact]
		public async Task JoinOrderSurvivesTheLiveState()
		{
			Lobby lobby = NewLobby(joinOrder: new long[] { 1, 5, 4 });
			LobbyLive live = await lobby.RunExclusiveAsync(() => Task.FromResult(lobby.CaptureLiveLocked()));
			Lobby restored = Lobby.FromLive(live);
			await restored.RemoveMemberAsync(Host);
			Assert.Equal(Third, restored.Owner);
		}
	}
}
