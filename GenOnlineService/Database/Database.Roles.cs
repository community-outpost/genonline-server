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

using GenOnlineService;
using GenOnlineService.Rest;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class StoredRole
{
	// The key, lowercase and fixed once created because user_roles refers to it by name
	public string Name { get; set; } = String.Empty;
	public string DisplayName { get; set; } = String.Empty;

	public string? Color { get; set; }
	public string? Description { get; set; }

	// A higher position outranks a lower one, and no two roles share one
	public int Position { get; set; }

	public bool BuiltIn { get; set; }

	public DateTime CreatedAt { get; set; } = DateTime.UnixEpoch;
	public DateTime UpdatedAt { get; set; } = DateTime.UnixEpoch;

	public string? UpdatedBy { get; set; }
}

public class StoredRolePermission
{
	public string Role { get; set; } = String.Empty;
	public string Permission { get; set; } = String.Empty;
}

public class StoredRoleConfiguration : IEntityTypeConfiguration<StoredRole>
{
	public void Configure(EntityTypeBuilder<StoredRole> builder)
	{
		builder.ToTable("roles");

		builder.HasKey(e => e.Name);
		builder.HasIndex(e => e.Position).IsUnique().HasDatabaseName("ux_roles_position");

		builder.Property(e => e.Name).HasColumnName("name").HasMaxLength(RoleCatalog.NameMaxLength);
		builder.Property(e => e.DisplayName).HasColumnName("display_name").HasMaxLength(RoleCatalog.DisplayNameMaxLength);
		builder.Property(e => e.Color).HasColumnName("color").HasMaxLength(7);
		builder.Property(e => e.Description).HasColumnName("description").HasMaxLength(RoleCatalog.DescriptionMaxLength);
		builder.Property(e => e.Position).HasColumnName("position");
		builder.Property(e => e.BuiltIn).HasColumnName("built_in");
		builder.Property(e => e.CreatedAt).HasColumnName("created_at");
		builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
		builder.Property(e => e.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);
	}
}

public class StoredRolePermissionConfiguration : IEntityTypeConfiguration<StoredRolePermission>
{
	public void Configure(EntityTypeBuilder<StoredRolePermission> builder)
	{
		builder.ToTable("role_permissions");

		builder.HasKey(e => new { e.Role, e.Permission });
		builder.HasOne<StoredRole>().WithMany().HasForeignKey(e => e.Role).OnDelete(DeleteBehavior.Cascade);

		builder.Property(e => e.Role).HasColumnName("role").HasMaxLength(RoleCatalog.NameMaxLength);
		builder.Property(e => e.Permission).HasColumnName("permission").HasMaxLength(64);
	}
}

namespace Database
{
	public static class StoredRoles
	{
		// The roles as the checks read them, the built-in defaults while the tables are empty
		public static async Task<RoleCatalog> LoadCatalogAsync(AppDbContext db, CancellationToken cancellationToken = default)
		{
			List<StoredRole> roles = await db.StoredRoles.AsNoTracking().ToListAsync(cancellationToken);
			if (roles.Count == 0)
			{
				return RoleCatalog.Defaults;
			}

			List<StoredRolePermission> permissions = await db.StoredRolePermissions.AsNoTracking().ToListAsync(cancellationToken);
			ILookup<string, string> byRole = permissions.ToLookup(p => p.Role, p => p.Permission, StringComparer.Ordinal);
			return RoleCatalog.From(roles.Select(r => new RoleDefinition(r.Name, r.DisplayName, r.Color, r.Description, r.Position, r.BuiltIn,
				new HashSet<string>(byRole[r.Name], StringComparer.Ordinal))));
		}

		public static async Task<Dictionary<string, int>> MemberCountsAsync(AppDbContext db, CancellationToken cancellationToken = default)
		{
			var counts = await db.UserRoles.AsNoTracking().GroupBy(r => r.Role).Select(g => new { Role = g.Key, Count = g.Select(r => r.UserID).Distinct().Count() }).ToListAsync(cancellationToken);
			return counts.ToDictionary(c => c.Role, c => c.Count, StringComparer.Ordinal);
		}

		public static async Task<bool> CreateAsync(AppDbContext db, RoleChange role, Database.ModerationActor actor, DateTime now, CancellationToken cancellationToken = default)
		{
			db.StoredRoles.Add(new StoredRole
			{
				Name = role.Name,
				DisplayName = role.DisplayName,
				Color = role.Color,
				Description = role.Description,
				Position = role.Position,
				BuiltIn = false,
				CreatedAt = now,
				UpdatedAt = now,
				UpdatedBy = actor.Name
			});
			db.StoredRolePermissions.AddRange(role.Permissions.Select(p => new StoredRolePermission { Role = role.Name, Permission = p }));
			db.AuditLog.Add(Database.Moderation.Entry(actor, ModerationActions.RoleCreate, AuditTargetKinds.Role, null, null,
				new { role = role.Name, display_name = role.DisplayName, position = role.Position, permissions = role.Permissions.Order(StringComparer.Ordinal).ToArray() }, now));
			return await SaveOrConflictAsync(db, cancellationToken);
		}

