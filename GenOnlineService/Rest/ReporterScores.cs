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

namespace GenOnlineService.Rest
{
	// Staff only: how often the decided reports of one player ended in a sanction, as context for a reviewer
	public sealed record ReporterScoreView
	{
		public int Upheld { get; init; }
		public int Decided { get; init; }

		// new, reliable, mixed or often_dismissed
		public string Band { get; init; } = "";

		public static ReporterScoreView Of(Database.ReporterScore score) => new() { Upheld = score.Upheld, Decided = score.Decided, Band = score.Band };
	}

	public sealed record ReporterScoreResponse
	{
		public ReporterScoreView ReporterScore { get; init; } = new ReporterScoreView();
	}

	[ApiController]
	[AllowSignInCookie]
	public sealed class ReporterScoresController : ControllerBase
	{
		private readonly IDbContextFactory<AppDbContext> m_dbFactory;
		private readonly IAuthorizationService m_authorization;

		public ReporterScoresController(IDbContextFactory<AppDbContext> dbFactory, IAuthorizationService authorization)
		{
			m_dbFactory = dbFactory;
			m_authorization = authorization;
		}

		[EndpointSummary("Reads how often the decided reports of one player ended in a sanction.")]
		[ProducesResponseType<ReporterScoreResponse>(StatusCodes.Status200OK)]
		[ApiPermission(Permissions.ModerationKick, Permissions.ModerationMute, Permissions.ModerationBan)]
		[HttpGet("~/api/v1/admin/players/{userID:long}/reporter_score")]
		public async Task<IActionResult> Read(long userID)
		{
			if (!await StaffAccess.IsStaffAsync(m_authorization, User))
			{
				return RestResults.Fail(Result.Forbidden(Permissions.ModerationKick));
			}
			await using AppDbContext db = await m_dbFactory.CreateDbContextAsync(HttpContext.RequestAborted);
			if (userID <= 0 || !await Database.Users.Exists(db, userID))
			{
				return RestResults.NotFound("user");
			}
			Database.ReporterScore score = (await Database.ReporterScores.For(db, [userID], HttpContext.RequestAborted)).GetValueOrDefault(userID);
			return RestResults.Ok(new ReporterScoreResponse { ReporterScore = ReporterScoreView.Of(score) });
		}
	}
}
