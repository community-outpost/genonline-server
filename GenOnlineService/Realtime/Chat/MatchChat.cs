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
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace GenOnlineService.Realtime
{
	// POST /api/v1/matches/{id}/chat keeps a copy of the in-game chat, which the players already saw, for moderation and reports
	public static class MatchChat
	{
		// A match accepts lines until this long after it started
		public static readonly TimeSpan Window = TimeSpan.FromHours(24);

		// Pure checks of the body, the text is trimmed, the recipients deduplicated in order
		internal static Failure? Validate(SendMatchChatRequest request, out string text, out string scope, out long[] recipients)
		{
			text = (request.Text ?? String.Empty).Trim();
			scope = request.Scope ?? String.Empty;
			recipients = (request.RecipientUserIds ?? []).Distinct().ToArray();

			if (text.Length == 0)
			{
				return Result.Invalid("text");
			}
			if (text.Length > RealtimeHub.ChatMaxLength)
			{
				return Result.Fail(ErrorCodes.ChatTooLong, new { Max = RealtimeHub.ChatMaxLength });
			}
			if (!ChatMessageScopes.All.Contains(scope))
			{
				return Result.Invalid("scope");
			}
			return null;
		}

		// The roster comes from the match rows written when the match starts, so a running match works
		internal static Failure? CheckRoster(DateTime? startedAt, DateTime now, IReadOnlyCollection<long> roster, Int64 me, IReadOnlyList<long> recipients)
		{
			if (startedAt == null || !roster.Contains(me) || now - startedAt.Value > Window)
			{
				return Result.NotFound("match");
			}
			if (recipients.Any(id => id == me || !roster.Contains(id)))
			{
				return Result.Invalid("recipient_user_ids");
			}
			return null;
		}

		// The rules and terms that disclose match chat must be accepted first, so nothing is kept from a player who has not accepted them yet
		public static Failure? CheckAgreements(IReadOnlyList<AgreementDocument> owed) =>
			owed.Count == 0 ? null : Result.Fail(ErrorCodes.AgreementsOwed, new { Documents = owed.Select(d => d.Name).ToArray() });

		public static async Task<Result<SendMatchChatResponse>> SendAsync(IDbContextFactory<AppDbContext> dbFactory, LobbyManager lobbyManager, AgreementsSettings agreements, Int64 me, Int64 matchID, SendMatchChatRequest request)
		{
			if (Validate(request, out string text, out string scope, out long[] recipients) is Failure invalid)
			{
				return invalid;
			}

			ChatMessage stored;
			await using (AppDbContext db = await dbFactory.CreateDbContextAsync())
			{
				DateTime? startedAt = matchID <= 0 ? null : await db.Matches.AsNoTracking().Where(m => m.Id == matchID).Select(m => (DateTime?)m.StartedAt).FirstOrDefaultAsync();
				List<long> roster = startedAt == null ? [] : await db.MatchPlayers.AsNoTracking().Where(p => p.MatchId == matchID && p.UserId != null).Select(p => p.UserId!.Value).ToListAsync();
				if (CheckRoster(startedAt == null ? null : DateTime.SpecifyKind(startedAt.Value, DateTimeKind.Utc), DateTime.UtcNow, roster, me, recipients) is Failure refused)
				{
					return refused;
				}

				if (CheckAgreements(await Agreements.OwedAsync(db, agreements, me)) is Failure owes)
				{
					return owes;
				}

				Sanction? mute = await Database.Moderation.GetActiveMute(db, me);
				if (mute != null)
				{
					return RealtimeHub.MutedFailure(mute);
				}

				SharedUserData? sender = WebSocketManager.GetSharedDataForUser(me);
				if (sender == null)
				{
					return Result.NotFound("user");
				}
				if (!ChatRateLimiter.Match.TryConsume(sender, out long retryAfterMs))
				{
					return Result.Fail(ErrorCodes.ChatRateLimited, new { RetryAfterMs = retryAfterMs });
				}

				Int64 lobbyID = lobbyManager.GetLobbies().FirstOrDefault(lobby => lobby.MatchID == (UInt64)matchID)?.LobbyID ?? -1;
				stored = await Database.Chat.InsertMatchMessage(db, matchID, lobbyID > 0 ? lobbyID : null, me, text, scope, recipients);
			}

			return Result.Success(new SendMatchChatResponse { MessageId = stored.ID.ToString(CultureInfo.InvariantCulture) });
		}
	}
}
