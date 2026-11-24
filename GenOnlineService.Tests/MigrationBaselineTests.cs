using Xunit;

namespace GenOnlineService.Tests
{
	public class MigrationBaselineTests
	{
		private const string Baseline = "20261121195430_Baseline";
		private const string Last = MigrationBaseline.LastCollapsedPostgres;

		[Fact]
		public void EmptyDatabaseRunsTheBaseline() => Assert.Equal(MigrationBaseline.Outcome.NotNeeded, MigrationBaseline.Decide([], Baseline, Last));

		[Fact]
		public void AdoptedDatabaseIsLeftAlone() => Assert.Equal(MigrationBaseline.Outcome.NotNeeded, MigrationBaseline.Decide([Baseline, "20261122112253_Appeals"], Baseline, Last));

		[Fact]
		public void DatabaseThatRanTheWholeChainAdoptsTheBaseline() =>
			Assert.Equal(MigrationBaseline.Outcome.Adopted, MigrationBaseline.Decide(["20261113154718_InitialCreate", "20261119193640_MailboxHoldsOnly", Last], Baseline, Last));

		[Fact]
		public void DatabasePartWayThroughTheChainIsRefused() =>
			Assert.Equal(MigrationBaseline.Outcome.Behind, MigrationBaseline.Decide(["20261113154718_InitialCreate", "20261119193640_MailboxHoldsOnly"], Baseline, Last));
	}
}
