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

using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Xunit;

namespace GenOnlineService.Tests;

[Collection(AdminAreaCollection.Name)]
public class StaffRolesCookieTests
{
	private static readonly object s_empty = new { };

	private static async Task<(AdminBrowser Browser, User User)> StaffAsync(ProbeDatabase probe, string name, string role)
	{
		var browser = new AdminBrowser(probe);
		string email = name.ToLowerInvariant() + "@example.org";
		await browser.SignUp(name, email);
		return (browser, await AdminArea.Grant(probe, email, role));
	}

	private static async Task<User> PlayerAsync(ProbeDatabase probe, string name)
	{
		var browser = new AdminBrowser(probe);
		string email = name.ToLowerInvariant() + "@example.org";
		await browser.SignUp(name, email);
		return (await PasswordPagesTests.UserByEmail(probe, email))!;
	}

	private static async Task<List<UserRole>> RolesOf(ProbeDatabase probe, long userID)
	{
		await using AppDbContext db = probe.Open();
		return await db.UserRoles.AsNoTracking().Where(r => r.UserID == userID).ToListAsync();
	}

	private static async Task<List<AuditLogEntry>> RoleAudit(ProbeDatabase probe, long target)
	{
		await using AppDbContext db = probe.Open();
		return await db.AuditLog.AsNoTracking()
			.Where(e => e.TargetKind == AuditTargetKinds.User && e.TargetID == target && (e.Action == ModerationActions.RoleGrant || e.Action == ModerationActions.RoleRevoke))
			.OrderBy(e => e.ID)
			.ToListAsync();
	}

	private static async Task AddUpstream(ProbeDatabase probe, long userID, string role)
	{
		await using AppDbContext db = probe.Open();
		db.UserRoles.Add(new UserRole { UserID = userID, Role = role, Source = AuthoritySources.Upstream("discord"), GrantedAt = DateTime.UtcNow, GrantedBy = "discord" });
		await db.SaveChangesAsync();
	}

	private static async Task<Dictionary<string, string>> ServiceToken(ProbeDatabase probe, params string[] permissions)
	{
		string token = ApiTokenFormat.Generate();
		await using AppDbContext db = probe.Open();
		var account = new ServiceAccount { Name = "operator", CreatedBy = "test", CreatedAt = DateTime.UtcNow };
		db.ServiceAccounts.Add(account);
		await db.SaveChangesAsync();
		foreach (string permission in permissions)
		{
			db.ServiceAccountPermissions.Add(new ServiceAccountPermission { ServiceAccountID = account.ID, Permission = permission, GrantedBy = "test", GrantedAt = DateTime.UtcNow });
		}
		db.ApiTokens.Add(new ApiToken
		{
			OwnerServiceID = account.ID,
			Name = "operator",
			TokenHash = ApiTokenFormat.Hash(token),
			TokenLast4 = ApiTokenFormat.Last4(token),
			Scopes = String.Join(" ", permissions),
			CreatedBy = "test",
			CreatedAt = DateTime.UtcNow,
			ExpiresAt = DateTime.UtcNow.AddDays(1)
		});
		await db.SaveChangesAsync();
		await ApiTokenCache.Reload(db);
		return new Dictionary<string, string> { ["Authorization"] = "Bearer " + token };
	}

	private static List<string> Strings(JsonElement array) => array.EnumerateArray().Select(e => e.GetString()!).ToList();

	// The owner role (L69): rank 3 over admin, admin's permissions, granted with the CLI only, never by a provider
	[Fact]
	public void AnOwnerHoldsEveryAdminPermissionAndTheTopRank()
	{
		Assert.True(StaffRoles.IsKnown(StaffRoles.Owner));
		Assert.True(StaffRoles.IsStaff("owner"));
		Assert.Equal(StaffRoles.PermissionsFor([StaffRoles.Admin]).Order(), StaffRoles.PermissionsFor(["owner"]).Order());
		Assert.Equal(StaffRoles.OwnerRank, StaffRoles.RankOf(["owner"]));
		Assert.Equal((StaffRoles.AdminRank, RoleCatalog.ModeratorPosition, 0), (StaffRoles.RankOf([StaffRoles.Admin]), StaffRoles.RankOf([StaffRoles.Moderator]), StaffRoles.RankOf([StaffRoles.Caster])));
		Assert.Equal(ClientFeatures.Defaults[StaffRoles.Admin], ClientFeatures.Defaults["owner"]);
		Assert.Equal((IReadOnlyList<string>?)["owner"], Rest.RoleRules.Normalize(["Owner"]).Roles);
	}
}
