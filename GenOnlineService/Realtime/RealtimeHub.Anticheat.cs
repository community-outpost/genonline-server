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

using Database;

namespace GenOnlineService.Realtime
{
	public sealed partial class RealtimeHub
	{
		public async Task<Result<Empty>> ReportAnticheatEvent(ReportAnticheatEventRequest request)
		{
			if (RequireAccess(ESessionAccessType.Gameplay) is Failure denied)
			{
				return denied;
			}
			SharedUserData? user = WebSocketManager.GetSharedDataForUser(UserID);
			if (user == null)
			{
				return Result.NotFound("user");
			}
			if (!EacReports.Limiter.TryConsume(user, out long retryAfterMs))
			{
				return Result.Fail(ErrorCodes.RateLimited, new { RetryAfterMs = retryAfterMs });
			}
			DateTime now = DateTime.UtcNow;
			if (EacReports.Validate(request, UserID, now, out EacEventReport? report) is Failure invalid)
			{
				return invalid;
			}

			Lobby? lobby = CurrentLobby();
			await using AppDbContext db = await m_dbFactory.CreateDbContextAsync();
			if (await EacReports.CheckScope(db, report!, lobby?.LobbyID ?? -1, lobby?.GetMemberFromUserID(report!.TargetUserId) != null, (Int64)(lobby?.MatchID ?? 0), now) is Failure outside)
			{
				return outside;
			}

			await Database.AntiCheat.RecordEacEvent(db, report!);
			string productUserId = WebSocketManager.GetMiddlewareIDForUser(report!.TargetUserId);
			if (productUserId.Length > 0)
			{
				// the report never waits for Epic
				_ = m_sanctions.CheckAsync(report.TargetUserId, productUserId);
			}
			return Result.Success();
		}
	}
}
