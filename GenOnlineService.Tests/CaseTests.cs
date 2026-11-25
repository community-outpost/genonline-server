using System.Text.Json;
using GenOnlineService.Realtime;
using GenOnlineService.Rest;
using GenOnlineService.Rules;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GenOnlineService.Tests;

[Collection(AdminAreaCollection.Name)]
public class CaseTests
{
	private sealed record Staff(AdminBrowser Moderator, AdminBrowser Admin, User Player, User Reporter, User OtherReporter);

	private static async Task<Staff> SetUpAsync(ProbeDatabase probe)
	{
		var moderator = new AdminBrowser(probe);
		await moderator.SignUp("Warden", "warden@example.org");
		await AdminArea.Grant(probe, "warden@example.org", StaffRoles.Moderator);
		var admin = new AdminBrowser(probe);
		await admin.SignUp("Chief", "chief@example.org");
		await AdminArea.Grant(probe, "chief@example.org", StaffRoles.Admin);
		foreach ((string name, string email) in new[] { ("Suspect", "suspect@example.org"), ("Witness", "witness@example.org"), ("Bystander", "bystander@example.org") })
		{
			await new AdminBrowser(probe).SignUp(name, email);
		}
		await using (AppDbContext db = probe.Open())
		{
			await StaffRoles.Reload(db);
		}
		return new Staff(moderator, admin,
			(await PasswordPagesTests.UserByEmail(probe, "suspect@example.org"))!,
			(await PasswordPagesTests.UserByEmail(probe, "witness@example.org"))!,
			(await PasswordPagesTests.UserByEmail(probe, "bystander@example.org"))!);
	}

	private static async Task SeedAsync(ProbeDatabase probe, Staff staff)
	{
		DateTime now = DateTime.UtcNow;
		await using AppDbContext db = probe.Open();
		db.PlayerReports.Add(new PlayerReport
		{
			ReporterID = staff.Reporter.ID, ReportedUserID = staff.Player.ID, Category = PlayerReportCategories.Cheating, Reason = "map hack",
			Evidence = "{\"messages\":[]}", Status = PlayerReportStatuses.Open, CreatedAt = now.AddMinutes(-3)
		});
		db.PlayerReports.Add(new PlayerReport
		{
			ReporterID = staff.OtherReporter.ID, ReportedUserID = staff.Player.ID, Category = PlayerReportCategories.Other,
			Evidence = "{\"messages\":[]}", Status = PlayerReportStatuses.Open, CreatedAt = now.AddMinutes(-2)
		});
		db.AnticheatModuleReports.Add(new AnticheatModuleReport { UserId = staff.Player.ID, MatchId = 0, ModuleName = "inject.dll", ModulePath = "C:\\tmp\\", ModuleSize = 4096, CreatedAt = now.AddMinutes(-1) });
		db.AutomatedRuleMatches.Add(new AutomatedRuleMatch
		{
			RuleID = 1, RuleVersion = 1, UserID = staff.Player.ID, Trigger = RuleTriggers.AnticheatReport, MatchedAt = now, Mode = RuleModes.Enforce,
			Outcome = RuleOutcomes.Flagged, Facts = "{\"reports\":[2]}", ReviewState = RuleReviewStates.Pending
		});
		await db.SaveChangesAsync();
	}

	private static JsonElement Json(PageResponse response) => JsonDocument.Parse(response.Body).RootElement;

