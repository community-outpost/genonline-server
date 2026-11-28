using GenOnlineService.Api;
using GenOnlineService.Realtime;
using System.Text.Json;
using Xunit;

namespace GenOnlineService.Tests
{
	public class MatchResultAnswerTests
	{
		private static MatchResultRequest Read(string json) => JsonSerializer.Deserialize<MatchResultRequest>(json, ApiResults.Json)!;

		[Fact]
		public void MissingStatsFileStillGetsAStatsURL()
		{
			Assert.True(MatchActions.WantsStatsURL(true, Read("{}")));
		}

		[Fact]
		public void StatsFileTrueGetsAStatsURL()
		{
			Assert.True(MatchActions.WantsStatsURL(true, Read("{\"stats_file\":true}")));
		}

		[Fact]
		public void StatsFileFalseGetsNoStatsURL()
		{
			Assert.False(MatchActions.WantsStatsURL(true, Read("{\"stats_file\":false}")));
		}

		[Fact]
		public void DisabledStatsUploadsGetNoStatsURL()
		{
			Assert.False(MatchActions.WantsStatsURL(false, Read("{\"stats_file\":true}")));
		}

		private static readonly DateTime Now = new DateTime(2026, 11, 28, 12, 0, 0, DateTimeKind.Utc);

		[Fact]
		public void KeptAnswerIsReusedWhileItsURLsLastLonger()
		{
			Assert.True(MatchActions.KeptAnswerUsable(new MatchResultResponse { ExpiresAt = Now.AddMinutes(10) }, Now - TimeSpan.FromMinutes(1), Now));
		}

		[Fact]
		public void KeptAnswerCloseToExpiryGivesFreshURLs()
		{
			Assert.False(MatchActions.KeptAnswerUsable(new MatchResultResponse { ExpiresAt = Now.AddMinutes(2) }, Now - TimeSpan.FromMinutes(1), Now));
			Assert.False(MatchActions.KeptAnswerUsable(new MatchResultResponse { ExpiresAt = Now.AddSeconds(90) }, Now - TimeSpan.FromMinutes(1), Now));
		}

		[Fact]
		public void KeptAnswerPastItsLifetimeGivesFreshURLs()
		{
			Assert.False(MatchActions.KeptAnswerUsable(new MatchResultResponse { ExpiresAt = Now.AddHours(5) }, Now - TimeSpan.FromHours(1), Now));
		}
	}
}
