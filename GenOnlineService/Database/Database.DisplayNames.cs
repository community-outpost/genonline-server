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
using GenOnlineService.Names;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage;
using System.Globalization;

public sealed record NameChange(Int64 UserID, string OldName, int OldTag, string NewName, int NewTag)
{
	public bool NameChanged => !String.Equals(OldName, NewName, StringComparison.Ordinal);
}

// A name#tag a user has held, kept for that user under the look-alike key: no other user is given it, and the user gets
// the tag back when they take the name again. AppDbContext writes NameKey from Name on every save.
public class NameReservation
{
	public Int64 ID { get; set; }
	public Int64 UserID { get; set; }
	public User? User { get; set; }

	public string Name { get; set; } = String.Empty;
	public string NameKey { get; set; } = String.Empty;
	public int NameTag { get; set; }

	// when the user last held the pair: taken, or left (changed_at of that user_username_history row); the newest tag per key comes first
	public DateTime HeldAt { get; set; } = DateTime.UnixEpoch;
}

public class NameReservationConfiguration : IEntityTypeConfiguration<NameReservation>
{
	public void Configure(EntityTypeBuilder<NameReservation> builder)
	{
		builder.ToTable("name_reservations");

		builder.HasKey(e => e.ID);
		builder.HasIndex(e => new { e.NameKey, e.NameTag }).IsUnique().HasDatabaseName("ux_name_reservations_name_key_name_tag");
		builder.HasIndex(e => e.UserID).HasDatabaseName("ix_name_reservations_user_id");
		builder.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserID).OnDelete(DeleteBehavior.Cascade);

		builder.Property(e => e.ID).HasColumnName("id").ValueGeneratedOnAdd();
		builder.Property(e => e.UserID).HasColumnName("user_id");
		builder.Property(e => e.Name).HasColumnName("name").HasMaxLength(User.UsernameMaxLength);
		builder.Property(e => e.NameKey).HasColumnName("name_key").HasMaxLength(NameKey.MaxLength);
		builder.Property(e => e.NameTag).HasColumnName("name_tag");
		builder.Property(e => e.HeldAt).HasColumnName("held_at");
	}
}

public enum LocalRenameOutcome
{
	Renamed,
	Unchanged,
	NotFound,
	Taken
}

// Another user holds the reservation of the pair a name change was about to take; the caller reads again and retries
public sealed class NameReservationConflictException : Exception
{
	public NameReservationConflictException(string name, int tag) : base($"The pair {name}#{tag:D4} is reserved for another user") { }
}

public sealed class NameTagsExhaustedException : Exception
{
	public NameTagsExhaustedException(string name) : base($"No free tag for the name {name}") { }
}

namespace Database
{
	// name is not unique, name#tag is. Tags follow the name (NameTags) and every pair a player held stays reserved (name_reservations).
	// Look-alikes count as one name: NameKey is
	// unique with the tag (ux_users_name_key_name_tag, ux_name_reservations_name_key_name_tag).
	public static class DisplayNames
	{
		public const int MinTag = 1;
		public const int MaxTag = 9999;

		private const int CandidateLimit = 64;

		public const int NewUserAttempts = 8;

		// Retries up to this attempt pick among the next free candidates; later ones pick any free tag
		private const int SpreadAttempts = 4;

		public static string FormatTag(int tag) => tag.ToString("D4", CultureInfo.InvariantCulture);

		public static string Format(string name, int tag) => $"{name}#{FormatTag(tag)}";

		// A copied name must fit the column; the rules on what players may choose live where names are chosen
		public static bool IsStorable(string? name)
		{
			return !String.IsNullOrWhiteSpace(name)
				&& !name.Any(Char.IsControl)
				&& String.Equals(TextSanitizer.Clean(name, User.UsernameMaxLength), name, StringComparison.Ordinal)
				&& NameNormalizer.Normalize(name).Length <= User.UsernameMaxLength
				&& NameKey.Of(name).Length <= NameKey.MaxLength;
		}