	private static object[] SignalsOf(JsonElement detail) =>
		detail.GetProperty("signals").EnumerateArray().Select(s => (object)new { kind = s.GetProperty("kind").GetString(), id = s.GetProperty("id").GetInt64() }).ToArray();

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task QueueGroupsSignalsByPlayerAndOneCaseDecidesThem(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		Staff staff = await SetUpAsync(probe);
		await SeedAsync(probe, staff);

		JsonElement queue = Json(await staff.Admin.Get("/api/v1/admin/cases"));
		JsonElement row = Assert.Single(queue.GetProperty("cases").EnumerateArray());
		Assert.Equal(staff.Player.ID, row.GetProperty("player").GetProperty("id").GetInt64());
		Assert.Equal(2, row.GetProperty("waiting").GetProperty("report").GetInt32());
		Assert.Equal(1, row.GetProperty("waiting").GetProperty("modules").GetInt32());
		Assert.Equal(1, row.GetProperty("waiting").GetProperty("rule_match").GetInt32());

		JsonElement moderated = Json(await staff.Moderator.Get("/api/v1/admin/cases"));
		JsonElement moderatorRow = Assert.Single(moderated.GetProperty("cases").EnumerateArray());
		Assert.Equal(["report"], moderatorRow.GetProperty("waiting").EnumerateObject().Select(p => p.Name).ToArray());

		PageResponse read = await staff.Admin.Get($"/api/v1/admin/players/{staff.Player.ID}/case");
		Assert.Equal(200, read.Status);
		JsonElement detail = Json(read);
		Assert.Equal(4, detail.GetProperty("signals").GetArrayLength());
		JsonElement cheating = detail.GetProperty("signals").EnumerateArray().Single(s => s.TryGetProperty("category", out JsonElement c) && c.GetString() == "cheating");
		Assert.Equal("map hack", cheating.GetProperty("reason").GetString());
		Assert.Equal(staff.Reporter.ID, cheating.GetProperty("reporter").GetProperty("id").GetInt64());
		await using (AppDbContext db = probe.Open())
		{
			Assert.True(await db.AuditLog.AnyAsync(a => a.Action == ModerationActions.CaseView && a.TargetID == staff.Player.ID));
		}

		Dictionary<string, string> moderatorCsrf = await staff.Moderator.Csrf();
		PageResponse refused = await staff.Moderator.SendJson("POST", $"/api/v1/admin/players/{staff.Player.ID}/case/close", new { signals = SignalsOf(detail), outcome = "no_action" }, moderatorCsrf);
		Assert.Equal(403, refused.Status);

		Dictionary<string, string> csrf = await staff.Admin.Csrf();
		PageResponse closed = await staff.Admin.SendJson("POST", $"/api/v1/admin/players/{staff.Player.ID}/case/close", new { signals = SignalsOf(detail), outcome = "no_action", note = "false alarm" }, csrf);
		Assert.Equal(200, closed.Status);
		Assert.Equal(4, Json(closed).GetProperty("decided").GetInt32());
		long caseID = Json(closed).GetProperty("case_id").GetInt64();

		await using (AppDbContext db = probe.Open())
		{
			Assert.All(await db.PlayerReports.Where(r => r.ReportedUserID == staff.Player.ID).ToListAsync(), r =>
			{
				Assert.Equal(PlayerReportStatuses.Dismissed, r.Status);
				Assert.Equal(caseID, r.CaseID);
				Assert.Equal("false alarm", r.ResolutionNote);
			});
			AnticheatModuleReport module = await db.AnticheatModuleReports.SingleAsync(r => r.UserId == staff.Player.ID);
			Assert.NotNull(module.ReviewedAt);
			Assert.Equal(caseID, module.CaseID);
			AutomatedRuleMatch match = await db.AutomatedRuleMatches.SingleAsync(m => m.UserID == staff.Player.ID);
			Assert.Equal(RuleReviewStates.Dismissed, match.ReviewState);
			Assert.Equal(caseID, match.CaseID);
		}

		Assert.Equal(0, Json(await staff.Admin.Get("/api/v1/admin/cases")).GetProperty("cases").GetArrayLength());
		JsonElement decided = Json(await staff.Admin.Get($"/api/v1/admin/cases/{caseID}"));
		Assert.Equal(4, decided.GetProperty("signals").GetArrayLength());
		Assert.Equal("no_action", decided.GetProperty("case").GetProperty("outcome").GetString());
		PageResponse again = await staff.Admin.SendJson("POST", $"/api/v1/admin/players/{staff.Player.ID}/case/close", new { signals = SignalsOf(detail), outcome = "no_action" }, csrf);
		Assert.Equal(409, again.Status);
	}

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task ClaimedCaseClosesWithASanctionThatPointsAtTheReport(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		Staff staff = await SetUpAsync(probe);
		await SeedAsync(probe, staff);
		Dictionary<string, string> csrf = await staff.Moderator.Csrf();

		Assert.Equal(200, (await staff.Moderator.SendJson("PUT", $"/api/v1/admin/players/{staff.Player.ID}/case/claim", new { }, csrf)).Status);
		JsonElement row = Json(await staff.Admin.Get("/api/v1/admin/cases")).GetProperty("cases")[0];
		Assert.Equal("Warden", row.GetProperty("claimed_by").GetProperty("name").GetString());
		Assert.Equal(0, Json(await staff.Admin.Get("/api/v1/admin/cases?claimed=unclaimed")).GetProperty("cases").GetArrayLength());

		JsonElement detail = Json(await staff.Moderator.Get($"/api/v1/admin/players/{staff.Player.ID}/case"));
		Assert.Equal(2, detail.GetProperty("signals").GetArrayLength());
		PageResponse closed = await staff.Moderator.SendJson("POST", $"/api/v1/admin/players/{staff.Player.ID}/case/close",
			new { signals = SignalsOf(detail), outcome = "sanctioned", sanction = new { type = "mute", duration_s = 600, reason = "abuse" }, note = "muted" }, csrf);
		Assert.Equal(200, closed.Status);
		long sanctionID = Json(closed).GetProperty("sanction_id").GetInt64();

		await using AppDbContext db = probe.Open();
		Sanction sanction = await db.Sanctions.SingleAsync(s => s.ID == sanctionID);
		Assert.Equal(SanctionTypes.Mute, sanction.Type);
		Assert.NotNull(sanction.ReportID);
		Assert.All(await db.PlayerReports.Where(r => r.ReportedUserID == staff.Player.ID).ToListAsync(), r => Assert.Equal(PlayerReportStatuses.Actioned, r.Status));
		ModerationCase decision = await db.ModerationCases.SingleAsync(c => c.UserID == staff.Player.ID);
		Assert.Equal(ModerationCaseStatuses.Closed, decision.Status);
		Assert.Equal(ModerationCaseOutcomes.Sanctioned, decision.Outcome);
		Assert.Equal(sanctionID, decision.SanctionID);

		JsonElement left = Json(await staff.Admin.Get("/api/v1/admin/cases")).GetProperty("cases")[0];
		Assert.False(left.GetProperty("waiting").TryGetProperty("report", out _));
		Assert.Equal(1, left.GetProperty("prior_cases").GetInt32());
	}

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task PlayerReportsCarryACategoryAndOnlyASharedMatch(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		await new AdminBrowser(probe).SignUp("Witness", "witness@example.org");
		await new AdminBrowser(probe).SignUp("Suspect", "suspect@example.org");
		User witness = (await PasswordPagesTests.UserByEmail(probe, "witness@example.org"))!;
		User suspect = (await PasswordPagesTests.UserByEmail(probe, "suspect@example.org"))!;
		var factory = new ProbeFactory(probe);

		Result<Api.CreateReportResponse> unknown = await MessageActions.ReportAsync(factory, witness.ID, new Api.CreateReportRequest { UserId = suspect.ID, Category = "spam" });
		Assert.Equal(ErrorCodes.InvalidArgument, unknown.Code);
		Result<Api.CreateReportResponse> notShared = await MessageActions.ReportAsync(factory, witness.ID, new Api.CreateReportRequest { UserId = suspect.ID, Category = "cheating", MatchId = 424242 });
		Assert.Equal(ErrorCodes.InvalidArgument, notShared.Code);
		Assert.Contains("match_id", JsonSerializer.Serialize(notShared.Params));
		Result<Api.CreateReportResponse> message = await MessageActions.ReportAsync(factory, witness.ID, new Api.CreateReportRequest { MessageId = "1", Category = "cheating" });
		Assert.Equal(ErrorCodes.InvalidArgument, message.Code);

		Result<Api.CreateReportResponse> filed = await MessageActions.ReportAsync(factory, witness.ID, new Api.CreateReportRequest { UserId = suspect.ID, Category = "griefing", Reason = "blocked the base" });
		Assert.True(filed.Ok);
		await using AppDbContext db = probe.Open();
		PlayerReport report = await db.PlayerReports.SingleAsync(r => r.ReportedUserID == suspect.ID);
		Assert.Equal(PlayerReportCategories.Griefing, report.Category);
		Assert.Null(report.MatchID);
	}

