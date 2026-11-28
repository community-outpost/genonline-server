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

using System.Buffers.Text;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using GenOnlineService.Rest;
using Microsoft.AspNetCore.Authorization;
using Xunit;
using static GenOnlineService.Tests.MatchesLegacyHttpTests;

namespace GenOnlineService.Tests;

public class MatchesFeedHttpTests
{
	private sealed record Answer(int Status, JsonElement Body, string Link, string Deprecation);

	private static async Task<Answer> Get(ProbeDatabase probe, string query, long? player = null)
	{
		MatchesController controller = Controller(probe, player == null ? "/api/v1/matches" : $"/api/v1/players/{player}/matches", query);
		(int status, JsonElement body) = Read(player == null ? await controller.List() : await controller.OfPlayer(player.Value));
		var headers = controller.HttpContext.Response.Headers;
		return new Answer(status, body, headers.Link.ToString(), headers["Deprecation"].ToString());
	}

	// The query of one rel of a Link header
	private static string LinkQuery(string link, string rel)
	{
		string part = link.Split(", ").Single(p => p.EndsWith($"rel=\"{rel}\""));
		string url = part.Substring(1, part.IndexOf('>') - 1);
		return url.Substring(url.IndexOf('?'));
	}

	private static async Task<ProbeDatabase?> Seeded(string provider)
	{
		ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return null;
		await using AppDbContext db = probe.Open();
		db.Users.Add(new User { ID = 5, Username = "five", UsernameNormalized = "five", CreatedAt = MatchSeed.Origin, UpdatedAt = MatchSeed.Origin });
		for (long id = 1; id <= 5; ++id)
		{
			MatchSeed.Add(db, id, MatchSeed.Origin.AddMinutes(id), map: id == 2 ? "Alpine Assault" : "Tournament Desert", players: new SeedPlayer(5));
		}
		await db.SaveChangesAsync();
		return probe;
	}

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task TheLinksAndCursorsWalkBothWays(string provider)
	{
		await using ProbeDatabase? probe = await Seeded(provider);
		if (probe == null) return;

		Answer first = await Get(probe, "?per_page=2");
		Assert.Equal(200, first.Status);
		Assert.Equal(new long[] { 5, 4 }, IdsOf(first.Body));
		Assert.False(first.Body.TryGetProperty("prev_cursor", out _));
		Assert.False(first.Body.TryGetProperty("partial", out _));
		Assert.Equal("", first.Deprecation);
		string nextQuery = LinkQuery(first.Link, "next");
		Assert.Equal($"?cursor={first.Body.GetProperty("next_cursor").GetString()}&per_page=2", nextQuery);
		Assert.DoesNotContain("rel=\"prev\"", first.Link);

		Answer second = await Get(probe, nextQuery);
		Assert.Equal(new long[] { 3, 2 }, IdsOf(second.Body));
		Answer third = await Get(probe, LinkQuery(second.Link, "next"));
		Assert.Equal(new long[] { 1 }, IdsOf(third.Body));
		Assert.False(third.Body.TryGetProperty("next_cursor", out _));
		Answer back = await Get(probe, LinkQuery(third.Link, "prev"));
		Assert.Equal(new long[] { 3, 2 }, IdsOf(back.Body));
		Answer start = await Get(probe, LinkQuery(back.Link, "prev"));
		Assert.Equal(new long[] { 5, 4 }, IdsOf(start.Body));

		// the cursor keeps the filters and the player of the route
		Answer filtered = await Get(probe, "?map=Tournament%20Desert&order=asc&per_page=2", player: 5);
		Assert.Equal(new long[] { 1, 3 }, IdsOf(filtered.Body));
		Assert.Equal(new long[] { 4, 5 }, IdsOf((await Get(probe, LinkQuery(filtered.Link, "next"), player: 5)).Body));
		Assert.Equal(400, (await Get(probe, LinkQuery(filtered.Link, "next"), player: 6)).Status);

		Assert.Equal(new long[] { 1, 2, 3, 4, 5 }, IdsOf((await Get(probe, "?sort=id&order=asc")).Body));
	}

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task AnUnknownPlayerIsNotFoundAndALegacyCallIsDeprecated(string provider)
	{
		await using ProbeDatabase? probe = await Seeded(provider);
		if (probe == null) return;

		Answer unknown = await Get(probe, "", player: 99);
		Assert.Equal(404, unknown.Status);
		Assert.Equal("not_found", unknown.Body.GetProperty("error").GetString());
		Assert.Equal(404, (await Get(probe, "?before_id=3", player: 99)).Status);

		Assert.Equal(new long[] { 5, 4, 3, 2, 1 }, IdsOf((await Get(probe, "", player: 5)).Body));
		Assert.Equal("true", (await Get(probe, "?before_id=3", player: 5)).Deprecation);
		Answer legacy = await Get(probe, "?after_id=0");
		Assert.Equal("true", legacy.Deprecation);
		Assert.False(legacy.Body.TryGetProperty("next_cursor", out _));
	}

