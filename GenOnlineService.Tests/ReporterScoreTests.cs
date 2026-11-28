using GenOnlineService.Rest;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GenOnlineService.Tests;

public class ReporterScoreMathTests
{
	[Theory]
	[InlineData(0, 0, "new")]
	[InlineData(4, 4, "new")]
	[InlineData(5, 5, "reliable")]
	[InlineData(4, 5, "mixed")]
	[InlineData(7, 8, "reliable")]
	[InlineData(6, 8, "mixed")]
	[InlineData(1, 6, "mixed")]
	[InlineData(0, 6, "often_dismissed")]
	[InlineData(0, 5, "often_dismissed")]
	[InlineData(36, 40, "reliable")]
	public void BandsFollowTheScoreAndTheDecidedCount(int upheld, int decided, string band)
	{
		Assert.Equal(band, new Database.ReporterScore(upheld, decided).Band);
	}

	[Fact]
	public void TheScoreSmoothsTowardsAHalfWithTwoPriorUpheldAndTwoPriorDismissed()
	{
		Assert.Equal(0.5, new Database.ReporterScore(0, 0).Score);
		Assert.Equal(38.0 / 44.0, new Database.ReporterScore(36, 40).Score);
	}

	[Theory]
	[InlineData("reliable", "Reliable")]
	[InlineData("mixed", "Mixed")]
	[InlineData("often_dismissed", "Often dismissed")]
	[InlineData("new", "New reporter")]
	public void EveryBandHasAPlainLabel(string band, string label)
	{
		Assert.Equal(label, GenOnlineService.Portal.Client.PortalWords.ReporterBand(band));
	}

	[Fact]
	public void OnlyStaffTypesCarryTheScore()
	{
		string[] carriers = typeof(ReporterScoreView).Assembly.GetTypes()
			.Where(t => t.GetProperties().Any(p => p.PropertyType == typeof(ReporterScoreView)))
			.Select(t => t.Name).Order().ToArray();
		Assert.Equal(["CaseEacWitness", "CaseSignal", "ReporterScoreResponse"], carriers);
	}
}

