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

using GenOnlineService.Catalog;
using GenOnlineService.Realtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;
using System.Text.Json.Serialization;

// A room is disabled and never deleted, its id stays reserved because messages, sessions and lobbies refer to it
public class ChatRoomRow
{
	public Int16 ID { get; set; }
	public string Name { get; set; } = String.Empty;
	public Int16? ParentID { get; set; }
	public bool IsDefault { get; set; }

	// the order among the siblings
	public int Position { get; set; }
	public bool Enabled { get; set; } = true;
	public bool ShowAllMatches { get; set; }
}

public class ChatRoomConfiguration : IEntityTypeConfiguration<ChatRoomRow>
{
	public void Configure(EntityTypeBuilder<ChatRoomRow> builder)
	{
		builder.ToTable("chat_rooms");
		builder.HasKey(e => e.ID);
		builder.Property(e => e.ID).HasColumnName("id").ValueGeneratedNever();
		builder.Property(e => e.Name).HasColumnName("name").HasMaxLength(GenOnlineService.Catalog.RoomRules.MaximumNameLength);
		builder.Property(e => e.ParentID).HasColumnName("parent_id");
		builder.Property(e => e.IsDefault).HasColumnName("is_default");
		builder.Property(e => e.Position).HasColumnName("position");
		builder.Property(e => e.Enabled).HasColumnName("enabled");
		builder.Property(e => e.ShowAllMatches).HasColumnName("show_all_matches");
	}
}

public abstract partial class AppDbContext
{
	public DbSet<ChatRoomRow> ChatRooms => Set<ChatRoomRow>();

	partial void ConfigureChannels(ModelBuilder modelBuilder) => modelBuilder.ApplyConfiguration(new ChatRoomConfiguration());
}

namespace GenOnlineService.Catalog
{
	public static partial class CatalogStartup
	{
		static partial void RegisterChannels() => CatalogSignals.Register(CatalogResources.Channels, RoomCatalog.ReloadAsync);

		static partial void AddChannelSeed(List<Func<AppDbContext, Task>> seeds) => seeds.Add(ChatRoomSeed.EnsureAsync);
	}

	public static class RoomRules
	{
		public const int MaximumNameLength = 32;

		// The field name of the first broken rule
		public static Failure? Check(IReadOnlyList<ChatRoomRow> rows)
		{
			HashSet<int> ids = [];
			foreach (ChatRoomRow row in rows)
			{
				if (row.ID < 0 || row.ID >= Int16.MaxValue || !ids.Add(row.ID))
				{
					return Result.Invalid("id");
				}
			}

			foreach (ChatRoomRow row in rows)
			{
				if (String.IsNullOrWhiteSpace(row.Name) || row.Name.Length > MaximumNameLength)
				{
					return Result.Invalid("name");
				}
			}

			Dictionary<int, ChatRoomRow> byID = rows.ToDictionary(r => (int)r.ID);
			foreach (ChatRoomRow row in rows)
			{
				if (row.ParentID is not Int16 parent)
				{
					continue;
				}

				int at = parent;
				for (int steps = 0; ; steps++)
				{
					if (at == row.ID || steps > rows.Count || !byID.TryGetValue(at, out ChatRoomRow? up))
					{
						return Result.Invalid("parent_id");
					}
					if (up.ParentID is not Int16 next)
					{
						break;
					}
					at = next;
				}
			}

			if (rows.Any(r => r.ParentID == null && r.IsDefault))
			{
				return Result.Invalid("is_default");
			}

			foreach (IGrouping<Int16?, ChatRoomRow> children in rows.Where(r => r.ParentID != null).GroupBy(r => r.ParentID))
			{
				if (children.Any(r => !r.Enabled && r.IsDefault))
				{
					return Result.Invalid("is_default");
				}
				if (children.Any(r => r.Enabled) && children.Count(r => r.Enabled && r.IsDefault) != 1)
				{
					return Result.Invalid("is_default");
				}
			}

			return null;
		}

