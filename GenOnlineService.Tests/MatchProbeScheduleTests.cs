using Xunit;

namespace GenOnlineService.Tests
{
	public class MatchProbeScheduleTests
	{
		[Fact]
		public void FirstProbeWaitsOneToTwoMinutesAndIsSpread()
		{
			var random = new Random(1);
			long[] delays = Enumerable.Range(0, 1000).Select(_ => Lobby.ProbeDelayMs(true, random)).ToArray();
			Assert.All(delays, delay => Assert.InRange(delay, 60000, 120000));
			Assert.True(delays.Distinct().Count() > 500);
		}

		[Fact]
		public void LaterProbesKeepTheFiveToTenMinuteInterval()
		{
			var random = new Random(2);
			long[] delays = Enumerable.Range(0, 1000).Select(_ => Lobby.ProbeDelayMs(false, random)).ToArray();
			Assert.All(delays, delay => Assert.InRange(delay, 5 * 60000, 10 * 60000));
			Assert.True(delays.Distinct().Count() > 500);
		}
	}
}
