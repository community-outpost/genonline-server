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

using GenOnlineService.Realtime;
using GenOnlineService.Rest;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Xunit;

namespace GenOnlineService.Tests;

public class OwnerRoleRulesTests
{
	private static IReadOnlySet<string> Set(params string[] items) => new HashSet<string>(items, StringComparer.Ordinal);

	private static RoleDefinition Owner => RoleCatalog.Defaults.Roles[RoleCatalog.Owner];

	private static RoleChange Wanted(string displayName, IReadOnlySet<string> permissions) => new(RoleCatalog.Owner, displayName, "#112233", "Edited", Owner.Position, permissions, "BOSS");

	[Fact]
	public void AnOwnerRenamesAndRecoloursTheOwnerRoleAndDropsAPermissionThatIsNotProtected()
	{
		HashSet<string> kept = new(Permissions.Human, StringComparer.Ordinal);
		kept.Remove(Permissions.ServerStats);

		Assert.Null(RoleDefinitionRules.CheckUpdate(RoleCatalog.Defaults, RoleCatalog.OwnerPosition, Permissions.Human, Owner, Wanted("Founder", kept)));
	}

	[Fact]
	public void ASaveThatDropsAProtectedPermissionIsRefused()
	{
		HashSet<string> dropped = new(Permissions.Human, StringComparer.Ordinal);
		dropped.Remove(Permissions.RolesManage);

		Failure? failure = RoleDefinitionRules.CheckUpdate(RoleCatalog.Defaults, RoleCatalog.OwnerPosition, Permissions.Human, Owner, Wanted("Owner", dropped));

		Assert.Equal(RestCodes.RolePermissionProtected, failure?.Code);
		Assert.Contains(Permissions.RolesManage, JsonSerializer.Serialize(failure!.Value.Params));
	}

	[Fact]
	public void OnlyAnOwnerEditsTheOwnerRole()
	{
		Failure? failure = RoleDefinitionRules.CheckUpdate(RoleCatalog.Defaults, RoleCatalog.AdminPosition, Permissions.Human, Owner, Wanted("Owner", Permissions.Human));

		Assert.Equal(RestCodes.RoleRank, failure?.Code);
		Assert.False(RoleDefinitionRules.CanManage(RoleCatalog.AdminPosition, Owner));
		Assert.True(RoleDefinitionRules.CanManage(RoleCatalog.OwnerPosition, Owner));
	}

	[Fact]
	public void TheKeyAndTheRankOfTheOwnerRoleStayFixedAndItIsNeverDeleted()
	{
		RoleChange moved = Wanted("Owner", Permissions.Human) with { Position = Owner.Position - 1 };
		RoleChange renamed = Wanted("Owner", Permissions.Human) with { Name = "boss" };

		Assert.Equal("invalid_argument", RoleDefinitionRules.CheckUpdate(RoleCatalog.Defaults, RoleCatalog.OwnerPosition, Permissions.Human, Owner, moved)?.Code);
		Assert.Equal("invalid_argument", RoleDefinitionRules.CheckUpdate(RoleCatalog.Defaults, RoleCatalog.OwnerPosition, Permissions.Human, Owner, renamed)?.Code);
		Assert.Equal(RestCodes.RoleLocked, RoleDefinitionRules.CheckDelete(RoleCatalog.OwnerPosition, Owner)?.Code);
	}

	[Fact]
	public void AnOwnerRoleStoredWithoutTheProtectedPermissionStillHoldsIt()
	{
		RoleCatalog catalog = RoleCatalog.From([Owner with { Permissions = Set(Permissions.ServerStats) }]);

		Assert.Equal([Permissions.RolesManage, Permissions.ServerStats], catalog.Roles[RoleCatalog.Owner].Permissions.Order(StringComparer.Ordinal));
	}
}

[Collection(AdminAreaCollection.Name)]
public class OwnerRoleProbeTests
{
	private static async Task<(AdminBrowser Browser, Dictionary<string, string> Csrf)> SignedIn(ProbeDatabase probe, string name, string role)
	{
		var browser = new AdminBrowser(probe);
		await browser.SignUp(name, name.ToLowerInvariant() + "@example.org");
		await AdminArea.Grant(probe, name.ToLowerInvariant() + "@example.org", role);
		await using (AppDbContext db = probe.Open())
		{
			await Database.StoredRoles.EnsureSeedAsync(db, DateTime.UtcNow);
			await StaffRoles.Reload(db);
		}
		return (browser, await browser.Csrf());
	}