		// Depth first, the siblings by position and then id, as the ids of the first catalog ran
		public static List<ChatRoomRow> Ordered(IReadOnlyList<ChatRoomRow> rows)
		{
			ILookup<Int16?, ChatRoomRow> byParent = rows.ToLookup(r => r.ParentID);
			List<ChatRoomRow> ordered = [];
			HashSet<int> seen = [];

			void Walk(Int16? parent)
			{
				foreach (ChatRoomRow row in byParent[parent].OrderBy(r => r.Position).ThenBy(r => r.ID))
				{
					if (seen.Add(row.ID))
					{
						ordered.Add(row);
						Walk(row.ID);
					}
				}
			}

			Walk(null);
			return ordered;
		}
	}

	// The rows after a change or the failure, where Promoted is the sibling that became the default
	public readonly record struct RoomEdit(List<ChatRoomRow>? Rows, int? ID, int? Promoted, Failure? Failure);

	public static class RoomEdits
	{
		private static List<ChatRoomRow> Clone(IReadOnlyList<ChatRoomRow> rows) => rows.Select(r => new ChatRoomRow
		{
			ID = r.ID,
			Name = r.Name,
			ParentID = r.ParentID,
			IsDefault = r.IsDefault,
			Position = r.Position,
			Enabled = r.Enabled,
			ShowAllMatches = r.ShowAllMatches
		}).ToList();

		private static RoomEdit Done(List<ChatRoomRow> rows, int? id, int? promoted = null) =>
			RoomRules.Check(rows) is Failure failure ? new RoomEdit(null, null, null, failure) : new RoomEdit(rows, id, promoted, null);

		private static RoomEdit Refused(string field) => new(null, null, null, Result.Invalid(field));

		private static void SetDefault(List<ChatRoomRow> rows, ChatRoomRow room)
		{
			foreach (ChatRoomRow sibling in rows.Where(r => r.ParentID == room.ParentID))
			{
				sibling.IsDefault = false;
			}
			room.IsDefault = true;
		}

		public static RoomEdit Add(IReadOnlyList<ChatRoomRow> rows, string? name, int? parentID, bool isDefault, bool showAllMatches)
		{
			if (parentID is < 0 or > Int16.MaxValue)
			{
				return Refused("parent_id");
			}

			List<ChatRoomRow> next = Clone(rows);
			int id = next.Count == 0 ? 0 : next.Max(r => r.ID) + 1;
			if (id >= Int16.MaxValue)
			{
				return Refused("id");
			}

			List<ChatRoomRow> siblings = next.Where(r => r.ParentID == parentID).ToList();
			ChatRoomRow room = new()
			{
				ID = (Int16)id,
				Name = (name ?? String.Empty).Trim(),
				ParentID = (Int16?)parentID,
				Position = siblings.Count == 0 ? 0 : siblings.Max(r => r.Position) + 1,
				ShowAllMatches = showAllMatches
			};
			next.Add(room);

			if (isDefault)
			{
				SetDefault(next, room);
			}
			else if (room.ParentID != null && !siblings.Any(r => r.Enabled && r.IsDefault))
			{
				room.IsDefault = true;
			}

			return Done(next, id);
		}

