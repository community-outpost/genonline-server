using Xunit;

namespace GenOnlineService.Tests
{
	public class MatchOutcomeChangeTests
	{
		private static readonly MatchPlayerDetails Reported = new MatchPlayerDetails { desynced = false, duration = 600 };

		[Fact]
		public void FirstReportChangesTheOutcome()
		{
			Assert.True(Database.MatchHistory.OutcomeChanged(new MatchPlayerDetails(), null, true, false, 600));
		}

		[Fact]
		public void FirstReportCountsAfterTheServerDecidedTheWinner()
		{
			Assert.True(Database.MatchHistory.OutcomeChanged(new MatchPlayerDetails(), MatchResults.Win, true, false, 600));
		}

		[Fact]
		public void ResendWithTheSameOutcomeChangesNothing()
		{
			Assert.False(Database.MatchHistory.OutcomeChanged(Reported, MatchResults.Win, true, false, 600));
		}

		[Fact]
		public void ChangedOutcomeChangesTheOutcome()
		{
			Assert.True(Database.MatchHistory.OutcomeChanged(Reported, MatchResults.Win, false, false, 600));
			Assert.True(Database.MatchHistory.OutcomeChanged(Reported, MatchResults.Win, true, true, 600));
			Assert.True(Database.MatchHistory.OutcomeChanged(Reported, MatchResults.Win, true, false, 601));
		}
	}
}
