using GenOnlineService.Api;
using GenOnlineService.Failover;
using GenOnlineService.Playlists;
using GenOnlineService.Realtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using System.Text.Json;
using Xunit;

namespace GenOnlineService.Tests;

// The playlist catalog is one static snapshot, so the tests that swap it never run beside each other
[CollectionDefinition(PlaylistCatalogCollection.Name, DisableParallelization = true)]
public sealed class PlaylistCatalogCollection
{
	public const string Name = "playlist catalog";
}

[Collection(PlaylistCatalogCollection.Name)]
public class PlaylistSeedTests
{
	[Fact]
	public void TheSeedIsTheTwoPlaylistsTheServiceShippedWith()
	{
		Assert.Equal([0, 1], PlaylistSeed.Playlists.Select(p => p.Id).ToArray());

		PlaylistDraft duel = PlaylistSeed.Playlists[0];
		Assert.Equal(("1v1 (Random Armies)", true, 2, 2, 1, false, -1, false, 0), (duel.Name, duel.Enabled, duel.MinPlayers, duel.DesiredPlayers, duel.MinSelectedMaps, duel.AllowTeams, duel.TeamSize, duel.AllowArmySelection, duel.GracePeriodMs));
		Assert.Equal(41, duel.Maps.Count);
		Assert.Equal(new MapDraft("[RANK] Snowy Drought ZH v5 (2)", "[RANK] Snowy Drought ZH v5", true, 2), duel.Maps[0]);
		Assert.Equal(new MapDraft("[RANK] Barren Badlands Balanced ZH v2 (2)", "[RANK] Barren Badlands Balanced ZH v2", true, 2), duel.Maps[14]);
		Assert.Equal(new MapDraft("Toxic Lake v5 (2)", "Toxic Lake v5", true, 2), duel.Maps[^1]);

		PlaylistDraft ffa = PlaylistSeed.Playlists[1];
		Assert.Equal(("6-8P FFA (Random Armies)", true, 6, 8, 1, false, -1, false, 30000), (ffa.Name, ffa.Enabled, ffa.MinPlayers, ffa.DesiredPlayers, ffa.MinSelectedMaps, ffa.AllowTeams, ffa.TeamSize, ffa.AllowArmySelection, ffa.GracePeriodMs));
		Assert.Equal(
			[
				new MapDraft("Beijing Uprise v4 (6)", "Beijing Uprise v4", true, 6),
				new MapDraft("Defcon6 (6)", "Defcon6", false, 6),
				new MapDraft("Taiga Terror (6)", "Taiga Terror", true, 6),
				new MapDraft("Swamp Assault v3 [WBC2021] (7)", "Swamp Assault v3 [WBC2021]", true, 7),
				new MapDraft("[RANK] Muddy Madness ZH v1 (8)", "[RANK] Muddy Madness ZH v1", true, 8),
				new MapDraft("[RANK] Wastelands Dust ZH v1 (8)", "[RANK] Wastelands Dust ZH v1", true, 8)
			], ffa.Maps);
	}

	[Fact]
	public void OnlyDefcon6IsARetailMapInTheSeed()
	{
		MapDraft[] retail = PlaylistSeed.Playlists.SelectMany(p => p.Maps).Where(map => !map.Custom).ToArray();
		Assert.Equal(["Defcon6"], retail.Select(map => map.Path).ToArray());
		Assert.Equal(46, PlaylistSeed.Playlists.SelectMany(p => p.Maps).Count(map => map.Custom));
	}

	[Fact]
	public void TheSeedKeepsEveryRuleTheSaveEnforces()
	{
		foreach (PlaylistDraft playlist in PlaylistSeed.Playlists)
		{
			Assert.Null(PlaylistRules.Check(playlist));
		}
	}

	[Fact]
	public void TheSeedSnapshotOffersTheSameIndexesAsBefore()
	{
		PlaylistSnapshot snapshot = PlaylistSnapshot.From(PlaylistSeed.Playlists);

		Assert.Equal([(UInt16)0, (UInt16)1], snapshot.Playlists.Keys.Order().ToArray());
		Assert.Equal(41, snapshot.Playlists[0].Maps.Count);
		Assert.Equal("Defcon6", snapshot.Playlists[1].Maps[1].Path);
		Assert.Equal(30000, snapshot.Playlists[1].GracePeriodAtMinPlayersMSec);
	}
}

