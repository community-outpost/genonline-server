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

using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GenOnlineService.Tests;

public sealed record SeedPlayer(long User, int Side = 1, string Result = MatchResults.Win, short Team = -1);

public static class MatchSeed
{
	public static readonly DateTime Origin = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

	public static void Add(AppDbContext db, long id, DateTime finished, string map = "Tournament Desert", ELobbyType type = ELobbyType.CustomGame, bool rated = true,
		short teamSize = 1, bool desynced = false, int durationS = 600, params SeedPlayer[] players)
	{
		db.Matches.Add(new Match
		{
			Id = id, Name = "m" + id, MapName = map, LobbyType = type, TrackStats = rated, TeamSize = teamSize, Desynced = desynced, RosterType = "1v1",
			StartedAt = finished.AddSeconds(-durationS), FinishedAt = finished
		});
		short slot = 0;
		foreach (SeedPlayer player in players)
		{
			db.MatchPlayers.Add(new MatchPlayer { MatchId = id, Slot = slot++, UserId = player.User, Side = player.Side, Result = player.Result, Team = player.Team, FinishedAt = finished });
		}
	}
}

public class MatchFeedTests
{
	private static MatchFeedQuery Newest(int perPage, MatchFeedFilter? filter = null) =>
		new MatchFeedQuery(filter ?? new MatchFeedFilter(), MatchFeedSort.FinishedAt, true, perPage);

	private static long[] Ids(MatchFeedPage page) => page.Matches.Select(m => m.match_id).ToArray();