		public static RoomEdit Update(IReadOnlyList<ChatRoomRow> rows, int id, string? name, bool? enabled, bool? isDefault, bool? showAllMatches)
		{
			List<ChatRoomRow> next = Clone(rows);
			ChatRoomRow? room = next.FirstOrDefault(r => r.ID == id);
			if (room == null)
			{
				return Refused("id");
			}

			// another room has to be made the default instead
			if (isDefault == false && room.IsDefault)
			{
				return Refused("is_default");
			}

			if (name != null)
			{
				room.Name = name.Trim();
			}
			if (showAllMatches != null)
			{
				room.ShowAllMatches = showAllMatches.Value;
			}

			int? promoted = null;
			if (enabled != null && enabled != room.Enabled)
			{
				room.Enabled = enabled.Value;
				if (!room.Enabled && room.IsDefault)
				{
					room.IsDefault = false;
					ChatRoomRow? sibling = next.Where(r => r.ParentID == room.ParentID && r.ID != room.ID && r.Enabled).OrderBy(r => r.Position).ThenBy(r => r.ID).FirstOrDefault();
					if (sibling != null)
					{
						sibling.IsDefault = true;
						promoted = sibling.ID;
					}
				}
				else if (room.Enabled && room.ParentID != null && !next.Any(r => r.ParentID == room.ParentID && r.ID != room.ID && r.Enabled && r.IsDefault))
				{
					room.IsDefault = true;
				}
			}

			if (isDefault == true)
			{
				SetDefault(next, room);
			}

			return Done(next, id, promoted);
		}

		// The ids are all the siblings under the parent, in their new order
		public static RoomEdit Reorder(IReadOnlyList<ChatRoomRow> rows, int? parentID, IReadOnlyList<int> ids)
		{
			List<ChatRoomRow> next = Clone(rows);
			if (parentID != null && !next.Any(r => r.ID == parentID))
			{
				return Refused("parent_id");
			}

			List<ChatRoomRow> siblings = next.Where(r => r.ParentID == parentID).ToList();
			if (ids.Count != siblings.Count || ids.Distinct().Count() != ids.Count || !siblings.All(r => ids.Contains(r.ID)))
			{
				return Refused("ids");
			}

			for (int i = 0; i < ids.Count; i++)
			{
				siblings.First(r => r.ID == ids[i]).Position = i;
			}

			return Done(next, null);
		}

		// The rooms whose fields differ, as before and after rows
		public static IReadOnlyList<(ChatRoomRow? Before, ChatRoomRow After)> Changes(IReadOnlyList<ChatRoomRow> before, IReadOnlyList<ChatRoomRow> after)
		{
			Dictionary<int, ChatRoomRow> old = before.ToDictionary(r => (int)r.ID);
			List<(ChatRoomRow?, ChatRoomRow)> changes = [];
			foreach (ChatRoomRow row in after)
			{
				if (!old.TryGetValue(row.ID, out ChatRoomRow? was))
				{
					changes.Add((null, row));
				}
				else if (!Same(was, row))
				{
					changes.Add((was, row));
				}
			}
			return changes;
		}

		public static bool Same(ChatRoomRow a, ChatRoomRow b) =>
			a.Name == b.Name && a.ParentID == b.ParentID && a.IsDefault == b.IsDefault && a.Position == b.Position && a.Enabled == b.Enabled && a.ShowAllMatches == b.ShowAllMatches;
	}

	internal sealed class RoomConfigData
	{
		[JsonPropertyName("name")]
		public required string Name { get; init; }

		[JsonPropertyName("default")]
		public bool IsDefault { get; init; }

		[JsonPropertyName("rooms")]
		public List<RoomConfigData?> Rooms { get; init; } = [];
	}

	public static class ChatRoomSeed
	{
		private static readonly JsonSerializerOptions s_jsonOptions = new()
		{
			ReadCommentHandling = JsonCommentHandling.Skip,
			AllowTrailingCommas = true,
			RespectNullableAnnotations = true
		};