public class PlaylistRulesTests
{
	private static MapDraft Map(int players = 2, string name = "Arena (2)", string path = "Arena", bool enabled = true) => new(name, path, true, players, enabled);

	private static PlaylistDraft Duel(params MapDraft[] maps) => new(5, "Duel", true, 2, 2, 1, false, -1, false, 0, maps.Length == 0 ? [Map()] : maps);

	[Fact]
	public void AValidPlaylistPasses()
	{
		Assert.Null(PlaylistRules.Check(Duel()));
	}

	[Theory]
	[InlineData("", "name")]
	[InlineData(" Duel", "name")]
	[InlineData("0123456789012345678901234567890123456789012345678901234567890123456789", "name")]
	public void TheNameIsOneToSixtyFourCharacters(string name, string field)
	{
		Assert.Equal(field, PlaylistRules.Check(Duel() with { Name = name }));
		Assert.Null(PlaylistRules.Check(Duel() with { Name = new string('a', 64) }));
	}

	[Fact]
	public void PlayerBoundsFollowTheLobbyCapacity()
	{
		Assert.Equal("min_players", PlaylistRules.Check(Duel() with { MinPlayers = 1 }));
		Assert.Equal("desired_players", PlaylistRules.Check(Duel() with { DesiredPlayers = Lobby.MaxLobbySize + 1 }));
		Assert.Equal("desired_players", PlaylistRules.Check(Duel() with { MinPlayers = 4, DesiredPlayers = 3 }));
	}

	[Theory]
	[InlineData(-1, true)]
	[InlineData(0, false)]
	[InlineData(1, true)]
	[InlineData(6, true)]
	[InlineData(7, false)]
	public void TheTeamSizeIsNoneOrOneToSix(int size, bool valid)
	{
		Assert.Equal(valid, PlaylistRules.Check(Duel() with { TeamSize = size }) == null);
	}

	[Fact]
	public void TheGracePeriodFitsTheUnsignedShortTheMatchmakingKeeps()
	{
		Assert.Null(PlaylistRules.Check(Duel() with { GracePeriodMs = 65535 }));
		Assert.Equal("grace_period_s", PlaylistRules.Check(Duel() with { GracePeriodMs = 65536 }));
		Assert.Equal("grace_period_s", PlaylistRules.Check(Duel() with { GracePeriodMs = -1 }));
	}

	[Theory]
	[InlineData("Arena/Two", false)]
	[InlineData("Arena\\Two", false)]
	[InlineData("Arena.map", false)]
	[InlineData("Arena.MAP", false)]
	[InlineData("..", false)]
	[InlineData("C:Arena", false)]
	[InlineData("", false)]
	[InlineData("Arena v2.5", true)]
	[InlineData("[RANK] Arena ZH v1", true)]
	public void TheFileStemHasNoSeparatorOrMapSuffix(string path, bool valid)
	{
		string? field = PlaylistRules.Check(Duel(Map(path: path)));
		Assert.Equal(valid ? null : "maps[0].path", field);
	}

	[Fact]
	public void AMapNeedsAnExactNameAndSensibleSize()
	{
		Assert.Equal("maps[0].name", PlaylistRules.Check(Duel(Map(name: ""))));
		Assert.Equal("maps[0].name", PlaylistRules.Check(Duel(Map(name: new string('a', 129)))));
		Assert.Equal("maps[0].path", PlaylistRules.Check(Duel(Map(path: new string('a', 129)))));
		Assert.Equal("maps[0].max_players", PlaylistRules.Check(Duel(Map(players: 1)).Disabled()));
		Assert.Equal("maps[0].max_players", PlaylistRules.Check(Duel(Map(players: 13)).Disabled()));
	}

