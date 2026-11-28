using Database;
using GenOnlineService.Portal.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Text;
using Xunit;

namespace GenOnlineService.Tests
{
	public class EpicSanctionsTests
	{
		private const string Puid = "0002aabbccddeeff0011223344556677";
		private static readonly DateTime Start = new(2026, 11, 28, 12, 0, 0, DateTimeKind.Utc);
		private static readonly EpicSanctionsClient.Settings Config = new("client-id", "client-secret", "deployment-1");

		private sealed class Clock(DateTime now) : TimeProvider
		{
			public DateTime Now = now;
			public override DateTimeOffset GetUtcNow() => new(Now);
		}

		private sealed class FakeEpic : HttpMessageHandler
		{
			public readonly List<(HttpRequestMessage Request, string Body)> Requests = [];
			public Func<HttpRequestMessage, HttpResponseMessage> Token = _ => Json(HttpStatusCode.OK, "{\"access_token\":\"token-1\",\"expires_in\":3600}");
			public Func<HttpRequestMessage, HttpResponseMessage> Sanctions = _ => Json(HttpStatusCode.OK, "{\"elements\":[]}");

			public int TokenCalls => Requests.Count(r => r.Request.RequestUri!.AbsolutePath == "/auth/v1/oauth/token");
			public int SanctionCalls => Requests.Count(r => r.Request.RequestUri!.AbsolutePath.StartsWith("/sanctions/", StringComparison.Ordinal));

			protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
			{
				string body = request.Content == null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
				Requests.Add((request, body));
				return request.RequestUri!.AbsolutePath == "/auth/v1/oauth/token" ? Token(request) : Sanctions(request);
			}
		}

		private static HttpResponseMessage Json(HttpStatusCode status, string json) => new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

		private static (EpicSanctionsClient Client, FakeEpic Epic, Clock Clock) Create(EpicSanctionsClient.Settings? settings = null)
		{
			FakeEpic epic = new();
			Clock clock = new(Start);
			return (new EpicSanctionsClient(settings ?? Config, null, new HttpClient(epic), clock), epic, clock);
		}

		[Fact]
		public async Task TheTokenIsKeptUntilAMinuteBeforeItExpires()
		{
			(EpicSanctionsClient client, FakeEpic epic, Clock clock) = Create();
			long expiresAt = new DateTimeOffset(Start.AddHours(1)).ToUnixTimeSeconds();
			epic.Token = _ => Json(HttpStatusCode.OK, "{\"access_token\":\"token-1\",\"expires_at\":" + expiresAt + ",\"expires_in\":3600}");

			Assert.NotNull(await client.GetActiveAsync("puid-a"));
			clock.Now = Start.AddMinutes(58);
			Assert.NotNull(await client.GetActiveAsync("puid-b"));
			Assert.Equal(1, epic.TokenCalls);

			clock.Now = Start.AddMinutes(59).AddSeconds(1);
			Assert.NotNull(await client.GetActiveAsync("puid-c"));
			Assert.Equal(2, epic.TokenCalls);
		}

		[Fact]
		public async Task TheTokenRequestUsesTheClientCredentialsOfTheDeployment()
		{
			(EpicSanctionsClient client, FakeEpic epic, _) = Create();

			Assert.NotNull(await client.GetActiveAsync(Puid));

			(HttpRequestMessage token, string body) = epic.Requests[0];
			Assert.Equal(HttpMethod.Post, token.Method);
			Assert.Equal("https://api.epicgames.dev/auth/v1/oauth/token", token.RequestUri!.ToString());
			Assert.Equal("Basic", token.Headers.Authorization!.Scheme);
			Assert.Equal(Convert.ToBase64String(Encoding.UTF8.GetBytes("client-id:client-secret")), token.Headers.Authorization.Parameter);
			Assert.Equal("grant_type=client_credentials&deployment_id=deployment-1", body);

			HttpRequestMessage sanctions = epic.Requests[1].Request;
			Assert.Equal(HttpMethod.Get, sanctions.Method);
			Assert.Equal("https://api.epicgames.dev/sanctions/v1/productUser/" + Puid + "/active", sanctions.RequestUri!.ToString());
			Assert.Equal(("Bearer", "token-1"), (sanctions.Headers.Authorization!.Scheme, sanctions.Headers.Authorization.Parameter));
		}

