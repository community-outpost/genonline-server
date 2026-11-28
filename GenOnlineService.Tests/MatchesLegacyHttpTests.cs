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

using System.Security.Claims;
using System.Text.Json;
using GenOnlineService.Rest;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GenOnlineService.Tests;

// The rows and Link headers of the paging parameters the first REST release had, recorded against 1939de82
public class MatchesLegacyHttpTests
{
	private static readonly DateTime Origin = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

	internal sealed class OneContextFactory(ProbeDatabase probe) : IDbContextFactory<AppDbContext>
	{
		public AppDbContext CreateDbContext() => probe.Open();
	}

	internal sealed class Grants(params string[] permissions) : IAuthorizationService
	{
		public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, IEnumerable<IAuthorizationRequirement> requirements) => Task.FromResult(AuthorizationResult.Failed());

		public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, string policyName) =>
			Task.FromResult(permissions.Select(PermissionPolicies.For).Contains(policyName) ? AuthorizationResult.Success() : AuthorizationResult.Failed());
	}

	internal static MatchesController Controller(ProbeDatabase probe, string path, string query)
	{
		DefaultHttpContext http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "1")], "test")) };
		http.Request.Path = path;
		http.Request.QueryString = new QueryString(query);
		return new MatchesController(new OneContextFactory(probe), new Grants(Permissions.MatchHistoryRead)) { ControllerContext = new ControllerContext { HttpContext = http } };
	}

	internal static (int Status, JsonElement Body) Read(IActionResult result) => result switch
	{
		JsonResult json => (json.StatusCode ?? 200, JsonSerializer.SerializeToElement(json.Value, GenOnlineService.Api.ApiResults.Json)),
		_ => throw new InvalidOperationException(result.GetType().Name)
	};

	internal static long[] IdsOf(JsonElement body) => body.GetProperty("matches").EnumerateArray().Select(m => m.GetProperty("id").GetInt64()).ToArray();

	private static async Task<(long[] Ids, string Link)> List(ProbeDatabase probe, string query)
	{
		MatchesController controller = Controller(probe, "/api/v1/matches", query);
		(int status, JsonElement body) = Read(await controller.List());
		Assert.Equal(200, status);
		return (IdsOf(body), controller.HttpContext.Response.Headers.Link.ToString());
	}

	private static async Task<(long[] Ids, string Link)> OfPlayer(ProbeDatabase probe, long user, string query)
	{
		MatchesController controller = Controller(probe, $"/api/v1/players/{user}/matches", query);
		(int status, JsonElement body) = Read(await controller.OfPlayer(user));
		Assert.Equal(200, status);
		return (IdsOf(body), controller.HttpContext.Response.Headers.Link.ToString());
	}

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task AfterIdSinceAndBeforeIdKeepTheirRowsAndLinks(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		await using (AppDbContext db = probe.Open())
		{
			db.Users.Add(new User { ID = 5, Username = "five", UsernameNormalized = "five", CreatedAt = Origin, UpdatedAt = Origin });
			for (long id = 1; id <= 6; ++id)
			{
				// match 3 finishes after match 4, and 6 is still running
				DateTime finished = Origin.AddMinutes(id == 3 ? 4.5 : id);
				db.Matches.Add(new Match { Id = id, Name = "m" + id, MapName = "Tournament Desert", RosterType = "1v1", StartedAt = Origin, FinishedAt = id == 6 ? null : finished });
				if (id != 3)
				{
					db.MatchPlayers.Add(new MatchPlayer { MatchId = id, Slot = 0, UserId = 5 });
				}
			}
			await db.SaveChangesAsync();
		}

		Same([2, 3], "</api/v1/matches?after_id=3&per_page=2>; rel=\"next\"", await List(probe, "?after_id=1&per_page=2"));
		Same([5], "", await List(probe, "?after_id=4&per_page=2"));
		Same([3, 4, 5], "", await List(probe, "?since=2026-11-01T12:04:00Z"));
		Same([3, 4], "</api/v1/matches?since=2026-11-01T12%3A04%3A00Z&per_page=2&after_id=4>; rel=\"next\"", await List(probe, "?since=2026-11-01T12:04:00Z&per_page=2"));
		Same([4], "</api/v1/players/5/matches?before_id=4&per_page=1>; rel=\"next\"", await OfPlayer(probe, 5, "?before_id=5&per_page=1"));
		Same([2, 1], "", await OfPlayer(probe, 5, "?before_id=4"));

		// unknown parameters stay ignored as the first release ignored them
		Same([5], "", await List(probe, "?after_id=4&per_page=2&page=9"));
		Same([2, 1], "", await OfPlayer(probe, 5, "?before_id=4&after_id=1"));
	}

	private static void Same(long[] ids, string link, (long[] Ids, string Link) actual)
	{
		Assert.Equal(ids, actual.Ids);
		Assert.Equal(link, actual.Link);
	}
}
