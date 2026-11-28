using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace GenOnlineService.Tests;

public class AdminDeletePermissionsMigrationTests
{
	private static StoredRole Role(string name, int position) =>
		new() { Name = name, DisplayName = name, Position = position, BuiltIn = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, UpdatedBy = "system" };

	private static Task<List<string>> PermissionsOf(AppDbContext db, string role) =>
		db.Set<StoredRolePermission>().AsNoTracking().Where(p => p.Role == role).Select(p => p.Permission).OrderBy(p => p).ToListAsync();

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task OnlyTheStoredAdminRoleGainsTheTwoDeletePermissions(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider, migrate: false);
		if (probe == null) return;
		await using (AppDbContext db = probe.Open())
		{
			await db.GetService<IMigrator>().MigrateAsync("PartnerClients");
			db.Set<StoredRole>().AddRange(Role("admin", 90), Role("moderator", 50));
			db.Set<StoredRolePermission>().AddRange(
				new StoredRolePermission { Role = "admin", Permission = "roles.manage" },
				new StoredRolePermission { Role = "admin", Permission = "rules.delete" },
				new StoredRolePermission { Role = "moderator", Permission = "moderation.mute" });
			await db.SaveChangesAsync();
		}

		await using (AppDbContext db = probe.Open())
		{
			await db.GetService<IMigrator>().MigrateAsync("AdminDeletePermissions");
		}

		await using (AppDbContext db = probe.Open())
		{
			Assert.Equal(["roles.manage", "rules.delete", "sanctions.delete"], await PermissionsOf(db, "admin"));
			Assert.Equal(["moderation.mute"], await PermissionsOf(db, "moderator"));
			Assert.DoesNotContain("partners.delete", await PermissionsOf(db, "admin"));
		}
	}

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task ADatabaseWithoutAStoredAdminRoleIsLeftAlone(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		await using AppDbContext db = probe.Open();
		Assert.Empty(await db.Set<StoredRolePermission>().AsNoTracking().ToListAsync());
	}
}
