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

namespace GenOnlineService.Names
{
	// Reserves every name#tag a user holds or has held under NameKey.Of(name). SQL cannot compute the key, so the migration creates the table
	// empty and "GenOnlineService migrate" runs this on every run. A pair several users claim goes to the current holder, else the last
	// holder, else the lowest id. held_at is the newest of the row and the pair's history rows, or renamed_at (created_at before any rename)
	// for a current pair without either.
	public static class NameReservationBackfill
	{
		public const int DefaultBatchSize = 1000;

		// Lost: claims another user's claim beat. Skipped: rows the running service reserved for someone else while this ran
		public sealed record Outcome(int Added, int Removed, int Lost, int Skipped);

		private sealed record Claim(Int64 UserID, string Name, bool Current, DateTime HeldAt);

		public static Task<Outcome> Run(AppDbContext db, CancellationToken cancellationToken = default) => Run(db, DefaultBatchSize, null, cancellationToken);

		// runs between reading a batch and writing it; tests use it to race the running service
		internal static async Task<Outcome> Run(AppDbContext db, int batchSize, Func<Task>? beforeWrite, CancellationToken cancellationToken = default)
		{
			int added = 0, removed = 0, lost = 0, skipped = 0;
			Int64 after = Int64.MinValue;
			while (true)
			{
				var users = await db.Users.AsNoTracking()
					.Where(u => u.ID > after)
					.OrderBy(u => u.ID)
					.Take(batchSize)
					.Select(u => new { u.ID, u.Username, u.NameTag, Since = u.RenamedAt ?? u.CreatedAt })
					.ToListAsync(cancellationToken);
				if (users.Count == 0)
				{
					break;
				}
				Int64 first = users[0].ID;
				after = users[^1].ID;
				List<Int64> ids = users.Select(u => u.ID).ToList();
				HashSet<Int64> inBatch = ids.ToHashSet();

				var history = await db.UserUsernameHistory.AsNoTracking()
					.Where(h => ids.Contains(h.UserID) && h.NameTag != null)
					.Select(h => new { h.UserID, h.Username, h.NameTag, h.ChangedAt })
					.ToListAsync(cancellationToken);
				List<NameReservation> own = await db.NameReservations.AsNoTracking().Where(r => ids.Contains(r.UserID)).ToListAsync(cancellationToken);

				// newest of the row and the history rows per user and pair (the row on a tie)
				Dictionary<(Int64 User, string Key, int Tag), Claim> claims = new();
				void Merge(Int64 user, string name, int tag, DateTime heldAt)
				{
					var id = (user, NameKey.Of(name), tag);
					if (!claims.TryGetValue(id, out Claim? claim) || heldAt > claim.HeldAt)
					{
						claims[id] = new Claim(user, name, false, heldAt);
					}
				}
				foreach (NameReservation row in own)
				{
					Merge(row.UserID, row.Name, row.NameTag, row.HeldAt);
				}
				foreach (var row in history)
				{
					Merge(row.UserID, row.Username, row.NameTag!.Value, row.ChangedAt);
				}
				foreach (var user in users)
				{
					var id = (user.ID, NameKey.Of(user.Username), user.NameTag);
					claims[id] = new Claim(user.ID, user.Username, true, claims.TryGetValue(id, out Claim? claim) ? claim.HeldAt : user.Since);
				}

				// other users' claims on the same pairs: the pairs they hold now, else their reservation rows
				List<string> keys = claims.Keys.Select(k => k.Key).Distinct().ToList();
				Dictionary<(string Key, int Tag), Claim> others = new();
				List<NameReservation> foreign = new();
				foreach (string[] chunk in keys.Chunk(500))
				{
					foreign.AddRange(await db.NameReservations.AsNoTracking().Where(r => chunk.Contains(r.NameKey) && !ids.Contains(r.UserID)).ToListAsync(cancellationToken));
					var holders = await db.Users.AsNoTracking()
						.Where(u => chunk.Contains(u.NameKey) && !ids.Contains(u.ID))
						.Select(u => new { u.ID, u.Username, u.NameTag, u.NameKey, Since = u.RenamedAt ?? u.CreatedAt })
						.ToListAsync(cancellationToken);
					foreach (var holder in holders)
					{
						others[(holder.NameKey, holder.NameTag)] = new Claim(holder.ID, holder.Username, true, holder.Since);
					}
				}
				foreach (NameReservation row in foreign)
				{
					others.TryAdd((row.NameKey, row.NameTag), new Claim(row.UserID, row.Name, false, row.HeldAt));
				}

				Dictionary<(string Key, int Tag), NameReservation> wanted = new();
				foreach (var pair in claims.GroupBy(c => (c.Key.Key, c.Key.Tag), c => c.Value))
				{
					List<Claim> contenders = pair.ToList();
					if (others.TryGetValue(pair.Key, out Claim? other))
					{
						contenders.Add(other);
					}
					List<Claim> ranked = contenders.OrderByDescending(c => c.Current).ThenByDescending(c => c.HeldAt).ThenBy(c => c.UserID).ToList();
					// a beaten outside user counts only when their batch has passed, so every lost claim counts once
					lost += ranked.Skip(1).Count(c => inBatch.Contains(c.UserID) || c.UserID < first);
					Claim winner = ranked[0];
					if (inBatch.Contains(winner.UserID))
					{
						wanted[pair.Key] = new NameReservation { UserID = winner.UserID, Name = winner.Name, NameKey = pair.Key.Key, NameTag = pair.Key.Tag, HeldAt = winner.HeldAt };
					}
				}

				// a row stays when it already says what is wanted; this batch's other rows and beaten outside rows go
				List<Int64> stale = new();
				foreach (NameReservation row in own)
				{
					if (wanted.TryGetValue((row.NameKey, row.NameTag), out NameReservation? want) && want.ID == 0 && want.UserID == row.UserID
						&& String.Equals(want.Name, row.Name, StringComparison.Ordinal) && want.HeldAt == row.HeldAt)
					{
						want.ID = row.ID;
					}
					else
					{
						stale.Add(row.ID);
					}
				}
				stale.AddRange(foreign.Where(r => wanted.ContainsKey((r.NameKey, r.NameTag))).Select(r => r.ID));
				List<NameReservation> inserts = wanted.Values.Where(r => r.ID == 0).ToList();

				if (beforeWrite != null)
				{
					await beforeWrite();
				}
				foreach (Int64[] chunk in stale.Chunk(1000))
				{
					removed += await db.NameReservations.Where(r => chunk.Contains(r.ID)).ExecuteDeleteAsync(cancellationToken);
				}
				int written = await Insert(db, inserts, cancellationToken);
				added += written;
				skipped += inserts.Count - written;
				db.ChangeTracker.Clear();
			}
			return new Outcome(added, removed, lost, skipped);
		}