		public static async Task<int> FreeTag(AppDbContext db, string name, Int64? exceptUserID = null, int? alsoAvoid = null)
		{
			string key = NameKey.Of(name);
			string normalized = NameNormalizer.Normalize(name);
			HashSet<int> used = (await db.Users
				.Where(u => (u.NameKey == key || u.UsernameNormalized == normalized) && (exceptUserID == null || u.ID != exceptUserID))
				.Select(u => u.NameTag)
				.ToListAsync()).ToHashSet();
			if (alsoAvoid != null)
			{
				used.Add(alsoAvoid.Value);
			}

			List<int> free = Enumerable.Range(MinTag, MaxTag - MinTag + 1).Where(t => !used.Contains(t)).ToList();
			if (free.Count == 0)
			{
				throw new NameTagsExhaustedException(name);
			}

			return free[Random.Shared.Next(free.Count)];
		}

		// Keeps the tag of a user who holds the name or a look-alike, else their reserved tag, else the first free candidate (NameTags). From attempt 2 a retry picks a random free candidate, past SpreadAttempts any free tag, so concurrent sign-ups of one name spread out.
		private static async Task<int> TagFor(AppDbContext db, Int64 userID, User? current, string name, int attempt = 1)
		{
			string key = NameKey.Of(name);
			string normalized = NameNormalizer.Normalize(name);
			HashSet<int> used = (await db.Users
				.Where(u => u.ID != userID && (u.NameKey == key || u.UsernameNormalized == normalized))
				.Select(u => u.NameTag)
				.ToListAsync()).ToHashSet();

			if (current != null && String.Equals(NameKey.Of(current.Username), key, StringComparison.Ordinal) && !used.Contains(current.NameTag))
			{
				return current.NameTag;
			}

			var reserved = await db.NameReservations
				.Where(r => r.NameKey == key)
				.Select(r => new { r.UserID, r.NameTag, r.HeldAt })
				.ToListAsync();
			foreach (var own in reserved.Where(r => r.UserID == userID).OrderByDescending(r => r.HeldAt))
			{
				// a name copied from an upstream can hold a reserved pair
				if (!used.Contains(own.NameTag))
				{
					return own.NameTag;
				}
			}

			used.UnionWith(reserved.Where(r => r.UserID != userID).Select(r => r.NameTag));
			const int range = MaxTag - MinTag + 1;
			if (used.Count >= range)
			{
				throw new NameTagsExhaustedException(name);
			}

			List<int> candidates = NameTags.Candidates(userID, key).Take(CandidateLimit).ToList();
			List<int> free = candidates.Where(t => !used.Contains(t)).ToList();
			if (attempt > 1 && attempt <= SpreadAttempts && free.Count > 1)
			{
				return free[1 + Random.Shared.Next(Math.Min(free.Count - 1, attempt * 4))];
			}
			if (attempt > SpreadAttempts)
			{
				List<int> any = Enumerable.Range(MinTag, range).Where(t => !used.Contains(t)).ToList();
				return any[Random.Shared.Next(any.Count)];
			}
			if (free.Count > 0)
			{
				return free[0];
			}
			int first = candidates[0];

			// a crowded name: the next free tag after the first candidate, still the same for the same player and name
			for (int step = 1; ; step++)
			{
				int tag = MinTag + (first - MinTag + step) % range;
				if (!used.Contains(tag))
				{
					return tag;
				}
			}
		}

		// Keeps name#tag for the user at now: take is the pair the user takes, otherwise the one they leave. A reservation of
		// theirs is refreshed, a missing one is added (a name copied from an upstream earlier has none). Another user's
		// reservation of a pair being taken means that user took the pair after TagFor read it, so the change must start over.
		internal static async Task Reserve(AppDbContext db, Int64 userID, string name, int tag, DateTime now, bool take)
		{
			string key = NameKey.Of(name);
			NameReservation? reservation = db.ChangeTracker.Entries<NameReservation>().Select(e => e.Entity).FirstOrDefault(r => r.NameKey == key && r.NameTag == tag)
				?? await db.NameReservations.AsTracking().FirstOrDefaultAsync(r => r.NameKey == key && r.NameTag == tag);
			if (reservation == null)
			{
				db.NameReservations.Add(new NameReservation { UserID = userID, Name = name, NameKey = key, NameTag = tag, HeldAt = now });
			}
			else if (reservation.UserID == userID)
			{
				reservation.Name = name;
				reservation.HeldAt = now;
			}
			else if (take)
			{
				throw new NameReservationConflictException(name, tag);
			}
		}