		[Fact]
		public async Task TheActiveSanctionsKeepTheirTimesAndAPermanentOneHasNoExpiration()
		{
			(EpicSanctionsClient client, FakeEpic epic, _) = Create();
			long placed = new DateTimeOffset(Start.AddDays(-2)).ToUnixTimeSeconds();
			long expires = new DateTimeOffset(Start.AddDays(5)).ToUnixTimeSeconds();
			epic.Sanctions = _ => Json(HttpStatusCode.OK, "{\"elements\":[{\"referenceId\":\"ref-1\",\"timestamp\":" + placed + ",\"action\":\"BAN\",\"expirationTimestamp\":null},"
				+ "{\"referenceId\":\"ref-2\",\"timestamp\":" + placed + ",\"action\":\"RESTRICT_MATCHMAKING\",\"expirationTimestamp\":" + expires + "}]}");

			IReadOnlyList<EpicSanction>? sanctions = await client.GetActiveAsync(Puid);

			Assert.Equal([new EpicSanction("ref-1", "BAN", Start.AddDays(-2), null), new EpicSanction("ref-2", "RESTRICT_MATCHMAKING", Start.AddDays(-2), Start.AddDays(5))], sanctions);
		}

		[Fact]
		public async Task TheResultOfOnePlayerIsKeptForTenMinutes()
		{
			(EpicSanctionsClient client, FakeEpic epic, Clock clock) = Create();

			await client.GetActiveAsync(Puid);
			clock.Now = Start.AddMinutes(9);
			await client.GetActiveAsync(Puid);
			Assert.Equal(1, epic.SanctionCalls);

			clock.Now = Start.AddMinutes(10).AddSeconds(1);
			await client.GetActiveAsync(Puid);
			Assert.Equal(2, epic.SanctionCalls);
		}

		[Theory]
		[InlineData("client_id")]
		[InlineData("client_secret")]
		[InlineData("deployment_id")]
		public void TheCheckIsOffWithoutEveryValue(string missing)
		{
			Dictionary<string, string?> values = new()
			{
				["Epic:client_id"] = "client-id", ["Epic:client_secret"] = "client-secret", ["Middleware:deployment_id"] = "deployment-1"
			};
			Assert.Equal(Config, EpicSanctionsClient.Load(new ConfigurationBuilder().AddInMemoryCollection(values).Build()));

			values.Remove(values.Keys.Single(key => key.EndsWith(missing, StringComparison.Ordinal)));
			Assert.Null(EpicSanctionsClient.Load(new ConfigurationBuilder().AddInMemoryCollection(values).Build()));
		}

		[Fact]
		public async Task AnUnconfiguredClientNeverCallsEpic()
		{
			FakeEpic epic = new();
			EpicSanctionsClient client = new(null, null, new HttpClient(epic), new Clock(Start));

			Assert.Null(await client.GetActiveAsync(Puid));
			await client.CheckAsync(7, Puid);
			Assert.Empty(epic.Requests);
		}

		public static TheoryData<string> Failures => ["token_500", "token_401", "sanctions_403", "sanctions_500", "network", "malformed"];

		[Theory]
		[MemberData(nameof(Failures))]
		public async Task AFailureAnswersNothingAndNeverThrows(string failure)
		{
			(EpicSanctionsClient client, FakeEpic epic, _) = Create();
			switch (failure)
			{
				case "token_500": epic.Token = _ => Json(HttpStatusCode.InternalServerError, "{}"); break;
				case "token_401": epic.Token = _ => Json(HttpStatusCode.Unauthorized, "{\"errorCode\":\"errors.com.epicgames.oauth.invalid_client\"}"); break;
				case "sanctions_403": epic.Sanctions = _ => Json(HttpStatusCode.Forbidden, "{}"); break;
				case "sanctions_500": epic.Sanctions = _ => Json(HttpStatusCode.InternalServerError, "{}"); break;
				case "network": epic.Sanctions = _ => throw new HttpRequestException("connection refused"); break;
				case "malformed": epic.Sanctions = _ => Json(HttpStatusCode.OK, "{\"elements\":[{\"referenceId\":"); break;
			}

			Assert.Null(await client.GetActiveAsync(Puid));
			await client.CheckAsync(7, Puid);
		}

