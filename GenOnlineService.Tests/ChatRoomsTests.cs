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
using GenOnlineService.Portal.Client;
using GenOnlineService.Realtime;
using GenOnlineService.Rest;
using Microsoft.AspNetCore.Authorization;
using System.Text.Json;
using Xunit;

namespace GenOnlineService.Tests;

public class ChatRoomsTests
{
	private static string RoomsJson()
	{
		for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
		{
			string path = Path.Combine(dir.FullName, "GenOnlineService", "data", "rooms.json");
			if (File.Exists(path))
			{
				return File.ReadAllText(path);
			}
		}
		throw new FileNotFoundException("data/rooms.json");
	}

	private static ChatRoomRow Room(int id, string name, int? parent = null, bool isDefault = false, int position = 0, bool enabled = true, bool showAll = false) =>
		new() { ID = (Int16)id, Name = name, ParentID = parent == null ? null : (Int16)parent, IsDefault = isDefault, Position = position, Enabled = enabled, ShowAllMatches = showAll };

	private static List<ChatRoomRow> Seed() => ChatRoomSeed.FromJson(RoomsJson());

	private static string? Field(Failure? failure) =>
		failure == null ? null : failure.Value.Params.GetType().GetProperty("Field")!.GetValue(failure.Value.Params) as string;

	[Fact]
	public void TheSeedKeepsTheIdsAndFieldsOfRoomsJson()
	{
		List<ChatRoomRow> rows = Seed();

		Assert.Equal(new[] { 0, 1, 2 }, rows.Select(r => (int)r.ID).ToArray());
		Assert.Equal(new[] { "All Games", "General", "Tournament" }, rows.Select(r => r.Name).ToArray());
		Assert.Equal(new int?[] { null, 0, 0 }, rows.Select(r => r.ParentID == null ? (int?)null : r.ParentID).ToArray());
		Assert.Equal(new[] { false, true, false }, rows.Select(r => r.IsDefault).ToArray());
		Assert.Equal(new[] { true, false, false }, rows.Select(r => r.ShowAllMatches).ToArray());
		Assert.Equal(new[] { 0, 0, 1 }, rows.Select(r => r.Position).ToArray());
		Assert.All(rows, r => Assert.True(r.Enabled));
		Assert.Null(RoomRules.Check(rows));
	}

	[Fact]
	public void TheSeedNumbersNestedRoomsDepthFirst()
	{
		List<ChatRoomRow> rows = ChatRoomSeed.FromJson("""
			[{"name":"A","rooms":[{"name":"B","default":true,"rooms":[{"name":"C","default":true}]},{"name":"D"}]},{"name":"E"}]
			""");

		Assert.Equal(new[] { "A", "B", "C", "D", "E" }, rows.Select(r => r.Name).ToArray());
		Assert.Equal(new[] { 0, 1, 2, 3, 4 }, rows.Select(r => (int)r.ID).ToArray());
		Assert.Equal(new int?[] { null, 0, 1, 0, null }, rows.Select(r => r.ParentID == null ? (int?)null : r.ParentID).ToArray());
	}

	[Fact]
	public void TheCatalogOfTheSeedMatchesTheOldBehaviour()
	{
		RoomCatalogData catalog = RoomCatalogData.Create(Seed());

		Assert.Equal(new[] { 0, 1, 2 }, catalog.Rooms.Select(r => r.ID).ToArray());
		Assert.True(catalog.TryResolveTargetRoomID(0, out short target));
		Assert.Equal(1, target);
		Assert.True(catalog.TryResolveTargetRoomID(2, out target));
		Assert.Equal(2, target);
		Assert.True(catalog.TryResolveTargetRoomID(-1, out target));
		Assert.Equal(-1, target);
		Assert.False(catalog.TryResolveTargetRoomID(9, out _));
		Assert.True(catalog.CanViewLobby(0, 2));
		Assert.True(catalog.CanViewLobby(1, 1));
		Assert.False(catalog.CanViewLobby(1, 2));
		Assert.False(catalog.CanViewLobby(1, 9));
	}