	[Fact]
	public void AnEnabledPlaylistNeedsEnoughEnabledMapsOfTheRightSizes()
	{
		Assert.Equal("min_selected_maps", PlaylistRules.Check(Duel(Map(enabled: false))));
		Assert.Equal("min_selected_maps", PlaylistRules.Check(Duel() with { MinSelectedMaps = 2 }));
		Assert.Equal("maps[1].max_players", PlaylistRules.Check(Duel(Map(), Map(players: 3, path: "Three"))));
		Assert.Equal("maps", PlaylistRules.Check(new PlaylistDraft(5, "FFA", true, 2, 3, 1, false, -1, false, 0, [Map()])));
		Assert.Null(PlaylistRules.Check(new PlaylistDraft(5, "FFA", true, 2, 3, 1, false, -1, false, 0, [Map(), Map(players: 3, path: "Three")])));
	}

	[Fact]
	public void ADisabledPlaylistMayStayIncomplete()
	{
		Assert.Null(PlaylistRules.Check(Duel(Map(enabled: false)).Disabled()));
		Assert.Null(PlaylistRules.Check(Duel(Map(players: 5)).Disabled()));
		Assert.Equal("name", PlaylistRules.Check(Duel() with { Enabled = false, Name = "" }));
	}

	[Fact]
	public void TheRequestKeepsWhatItLeavesOutAndRefusesAGraceOutsideTheRange()
	{
		PlaylistDraft draft = Duel();
		Assert.Equal(draft with { Name = "Renamed", GracePeriodMs = 30000 }, new PlaylistRequest { Name = " Renamed ", GracePeriodS = 30 }.ApplyTo(draft));
		Assert.Equal("grace_period_s", PlaylistRules.Check(new PlaylistRequest { GracePeriodS = 66 }.ApplyTo(draft)));
		Assert.Equal("grace_period_s", PlaylistRules.Check(new PlaylistRequest { GracePeriodS = Int32.MaxValue }.ApplyTo(draft)));
		Assert.Equal(new MapDraft("Arena (2)", "Arena", true, 2, false), new MapRequest { Enabled = false }.ApplyTo(Map()));
	}
}

internal static class PlaylistDraftExtensions
{
	public static PlaylistDraft Disabled(this PlaylistDraft draft) => draft with { Enabled = false };
}

[Collection(PlaylistCatalogCollection.Name)]
public class PlaylistSnapshotTests : IDisposable
{
	public PlaylistSnapshotTests() => PlaylistCatalog.Install(PlaylistSnapshot.Empty);

	public void Dispose() => PlaylistCatalog.Install(PlaylistSnapshot.Empty);

	private static MapDraft Map(string path, int players = 2, bool enabled = true, bool custom = true) => new(path + " (" + players + ")", path, custom, players, enabled);

	private static PlaylistDraft Duel(int id, bool enabled, params MapDraft[] maps) => new(id, "Duel " + id, enabled, 2, 2, 1, false, -1, false, 0, maps);

	[Fact]
	public void ADisabledPlaylistIsNotOffered()
	{
		PlaylistSnapshot snapshot = PlaylistSnapshot.From([Duel(1, true, Map("A")), Duel(2, false, Map("B"))]);

		Assert.Equal([(UInt16)1], snapshot.Playlists.Keys.ToArray());
	}

	[Fact]
	public void ADisabledMapIsNotOfferedAndTheIndexesCountTheEnabledOnes()
	{
		PlaylistCatalog.Install(PlaylistSnapshot.From([Duel(1, true, Map("A"), Map("B", enabled: false), Map("C"))]));

		var maps = MatchActions.Playlists().Playlists.Single().Maps;

		Assert.Equal([(0, "A"), (1, "C")], maps.Select(map => (map.Index, map.Path)).ToArray());
	}

	[Fact]
	public void ARuleBreakingPlaylistIsLeftOutInsteadOfTakingTheRestDown()
	{
		PlaylistSnapshot snapshot = PlaylistSnapshot.From([Duel(1, true, Map("A")), Duel(2, true, Map("B", players: 5))]);

		Assert.Equal([(UInt16)1], snapshot.Playlists.Keys.ToArray());
	}

