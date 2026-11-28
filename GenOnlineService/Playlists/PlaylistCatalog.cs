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
using System.Globalization;
using System.Text;

namespace GenOnlineService.Playlists
{
	public sealed record MapDraft(string Name, string Path, bool Custom, int MaxPlayers, bool Enabled = true, long Id = 0);

	public sealed record PlaylistDraft(int Id, string Name, bool Enabled, int MinPlayers, int DesiredPlayers, int MinSelectedMaps, bool AllowTeams, int TeamSize,
		bool AllowArmySelection, int GracePeriodMs, IReadOnlyList<MapDraft> Maps, int Version = 1, string UpdatedBy = "", DateTime UpdatedAt = default);

	// Returns the name of the first field that breaks a rule, as the API names it, or null
	public static class PlaylistRules
	{
		public const int MaximumNameLength = 64;
		public const int MaximumMapNameLength = 128;
		public const int MaximumPathLength = 128;
		public const int MinimumMapPlayers = 2;
		public const int MaximumMapPlayers = 12;
		public const int MaximumTeamSize = 6;
		public const int MaximumGracePeriodMs = UInt16.MaxValue;

		private const string ForbiddenInPath = "\\/:*?\"<>|";

		public static string? Check(PlaylistDraft playlist)
		{
			if (playlist.Id < 0 || playlist.Id > UInt16.MaxValue) return "id";
			if (!NameFits(playlist.Name, MaximumNameLength)) return "name";
			if (playlist.MinPlayers < 2) return "min_players";
			if (playlist.DesiredPlayers > Lobby.MaxLobbySize || playlist.DesiredPlayers < playlist.MinPlayers) return "desired_players";
			if (playlist.MinSelectedMaps < 1) return "min_selected_maps";
			if (playlist.TeamSize != -1 && (playlist.TeamSize < 1 || playlist.TeamSize > MaximumTeamSize)) return "team_size";
			if (playlist.GracePeriodMs < 0 || playlist.GracePeriodMs > MaximumGracePeriodMs) return "grace_period_s";

			for (int i = 0; i < playlist.Maps.Count; i++)
			{
				string? field = CheckMap(playlist.Maps[i]);
				if (field != null) return "maps[" + i.ToString(CultureInfo.InvariantCulture) + "]." + field;
			}

			if (!playlist.Enabled) return null;

			if (playlist.MinSelectedMaps > playlist.Maps.Count(map => map.Enabled)) return "min_selected_maps";
			for (int i = 0; i < playlist.Maps.Count; i++)
			{
				MapDraft map = playlist.Maps[i];
				if (map.Enabled && (map.MaxPlayers < playlist.MinPlayers || map.MaxPlayers > playlist.DesiredPlayers))
				{
					return "maps[" + i.ToString(CultureInfo.InvariantCulture) + "].max_players";
				}
			}

			// a formation of any allowed size needs a map of exactly that size
			for (int size = playlist.MinPlayers; size <= playlist.DesiredPlayers; size++)
			{
				if (!playlist.Maps.Any(map => map.Enabled && map.MaxPlayers == size)) return "maps";
			}
			return null;
		}

		public static string? CheckMap(MapDraft map)
		{
			if (!NameFits(map.Name, MaximumMapNameLength)) return "name";
			if (!PathFits(map.Path)) return "path";
			if (map.MaxPlayers < MinimumMapPlayers || map.MaxPlayers > MaximumMapPlayers) return "max_players";
			return null;
		}

		private static bool NameFits(string name, int maximum) => name.Length >= 1 && name.Length <= maximum && name == name.Trim() && !name.Any(Char.IsControl);

		// A stem with a separator, a .map suffix or a trailing dot would make the client build a different file than the one meant
		private static bool PathFits(string path) =>
			path.Length >= 1 && path.Length <= MaximumPathLength && path == path.Trim() && !path.EndsWith('.') && !path.EndsWith(".map", StringComparison.OrdinalIgnoreCase) && !path.Any(c => Char.IsControl(c) || ForbiddenInPath.Contains(c));
	}

	// The playlists as the matchmaking reads them: enabled ones only, with their enabled maps in order, so a map index counts enabled maps
	public sealed class PlaylistSnapshot
	{
		public static readonly PlaylistSnapshot Empty = new(new Dictionary<UInt16, global::Playlist>(), new Dictionary<UInt16, string>(), String.Empty);

		public IReadOnlyDictionary<UInt16, global::Playlist> Playlists { get; }

		// Everything that decides which queue entries still hold, a rename is not part of it
		private readonly IReadOnlyDictionary<UInt16, string> m_signatures;