	[Fact]
	public void TheReadyChannelsShapeIsUnchanged()
	{
		string json = JsonSerializer.Serialize(RoomCatalogData.Create(Seed()).Channels(), RealtimeJson.Create());

		Assert.Equal(
			"[{\"id\":0,\"name\":\"All Games\",\"parent_id\":null,\"show_all_matches\":true},"
			+ "{\"id\":1,\"name\":\"General\",\"parent_id\":0,\"show_all_matches\":false},"
			+ "{\"id\":2,\"name\":\"Tournament\",\"parent_id\":0,\"show_all_matches\":false}]",
			json);
	}

	[Fact]
	public void TheShowAllFlagReplacesTheIndexInCanViewLobby()
	{
		List<ChatRoomRow> rows = Seed();
		rows[0].ShowAllMatches = false;
		rows[2].ShowAllMatches = true;
		RoomCatalogData catalog = RoomCatalogData.Create(rows);

		Assert.True(catalog.CanViewLobby(2, 1));
		Assert.True(catalog.CanViewLobby(0, 1));
		Assert.False(catalog.CanViewLobby(1, 2));
	}

	[Fact]
	public void ADisabledRoomLeavesTheChannelsAndKeepsItsLobbiesVisibleToTheParent()
	{
		List<ChatRoomRow> rows = Seed();
		rows[2].Enabled = false;
		RoomCatalogData catalog = RoomCatalogData.Create(rows);

		Assert.Equal(new[] { 0, 1 }, catalog.Channels().Select(c => (int)c.Id).ToArray());
		Assert.False(catalog.TryResolveTargetRoomID(2, out _));
		Assert.True(catalog.CanViewLobby(0, 2));
	}

	[Fact]
	public void ADisabledParentHidesItsChildren()
	{
		List<ChatRoomRow> rows = ChatRoomSeed.FromJson("""
			[{"name":"A","rooms":[{"name":"B","default":true}]},{"name":"C","rooms":[{"name":"D","default":true}]}]
			""");
		rows[0].Enabled = false;
		RoomCatalogData catalog = RoomCatalogData.Create(rows);

		Assert.Equal(new[] { 2, 3 }, catalog.Channels().Select(c => (int)c.Id).ToArray());
	}

	[Fact]
	public void ASessionInADisabledRoomMovesToTheDefaultOfItsParent()
	{
		List<ChatRoomRow> rows = Seed();
		rows[1].Enabled = false;
		rows[1].IsDefault = false;
		rows[2].IsDefault = true;
		RoomCatalogData catalog = RoomCatalogData.Create(rows);

		Assert.Equal(2, catalog.FallbackRoomID(1));
		Assert.Equal(2, catalog.FallbackRoomID(2));
		Assert.Equal(0, catalog.FallbackRoomID(0));
		Assert.Equal(-1, catalog.FallbackRoomID(-1));
		Assert.Equal(-1, catalog.FallbackRoomID(40));
	}

	[Fact]
	public void ASessionFallsBackToTheNearestEnabledAncestor()
	{
		List<ChatRoomRow> rows = ChatRoomSeed.FromJson("""
			[{"name":"A","rooms":[{"name":"B","default":true,"rooms":[{"name":"C","default":true}]},{"name":"D"}]}]
			""");
		rows[1].Enabled = false;
		rows[3].IsDefault = true;
		rows[1].IsDefault = false;
		RoomCatalogData catalog = RoomCatalogData.Create(rows);

		Assert.Equal(3, catalog.FallbackRoomID(2));
		Assert.Equal(3, catalog.FallbackRoomID(1));
	}

