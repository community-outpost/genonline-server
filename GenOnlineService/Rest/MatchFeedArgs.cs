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
using Microsoft.Extensions.Primitives;
using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GenOnlineService.Rest
{
	public static class MatchFeedCodes
	{
		public const string InvalidCursor = "invalid_cursor";
		public const string InvalidParameter = "invalid_parameter";
		public const string UnknownParameter = "unknown_parameter";
		public const string MixedPaging = "mixed_paging";
		public const string FilterRequiresPlayer = "filter_requires_player";

		public static readonly IReadOnlyList<string> All = [InvalidCursor, InvalidParameter, UnknownParameter, MixedPaging, FilterRequiresPlayer];
	}

	// One read of a match feed: the query, and whether the caller sent a cursor
	public sealed record MatchFeedRequest(MatchFeedQuery Query, bool HadCursor);

	public static class MatchFeedArgs
	{
		public const int MaxPlayers = 4;
		public const int MaxCursorLength = 512;
		public const int MaxDurationS = 86400;
		public const int MaxTeamSize = 4;

		public const string Cursor = "cursor";
		public const string PerPage = "per_page";
		public const string Sort = "sort";
		public const string Order = "order";

		public static readonly IReadOnlyList<string> Filters =
			["player", "map", "type", "rated", "team_size", "desync", "finished_after", "finished_before", "duration_min", "duration_max", "faction", "result"];

		private const string TimeFormat = "yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'";

		// legacy names the paging parameters of the first REST release this route still answers, and a request without a cursor,
		// sort or filter ignores unknown parameters as that release did
		public static (bool Legacy, Failure? Failure) Classify(IQueryCollection query, IReadOnlyCollection<string> legacy)
		{
			bool isLegacy = query.Keys.Any(legacy.Contains);
			bool bNewStyle = query.Keys.Any(name => name == Cursor || name == Sort || name == Order || Filters.Contains(name));
			foreach (string name in query.Keys)
			{
				bool known = name == Cursor || name == Sort || name == Order || Filters.Contains(name);
				if (isLegacy && known)
				{
					return (false, Result.Fail(MatchFeedCodes.MixedPaging));
				}
				if (!isLegacy && bNewStyle && !known && name != PerPage)
				{
					return (false, Result.Fail(MatchFeedCodes.UnknownParameter, new { Parameter = name }));
				}
			}
			return (isLegacy, null);
		}

		// forcedPlayer leads the player list of the per-player route
		public static (MatchFeedRequest? Request, Failure? Failure) Read(IQueryCollection query, long? forcedPlayer)
		{
			int perPage = OffsetPage.DefaultPerPage;
			if (query.TryGetValue(PerPage, out StringValues perPageText) && (!int.TryParse(perPageText, NumberStyles.None, CultureInfo.InvariantCulture, out perPage) || perPage < 1 || perPage > OffsetPage.MaxPerPage))
			{
				return (null, Result.Invalid(PerPage));
			}

			if (query.TryGetValue(Cursor, out StringValues cursorText))
			{
				if (query.Keys.Any(name => name != Cursor && name != PerPage) || cursorText.Count != 1)
				{
					return (null, InvalidCursor());
				}
				MatchFeedQuery? decoded = Decode(cursorText.ToString(), perPage, forcedPlayer);
				return decoded == null ? (null, InvalidCursor()) : (new MatchFeedRequest(decoded, true), null);
			}

			Dictionary<string, StringValues> values = query.Where(pair => Filters.Contains(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value);
			(MatchFeedFilter? filter, Failure? failure) = ReadFilter(values, forcedPlayer);
			if (failure != null)
			{
				return (null, failure);
			}

			MatchFeedSort sort = MatchFeedSort.FinishedAt;
			if (query.TryGetValue(Sort, out StringValues sortText))
			{
				switch (Single(sortText))
				{
					case "finished_at": sort = MatchFeedSort.FinishedAt; break;
					case "id": sort = MatchFeedSort.Id; break;
					default: return (null, Invalid(Sort));
				}
			}
			bool descending = true;
			if (query.TryGetValue(Order, out StringValues orderText))
			{
				switch (Single(orderText))
				{
					case "desc": descending = true; break;
					case "asc": descending = false; break;
					default: return (null, Invalid(Order));
				}
			}
			if (sort == MatchFeedSort.Id && values.Keys.FirstOrDefault(name => name != "player") is string onlyById)
			{
				return (null, Invalid(onlyById));
			}

			return (new MatchFeedRequest(new MatchFeedQuery(filter!, sort, descending, perPage), false), null);
		}

		public static string Encode(MatchFeedQuery query, MatchFeedKey key, bool backward)
		{
			SortedDictionary<string, string> filter = Canonical(query.Filter);
			using MemoryStream stream = new MemoryStream();
			using (Utf8JsonWriter json = new Utf8JsonWriter(stream))
			{
				json.WriteStartObject();
				json.WriteNumber("v", 1);
				json.WriteString("s", query.Sort == MatchFeedSort.Id ? "i" : "f");
				json.WriteString("o", query.Descending ? "d" : "a");
				json.WriteString("dir", backward ? "p" : "n");
				json.WriteStartArray("k");
				if (query.Sort == MatchFeedSort.FinishedAt)
				{
					json.WriteStringValue(key.FinishedAt.ToString(TimeFormat, CultureInfo.InvariantCulture));
				}
				json.WriteNumberValue(key.Id);
				json.WriteEndArray();
				json.WriteString("q", HashOf(filter));
				json.WriteStartObject("f");
				foreach ((string name, string value) in filter)
				{
					json.WriteString(name, value);
				}
				json.WriteEndObject();
				json.WriteEndObject();
			}
			return Base64Url.EncodeToString(stream.ToArray());
		}

		private static MatchFeedQuery? Decode(string text, int perPage, long? forcedPlayer)
		{
			if (text.Length == 0 || text.Length > MaxCursorLength)
			{
				return null;
			}
			try
			{
				using JsonDocument document = JsonDocument.Parse(Base64Url.DecodeFromChars(text));
				JsonElement root = document.RootElement;
				if (root.GetProperty("v").GetInt32() != 1)
				{
					return null;
				}
				MatchFeedSort sort = root.GetProperty("s").GetString() switch { "f" => MatchFeedSort.FinishedAt, "i" => MatchFeedSort.Id, _ => throw new FormatException() };
				bool descending = root.GetProperty("o").GetString() switch { "d" => true, "a" => false, _ => throw new FormatException() };
				bool backward = root.GetProperty("dir").GetString() switch { "p" => true, "n" => false, _ => throw new FormatException() };

				JsonElement[] k = root.GetProperty("k").EnumerateArray().ToArray();
				MatchFeedKey key = sort == MatchFeedSort.Id && k.Length == 1
					? new MatchFeedKey(default, k[0].GetInt64())
					: sort == MatchFeedSort.FinishedAt && k.Length == 2
						? new MatchFeedKey(DateTime.ParseExact(k[0].GetString()!, TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal), k[1].GetInt64())
						: throw new FormatException();

				Dictionary<string, StringValues> values = root.GetProperty("f").EnumerateObject().ToDictionary(p => p.Name, p => new StringValues(p.Value.GetString()));
				if (values.Keys.Any(name => !Filters.Contains(name)) || (sort == MatchFeedSort.Id && values.Keys.Any(name => name != "player")))
				{
					return null;
				}
				(MatchFeedFilter? filter, Failure? failure) = ReadFilter(values, null);
				if (failure != null || root.GetProperty("q").GetString() != HashOf(Canonical(filter!)))
				{
					return null;
				}
				if (forcedPlayer != null && (filter!.Players.Count == 0 || filter.Players[0] != forcedPlayer))
				{
					return null;
				}
				return new MatchFeedQuery(filter!, sort, descending, perPage, key, backward);
			}
			catch (Exception ex) when (ex is FormatException or JsonException or KeyNotFoundException or InvalidOperationException or OverflowException or ArgumentException)
			{
				return null;
			}
		}

		private static (MatchFeedFilter? Filter, Failure? Failure) ReadFilter(IReadOnlyDictionary<string, StringValues> values, long? forcedPlayer)
		{
			List<long> players = forcedPlayer is long forced ? [forced] : [];
			if (values.TryGetValue("player", out StringValues playerText))
			{
				foreach (string part in playerText.SelectMany(text => (text ?? "").Split(',')))
				{
					if (!long.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out long player) || player <= 0 || players.Contains(player))
					{
						return (null, Invalid("player"));
					}
					players.Add(player);
				}
			}
			if (players.Count > MaxPlayers)
			{
				return (null, Invalid("player"));
			}

			MatchFeedFilter filter = new MatchFeedFilter { Players = players };
			foreach ((string name, StringValues raw) in values)
			{
				if (name == "player")
				{
					continue;
				}
				string? text = Single(raw);
				MatchFeedFilter? next = text == null ? null : name switch
				{
					"map" => text.Length is >= 1 and <= global::Match.MapNameMaxLength ? filter with { Map = text } : null,
					"type" => text switch { "quick_match" => filter with { Type = ELobbyType.QuickMatch }, "custom" => filter with { Type = ELobbyType.CustomGame }, _ => null },
					"rated" => Bool(text) is bool rated ? filter with { Rated = rated } : null,
					"desync" => Bool(text) is bool desync ? filter with { Desync = desync } : null,
					"team_size" => Int(text, 1, MaxTeamSize) is int size ? filter with { TeamSize = (short)size } : null,
					"finished_after" => Time(text) is DateTime after ? filter with { FinishedAfter = after } : null,
					"finished_before" => Time(text) is DateTime before ? filter with { FinishedBefore = before } : null,
					"duration_min" => Int(text, 0, MaxDurationS) is int min ? filter with { DurationMin = min } : null,
					"duration_max" => Int(text, 0, MaxDurationS) is int max ? filter with { DurationMax = max } : null,
					"faction" => int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int side) ? filter with { Side = side } : null,
					"result" => text is MatchResults.Win or MatchResults.Loss ? filter with { Result = text } : null,
					_ => null
				};
				if (next == null)
				{
					return (null, Invalid(name));
				}
				filter = next;
			}

			if (filter.FinishedAfter > filter.FinishedBefore)
			{
				return (null, Invalid("finished_before"));
			}
			if (filter.DurationMin > filter.DurationMax)
			{
				return (null, Invalid("duration_max"));
			}
			if (players.Count == 0 && (filter.Side != null || filter.Result != null))
			{
				return (null, Result.Fail(MatchFeedCodes.FilterRequiresPlayer, new { Parameter = filter.Side != null ? "faction" : "result" }));
			}
			return (filter, null);
		}

		private static SortedDictionary<string, string> Canonical(MatchFeedFilter filter)
		{
			SortedDictionary<string, string> values = new SortedDictionary<string, string>(StringComparer.Ordinal);
			if (filter.Players.Count > 0) values["player"] = String.Join(",", filter.Players.Select(p => p.ToString(CultureInfo.InvariantCulture)));
			if (filter.Map != null) values["map"] = filter.Map;
			if (filter.Type != null) values["type"] = filter.Type == ELobbyType.QuickMatch ? "quick_match" : "custom";
			if (filter.Rated != null) values["rated"] = filter.Rated.Value ? "true" : "false";
			if (filter.Desync != null) values["desync"] = filter.Desync.Value ? "true" : "false";
			if (filter.TeamSize != null) values["team_size"] = filter.TeamSize.Value.ToString(CultureInfo.InvariantCulture);
			if (filter.FinishedAfter != null) values["finished_after"] = filter.FinishedAfter.Value.ToString(TimeFormat, CultureInfo.InvariantCulture);
			if (filter.FinishedBefore != null) values["finished_before"] = filter.FinishedBefore.Value.ToString(TimeFormat, CultureInfo.InvariantCulture);
			if (filter.DurationMin != null) values["duration_min"] = filter.DurationMin.Value.ToString(CultureInfo.InvariantCulture);
			if (filter.DurationMax != null) values["duration_max"] = filter.DurationMax.Value.ToString(CultureInfo.InvariantCulture);
			if (filter.Side != null) values["faction"] = filter.Side.Value.ToString(CultureInfo.InvariantCulture);
			if (filter.Result != null) values["result"] = filter.Result;
			return values;
		}

		private static string HashOf(SortedDictionary<string, string> filter)
		{
			string canonical = JsonSerializer.Serialize(filter);
			return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).Substring(0, 16);
		}

		private static Failure Invalid(string name) => Result.Fail(MatchFeedCodes.InvalidParameter, new { Parameter = name });

		private static Failure InvalidCursor() => Result.Fail(MatchFeedCodes.InvalidCursor);

		private static string? Single(StringValues values) => values.Count == 1 ? values[0] : null;

		private static bool? Bool(string text) => text switch { "true" => true, "false" => false, _ => null };

		private static int? Int(string text, int min, int max) =>
			int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int value) && value >= min && value <= max ? value : null;

		// The parser of since, cut to the whole microseconds a cursor carries
		private static DateTime? Time(string text) =>
			text.Contains('T') && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset parsed) ? parsed.UtcDateTime.AddTicks(-(parsed.UtcDateTime.Ticks % 10)) : null;
	}
}
