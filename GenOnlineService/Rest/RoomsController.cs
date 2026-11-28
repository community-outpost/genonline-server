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
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Nodes;

namespace GenOnlineService.Rest
{
	public sealed record RoomCreateRequest
	{
		public string Name { get; init; } = "";
		public long? ParentId { get; init; }
		public bool IsDefault { get; init; }
		public bool ShowAllMatches { get; init; }
	}

	// An omitted field keeps its value
	public sealed record RoomUpdateRequest
	{
		public string? Name { get; init; }
		public bool? Enabled { get; init; }
		public bool? IsDefault { get; init; }
		public bool? ShowAllMatches { get; init; }
	}

	// The ids are every sibling under the parent in their new order
	public sealed record RoomReorderRequest
	{
		public long? ParentId { get; init; }
		public IReadOnlyList<long> Ids { get; init; } = [];
	}

	public sealed record RoomResponse
	{
		public long Id { get; init; }
		public string Name { get; init; } = "";
		public long? ParentId { get; init; }
		public bool IsDefault { get; init; }
		public int Position { get; init; }
		public bool Enabled { get; init; }
		public bool ShowAllMatches { get; init; }
	}

	// Depth first, the siblings in their order, the disabled rooms included
	public sealed record RoomsResponse
	{
		public IReadOnlyList<RoomResponse> Rooms { get; init; } = [];
	}

	public static class RoomBodies
	{
		private static readonly string[] s_create = ["name", "parent_id", "is_default", "show_all_matches"];
		private static readonly string[] s_update = ["name", "enabled", "is_default", "show_all_matches"];
		private static readonly string[] s_reorder = ["parent_id", "ids"];

		private static JsonObject? Fields(JsonNode? body, string[] known, out Failure? failure)
		{
			failure = null;
			if (body is not JsonObject fields)
			{
				failure = Result.Invalid("body");
				return null;
			}

			string? unknown = fields.Select(f => f.Key).FirstOrDefault(key => !known.Contains(key));
			if (unknown != null)
			{
				failure = Result.Invalid(unknown);
				return null;
			}
			return fields;
		}

		private static bool TryText(JsonObject fields, string key, out string? value)
		{
			value = null;
			return !fields.ContainsKey(key) || (fields[key] is JsonValue node && node.TryGetValue(out value));
		}

		private static bool TryFlag(JsonObject fields, string key, out bool? value)
		{
			value = null;
			if (!fields.ContainsKey(key))
			{
				return true;
			}
			if (fields[key] is JsonValue node && node.TryGetValue(out bool flag))
			{
				value = flag;
				return true;
			}
			return false;
		}

		private static bool TryInt(JsonNode? node, out int value)
		{
			value = 0;
			return node is JsonValue number && number.TryGetValue(out value);
		}

		private static bool TryParent(JsonObject fields, out int? parent)
		{
			parent = null;
			if (!fields.ContainsKey("parent_id") || fields["parent_id"] == null)
			{
				return true;
			}
			bool ok = TryInt(fields["parent_id"], out int id);
			parent = ok ? id : null;
			return ok;
		}

		public static (RoomCreateRequest? Body, Failure? Failure) ParseCreate(JsonNode? body)
		{
			JsonObject? fields = Fields(body, s_create, out Failure? failure);
			if (fields == null)
			{
				return (null, failure);
			}
			if (!TryText(fields, "name", out string? name) || name == null && fields.ContainsKey("name"))
			{
				return (null, Result.Invalid("name"));
			}
			if (!TryParent(fields, out int? parent))
			{
				return (null, Result.Invalid("parent_id"));
			}
			if (!TryFlag(fields, "is_default", out bool? isDefault))
			{
				return (null, Result.Invalid("is_default"));
			}
			if (!TryFlag(fields, "show_all_matches", out bool? showAll))
			{
				return (null, Result.Invalid("show_all_matches"));
			}

			return (new RoomCreateRequest { Name = name ?? "", ParentId = parent, IsDefault = isDefault ?? false, ShowAllMatches = showAll ?? false }, null);
		}