		public string Fingerprint { get; }

		private PlaylistSnapshot(IReadOnlyDictionary<UInt16, global::Playlist> playlists, IReadOnlyDictionary<UInt16, string> signatures, string fingerprint)
		{
			Playlists = playlists;
			m_signatures = signatures;
			Fingerprint = fingerprint;
		}

		public static PlaylistSnapshot From(IEnumerable<PlaylistDraft> drafts, ILogger? log = null)
		{
			var playlists = new Dictionary<UInt16, global::Playlist>();
			var signatures = new Dictionary<UInt16, string>();
			var fingerprint = new StringBuilder();
			foreach (PlaylistDraft draft in drafts.Where(d => d.Enabled).OrderBy(d => d.Id))
			{
				string? field = PlaylistRules.Check(draft);
				if (field != null)
				{
					log?.LogError("Disabling matchmaking playlist {PlaylistId} ({PlaylistName}): {Field} breaks a rule", draft.Id, draft.Name, field);
					continue;
				}

				List<MapDraft> maps = draft.Maps.Where(map => map.Enabled).ToList();
				UInt16 id = (UInt16)draft.Id;
				playlists[id] = new global::Playlist(id, draft.Name, draft.MinPlayers, draft.DesiredPlayers, draft.MinSelectedMaps, draft.AllowTeams, draft.TeamSize,
					draft.AllowArmySelection, (UInt16)draft.GracePeriodMs, maps.Select(map => new PlaylistMap(map.Name, map.Path, map.Custom, map.MaxPlayers)).ToList());

				var signature = new StringBuilder();
				signature.Append(draft.MinPlayers).Append('|').Append(draft.DesiredPlayers).Append('|').Append(draft.MinSelectedMaps).Append('|').Append(draft.AllowTeams).Append('|')
					.Append(draft.TeamSize).Append('|').Append(draft.AllowArmySelection).Append('|').Append(draft.GracePeriodMs);
				foreach (MapDraft map in maps)
				{
					signature.Append('\n').Append(map.Path).Append('|').Append(map.MaxPlayers);
				}
				signatures[id] = signature.ToString();

				fingerprint.Append(id).Append('|').Append(draft.Name).Append('|').Append(signature).Append('|');
				foreach (MapDraft map in maps)
				{
					fingerprint.Append(map.Name).Append('|').Append(map.Custom).Append('|');
				}
				fingerprint.Append('\n');
			}
			return new PlaylistSnapshot(playlists, signatures, fingerprint.ToString());
		}

		// The playlists a queue entry may no longer hold: gone from the offer, or with other maps or player counts
		public static IReadOnlySet<UInt16> ChangedIds(PlaylistSnapshot before, PlaylistSnapshot after)
		{
			var changed = new HashSet<UInt16>();
			foreach ((UInt16 id, string signature) in before.m_signatures)
			{
				if (!after.m_signatures.TryGetValue(id, out string? next) || next != signature)
				{
					changed.Add(id);
				}
			}
			return changed;
		}
	}

	// Reloaded at startup, on a staff change, on a backplane message and on the reconcile tick (safety net)
	public static class PlaylistCatalog
	{
		private static readonly ILogger s_log = AppLog.For(typeof(PlaylistCatalog));

		private static volatile PlaylistSnapshot s_current = PlaylistSnapshot.Empty;

		public static PlaylistSnapshot Current => s_current;

		public static async Task Reload(AppDbContext db)
		{
			PlaylistSnapshot next = PlaylistSnapshot.From(await PlaylistStore.LoadAll(db), s_log);
			IReadOnlySet<UInt16> changed = Install(next);
			if (changed.Count > 0)
			{
				await MatchmakingManager.CancelQueuedAsync(changed);
			}
		}

		// Returns the playlists whose queue entries no longer hold
		internal static IReadOnlySet<UInt16> Install(PlaylistSnapshot next)
		{
			PlaylistSnapshot before = s_current;
			if (before.Fingerprint == next.Fingerprint)
			{
				return new HashSet<UInt16>();
			}

			s_current = next;
			s_log.LogInformation("Matchmaking playlists changed, {Count} are on offer", next.Playlists.Count);
			return PlaylistSnapshot.ChangedIds(before, next);
		}
	}

	public static class PlaylistMapRefs
	{
		// A custom map is one the players download, so only a retail map is official
		public static MapRef Of(PlaylistMap map) => new() { Name = map.Name, Path = map.Path + ".map", Official = !map.Custom };
	}
}
