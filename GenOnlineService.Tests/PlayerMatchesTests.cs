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

using Xunit;

namespace GenOnlineService.Tests;

public class PlayerMatchesProbeTests
{
	private const long Player = 71, Other = 72;

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task APlayersFinishedMatchesComeNewestFirstAndContinueBelowAnId(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		await using AppDbContext db = probe.Open();
		DateTime now = DateTime.UtcNow;

		foreach (long id in new long[] { 1, 2, 3, 4 })
		{
			db.Matches.Add(new Match { Id = id, Name = "m" + id, MapName = "Tournament Desert", RosterType = "1v1", StartedAt = now.AddHours(-id), FinishedAt = id == 4 ? null : now.AddHours(-id) });
		}
		db.MatchPlayers.AddRange(
			new MatchPlayer { MatchId = 1, Slot = 0, UserId = Player },
			new MatchPlayer { MatchId = 2, Slot = 0, UserId = Player },
			new MatchPlayer { MatchId = 2, Slot = 1, UserId = Other },
			new MatchPlayer { MatchId = 3, Slot = 0, UserId = Other },
			new MatchPlayer { MatchId = 4, Slot = 0, UserId = Player });
		await db.SaveChangesAsync();
		db.ChangeTracker.Clear();

		// 3 is another player's and 4 is still running
		Assert.Equal(new long[] { 2, 1 }, (await Database.MatchHistory.GetMatchesOfPlayer(db, Player, null, 10)).Select(m => m.match_id).ToArray());
		Assert.Equal(new long[] { 2 }, (await Database.MatchHistory.GetMatchesOfPlayer(db, Player, null, 1)).Select(m => m.match_id).ToArray());
		Assert.Equal(new long[] { 1 }, (await Database.MatchHistory.GetMatchesOfPlayer(db, Player, 2, 10)).Select(m => m.match_id).ToArray());
		Assert.Empty(await Database.MatchHistory.GetMatchesOfPlayer(db, Player, 1, 10));
	}
}