		private static async Task<int> Insert(AppDbContext db, List<NameReservation> rows, CancellationToken cancellationToken)
		{
			if (rows.Count == 0)
			{
				return 0;
			}

			db.NameReservations.AddRange(rows);
			try
			{
				await db.SaveChangesAsync(cancellationToken);
				return rows.Count;
			}
			catch (DbUpdateException ex) when (DbHelpers.IsUniqueViolation(ex))
			{
				db.ChangeTracker.Clear();
			}

			int written = 0;
			foreach (NameReservation row in rows)
			{
				row.ID = 0;
				db.NameReservations.Add(row);
				try
				{
					await db.SaveChangesAsync(cancellationToken);
					written++;
				}
				catch (DbUpdateException ex) when (DbHelpers.IsUniqueViolation(ex))
				{
					// the running service reserved this pair for a player after the batch was read; its row stands
				}
				db.ChangeTracker.Clear();
			}
			return written;
		}

		public static async Task RunCommand(AppDbContext db, TextWriter output, CancellationToken cancellationToken = default)
		{
			Outcome outcome = await Run(db, cancellationToken);
			output.WriteLine(outcome.Added == 0 && outcome.Removed == 0
				? "Name reservations are up to date."
				: $"Reserved {outcome.Added} name#tag pair(s) and removed {outcome.Removed} outdated reservation(s).");
			if (outcome.Lost > 0)
			{
				output.WriteLine($"{outcome.Lost} name#tag claim(s) lost to another user; each pair stays reserved for the user who holds it now, else the one who held it last.");
			}
			if (outcome.Skipped > 0)
			{
				output.WriteLine($"Skipped {outcome.Skipped} pair(s) the running service reserved meanwhile; run migrate again to settle them.");
			}
		}
	}
}
