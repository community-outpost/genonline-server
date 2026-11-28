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
using GenOnlineService.Catalog;
using GenOnlineService.OpenApi;
using GenOnlineService.Realtime;
using GenOnlineService.Rest;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using System.Globalization;
using System.Text.Json;

namespace GenOnlineService.Playlists
{
	// Every field is optional on an update and keeps its value when left out
	public sealed record MapRequest
	{
		public string? Name { get; init; }
		public string? Path { get; init; }
		public bool? Custom { get; init; }
		public int? MaxPlayers { get; init; }
		public bool? Enabled { get; init; }

		public MapDraft ApplyTo(MapDraft current) => current with
		{
			Name = Name?.Trim() ?? current.Name,
			Path = Path?.Trim() ?? current.Path,
			Custom = Custom ?? current.Custom,
			MaxPlayers = MaxPlayers ?? current.MaxPlayers,
			Enabled = Enabled ?? current.Enabled
		};
	}

	public sealed record PlaylistRequest
	{
		public string? Name { get; init; }
		public bool? Enabled { get; init; }
		public int? MinPlayers { get; init; }
		public int? DesiredPlayers { get; init; }
		public int? MinSelectedMaps { get; init; }
		public bool? AllowTeams { get; init; }

		// -1 means no teams
		public int? TeamSize { get; init; }

		public bool? AllowArmySelection { get; init; }
		public int? GracePeriodS { get; init; }

		// only on create
		public List<MapRequest>? Maps { get; init; }

		public PlaylistDraft ApplyTo(PlaylistDraft current) => current with
		{
			Name = Name?.Trim() ?? current.Name,
			Enabled = Enabled ?? current.Enabled,
			MinPlayers = MinPlayers ?? current.MinPlayers,
			DesiredPlayers = DesiredPlayers ?? current.DesiredPlayers,
			MinSelectedMaps = MinSelectedMaps ?? current.MinSelectedMaps,
			AllowTeams = AllowTeams ?? current.AllowTeams,
			TeamSize = TeamSize ?? current.TeamSize,
			AllowArmySelection = AllowArmySelection ?? current.AllowArmySelection,
			// outside 0..65 seconds the draft carries a value the rules refuse
			GracePeriodMs = GracePeriodS is null ? current.GracePeriodMs : GracePeriodS is >= 0 and <= PlaylistRules.MaximumGracePeriodMs / 1000 ? GracePeriodS.Value * 1000 : -1
		};
	}

	public sealed record MapOrderRequest
	{
		public List<long>? MapIds { get; init; }
	}

	public sealed record PlaylistMapResponse
	{
		public long Id { get; init; }

		// the index the game client sends, null while the map is disabled
		public int? Index { get; init; }

		public string Name { get; init; } = String.Empty;
		public string Path { get; init; } = String.Empty;
		public bool Custom { get; init; }
		public int MaxPlayers { get; init; }
		public bool Enabled { get; init; }
	}

	public sealed record PlaylistResponse
	{
		public int Id { get; init; }
		public string Name { get; init; } = String.Empty;
		public bool Enabled { get; init; }
		public int MinPlayers { get; init; }
		public int DesiredPlayers { get; init; }
		public int MinSelectedMaps { get; init; }
		public bool AllowTeams { get; init; }
		public int TeamSize { get; init; }
		public bool AllowArmySelection { get; init; }
		public int GracePeriodS { get; init; }
		public int Version { get; init; }
		public string UpdatedBy { get; init; } = String.Empty;
		public DateTime UpdatedAt { get; init; }

		// whether the queue offers it right now, an enabled playlist that breaks a rule is not offered
		public bool Offered { get; init; }

		public IReadOnlyList<PlaylistMapResponse> Maps { get; init; } = [];
	}