	[Fact]
	public void TheOfferedPlaylistsKeepTheirApiShape()
	{
		PlaylistCatalog.Install(PlaylistSnapshot.From(PlaylistSeed.Playlists));

		JsonElement body = JsonSerializer.SerializeToElement(MatchActions.Playlists(), ApiResults.Json);
		JsonElement ffa = body.GetProperty("playlists")[1];

		Assert.Equal(2, body.GetProperty("playlists").GetArrayLength());
		Assert.Equal(["id", "name", "min_players", "desired_players", "min_selected_maps", "allow_teams", "team_size", "allow_army_selection", "grace_period_s", "maps"],
			ffa.EnumerateObject().Select(p => p.Name).ToArray());
		Assert.Equal(30, ffa.GetProperty("grace_period_s").GetInt32());
		Assert.Equal(["index", "name", "path", "custom", "max_players"], ffa.GetProperty("maps")[0].EnumerateObject().Select(p => p.Name).ToArray());
		Assert.False(ffa.GetProperty("maps")[1].GetProperty("custom").GetBoolean());
		Assert.Equal(41, body.GetProperty("playlists")[0].GetProperty("maps").GetArrayLength());
	}

	[Fact]
	public void EveryEditChangesTheBodyTheETagHashes()
	{
		PlaylistCatalog.Install(PlaylistSnapshot.From([Duel(1, true, Map("A"), Map("B"))]));
		string before = JsonSerializer.Serialize(MatchActions.Playlists(), ApiResults.Json);

		PlaylistCatalog.Install(PlaylistSnapshot.From([Duel(1, true, Map("A"), Map("B", enabled: false))]));
		string disabled = JsonSerializer.Serialize(MatchActions.Playlists(), ApiResults.Json);
		PlaylistCatalog.Install(PlaylistSnapshot.From([Duel(1, true, Map("B"), Map("A"))]));
		string reordered = JsonSerializer.Serialize(MatchActions.Playlists(), ApiResults.Json);
		PlaylistCatalog.Install(PlaylistSnapshot.From([Duel(1, true, Map("A"), Map("B")) with { Name = "Renamed" }]));
		string renamed = JsonSerializer.Serialize(MatchActions.Playlists(), ApiResults.Json);

		Assert.Equal(4, new[] { before, disabled, reordered, renamed }.Distinct().Count());
	}

	[Fact]
	public void InstallingTheSameContentChangesNothing()
	{
		PlaylistCatalog.Install(PlaylistSnapshot.From(PlaylistSeed.Playlists));

		Assert.Empty(PlaylistCatalog.Install(PlaylistSnapshot.From(PlaylistSeed.Playlists)));
	}

	[Fact]
	public void QueuedPlayersAreCancelledOnlyWhenTheirMapIndexesOrSizesMayHaveChanged()
	{
		PlaylistSnapshot start = PlaylistSnapshot.From([Duel(1, true, Map("A"), Map("B")), Duel(2, true, Map("C"))]);

		IReadOnlySet<UInt16> Changed(params PlaylistDraft[] next) => PlaylistSnapshot.ChangedIds(start, PlaylistSnapshot.From(next));
		PlaylistDraft one = Duel(1, true, Map("A"), Map("B"));
		PlaylistDraft two = Duel(2, true, Map("C"));

		Assert.Empty(Changed(one, two));
		Assert.Empty(Changed(one with { Name = "Renamed" }, two with { Maps = [Map("C") with { Name = "Other name", Custom = false }] }));
		Assert.Equal([(UInt16)1], Changed(one with { Maps = [Map("A"), Map("B"), Map("D")] }, two));
		Assert.Equal([(UInt16)1], Changed(one with { Maps = [Map("B"), Map("A")] }, two));
		Assert.Equal([(UInt16)1], Changed(one with { Maps = [Map("A"), Map("B", enabled: false)] }, two));
		Assert.Equal([(UInt16)2], Changed(one, two with { MinSelectedMaps = 1, GracePeriodMs = 5000 }));
		Assert.Equal([(UInt16)2], Changed(one));
		Assert.Equal([(UInt16)1, (UInt16)2], Changed(one with { Enabled = false }, two with { Enabled = false }).Order().ToArray());
	}
}