	[Fact]
	public void TheListsNeedMatchHistoryRead()
	{
		foreach (string action in new[] { nameof(MatchesController.List), nameof(MatchesController.OfPlayer) })
		{
			AuthorizeAttribute authorize = typeof(MatchesController).GetMethod(action)!.GetCustomAttribute<AuthorizeAttribute>()!;
			Assert.Equal(PermissionPolicies.For(Permissions.MatchHistoryRead), authorize.Policy);
		}
	}

	private static string CursorWith(string cursor, Action<JsonObject> change)
	{
		JsonObject json = JsonNode.Parse(Base64Url.DecodeFromChars(cursor))!.AsObject();
		change(json);
		return Base64Url.EncodeToString(Encoding.UTF8.GetBytes(json.ToJsonString()));
	}

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task BadArgumentsAnswer400WithTheirCode(string provider)
	{
		await using ProbeDatabase? probe = await Seeded(provider);
		if (probe == null) return;
		string cursor = (await Get(probe, "?per_page=1&map=Tournament%20Desert")).Body.GetProperty("next_cursor").GetString()!;
		string Q(string value) => Uri.EscapeDataString(value);

		(string Query, long? Player, string Code, string? Parameter)[] cases =
		[
			("?per_page=0", null, "invalid_argument", null),
			("?per_page=101", null, "invalid_argument", null),
			("?finished_after=2026-11-01", null, "invalid_parameter", "finished_after"),
			("?finished_after=2026-11-02T00:00:00Z&finished_before=2026-11-01T00:00:00Z", null, "invalid_parameter", "finished_before"),
			("?duration_min=10&duration_max=5", null, "invalid_parameter", "duration_max"),
			("?team_size=5", null, "invalid_parameter", "team_size"),
			("?type=ranked", null, "invalid_parameter", "type"),
			("?player=5,5", null, "invalid_parameter", "player"),
			("?player=1&player=2&player=3&player=4&player=6", null, "invalid_parameter", "player"),
			("?player=1,2,3,4", 5, "invalid_parameter", "player"),
			("?map=a&map=b", null, "invalid_parameter", "map"),
			("?sort=id&map=Tournament%20Desert", null, "invalid_parameter", "map"),
			("?sort=name", null, "invalid_parameter", "sort"),
			("?faction=1", null, "filter_requires_player", "faction"),
			("?result=win", null, "filter_requires_player", "result"),
			("?after_id=1&map=x", null, "mixed_paging", null),
			("?since=2026-11-01T00:00:00Z&cursor=" + Q(cursor), null, "mixed_paging", null),
			("?before_id=1&order=asc", 5, "mixed_paging", null),
			("?cursor=" + Q(cursor) + "&map=Alpine%20Assault", null, "invalid_cursor", null),
			("?cursor=" + Q(cursor.Substring(0, cursor.Length - 4)), null, "invalid_cursor", null),
			("?cursor=" + Q("x" + cursor), null, "invalid_cursor", null),
			("?cursor=" + Q(CursorWith(cursor, json => json["v"] = 2)), null, "invalid_cursor", null),
			("?cursor=" + Q(CursorWith(cursor, json => json["f"]!["map"] = "Alpine Assault")), null, "invalid_cursor", null),
			("?cursor=" + Q(new string('a', 513)), null, "invalid_cursor", null),
		];
		// A request without a cursor, sort or filter ignores unknown parameters as the first REST release did
		Assert.Equal(200, (await Get(probe, "?maps=x")).Status);
		Assert.Equal(200, (await Get(probe, "?after_id=1", 5)).Status);
		foreach ((string query, long? player, string code, string? parameter) in cases)
		{
			Answer answer = await Get(probe, query, player);
			Assert.True(answer.Status == 400, $"{query}: {answer.Status} {answer.Body}");
			Assert.True(code == answer.Body.GetProperty("error").GetString(), $"{query}: {answer.Body}");
			if (parameter != null)
			{
				Assert.Equal(parameter, answer.Body.GetProperty("params").GetProperty("parameter").GetString());
			}
		}

		// a cursor with its own filters edited consistently still only reads what the filters allow
		Assert.Equal(200, (await Get(probe, "?cursor=" + Q(cursor) + "&per_page=3")).Status);
	}
}
