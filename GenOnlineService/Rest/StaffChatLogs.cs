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
using GenOnlineService.Portal.Client;
using GenOnlineService.Realtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace GenOnlineService.Rest
{
	// Kind is null for the lines of a player across every public conversation, and the times are a start and an exclusive end
	public sealed record ChatLogFilter(string? Kind, long? ConversationID, long? UserID, DateTime? From, DateTime? To, long? BeforeID, int Limit);

	public static class StaffChatLogs
	{
		public const int DefaultLimit = 50;
		public const int MaximumLimit = 200;

		// Only public conversations are readable: dm: and group: are refused with every other prefix
		public static (ChatLogFilter? Filter, Failure? Failure) ReadFilter(string? conversation, string? userID, string? from, string? to, string? beforeID, string? limit)
		{
			string? kind = null;
			long? conversationID = null;
			if (conversation != null)
			{
				if (!RealtimeHub.TryParseChannel(conversation, out string parsed, out long id) || parsed is not ("channel" or "lobby" or "match"))
				{
					return (null, Result.Invalid("conversation"));
				}
				kind = parsed;
				conversationID = id;
			}

			long? user = null;
			if (userID != null)
			{
				if (!long.TryParse(userID, NumberStyles.None, CultureInfo.InvariantCulture, out long parsedUser) || parsedUser <= 0)
				{
					return (null, Result.Invalid("user_id"));
				}
				user = parsedUser;
			}
			if (conversation == null && user == null)
			{
				return (null, Result.Invalid("conversation"));
			}

			(DateTime? start, bool fromValid) = StaffMatchActions.ReadTime(from, endOfDay: false);
			if (!fromValid)
			{
				return (null, Result.Invalid("from"));
			}
			(DateTime? end, bool toValid) = StaffMatchActions.ReadTime(to, endOfDay: true);
			if (!toValid)
			{
				return (null, Result.Invalid("to"));
			}

			long? before = null;
			if (beforeID != null)
			{
				if (!long.TryParse(beforeID, NumberStyles.None, CultureInfo.InvariantCulture, out long parsedBefore) || parsedBefore <= 0)
				{
					return (null, Result.Invalid("before_id"));
				}
				before = parsedBefore;
			}

			int count = DefaultLimit;
			if (limit != null && (!int.TryParse(limit, NumberStyles.None, CultureInfo.InvariantCulture, out count) || count < 1 || count > MaximumLimit))
			{
				return (null, Result.Invalid("limit"));
			}
			return (new ChatLogFilter(kind, conversationID, user, start, end, before, count), null);
		}

		// The filter only, the audit row never holds chat text
		public static object AuditDetails(ChatLogFilter filter) => new
		{
			conversation = filter.Kind == null ? null : filter.Kind + ":" + filter.ConversationID?.ToString(CultureInfo.InvariantCulture),
			user_id = filter.UserID,
			from = filter.From,
			to = filter.To,
			before_id = filter.BeforeID,
			limit = filter.Limit
		};

		public static PortalChatLogLine LineOf(ChatMessage row, IReadOnlyDictionary<long, (string Name, int Tag)> names)
		{
			(string name, int tag) = names.TryGetValue(row.SenderID, out (string Name, int Tag) found) ? found : (String.Empty, 0);
			return new PortalChatLogLine
			{
				Id = row.ID,
				Conversation = RealtimeHub.ConversationIDOf(row, row.SenderID),
				Sender = new PortalChatLogSender { Id = row.SenderID, Name = name, Tag = PlayerTags.Of(tag) },
				Scope = row.Kind == ChatMessageKinds.Match ? row.Scope : null,
				Text = row.Content,
				CreatedAt = DateTime.SpecifyKind(row.CreatedAt, DateTimeKind.Utc),
				Deleted = row.DeletedAt != null
			};
		}

		// The audit row is written before anything is shown, so a failed audit write returns no lines
		public static async Task<PortalChatLogPage> ReadAsync(IDbContextFactory<AppDbContext> dbFactory, Database.ModerationActor actor, ChatLogFilter filter, CancellationToken cancellationToken = default)
		{
			await using AppDbContext db = await dbFactory.CreateDbContextAsync(cancellationToken);
			List<ChatMessage> rows = await Database.Chat.GetLog(db, filter.Kind, filter.ConversationID, filter.UserID, filter.From, filter.To, filter.BeforeID, filter.Limit, cancellationToken);
			await Database.Moderation.AddAuditEntry(db, actor, ModerationActions.ChatLogView, AuditTargetKinds.Investigation, null, null, AuditDetails(filter));

			Dictionary<long, (string Name, int Tag)> names = await RealtimeHub.NamesAsync(db, rows.Select(r => r.SenderID));
			return new PortalChatLogPage { Messages = rows.Select(r => LineOf(r, names)).ToList() };
		}
	}

	[ApiController]
	[Authorize]
	public sealed class StaffChatLogsController : ControllerBase
	{
		private readonly IDbContextFactory<AppDbContext> m_dbFactory;

		public StaffChatLogsController(IDbContextFactory<AppDbContext> dbFactory)
		{
			m_dbFactory = dbFactory;
		}

		// ?conversation=channel:<id>|lobby:<id>|match:<id>&user_id=&from=&to=&before_id=&limit= (default 50, at most 200), newest first, keyset paged
		// by before_id, where a player alone spans the channel, lobby and match lines, direct and group messages are never readable and every read writes an audit row
		[AllowSignInCookie]
		[Authorize(Policy = PermissionPolicies.Prefix + Permissions.ChatLogs)]
		[EndpointSummary("Reads the chat of public channels, lobbies and matches, newest first.")]
		[ProducesResponseType<PortalChatLogPage>(StatusCodes.Status200OK)]
		[HttpGet("~/api/v1/admin/chat")]
		public async Task<IActionResult> Read()
		{
			(ChatLogFilter? filter, Failure? failure) = StaffChatLogs.ReadFilter(Text("conversation"), Text("user_id"), Text("from"), Text("to"), Text("before_id"), Text("limit"));
			if (failure != null)
			{
				return RestResults.Fail(failure.Value);
			}
			return RestResults.Ok(await StaffChatLogs.ReadAsync(m_dbFactory, CallerActors.Of(User), filter!, HttpContext.RequestAborted));
		}

		private string? Text(string name) => StaffPlayerActions.First(Request.Query, name) is { Length: > 0 } text ? text : null;
	}
}
