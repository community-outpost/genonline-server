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
	public sealed record StaffOverviewQueue
	{
		public string Kind { get; init; } = "";
		public int Waiting { get; init; }
	}

	public sealed record StaffOverviewSanction
	{
		public long Id { get; init; }
		public string Type { get; init; } = "";
		public Member Player { get; init; } = new Member();
		public string? Reason { get; init; }
		public DateTime CreatedAt { get; init; }
		public DateTime? ExpiresAt { get; init; }
		public DateTime? LiftedAt { get; init; }
		public Member? CreatedBy { get; init; }
		public bool Active { get; init; }
	}

	public sealed record StaffOverviewResponse
	{
		public int PlayersOnline { get; init; }
		public int LobbiesOpen { get; init; }
		public int MatchesToday { get; init; }

		public int PlayersToReview { get; init; }
		public IReadOnlyList<StaffOverviewQueue> Waiting { get; init; } = [];

		public IReadOnlyList<CaseQueueRow> NextCases { get; init; } = [];
		public IReadOnlyList<StaffOverviewSanction> RecentSanctions { get; init; } = [];
	}

	public static class StaffOverview
	{
		public const int RecentCount = 5;

		// playersOnline and lobbiesOpen are this process's live counts, the rest comes from the database.
		// Matches today are the ones finished since 00:00 UTC, the day of the faction numbers of /api/v1/stats/global,
		// counted on the finished_at index rather than a scan of every match ever started.
		public static async Task<StaffOverviewResponse> GetAsync(IDbContextFactory<AppDbContext> dbFactory, int playersOnline, int lobbiesOpen, IReadOnlyCollection<string> kinds, long me, DateTime utcNow, CancellationToken cancellationToken = default)
		{
			await using AppDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);
			DateTime dayStart = DateTime.SpecifyKind(utcNow.Date, DateTimeKind.Utc);
			int matchesToday = await db.Matches.AsNoTracking().CountAsync(m => m.FinishedAt != null && m.FinishedAt >= dayStart, cancellationToken);

			Dictionary<long, Database.CaseQueueEntry> queue = await Database.Cases.Queue(db, kinds, cancellationToken);
			List<StaffOverviewQueue> waiting = kinds.Select(kind => new StaffOverviewQueue { Kind = kind, Waiting = queue.Values.Sum(e => e.Waiting.GetValueOrDefault(kind)) }).ToList();
			(_, CasesResponse next) = await CaseActions.QueueAsync(db, kinds, me, "all", 0, RecentCount, cancellationToken);

			// the primary key orders by creation without an index on created_at
			List<Sanction> sanctions = await db.Sanctions.AsNoTracking().OrderByDescending(s => s.ID).Take(RecentCount).ToListAsync(cancellationToken);

			HashSet<long> userIDs = sanctions.Select(s => s.UserID)
				.Concat(sanctions.Where(s => s.CreatedByID != null).Select(s => s.CreatedByID!.Value))
				.ToHashSet();
			Dictionary<long, (string Name, int Tag)> names = await Database.DisplayNames.GetBulk(db, userIDs);

			return new StaffOverviewResponse
			{
				PlayersOnline = playersOnline,
				LobbiesOpen = lobbiesOpen,
				MatchesToday = matchesToday,
				PlayersToReview = queue.Count,
				Waiting = waiting,
				NextCases = next.Cases,
				RecentSanctions = sanctions.Select(s => new StaffOverviewSanction
				{
					Id = s.ID,
					Type = s.Type,
					Player = RealtimeMembers.Of(s.UserID, names),
					Reason = String.IsNullOrEmpty(s.Reason) ? null : s.Reason,
					CreatedAt = s.CreatedAt,
					ExpiresAt = s.Type == SanctionTypes.Kick ? null : s.ExpiresAt,
					LiftedAt = s.LiftedAt,
					CreatedBy = s.CreatedByID is long creator ? RealtimeMembers.Of(creator, names) : null,
					// a kick ends as it starts, so it is never active
					Active = s.Type != SanctionTypes.Kick && s.LiftedAt == null && (s.ExpiresAt == null || s.ExpiresAt > utcNow)
				}).ToList()
			};
		}
	}

	// Any staff permission; the anti-cheat and rule match numbers need anticheat.review, as in the review queue. A cookie answer is never cached (ApiAuthentication)
	[ApiController]
	[Authorize]
	public sealed class StaffOverviewController : ControllerBase
	{
		private readonly IDbContextFactory<AppDbContext> m_dbFactory;
		private readonly IAuthorizationService m_authorization;

		public StaffOverviewController(IDbContextFactory<AppDbContext> dbFactory, IAuthorizationService authorization)
		{
			m_dbFactory = dbFactory;
			m_authorization = authorization;
		}

		[AllowSignInCookie]
		[EndpointSummary("Reads the counts of the staff overview page.")]
		[ProducesResponseType<StaffOverviewResponse>(StatusCodes.Status200OK)]
		[ApiPermission(Permissions.ModerationKick, Permissions.ModerationMute, Permissions.ModerationBan)]
		[HttpGet("~/api/v1/admin/overview")]
		public async Task<IActionResult> Get()
		{
			if (!await StaffAccess.IsStaffAsync(m_authorization, User))
			{
				return RestResults.Fail(Result.Forbidden(Permissions.ModerationKick));
			}

			List<string> kinds = [CaseSignalKinds.Report];
			if ((await m_authorization.AuthorizeAsync(User, PermissionPolicies.For(Permissions.AnticheatReview))).Succeeded)
			{
				kinds.AddRange(CaseSignalKinds.AntiCheat);
				kinds.Add(CaseSignalKinds.RuleMatch);
			}
			int lobbies = HttpContext.RequestServices.GetService<LobbyManager>()?.GetNumLobbies() ?? 0;
			return RestResults.Ok(await StaffOverview.GetAsync(m_dbFactory, WebSocketManager.GetNumberOfUsersOnline(), lobbies, kinds, RealtimeClaims.UserID(User), DateTime.UtcNow, HttpContext.RequestAborted));
		}
	}
}