	[Fact]
	public void AParentWithEnabledChildrenNeedsExactlyOneDefault()
	{
		List<ChatRoomRow> none = Seed();
		none[1].IsDefault = false;
		Assert.Equal("is_default", Field(RoomRules.Check(none)));

		List<ChatRoomRow> two = Seed();
		two[2].IsDefault = true;
		Assert.Equal("is_default", Field(RoomRules.Check(two)));
	}

	[Fact]
	public void ADisabledChildCannotBeTheDefault()
	{
		List<ChatRoomRow> rows = Seed();
		rows[1].Enabled = false;

		Assert.Equal("is_default", Field(RoomRules.Check(rows)));
	}

	[Fact]
	public void AParentWithOnlyDisabledChildrenNeedsNoDefault()
	{
		List<ChatRoomRow> rows = Seed();
		rows[1].Enabled = false;
		rows[1].IsDefault = false;
		rows[2].Enabled = false;

		Assert.Null(RoomRules.Check(rows));
	}

	[Fact]
	public void ATopLevelRoomCannotBeTheDefault()
	{
		List<ChatRoomRow> rows = Seed();
		rows[0].IsDefault = true;

		Assert.Equal("is_default", Field(RoomRules.Check(rows)));
	}

	[Fact]
	public void IdsStayBelowInt16MaxAndAreUnique()
	{
		List<ChatRoomRow> rows = Seed();
		rows.Add(Room(Int16.MaxValue, "Max", 0, position: 5));
		Assert.Equal("id", Field(RoomRules.Check(rows)));

		List<ChatRoomRow> clash = Seed();
		clash.Add(Room(1, "Again", 0, position: 5));
		Assert.Equal("id", Field(RoomRules.Check(clash)));

		List<ChatRoomRow> negative = Seed();
		negative.Add(Room(-1, "Below", 0, position: 5));
		Assert.Equal("id", Field(RoomRules.Check(negative)));
	}

	[Fact]
	public void ANameHasOneToThirtyTwoCharacters()
	{
		List<ChatRoomRow> blank = Seed();
		blank[2].Name = "  ";
		Assert.Equal("name", Field(RoomRules.Check(blank)));

		List<ChatRoomRow> longest = Seed();
		longest[2].Name = new string('x', 32);
		Assert.Null(RoomRules.Check(longest));

		longest[2].Name = new string('x', 33);
		Assert.Equal("name", Field(RoomRules.Check(longest)));
	}

	[Fact]
	public void AParentMustExistAndCyclesAreRefused()
	{
		List<ChatRoomRow> missing = Seed();
		missing[2].ParentID = 77;
		Assert.Equal("parent_id", Field(RoomRules.Check(missing)));

		List<ChatRoomRow> self = Seed();
		self[1].ParentID = 1;
		Assert.Equal("parent_id", Field(RoomRules.Check(self)));

		List<ChatRoomRow> cycle = ChatRoomSeed.FromJson("""
			[{"name":"A","rooms":[{"name":"B","default":true,"rooms":[{"name":"C","default":true}]}]}]
			""");
		cycle[0].ParentID = 2;
		Assert.Equal("parent_id", Field(RoomRules.Check(cycle)));
	}

	[Fact]
	public void AddingARoomTakesTheNextIdAndTheEndOfItsSiblings()
	{
		RoomEdit edit = RoomEdits.Add(Seed(), " Ladder ", 0, false, false);

		Assert.Null(edit.Failure);
		Assert.Equal(3, edit.ID);
		ChatRoomRow added = edit.Rows!.Single(r => r.ID == 3);
		Assert.Equal("Ladder", added.Name);
		Assert.Equal((short?)0, added.ParentID);
		Assert.Equal(2, added.Position);
		Assert.True(added.Enabled);
		Assert.False(added.IsDefault);
	}

	[Fact]
	public void TheFirstEnabledChildBecomesTheDefault()
	{
		RoomEdit edit = RoomEdits.Add(Seed(), "Sub", 2, false, false);

		Assert.Null(edit.Failure);
		Assert.True(edit.Rows!.Single(r => r.ID == 3).IsDefault);
	}

