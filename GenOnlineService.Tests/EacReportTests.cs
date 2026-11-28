using Database;
using GenOnlineService.Portal.Client;
using GenOnlineService.Realtime;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GenOnlineService.Tests
{
	public class EacReportTests
	{
		private const long Me = 7;
		private static readonly DateTime At = new(2026, 11, 28, 12, 0, 0, DateTimeKind.Utc);

		private static ReportAnticheatEventRequest Action(long target = 5_000_000_001, int action = 1, int reason = 6, string? match = "8000000000") => new()
		{
			Kind = "action_required", TargetUserId = target, LobbyId = 12, MatchId = match, Action = action, ReasonCode = reason, Details = "  Client violation  "
		};

		private static ReportAnticheatEventRequest Violation(int type = 9, long? target = null) => new()
		{
			Kind = "integrity_violation", TargetUserId = target, ViolationType = type, Details = "Forbidden module cheat.dll"
		};

		[Fact]
		public void AnActionReportKeepsTheFullUserIdAndTheCodes()
		{
			Assert.Null(EacReports.Validate(Action(), Me, At, out EacEventReport? report));

			Assert.Equal(new EacEventReport(Me, 5_000_000_001, 8_000_000_000, 0, "action_required", 1, 6, 0, "Client violation", At), report);
		}

		[Fact]
		public void AReportWithoutAMatchKeepsTheLobby()
		{
			Assert.Null(EacReports.Validate(Action(match: null), Me, At, out EacEventReport? report));

			Assert.Equal((0L, 12L), (report!.MatchId, report.LobbyId));
		}

		[Fact]
		public void AnActionAboutTheCallerNeedsNoLobbyOrMatch()
		{
			Assert.Null(EacReports.Validate(Action(target: Me, match: null) with { LobbyId = null }, Me, At, out EacEventReport? report));

			Assert.Equal((Me, 0L, 0L), (report!.TargetUserId, report.MatchId, report.LobbyId));
		}

		[Fact]
		public void AnIntegrityViolationNamesTheReporter()
		{
			Assert.Null(EacReports.Validate(Violation(), Me, At, out EacEventReport? report));

			Assert.Equal(new EacEventReport(Me, Me, 0, 0, "integrity_violation", 0, 0, 9, "Forbidden module cheat.dll", At), report);
		}

		public static TheoryData<string, ReportAnticheatEventRequest> Refused => new()
		{
			{ "kind", Action() with { Kind = "banned" } },
			{ "kind", Action() with { Kind = null } },
			{ "target_user_id", Action(target: 0) },
			{ "action", Action(action: 2) },
			{ "reason_code", Action(reason: 11) },
			{ "reason_code", Action(reason: -1) },
			{ "violation_type", Action() with { ViolationType = 3 } },
			{ "violation_type", Violation(16) },
			{ "target_user_id", Violation(target: 8) },
			{ "action", Violation() with { Action = 1 } },
			{ "match_id", Action(match: "abc") },
			{ "match_id", Action(match: "-4") },
			{ "match_id", Action(match: "9223372036854775808") },
			{ "lobby_id", Action(match: null) with { LobbyId = null } },
			{ "lobby_id", Action() with { LobbyId = 0 } }
		};

		[Theory]
		[MemberData(nameof(Refused))]
		public void ABadFieldIsNamed(string field, ReportAnticheatEventRequest request)
		{
			Failure? failure = EacReports.Validate(request, Me, At, out _);

			Assert.Equal(ErrorCodes.InvalidArgument, failure?.Code);
			Assert.Equal(Result.Invalid(field).Params.ToString(), failure?.Params.ToString());
		}

		[Fact]
		public void TheDetailsStayWithin512BytesOnACharacterBoundary()
		{
			Assert.Null(EacReports.Validate(Violation() with { Details = new string('é', 400) }, Me, At, out EacEventReport? report));

			Assert.Equal(256, report!.Details.Length);
			Assert.Null(EacReports.Validate(Violation() with { Details = null }, Me, At, out report));
			Assert.Equal("", report!.Details);
		}

		[Fact]
		public void AUserSendsAtMost30EventsAMinute()
		{
			static SharedUserData User(long id) => new(id, new UserSocialContainer { Friends = [], PendingRequests = [], Blocked = [] }, "player", 1, new PlayerStats(id, 1500, 0, 1500));
			SharedUserData user = User(Me);
			for (int i = 0; i < 30; ++i)
			{
				Assert.True(EacReports.Limiter.TryConsume(user, out _));
			}

			Assert.False(EacReports.Limiter.TryConsume(user, out long retryAfterMs));
			Assert.InRange(retryAfterMs, 1, 60_000);
			Assert.True(EacReports.Limiter.TryConsume(User(Me + 1), out _));
		}

		[Fact]
		public void ThePortalNamesEveryCode()
		{
			Assert.Equal("Remove player", PortalWords.EacAction(1));
			Assert.Equal("Temporary ban", PortalWords.EacReason(9));
			Assert.Equal("Permanent ban", PortalWords.EacReason(10));
			Assert.Equal("Forbidden module loaded", PortalWords.EacViolation(9));
			Assert.Equal("Forbidden system configuration", PortalWords.EacViolation(15));
			Assert.Equal("Code 99", PortalWords.EacViolation(99));
			Assert.All(Enumerable.Range(0, 11), code => Assert.DoesNotContain("Code", PortalWords.EacReason(code)));
			Assert.All(Enumerable.Range(0, 16), code => Assert.DoesNotContain("Code", PortalWords.EacViolation(code)));
			Assert.Equal("Easy Anti-Cheat", PortalReviewList.Find("eac")?.Title);
		}

		[ProbeTheory]
		[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
		public async Task AReportAboutAnotherPlayerMustShareTheirLobbyOrARecentMatch(string provider)
		{
			await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
			if (probe == null) return;

			const long Target = 9;
			await using (AppDbContext seed = probe.Open())
			{
				foreach ((long id, int hoursAgo) in new[] { (50L, 1), (60L, 7) })
				{
					seed.Matches.Add(new Match { Id = id, Name = "m" + id, MapName = "Tournament Desert", RosterType = "1v1", StartedAt = At.AddHours(-hoursAgo) });
					seed.MatchPlayers.AddRange(new MatchPlayer { MatchId = id, Slot = 0, UserId = Me }, new MatchPlayer { MatchId = id, Slot = 1, UserId = Target });
				}
				await seed.SaveChangesAsync();
			}

			EacEventReport inMatch = new(Me, Target, 50, 0, "action_required", 1, 6, 0, "", At);
			EacEventReport inLobby = inMatch with { MatchId = 0, LobbyId = 12 };
			await using AppDbContext db = probe.Open();
			Task<Failure?> Check(EacEventReport report, long lobby = -1, bool targetInLobby = false, long match = 0) => EacReports.CheckScope(db, report, lobby, targetInLobby, match, At);
			static string? Named(Failure? failure) => failure is Failure f ? f.Code + f.Params : null;

			Assert.Null(await Check(inMatch));
			Assert.Null(await Check(inMatch with { MatchId = 60 }, match: 60));
			Assert.Null(await Check(inLobby, lobby: 12, targetInLobby: true));
			string? match = Named(Result.NotFound("match"));
			Assert.Equal(match, Named(await Check(inMatch with { MatchId = 60 })));
			Assert.Equal(match, Named(await Check(inMatch with { MatchId = 61 }, match: 61)));
			Assert.Equal(match, Named(await Check(inMatch with { TargetUserId = Target + 1 })));
			Assert.Equal(match, Named(await Check(inMatch with { ReporterId = Target + 1, TargetUserId = Me })));
			string? lobby = Named(Result.NotFound("lobby"));
			Assert.Equal(lobby, Named(await Check(inLobby, lobby: 13, targetInLobby: true)));
			Assert.Equal(lobby, Named(await Check(inLobby, lobby: 12)));
		}

		[ProbeTheory]
		[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
		public async Task WitnessesOfOneTargetAndMatchMeetInOneFinding(string provider)
		{
			await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
			if (probe == null) return;

			EacEventReport first = new(1, 7, 50, 12, "action_required", 1, 6, 0, "seen by one", At);
			foreach (EacEventReport report in new[] { first, first with { Details = "repeat" }, first with { ReporterId = 2, Details = "seen by two" }, new EacEventReport(7, 7, 50, 12, "integrity_violation", 0, 0, 9, "self", At) })
			{
				await using AppDbContext db = probe.Open();
				Assert.True(await Database.AntiCheat.RecordEacEvent(db, report));
			}

			await using AppDbContext read = probe.Open();
			List<AnticheatEacFinding> findings = await read.AnticheatEacFindings.OrderBy(f => f.ReportId).ToListAsync();
			Assert.Equal([("action_required", false), ("integrity_violation", true)], findings.Select(f => (f.Kind, f.SelfReported)));
			Assert.All(findings, f => Assert.Equal((7L, 50L, 12L), (f.UserId, f.MatchId, f.LobbyId)));

			List<AnticheatEacWitness> witnesses = await read.AnticheatEacWitnesses.Where(w => w.FindingId == findings[0].ReportId).OrderBy(w => w.ReporterId).ToListAsync();
			Assert.Equal([(1L, "seen by one"), (2L, "seen by two")], witnesses.Select(w => (w.ReporterId, w.Details)));

			Assert.Equal(2, await AntiCheatReviews.Rows(read, AntiCheatReviewKinds.Eac).CountAsync(r => r.UserID == 7 && r.ReviewedAt == null));
			List<CaseSignalRow> signals = await Database.Cases.Signals(read, 7, null, [AntiCheatReviewKinds.Eac]);
			Assert.Equal(["action_required", "integrity_violation"], signals.OrderBy(s => s.ID).Select(s => s.EacKind));
			Assert.Equal(1, await AntiCheatReviews.Decide(read, AntiCheatReviewKinds.Eac, [findings[0].ReportId], 7, 99, 5, DateTime.UtcNow));
		}
	}
}
