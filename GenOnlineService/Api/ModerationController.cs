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

using GenOnlineService.Names;
using GenOnlineService.OpenApi;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace GenOnlineService.Api
{
	using GenOnlineService.Realtime;

	public sealed class ModerationController : ApiControllerBase
	{
		private readonly IDbContextFactory<AppDbContext> m_dbFactory;
		private readonly PlayerNames m_names;
		private readonly LobbyManager? m_lobbyManager;

		public ModerationController(IDbContextFactory<AppDbContext> dbFactory, IAuthorizationService authorization, PlayerNames names, LobbyManager? lobbyManager = null) : base(authorization)
		{
			m_dbFactory = dbFactory;
			m_names = names;
			m_lobbyManager = lobbyManager;
		}

		[AllowSignInCookie]
		[EndpointSummary("Renames a player as staff.")]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
		[ApiRequestBody(typeof(RenamePlayerRequest))]
		[ApiPermission(Permissions.ModerationRename)]
		[HttpPatch("~/api/v1/players/{userID:long}")]
		public async Task<IActionResult> RenamePlayer(long userID)
		{
			if (await RequirePermissionAsync(Permissions.ModerationRename) is Failure denied)
			{
				return ApiResults.Fail(denied);
			}

			(RenamePlayerRequest? body, Failure? malformed) = await ReadBodyAsync<RenamePlayerRequest>();
			if (malformed != null)
			{
				return ApiResults.Fail(malformed.Value);
			}
			return ApiResults.NoContent(await m_names.RenameByStaffAsync(SanctionActions.ApiActorOf(Me), userID, body?.Name, body?.Reason));
		}

		// A placeholder replaces the name; ending every sign-in forces the player to choose a new one
		[AllowSignInCookie]
		[EndpointSummary("Resets a player name to a generated one as staff.")]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
		[ApiRequestBody(typeof(ResetNameRequest))]
		[ApiPermission(Permissions.ModerationRename)]
		[HttpPost("~/api/v1/players/{userID:long}/reset_name")]
		public async Task<IActionResult> ResetPlayerName(long userID)
		{
			if (await RequirePermissionAsync(Permissions.ModerationRename) is Failure denied)
			{
				return ApiResults.Fail(denied);
			}

			(ResetNameRequest? body, Failure? malformed) = await ReadBodyAsync<ResetNameRequest>();
			if (malformed != null)
			{
				return ApiResults.Fail(malformed.Value);
			}
			return ApiResults.NoContent(await m_names.ResetNameByStaffAsync(SanctionActions.ApiActorOf(Me), userID, body?.Reason));
		}

		[AllowSignInCookie]
		[EndpointSummary("Kicks, mutes or bans a player.")]
		[ProducesResponseType<CreateSanctionResponse>(StatusCodes.Status200OK)]
		[ApiRequestBody(typeof(CreateSanctionRequest))]
		[ApiPermission(Permissions.ModerationKick, Permissions.ModerationMute, Permissions.ModerationBan)]
		[HttpPost("~/api/v1/players/{userID:long}/sanctions")]
		public async Task<IActionResult> CreateSanction(long userID)
		{
			(CreateSanctionRequest? body, Failure? malformed) = await ReadBodyAsync<CreateSanctionRequest>();
			if (malformed != null)
			{
				return ApiResults.Fail(malformed.Value);
			}
			return ApiResults.Answer(await SanctionActions.CreateAsync(m_dbFactory, Me, userID, body ?? new CreateSanctionRequest(), RequirePermissionAsync, m_lobbyManager));
		}

		// moderation.kick or higher reads the list
		[EndpointSummary("Lists the sanctions of a player.")]
		[ProducesResponseType<SanctionsResponse>(StatusCodes.Status200OK)]
		[ApiPermission(Permissions.ModerationKick, Permissions.ModerationMute, Permissions.ModerationBan)]
		[HttpGet("~/api/v1/players/{userID:long}/sanctions")]
		public async Task<IActionResult> GetSanctions(long userID, [FromQuery] string? active)
		{
			if (await RequireStaffAsync() is Failure denied)
			{
				return ApiResults.Fail(denied);
			}

			bool activeOnly = false;
			if (active != null && !Boolean.TryParse(active, out activeOnly))
			{
				return ApiResults.Fail(Result.Invalid("active"));
			}
			return ApiResults.Answer(await SanctionActions.ListAsync(m_dbFactory, userID, activeOnly));
		}

		[AllowSignInCookie]
		[EndpointSummary("Lifts a mute or a ban.")]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
		[ApiRequestBody(typeof(ReasonRequest))]
		[ApiPermission(Permissions.ModerationMute, Permissions.ModerationBan)]
		[HttpDelete("~/api/v1/sanctions/{sanctionID:long}")]
		public async Task<IActionResult> LiftSanction(long sanctionID)
		{
			// a caller who can lift neither kind learns nothing about the sanction
			if (await RequirePermissionAsync(Permissions.ModerationMute) != null && await RequirePermissionAsync(Permissions.ModerationBan) != null)
			{
				return ApiResults.Fail(Result.Forbidden(Permissions.ModerationMute));
			}

			(ReasonRequest? body, Failure? malformed) = await ReadBodyAsync<ReasonRequest>();
			if (malformed != null)
			{
				return ApiResults.Fail(malformed.Value);
			}
			return ApiResults.NoContent(await SanctionActions.LiftAsync(m_dbFactory, Me, sanctionID, body?.Reason, RequirePermissionAsync));
		}

		[AllowSignInCookie]
		[EndpointSummary("Deletes a sanction record with its appeal and evidence, after ending it when it is active.")]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
		[ApiRequestBody(typeof(ReasonRequest))]
		[ApiPermission(Permissions.SanctionsDelete)]
		[HttpDelete("~/api/v1/sanctions/{sanctionID:long}/record")]
		public async Task<IActionResult> DeleteSanction(long sanctionID)
		{
			(ReasonRequest? body, Failure? malformed) = await ReadBodyAsync<ReasonRequest>();
			if (malformed != null)
			{
				return ApiResults.Fail(malformed.Value);
			}
			return ApiResults.NoContent(await SanctionActions.DeleteAsync(m_dbFactory, SanctionActions.ApiActorOf(Me), sanctionID, body?.Reason, S3CredentialManager.Store, RequirePermissionAsync));
		}

		// Kept until 90 days after the sanction ends; needs the permission of the sanction's type
		[AllowSignInCookie]
		[EndpointSummary("Keeps a match's screenshots of the sanctioned player as evidence for a sanction.")]
		[ProducesResponseType<HoldEvidenceResponse>(StatusCodes.Status200OK)]
		[ApiRequestBody(typeof(HoldEvidenceRequest))]
		[ApiPermission(Permissions.ModerationKick, Permissions.ModerationMute, Permissions.ModerationBan)]
		[HttpPost("~/api/v1/sanctions/{sanctionID:long}/evidence")]
		public async Task<IActionResult> HoldEvidence(long sanctionID)
		{
			(HoldEvidenceRequest? body, Failure? malformed) = await ReadBodyAsync<HoldEvidenceRequest>();
			if (malformed != null)
			{
				return ApiResults.Fail(malformed.Value);
			}
			if (body?.MatchId is not long matchID || matchID <= 0)
			{
				return ApiResults.Fail(Result.Invalid("match_id"));
			}

			await using AppDbContext db = await m_dbFactory.CreateDbContextAsync();
			string? type = await db.Sanctions.AsNoTracking().Where(s => s.ID == sanctionID).Select(s => s.Type).FirstOrDefaultAsync();
			string permission = type switch
			{
				SanctionTypes.Ban => Permissions.ModerationBan,
				SanctionTypes.Mute => Permissions.ModerationMute,
				_ => Permissions.ModerationKick,
			};
			// staff who may kick get the same not_found for a sanction they may not give, so a caller without the permission learns nothing
			if (await RequirePermissionAsync(permission) is Failure denied)
			{
				return await RequirePermissionAsync(Permissions.ModerationKick) == null
					? ApiResults.Fail(Result.NotFound("sanction"))
					: ApiResults.Fail(denied);
			}

			EvidenceResult result = await Database.SanctionEvidenceStore.HoldMatchScreenshots(db, S3CredentialManager.Store, sanctionID, matchID, SanctionActions.ApiActorOf(Me), DateTime.UtcNow);
			return result.Outcome switch
			{
				EvidenceOutcome.Held => ApiResults.Answer(Result.Success(new HoldEvidenceResponse { Held = result.Held, Missing = result.Missing })),
				EvidenceOutcome.SanctionNotFound => ApiResults.Fail(Result.NotFound("sanction")),
				EvidenceOutcome.MatchNotFound => ApiResults.Fail(Result.NotFound("match")),
				EvidenceOutcome.NotInMatch => ApiResults.Fail(Result.Invalid("match_id")),
				EvidenceOutcome.HoldOver => ApiResults.Fail(Result.Invalid("sanction_id")),
				_ => ApiResults.Fail(Result.Fail(GenOnlineService.Rest.RestCodes.ServiceUnavailable, new { What = "match_data" })),
			};
		}

		[AllowSignInCookie]
		[EndpointSummary("Deletes a chat message for everyone.")]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
		[ApiRequestBody(typeof(ReasonRequest))]
		[ApiPermission(Permissions.ChatDeleteAny)]
		[HttpDelete("~/api/v1/messages/{messageID}")]
		public async Task<IActionResult> DeleteMessage(string messageID)
		{
			(ReasonRequest? body, Failure? malformed) = await ReadBodyAsync<ReasonRequest>();
			if (malformed != null)
			{
				return ApiResults.Fail(malformed.Value);
			}
			return ApiResults.NoContent(await MessageActions.DeleteAsync(m_dbFactory, Me, messageID, body?.Reason, RequirePermissionAsync));
		}

		private async Task<Failure?> RequireStaffAsync()
		{
			if (await RequirePermissionAsync(Permissions.ModerationKick) != null
				&& await RequirePermissionAsync(Permissions.ModerationMute) != null
				&& await RequirePermissionAsync(Permissions.ModerationBan) != null)
			{
				return Result.Forbidden(Permissions.ModerationKick);
			}
			return null;
		}

		[EndpointSummary("Reports a player or a chat message.")]
		[ProducesResponseType<CreateReportResponse>(StatusCodes.Status200OK)]
		[ApiRequestBody(typeof(CreateReportRequest))]
		[HttpPost("~/api/v1/reports")]
		public async Task<IActionResult> CreateReport()
		{
			(CreateReportRequest? body, Failure? malformed) = await ReadBodyAsync<CreateReportRequest>();
			if (malformed != null)
			{
				return ApiResults.Fail(malformed.Value);
			}
			return ApiResults.Answer(await MessageActions.ReportAsync(m_dbFactory, Me, body ?? new CreateReportRequest()));
		}
	}
}