	[Theory]
	[InlineData("bogus", null, "outcome")]
	[InlineData("no_action", "mute", "sanction")]
	[InlineData("sanctioned", null, "sanction")]
	public void CloseRequestIsChecked(string outcome, string? sanction, string field)
	{
		CloseCaseRequest request = new CloseCaseRequest
		{
			Signals = [new CaseSignalRef { Kind = CaseSignalKinds.Report, Id = 1 }],
			Outcome = outcome,
			Sanction = sanction == null ? null : new Api.CreateSanctionRequest { Type = Api.SanctionType.Mute, DurationS = 60 }
		};
		Failure? failure = CaseActions.Validate(request, CaseSignalKinds.All);
		Assert.Equal(ErrorCodes.InvalidArgument, failure?.Code);
		Assert.Contains(field, JsonSerializer.Serialize(failure?.Params));
	}

	[Fact]
	public void CloseOfAKindTheCallerMayNotReviewIsForbidden()
	{
		CloseCaseRequest request = new CloseCaseRequest { Signals = [new CaseSignalRef { Kind = "modules", Id = 1 }], Outcome = "no_action" };
		Assert.Equal(ErrorCodes.Forbidden, CaseActions.Validate(request, [CaseSignalKinds.Report])?.Code);
	}

	[Fact]
	public void AntiCheatSignalsOutrankReports() =>
		Assert.True(CaseActions.PriorityOf(new Dictionary<string, int> { ["modules"] = 1 }) > CaseActions.PriorityOf(new Dictionary<string, int> { ["report"] = 3 }));
}
