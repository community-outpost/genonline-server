/*
**    GeneralsOnline Game Services - Backend Services for Command & Conquer Generals Online: Zero Hour
**    Copyright (C) 2025  GeneralsOnline Development Team
**
**    This program is free software: you can redistribute it and/or modify
**    it under the terms of the GNU Affero General Public License as
**    published by the Free Software Foundation, either version 3 of the
**    License, or (at your option) any later version.
**
**    This program is distributed in the hope that it will be useful,
**    but WITHOUT ANY WARRANTY; without even the implied warranty of
**    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
**    GNU Affero General Public License for more details.
**
**    You should have received a copy of the GNU Affero General Public License
**    along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

using GenOnlineService.Identity;
using GenOnlineService.Rest;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Xunit;

namespace GenOnlineService.Tests;

public class AppealWindowTests
{
	private static readonly DateTime Now = new DateTime(2026, 11, 22, 12, 0, 0, DateTimeKind.Utc);

	private static Sanction Ban(DateTime? expires = null, DateTime? lifted = null, string type = SanctionTypes.Ban, string source = AuthoritySources.Local) =>
		new Sanction { ID = 1, UserID = 7, Type = type, Source = source, CreatedAt = Now.AddDays(-90), ExpiresAt = expires, LiftedAt = lifted };

	[Fact]
	public void AnActiveBanOrMuteMayBeAppealed()
	{
		Assert.True(Database.Appeals.CanAppeal(Ban(), Now));
		Assert.True(Database.Appeals.CanAppeal(Ban(expires: Now.AddDays(3)), Now));
		Assert.True(Database.Appeals.CanAppeal(Ban(type: SanctionTypes.Mute, expires: Now.AddDays(1)), Now));
	}

	[Fact]
	public void AnEndedSanctionMayBeAppealedForThirtyDays()
	{
		Assert.True(Database.Appeals.CanAppeal(Ban(expires: Now.AddDays(-30)), Now));
		Assert.False(Database.Appeals.CanAppeal(Ban(expires: Now.AddDays(-30).AddSeconds(-1)), Now));
		Assert.True(Database.Appeals.CanAppeal(Ban(lifted: Now.AddDays(-5)), Now));
		Assert.False(Database.Appeals.CanAppeal(Ban(lifted: Now.AddDays(-31)), Now));
	}

	[Fact]
	public void TheEarlierEndCountsWhenASanctionWasLiftedBeforeItExpired()
	{
		// lifted 40 days ago, would have expired 2 days ago: it ended 40 days ago
		Assert.False(Database.Appeals.CanAppeal(Ban(expires: Now.AddDays(-2), lifted: Now.AddDays(-40)), Now));
		// lifted in the future of its own expiry is not possible, a later lift of an expired one counts the expiry
		Assert.False(Database.Appeals.CanAppeal(Ban(expires: Now.AddDays(-40), lifted: Now.AddDays(-2)), Now));
	}

	[Fact]
	public void AKickAndASanctionOfAProviderCannotBeAppealed()
	{
		Assert.False(Database.Appeals.CanAppeal(Ban(type: SanctionTypes.Kick, expires: Now), Now));
		Assert.False(Database.Appeals.CanAppeal(Ban(source: AuthoritySources.Upstream("discord")), Now));
	}

	// A play-banned player's token is refused everywhere except on these two routes
	[Fact]
	public void OnlyTheSanctionAndAppealRoutesAllowABannedSession()
	{
		string[] allowed = typeof(Database.Appeals).Assembly.GetTypes()
			.SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
			.Where(m => m.GetCustomAttribute<AllowBannedSessionAttribute>() != null)
			.Select(m => m.DeclaringType!.Name + "." + m.Name)
			.Order().ToArray();

		Assert.Equal(new[] { "MeAppealsController.Create", "MeAppealsController.List" }, allowed);
	}
}

[Collection(AdminAreaCollection.Name)]
public class AppealsProbeTests
{
	private const long Player = 501, Other = 502, Reviewer = 601;

	private sealed class OneContextFactory(ProbeDatabase probe) : IDbContextFactory<AppDbContext>
	{
		public AppDbContext CreateDbContext() => probe.Open();
	}

	private sealed class Grants(params string[] permissions) : IAuthorizationService
	{
		public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, IEnumerable<IAuthorizationRequirement> requirements) => Task.FromResult(AuthorizationResult.Failed());

		public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, string policyName) =>
			Task.FromResult(permissions.Select(PermissionPolicies.For).Contains(policyName) ? AuthorizationResult.Success() : AuthorizationResult.Failed());
	}

	private static DefaultHttpContext ContextOf(long caller, object? body = null)
	{
		DefaultHttpContext http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, caller.ToString())], "test")) };
		if (body != null)
		{
			byte[] json = JsonSerializer.SerializeToUtf8Bytes(body, GenOnlineService.Api.ApiResults.Json);
			http.Request.Body = new MemoryStream(json);
			http.Request.ContentLength = json.Length;
			http.Request.ContentType = "application/json";
		}
		return http;
	}

	private static MeAppealsController PlayerSide(ProbeDatabase probe, long caller, object? body = null) =>
		new(new OneContextFactory(probe)) { ControllerContext = new ControllerContext { HttpContext = ContextOf(caller, body) } };

	private static StaffAppealsController StaffSide(ProbeDatabase probe, object? body, params string[] permissions) =>
		new(new OneContextFactory(probe), new Grants(permissions)) { ControllerContext = new ControllerContext { HttpContext = ContextOf(Reviewer, body) } };

	private static (int Status, JsonElement Body) Read(IActionResult result)
	{
		return result switch
		{
			JsonResult json => (json.StatusCode ?? 200, JsonSerializer.SerializeToElement(json.Value, GenOnlineService.Api.ApiResults.Json)),
			StatusCodeResult plain => (plain.StatusCode, default),
			_ => throw new InvalidOperationException(result.GetType().Name)
		};
	}

	private static async Task<Sanction> Add(AppDbContext db, long user, string type, DateTime? expires = null, DateTime? lifted = null, DateTime? created = null)
	{
		Sanction s = new Sanction { UserID = user, Type = type, Reason = "toxic chat", CreatedByKind = AuditActorKinds.User, CreatedByID = Reviewer, CreatedAt = created ?? DateTime.UtcNow.AddDays(-60), ExpiresAt = expires, LiftedAt = lifted };
		db.Sanctions.Add(s);
		await db.SaveChangesAsync();
		db.ChangeTracker.Clear();
		return s;
	}

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task APlayerReadsOnlyTheirOwnMutesAndBansAndAppealsOneOnce(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		await using AppDbContext db = probe.Open();
		Sanction ban = await Add(db, Player, SanctionTypes.Ban);
		await Add(db, Player, SanctionTypes.Kick, expires: DateTime.UtcNow.AddDays(-1));
		Sanction others = await Add(db, Other, SanctionTypes.Ban);

		(int status, JsonElement list) = Read(await PlayerSide(probe, Player).List());
		Assert.Equal(200, status);
		JsonElement only = Assert.Single(list.GetProperty("sanctions").EnumerateArray());
		Assert.Equal(ban.ID, only.GetProperty("id").GetInt64());
		Assert.Equal("toxic chat", only.GetProperty("reason").GetString());
		Assert.True(only.GetProperty("can_appeal").GetBoolean());
		Assert.False(only.TryGetProperty("created_by", out _));

		(status, JsonElement made) = Read(await PlayerSide(probe, Player, new AppealRequest { Text = "  It was a joke.  " }).Create(ban.ID));
		Assert.Equal(201, status);
		Assert.Equal("open", made.GetProperty("status").GetString());
		Assert.Equal("It was a joke.", made.GetProperty("text").GetString());

		Assert.Equal(409, Read(await PlayerSide(probe, Player, new AppealRequest { Text = "again" }).Create(ban.ID)).Status);
		Assert.Equal(404, Read(await PlayerSide(probe, Player, new AppealRequest { Text = "mine now" }).Create(others.ID)).Status);

		(_, list) = Read(await PlayerSide(probe, Player).List());
		only = Assert.Single(list.GetProperty("sanctions").EnumerateArray());
		Assert.False(only.GetProperty("can_appeal").GetBoolean());
		Assert.Equal("open", only.GetProperty("appeal").GetProperty("status").GetString());
		Assert.Equal(1, await db.AuditLog.CountAsync(e => e.Action == ModerationActions.AppealOpen && e.ActorID == Player));
	}

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task AnAppealNeedsTextAndASanctionInsideTheWindow(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		await using AppDbContext db = probe.Open();
		Sanction old = await Add(db, Player, SanctionTypes.Mute, expires: DateTime.UtcNow.AddDays(-45), created: DateTime.UtcNow.AddDays(-50));
		Sanction kick = await Add(db, Player, SanctionTypes.Kick, expires: DateTime.UtcNow);
		Sanction live = await Add(db, Player, SanctionTypes.Mute, expires: DateTime.UtcNow.AddDays(2));

		Assert.Equal(400, Read(await PlayerSide(probe, Player, new AppealRequest { Text = "  " }).Create(live.ID)).Status);
		Assert.Equal(400, Read(await PlayerSide(probe, Player, new AppealRequest { Text = new string('x', 2001) }).Create(live.ID)).Status);
		Assert.Equal(422, Read(await PlayerSide(probe, Player, new AppealRequest { Text = "late" }).Create(old.ID)).Status);
		Assert.Equal(422, Read(await PlayerSide(probe, Player, new AppealRequest { Text = "a kick" }).Create(kick.ID)).Status);
		Assert.Equal(201, Read(await PlayerSide(probe, Player, new AppealRequest { Text = "in time" }).Create(live.ID)).Status);
	}

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task StaffLiftAShortenOrUpholdAndTheDecisionIsFinal(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		await using AppDbContext db = probe.Open();
		Sanction ban = await Add(db, Player, SanctionTypes.Ban);
		Sanction mute = await Add(db, Player, SanctionTypes.Mute, expires: DateTime.UtcNow.AddDays(10));
		Sanction held = await Add(db, Player, SanctionTypes.Mute, expires: DateTime.UtcNow.AddDays(10));
		long banAppeal = (await Database.Appeals.Open(db, Player, ban.ID, "please", DateTime.UtcNow)).Appeal!.ID;
		long muteAppeal = (await Database.Appeals.Open(db, Player, mute.ID, "please", DateTime.UtcNow)).Appeal!.ID;
		long heldAppeal = (await Database.Appeals.Open(db, Player, held.ID, "please", DateTime.UtcNow)).Appeal!.ID;

		(int status, JsonElement queue) = Read(await StaffSide(probe, null, Permissions.AppealsReview).List(AppealStatuses.Open));
		Assert.Equal(200, status);
		Assert.Equal(3, queue.GetProperty("appeals").GetArrayLength());

		Assert.Equal(403, Read(await StaffSide(probe, null).List(null)).Status);
		var lift = new DecideAppealRequest { Decision = "lift", Reason = "first offence" };
		Assert.Equal(403, Read(await StaffSide(probe, lift, Permissions.AppealsReview, Permissions.ModerationMute).Decide(banAppeal)).Status);
		Assert.Equal(400, Read(await StaffSide(probe, new DecideAppealRequest { Decision = "lift" }, Permissions.AppealsReview, Permissions.ModerationBan).Decide(banAppeal)).Status);
		Assert.Equal(400, Read(await StaffSide(probe, new DecideAppealRequest { Decision = "maybe", Reason = "x" }, Permissions.AppealsReview).Decide(banAppeal)).Status);

		(status, JsonElement decided) = Read(await StaffSide(probe, lift, Permissions.AppealsReview, Permissions.ModerationBan).Decide(banAppeal));
		Assert.Equal(200, status);
		Assert.Equal("lifted", decided.GetProperty("status").GetString());
		Assert.Equal("first offence", decided.GetProperty("decision_reason").GetString());
		Assert.NotNull((await Database.Moderation.GetSanction(db, ban.ID))!.LiftedAt);

		Assert.Equal(409, Read(await StaffSide(probe, new DecideAppealRequest { Decision = "uphold", Reason = "no" }, Permissions.AppealsReview).Decide(banAppeal)).Status);
		Assert.Equal("lifted", (await Database.Appeals.Get(db, banAppeal))!.Status);

		var past = new DecideAppealRequest { Decision = "shorten", Reason = "r", ExpiresAt = DateTime.UtcNow.AddDays(-1) };
		var later = new DecideAppealRequest { Decision = "shorten", Reason = "r", ExpiresAt = DateTime.UtcNow.AddDays(20) };
		Assert.Equal(400, Read(await StaffSide(probe, past, Permissions.AppealsReview, Permissions.ModerationMute).Decide(muteAppeal)).Status);
		Assert.Equal(400, Read(await StaffSide(probe, later, Permissions.AppealsReview, Permissions.ModerationMute).Decide(muteAppeal)).Status);
		DateTime sooner = DateTime.UtcNow.AddDays(1);
		Assert.Equal(200, Read(await StaffSide(probe, new DecideAppealRequest { Decision = "shorten", Reason = "r", ExpiresAt = sooner }, Permissions.AppealsReview, Permissions.ModerationMute).Decide(muteAppeal)).Status);
		Assert.True(Math.Abs(((await Database.Moderation.GetSanction(db, mute.ID))!.ExpiresAt!.Value - sooner).TotalSeconds) < 1);

		Assert.Equal(200, Read(await StaffSide(probe, new DecideAppealRequest { Decision = "uphold", Reason = "stands" }, Permissions.AppealsReview).Decide(heldAppeal)).Status);
		Assert.Null((await Database.Moderation.GetSanction(db, held.ID))!.LiftedAt);

		Assert.Equal(3, await db.AuditLog.CountAsync(e => e.Action == ModerationActions.AppealDecide));
		(_, JsonElement mine) = Read(await PlayerSide(probe, Player).List());
		Assert.Contains(mine.GetProperty("sanctions").EnumerateArray(), s => s.GetProperty("appeal").GetProperty("decision_reason").GetString() == "stands");
	}

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task StaffNeverDecideTheirOwnAppeal(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		await using AppDbContext db = probe.Open();
		Sanction mine = await Add(db, Reviewer, SanctionTypes.Mute, expires: DateTime.UtcNow.AddDays(10));
		long appeal = (await Database.Appeals.Open(db, Reviewer, mine.ID, "please", DateTime.UtcNow)).Appeal!.ID;

		Assert.Equal(400, Read(await StaffSide(probe, new DecideAppealRequest { Decision = "uphold", Reason = "ok" }, Permissions.AppealsReview).Decide(appeal)).Status);
		Assert.Equal("open", (await Database.Appeals.Get(db, appeal))!.Status);
	}

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task AnAppealIsPurgedWithItsSanction(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		await using AppDbContext db = probe.Open();
		DateTime now = DateTime.UtcNow;
		Sanction ancient = await Add(db, Player, SanctionTypes.Ban, expires: now.AddYears(-3), created: now.AddYears(-4));
		Sanction recent = await Add(db, Player, SanctionTypes.Ban);
		db.Appeals.Add(new Appeal { SanctionID = ancient.ID, UserID = Player, Text = "old", CreatedAt = now.AddYears(-4) });
		db.Appeals.Add(new Appeal { SanctionID = recent.ID, UserID = Player, Text = "new", CreatedAt = now });
		await db.SaveChangesAsync();
		db.ChangeTracker.Clear();

		await Database.Moderation.PurgeExpired(db, now);

		Appeal kept = Assert.Single(await db.Appeals.ToListAsync());
		Assert.Equal(recent.ID, kept.SanctionID);
	}
}
