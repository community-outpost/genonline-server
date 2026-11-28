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
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;

namespace GenOnlineService.Realtime
{
	// ReportAnticheatEvent passes on the Easy Anti-Cheat callbacks of a game client
	public static class EacReports
	{
		public const string ActionRequired = "action_required";
		public const string IntegrityViolation = "integrity_violation";

		// EOS_EAntiCheatCommonClientAction, EOS_EAntiCheatCommonClientActionReason and EOS_EAntiCheatClientViolationType of EOS SDK 1.18
		public const int MaxAction = 1;
		public const int MaxReasonCode = 10;
		public const int MaxViolationType = 15;

		internal static readonly ChatRateLimiter Limiter = new(30, 60_000);

		public static readonly TimeSpan MatchWindow = TimeSpan.FromHours(6);

		internal static Failure? Validate(ReportAnticheatEventRequest request, Int64 me, DateTime now, out EacEventReport? report)
		{
			report = null;
			bool action = request.Kind == ActionRequired;
			if (!action && request.Kind != IntegrityViolation)
			{
				return Result.Invalid("kind");
			}

			Int64 target = action ? request.TargetUserId ?? 0 : request.TargetUserId ?? me;
			if (target <= 0 || (!action && target != me))
			{
				return Result.Invalid("target_user_id");
			}
			if (request.LobbyId is <= 0)
			{
				return Result.Invalid("lobby_id");
			}

			Int64 match = 0;
			if (!String.IsNullOrEmpty(request.MatchId) && (!Int64.TryParse(request.MatchId, NumberStyles.None, CultureInfo.InvariantCulture, out match) || match <= 0))
			{
				return Result.Invalid("match_id");
			}
			if (target != me && match == 0 && request.LobbyId == null)
			{
				return Result.Invalid("lobby_id");
			}

			int code = request.Action ?? 0;
			if (code < 0 || code > (action ? MaxAction : 0))
			{
				return Result.Invalid("action");
			}
			int reason = request.ReasonCode ?? 0;
			if (reason < 0 || reason > (action ? MaxReasonCode : 0))
			{
				return Result.Invalid("reason_code");
			}
			int violation = request.ViolationType ?? 0;
			if (violation < 0 || violation > (action ? 0 : MaxViolationType))
			{
				return Result.Invalid("violation_type");
			}

			// witnesses in one match can sit in different lobbies, so a match report keeps no lobby
			report = new EacEventReport(me, target, match, match > 0 ? 0 : request.LobbyId ?? 0, request.Kind!, code, reason, violation, Details(request.Details), now);
			return null;
		}

		// a running match can outlast the window, so the caller's current match is always accepted
		internal static async Task<Failure?> CheckScope(AppDbContext db, EacEventReport report, Int64 currentLobbyId, bool targetInLobby, Int64 currentMatchId, DateTime now)
		{
			if (report.MatchId > 0)
			{
				DateTime? startedAt = await db.Matches.AsNoTracking().Where(m => m.Id == report.MatchId).Select(m => (DateTime?)m.StartedAt).FirstOrDefaultAsync();
				if (report.MatchId != currentMatchId && (startedAt == null || now - DateTime.SpecifyKind(startedAt.Value, DateTimeKind.Utc) > MatchWindow))
				{
					return Result.NotFound("match");
				}
				List<long> roster = await db.MatchPlayers.AsNoTracking().Where(p => p.MatchId == report.MatchId && p.UserId != null).Select(p => p.UserId!.Value).ToListAsync();
				if (!roster.Contains(report.ReporterId) || !roster.Contains(report.TargetUserId))
				{
					return Result.NotFound("match");
				}
			}
			else if (report.LobbyId > 0 && (currentLobbyId != report.LobbyId || !targetInLobby))
			{
				return Result.NotFound("lobby");
			}
			return null;
		}

		// trimmed and cut to the column size in UTF-8 bytes, never inside a character
		internal static string Details(string? text)
		{
			string details = TextSanitizer.Clean(text, AnticheatEacWitnessConfiguration.DetailsMaxBytes).Trim();
			while (Encoding.UTF8.GetByteCount(details) > AnticheatEacWitnessConfiguration.DetailsMaxBytes)
			{
				details = details[..(Char.IsLowSurrogate(details[^1]) ? details.Length - 2 : details.Length - 1)];
			}
			return details;
		}
	}
}
