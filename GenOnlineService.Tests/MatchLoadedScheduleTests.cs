using GenOnlineService.Realtime;
using System.Text.Json;
using Xunit;

namespace GenOnlineService.Tests
{
	public class MatchLoadedScheduleTests
	{
		private const Int64 Deadline = 180000;

		[Fact]
		public void ScheduleWaitsWhileAMemberIsStillLoading()
		{
			HashSet<Int64> loaded = new() { 1 };
			Assert.False(Lobby.ProbeScheduleStarts(new Int64[] { 1, 2 }, loaded.Contains, 1000, Deadline));
		}

		[Fact]
		public void ScheduleStartsWhenEveryMemberLoaded()
		{
			HashSet<Int64> loaded = new() { 1, 2 };
			Assert.True(Lobby.ProbeScheduleStarts(new Int64[] { 1, 2 }, loaded.Contains, 1000, Deadline));
		}

		[Fact]
		public void ScheduleStartsAtTheDeadlineWithoutEveryReport()
		{
			Assert.True(Lobby.ProbeScheduleStarts(new Int64[] { 1, 2 }, _ => false, Deadline, Deadline));
		}

		[Fact]
		public void DeadlineIsThreeMinutes()
		{
			Assert.Equal(180000, Lobby.MatchLoadWaitMs);
		}

		[Fact]
		public void RequestReadsTheMatchIdString()
		{
			MatchLoadedRequest request = JsonSerializer.Deserialize<MatchLoadedRequest>("{\"match_id\":\"12345678901234\"}", RealtimeJson.Create())!;
			Assert.True(RealtimeHub.TryParseMatchID(request.MatchId, out UInt64 id));
			Assert.Equal(12345678901234UL, id);
			Assert.False(RealtimeHub.TryParseMatchID("-1", out _));
			Assert.False(RealtimeHub.TryParseMatchID("0", out _));
		}
	}
}