	// The one rating every playlist shares, read from the same settings the match results use
	public sealed record PlaylistRatingResponse
	{
		public int BaseRating { get; init; } = EloConfig.BaseRating;
		public int KFactor { get; init; } = EloConfig.KFactor;
		public int ExpansionStep { get; init; } = EloConfig.EloExpansionValue_Standard;
		public int ExpansionStepHigh { get; init; } = EloConfig.EloExpansionValue_HighELO;
		public int ExpansionIntervalS { get; init; } = EloConfig.SecondsBetweenEloExpansionsInMatchmaking;
		public int HighEloThreshold { get; init; } = EloConfig.HighEloThreshold;
	}

	public sealed record PlaylistListResponse
	{
		public PlaylistRatingResponse Rating { get; init; } = new();
		public IReadOnlyList<PlaylistResponse> Playlists { get; init; } = [];
	}

	[ApiController]
	[AllowSignInCookie]
	[Authorize(Policy = PermissionPolicies.Prefix + Permissions.PlaylistsManage)]
	public sealed class PlaylistsController : ControllerBase
	{
		private readonly IDbContextFactory<AppDbContext> m_dbFactory;

		public PlaylistsController(IDbContextFactory<AppDbContext> dbFactory)
		{
			m_dbFactory = dbFactory;
		}

		public static PlaylistResponse Describe(PlaylistDraft draft)
		{
			int index = 0;
			return new PlaylistResponse
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
				GracePeriodS = (draft.GracePeriodMs + 999) / 1000,
				Version = draft.Version,
				UpdatedBy = draft.UpdatedBy,
				UpdatedAt = DateTime.SpecifyKind(draft.UpdatedAt, DateTimeKind.Utc),
				Offered = PlaylistCatalog.Current.Playlists.ContainsKey((UInt16)draft.Id),
				Maps = draft.Maps.Select(map => new PlaylistMapResponse
				{
					Id = map.Id,
					Index = map.Enabled ? index++ : null,
					Name = map.Name,
					Path = map.Path,
					Custom = map.Custom,
					MaxPlayers = map.MaxPlayers,
					Enabled = map.Enabled
				}).ToList()
			};
		}

		[EndpointSummary("Lists the matchmaking playlists with their maps, enabled or not.")]
		[ProducesResponseType<PlaylistListResponse>(StatusCodes.Status200OK)]
		[HttpGet("~/api/v1/admin/playlists")]
		public async Task<IActionResult> List()
		{
			await using AppDbContext db = await m_dbFactory.CreateDbContextAsync();
			return Ok(new PlaylistListResponse { Playlists = (await PlaylistStore.LoadAll(db)).Select(Describe).ToList() });
		}

		[EndpointSummary("Adds a playlist, disabled unless the body says otherwise.")]
		[ProducesResponseType<PlaylistResponse>(StatusCodes.Status201Created)]
		[ApiRequestBody(typeof(PlaylistRequest))]
		[HttpPost("~/api/v1/admin/playlists")]
		public async Task<IActionResult> Create()
		{
			(PlaylistRequest? body, Failure? bad) = await ReadAsync<PlaylistRequest>();
			if (bad != null) return RestResults.Fail(bad.Value);

			PlaylistDraft start = new(0, String.Empty, false, 2, 2, 1, false, -1, false, 0, (body!.Maps ?? []).Select(map => map.ApplyTo(new MapDraft(String.Empty, String.Empty, true, 2))).ToList());
			await using AppDbContext db = await m_dbFactory.CreateDbContextAsync();
			(PlaylistDraft? created, Failure? failure) = await PlaylistStore.Create(db, body.ApplyTo(start), CallerActors.Of(User));
			return await Answer(db, created, failure, StatusCodes.Status201Created);
		}

		[EndpointSummary("Reads one playlist; the ETag is its version.")]
		[ProducesResponseType<PlaylistResponse>(StatusCodes.Status200OK)]
		[HttpGet("~/api/v1/admin/playlists/{id:int}")]
		public async Task<IActionResult> Get(int id)
		{
			await using AppDbContext db = await m_dbFactory.CreateDbContextAsync();
			PlaylistDraft? playlist = await PlaylistStore.Load(db, id);
			if (playlist == null) return RestResults.NotFound("playlist");
			Response.Headers[HeaderNames.ETag] = ETagOf(playlist.Version);
			return Ok(Describe(playlist));
		}