		[Fact]
		public async Task AFailurePausesEveryLookupForAMinute()
		{
			(EpicSanctionsClient client, FakeEpic epic, Clock clock) = Create();
			epic.Token = _ => Json(HttpStatusCode.Unauthorized, "{}");

			Assert.Null(await client.GetActiveAsync("puid-a"));
			clock.Now = Start.AddSeconds(59);
			Assert.Null(await client.GetActiveAsync("puid-b"));
			Assert.Equal(1, epic.TokenCalls);

			epic.Token = _ => Json(HttpStatusCode.OK, "{\"access_token\":\"token-1\",\"expires_in\":3600}");
			clock.Now = Start.AddMinutes(1);
			Assert.NotNull(await client.GetActiveAsync("puid-b"));
			Assert.Equal(2, epic.TokenCalls);
		}

		[Fact]
		public async Task ExpiredResultsAreDroppedWhenANewOneIsKept()
		{
			(EpicSanctionsClient client, FakeEpic epic, Clock clock) = Create();

			await client.GetActiveAsync("puid-a");
			clock.Now = Start.AddMinutes(11);
			await client.GetActiveAsync("puid-b");
			clock.Now = Start.AddMinutes(12);
			await client.GetActiveAsync("puid-a");

			Assert.Equal(3, epic.SanctionCalls);
			Assert.Equal(2, client.CachedResultCount);
		}

		[ProbeTheory]
		[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
		public async Task ASanctionIsOneEpicFindingByItsReferenceIdAndStaysAsHistory(string provider)
		{
			await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
			if (probe == null) return;

			EpicSanction ban = new("ref-1", "BAN", Start.AddDays(-2), null);
			EpicSanction restrict = new("ref-2", "RESTRICT_MATCHMAKING", Start.AddDays(-1), Start.AddDays(5));
			foreach (IReadOnlyList<EpicSanction> seen in new[] { [ban], [ban, restrict], new List<EpicSanction>() })
			{
				await using AppDbContext db = probe.Open();
				Assert.True(await Database.AntiCheat.RecordEpicSanctions(db, 7, seen, Start));
			}
			await using (AppDbContext db = probe.Open())
			{
				Assert.True(await Database.AntiCheat.RecordEacEvent(db, new EacEventReport(1, 7, 50, 0, "action_required", 1, 6, 0, "seen", Start)));
			}

			await using AppDbContext read = probe.Open();
			List<AnticheatEacFinding> findings = await read.AnticheatEacFindings.Where(f => f.Source == AnticheatEacSources.Epic).OrderBy(f => f.ReportId).ToListAsync();
			Assert.Equal([("ref-1", "BAN", (DateTime?)Start.AddDays(-2), (DateTime?)null), ("ref-2", "RESTRICT_MATCHMAKING", Start.AddDays(-1), Start.AddDays(5))],
				findings.Select(f => (f.ReferenceId, f.SanctionAction, f.PlacedAt, f.ExpiresAt)));
			Assert.All(findings, f => Assert.Equal(7L, f.UserId));

			Assert.Equal(1, await AntiCheatReviews.Rows(read, AntiCheatReviewKinds.Eac).CountAsync(r => r.UserID == 7));
			Assert.Equal(2, await AntiCheatReviews.Rows(read, AntiCheatReviewKinds.Epic).CountAsync(r => r.UserID == 7 && r.ReviewedAt == null));
			List<CaseSignalRow> signals = await Database.Cases.Signals(read, 7, null, [AntiCheatReviewKinds.Epic]);
			Assert.Equal(["ref-1", "ref-2"], signals.OrderBy(s => s.ID).Select(s => s.EpicReferenceId));
			Assert.Equal(1, await AntiCheatReviews.Decide(read, AntiCheatReviewKinds.Epic, [findings[0].ReportId], 7, 99, 5, DateTime.UtcNow));
			Assert.Equal(0, await AntiCheatReviews.Decide(read, AntiCheatReviewKinds.Eac, [findings[1].ReportId], 7, 99, 5, DateTime.UtcNow));
		}

		[Fact]
		public void ThePortalListsEpicFindingsApartFromClientReports()
		{
			Assert.Equal("Epic verified sanctions", PortalReviewList.Find("epic")?.Title);
			Assert.Contains("epic", PortalCaseKinds.All);
			Assert.Contains(AntiCheatReviewKinds.Epic, CaseSignalKinds.All);
		}
	}
}
