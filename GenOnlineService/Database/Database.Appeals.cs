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
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public static class AppealStatuses
{
	public const string Open = "open";
	public const string Upheld = "upheld";
	public const string Lifted = "lifted";
	public const string Shortened = "shortened";
}

// One appeal per sanction, final once decided, purged with the sanction (two years after it ended)
public class Appeal
{
	public Int64 ID { get; set; }
	public Int64 SanctionID { get; set; }
	public Int64 UserID { get; set; }

	public string Text { get; set; } = String.Empty;

	public DateTime CreatedAt { get; set; } = DateTime.UnixEpoch;
	public string Status { get; set; } = AppealStatuses.Open;
	public DateTime? DecidedAt { get; set; }
	public Int64? DecidedByID { get; set; }

	public string? DecisionReason { get; set; }
}

public class AppealConfiguration : IEntityTypeConfiguration<Appeal>
{
	public void Configure(EntityTypeBuilder<Appeal> builder)
	{
		builder.ToTable("appeals");

		builder.HasKey(e => e.ID);
		builder.HasIndex(e => e.SanctionID).IsUnique().HasDatabaseName("ux_appeals_sanction");
		builder.HasIndex(e => new { e.UserID, e.CreatedAt }).HasDatabaseName("ix_appeals_user_created");
		builder.HasIndex(e => new { e.Status, e.CreatedAt }).HasDatabaseName("ix_appeals_status_created");

		builder.Property(e => e.ID).HasColumnName("id").ValueGeneratedOnAdd();
		builder.Property(e => e.SanctionID).HasColumnName("sanction_id");
		builder.Property(e => e.UserID).HasColumnName("user_id");
		builder.Property(e => e.Text).HasColumnName("text").HasMaxLength(Database.Appeals.MaxTextLength);
		builder.Property(e => e.CreatedAt).HasColumnName("created_at");
		builder.Property(e => e.Status).HasColumnName("status").HasMaxLength(16);
		builder.Property(e => e.DecidedAt).HasColumnName("decided_at");
		builder.Property(e => e.DecidedByID).HasColumnName("decided_by_id");
		builder.Property(e => e.DecisionReason).HasColumnName("decision_reason").HasMaxLength(ModerationManager.MaximumReasonLength);
	}
}

public enum AppealOutcome
{
	Done,
	SanctionNotFound,
	// A kick, a sanction of a sign-in provider, or one that ended more than 30 days ago
	NotAppealable,
	AlreadyAppealed,
	AppealNotFound,
	AlreadyDecided,
	// shorten needs a time in the future that is earlier than the current end
	InvalidExpiry
}

namespace Database
{
	public static class Appeals
	{
		public const int MaxTextLength = 2000;

		// A player may appeal while the sanction is active and for this long after it ended
		public static readonly TimeSpan Window = TimeSpan.FromDays(30);

		private static DateTime? EndedAt(Sanction s, DateTime now)
		{
			DateTime? ended = s.LiftedAt;
			if (s.ExpiresAt is DateTime expires && expires <= now && (ended == null || expires < ended))
			{
				ended = expires;
			}
			return ended;
		}

		public static bool CanAppeal(Sanction s, DateTime now)
		{
			if (s.Type == SanctionTypes.Kick || s.Source != AuthoritySources.Local)
			{
				return false;
			}
			DateTime? ended = EndedAt(s, now);
			return ended == null || now - ended.Value <= Window;
		}

		public static async Task<Appeal?> ForSanction(AppDbContext db, Int64 sanctionID, CancellationToken cancellationToken = default)
		{
			return await db.Appeals.AsNoTracking().FirstOrDefaultAsync(a => a.SanctionID == sanctionID, cancellationToken);
		}

		public static async Task<Appeal?> Get(AppDbContext db, Int64 appealID, CancellationToken cancellationToken = default)
		{
			return await db.Appeals.AsNoTracking().FirstOrDefaultAsync(a => a.ID == appealID, cancellationToken);
		}

		public static async Task<Dictionary<Int64, Appeal>> ForSanctions(AppDbContext db, IReadOnlyCollection<Int64> sanctionIDs, CancellationToken cancellationToken = default)
		{
			return await db.Appeals.AsNoTracking().Where(a => sanctionIDs.Contains(a.SanctionID)).ToDictionaryAsync(a => a.SanctionID, cancellationToken);
		}