		// Writes the changed fields and the permission difference with one audit row; false when the position was taken meanwhile
		public static async Task<bool> UpdateAsync(AppDbContext db, RoleDefinition before, RoleChange wanted, Database.ModerationActor actor, DateTime now, CancellationToken cancellationToken = default)
		{
			StoredRole? row = await db.StoredRoles.AsTracking().FirstOrDefaultAsync(r => r.Name == before.Name, cancellationToken);
			if (row == null)
			{
				return false;
			}

			List<StoredRolePermission> current = await db.StoredRolePermissions.AsTracking().Where(p => p.Role == before.Name).ToListAsync(cancellationToken);
			string[] added = wanted.Permissions.Except(before.Permissions).Order(StringComparer.Ordinal).ToArray();
			string[] removed = before.Permissions.Except(wanted.Permissions).Order(StringComparer.Ordinal).ToArray();

			db.StoredRolePermissions.RemoveRange(current.Where(p => removed.Contains(p.Permission) || !Permissions.Human.Contains(p.Permission)));
			db.StoredRolePermissions.AddRange(added.Select(p => new StoredRolePermission { Role = before.Name, Permission = p }));

			row.DisplayName = wanted.DisplayName;
			row.Color = wanted.Color;
			row.Description = wanted.Description;
			row.Position = wanted.Position;
			row.UpdatedAt = now;
			row.UpdatedBy = actor.Name;

			db.AuditLog.Add(Database.Moderation.Entry(actor, ModerationActions.RoleUpdate, AuditTargetKinds.Role, null, null, new
			{
				role = before.Name,
				display_name = wanted.DisplayName != before.DisplayName ? wanted.DisplayName : null,
				color_changed = wanted.Color != before.Color,
				description_changed = wanted.Description != before.Description,
				position = wanted.Position != before.Position ? wanted.Position : (int?)null,
				added,
				removed
			}, now));
			return await SaveOrConflictAsync(db, cancellationToken);
		}

		// Takes the role away from everyone who held it, then removes the role with its permissions; returns how many people held it
		public static async Task<int> DeleteAsync(AppDbContext db, RoleDefinition role, Database.ModerationActor actor, DateTime now, CancellationToken cancellationToken = default)
		{
			// one transaction, so a failure never leaves the members stripped from a role that still exists
			await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

			int members = await db.UserRoles.Where(r => r.Role == role.Name).Select(r => r.UserID).Distinct().CountAsync(cancellationToken);
			await db.UserRoles.Where(r => r.Role == role.Name).ExecuteDeleteAsync(cancellationToken);
			await db.StoredRoles.Where(r => r.Name == role.Name).ExecuteDeleteAsync(cancellationToken);

			db.AuditLog.Add(Database.Moderation.Entry(actor, ModerationActions.RoleDelete, AuditTargetKinds.Role, null, null,
				new { role = role.Name, members_removed = members, permissions = role.Permissions.Order(StringComparer.Ordinal).ToArray() }, now));
			await db.SaveChangesAsync(cancellationToken);
			await transaction.CommitAsync(cancellationToken);
			return members;
		}

		private static async Task<bool> SaveOrConflictAsync(AppDbContext db, CancellationToken cancellationToken)
		{
			try
			{
				await db.SaveChangesAsync(cancellationToken);
				return true;
			}
			catch (DbUpdateException ex) when (DbHelpers.IsUniqueViolation(ex))
			{
				db.ChangeTracker.Clear();
				return false;
			}
		}

		// Adds the built-in roles that are missing and never changes one that exists, so staff edits survive a restart
		public static async Task EnsureSeedAsync(AppDbContext db, DateTime now, CancellationToken cancellationToken = default)
		{
			HashSet<string> present = (await db.StoredRoles.AsNoTracking().Select(r => r.Name).ToListAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);

			foreach (RoleDefinition builtIn in RoleCatalog.BuiltInDefinitions.Where(b => !present.Contains(b.Name)))
			{
				db.StoredRoles.Add(new StoredRole
				{
					Name = builtIn.Name,
					DisplayName = builtIn.DisplayName,
					Color = builtIn.Color,
					Description = builtIn.Description,
					Position = builtIn.Position,
					BuiltIn = true,
					CreatedAt = now,
					UpdatedAt = now,
					UpdatedBy = "system"
				});
				db.StoredRolePermissions.AddRange(builtIn.Permissions.Select(p => new StoredRolePermission { Role = builtIn.Name, Permission = p }));
			}

			try
			{
				await db.SaveChangesAsync(cancellationToken);
			}
			catch (DbUpdateException ex) when (DbHelpers.IsUniqueViolation(ex))
			{
				// another copy seeded at the same moment
				db.ChangeTracker.Clear();
			}
		}
	}
}