		// The ids are the depth first index, the first top level room shows the matches of every room
		public static List<ChatRoomRow> FromJson(string json)
		{
			List<RoomConfigData?> configured = JsonSerializer.Deserialize<List<RoomConfigData?>>(json, s_jsonOptions)
				?? throw new InvalidDataException("The room catalog must contain a JSON array.");
			if (configured.Count == 0)
			{
				throw new InvalidDataException("The room catalog must contain at least one room.");
			}

			List<ChatRoomRow> rows = [];

			void Flatten(RoomConfigData? config, Int16? parentID, int position)
			{
				if (config is null)
				{
					throw new InvalidDataException("The room catalog cannot contain null room entries.");
				}
				if (rows.Count >= Int16.MaxValue)
				{
					throw new InvalidDataException($"The room catalog cannot contain more than {Int16.MaxValue} rooms.");
				}

				ChatRoomRow row = new()
				{
					ID = (Int16)rows.Count,
					Name = config.Name,
					ParentID = parentID,
					IsDefault = config.IsDefault,
					Position = position,
					ShowAllMatches = rows.Count == 0
				};
				rows.Add(row);

				for (int i = 0; i < config.Rooms.Count; i++)
				{
					Flatten(config.Rooms[i], row.ID, i);
				}
			}

			for (int i = 0; i < configured.Count; i++)
			{
				Flatten(configured[i], null, i);
			}

			if (RoomRules.Check(rows) is Failure failure)
			{
				throw new InvalidDataException($"The room catalog breaks the rule of '{failure.Params.GetType().GetProperty("Field")?.GetValue(failure.Params)}'.");
			}
			return rows;
		}

		// data/rooms.json is only the import source of the first start, nothing reads it afterwards
		public static async Task EnsureAsync(AppDbContext db)
		{
			if (await db.ChatRooms.AnyAsync())
			{
				return;
			}

			string path = Path.Combine(CatalogStartup.DataDirectory, "rooms.json");
			List<ChatRoomRow> rows;
			try
			{
				rows = FromJson(await File.ReadAllTextAsync(path));
			}
			catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException)
			{
				throw new InvalidDataException($"Failed to load room catalog '{path}': {ex.Message}", ex);
			}

			db.ChatRooms.AddRange(rows);
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
	}
}

namespace GenOnlineService
{
	internal sealed class RoomCatalogEntry
	{
		public required int ID { get; init; }
		public required string Name { get; init; }
		public int? ParentID { get; init; }
		public required Int16 TargetRoomID { get; set; }
		public bool ShowAllMatches { get; init; }
	}

	internal sealed class RoomCatalogData
	{
		// The enabled rooms whose parents are enabled too, in the order clients list them
		public required IReadOnlyList<RoomCatalogEntry> Rooms { get; init; }
		public required IReadOnlyDictionary<int, RoomCatalogEntry> RoomsByID { get; init; }
		public required IReadOnlyDictionary<int, HashSet<int>> DescendantRoomIDs { get; init; }

		// Every room including the disabled ones, whose lobbies stay visible to their parents
		public required IReadOnlyDictionary<int, int?> ParentOf { get; init; }

		public static RoomCatalogData Create(IReadOnlyList<ChatRoomRow> rows)
		{
			List<ChatRoomRow> ordered = RoomRules.Ordered(rows);
			ILookup<Int16?, ChatRoomRow> children = ordered.ToLookup(r => r.ParentID);
			Dictionary<int, HashSet<int>> descendants = [];
			Dictionary<int, RoomCatalogEntry> entries = [];
			List<RoomCatalogEntry> rooms = [];

			HashSet<int> Descendants(ChatRoomRow row)
			{
				HashSet<int> all = [];
				foreach (ChatRoomRow child in children[row.ID])
				{
					all.Add(child.ID);
					all.UnionWith(Descendants(child));
				}
				return all;
			}

			Int16 Target(ChatRoomRow row)
			{
				List<ChatRoomRow> enabled = children[row.ID].Where(c => c.Enabled).ToList();
				return enabled.Count == 0 ? row.ID : Target(enabled.FirstOrDefault(c => c.IsDefault) ?? enabled[0]);
			}

			void Walk(Int16? parent, bool parentEnabled)
			{
				foreach (ChatRoomRow row in children[parent])
				{
					bool enabled = parentEnabled && row.Enabled;
					if (enabled)
					{
						RoomCatalogEntry entry = new() { ID = row.ID, Name = row.Name, ParentID = row.ParentID, TargetRoomID = Target(row), ShowAllMatches = row.ShowAllMatches };
						rooms.Add(entry);
						entries.Add(entry.ID, entry);
						descendants.Add(entry.ID, Descendants(row));
					}
					Walk(row.ID, enabled);
				}
			}

			Walk(null, true);

			return new RoomCatalogData
			{
				Rooms = rooms,
				RoomsByID = entries,
				DescendantRoomIDs = descendants,
				ParentOf = ordered.ToDictionary(r => (int)r.ID, r => (int?)r.ParentID)
			};
		}

