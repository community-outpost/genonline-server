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
using GenOnlineService.Identity;
using GenOnlineService.OpenApi;
using GenOnlineService.Realtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace GenOnlineService.Rest
{
	public sealed record AppealResponse
	{
		public long Id { get; init; }
		public long SanctionId { get; init; }

		public string Status { get; init; } = "";
		public string Text { get; init; } = "";
		public DateTime CreatedAt { get; init; }
		public DateTime? DecidedAt { get; init; }

		public string? DecisionReason { get; init; }

		public static AppealResponse Of(Appeal a) => new AppealResponse
		{
			Id = a.ID, SanctionId = a.SanctionID, Status = a.Status, Text = a.Text, CreatedAt = a.CreatedAt, DecidedAt = a.DecidedAt, DecisionReason = a.DecisionReason
		};
	}

	// Public reason and dates only, never the notes, evidence or staff member
	public sealed record MySanctionResponse
	{
		public long Id { get; init; }

		public string Type { get; init; } = "";
		public string? Reason { get; init; }
		public DateTime CreatedAt { get; init; }
		public DateTime? ExpiresAt { get; init; }
		public DateTime? LiftedAt { get; init; }
		public bool Active { get; init; }

		// Open to a new appeal: no appeal yet, and the sanction is active or ended within the last 30 days
		public bool CanAppeal { get; init; }
		public AppealResponse? Appeal { get; init; }
	}

	public sealed record MySanctionsResponse
	{
		public IReadOnlyList<MySanctionResponse> Sanctions { get; init; } = [];
	}

	public sealed record AppealRequest
	{
		public string? Text { get; init; }
	}

	public sealed record StaffAppealResponse
	{
		public long Id { get; init; }
		public long SanctionId { get; init; }
		public Member Player { get; init; } = new();
		public string Status { get; init; } = "";
		public string Text { get; init; } = "";
		public DateTime CreatedAt { get; init; }
		public DateTime? DecidedAt { get; init; }
		public string? DecisionReason { get; init; }
		public Member? DecidedBy { get; init; }
		public string SanctionType { get; init; } = "";
		public string? SanctionReason { get; init; }
		public DateTime SanctionCreatedAt { get; init; }
		public DateTime? SanctionExpiresAt { get; init; }
		public DateTime? SanctionLiftedAt { get; init; }
	}

	public sealed record StaffAppealsResponse
	{
		public IReadOnlyList<StaffAppealResponse> Appeals { get; init; } = [];
	}

	public sealed record DecideAppealRequest
	{
		public string? Decision { get; init; }
		public string? Reason { get; init; }
		public DateTime? ExpiresAt { get; init; }
	}

	// A banned player may use these routes; the realtime hub and the rest stay closed to them
	[ApiController]
	[Authorize(AuthenticationSchemes = SessionTokenHandler.SchemeName)]
	public sealed class MeAppealsController : ControllerBase
	{
		private readonly IDbContextFactory<AppDbContext> m_dbFactory;

		public MeAppealsController(IDbContextFactory<AppDbContext> dbFactory)
		{
			m_dbFactory = dbFactory;
		}

		private Int64 Me => RealtimeClaims.UserID(User);

		[AllowBannedSession]
		[EndpointSummary("Lists the mutes and bans of the caller, newest first.")]
		[ProducesResponseType<MySanctionsResponse>(StatusCodes.Status200OK)]
		[HttpGet("~/api/v1/me/sanctions")]
		public async Task<IActionResult> List()
		{
			DateTime now = DateTime.UtcNow;
			await using AppDbContext db = await m_dbFactory.CreateDbContextAsync(HttpContext.RequestAborted);
			List<Sanction> rows = (await Database.Moderation.GetSanctions(db, Me, activeOnly: false)).Where(s => s.Type != SanctionTypes.Kick).ToList();
			Dictionary<long, Appeal> appeals = await Database.Appeals.ForSanctions(db, rows.Select(s => s.ID).ToArray(), HttpContext.RequestAborted);

			return RestResults.Ok(new MySanctionsResponse
			{
				Sanctions = rows.Select(s =>
				{
					appeals.TryGetValue(s.ID, out Appeal? appeal);
					return new MySanctionResponse
					{
						Id = s.ID,
						Type = s.Type,
						Reason = String.IsNullOrEmpty(s.Reason) ? null : s.Reason,
						CreatedAt = s.CreatedAt,
						ExpiresAt = s.ExpiresAt,
						LiftedAt = s.LiftedAt,
						Active = s.LiftedAt == null && (s.ExpiresAt == null || s.ExpiresAt > now),
						CanAppeal = appeal == null && Database.Appeals.CanAppeal(s, now),
						Appeal = appeal == null ? null : AppealResponse.Of(appeal)
					};
				}).ToArray()
			});
		}

		[AllowBannedSession]
		[EndpointSummary("Appeals one of the sanctions of the caller.")]
		[EndpointDescription("While the sanction is active and for 30 days after it ended; one appeal per sanction, and a decision is final.")]
		[ProducesResponseType<AppealResponse>(StatusCodes.Status201Created)]
		[ApiRequestBody(typeof(AppealRequest))]
		[HttpPost("~/api/v1/me/sanctions/{sanctionID:long}/appeal")]
		public async Task<IActionResult> Create(long sanctionID)
		{
			AppealRequest? body;
			try
			{
				using StreamReader reader = new StreamReader(Request.Body);
				string json = await reader.ReadToEndAsync();
				body = String.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<AppealRequest>(json, ApiResults.Json);
			}
			catch (JsonException)
			{
				return RestResults.Invalid("body");
			}

			string text = body?.Text?.Trim() ?? "";
			if (text.Length == 0 || text.Length > Database.Appeals.MaxTextLength)
			{
				return RestResults.Invalid("text");
			}

			await using AppDbContext db = await m_dbFactory.CreateDbContextAsync(HttpContext.RequestAborted);
			(AppealOutcome outcome, Appeal? appeal) = await Database.Appeals.Open(db, Me, sanctionID, text, DateTime.UtcNow, HttpContext.RequestAborted);
			return outcome switch
			{
				AppealOutcome.Done => RestResults.Created(AppealResponse.Of(appeal!)),
				AppealOutcome.AlreadyAppealed => RestResults.Fail(RestCodes.AppealExists),
				AppealOutcome.NotAppealable => RestResults.Fail(RestCodes.AppealNotAllowed),
				_ => RestResults.NotFound("sanction")
			};
		}
	}

	// Reading needs appeals.review; a decision that lifts or shortens needs the permission of the sanction's type too
	public sealed class StaffAppealsController : ApiControllerBase
	{
		private readonly IDbContextFactory<AppDbContext> m_dbFactory;

		public StaffAppealsController(IDbContextFactory<AppDbContext> dbFactory, IAuthorizationService authorization) : base(authorization)
		{
			m_dbFactory = dbFactory;
		}

		private static readonly Dictionary<string, string> s_decisions = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["uphold"] = AppealStatuses.Upheld,
			["lift"] = AppealStatuses.Lifted,
			["shorten"] = AppealStatuses.Shortened
		};

		[AllowSignInCookie]
		[EndpointSummary("Lists appeals, oldest first.")]
		[EndpointDescription("Filter with status (open, upheld, lifted or shortened); paged like the other lists.")]
		[ProducesResponseType<StaffAppealsResponse>(StatusCodes.Status200OK)]
		[ApiPermission(Permissions.AppealsReview)]
		[HttpGet("~/api/v1/appeals")]
		public async Task<IActionResult> List([FromQuery] string? status)
		{
			if (await RequirePermissionAsync(Permissions.AppealsReview) is Failure denied)
			{
				return ApiResults.Fail(denied);
			}
			if (status != null && status != AppealStatuses.Open && status != AppealStatuses.Upheld && status != AppealStatuses.Lifted && status != AppealStatuses.Shortened)
			{
				return RestResults.Invalid("status");
			}
			(OffsetPage page, Failure? failure) = Paging.ReadOffset(Request);
			if (failure != null)
			{
				return RestResults.Fail(failure.Value);
			}

			await using AppDbContext db = await m_dbFactory.CreateDbContextAsync(HttpContext.RequestAborted);
			List<Appeal> rows = await Database.Appeals.Queue(db, status, (page.Page - 1) * page.PerPage, page.PerPage + 1, HttpContext.RequestAborted);
			bool hasNext = rows.Count > page.PerPage;
			Paging.WriteOffsetHeaders(HttpContext, page, hasNext);
			return RestResults.Ok(new StaffAppealsResponse { Appeals = await Describe(db, rows.Take(page.PerPage).ToList()) });
		}

		[AllowSignInCookie]
		[EndpointSummary("Reads one appeal.")]
		[ProducesResponseType<StaffAppealResponse>(StatusCodes.Status200OK)]
		[ApiPermission(Permissions.AppealsReview)]
		[HttpGet("~/api/v1/appeals/{appealID:long}")]
		public async Task<IActionResult> Get(long appealID)
		{
			if (await RequirePermissionAsync(Permissions.AppealsReview) is Failure denied)
			{
				return ApiResults.Fail(denied);
			}

			await using AppDbContext db = await m_dbFactory.CreateDbContextAsync(HttpContext.RequestAborted);
			Appeal? appeal = await Database.Appeals.Get(db, appealID, HttpContext.RequestAborted);
			if (appeal == null)
			{
				return RestResults.NotFound("appeal");
			}
			return RestResults.Ok((await Describe(db, new List<Appeal> { appeal }))[0]);
		}

		[AllowSignInCookie]
		[EndpointSummary("Decides an open appeal.")]
		[EndpointDescription("uphold keeps the sanction, lift ends it, shorten ends it at expires_at. reason is the answer the player reads.")]
		[ProducesResponseType<StaffAppealResponse>(StatusCodes.Status200OK)]
		[ApiRequestBody(typeof(DecideAppealRequest))]
		[ApiPermission(Permissions.AppealsReview)]
		[HttpPost("~/api/v1/appeals/{appealID:long}/decision")]
		public async Task<IActionResult> Decide(long appealID)
		{
			if (await RequirePermissionAsync(Permissions.AppealsReview) is Failure denied)
			{
				return ApiResults.Fail(denied);
			}

			(DecideAppealRequest? body, Failure? malformed) = await ReadBodyAsync<DecideAppealRequest>();
			if (malformed != null)
			{
				return ApiResults.Fail(malformed.Value);
			}
			if (body?.Decision == null || !s_decisions.TryGetValue(body.Decision, out string? status))
			{
				return RestResults.Invalid("decision");
			}
			string reason = body.Reason?.Trim() ?? "";
			if (reason.Length == 0 || reason.Length > ModerationManager.MaximumReasonLength)
			{
				return RestResults.Invalid("reason");
			}

			await using AppDbContext db = await m_dbFactory.CreateDbContextAsync(HttpContext.RequestAborted);
			Appeal? appeal = await Database.Appeals.Get(db, appealID, HttpContext.RequestAborted);
			Sanction? sanction = appeal == null ? null : await Database.Moderation.GetSanction(db, appeal.SanctionID);
			if (appeal == null || sanction == null)
			{
				return RestResults.NotFound("appeal");
			}

			// staff never decide their own appeal, and lifting or shortening needs the permission that sets the sanction
			if (appeal.UserID == Me)
			{
				return RestResults.Invalid("appeal");
			}
			if (status != AppealStatuses.Upheld
				&& await RequirePermissionAsync(sanction.Type == SanctionTypes.Mute ? Permissions.ModerationMute : Permissions.ModerationBan) is Failure missing)
			{
				return ApiResults.Fail(missing);
			}

			AppealOutcome outcome = await Database.Appeals.Decide(db, appealID, status, reason, status == AppealStatuses.Shortened ? body.ExpiresAt?.ToUniversalTime() : null,
				SanctionActions.ApiActorOf(Me), DateTime.UtcNow, HttpContext.RequestAborted);
			switch (outcome)
			{
				case AppealOutcome.Done:
					break;
				case AppealOutcome.AlreadyDecided:
					return RestResults.Fail(RestCodes.AppealDecided);
				case AppealOutcome.InvalidExpiry:
					return RestResults.Invalid("expires_at");
				default:
					return RestResults.NotFound("appeal");
			}

			if (sanction.Type == SanctionTypes.Ban && status != AppealStatuses.Upheld)
			{
				// lets the player connect again without waiting for the reload tick
				await AccessControl.Reload(db);
			}

			Appeal decided = (await Database.Appeals.Get(db, appealID, HttpContext.RequestAborted))!;
			return RestResults.Ok((await Describe(db, new List<Appeal> { decided }))[0]);
		}

		private static async Task<StaffAppealResponse[]> Describe(AppDbContext db, List<Appeal> appeals)
		{
			Int64[] sanctionIDs = appeals.Select(a => a.SanctionID).ToArray();
			Dictionary<long, Sanction> sanctions = await db.Sanctions.AsNoTracking().Where(s => sanctionIDs.Contains(s.ID)).ToDictionaryAsync(s => s.ID);
			IEnumerable<Int64> people = appeals.Select(a => a.UserID).Concat(appeals.Where(a => a.DecidedByID != null).Select(a => a.DecidedByID!.Value));
			Dictionary<long, (string Name, int Tag)> names = await RealtimeHub.NamesAsync(db, people);

			return appeals.Select(a =>
			{
				sanctions.TryGetValue(a.SanctionID, out Sanction? s);
				return new StaffAppealResponse
				{
					Id = a.ID,
					SanctionId = a.SanctionID,
					Player = RealtimeMembers.Of(a.UserID, names),
					Status = a.Status,
					Text = a.Text,
					CreatedAt = a.CreatedAt,
					DecidedAt = a.DecidedAt,
					DecisionReason = a.DecisionReason,
					DecidedBy = a.DecidedByID is long decider ? RealtimeMembers.Of(decider, names) : null,
					SanctionType = s?.Type ?? "",
					SanctionReason = String.IsNullOrEmpty(s?.Reason) ? null : s!.Reason,
					SanctionCreatedAt = s?.CreatedAt ?? DateTime.UnixEpoch,
					SanctionExpiresAt = s?.ExpiresAt,
					SanctionLiftedAt = s?.LiftedAt
				};
			}).ToArray();
		}
	}
}