public class QuickMatchOfficialMapTests
{
	[Fact]
	public void ACustomPlaylistMapIsNotOfficialAndKeepsItsNameAndPath()
	{
		MapRef map = PlaylistMapRefs.Of(new PlaylistMap("[RANK] Snowy Drought ZH v5 (2)", "[RANK] Snowy Drought ZH v5", true, 2));

		Assert.Equal(new MapRef { Name = "[RANK] Snowy Drought ZH v5 (2)", Path = "[RANK] Snowy Drought ZH v5.map", Official = false }, map);
	}

	[Fact]
	public void ARetailPlaylistMapIsOfficial()
	{
		MapRef map = PlaylistMapRefs.Of(new PlaylistMap("Defcon6 (6)", "Defcon6", false, 6));

		Assert.Equal(new MapRef { Name = "Defcon6 (6)", Path = "Defcon6.map", Official = true }, map);
	}

	private static Lobby QuickMatchLobby(MapRef map) => Lobby.FromLive(new LobbyLive
	{
		Id = 10,
		Owner = 1,
		Name = "Quickmatch Lobby",
		State = (int)ELobbyState.GAME_SETUP,
		Type = (int)ELobbyType.QuickMatch,
		MapName = map.Name,
		MapPath = map.Path,
		MapOfficial = map.Official,
		Slots = Enumerable.Range(0, Lobby.MaxLobbySize).Select(index => new LobbyMemberLive
		{
			SlotIndex = index,
			SlotState = (int)(index < 2 ? EPlayerType.SLOT_PLAYER : EPlayerType.SLOT_OPEN),
			UserId = index < 2 ? index + 1 : -1
		}).ToList()
	});

	// the lobby state, the lobby summary and the copy the other servers get all read the lobby, and the match found event reads the same fields
	[Fact]
	public void ACustomMapReachesEveryMessageAsNotOfficial()
	{
		MapRef custom = PlaylistMapRefs.Of(new PlaylistMap("Arena (2)", "Arena", true, 2));
		Lobby lobby = QuickMatchLobby(custom);

		Assert.False(lobby.IsMapOfficial);
		Assert.Equal(custom, RealtimeLobbies.StateOf(lobby).Map);
		LobbySummaryMap summary = RealtimeLobbies.SummaryOf(lobby, null).Map;
		Assert.Equal(("Arena (2)", "Arena.map", false), (summary.Name, summary.Path, summary.Official));
		Assert.False(lobby.CaptureLiveLocked().MapOfficial);
	}

	[Fact]
	public void ARetailMapReachesEveryMessageAsOfficial()
	{
		MapRef retail = PlaylistMapRefs.Of(new PlaylistMap("Defcon6 (6)", "Defcon6", false, 6));
		Lobby lobby = QuickMatchLobby(retail);

		Assert.True(lobby.IsMapOfficial);
		Assert.Equal(retail, RealtimeLobbies.StateOf(lobby).Map);
		Assert.True(RealtimeLobbies.SummaryOf(lobby, null).Map.Official);
	}
}