		// Sets a new, unsaved user's normalized name, key and tag (TagFor), and reserves the pair for the user. The id is only
		// known after the insert, so a sign-up uses the candidates of user id 0 unless the caller gave an id. Two look-alike
		// sign-ups that pick the same tag at once meet a unique index, and the caller retries with the next attempt, up to
		// NewUserAttempts; only the first attempt is deterministic.
		public static async Task AssignNewUser(AppDbContext db, User user, int attempt = 1)
		{
			// a retry detaches the user after a unique violation; the reservation of that attempt goes with it
			foreach (EntityEntry<NameReservation> stale in db.ChangeTracker.Entries<NameReservation>().Where(e => ReferenceEquals(e.Entity.User, user)).ToList())
			{
				stale.State = EntityState.Detached;
			}

			user.UsernameNormalized = NameNormalizer.Normalize(user.Username);
			user.NameKey = NameKey.Of(user.Username);
			user.NameTag = await TagFor(db, user.ID, null, user.Username, attempt);
			db.NameReservations.Add(new NameReservation { User = user, Name = user.Username, NameKey = user.NameKey, NameTag = user.NameTag, HeldAt = DateTime.UtcNow });
		}

		// neutral name of an account that still owes a chosen one, "Player" and six digits, so pending accounts spread over many names
		public static string Placeholder() => "Player" + Random.Shared.Next(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);

		public static bool IsPlaceholderShaped(string name)
			=> name.Length == 12 && name.StartsWith("Player", StringComparison.OrdinalIgnoreCase) && name.AsSpan(6).IndexOfAnyExceptInRange('0', '9') < 0;

		// The name a player owes, chosen: the tag follows the name (TagFor), so the choice never fails on a taken tag. Settles
		// name_required_at even when the name is unchanged. False when the user is gone.
		public static async Task<(bool Found, NameChange? Change)> Choose(AppDbContext db, Int64 userID, string name, string changedBy)
		{
			for (int attempt = 1; ; attempt++)
			{
				User? user = await db.Users.AsTracking().FirstOrDefaultAsync(u => u.ID == userID);
				if (user == null)
				{
					return (false, null);
				}

				NameChange? change = null;
				if (!String.Equals(user.Username, name, StringComparison.Ordinal))
				{
					try
					{
						change = await Assign(db, user, name, await TagFor(db, userID, user, name), changedBy);
					}
					catch (NameReservationConflictException) when (attempt < 3)
					{
						db.ChangeTracker.Clear();
						continue;
					}
				}
				user.NameRequiredAt = null;

				try
				{
					await db.SaveChangesAsync();
					return (true, change);
				}
				catch (DbUpdateException ex) when (DbHelpers.IsUniqueViolation(ex) && attempt < 3)
				{
					// another player took the name with that tag at the same moment; draw again
					db.ChangeTracker.Clear();
				}
			}
		}

		// Staff reset an inappropriate name: a placeholder with its tag (TagFor), the player owes a new name, and the security
		// stamp changes so website sessions end. Null when the user is gone.
		public static async Task<NameChange?> ResetName(AppDbContext db, Int64 userID, string changedBy)
		{
			for (int attempt = 1; ; attempt++)
			{
				User? user = await db.Users.AsTracking().FirstOrDefaultAsync(u => u.ID == userID);
				if (user == null)
				{
					return null;
				}

				string placeholder = Placeholder();
				NameChange change;
				try
				{
					change = await Assign(db, user, placeholder, await TagFor(db, userID, user, placeholder), changedBy);
				}
				catch (NameReservationConflictException) when (attempt < 3)
				{
					db.ChangeTracker.Clear();
					continue;
				}
				user.NameRequiredAt = DateTime.UtcNow;
				user.SecurityStamp = Guid.NewGuid().ToString("N");

				try
				{
					await db.SaveChangesAsync();
					return change;
				}
				catch (DbUpdateException ex) when (DbHelpers.IsUniqueViolation(ex) && attempt < 3)
				{
					db.ChangeTracker.Clear();
				}
			}
		}