		public IReadOnlyList<ChannelInfo> Channels() => Rooms.Select(room => new ChannelInfo
		{
			Id = room.ID,
			Name = room.Name,
			ParentId = room.ParentID,
			ShowAllMatches = room.ShowAllMatches
		}).ToArray();

		public bool TryResolveTargetRoomID(int selectedRoomID, out Int16 targetRoomID)
		{
			if (selectedRoomID == -1)
			{
				targetRoomID = -1;
				return true;
			}

			if (!RoomsByID.TryGetValue(selectedRoomID, out RoomCatalogEntry? room))
			{
				targetRoomID = -1;
				return false;
			}

			targetRoomID = room.TargetRoomID;
			return true;
		}

		public bool CanViewLobby(int selectedRoomID, int lobbyRoomID)
		{
			if (!RoomsByID.TryGetValue(selectedRoomID, out RoomCatalogEntry? selected) || !ParentOf.ContainsKey(lobbyRoomID))
			{
				return false;
			}

			return selected.ShowAllMatches
				|| selectedRoomID == lobbyRoomID
				|| DescendantRoomIDs[selectedRoomID].Contains(lobbyRoomID);
		}

		// The room a session in a disabled room moves to is the default of the nearest enabled ancestor, and -1 leaves the channels
		public int FallbackRoomID(int roomID)
		{
			if (RoomsByID.ContainsKey(roomID))
			{
				return roomID;
			}

			for (int steps = 0; ParentOf.TryGetValue(roomID, out int? parent) && parent is int up && steps <= ParentOf.Count; steps++)
			{
				if (RoomsByID.TryGetValue(up, out RoomCatalogEntry? ancestor))
				{
					return ancestor.TargetRoomID;
				}
				roomID = up;
			}

			return -1;
		}
	}

	public static class RoomCatalog
	{
		private static readonly ILogger s_log = AppLog.For(typeof(RoomCatalogData));

		private static RoomCatalogData? s_catalog;

		internal static IReadOnlyList<RoomCatalogEntry> Rooms => Current.Rooms;
		private static RoomCatalogData Current => Volatile.Read(ref s_catalog)
			?? throw new InvalidOperationException("The room catalog has not been loaded.");

		internal static IReadOnlyList<ChannelInfo> Channels() => Current.Channels();

		public static bool TryResolveTargetRoomID(int selectedRoomID, out Int16 targetRoomID)
		{
			return Current.TryResolveTargetRoomID(selectedRoomID, out targetRoomID);
		}

		public static bool CanViewLobby(int selectedRoomID, int lobbyRoomID)
		{
			return Current.CanViewLobby(selectedRoomID, lobbyRoomID);
		}

		// Every copy reloads on the catalog signal and tells only its own connections about a change
		public static async Task ReloadAsync(AppDbContext db)
		{
			List<ChatRoomRow> rows = await db.ChatRooms.AsNoTracking().ToListAsync();
			if (RoomRules.Check(rows) is Failure failure)
			{
				s_log.LogError("The chat rooms in the database break the rule of {Field}, the catalog in memory stays", failure.Params.GetType().GetProperty("Field")?.GetValue(failure.Params));
				return;
			}

			RoomCatalogData next = RoomCatalogData.Create(rows);
			RoomCatalogData? previous = Interlocked.Exchange(ref s_catalog, next);
			if (previous != null && !previous.Channels().SequenceEqual(next.Channels()))
			{
				await RealtimeHub.PublishChannelsAsync(next);
			}
		}
	}
}
