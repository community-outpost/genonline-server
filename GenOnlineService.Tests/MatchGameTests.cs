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

using System.Text.Json;
using GenOnlineService.Realtime;
using GenOnlineService.Rest;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GenOnlineService.Tests;

public class MatchGameTests
{
	private static MatchHistory_Entry Entry(string? game) =>
		new MatchHistory_Entry(1, -1, "m", true, "2026-11-01T12:00:00.0000000Z", "2026-11-01T12:10:00.0000000Z", "Map", "", "1v1", ELobbyType.QuickMatch,
			true, false, 10000, false, true, false, 300, 1, 1) { game = game };

	private static string Json(MatchHistory_Entry entry) => JsonSerializer.Serialize(MatchResponse.Of(entry, new Dictionary<long, int>()), RealtimeJson.Create());

	[Fact]
	public void TheMatchJsonCarriesTheGameAndLeavesItOutWhenUnknown()
	{
		Assert.Contains("\"game\":\"generals\"", Json(Entry(ClientGames.Generals)));
		Assert.Contains("\"game\":\"zerohour\"", Json(Entry(ClientGames.ZeroHour)));
		Assert.DoesNotContain("\"game\"", Json(Entry(null)));
	}

	[Fact]
	public void TheExternalLeaderboardPayloadLeavesTheGameOut()
	{
		string payload = ExternalLeaderboardsClient.PayloadOf(Entry(ClientGames.ZeroHour));
		Assert.DoesNotContain("\"game\"", payload);
		Assert.Contains("\"match_id\":1", payload);
	}

	[Theory]
	[InlineData("zerohour", "zerohour")]
	[InlineData("generals", "generals")]
	[InlineData("none", null)]
	[InlineData("", null)]
	[InlineData("Generals", null)]
	public void AMatchStoresOnlyAKnownGame(string lobbyGame, string? stored)
	{
		Assert.Equal(stored, Database.MatchHistory.StoredGameOf(lobbyGame));
	}

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task TheGameOfAMatchComesBackInItsEntry(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		await using AppDbContext db = probe.Open();
		MatchSeed.Add(db, 1, MatchSeed.Origin, game: ClientGames.Generals, players: new SeedPlayer(5));
		MatchSeed.Add(db, 2, MatchSeed.Origin.AddMinutes(1), players: new SeedPlayer(5));
		await db.SaveChangesAsync();

		MatchFeedPage page = await Database.MatchHistory.GetMatchFeed(db, new MatchFeedQuery(new MatchFeedFilter(), MatchFeedSort.Id, false, 10));
		Assert.Equal(new string?[] { ClientGames.Generals, null }, page.Matches.Select(m => m.game).ToArray());
	}

	private static IQueryCollection Query(string text) => new QueryCollection(QueryHelpers.ParseQuery(text));

	[Theory]
	[InlineData("?game=generals", null)]
	[InlineData("?game=zerohour&player=5&faction=2", null)]
	[InlineData("?player=5&faction=2", "filter_requires_game")]
	[InlineData("?faction=2", "filter_requires_player")]
	[InlineData("?game=none", "invalid_parameter")]
	[InlineData("?game=Generals", "invalid_parameter")]
	[InlineData("?game=generals&game=zerohour", "invalid_parameter")]
	[InlineData("?sort=id&game=generals", "invalid_parameter")]
	public void TheGameFilterTakesAKnownGameAndAFactionNeedsIt(string query, string? code)
	{
		(MatchFeedRequest? request, Failure? failure) = MatchFeedArgs.Read(Query(query), null);
		Assert.Equal(code, failure?.Code);
		if (code == null)
		{
			Assert.Equal(query.Contains("generals") ? ClientGames.Generals : ClientGames.ZeroHour, request!.Query.Filter.Game);
		}
		else if (code != "invalid_parameter")
		{
			Assert.Equal("faction", JsonSerializer.SerializeToElement(failure!.Value.Params).GetProperty("Parameter").GetString());
		}
	}

	[Fact]
	public void ACursorKeepsTheGameFilter()
	{
		(MatchFeedRequest? request, _) = MatchFeedArgs.Read(Query("?game=generals&player=5&faction=3"), null);
		string cursor = MatchFeedArgs.Encode(request!.Query, new MatchFeedKey(MatchSeed.Origin, 9), false);
		(MatchFeedRequest? next, Failure? failure) = MatchFeedArgs.Read(Query("?cursor=" + Uri.EscapeDataString(cursor)), null);
		Assert.Null(failure);
		Assert.Equal(ClientGames.Generals, next!.Query.Filter.Game);
		Assert.Equal(3, next.Query.Filter.Side);
	}

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task TheGameFilterKeepsOnlyMatchesOfThatGame(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		await using AppDbContext db = probe.Open();
		DateTime t = MatchSeed.Origin;
		MatchSeed.Add(db, 1, t.AddMinutes(1), game: ClientGames.Generals, players: [new SeedPlayer(5, Side: 2), new SeedPlayer(6)]);
		MatchSeed.Add(db, 2, t.AddMinutes(2), game: ClientGames.ZeroHour, players: [new SeedPlayer(5, Side: 2), new SeedPlayer(6)]);
		MatchSeed.Add(db, 3, t.AddMinutes(3), players: [new SeedPlayer(5, Side: 2)]);
		MatchSeed.Add(db, 4, t.AddMinutes(4), game: ClientGames.Generals, map: "Alpine Assault", players: [new SeedPlayer(5, Side: 3)]);
		await db.SaveChangesAsync();

		async Task<long[]> Of(MatchFeedFilter filter) =>
			(await Database.MatchHistory.GetMatchFeed(db, new MatchFeedQuery(filter, MatchFeedSort.FinishedAt, true, 20))).Matches.Select(m => m.match_id).ToArray();

		Assert.Equal(new long[] { 4, 1 }, await Of(new MatchFeedFilter { Game = ClientGames.Generals }));
		Assert.Equal(new long[] { 2 }, await Of(new MatchFeedFilter { Game = ClientGames.ZeroHour }));
		Assert.Equal(new long[] { 4 }, await Of(new MatchFeedFilter { Game = ClientGames.Generals, Map = "Alpine Assault" }));
		Assert.Equal(new long[] { 1 }, await Of(new MatchFeedFilter { Players = [5], Game = ClientGames.Generals, Side = 2 }));
		Assert.Equal(new long[] { 2 }, await Of(new MatchFeedFilter { Players = [5, 6], Game = ClientGames.ZeroHour }));
	}
}