	private static async Task<List<long>> WalkForward(AppDbContext db, MatchFeedQuery first)
	{
		List<long> ids = new();
		MatchFeedQuery? query = first;
		for (int guard = 0; query != null && guard < 100; ++guard)
		{
			MatchFeedPage page = await Database.MatchHistory.GetMatchFeed(db, query);
			ids.AddRange(Ids(page));
			query = page.Next is MatchFeedKey next ? query with { Boundary = next, Backward = false } : null;
		}
		return ids;
	}

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task PagesWalkForwardAndBackWithoutGapsOrDuplicates(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		await using AppDbContext db = probe.Open();
		for (long id = 1; id <= 7; ++id)
		{
			MatchSeed.Add(db, id, MatchSeed.Origin.AddMinutes(id));
		}
		await db.SaveChangesAsync();

		MatchFeedPage first = await Database.MatchHistory.GetMatchFeed(db, Newest(3));
		Assert.Equal(new long[] { 7, 6, 5 }, Ids(first));
		Assert.Null(first.Prev);
		MatchFeedPage second = await Database.MatchHistory.GetMatchFeed(db, Newest(3) with { Boundary = first.Next });
		Assert.Equal(new long[] { 4, 3, 2 }, Ids(second));
		MatchFeedPage third = await Database.MatchHistory.GetMatchFeed(db, Newest(3) with { Boundary = second.Next });
		Assert.Equal(new long[] { 1 }, Ids(third));
		Assert.Null(third.Next);
		Assert.False(third.Partial);

		MatchFeedPage back = await Database.MatchHistory.GetMatchFeed(db, Newest(3) with { Boundary = third.Prev, Backward = true });
		Assert.Equal(new long[] { 4, 3, 2 }, Ids(back));
		MatchFeedPage backAgain = await Database.MatchHistory.GetMatchFeed(db, Newest(3) with { Boundary = back.Prev, Backward = true });
		Assert.Equal(new long[] { 7, 6, 5 }, Ids(backAgain));
		Assert.Null(backAgain.Prev);
		Assert.NotNull(backAgain.Next);

		Assert.Equal(new long[] { 1, 2, 3, 4, 5, 6, 7 }, await WalkForward(db, new MatchFeedQuery(new MatchFeedFilter(), MatchFeedSort.FinishedAt, false, 2)));
		Assert.Equal(new long[] { 1, 2, 3, 4, 5, 6, 7 }, await WalkForward(db, new MatchFeedQuery(new MatchFeedFilter(), MatchFeedSort.Id, false, 3)));
	}

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task MatchesFinishedAtTheSameTimeComeInIdOrder(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		await using AppDbContext db = probe.Open();
		foreach (long id in new long[] { 13, 11, 15, 12, 14 })
		{
			MatchSeed.Add(db, id, MatchSeed.Origin, players: new SeedPlayer(5));
		}
		await db.SaveChangesAsync();

		Assert.Equal(new long[] { 11, 12, 13, 14, 15 }, await WalkForward(db, new MatchFeedQuery(new MatchFeedFilter(), MatchFeedSort.FinishedAt, false, 2)));
		Assert.Equal(new long[] { 15, 14, 13, 12, 11 }, await WalkForward(db, new MatchFeedQuery(new MatchFeedFilter { Players = [5] }, MatchFeedSort.FinishedAt, true, 2)));
	}

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task EachFilterKeepsOnlyItsMatches(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		await using AppDbContext db = probe.Open();
		DateTime t = MatchSeed.Origin;
		MatchSeed.Add(db, 1, t.AddMinutes(1), map: "Alpine Assault", players: [new SeedPlayer(5, Side: 2, Result: MatchResults.Win), new SeedPlayer(6, Side: 3, Result: MatchResults.Loss)]);
		MatchSeed.Add(db, 2, t.AddMinutes(2), type: ELobbyType.QuickMatch, players: [new SeedPlayer(5, Side: 3, Result: MatchResults.Loss), new SeedPlayer(7)]);
		MatchSeed.Add(db, 3, t.AddMinutes(3), rated: false, players: [new SeedPlayer(6), new SeedPlayer(7)]);
		MatchSeed.Add(db, 4, t.AddMinutes(4), teamSize: 2, durationS: 3000, players: [new SeedPlayer(5), new SeedPlayer(6), new SeedPlayer(7)]);
		MatchSeed.Add(db, 5, t.AddMinutes(5), desynced: true, durationS: 60, players: new SeedPlayer(8));
		await db.SaveChangesAsync();

		async Task<long[]> Of(MatchFeedFilter filter) => Ids(await Database.MatchHistory.GetMatchFeed(db, Newest(20, filter)));

		Assert.Equal(new long[] { 1 }, await Of(new MatchFeedFilter { Map = "Alpine Assault" }));
		Assert.Empty(await Of(new MatchFeedFilter { Map = "Nowhere" }));
		Assert.Equal(new long[] { 2 }, await Of(new MatchFeedFilter { Type = ELobbyType.QuickMatch }));
		Assert.Equal(new long[] { 5, 4, 3, 1 }, await Of(new MatchFeedFilter { Type = ELobbyType.CustomGame }));
		Assert.Equal(new long[] { 3 }, await Of(new MatchFeedFilter { Rated = false }));
		Assert.Equal(new long[] { 4 }, await Of(new MatchFeedFilter { TeamSize = 2 }));
		Assert.Empty(await Of(new MatchFeedFilter { TeamSize = 3 }));
		Assert.Equal(new long[] { 5 }, await Of(new MatchFeedFilter { Desync = true }));
		Assert.Equal(new long[] { 4, 3, 2, 1 }, await Of(new MatchFeedFilter { Desync = false }));
		Assert.Equal(new long[] { 3, 2 }, await Of(new MatchFeedFilter { FinishedAfter = t.AddMinutes(2), FinishedBefore = t.AddMinutes(4) }));
		Assert.Equal(new long[] { 4 }, await Of(new MatchFeedFilter { DurationMin = 3000 }));
		Assert.Equal(new long[] { 5 }, await Of(new MatchFeedFilter { DurationMax = 60 }));
		Assert.Equal(new long[] { 3, 2, 1 }, await Of(new MatchFeedFilter { DurationMin = 61, DurationMax = 2999 }));

		// several equality filters: one leads, the others are checked on the rows it reads
		Assert.Equal(new long[] { 4 }, await Of(new MatchFeedFilter { Type = ELobbyType.CustomGame, TeamSize = 2, Rated = true }));
		Assert.Empty(await Of(new MatchFeedFilter { Map = "Alpine Assault", Type = ELobbyType.QuickMatch }));

		Assert.Equal(new long[] { 4, 2, 1 }, await Of(new MatchFeedFilter { Players = [5] }));
		Assert.Equal(new long[] { 4, 1 }, await Of(new MatchFeedFilter { Players = [5, 6] }));
		Assert.Equal(new long[] { 4 }, await Of(new MatchFeedFilter { Players = [5, 6, 7] }));
		Assert.Empty(await Of(new MatchFeedFilter { Players = [5, 8] }));
		Assert.Equal(new long[] { 1 }, await Of(new MatchFeedFilter { Players = [5], Side = 2 }));
		Assert.Equal(new long[] { 2 }, await Of(new MatchFeedFilter { Players = [5], Result = MatchResults.Loss }));
		Assert.Equal(new long[] { 1 }, await Of(new MatchFeedFilter { Players = [6, 5], Result = MatchResults.Loss }));
		Assert.Equal(new long[] { 2 }, await Of(new MatchFeedFilter { Players = [5], Type = ELobbyType.QuickMatch }));
		Assert.Equal(new long[] { 4, 2, 1 }, Ids(await Database.MatchHistory.GetMatchFeed(db, new MatchFeedQuery(new MatchFeedFilter { Players = [5] }, MatchFeedSort.Id, true, 20))));
	}

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task ARareFilterBeyondTheScanBudgetReturnsPartialPagesThatStillFindEveryMatch(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		await using AppDbContext db = probe.Open();
		// with per_page 1 the budget is 50 rows: the rare matches sit 120 rows apart
		long[] rare = [5, 125, 245];
		for (long id = 1; id <= 250; ++id)
		{
			MatchSeed.Add(db, id, MatchSeed.Origin.AddSeconds(id), map: rare.Contains(id) ? "Rare" : "Common", durationS: rare.Contains(id) ? 60 : 600);
		}
		await db.SaveChangesAsync();

		MatchFeedQuery query = Newest(1, new MatchFeedFilter { Map = "Common", DurationMax = 60 });
		MatchFeedPage first = await Database.MatchHistory.GetMatchFeed(db, query);
		Assert.Empty(first.Matches);
		Assert.True(first.Partial);
		// the 50 examined Common rows run from 250 down to 200, skipping the rare 245
		Assert.Equal(200, first.Next!.Value.Id);
		Assert.Empty(await WalkForward(db, query));

		List<long> found = await WalkForward(db, Newest(1, new MatchFeedFilter { DurationMax = 60 }));
		Assert.Equal(new long[] { 245, 125, 5 }, found);
	}

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task AUserHoldingTwoSlotsSeesTheMatchOnce(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		await using AppDbContext db = probe.Open();
		MatchSeed.Add(db, 1, MatchSeed.Origin, players: [new SeedPlayer(5, Side: 1), new SeedPlayer(5, Side: 2, Result: MatchResults.Loss)]);
		MatchSeed.Add(db, 2, MatchSeed.Origin.AddMinutes(1), players: [new SeedPlayer(5), new SeedPlayer(6)]);
		await db.SaveChangesAsync();

		Assert.Equal(new long[] { 2, 1 }, (await Database.MatchHistory.GetMatchesOfPlayer(db, 5, null, 10)).Select(m => m.match_id).ToArray());
		Assert.Equal(new long[] { 2, 1 }, await WalkForward(db, Newest(1, new MatchFeedFilter { Players = [5] })));
		Assert.Equal(new long[] { 1 }, await WalkForward(db, Newest(10, new MatchFeedFilter { Players = [5], Side = 2 })));
		Assert.Equal(new long[] { 2, 1 }, await WalkForward(db, Newest(10, new MatchFeedFilter { Players = [5], Result = MatchResults.Win })));
	}

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task NoQueryShapeUsesOffsetOrCount(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		await using AppDbContext db = probe.Open();
		MatchFeedKey key = new MatchFeedKey(MatchSeed.Origin, 9);
		MatchFeedFilter[] filters =
		[
			new MatchFeedFilter(),
			new MatchFeedFilter { Map = "x", Rated = true, DurationMin = 1 },
			new MatchFeedFilter { TeamSize = 2 },
			new MatchFeedFilter { Type = ELobbyType.QuickMatch, FinishedAfter = MatchSeed.Origin, FinishedBefore = MatchSeed.Origin.AddDays(1) },
			new MatchFeedFilter { Desync = true },
			new MatchFeedFilter { Players = [1, 2], Side = 3, Result = MatchResults.Win },
		];
		foreach (MatchFeedFilter filter in filters)
		{
			foreach (MatchFeedSort sort in new[] { MatchFeedSort.FinishedAt, MatchFeedSort.Id })
			{
				foreach (bool backward in new[] { false, true })
				{
					string sql = Database.MatchHistory.BuildFeedQuery(db, new MatchFeedQuery(filter, sort, true, 20, key, backward)).Rows.ToQueryString();
					Assert.DoesNotContain("OFFSET", sql, StringComparison.OrdinalIgnoreCase);
					Assert.DoesNotContain("COUNT(", sql, StringComparison.OrdinalIgnoreCase);
				}
			}
		}
	}
}