		public static async Task<List<Appeal>> Queue(AppDbContext db, string? status, int skip, int take, CancellationToken cancellationToken = default)
		{
			IQueryable<Appeal> query = db.Appeals.AsNoTracking();
			if (status != null)
			{
				query = query.Where(a => a.Status == status);
			}
			return await query.OrderBy(a => a.CreatedAt).ThenBy(a => a.ID).Skip(skip).Take(take).ToListAsync(cancellationToken);
		}

		public static async Task<(AppealOutcome Outcome, Appeal? Appeal)> Open(AppDbContext db, Int64 userID, Int64 sanctionID, string text, DateTime now, CancellationToken cancellationToken = default)
		{
			Sanction? sanction = await db.Sanctions.AsNoTracking().FirstOrDefaultAsync(s => s.ID == sanctionID && s.UserID == userID, cancellationToken);
			if (sanction == null)
			{
				return (AppealOutcome.SanctionNotFound, null);
			}
			if (!CanAppeal(sanction, now))
			{
				return (AppealOutcome.NotAppealable, null);
			}

			Appeal appeal = new Appeal { SanctionID = sanctionID, UserID = userID, Text = text, CreatedAt = now };
			ModerationActor actor = new ModerationActor { Kind = AuditActorKinds.User, UserID = userID, Name = $"user:{userID}", Source = ModerationSources.Api };
			db.Appeals.Add(appeal);
			db.AuditLog.Add(Moderation.Entry(actor, ModerationActions.AppealOpen, AuditTargetKinds.User, userID, null, new { sanction_id = sanctionID }, now));
			try
			{
				await db.SaveChangesAsync(cancellationToken);
			}
			catch (DbUpdateException)
			{
				// the unique index on sanction_id: a second appeal, also a concurrent one
				db.ChangeTracker.Clear();
				if (await db.Appeals.AsNoTracking().AnyAsync(a => a.SanctionID == sanctionID, cancellationToken))
				{
					return (AppealOutcome.AlreadyAppealed, null);
				}
				throw;
			}
			finally
			{
				db.ChangeTracker.Clear();
			}
			return (AppealOutcome.Done, appeal);
		}

		// Claimed with one conditional update, so two reviewers never both decide it.
		public static async Task<AppealOutcome> Decide(AppDbContext db, Int64 appealID, string status, string reason, DateTime? expiresAt, ModerationActor actor, DateTime now, CancellationToken cancellationToken = default)
		{
			Appeal? appeal = await db.Appeals.AsNoTracking().FirstOrDefaultAsync(a => a.ID == appealID, cancellationToken);
			if (appeal == null)
			{
				return AppealOutcome.AppealNotFound;
			}
			Sanction? sanction = await db.Sanctions.AsTracking().FirstOrDefaultAsync(s => s.ID == appeal.SanctionID, cancellationToken);
			if (sanction == null)
			{
				return AppealOutcome.SanctionNotFound;
			}

			if (status == AppealStatuses.Shortened && (expiresAt == null || expiresAt <= now || (sanction.ExpiresAt != null && expiresAt >= sanction.ExpiresAt)))
			{
				return AppealOutcome.InvalidExpiry;
			}

			int claimed = await db.Appeals
				.Where(a => a.ID == appealID && a.Status == AppealStatuses.Open)
				.ExecuteUpdateAsync(s => s
					.SetProperty(a => a.Status, status)
					.SetProperty(a => a.DecidedAt, now)
					.SetProperty(a => a.DecidedByID, actor.UserID)
					.SetProperty(a => a.DecisionReason, reason), cancellationToken);
			if (claimed == 0)
			{
				db.ChangeTracker.Clear();
				return AppealOutcome.AlreadyDecided;
			}

			bool active = sanction.LiftedAt == null && (sanction.ExpiresAt == null || sanction.ExpiresAt > now);
			if (status == AppealStatuses.Lifted && active)
			{
				Moderation.Lift(sanction, reason, actor, now);
			}
			else if (status == AppealStatuses.Shortened && active)
			{
				sanction.ExpiresAt = expiresAt;
			}

			db.AuditLog.Add(Moderation.Entry(actor, ModerationActions.AppealDecide, AuditTargetKinds.User, appeal.UserID, reason,
				new { appeal_id = appealID, sanction_id = sanction.ID, type = sanction.Type, decision = status, expires_at = status == AppealStatuses.Shortened ? expiresAt : null }, now));
			try
			{
				await db.SaveChangesAsync(cancellationToken);
			}
			finally
			{
				db.ChangeTracker.Clear();
			}
			return AppealOutcome.Done;
		}
	}
}