// Runs against a real database when GENONLINE_PROBE_POSTGRES, GENONLINE_PROBE_MARIADB or GENONLINE_PROBE_MYSQL names a server
[Collection(PlaylistCatalogCollection.Name)]
public class PlaylistStoreProbeTests
{
	private static readonly Database.ModerationActor Staff = new() { Kind = AuditActorKinds.User, UserID = 7, Name = "staff", Source = ModerationSources.Api };

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task TheSeedHoldsTheTwoPlaylistsOnceAndEveryChangeIsAudited(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		await using AppDbContext db = probe.Open();

		await PlaylistStore.EnsureSeedAsync(db);
		await PlaylistStore.EnsureSeedAsync(db);
		List<PlaylistDraft> seeded = await PlaylistStore.LoadAll(db);
		Assert.Equal(PlaylistSeed.Playlists.Select(p => p.Maps.Select(m => m.Path).ToArray()), seeded.Select(p => p.Maps.Select(m => m.Path).ToArray()));
		Assert.All(seeded.SelectMany(p => p.Maps), map => Assert.NotEqual(0, map.Id));

		// a rule break is refused with the field, and nothing is written
		(PlaylistDraft? refused, Failure? failure) = await PlaylistStore.ChangeMaps(db, 0, null, "add", maps => { maps.Add(new MapDraft("Bad", "Bad.map", true, 2)); return null; }, Staff);
		Assert.Null(refused);
		Assert.Equal("maps[41].path", PlaylistRulesTestsField(failure));
		Assert.Equal(41, (await PlaylistStore.Load(db, 0))!.Maps.Count);

		(PlaylistDraft? added, failure) = await PlaylistStore.ChangeMaps(db, 0, null, "add", maps => { maps.Add(new MapDraft("Pit (2)", "Pit", true, 2)); return null; }, Staff);
		Assert.Null(failure);
		Assert.Equal(42, added!.Maps.Count);
		long firstId = added.Maps[0].Id;
		Assert.Equal(2, added.Version);

		// a stale version is refused
		(_, failure) = await PlaylistStore.ChangeMaps(db, 0, 1, "remove", maps => { maps.RemoveAt(0); return null; }, Staff);
		Assert.Equal("conflict.stale", failure?.Code);

		(PlaylistDraft? moved, _) = await PlaylistStore.ChangeMaps(db, 0, 2, "reorder", maps => { (maps[0], maps[1]) = (maps[1], maps[0]); return null; }, Staff);
		Assert.Equal(firstId, moved!.Maps[1].Id);

		// the only enabled playlist cannot be deleted, a disabled one can
		(PlaylistDraft? second, _) = await PlaylistStore.Update(db, 1, null, p => p with { Enabled = false }, Staff);
		Assert.False(second!.Enabled);
		Assert.Equal(PlaylistCodes.LastEnabled, (await PlaylistStore.Delete(db, 0, null, Staff))?.Code);
		Assert.Null(await PlaylistStore.Delete(db, 1, null, Staff));
		Assert.Null(await PlaylistStore.Load(db, 1));

		string[] actions = await db.AuditLog.Where(e => e.TargetKind == AuditTargetKinds.Playlist).OrderBy(e => e.ID).Select(e => e.Action).ToArrayAsync();
		Assert.Equal([ModerationActions.PlaylistMapChange, ModerationActions.PlaylistMapChange, ModerationActions.PlaylistUpdate, ModerationActions.PlaylistDelete], actions);
	}

	private static string? PlaylistRulesTestsField(Failure? failure) => failure?.Params is { } p ? p.GetType().GetProperty("Field")?.GetValue(p) as string : null;
}

public class PlaylistAccessTests
{
	[Fact]
	public void EveryPlaylistRouteNeedsThePlaylistsPermission()
	{
		MethodInfo[] routes = typeof(PlaylistsController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).ToArray();
		Assert.NotEmpty(routes);

		AuthorizeAttribute authorize = Assert.Single(typeof(PlaylistsController).GetCustomAttributes<AuthorizeAttribute>());
		Assert.Equal(PermissionPolicies.Prefix + Permissions.PlaylistsManage, authorize.Policy);
		Assert.NotNull(typeof(PlaylistsController).GetCustomAttribute<AllowSignInCookieAttribute>());
		Assert.All(routes, route => Assert.Empty(route.GetCustomAttributes<AllowAnonymousAttribute>()));
	}

	[Fact]
	public void TheLaddersPermissionStillGuardsNothing()
	{
		Assert.DoesNotContain(Permissions.LaddersManage, typeof(PlaylistsController).GetCustomAttributes<AuthorizeAttribute>().Select(a => a.Policy));
	}

	[Fact]
	public void EveryPlaylistChangeHasAnAuditActionAndWords()
	{
		string[] actions = ["playlist_create", "playlist_update", "playlist_delete", "playlist_map_change"];

		Assert.Equal(actions, new[] { ModerationActions.PlaylistCreate, ModerationActions.PlaylistUpdate, ModerationActions.PlaylistDelete, ModerationActions.PlaylistMapChange });
		Assert.Equal("playlist", AuditTargetKinds.Playlist);
		Assert.All(actions, action =>
		{
			Assert.NotEqual(action, GenOnlineService.Portal.Client.PortalAuditActions.LabelOf(action));
			Assert.Contains(action, GenOnlineService.Portal.Client.PortalAuditActions.All);
		});
	}
}
