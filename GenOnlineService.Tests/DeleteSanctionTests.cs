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
using GenOnlineService.OpenApi;
using GenOnlineService.Realtime;
using GenOnlineService.Rest;
using GenOnlineService.Service;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using System.Text.Json;
using Xunit;

namespace GenOnlineService.Tests;

public class DeletePermissionTests
{
	[Fact]
	public void OwnerAndAdminHoldBothDeletePermissionsAndModeratorNeither()
	{
		RoleCatalog catalog = RoleCatalog.Defaults;

		foreach (string role in new[] { RoleCatalog.Owner, RoleCatalog.Admin })
		{
			Assert.Contains(Permissions.SanctionsDelete, catalog.Roles[role].Permissions);
			Assert.Contains(Permissions.RulesDelete, catalog.Roles[role].Permissions);
		}
		Assert.DoesNotContain(Permissions.SanctionsDelete, catalog.Roles[RoleCatalog.Moderator].Permissions);
		Assert.DoesNotContain(Permissions.RulesDelete, catalog.Roles[RoleCatalog.Moderator].Permissions);
		Assert.DoesNotContain(Permissions.SanctionsDelete, catalog.EveryoneRole.Permissions);
		Assert.DoesNotContain(Permissions.RulesDelete, catalog.EveryoneRole.Permissions);
	}

	[Fact]
	public void TheRoleEditorListsBothWithALabelAndADescription()
	{
		foreach (string name in new[] { "sanctions.delete", "rules.delete" })
		{
			var entry = PermissionCatalog.Entries.Single(e => e.Name == name);
			Assert.Equal("Moderation", entry.Area);
			Assert.False(String.IsNullOrWhiteSpace(entry.Label));
			Assert.False(String.IsNullOrWhiteSpace(entry.Description));
			Assert.Contains(name, Permissions.Human);
		}
	}

	[Fact]
	public void TheDeleteRouteNeedsItsPermissionAndTheLiftRouteIsUnchanged()
	{
		MethodInfo sanction = typeof(Api.ModerationController).GetMethod(nameof(Api.ModerationController.DeleteSanction))!;
		Assert.Equal([Permissions.SanctionsDelete], sanction.GetCustomAttribute<ApiPermissionAttribute>()!.AnyOf);
		Assert.Equal("~/api/v1/sanctions/{sanctionID:long}/record", sanction.GetCustomAttribute<Microsoft.AspNetCore.Mvc.HttpDeleteAttribute>()!.Template);

		MethodInfo lift = typeof(Api.ModerationController).GetMethod(nameof(Api.ModerationController.LiftSanction))!;
		Assert.Equal("~/api/v1/sanctions/{sanctionID:long}", lift.GetCustomAttribute<Microsoft.AspNetCore.Mvc.HttpDeleteAttribute>()!.Template);
	}
}

[Collection(AdminAreaCollection.Name)]
public class DeleteRecordProbeTests
{
	private const string SharedKey = "evidence/20261101/1/7/shared.png";
	private const string OwnKey = "evidence/20261101/1/7/own.png";

	private sealed class OneContextFactory(ProbeDatabase probe) : IDbContextFactory<AppDbContext>
	{
		public AppDbContext CreateDbContext() => probe.Open();
	}

	private sealed class RecordingStore : IMatchDataStore
	{
		public List<string> Deleted { get; } = [];

		public Task<IReadOnlyList<string>> ListAsync(string prefix, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<string>>([]);

		public Task<StoredObjectInfo?> HeadAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult<StoredObjectInfo?>(null);

		public Task<StoredObject?> GetAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult<StoredObject?>(null);

		public Task PutAsync(string key, StoredObject value, CancellationToken cancellationToken = default) => Task.CompletedTask;

		public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
		{
			Deleted.Add(key);
			return Task.CompletedTask;
		}
	}

	private static Func<string, Task<Failure?>> Granting(params string[] permissions) =>
		permission => Task.FromResult<Failure?>(permissions.Contains(permission) ? null : Result.Forbidden(permission));