	private static async Task<List<AuditLogEntry>> RoleAudit(ProbeDatabase probe)
	{
		await using AppDbContext db = probe.Open();
		return await db.AuditLog.AsNoTracking().Where(e => e.Action == ModerationActions.RoleUpdate).OrderBy(e => e.ID).ToListAsync();
	}

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task ProbeAnOwnerRenamesRecoloursAndTrimsTheOwnerRoleWithAnAuditRow(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		(AdminBrowser owner, Dictionary<string, string> csrf) = await SignedIn(probe, "Founder", StaffRoles.Owner);
		List<string> trimmed = Permissions.Human.Where(p => p != Permissions.ServerStats).Order(StringComparer.Ordinal).ToList();

		PageResponse saved = await owner.Patch("/api/v1/roles/owner", new { display_name = "Founder", color = "#112233", tag = "BOSS", description = "Runs the service.", permissions = trimmed }, csrf);

		Assert.Equal(200, saved.Status);
		await using AppDbContext db = probe.Open();
		StoredRole row = await db.StoredRoles.AsNoTracking().SingleAsync(r => r.Name == "owner");
		Assert.Equal(("Founder", "#112233", "BOSS", "Runs the service."), (row.DisplayName, row.Color, row.Tag, row.Description));
		Assert.Equal(RoleCatalog.OwnerPosition, row.Position);
		Assert.True(row.BuiltIn);
		Assert.DoesNotContain(Permissions.ServerStats, await db.StoredRolePermissions.AsNoTracking().Where(p => p.Role == "owner").Select(p => p.Permission).ToListAsync());

		AuditLogEntry audit = Assert.Single(await RoleAudit(probe));
		JsonElement details = JsonDocument.Parse(audit.Details!).RootElement;
		Assert.Equal("owner", details.GetProperty("role").GetString());
		Assert.Equal(Permissions.ServerStats, details.GetProperty("removed")[0].GetString());
		Assert.Equal("Founder", details.GetProperty("display_name").GetString());
		Assert.Contains(Permissions.RolesManage, StaffRoles.PermissionsFor([StaffRoles.Owner]));
		Assert.DoesNotContain(Permissions.ServerStats, StaffRoles.PermissionsFor([StaffRoles.Owner]));
	}

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task ProbeASaveThatDropsRolesManageIsRefusedAndChangesNothing(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		(AdminBrowser owner, Dictionary<string, string> csrf) = await SignedIn(probe, "Founder", StaffRoles.Owner);
		List<string> without = Permissions.Human.Where(p => p != Permissions.RolesManage).Order(StringComparer.Ordinal).ToList();

		PageResponse refused = await owner.Patch("/api/v1/roles/owner", new { display_name = "Nope", permissions = without }, csrf);

		Assert.Equal(403, refused.Status);
		Assert.Equal(RestCodes.RolePermissionProtected, AdminArea.ErrorOf(refused));
		Assert.Empty(await RoleAudit(probe));
		await using AppDbContext db = probe.Open();
		Assert.Equal("Owner", (await db.StoredRoles.AsNoTracking().SingleAsync(r => r.Name == "owner")).DisplayName);
	}

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task ProbeAnAdminCannotEditTheOwnerRole(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		(AdminBrowser admin, Dictionary<string, string> csrf) = await SignedIn(probe, "Chief", StaffRoles.Admin);

		PageResponse refused = await admin.Patch("/api/v1/roles/owner", new { display_name = "Mine" }, csrf);

		Assert.Equal(403, refused.Status);
		Assert.Equal(RestCodes.RoleRank, AdminArea.ErrorOf(refused));
		Assert.Empty(await RoleAudit(probe));
	}

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task ProbeAnOwnerRoleNobodyEditedGainsANewCodePermissionOnSeedAndAnEditedOneDoesNot(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		await Seed(probe);
		await Mutate(probe, db => db.StoredRolePermissions.Where(p => p.Role == "owner" && p.Permission == Permissions.ServerStats).ExecuteDeleteAsync());

		await Seed(probe);
		Assert.Contains(Permissions.ServerStats, await OwnerPermissions(probe));

		await Mutate(probe, db => db.StoredRoles.Where(r => r.Name == "owner").ExecuteUpdateAsync(s => s.SetProperty(r => r.UpdatedBy, "user:1")));
		await Mutate(probe, db => db.StoredRolePermissions.Where(p => p.Role == "owner" && p.Permission == Permissions.ServerStats).ExecuteDeleteAsync());
		await Seed(probe);
		Assert.DoesNotContain(Permissions.ServerStats, await OwnerPermissions(probe));
	}

	private static async Task Seed(ProbeDatabase probe)
	{
		await using AppDbContext db = probe.Open();
		await Database.StoredRoles.EnsureSeedAsync(db, DateTime.UtcNow);
	}

	private static async Task Mutate(ProbeDatabase probe, Func<AppDbContext, Task> change)
	{
		await using AppDbContext db = probe.Open();
		await change(db);
	}

	private static async Task<List<string>> OwnerPermissions(ProbeDatabase probe)
	{
		await using AppDbContext db = probe.Open();
		return await db.StoredRolePermissions.AsNoTracking().Where(p => p.Role == "owner").Select(p => p.Permission).ToListAsync();
	}
}