	[Fact]
	public void AddingADefaultMovesTheDefaultAwayFromTheSibling()
	{
		RoomEdit edit = RoomEdits.Add(Seed(), "Ladder", 0, true, false);

		Assert.Null(edit.Failure);
		Assert.True(edit.Rows!.Single(r => r.ID == 3).IsDefault);
		Assert.False(edit.Rows!.Single(r => r.ID == 1).IsDefault);
	}

	[Fact]
	public void AnIdIsNeverReusedAfterARoomWasDisabled()
	{
		List<ChatRoomRow> rows = Seed();
		rows[2].Enabled = false;

		Assert.Equal(3, RoomEdits.Add(rows, "Again", 0, false, false).ID);
	}

	[Fact]
	public void AddingARoomChecksTheName()
	{
		Assert.Equal("name", Field(RoomEdits.Add(Seed(), "", 0, false, false).Failure));
		Assert.Equal("name", Field(RoomEdits.Add(Seed(), null, 0, false, false).Failure));
		Assert.Equal("parent_id", Field(RoomEdits.Add(Seed(), "Ok", 55, false, false).Failure));
		Assert.Equal("is_default", Field(RoomEdits.Add(Seed(), "Top", null, true, false).Failure));
	}

	[Fact]
	public void TheLastFreeIdIsStillGivenAndThenTheAddIsRefused()
	{
		List<ChatRoomRow> rows = Seed();
		rows.Add(Room(Int16.MaxValue - 2, "Near", 0, position: 5));
		Assert.Equal(Int16.MaxValue - 1, RoomEdits.Add(rows, "Last", 0, false, false).ID);

		rows.Add(Room(Int16.MaxValue - 1, "Last", 0, position: 6));
		Assert.Equal("id", Field(RoomEdits.Add(rows, "More", 0, false, false).Failure));
	}

	[Fact]
	public void RenamingKeepsTheOtherFields()
	{
		RoomEdit edit = RoomEdits.Update(Seed(), 2, "Cups", null, null, null);

		Assert.Null(edit.Failure);
		ChatRoomRow room = edit.Rows!.Single(r => r.ID == 2);
		Assert.Equal("Cups", room.Name);
		Assert.True(room.Enabled);
		Assert.Equal("is_default", Field(RoomEdits.Update(Seed(), 1, null, null, false, null).Failure));
	}

	[Fact]
	public void SettingTheDefaultMovesItFromTheSibling()
	{
		RoomEdit edit = RoomEdits.Update(Seed(), 2, null, null, true, null);

		Assert.Null(edit.Failure);
		Assert.True(edit.Rows!.Single(r => r.ID == 2).IsDefault);
		Assert.False(edit.Rows!.Single(r => r.ID == 1).IsDefault);
	}

	[Fact]
	public void DisablingTheDefaultPromotesTheNextEnabledSibling()
	{
		RoomEdit edit = RoomEdits.Update(Seed(), 1, null, false, null, null);

		Assert.Null(edit.Failure);
		Assert.False(edit.Rows!.Single(r => r.ID == 1).Enabled);
		Assert.False(edit.Rows!.Single(r => r.ID == 1).IsDefault);
		Assert.True(edit.Rows!.Single(r => r.ID == 2).IsDefault);
		Assert.Equal(2, edit.Promoted);
	}

	[Fact]
	public void EnablingARoomUnderAParentWithoutAnEnabledChildMakesItTheDefault()
	{
		List<ChatRoomRow> rows = Seed();
		rows[1].Enabled = false;
		rows[1].IsDefault = false;
		rows[2].Enabled = false;

		RoomEdit edit = RoomEdits.Update(rows, 2, null, true, null, null);

		Assert.Null(edit.Failure);
		Assert.True(edit.Rows!.Single(r => r.ID == 2).IsDefault);
	}