		[EndpointSummary("Changes the fields of one playlist and leaves its maps alone; If-Match takes its version.")]
		[ProducesResponseType<PlaylistResponse>(StatusCodes.Status200OK)]
		[ApiRequestBody(typeof(PlaylistRequest))]
		[HttpPut("~/api/v1/admin/playlists/{id:int}")]
		public async Task<IActionResult> Update(int id)
		{
			(int? expected, Failure? badTag) = ReadIfMatchVersion();
			if (badTag != null) return RestResults.Fail(badTag.Value);
			(PlaylistRequest? body, Failure? bad) = await ReadAsync<PlaylistRequest>();
			if (bad != null) return RestResults.Fail(bad.Value);

			await using AppDbContext db = await m_dbFactory.CreateDbContextAsync();
			(PlaylistDraft? updated, Failure? failure) = await PlaylistStore.Update(db, id, expected, body!.ApplyTo, CallerActors.Of(User));
			return await Answer(db, updated, failure);
		}

		[EndpointSummary("Deletes a playlist with its maps; the only enabled playlist stays, disable it to pause the quick match.")]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
		[HttpDelete("~/api/v1/admin/playlists/{id:int}")]
		public async Task<IActionResult> Delete(int id)
		{
			(int? expected, Failure? badTag) = ReadIfMatchVersion();
			if (badTag != null) return RestResults.Fail(badTag.Value);

			await using AppDbContext db = await m_dbFactory.CreateDbContextAsync();
			Failure? failure = await PlaylistStore.Delete(db, id, expected, CallerActors.Of(User));
			if (failure != null) return RestResults.Fail(failure.Value);
			await Signal(db);
			return NoContent();
		}

		[EndpointSummary("Adds a map at the end of a playlist.")]
		[ProducesResponseType<PlaylistResponse>(StatusCodes.Status200OK)]
		[ApiRequestBody(typeof(MapRequest))]
		[HttpPost("~/api/v1/admin/playlists/{id:int}/maps")]
		public async Task<IActionResult> AddMap(int id)
		{
			(int? expected, Failure? badTag) = ReadIfMatchVersion();
			if (badTag != null) return RestResults.Fail(badTag.Value);
			(MapRequest? body, Failure? bad) = await ReadAsync<MapRequest>();
			if (bad != null) return RestResults.Fail(bad.Value);

			return await ChangeMaps(id, expected, "add", maps =>
			{
				maps.Add(body!.ApplyTo(new MapDraft(String.Empty, String.Empty, true, 2)));
				return null;
			});
		}

		[EndpointSummary("Changes one map of a playlist; the id stays when the map is renamed or moved.")]
		[ProducesResponseType<PlaylistResponse>(StatusCodes.Status200OK)]
		[ApiRequestBody(typeof(MapRequest))]
		[HttpPut("~/api/v1/admin/playlists/{id:int}/maps/{mapId:long}")]
		public async Task<IActionResult> UpdateMap(int id, long mapId)
		{
			(int? expected, Failure? badTag) = ReadIfMatchVersion();
			if (badTag != null) return RestResults.Fail(badTag.Value);
			(MapRequest? body, Failure? bad) = await ReadAsync<MapRequest>();
			if (bad != null) return RestResults.Fail(bad.Value);

			return await ChangeMaps(id, expected, "update", maps =>
			{
				int at = maps.FindIndex(map => map.Id == mapId);
				if (at < 0) return Result.NotFound("map");
				maps[at] = body!.ApplyTo(maps[at]);
				return null;
			});
		}

		[EndpointSummary("Removes one map from a playlist.")]
		[ProducesResponseType<PlaylistResponse>(StatusCodes.Status200OK)]
		[HttpDelete("~/api/v1/admin/playlists/{id:int}/maps/{mapId:long}")]
		public async Task<IActionResult> RemoveMap(int id, long mapId)
		{
			(int? expected, Failure? badTag) = ReadIfMatchVersion();
			if (badTag != null) return RestResults.Fail(badTag.Value);

			return await ChangeMaps(id, expected, "remove", maps => maps.RemoveAll(map => map.Id == mapId) == 0 ? Result.NotFound("map") : null);
		}