		public static (RoomUpdateRequest? Body, Failure? Failure) ParseUpdate(JsonNode? body)
		{
			JsonObject? fields = Fields(body, s_update, out Failure? failure);
			if (fields == null)
			{
				return (null, failure);
			}
			if (!TryText(fields, "name", out string? name) || name == null && fields.ContainsKey("name"))
			{
				return (null, Result.Invalid("name"));
			}
			if (!TryFlag(fields, "enabled", out bool? enabled))
			{
				return (null, Result.Invalid("enabled"));
			}
			if (!TryFlag(fields, "is_default", out bool? isDefault))
			{
				return (null, Result.Invalid("is_default"));
			}
			if (!TryFlag(fields, "show_all_matches", out bool? showAll))
			{
				return (null, Result.Invalid("show_all_matches"));
			}

			return (new RoomUpdateRequest { Name = name, Enabled = enabled, IsDefault = isDefault, ShowAllMatches = showAll }, null);
		}

		public static (RoomReorderRequest? Body, Failure? Failure) ParseReorder(JsonNode? body)
		{
			JsonObject? fields = Fields(body, s_reorder, out Failure? failure);
			if (fields == null)
			{
				return (null, failure);
			}
			if (!TryParent(fields, out int? parent))
			{
				return (null, Result.Invalid("parent_id"));
			}
			if (fields["ids"] is not JsonArray list)
			{
				return (null, Result.Invalid("ids"));
			}

			List<long> ids = [];
			foreach (JsonNode? node in list)
			{
				if (!TryInt(node, out int id))
				{
					return (null, Result.Invalid("ids"));
				}
				ids.Add(id);
			}
			return (new RoomReorderRequest { ParentId = parent, Ids = ids }, null);
		}
	}

	// Every change is audited and reaches every copy and every connected client at once
	[ApiController]
	[AllowSignInCookie]
	[Authorize(Policy = PermissionPolicies.Prefix + Permissions.ChannelsManage)]
	public sealed class RoomsController : ControllerBase
	{
		private readonly IDbContextFactory<AppDbContext> m_dbFactory;

		public RoomsController(IDbContextFactory<AppDbContext> dbFactory)
		{
			m_dbFactory = dbFactory;
		}

		public static RoomResponse Describe(ChatRoomRow row) => new()
		{
			Id = row.ID,
			Name = row.Name,
			ParentId = row.ParentID,
			IsDefault = row.IsDefault,
			Position = row.Position,
			Enabled = row.Enabled,
			ShowAllMatches = row.ShowAllMatches
		};

		private static RoomsResponse Answer(IReadOnlyList<ChatRoomRow> rows) => new() { Rooms = RoomRules.Ordered(rows).Select(Describe).ToArray() };

		[EndpointSummary("Lists the chat rooms in order, the disabled ones included.")]
		[ProducesResponseType<RoomsResponse>(StatusCodes.Status200OK)]
		[HttpGet("~/api/v1/admin/rooms")]
		public async Task<IActionResult> List()
		{
			await using AppDbContext db = await m_dbFactory.CreateDbContextAsync();
			return RestResults.Ok(Answer(await db.ChatRooms.AsNoTracking().ToListAsync()));
		}

		[EndpointSummary("Adds a chat room at the end of its siblings; the first enabled child of a room becomes its default.")]
		[ProducesResponseType<RoomsResponse>(StatusCodes.Status201Created)]
		[ApiRequestBody(typeof(RoomCreateRequest))]
		[HttpPost("~/api/v1/admin/rooms")]
		public async Task<IActionResult> Create([FromBody] JsonNode? body)
		{
			(RoomCreateRequest? request, Failure? bad) = RoomBodies.ParseCreate(body);
			if (bad != null)
			{
				return RestResults.Fail(bad.Value);
			}

			return await ChangeAsync(
				rows => RoomEdits.Add(rows, request!.Name, (int?)request.ParentId, request.IsDefault, request.ShowAllMatches),
				ModerationActions.RoomCreate, StatusCodes.Status201Created);
		}