	[Fact]
	public void AnUpdateOfAnUnknownRoomIsRefused()
	{
		Assert.Equal("id", Field(RoomEdits.Update(Seed(), 40, "x", null, null, null).Failure));
	}

	[Fact]
	public void ADefaultCannotBeSetOnATopLevelOrADisabledRoom()
	{
		Assert.Equal("is_default", Field(RoomEdits.Update(Seed(), 0, null, null, true, null).Failure));

		List<ChatRoomRow> rows = Seed();
		rows[2].Enabled = false;
		Assert.Equal("is_default", Field(RoomEdits.Update(rows, 2, null, null, true, null).Failure));
	}

	[Fact]
	public void ReorderingNumbersTheSiblingsInTheGivenOrder()
	{
		RoomEdit edit = RoomEdits.Reorder(Seed(), 0, [2, 1]);

		Assert.Null(edit.Failure);
		Assert.Equal(0, edit.Rows!.Single(r => r.ID == 2).Position);
		Assert.Equal(1, edit.Rows!.Single(r => r.ID == 1).Position);
		Assert.Equal(new[] { 0, 2, 1 }, RoomRules.Ordered(edit.Rows!).Select(r => (int)r.ID).ToArray());
	}

	[Fact]
	public void ReorderingNeedsEverySiblingOnce()
	{
		Assert.Equal("ids", Field(RoomEdits.Reorder(Seed(), 0, [1]).Failure));
		Assert.Equal("ids", Field(RoomEdits.Reorder(Seed(), 0, [1, 1]).Failure));
		Assert.Equal("ids", Field(RoomEdits.Reorder(Seed(), 0, [1, 0]).Failure));
		Assert.Equal("parent_id", Field(RoomEdits.Reorder(Seed(), 9, [1, 2]).Failure));
	}

	[Fact]
	public void TheStaffRoutesNeedChannelsManageAndTakeTheSignInCookie()
	{
		Type controller = typeof(RoomsController);

		AuthorizeAttribute authorize = Assert.Single(controller.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
		Assert.Equal(PermissionPolicies.Prefix + Permissions.ChannelsManage, authorize.Policy);
		Assert.NotEmpty(controller.GetCustomAttributes(typeof(GenOnlineService.Api.AllowSignInCookieAttribute), true));
	}

	[Fact]
	public void TheRoomActionsAreAuditedWithLabels()
	{
		Assert.Equal("room_create", ModerationActions.RoomCreate);
		Assert.Equal("room_update", ModerationActions.RoomUpdate);
		Assert.Equal("room", AuditTargetKinds.Room);
		Assert.Equal("Chat room added", PortalAuditActions.LabelOf("room_create"));
		Assert.Equal("Chat room changed", PortalAuditActions.LabelOf("room_update"));
		Assert.Contains("room_create", PortalAuditActions.All);
		Assert.Contains("room_update", PortalAuditActions.All);
	}

	[Fact]
	public void TheChannelsUpdateEventCarriesTheReadyChannelList()
	{
		IReadOnlyList<ChannelInfo> channels = RoomCatalogData.Create(Seed()).Channels();

		string update = JsonSerializer.Serialize(new ChannelsUpdateEvent { Channels = channels }, RealtimeJson.Create());
		string ready = JsonSerializer.Serialize(new ReadyEvent { Channels = channels }, RealtimeJson.Create());

		Assert.StartsWith("{\"channels\":[{\"id\":0,\"name\":\"All Games\"", update);
		Assert.Contains(update.Substring("{\"channels\":".Length, update.Length - "{\"channels\":}".Length), ready);
	}

	[Fact]
	public void TheChannelsEventAndResourceExist()
	{
		Assert.NotNull(typeof(IRealtimeClient).GetMethod("ChannelsUpdate"));
		Assert.Equal("channels", CatalogResources.Channels);
	}
}