		[EndpointSummary("Sets the order of the maps of a playlist; map_ids lists every map id of it once.")]
		[ProducesResponseType<PlaylistResponse>(StatusCodes.Status200OK)]
		[ApiRequestBody(typeof(MapOrderRequest))]
		[HttpPut("~/api/v1/admin/playlists/{id:int}/maps/order")]
		public async Task<IActionResult> ReorderMaps(int id)
		{
			(int? expected, Failure? badTag) = ReadIfMatchVersion();
			if (badTag != null) return RestResults.Fail(badTag.Value);
			(MapOrderRequest? body, Failure? bad) = await ReadAsync<MapOrderRequest>();
			if (bad != null) return RestResults.Fail(bad.Value);

			return await ChangeMaps(id, expected, "reorder", maps =>
			{
				List<long> order = body!.MapIds ?? [];
				if (order.Count != maps.Count || order.Distinct().Count() != order.Count || maps.Any(map => !order.Contains(map.Id)))
				{
					return Result.Invalid("map_ids");
				}
				Dictionary<long, MapDraft> byId = maps.ToDictionary(map => map.Id);
				maps.Clear();
				maps.AddRange(order.Select(mapId => byId[mapId]));
				return null;
			});
		}

		private async Task<IActionResult> ChangeMaps(int id, int? expected, string change, Func<List<MapDraft>, Failure?> edit)
		{
			await using AppDbContext db = await m_dbFactory.CreateDbContextAsync();
			(PlaylistDraft? changed, Failure? failure) = await PlaylistStore.ChangeMaps(db, id, expected, change, edit, CallerActors.Of(User));
			return await Answer(db, changed, failure);
		}

		private async Task<IActionResult> Answer(AppDbContext db, PlaylistDraft? playlist, Failure? failure, int status = StatusCodes.Status200OK)
		{
			if (failure != null) return RestResults.Fail(failure.Value);
			await Signal(db);
			Response.Headers[HeaderNames.ETag] = ETagOf(playlist!.Version);
			return new JsonResult(Describe(playlist), ApiResults.Json) { StatusCode = status };
		}

		// Reloads this copy and tells the others, so the change holds without a redeploy
		private static Task Signal(AppDbContext db) => CatalogSignals.ChangedAsync(db, CatalogResources.Playlists, DateTime.UtcNow.Ticks);

		private new IActionResult Ok(object body) => new JsonResult(body, ApiResults.Json) { StatusCode = StatusCodes.Status200OK };

		private static string ETagOf(int version) => "\"" + version.ToString(CultureInfo.InvariantCulture) + "\"";

		// If-Match "<version>" for one playlist, where an absent header means no check
		private (int? Version, Failure? Failure) ReadIfMatchVersion()
		{
			string value = Request.Headers[HeaderNames.IfMatch].ToString();
			if (String.IsNullOrWhiteSpace(value) || value.Trim() == "*")
			{
				return (null, null);
			}
			string inner = value.Trim();
			inner = (inner.StartsWith("W/", StringComparison.Ordinal) ? inner[2..] : inner).Trim('"');
			return Int32.TryParse(inner, NumberStyles.None, CultureInfo.InvariantCulture, out int version) ? (version, null) : (null, Result.Invalid("If-Match"));
		}

		private async Task<(T? Body, Failure? Failure)> ReadAsync<T>() where T : class
		{
			try
			{
				using var reader = new StreamReader(Request.Body);
				string text = await reader.ReadToEndAsync();
				T? body = String.IsNullOrWhiteSpace(text) ? null : JsonSerializer.Deserialize<T>(text, ApiResults.Json);
				return body == null ? (null, Result.Invalid("body")) : (body, null);
			}
			catch (JsonException ex)
			{
				string field = ex.Path is string path && path.StartsWith("$.", StringComparison.Ordinal) ? path.Substring(2) : "body";
				return (null, Result.Invalid(field));
			}
		}
	}
}
