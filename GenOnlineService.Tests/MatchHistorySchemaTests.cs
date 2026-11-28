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
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace GenOnlineService.Tests;

public class MatchHistorySchemaTests
{
	[Fact]
	public void TeamSizeIsTheLargestTeamWithObserversLeftOutAndLonePlayersCountingOne()
	{
		Assert.Equal((short)2, Database.MatchHistory.TeamSizeOf([
			new MatchPlayer { Slot = 0, Team = 0, Side = 1 },
			new MatchPlayer { Slot = 1, Team = 0, Side = 2 },
			new MatchPlayer { Slot = 2, Team = 1, Side = 3 },
			new MatchPlayer { Slot = 3, Team = 1, Side = -2 },
			new MatchPlayer { Slot = 4, Team = 1, Side = -2 }]));
		Assert.Equal((short)1, Database.MatchHistory.TeamSizeOf([
			new MatchPlayer { Slot = 0, Team = -1, Side = 1 },
			new MatchPlayer { Slot = 1, Team = -1, Side = 2 },
			new MatchPlayer { Slot = 2, Team = null, Side = 3 }]));
		Assert.Null(Database.MatchHistory.TeamSizeOf([new MatchPlayer { Slot = 0, Team = 0, Side = -2 }]));
	}

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task FinishingAMatchStoresTeamSizeDesyncAndThePlayersFinishTime(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		await using AppDbContext db = probe.Open();

		db.Matches.Add(new Match { Id = 10, Name = "m", MapName = "Tournament Desert", RosterType = "2v1", StartedAt = DateTime.UtcNow.AddMinutes(-20) });
		db.MatchPlayers.AddRange(
			new MatchPlayer { MatchId = 10, Slot = 0, UserId = 1, Team = 0, Side = 1 },
			new MatchPlayer { MatchId = 10, Slot = 1, UserId = 2, Team = 0, Side = 2 },
			new MatchPlayer { MatchId = 10, Slot = 2, UserId = 3, Team = 1, Side = 3, Details = "{\"desynced\":true}" });
		db.Matches.Add(new Match { Id = 11, Name = "m", MapName = "Tournament Desert", RosterType = "1v1", StartedAt = DateTime.UtcNow.AddMinutes(-20) });
		db.MatchPlayers.AddRange(
			new MatchPlayer { MatchId = 11, Slot = 0, UserId = 1, Team = -1, Side = 1 },
			new MatchPlayer { MatchId = 11, Slot = 1, UserId = 2, Team = -1, Side = 2 });
		await db.SaveChangesAsync();
		db.ChangeTracker.Clear();

		await Database.MatchHistory.FinishMatch(db, 10, default);
		await Database.MatchHistory.FinishMatch(db, 11, default);

		Match first = await db.Matches.SingleAsync(m => m.Id == 10);
		Assert.Equal((short)2, first.TeamSize);
		Assert.True(first.Desynced);
		Assert.All(await db.MatchPlayers.Where(p => p.MatchId == 10).ToListAsync(), p => Assert.Equal(first.FinishedAt, p.FinishedAt));

		Match second = await db.Matches.SingleAsync(m => m.Id == 11);
		Assert.Equal((short)1, second.TeamSize);
		Assert.False(second.Desynced);

		// A desync the player reports after the finish still marks the match
		await Database.MatchHistory.CommitPlayerOutcome(db, 1, 11, 2, 0, 0, 0, 0, 0, 0, 0, false, true, 600);
		Assert.True((await db.Matches.SingleAsync(m => m.Id == 11)).Desynced);
	}

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task TheMigrationBackfillsTeamSizeDesyncAndThePlayersFinishTime(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider, migrate: false);
		if (probe == null) return;
		await using AppDbContext db = probe.Open();
		IMigrator migrator = db.GetService<IMigrator>();
		await migrator.MigrateAsync("ChatRooms");

		const string Columns = "id, name, roster_type, map_name, is_official_map, vanilla_teams, starting_cash, limit_superweapons, track_stats, allow_observers, max_cam_height, exe_crc, ini_crc, started_at, finished_at";
		await db.Database.ExecuteSqlRawAsync($"INSERT INTO matches ({Columns}) VALUES "
			+ "(1, 'a', '2v1', 'Map', FALSE, FALSE, 10000, FALSE, TRUE, FALSE, 300, 1, 1, '2026-01-01 10:00:00', '2026-01-01 10:30:00'),"
			+ "(2, 'b', '1v1', 'Map', FALSE, FALSE, 10000, FALSE, TRUE, FALSE, 300, 1, 1, '2026-01-01 11:00:00', NULL)");
		await db.Database.ExecuteSqlRawAsync("INSERT INTO match_players (match_id, slot, user_id, team, side, color, details) VALUES "
			+ "(1, 0, 5, 0, 1, 0, '{{\"desynced\":true}}'), (1, 1, 6, 0, 2, 1, NULL), (1, 2, 7, 1, 3, 2, NULL), (1, 3, 8, 1, -2, 3, NULL),"
			+ "(2, 0, 5, -1, 1, 0, NULL), (2, 1, 6, -1, 2, 1, NULL)");

		await migrator.MigrateAsync();
		db.ChangeTracker.Clear();

		Match finished = await db.Matches.SingleAsync(m => m.Id == 1);
		Assert.Equal((short)2, finished.TeamSize);
		Assert.True(finished.Desynced);
		Assert.All(await db.MatchPlayers.Where(p => p.MatchId == 1).ToListAsync(), p => Assert.Equal(finished.FinishedAt, p.FinishedAt));

		// A running match gets its team size when it finishes
		Match running = await db.Matches.SingleAsync(m => m.Id == 2);
		Assert.Null(running.TeamSize);
		Assert.False(running.Desynced);
		Assert.All(await db.MatchPlayers.Where(p => p.MatchId == 2).ToListAsync(), p => Assert.Null(p.FinishedAt));
	}
}