		// A rename where the environment owns names (self-hosted): the tag follows the name (TagFor), so a look-alike holding a
		// tag only moves the rename on to the next one; Taken only when every tag of the name is held or reserved. The unique
		// indexes decide a race, and the loser draws again.
		public static async Task<(LocalRenameOutcome Outcome, NameChange? Change)> Rename(AppDbContext db, Int64 userID, string name, string changedBy)
		{
			for (int attempt = 1; ; attempt++)
			{
				User? user = await db.Users.AsTracking().FirstOrDefaultAsync(u => u.ID == userID);
				if (user == null)
				{
					return (LocalRenameOutcome.NotFound, null);
				}
				if (String.Equals(user.Username, name, StringComparison.Ordinal))
				{
					return (LocalRenameOutcome.Unchanged, null);
				}

				NameChange change;
				try
				{
					change = await Assign(db, user, name, await TagFor(db, userID, user, name), changedBy);
				}
				catch (NameTagsExhaustedException)
				{
					return (LocalRenameOutcome.Taken, null);
				}
				catch (NameReservationConflictException)
				{
					// another player took the pair after it was read
					db.ChangeTracker.Clear();
					if (attempt == 3)
					{
						return (LocalRenameOutcome.Taken, null);
					}
					continue;
				}

				try
				{
					await db.SaveChangesAsync();
					return (LocalRenameOutcome.Renamed, change);
				}
				catch (DbUpdateException ex) when (DbHelpers.IsUniqueViolation(ex))
				{
					// another rename took a look-alike name with this tag at the same moment
					db.ChangeTracker.Clear();
					if (attempt == 3)
					{
						return (LocalRenameOutcome.Taken, null);
					}
				}
			}
		}

		// A local name change: Record, and the old and new pairs reserved for the user (Reserve), all at one time, so the
		// left pair's held_at equals its history row's changed_at and the taken pair's equals renamed_at
		private static async Task<NameChange> Assign(AppDbContext db, User user, string name, int tag, string changedBy)
		{
			DateTime now = DateTime.UtcNow;
			await Reserve(db, user.ID, user.Username, user.NameTag, now, take: false);
			NameChange change = Record(db, user, name, tag, changedBy, now);
			await Reserve(db, user.ID, name, tag, now, take: true);
			return change;
		}

		private static NameChange Record(AppDbContext db, User user, string name, int tag, string changedBy, DateTime? at = null)
		{
			DateTime now = at ?? DateTime.UtcNow;
			NameChange change = new(user.ID, user.Username, user.NameTag, name, tag);

			db.UserUsernameHistory.Add(new UserUsernameHistory
			{
				UserID = user.ID,
				Username = user.Username,
				NameTag = user.NameTag,
				ChangedAt = now,
				ChangedBy = changedBy
			});

			user.Username = name;
			user.UsernameNormalized = NameNormalizer.Normalize(name);
			user.NameKey = NameKey.Of(name);
			user.NameTag = tag;
			user.RenamedAt = now;
			user.UpdatedAt = now;
			// any new name settles one the player owed; a staff name reset owes it again afterwards
			user.NameRequiredAt = null;
			return change;
		}

		public static async Task<(string Name, int Tag)?> Get(AppDbContext db, Int64 userID)
		{
			var row = await db.Users.Where(u => u.ID == userID).Select(u => new { u.Username, u.NameTag }).FirstOrDefaultAsync();
			return row == null ? null : (row.Username, row.NameTag);
		}

		public static async Task<Dictionary<Int64, (string Name, int Tag)>> GetBulk(AppDbContext db, IReadOnlyCollection<Int64> userIDs)
		{
			return await db.Users
				.Where(u => userIDs.Contains(u.ID))
				.Select(u => new { u.ID, u.Username, u.NameTag })
				.ToDictionaryAsync(u => u.ID, u => (u.Username, u.NameTag));
		}
	}
}
