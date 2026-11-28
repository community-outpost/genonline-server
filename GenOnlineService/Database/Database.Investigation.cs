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

using GenOnlineService.Investigation;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace Database
{
	// Events counts the account's sessions on the addresses, null for devices
	public sealed record IdentifierAccount(long UserID, string Name, int NameTag, string State, DateTime FirstSeenAt, DateTime LastSeenAt, int? Events);

	public sealed record IdentifierMatch(long MatchID, DateTime StartedAt, long UserID, string Name, int NameTag);

	// most recently seen first; a page reads Take plus one to tell whether another follows
	public static class IdentifierAccounts
	{
		public const int MaximumRangeRows = 50000;
		public const int MaximumMatches = 50;

		private sealed record Seen(long UserID, DateTime First, DateTime Last, int? Events);

		// At most two devices whose keyed hash starts with the prefix, a range read on the kind and hash index
		public static async Task<List<(string Kind, byte[] Hash)>> ByHashPrefix(AppDbContext db, byte[] prefix, CancellationToken cancellationToken = default)
		{
			byte[] low = new byte[Device.ValueHmacLength];
			byte[] high = Enumerable.Repeat((byte)0xFF, Device.ValueHmacLength).ToArray();
			prefix.CopyTo(low, 0);
			prefix.CopyTo(high, 0);
			var found = new List<(string Kind, byte[] Hash)>();
			foreach (string kind in new[] { IdentifierKinds.MachineGuid, IdentifierKinds.Mac, IdentifierKinds.VolumeSerial })
			{
				List<byte[]> hashes = await db.Devices
					.FromSql($"SELECT * FROM devices WHERE kind = {kind} AND value_hmac >= {low} AND value_hmac <= {high}")
					.AsNoTracking()
					.Select(d => d.ValueHmac)
					.Distinct()
					.Take(2)
					.ToListAsync(cancellationToken);
				found.AddRange(hashes.Select(h => (kind, h)));
			}
			return found.DistinctBy(f => (f.Kind, Convert.ToHexString(f.Hash))).Take(2).ToList();
		}

		// Null when the range holds more sign-ins than one search reads
		public static async Task<(List<IdentifierAccount> Accounts, bool HasNext)?> Find(AppDbContext db, IdentifierQuery query, int skip, int take, CancellationToken cancellationToken = default)
		{
			List<Seen> seen;
			if (IdentifierKinds.IsDevice(query.Kind))
			{
				List<byte[]> hashes = query.Hashes.ToList();
				seen = (await db.Devices.AsNoTracking()
					.Where(d => d.Kind == query.Kind && hashes.Contains(d.ValueHmac))
					.GroupBy(d => d.UserID)
					.Select(g => new { UserID = g.Key, First = g.Min(d => d.FirstSeenAt), Last = g.Max(d => d.LastSeenAt) })
					.OrderByDescending(s => s.Last).ThenBy(s => s.UserID)
					.Skip(skip).Take(take + 1)
					.ToListAsync(cancellationToken))
					.Select(s => new Seen(s.UserID, s.First, s.Last, null)).ToList();
			}
			else if (query.Kind == IdentifierKinds.IpRange)
			{
				List<Seen>? inRange = await InRange(db, query.Range!.Value, query.StoredPrefix!, cancellationToken);
				if (inRange == null)
				{
					return null;
				}
				seen = inRange.OrderByDescending(s => s.Last).ThenBy(s => s.UserID).Skip(skip).Take(take + 1).ToList();
			}
			else
			{
				IQueryable<SessionEvent> events = db.SessionEvents.AsNoTracking();
				events = query.Kind == IdentifierKinds.Ip ? events.Where(e => e.Ip == query.Address) : events.Where(e => e.Country == query.Country);
				seen = (await events
					.GroupBy(e => e.UserID)
					.Select(g => new { UserID = g.Key, First = g.Min(e => e.CreatedAt), Last = g.Max(e => e.CreatedAt), Events = g.Count() })
					.OrderByDescending(s => s.Last).ThenBy(s => s.UserID)
					.Skip(skip).Take(take + 1)
					.ToListAsync(cancellationToken))
					.Select(s => new Seen(s.UserID, s.First, s.Last, s.Events)).ToList();
			}

			bool hasNext = seen.Count > take;
			List<long> ids = seen.Take(take).Select(s => s.UserID).ToList();
			var users = await db.Users.AsNoTracking()
				.Where(u => ids.Contains(u.ID))
				.Select(u => new { u.ID, u.Username, u.NameTag, u.State })
				.ToDictionaryAsync(u => u.ID, cancellationToken);

			// a row of a removed account names nobody
			List<IdentifierAccount> accounts = seen.Take(take).Where(s => users.ContainsKey(s.UserID))
				.Select(s => new IdentifierAccount(s.UserID, users[s.UserID].Username, users[s.UserID].NameTag, users[s.UserID].State, s.First, s.Last, s.Events))
				.ToList();
			return (accounts, hasNext);
		}

		// The latest matches played in a session of the device or address, newest first and capped, and none for a country
		// shortcut: a range reads the latest MaximumRangeRows matches of its text prefix, narrow the prefix when a range needs more
		public static async Task<List<IdentifierMatch>> MatchesOf(AppDbContext db, IdentifierQuery query, CancellationToken cancellationToken = default)
		{
			IQueryable<SessionEvent> events = db.SessionEvents.AsNoTracking();
			if (IdentifierKinds.IsDevice(query.Kind))
			{
				List<byte[]> hashes = query.Hashes.ToList();
				IQueryable<long> deviceIDs = db.Devices.AsNoTracking().Where(d => d.Kind == query.Kind && hashes.Contains(d.ValueHmac)).Select(d => d.ID);
				IQueryable<long> machineIDs = query.Kind switch
				{
					IdentifierKinds.MachineGuid => db.Machines.AsNoTracking().Where(m => m.MachineGuidID != null && deviceIDs.Contains(m.MachineGuidID.Value)).Select(m => m.ID),
					IdentifierKinds.Mac => db.Machines.AsNoTracking().Where(m => m.MacID != null && deviceIDs.Contains(m.MacID.Value)).Select(m => m.ID),
					_ => db.Machines.AsNoTracking().Where(m => m.VolumeSerialID != null && deviceIDs.Contains(m.VolumeSerialID.Value)).Select(m => m.ID)
				};
				events = events.Where(e => e.MachineID != null && machineIDs.Contains(e.MachineID.Value));
			}
			else if (query.Kind == IdentifierKinds.Ip)
			{
				events = events.Where(e => e.Ip == query.Address);
			}
			else if (query.Kind == IdentifierKinds.IpRange)
			{
				events = events.Where(e => e.Ip.StartsWith(query.StoredPrefix!));
			}
			else
			{
				return [];
			}

			bool range = query.Kind == IdentifierKinds.IpRange;
			var rows = await (
				from p in db.MatchPlayers.AsNoTracking()
				where p.UserId != null && p.SessionEventId != null
				join e in events on p.SessionEventId!.Value equals e.ID
				join m in db.Matches.AsNoTracking() on p.MatchId equals m.Id
				orderby m.StartedAt descending, m.Id descending
				select new { MatchID = m.Id, m.StartedAt, UserID = p.UserId!.Value, e.Ip })
				.Take(range ? MaximumRangeRows : MaximumMatches)
				.ToListAsync(cancellationToken);
			if (range)
			{
				rows = rows.Where(r => IPAddress.TryParse(r.Ip, out IPAddress? address) && query.Range!.Value.Contains(address)).Take(MaximumMatches).ToList();
			}

			List<long> ids = rows.Select(r => r.UserID).Distinct().ToList();
			var users = await db.Users.AsNoTracking()
				.Where(u => ids.Contains(u.ID))
				.Select(u => new { u.ID, u.Username, u.NameTag })
				.ToDictionaryAsync(u => u.ID, cancellationToken);

			// a row of a removed account names nobody
			return rows.Where(r => users.ContainsKey(r.UserID))
				.Select(r => new IdentifierMatch(r.MatchID, r.StartedAt, r.UserID, users[r.UserID].Username, users[r.UserID].NameTag))
				.ToList();
		}

		// Addresses are stored as text: the database narrows by the text prefix, the exact range check runs here
		private static async Task<List<Seen>?> InRange(AppDbContext db, IPNetwork range, string storedPrefix, CancellationToken cancellationToken)
		{
			var rows = await db.SessionEvents.AsNoTracking()
				.Where(e => e.Ip.StartsWith(storedPrefix))
				.GroupBy(e => new { e.Ip, e.UserID })
				.Select(g => new { g.Key.Ip, g.Key.UserID, First = g.Min(e => e.CreatedAt), Last = g.Max(e => e.CreatedAt), Events = g.Count() })
				.Take(MaximumRangeRows + 1)
				.ToListAsync(cancellationToken);
			if (rows.Count > MaximumRangeRows)
			{
				return null;
			}

			return rows.Where(r => IPAddress.TryParse(r.Ip, out IPAddress? address) && range.Contains(address))
				.GroupBy(r => r.UserID)
				.Select(g => new Seen(g.Key, g.Min(r => r.First), g.Max(r => r.Last), g.Sum(r => r.Events)))
				.ToList();
		}
	}
}