		[EndpointSummary("Renames a chat room, enables or disables it, makes it the default of its parent or sets whether it shows the matches of every room; a room is never deleted.")]
		[ProducesResponseType<RoomsResponse>(StatusCodes.Status200OK)]
		[ApiRequestBody(typeof(RoomUpdateRequest))]
		[HttpPatch("~/api/v1/admin/rooms/{id:int}")]
		public async Task<IActionResult> Update(int id, [FromBody] JsonNode? body)
		{
			(RoomUpdateRequest? request, Failure? bad) = RoomBodies.ParseUpdate(body);
			if (bad != null)
			{
				return RestResults.Fail(bad.Value);
			}

			return await ChangeAsync(
				rows => RoomEdits.Update(rows, id, request!.Name, request.Enabled, request.IsDefault, request.ShowAllMatches),
				ModerationActions.RoomUpdate, StatusCodes.Status200OK);
		}

		[EndpointSummary("Sets the order of the children of one room, or of the top level rooms when parent_id is null.")]
		[ProducesResponseType<RoomsResponse>(StatusCodes.Status200OK)]
		[ApiRequestBody(typeof(RoomReorderRequest))]
		[HttpPost("~/api/v1/admin/rooms/reorder")]
		public async Task<IActionResult> Reorder([FromBody] JsonNode? body)
		{
			(RoomReorderRequest? request, Failure? bad) = RoomBodies.ParseReorder(body);
			if (bad != null)
			{
				return RestResults.Fail(bad.Value);
			}

			return await ChangeAsync(
				rows => RoomEdits.Reorder(rows, (int?)request!.ParentId, request.Ids.Select(i => (int)i).ToArray()),
				ModerationActions.RoomUpdate, StatusCodes.Status200OK);
		}

		private async Task<IActionResult> ChangeAsync(Func<IReadOnlyList<ChatRoomRow>, RoomEdit> edit, string action, int status)
		{
			Database.ModerationActor actor = CallerActors.Of(User);
			await using AppDbContext db = await m_dbFactory.CreateDbContextAsync();

			try
			{
				await using var tx = await db.Database.BeginTransactionAsync();
				List<ChatRoomRow> current = await db.ChatRooms.AsTracking().ToListAsync();
				RoomEdit result = edit(current);
				if (result.Failure != null)
				{
					return RestResults.Fail(result.Failure.Value);
				}

				IReadOnlyList<(ChatRoomRow? Before, ChatRoomRow After)> changes = RoomEdits.Changes(current, result.Rows!);
				List<object> before = changes.Where(c => c.Before != null).Select(c => (object)Describe(c.Before!)).ToList();
				List<object> after = changes.Select(c => (object)Describe(c.After)).ToList();

				foreach ((ChatRoomRow? was, ChatRoomRow now) in changes)
				{
					if (was == null)
					{
						db.ChatRooms.Add(now);
					}
					else
					{
						ChatRoomRow row = current.First(r => r.ID == now.ID);
						row.Name = now.Name;
						row.IsDefault = now.IsDefault;
						row.Position = now.Position;
						row.Enabled = now.Enabled;
						row.ShowAllMatches = now.ShowAllMatches;
					}
				}

				if (changes.Count > 0)
				{
					await db.SaveChangesAsync();
					await Database.Moderation.AddAuditEntry(db, actor, action, AuditTargetKinds.Room, result.ID, null, new { before, after, promoted = result.Promoted });
				}
				await tx.CommitAsync();

				if (changes.Count > 0)
				{
					await CatalogSignals.ChangedAsync(db, CatalogResources.Channels, DateTime.UtcNow.Ticks);
				}
			}
			catch (DbUpdateException ex) when (DbHelpers.IsUniqueViolation(ex))
			{
				// another staff member added a room at the same moment
				return RestResults.Fail(RestCodes.Conflict);
			}

			List<ChatRoomRow> saved = await db.ChatRooms.AsNoTracking().ToListAsync();
			return status == StatusCodes.Status201Created ? RestResults.Created(Answer(saved)) : RestResults.Ok(Answer(saved));
		}
	}
}
