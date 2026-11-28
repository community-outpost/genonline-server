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

namespace GenOnlineService.OpenApi
{
	// The JSON body an action reads itself (ReadBodyAsync), for the document only: binding stays as it is
	[AttributeUsage(AttributeTargets.Method, Inherited = false)]
	public sealed class ApiRequestBodyAttribute : Attribute
	{
		public ApiRequestBodyAttribute(Type type)
		{
			Type = type;
		}

		public Type Type { get; }
	}

	// The permissions an action checks in its own code, any one of them is enough, for the document only.
	// Permissions of [Authorize(Policy = "perm:...")] are read from the policy and need no attribute
	[AttributeUsage(AttributeTargets.Method, Inherited = false)]
	public sealed class ApiPermissionAttribute : Attribute
	{
		public ApiPermissionAttribute(params string[] anyOf)
		{
			AnyOf = anyOf;
		}

		public IReadOnlyList<string> AnyOf { get; }
	}

	// A query parameter an action reads itself (Request.Query), for the document only
	[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
	public sealed class ApiQueryParameterAttribute : Attribute
	{
		public ApiQueryParameterAttribute(string name, string type, string description)
		{
			Name = name;
			Type = type;
			Description = description;
		}

		public string Name { get; }

		// integer, string or boolean
		public string Type { get; }

		public string Description { get; }

		public string? Format { get; init; }

		// long.MinValue leaves the bound out, since an attribute argument cannot be null
		public long Minimum { get; init; } = long.MinValue;
		public long Maximum { get; init; } = long.MinValue;

		public string[]? Enum { get; init; }

		public bool Explode { get; init; }

		public bool Deprecated { get; init; }
	}

	// The cursor, sort and filter parameters of the match feeds (MatchFeedArgs)
	[AttributeUsage(AttributeTargets.Method, Inherited = false)]
	public sealed class ApiFeedParametersAttribute : Attribute
	{
		public static readonly IReadOnlyList<ApiQueryParameterAttribute> Parameters =
		[
			new("cursor", "string", "`next_cursor` or `prev_cursor` of an earlier page, up to 512 characters. Only `per_page` may be sent with it."),
			new("per_page", "integer", "Matches per page, 20 by default.") { Minimum = 1, Maximum = GenOnlineService.Rest.OffsetPage.MaxPerPage },
			new("sort", "string", "`finished_at` (the default) or `id`. `id` allows only `player` and `order`.") { Enum = ["finished_at", "id"] },
			new("order", "string", "`desc` (the default) or `asc`.") { Enum = ["desc", "asc"] },
			new("player", "integer", "Up to 4 distinct user ids, comma separated or repeated. Every one of them played in the match.") { Minimum = 1, Explode = true },
			new("map", "string", "The exact map name, as `map.name` returns it, up to 128 characters. It compares case-sensitive and ordinal, character for character."),
			new("type", "string", "The lobby type.") { Enum = ["quick_match", "custom"] },
			new("rated", "boolean", "Whether the match tracked stats."),
			new("team_size", "integer", "The most players on one team, 1 for 1v1 or a free for all.") { Minimum = 1, Maximum = GenOnlineService.Rest.MatchFeedArgs.MaxTeamSize },
			new("desync", "boolean", "Whether a player reported a desync."),
			new("finished_after", "string", "Finished at or after this time, ISO 8601 with a time, UTC without an offset.") { Format = "date-time" },
			new("finished_before", "string", "Finished before this time, at or after `finished_after`.") { Format = "date-time" },
			new("duration_min", "integer", "The shortest duration in seconds.") { Minimum = 0, Maximum = GenOnlineService.Rest.MatchFeedArgs.MaxDurationS },
			new("duration_max", "integer", "The longest duration in seconds, at least `duration_min`.") { Minimum = 0, Maximum = GenOnlineService.Rest.MatchFeedArgs.MaxDurationS },
			new("faction", "integer", "The side the first player played. Needs `player`."),
			new("result", "string", "The result of the first player. Needs `player`.") { Enum = ["win", "loss"] },
		];
	}
}