[Collection(AdminAreaCollection.Name)]
public class ReporterScoreProbeTests
{
	private static async Task<long> AddUser(AppDbContext db, string name, int tag)
	{
		var user = new User { Username = name, UsernameNormalized = NameNormalizer.Normalize(name), NameTag = tag, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
		db.Users.Add(user);
		await db.SaveChangesAsync();
		return user.ID;
	}

	private static async Task<Sanction> AddSanction(AppDbContext db, long user)
	{
		var sanction = new Sanction { UserID = user, Type = SanctionTypes.Mute, Reason = "toxic", CreatedByKind = AuditActorKinds.User, CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddDays(1) };
		db.Sanctions.Add(sanction);
		await db.SaveChangesAsync();
		return sanction;
	}

	private static async Task<ModerationCase> AddCase(AppDbContext db, long user, string outcome, long? sanctionID)
	{
		var decided = new ModerationCase { UserID = user, Status = ModerationCaseStatuses.Closed, OpenedAt = DateTime.UtcNow, ClosedAt = DateTime.UtcNow, Outcome = outcome, SanctionID = sanctionID };
		db.ModerationCases.Add(decided);
		await db.SaveChangesAsync();
		return decided;
	}

	private static async Task AddReport(AppDbContext db, long reporter, long reported, string status, long? caseID, long? matchID)
	{
		db.PlayerReports.Add(new PlayerReport
		{
			ReporterID = reporter, ReportedUserID = reported, Status = status, CaseID = caseID, MatchID = matchID, Category = PlayerReportCategories.Cheating,
			CreatedAt = DateTime.UtcNow, ResolvedAt = status == PlayerReportStatuses.Open ? null : DateTime.UtcNow
		});
		await db.SaveChangesAsync();
	}

	private static async Task<(long Reporter, long Player)> Seed(AppDbContext db) => (await AddUser(db, "Reporter", 2001), await AddUser(db, "Player", 2002));

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task UpheldAndDismissedDecisionsCountAndOpenReportsDoNot(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		await using AppDbContext db = probe.Open();
		(long reporter, long player) = await Seed(db);
		Sanction sanction = await AddSanction(db, player);
		ModerationCase upheld = await AddCase(db, player, ModerationCaseOutcomes.Sanctioned, sanction.ID);
		ModerationCase dismissed = await AddCase(db, player, ModerationCaseOutcomes.NoAction, null);
		await AddReport(db, reporter, player, PlayerReportStatuses.Actioned, upheld.ID, 10);
		await AddReport(db, reporter, player, PlayerReportStatuses.Dismissed, dismissed.ID, 11);
		await AddReport(db, reporter, player, PlayerReportStatuses.Open, null, 12);

		Dictionary<long, Database.ReporterScore> scores = await Database.ReporterScores.For(db, [reporter]);

		Assert.Equal(new Database.ReporterScore(1, 2), scores[reporter]);
	}

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task ReportsAboutOnePlayerInOneMatchCountOncePerDecision(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		await using AppDbContext db = probe.Open();
		(long reporter, long player) = await Seed(db);
		long other = await AddUser(db, "Other", 2003);
		Sanction sanction = await AddSanction(db, player);
		ModerationCase decision = await AddCase(db, player, ModerationCaseOutcomes.Sanctioned, sanction.ID);
		await AddReport(db, reporter, player, PlayerReportStatuses.Actioned, decision.ID, 10);
		await AddReport(db, reporter, player, PlayerReportStatuses.Actioned, decision.ID, 10);
		await AddReport(db, reporter, player, PlayerReportStatuses.Actioned, decision.ID, 11);
		await AddReport(db, reporter, other, PlayerReportStatuses.Dismissed, null, 10);
		await AddReport(db, reporter, other, PlayerReportStatuses.Dismissed, null, null);
		await AddReport(db, reporter, other, PlayerReportStatuses.Dismissed, null, null);

		Dictionary<long, Database.ReporterScore> scores = await Database.ReporterScores.For(db, [reporter]);

		// two matches about the player, then one match and two reports without a match about the other
		Assert.Equal(new Database.ReporterScore(2, 5), scores[reporter]);
	}

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task AWonAppealOrADeletedSanctionTurnsAnUpheldReportIntoADismissedOne(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		await using AppDbContext db = probe.Open();
		(long reporter, long player) = await Seed(db);
		Sanction won = await AddSanction(db, player);
		Sanction lost = await AddSanction(db, player);
		Sanction cut = await AddSanction(db, player);
		db.Appeals.AddRange(
			new Appeal { SanctionID = won.ID, UserID = player, Text = "no", Status = AppealStatuses.Lifted, CreatedAt = DateTime.UtcNow },
			new Appeal { SanctionID = lost.ID, UserID = player, Text = "no", Status = AppealStatuses.Upheld, CreatedAt = DateTime.UtcNow },
			new Appeal { SanctionID = cut.ID, UserID = player, Text = "no", Status = AppealStatuses.Shortened, CreatedAt = DateTime.UtcNow });
		await db.SaveChangesAsync();
		ModerationCase wonCase = await AddCase(db, player, ModerationCaseOutcomes.Sanctioned, won.ID);
		ModerationCase lostCase = await AddCase(db, player, ModerationCaseOutcomes.Sanctioned, lost.ID);
		ModerationCase cutCase = await AddCase(db, player, ModerationCaseOutcomes.Sanctioned, cut.ID);
		ModerationCase deletedCase = await AddCase(db, player, ModerationCaseOutcomes.Sanctioned, null);
		await AddReport(db, reporter, player, PlayerReportStatuses.Actioned, wonCase.ID, 1);
		await AddReport(db, reporter, player, PlayerReportStatuses.Actioned, lostCase.ID, 2);
		await AddReport(db, reporter, player, PlayerReportStatuses.Actioned, cutCase.ID, 3);
		await AddReport(db, reporter, player, PlayerReportStatuses.Actioned, deletedCase.ID, 4);

		Dictionary<long, Database.ReporterScore> scores = await Database.ReporterScores.For(db, [reporter]);

		Assert.Equal(new Database.ReporterScore(2, 4), scores[reporter]);
	}

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task EacWitnessesCountThroughTheCaseOfTheirFindingAndSelfReportsDoNot(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		await using AppDbContext db = probe.Open();
		(long reporter, long player) = await Seed(db);
		Sanction sanction = await AddSanction(db, player);
		ModerationCase decision = await AddCase(db, player, ModerationCaseOutcomes.Sanctioned, sanction.ID);
		var finding = new AnticheatEacFinding { UserId = player, MatchId = 20, Kind = "action_required", CreatedAt = DateTime.UtcNow, ReviewedAt = DateTime.UtcNow, CaseID = decision.ID };
		var own = new AnticheatEacFinding { UserId = reporter, MatchId = 20, Kind = "action_required", SelfReported = true, CreatedAt = DateTime.UtcNow, ReviewedAt = DateTime.UtcNow, CaseID = decision.ID };
		var waiting = new AnticheatEacFinding { UserId = player, MatchId = 21, Kind = "action_required", CreatedAt = DateTime.UtcNow };
		db.AnticheatEacFindings.AddRange(finding, own, waiting);
		await db.SaveChangesAsync();
		foreach (AnticheatEacFinding f in new[] { finding, own, waiting })
		{
			db.AnticheatEacWitnesses.Add(new AnticheatEacWitness { FindingId = f.ReportId, ReporterId = reporter, Details = "", CreatedAt = DateTime.UtcNow });
		}
		await db.SaveChangesAsync();
		// a player report of the same match merges with the witness report
		await AddReport(db, reporter, player, PlayerReportStatuses.Actioned, decision.ID, 20);

		Dictionary<long, Database.ReporterScore> scores = await Database.ReporterScores.For(db, [reporter]);

		Assert.Equal(new Database.ReporterScore(1, 1), scores[reporter]);
	}

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task AReporterWithNothingDecidedGetsAnEmptyScore(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		await using AppDbContext db = probe.Open();
		(long reporter, _) = await Seed(db);

		Dictionary<long, Database.ReporterScore> scores = await Database.ReporterScores.For(db, [reporter]);

		Assert.Equal(new Database.ReporterScore(0, 0), scores.GetValueOrDefault(reporter));
	}
}
