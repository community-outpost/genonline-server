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

using GenOnlineService.Api;
using GenOnlineService.Identity;
using GenOnlineService.Realtime;
using GenOnlineService.Rest;
using GenOnlineService.Rules;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using System.Text.Json;
using Xunit;

namespace GenOnlineService.Tests;

[Collection(AdminAreaCollection.Name)]
public class DeleteRuleTests
{
	private sealed class OneContextFactory(ProbeDatabase probe) : IDbContextFactory<AppDbContext>
	{
		public AppDbContext CreateDbContext() => probe.Open();
	}

	private static Database.ModerationActor Staff(long id) => GenOnlineService.Realtime.SanctionActions.ApiActorOf(id);

	private static async Task<long> AddUser(AppDbContext db, string name, int tag)
	{
		var user = new User { Username = name, UsernameNormalized = NameNormalizer.Normalize(name), NameTag = tag, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
		db.Users.Add(user);
		await db.SaveChangesAsync();
		return user.ID;
	}

	[Fact]
	public void TheDeleteRouteNeedsItsPermission()
	{
		MethodInfo rule = typeof(RulesController).GetMethod(nameof(RulesController.Delete))!;
		Assert.Equal(PermissionPolicies.For(Permissions.RulesDelete), rule.GetCustomAttribute<AuthorizeAttribute>()!.Policy);
		Assert.Equal("~/api/v1/rules/{id:long}", rule.GetCustomAttribute<Microsoft.AspNetCore.Mvc.HttpDeleteAttribute>()!.Template);
	}

	private static async Task<long> AddRule(AppDbContext db, string name, int versions, int matches)
	{
		DateTime now = DateTime.UtcNow;
		var rule = new AutomatedRule { Name = name, Mode = RuleModes.Watch, CurrentVersion = versions, CreatedBy = "user:1", UpdatedBy = "user:1", CreatedAt = now, UpdatedAt = now };
		db.AutomatedRules.Add(rule);
		await db.SaveChangesAsync();
		for (int number = 1; number <= versions; number++)
		{
			db.AutomatedRuleVersions.Add(new AutomatedRuleVersion { RuleID = rule.ID, Number = number, Document = RuleDocuments.Serialize(Document()), CreatedBy = "user:1", CreatedAt = now });
		}
		for (int i = 0; i < matches; i++)
		{
			db.AutomatedRuleMatches.Add(new AutomatedRuleMatch { RuleID = rule.ID, RuleVersion = versions, UserID = 100 + i, Trigger = RuleTriggers.Sweep, MatchedAt = now });
		}
		await db.SaveChangesAsync();
		db.ChangeTracker.Clear();
		return rule.ID;
	}

	private static RuleDocument Document() => new()
	{
		Nodes =
		[
			new RuleNode { Id = "trigger", Kind = RuleNodeKinds.Trigger, Event = new RuleNodeEvent { Action = RuleTriggers.Sweep } },
			new RuleNode { Id = "action", Kind = RuleNodeKinds.Action, Action = RuleActionKinds.Flag, Reason = "test" }
		],
		Edges = [new RuleEdge { From = "trigger", To = "action" }]
	};

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task DeletingARuleRemovesItsVersionsAndMatchesAndWritesOneAuditRow(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		await using AppDbContext db = probe.Open();
		long doomed = await AddRule(db, "Doomed", versions: 3, matches: 4);
		long kept = await AddRule(db, "Kept", versions: 2, matches: 1);

		Result<Empty> deleted = await RuleStore.DeleteAsync(db, Staff(1), doomed);

		Assert.True(deleted.Ok, deleted.Code);
		Assert.Null(await RuleStore.GetAsync(db, doomed));
		Assert.Equal(0, await db.AutomatedRuleVersions.CountAsync(v => v.RuleID == doomed));
		Assert.Equal(0, await db.AutomatedRuleMatches.CountAsync(m => m.RuleID == doomed));
		Assert.NotNull(await RuleStore.GetAsync(db, kept));
		Assert.Equal(2, await db.AutomatedRuleVersions.CountAsync(v => v.RuleID == kept));
		Assert.Equal(1, await db.AutomatedRuleMatches.CountAsync(m => m.RuleID == kept));

		AuditLogEntry entry = await db.AuditLog.AsNoTracking().SingleAsync(e => e.Action == RuleAudit.Delete);
		Assert.Equal(doomed, entry.TargetID);
		Assert.Equal(RuleAudit.TargetKind, entry.TargetKind);
		JsonElement details = JsonDocument.Parse(entry.Details!).RootElement;
		Assert.Equal("Doomed", details.GetProperty("name").GetString());
		Assert.Equal(3, details.GetProperty("versions").GetInt32());
		Assert.Equal(4, details.GetProperty("matches").GetInt32());

		Assert.False((await RuleStore.DeleteAsync(db, Staff(1), doomed)).Ok);
	}

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task ARuleWithARunningSweepIsRefusedUntilTheSweepEnds(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		await using AppDbContext db = probe.Open();
		long id = await AddRule(db, "Sweeping", versions: 1, matches: 2);
		await AddUser(db, "Someone", 1003);

		var gate = new TaskCompletionSource();
		Func<bool> serving = RuleSweeps.Serving;
		Func<int, Task>? before = RuleSweeps.BeforeBatch;
		RuleSweeps.Serving = () => true;
		RuleSweeps.BeforeBatch = _ => gate.Task;
		try
		{
			var environment = new RuleEnvironment(new OneContextFactory(probe), null!, null!);
			Assert.True((await RuleSweeps.StartAsync(environment, db, Staff(1), id)).Ok);

			Result<Empty> refused = await RuleStore.DeleteAsync(db, Staff(1), id);
			Assert.False(refused.Ok);
			Assert.Equal(RestCodes.Conflict, refused.Code);
			Assert.NotNull(await RuleStore.GetAsync(db, id));
			Assert.Equal(2, await db.AutomatedRuleMatches.CountAsync(m => m.RuleID == id));

			gate.SetResult();
			await RuleSweeps.WaitAsync(id, TimeSpan.FromSeconds(10));
			Assert.True((await RuleStore.DeleteAsync(db, Staff(1), id)).Ok);
		}
		finally
		{
			gate.TrySetResult();
			RuleSweeps.Serving = serving;
			RuleSweeps.BeforeBatch = before;
		}
	}
}