	private static async Task<long> AddUser(AppDbContext db, string name, int tag)
	{
		var user = new User { Username = name, UsernameNormalized = NameNormalizer.Normalize(name), NameTag = tag, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
		db.Users.Add(user);
		await db.SaveChangesAsync();
		return user.ID;
	}

	private static Database.ModerationActor Staff(long id) => SanctionActions.ApiActorOf(id);

	private static async Task<(long Admin, long Player)> Seed(AppDbContext db)
	{
		long admin = await AddUser(db, "Chief", 1001);
		long player = await AddUser(db, "Player", 1002);
		db.UserRoles.Add(new UserRole { UserID = admin, Role = StaffRoles.Admin, GrantedAt = DateTime.UtcNow });
		await db.SaveChangesAsync();
		await StaffRoles.Reload(db);
		return (admin, player);
	}

	private static async Task<Sanction> AddSanction(AppDbContext db, long user, string type, DateTime? expires)
	{
		var sanction = new Sanction { UserID = user, Type = type, Reason = "toxic chat", CreatedByKind = AuditActorKinds.User, CreatedAt = DateTime.UtcNow.AddDays(-3), ExpiresAt = expires };
		db.Sanctions.Add(sanction);
		await db.SaveChangesAsync();
		db.ChangeTracker.Clear();
		return sanction;
	}

	// The process-wide rank cache (StaffRoles.Reload) is shared, so these run one at a time
	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task DeletingAnActiveBanEndsItAndRemovesEverythingThatExistedOnlyForIt(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		await using AppDbContext db = probe.Open();
		(long admin, long player) = await Seed(db);
		Sanction ban = await AddSanction(db, player, SanctionTypes.Ban, null);
		Sanction other = await AddSanction(db, player, SanctionTypes.Mute, DateTime.UtcNow.AddDays(1));

		await Database.Appeals.Open(db, player, ban.ID, "please", DateTime.UtcNow);
		await Database.Appeals.Open(db, player, other.ID, "please", DateTime.UtcNow);
		db.ChangeTracker.Clear();
		db.SanctionEvidence.AddRange(
			new SanctionEvidence { SanctionID = ban.ID, UserID = player, MatchID = 1, ObjectKey = SharedKey, CreatedAt = DateTime.UtcNow },
			new SanctionEvidence { SanctionID = ban.ID, UserID = player, MatchID = 1, ObjectKey = OwnKey, CreatedAt = DateTime.UtcNow },
			new SanctionEvidence { SanctionID = other.ID, UserID = player, MatchID = 1, ObjectKey = SharedKey, CreatedAt = DateTime.UtcNow });
		byte[] hash = new byte[32];
		db.IdentifierHashes.AddRange(
			new IdentifierHash { UserID = player, Kind = HashedIdentifierKinds.Email, ValueHmac = hash, SanctionID = ban.ID, CreatedAt = DateTime.UtcNow },
			new IdentifierHash { UserID = player, Kind = HashedIdentifierKinds.Provider("discord"), ValueHmac = hash, SanctionID = other.ID, CreatedAt = DateTime.UtcNow });
		db.ModerationCases.Add(new ModerationCase { UserID = player, Status = ModerationCaseStatuses.Closed, OpenedAt = DateTime.UtcNow, SanctionID = ban.ID });
		await db.SaveChangesAsync();
		db.ChangeTracker.Clear();

		var store = new RecordingStore();
		Result<Empty> deleted = await SanctionActions.DeleteAsync(new OneContextFactory(probe), Staff(admin), ban.ID, "wrong player", store, Granting(Permissions.SanctionsDelete));

		Assert.True(deleted.Ok, deleted.Code);
		Assert.Null(await db.Sanctions.AsNoTracking().FirstOrDefaultAsync(s => s.ID == ban.ID));
		Assert.NotNull(await db.Sanctions.AsNoTracking().FirstOrDefaultAsync(s => s.ID == other.ID));
		Assert.Empty(await db.Appeals.AsNoTracking().Where(a => a.SanctionID == ban.ID).ToListAsync());
		Assert.Single(await db.Appeals.AsNoTracking().Where(a => a.SanctionID == other.ID).ToListAsync());
		Assert.Empty(await db.SanctionEvidence.AsNoTracking().Where(e => e.SanctionID == ban.ID).ToListAsync());
		Assert.Single(await db.SanctionEvidence.AsNoTracking().Where(e => e.SanctionID == other.ID).ToListAsync());
		Assert.Equal([OwnKey], store.Deleted);
		Assert.Empty(await db.IdentifierHashes.AsNoTracking().Where(h => h.SanctionID == ban.ID).ToListAsync());
		Assert.Single(await db.IdentifierHashes.AsNoTracking().Where(h => h.SanctionID == other.ID).ToListAsync());
		ModerationCase unlinked = await db.ModerationCases.AsNoTracking().SingleAsync(c => c.UserID == player);
		Assert.Null(unlinked.SanctionID);

		Assert.Equal(1, await db.AuditLog.CountAsync(e => e.Action == ModerationActions.Unban && e.TargetID == player));
		AuditLogEntry entry = await db.AuditLog.AsNoTracking().SingleAsync(e => e.Action == ModerationActions.SanctionDelete);
		Assert.Equal(player, entry.TargetID);
		Assert.Equal(admin, entry.ActorID);
		Assert.Equal("wrong player", entry.Reason);
		JsonElement details = JsonDocument.Parse(entry.Details!).RootElement;
		Assert.Equal(SanctionTypes.Ban, details.GetProperty("type").GetString());
		Assert.Equal(ban.ID, details.GetProperty("sanction_id").GetInt64());
		Assert.Equal("toxic chat", details.GetProperty("original_reason").GetString());
		Assert.True(details.GetProperty("appeal").GetBoolean());
		Assert.Equal(2, details.GetProperty("evidence").GetInt32());
		Assert.Equal(1, details.GetProperty("identifier_hashes").GetInt32());
		Assert.Equal(1, details.GetProperty("cases_unlinked").GetInt32());
	}

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task ADeleteWithoutThePermissionOrAReasonRemovesNothing(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		await using AppDbContext db = probe.Open();
		(long admin, long player) = await Seed(db);
		Sanction mute = await AddSanction(db, player, SanctionTypes.Mute, DateTime.UtcNow.AddDays(1));
		var factory = new OneContextFactory(probe);

		Result<Empty> denied = await SanctionActions.DeleteAsync(factory, Staff(admin), mute.ID, "reason", null, Granting(Permissions.ModerationMute, Permissions.ModerationBan));
		Assert.False(denied.Ok);
		Assert.Equal(ErrorCodes.Forbidden, denied.Code);

		Result<Empty> noReason = await SanctionActions.DeleteAsync(factory, Staff(admin), mute.ID, "  ", null, Granting(Permissions.SanctionsDelete));
		Assert.False(noReason.Ok);
		Assert.Equal(ErrorCodes.InvalidArgument, noReason.Code);

		Result<Empty> missing = await SanctionActions.DeleteAsync(factory, Staff(admin), mute.ID + 1000, "reason", null, Granting(Permissions.SanctionsDelete));
		Assert.False(missing.Ok);

		Sanction kept = await db.Sanctions.AsNoTracking().SingleAsync(s => s.ID == mute.ID);
		Assert.Null(kept.LiftedAt);
		Assert.Equal(0, await db.AuditLog.CountAsync(e => e.Action == ModerationActions.SanctionDelete || e.Action == ModerationActions.Unmute));
	}

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task EvidenceWithoutAStoreIsRefusedBeforeAnythingIsRemoved(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		await using AppDbContext db = probe.Open();
		(long admin, long player) = await Seed(db);
		Sanction kick = await AddSanction(db, player, SanctionTypes.Kick, DateTime.UtcNow);
		db.SanctionEvidence.Add(new SanctionEvidence { SanctionID = kick.ID, UserID = player, MatchID = 1, ObjectKey = OwnKey, CreatedAt = DateTime.UtcNow });
		await db.SaveChangesAsync();
		db.ChangeTracker.Clear();

		Result<Empty> refused = await SanctionActions.DeleteAsync(new OneContextFactory(probe), Staff(admin), kick.ID, "reason", null, Granting(Permissions.SanctionsDelete));

		Assert.False(refused.Ok);
		Assert.Equal(1, await db.Sanctions.CountAsync(s => s.ID == kick.ID));
		Assert.Equal(1, await db.SanctionEvidence.CountAsync(e => e.SanctionID == kick.ID));
	}

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task ARefusedDeleteLeavesAnActiveBanRunning(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		await using AppDbContext db = probe.Open();
		(long admin, long player) = await Seed(db);
		Sanction ban = await AddSanction(db, player, SanctionTypes.Ban, null);
		db.SanctionEvidence.Add(new SanctionEvidence { SanctionID = ban.ID, UserID = player, MatchID = 1, ObjectKey = OwnKey, CreatedAt = DateTime.UtcNow });
		await db.SaveChangesAsync();
		db.ChangeTracker.Clear();

		Result<Empty> refused = await SanctionActions.DeleteAsync(new OneContextFactory(probe), Staff(admin), ban.ID, "reason", null, Granting(Permissions.SanctionsDelete));

		Assert.False(refused.Ok);
		Sanction kept = await db.Sanctions.AsNoTracking().SingleAsync(s => s.ID == ban.ID);
		Assert.Null(kept.LiftedAt);
		Assert.Equal(0, await db.AuditLog.CountAsync());
	}
}
