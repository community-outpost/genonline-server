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
using GenOnlineService.Flags;
using GenOnlineService.Realtime;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace GenOnlineService.Playlists
{
	public static class PlaylistCodes
	{
		public const string LastEnabled = "playlist.last_enabled";
	}

	public static class PlaylistStore
	{
		public static PlaylistDraft ToDraft(PlaylistRow row, IEnumerable<PlaylistMapRow> maps) => new(row.Id, row.Name, row.Enabled, row.MinPlayers, row.DesiredPlayers, row.MinSelectedMaps,
			row.AllowTeams, row.TeamSize, row.AllowArmySelection, row.GracePeriodMs,
			maps.OrderBy(m => m.Position).ThenBy(m => m.Id).Select(m => new MapDraft(m.Name, m.Path, m.Custom, m.MaxPlayers, m.Enabled, m.Id)).ToList(),
			row.Version, row.UpdatedBy, row.UpdatedAt);

		public static async Task<List<PlaylistDraft>> LoadAll(AppDbContext db)
		{
			List<PlaylistRow> rows = await db.Playlists.AsNoTracking().OrderBy(p => p.Id).ToListAsync();
			ILookup<int, PlaylistMapRow> maps = (await db.PlaylistMaps.AsNoTracking().ToListAsync()).ToLookup(m => m.PlaylistId);
			return rows.Select(row => ToDraft(row, maps[row.Id])).ToList();
		}

		public static async Task<PlaylistDraft?> Load(AppDbContext db, int id)
		{
			PlaylistRow? row = await db.Playlists.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
			return row == null ? null : ToDraft(row, await db.PlaylistMaps.AsNoTracking().Where(m => m.PlaylistId == id).ToListAsync());
		}

		public static async Task EnsureSeedAsync(AppDbContext db)
		{
			if (await db.Playlists.AnyAsync())
			{
				return;
			}

			DateTime now = DateTime.UtcNow;
			foreach (PlaylistDraft draft in PlaylistSeed.Playlists)
			{
				AddRows(db, draft, PlaylistSeed.Actor, now);
			}
			try
			{
				await db.SaveChangesAsync();
			}
			catch (DbUpdateException ex) when (DbHelpers.IsUniqueViolation(ex))
			{
				// another copy seeded at the same moment
				db.ChangeTracker.Clear();
			}
		}

		private static void AddRows(AppDbContext db, PlaylistDraft draft, string updatedBy, DateTime now)
		{
			db.Playlists.Add(new PlaylistRow
			{
				Id = draft.Id,
				Name = draft.Name,
				Enabled = draft.Enabled,
				MinPlayers = draft.MinPlayers,
				DesiredPlayers = draft.DesiredPlayers,
				MinSelectedMaps = draft.MinSelectedMaps,
				AllowTeams = draft.AllowTeams,
				TeamSize = draft.TeamSize,
				AllowArmySelection = draft.AllowArmySelection,
				GracePeriodMs = draft.GracePeriodMs,
				Version = 1,
				UpdatedBy = updatedBy,
				UpdatedAt = now
			});
			for (int i = 0; i < draft.Maps.Count; i++)
			{
				db.PlaylistMaps.Add(MapRowOf(draft.Id, i, draft.Maps[i]));
			}
		}

		private static PlaylistMapRow MapRowOf(int playlistId, int position, MapDraft map) => new()
		{
			PlaylistId = playlistId,
			Position = position,
			Name = map.Name,
			Path = map.Path,
			Custom = map.Custom,
			MaxPlayers = map.MaxPlayers,
			Enabled = map.Enabled
		};

		// The playlist fields without the maps, so an audit row stays small
		private static JsonNode Fields(PlaylistDraft draft)
		{
			JsonObject node = (JsonObject)JsonSerializer.SerializeToNode(draft with { Maps = [] }, ApiResults.Json)!;
			node["map_count"] = draft.Maps.Count;
			node["enabled_map_count"] = draft.Maps.Count(m => m.Enabled);
			return node;
		}

		public static async Task<(PlaylistDraft? Playlist, Failure? Failure)> Create(AppDbContext db, PlaylistDraft draft, Database.ModerationActor actor)
		{
			await using var tx = await db.Database.BeginTransactionAsync();
			int next = ((await db.Playlists.AsNoTracking().MaxAsync(p => (int?)p.Id)) ?? -1) + 1;
			PlaylistDraft created = draft with { Id = next };
			string? field = PlaylistRules.Check(created);
			if (field != null)
			{
				return (null, Result.Invalid(field));
			}

			AddRows(db, created, actor.Name, DateTime.UtcNow);
			try
			{
				await db.SaveChangesAsync();
			}
			catch (DbUpdateException ex) when (DbHelpers.IsUniqueViolation(ex))
			{
				return (null, Result.Fail(FlagCodes.Stale, new { Version = (int?)null }));
			}
			db.ChangeTracker.Clear();

			await Database.Moderation.AddAuditEntry(db, actor, ModerationActions.PlaylistCreate, AuditTargetKinds.Playlist, next, null, new { playlist_id = next, after = Fields(created) });
			await tx.CommitAsync();
			return (await Load(db, next), null);
		}

		// Replaces the playlist fields and leaves the maps alone
		public static Task<(PlaylistDraft? Playlist, Failure? Failure)> Update(AppDbContext db, int id, int? expectedVersion, Func<PlaylistDraft, PlaylistDraft> edit, Database.ModerationActor actor) =>
			Change(db, id, expectedVersion, actor, ModerationActions.PlaylistUpdate, before => (edit(before) with { Maps = before.Maps }, null, null));

		// Every map change goes through here, so the rules see the whole playlist after it
		public static Task<(PlaylistDraft? Playlist, Failure? Failure)> ChangeMaps(AppDbContext db, int id, int? expectedVersion, string change, Func<List<MapDraft>, Failure?> edit, Database.ModerationActor actor) =>
			Change(db, id, expectedVersion, actor, ModerationActions.PlaylistMapChange, before =>
			{
				List<MapDraft> maps = before.Maps.ToList();
				Failure? failure = edit(maps);
				return (before with { Maps = maps }, failure, new { change });
			});

		private static async Task<(PlaylistDraft? Playlist, Failure? Failure)> Change(AppDbContext db, int id, int? expectedVersion, Database.ModerationActor actor, string action,
			Func<PlaylistDraft, (PlaylistDraft Next, Failure? Failure, object? Detail)> apply)
		{
			await using var tx = await db.Database.BeginTransactionAsync();
			PlaylistRow? row = await db.Playlists.AsTracking().FirstOrDefaultAsync(p => p.Id == id);
			if (row == null)
			{
				return (null, Result.NotFound("playlist"));
			}
			if (expectedVersion != null && row.Version != expectedVersion)
			{
				return (null, Result.Fail(FlagCodes.Stale, new { row.Version }));
			}

			List<PlaylistMapRow> mapRows = await db.PlaylistMaps.AsTracking().Where(m => m.PlaylistId == id).ToListAsync();
			PlaylistDraft before = ToDraft(row, mapRows);
			(PlaylistDraft after, Failure? failure, object? detail) = apply(before);
			if (failure != null)
			{
				return (null, failure);
			}
			string? field = PlaylistRules.Check(after);
			if (field != null)
			{
				return (null, Result.Invalid(field));
			}

			row.Name = after.Name;
			row.Enabled = after.Enabled;
			row.MinPlayers = after.MinPlayers;
			row.DesiredPlayers = after.DesiredPlayers;
			row.MinSelectedMaps = after.MinSelectedMaps;
			row.AllowTeams = after.AllowTeams;
			row.TeamSize = after.TeamSize;
			row.AllowArmySelection = after.AllowArmySelection;
			row.GracePeriodMs = after.GracePeriodMs;
			row.Version++;
			row.UpdatedBy = actor.Name;
			row.UpdatedAt = DateTime.UtcNow;

			// a map keeps its row, and so its id, wherever it moves to
			Dictionary<long, PlaylistMapRow> existing = mapRows.ToDictionary(m => m.Id);
			for (int i = 0; i < after.Maps.Count; i++)
			{
				MapDraft map = after.Maps[i];
				if (map.Id != 0 && existing.Remove(map.Id, out PlaylistMapRow? kept))
				{
					kept.Position = i;
					kept.Name = map.Name;
					kept.Path = map.Path;
					kept.Custom = map.Custom;
					kept.MaxPlayers = map.MaxPlayers;
					kept.Enabled = map.Enabled;
				}
				else
				{
					db.PlaylistMaps.Add(MapRowOf(id, i, map));
				}
			}
			db.PlaylistMaps.RemoveRange(existing.Values);

			try
			{
				await db.SaveChangesAsync();
			}
			catch (DbUpdateConcurrencyException)
			{
				return (null, Result.Fail(FlagCodes.Stale, new { Version = (int?)null }));
			}
			db.ChangeTracker.Clear();

			var details = new JsonObject { ["playlist_id"] = id, ["before"] = Fields(before), ["after"] = Fields(after) };
			if (detail != null)
			{
				details["change"] = JsonSerializer.SerializeToNode(detail, ApiResults.Json);
				details["maps_before"] = MapsOf(before);
				details["maps_after"] = MapsOf(after);
			}
			await Database.Moderation.AddAuditEntry(db, actor, action, AuditTargetKinds.Playlist, id, null, details);
			await tx.CommitAsync();
			return (await Load(db, id), null);
		}

				private static JsonNode? MapsOf(PlaylistDraft draft) => JsonSerializer.SerializeToNode(draft.Maps.Select(m => new { m.Id, m.Name, m.Path, m.Custom, m.MaxPlayers, m.Enabled }), ApiResults.Json);

		public static async Task<Failure?> Delete(AppDbContext db, int id, int? expectedVersion, Database.ModerationActor actor)
		{
			await using var tx = await db.Database.BeginTransactionAsync();
			PlaylistRow? row = await db.Playlists.AsTracking().FirstOrDefaultAsync(p => p.Id == id);
			if (row == null)
			{
				return Result.NotFound("playlist");
			}
			if (expectedVersion != null && row.Version != expectedVersion)
			{
				return Result.Fail(FlagCodes.Stale, new { row.Version });
			}
			// without one the quick match would offer nothing, disabling it is the way to pause it
			if (row.Enabled && !await db.Playlists.AnyAsync(p => p.Id != id && p.Enabled))
			{
				return Result.Fail(PlaylistCodes.LastEnabled, new { PlaylistId = id });
			}

			List<PlaylistMapRow> mapRows = await db.PlaylistMaps.AsTracking().Where(m => m.PlaylistId == id).ToListAsync();
			PlaylistDraft before = ToDraft(row, mapRows);
			db.PlaylistMaps.RemoveRange(mapRows);
			db.Playlists.Remove(row);
			try
			{
				await db.SaveChangesAsync();
			}
			catch (DbUpdateConcurrencyException)
			{
				return Result.Fail(FlagCodes.Stale, new { Version = (int?)null });
			}
			db.ChangeTracker.Clear();

			await Database.Moderation.AddAuditEntry(db, actor, ModerationActions.PlaylistDelete, AuditTargetKinds.Playlist, id, null, new { playlist_id = id, before = Fields(before), maps_before = MapsOf(before) });
			await tx.CommitAsync();
			return null;
		}
	}
}
