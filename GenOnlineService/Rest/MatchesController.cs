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
using GenOnlineService.OpenApi;
using GenOnlineService.Realtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace GenOnlineService.Rest
{
	public readonly record struct MatchListArgs(long? AfterID, DateTime? Since, int PerPage);

	public static class MatchArgs
	{
		public static (MatchListArgs Args, Failure? Failure) Read(IQueryCollection query)
		{
			long? afterID = null;
			DateTime? since = null;
			int perPage = OffsetPage.DefaultPerPage;

			if (query.TryGetValue("after_id", out var afterText))
			{
				if (!long.TryParse(afterText, NumberStyles.None, CultureInfo.InvariantCulture, out long after))
				{
					return (default, Result.Invalid("after_id"));
				}
				afterID = after;
			}

			if (query.TryGetValue("since", out var sinceText))
			{
				// ISO 8601 with a time; a time without Z or an offset is read as UTC
				string text = sinceText.ToString();
				if (!text.Contains('T') || !DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset parsed))
				{
					return (default, Result.Invalid("since"));
				}
				since = parsed.UtcDateTime;
			}

			if (query.TryGetValue("per_page", out var perPageText) && (!int.TryParse(perPageText, NumberStyles.None, CultureInfo.InvariantCulture, out perPage) || perPage < 1 || perPage > OffsetPage.MaxPerPage))
			{
				return (default, Result.Invalid("per_page"));
			}

			return (new MatchListArgs(afterID, since, perPage), null);
		}
	}

	[ApiController]
	[Authorize]
	public sealed class MatchesController : ControllerBase
	{
		private readonly IDbContextFactory<AppDbContext> m_dbFactory;
		private readonly IAuthorizationService m_authorization;

		public MatchesController(IDbContextFactory<AppDbContext> dbFactory, IAuthorizationService authorization)
		{
			m_dbFactory = dbFactory;
			m_authorization = authorization;
		}

		private const string FeedDescription =
			"Pages by cursor: follow `next_cursor` or `prev_cursor` (also sent as `Link` rel=\"next\" and rel=\"prev\"), sending only `cursor` and `per_page` with it. "
			+ "The cursor carries the sort and the filters. No total is counted. `partial: true` means the page stopped at its scan budget before it filled: keep following `next_cursor`.\n\n"
			+ "`sort=id` allows only `player` and `order` and walks every finished match once, as a full crawl. `finished_after` with `order=asc` is the incremental feed.\n\n"
			+ "A 400 names its reason: `invalid_cursor`, `invalid_parameter` (`params.parameter`), `unknown_parameter`, `mixed_paging` (a legacy parameter with a new one) "
			+ "or `filter_requires_player` (`faction` or `result` without `player`). A legacy parameter answers with `Deprecation: true`.";

		private static readonly string[] s_legacyList = ["after_id", "since"];
		private static readonly string[] s_legacyOfPlayer = ["before_id"];

		// The legacy after_id and since keep their rows: matches with an id above after_id that finished at or after since, lowest id
		// first, and a Link rel=next with after_id=<last id> when the page is full
		[Authorize(Policy = PermissionPolicies.Prefix + Permissions.MatchHistoryRead)]
		[EndpointSummary("Lists finished matches, newest finished first.")]
		[EndpointDescription(FeedDescription)]
		[ProducesResponseType<MatchListResponse>(StatusCodes.Status200OK)]
		[ApiFeedParameters]
		[ApiQueryParameter("after_id", "integer", "Legacy: matches with a higher id, lowest id first. Use `sort=id&order=asc` instead.", Deprecated = true)]
		[ApiQueryParameter("since", "string", "Legacy: matches finished at or after this time, lowest id first. Use `finished_after` with `order=asc` instead.", Format = "date-time", Deprecated = true)]
		[HttpGet("~/api/v1/matches")]
		public async Task<IActionResult> List()
		{
			(bool legacy, Failure? classified) = MatchFeedArgs.Classify(Request.Query, s_legacyList);
			if (classified != null)
			{
				return RestResults.Fail(classified.Value);
			}
			if (!legacy)
			{
				return await Feed(null);
			}

			(MatchListArgs args, Failure? failure) = MatchArgs.Read(Request.Query);
			if (failure != null)
			{
				return RestResults.Fail(failure.Value);
			}

			await using AppDbContext db = await m_dbFactory.CreateDbContextAsync(HttpContext.RequestAborted);
			List<MatchHistory_Entry> entries = await Database.MatchHistory.GetMatchesPage(db, args.AfterID, args.Since, args.PerPage, HttpContext.RequestAborted);

			if (entries.Count == args.PerPage)
			{
				Paging.WriteKeysetNext(HttpContext, "after_id", entries[^1].match_id);
			}
			Response.Headers["Deprecation"] = "true";
			IReadOnlyDictionary<long, int> tags = await TagsOf(db, entries.SelectMany(MatchResponse.UserIdsOf));
			return RestResults.Ok(new MatchListResponse { Matches = entries.Select(entry => MatchResponse.Of(entry, tags)).ToArray() });
		}

		[Authorize(Policy = PermissionPolicies.Prefix + Permissions.MatchHistoryRead)]
		[EndpointSummary("Lists the finished matches of one player, newest finished first.")]
		[EndpointDescription(FeedDescription + "\n\nThe player of the path is the first player, `player` adds up to 3 more.")]
		[ProducesResponseType<MatchListResponse>(StatusCodes.Status200OK)]
		[ApiFeedParameters]
		[ApiQueryParameter("before_id", "integer", "Legacy: matches with a lower id, highest id first. Use `sort=id` instead.", Deprecated = true)]
		[HttpGet("~/api/v1/players/{userID:long}/matches")]
		public async Task<IActionResult> OfPlayer(long userID)
		{
			(bool legacy, Failure? classified) = MatchFeedArgs.Classify(Request.Query, s_legacyOfPlayer);
			if (classified != null)
			{
				return RestResults.Fail(classified.Value);
			}
			if (!legacy)
			{
				return await Feed(userID);
			}

			long? beforeID = null;
			if (Request.Query.TryGetValue("before_id", out var beforeText))
			{
				if (!long.TryParse(beforeText, NumberStyles.None, CultureInfo.InvariantCulture, out long before))
				{
					return RestResults.Invalid("before_id");
				}
				beforeID = before;
			}
			int perPage = OffsetPage.DefaultPerPage;
			if (Request.Query.TryGetValue("per_page", out var perPageText) && (!int.TryParse(perPageText, NumberStyles.None, CultureInfo.InvariantCulture, out perPage) || perPage < 1 || perPage > OffsetPage.MaxPerPage))
			{
				return RestResults.Invalid("per_page");
			}

			await using AppDbContext db = await m_dbFactory.CreateDbContextAsync(HttpContext.RequestAborted);
			if (!await Database.Users.Exists(db, userID))
			{
				return RestResults.NotFound("user");
			}

			List<MatchHistory_Entry> entries = await Database.MatchHistory.GetMatchesOfPlayer(db, userID, beforeID, perPage, HttpContext.RequestAborted);
			if (entries.Count == perPage)
			{
				Paging.WriteKeysetNext(HttpContext, "before_id", entries[^1].match_id);
			}
			Response.Headers["Deprecation"] = "true";
			IReadOnlyDictionary<long, int> tags = await TagsOf(db, entries.SelectMany(MatchResponse.UserIdsOf));
			return RestResults.Ok(new MatchListResponse { Matches = entries.Select(entry => MatchResponse.Of(entry, tags)).ToArray() });
		}

		private async Task<IActionResult> Feed(long? player)
		{
			(MatchFeedRequest? request, Failure? failure) = MatchFeedArgs.Read(Request.Query, player);
			if (failure != null)
			{
				return RestResults.Fail(failure.Value);
			}

			await using AppDbContext db = await m_dbFactory.CreateDbContextAsync(HttpContext.RequestAborted);
			if (player is long userID && !await Database.Users.Exists(db, userID))
			{
				return RestResults.NotFound("user");
			}

			MatchFeedQuery query = request!.Query;
			MatchFeedPage page = await Database.MatchHistory.GetMatchFeed(db, query, HttpContext.RequestAborted);
			string? next = page.Next is MatchFeedKey after ? MatchFeedArgs.Encode(query, after, false) : null;
			string? prev = request.HadCursor && page.Prev is MatchFeedKey before ? MatchFeedArgs.Encode(query, before, true) : null;
			Paging.WriteCursorLinks(HttpContext, next, prev, query.PerPage);

			IReadOnlyDictionary<long, int> tags = await TagsOf(db, page.Matches.SelectMany(MatchResponse.UserIdsOf));
			return RestResults.Ok(new MatchListResponse
			{
				Matches = page.Matches.Select(entry => MatchResponse.Of(entry, tags)).ToArray(),
				NextCursor = next,
				PrevCursor = prev,
				Partial = page.Partial
			});
		}

		// One match, finished or running: match_history.read, or any staff permission through the sign-in cookie, so the
		// staff pages open a match as the first staff area did (A1); a bearer caller still needs match_history.read
		[AllowSignInCookie]
		[EndpointSummary("Reads one match.")]
		[EndpointDescription("With the sign-in cookie any staff member (moderation.kick, moderation.mute or moderation.ban) may read it.")]
		[ProducesResponseType<MatchResponse>(StatusCodes.Status200OK)]
		[ApiPermission(Permissions.MatchHistoryRead)]
		[HttpGet("~/api/v1/matches/{matchID:long}")]
		public async Task<IActionResult> Get(long matchID)
		{
			if (!(await m_authorization.AuthorizeAsync(User, PermissionPolicies.For(Permissions.MatchHistoryRead))).Succeeded
				&& !(ApiAuthentication.IsSignInCookie(User) && await StaffAccess.IsStaffAsync(m_authorization, User)))
			{
				return RestResults.Fail(Result.Forbidden(Permissions.MatchHistoryRead));
			}

			await using AppDbContext db = await m_dbFactory.CreateDbContextAsync(HttpContext.RequestAborted);
			MatchHistory_Entry? entry = await Database.MatchHistory.LoadMatchHistoryEntryAsync(db, matchID, HttpContext.RequestAborted);
			return entry == null ? RestResults.NotFound("match") : RestResults.Ok(MatchResponse.Of(entry, await TagsOf(db, MatchResponse.UserIdsOf(entry))));
		}

		private static async Task<IReadOnlyDictionary<long, int>> TagsOf(AppDbContext db, IEnumerable<long> userIDs)
		{
			Dictionary<long, (string Name, int Tag)> names = await Database.DisplayNames.GetBulk(db, userIDs.ToHashSet());
			return names.ToDictionary(pair => pair.Key, pair => pair.Value.Tag);
		}
	}
}
