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

using System.Runtime.CompilerServices;
using System.Text.Json;

namespace GenOnlineService.Realtime
{
	internal static class RealtimeAudience
	{
		public static IEnumerable<UserSession> AllSessions() => WebSocketManager.GetUserDataCache().Values.SelectMany(byUser => byUser.Values);

		public static string? ConnectionOf(UserSession session) => WebSocketManager.GetWebSocketForSession(session)?.ConnectionId;

		// True when either user blocked the other, read from the online users only
		public static bool BlockedEitherWay(Int64 userA, Int64 userB)
		{
			return SocialState.HasBlocked(userA, userB) || SocialState.HasBlocked(userB, userA);
		}
	}

	internal static class SocialState
	{
		public static UserSocialContainer? Of(Int64 userID) => WebSocketManager.GetSharedDataForUser(userID)?.GetSocialContainer();

		public static bool HasBlocked(Int64 userID, Int64 otherID) => Read(userID, c => c.Blocked.Contains(otherID));
		public static bool HasFriend(Int64 userID, Int64 otherID) => Read(userID, c => c.Friends.Contains(otherID));
		public static bool HasIncoming(Int64 userID, Int64 fromID) => Read(userID, c => c.PendingRequests.Contains(fromID));

		public static bool Read(Int64 userID, Func<UserSocialContainer, bool> read)
		{
			UserSocialContainer? container = Of(userID);
			if (container == null)
			{
				return false;
			}

			lock (container)
			{
				return read(container);
			}
		}

		// Also updates the sets kept for the user as a lobby host, and moves the sequence so a database read that raced the change reads again
		public static void Update(Int64 userID, Action<UserSocialContainer> update)
		{
			RealtimeBlocks.Local.Touch();
			foreach (UserSocialContainer? container in new[] { Of(userID), LobbyHosts.Of(userID) })
			{
				if (container == null)
				{
					continue;
				}

				lock (container)
				{
					update(container);
				}
			}
		}

		public static long[] Snapshot(Int64 userID, Func<UserSocialContainer, HashSet<Int64>> set)
		{
			UserSocialContainer? container = Of(userID);
			if (container == null)
			{
				return Array.Empty<long>();
			}

			lock (container)
			{
				return set(container).ToArray();
			}
		}
	}

	// Presence of a user from their sessions, an abandoned session in its reconnect grace still counts
	internal static class RealtimePresence
	{
		// What friends see, an invisible user looks exactly like an offline one
		public static FriendPresenceEvent Of(Int64 userID) => ForFriends(Real(userID), StatusOf(userID));

		public static PresenceStatus StatusOf(Int64 userID) => WebSocketManager.GetSharedDataForUser(userID)?.PresenceStatus ?? PresenceStatus.Online;

		public static bool TryParseStatus(string? value, out PresenceStatus status)
		{
			foreach (PresenceStatus candidate in Enum.GetValues<PresenceStatus>())
			{
				if (value == JsonNamingPolicy.SnakeCaseLower.ConvertName(candidate.ToString()))
				{
					status = candidate;
					return true;
				}
			}
			status = default;
			return false;
		}

		public static FriendPresenceEvent ForFriends(FriendPresenceEvent real, PresenceStatus status) =>
			status == PresenceStatus.Invisible ? new FriendPresenceEvent { UserId = real.UserId } : real with { Status = status };

		private static FriendPresenceEvent Real(Int64 userID)
		{
			List<UserSession> sessions = WebSocketManager.GetAllDataFromUser(userID);
			PresenceActivity activity = PresenceActivity.Offline;
			Int64? channelID = null;
			foreach (UserSession session in sessions)
			{
				PresenceActivity sessionActivity = ActivityOf(session);
				if (Rank(sessionActivity) > Rank(activity))
				{
					activity = sessionActivity;
				}
				// the game client's channel wins when the sessions sit in different channels
				if (session.networkRoomID >= 0 && (channelID == null || session.GetSessionType() == EUserSessionType.GameClient))
				{
					channelID = session.networkRoomID;
				}
			}

			return new FriendPresenceEvent
			{
				UserId = userID,
				Online = sessions.Count > 0,
				Clients = sessions.Select(s => RealtimeMembers.ClientOf(s.GetSessionType())).Distinct().OrderBy(c => c).ToArray(),
				Activity = activity,
				ChannelId = channelID
			};
		}

		private static PresenceActivity ActivityOf(UserSession session)
		{
			if (session.currentLobbyID != -1)
			{
				Lobby? lobby = ServiceLocator.Services.GetRequiredService<LobbyManager>().GetLobby(session.currentLobbyID);
				return lobby?.State == ELobbyState.INGAME ? PresenceActivity.InGame : PresenceActivity.Lobby;
			}

			if (session.IsRegisteredForMatchmaking)
			{
				return PresenceActivity.Matchmaking;
			}

			return session.networkRoomID >= 0 ? PresenceActivity.Channel : PresenceActivity.Menu;
		}

		private static int Rank(PresenceActivity activity) => activity switch
		{
			PresenceActivity.InGame => 5,
			PresenceActivity.Lobby => 4,
			PresenceActivity.Matchmaking => 3,
			PresenceActivity.Channel => 2,
			PresenceActivity.Menu => 1,
			_ => 0
		};
	}

	internal sealed class ChatRateLimiter
	{
		// Public channel chat
		public static readonly ChatRateLimiter Channel = new(RealtimeHub.ChatRateMessages, RealtimeHub.ChatRateWindowMs);

		// In-game match chat mirrored over REST, 20 lines per 10 s
		public static readonly ChatRateLimiter Match = new(20, 10_000);

		private readonly ConditionalWeakTable<SharedUserData, Queue<long>> m_windows = new();
		private readonly int m_messages;
		private readonly long m_windowMs;

		public ChatRateLimiter(int messages, long windowMs)
		{
			m_messages = messages;
			m_windowMs = windowMs;
		}

		public bool TryConsume(SharedUserData user, out long retryAfterMs)
		{
			Queue<long> window = m_windows.GetValue(user, _ => new Queue<long>());
			long now = Environment.TickCount64;

			lock (window)
			{
				while (window.TryPeek(out long sent) && now - sent >= m_windowMs)
				{
					window.Dequeue();
				}

				if (window.Count >= m_messages)
				{
					retryAfterMs = Math.Max(1, m_windowMs - (now - window.Peek()));
					return false;
				}

				window.Enqueue(now);
				retryAfterMs = 0;
				return true;
			}
		}
	}
}
