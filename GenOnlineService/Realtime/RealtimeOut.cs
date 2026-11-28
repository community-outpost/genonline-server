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

using Microsoft.AspNetCore.SignalR;
using System.Reflection;

namespace GenOnlineService.Realtime
{
	public static class RealtimeGroups
	{
		public const string LobbyList = "lobby-list";

		public static string User(Int64 userID) => $"user:{userID}";
		public static string UserGame(Int64 userID) => $"user:{userID}:game";

		// The one connection of a user and session type, a newer connection supersedes the older one
		public static string UserSession(Int64 userID, EUserSessionType sessionType) => sessionType switch
		{
			EUserSessionType.GameLauncher => $"user:{userID}:launcher",
			EUserSessionType.ChatClient => $"user:{userID}:chat",
			_ => UserGame(userID),
		};

		public static string Channel(Int64 channelID) => $"channel:{channelID}";
		public static string Lobby(Int64 lobbyID) => $"lobby:{lobbyID}";
	}

	// Sends server events from anywhere in the service, only through SignalR users and groups so a backplane carries them to every copy
	public static class RealtimeOut
	{
		private static readonly ILogger s_log = AppLog.For(typeof(RealtimeOut));
		private static readonly IRealtimeClient s_nullClient = NullRealtimeClient.Create();
		private static IHubContext<RealtimeHub, IRealtimeClient>? s_hub;

		public static void Initialize(IHubContext<RealtimeHub, IRealtimeClient> hub)
		{
			s_hub = hub;
		}

		public static IRealtimeClient All => s_hub?.Clients.All ?? s_nullClient;
		public static IRealtimeClient Group(string group) => s_hub?.Clients.Group(group) ?? s_nullClient;
		public static IRealtimeClient Groups(IReadOnlyList<string> groups) => groups.Count == 0 ? s_nullClient : s_hub?.Clients.Groups(groups) ?? s_nullClient;

		// The excluded connection ids come from this copy's sessions, so chat uses it only with backplane none and RealtimeFanout otherwise
		public static IRealtimeClient GroupExcept(string group, IReadOnlyList<string> excludedConnectionIDs) => s_hub?.Clients.GroupExcept(group, excludedConnectionIDs) ?? s_nullClient;

		// Every connection of the user on any copy through the user group, which a connection joins just before Ready so Ready stays the first event
		public static IRealtimeClient User(Int64 userID) => Group(RealtimeGroups.User(userID));
		public static IRealtimeClient Users(IEnumerable<Int64> userIDs) => Groups(userIDs.Distinct().Select(RealtimeGroups.User).ToArray());
		public static IRealtimeClient UserGame(Int64 userID) => Group(RealtimeGroups.UserGame(userID));
		public static IRealtimeClient Lobby(Int64 lobbyID) => Group(RealtimeGroups.Lobby(lobbyID));

		public static Task AddToGroupAsync(string connectionID, string group) => s_hub?.Groups.AddToGroupAsync(connectionID, group) ?? Task.CompletedTask;
		public static Task RemoveFromGroupAsync(string connectionID, string group) => s_hub?.Groups.RemoveFromGroupAsync(connectionID, group) ?? Task.CompletedTask;

		// a failed send is only logged
		public static void Fire(Task send)
		{
			if (send.IsCompletedSuccessfully)
			{
				return;
			}

			send.ContinueWith(t => s_log.LogWarning(t.Exception, "Realtime send failed"), TaskContinuationOptions.OnlyOnFaulted);
		}

		public static IRealtimeClient Session(UserSession? session)
		{
			// the lookup only decides whether the session still owns a connection, delivery goes through the group
			if (session == null || WebSocketManager.GetWebSocketForSession(session) == null)
			{
				return s_nullClient;
			}
			return Group(RealtimeGroups.UserSession(session.m_UserID, session.GetSessionType()));
		}
	}

	// Names SignalR users by the user id the session principal carries in ClaimTypes.NameIdentifier, the OpenIddict subject
	public sealed class RealtimeUserIdProvider : IUserIdProvider
	{
		public static string Format(Int64 userID) => userID.ToString(System.Globalization.CultureInfo.InvariantCulture);

		public string? GetUserId(HubConnectionContext connection)
		{
			Int64 userID = RealtimeClaims.UserID(connection.User);
			return userID > 0 ? Format(userID) : null;
		}
	}

	public class NullRealtimeClient : DispatchProxy
	{
		public static IRealtimeClient Create() => DispatchProxy.Create<IRealtimeClient, NullRealtimeClient>();

		protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Task.CompletedTask;
	}

	public static class RealtimeMembers
	{
		public static ClientType ClientOf(EUserSessionType sessionType) => sessionType switch
		{
			EUserSessionType.GameLauncher => ClientType.Launcher,
			EUserSessionType.ChatClient => ClientType.Chat,
			_ => ClientType.Game,
		};

		public static string NameOf(Int64 userID) => WebSocketManager.GetSharedDataForUser(userID)?.m_strDisplayName ?? String.Empty;

		// The tag of an online user; 0 when offline or not known
		public static int TagOf(Int64 userID) => WebSocketManager.GetSharedDataForUser(userID)?.m_NameTag ?? 0;

		// displayName and nameTag come from the database for users who may be offline; otherwise the online data is used
		public static Member Of(Int64 userID, string? displayName = null, ClientType? client = null, int? nameTag = null)
		{
			int tag = nameTag ?? (displayName == null ? TagOf(userID) : 0);
			return new Member
			{
				Id = userID,
				Name = displayName ?? NameOf(userID),
				Tag = tag > 0 ? Database.DisplayNames.FormatTag(tag) : null,
				Badges = RealtimeBadges.For(userID),
				Client = client
			};
		}

		// A name looked up with RealtimeHub.NamesAsync; users not in it fall back to the online data
		public static Member Of(Int64 userID, IReadOnlyDictionary<long, (string Name, int Tag)> names) =>
			names.TryGetValue(userID, out (string Name, int Tag) stored) ? Of(userID, stored.Name, null, stored.Tag) : Of(userID);

		public static Member Of(UserSession session) => Of(session.m_UserID, null, ClientOf(session.GetSessionType())) with { Status = VisibleStatusOf(session.m_UserID) };

		public static PresenceStatus? VisibleStatusOf(Int64 userID)
		{
			PresenceStatus? status = WebSocketManager.GetSharedDataForUser(userID)?.PresenceStatus;
			return status == PresenceStatus.Invisible ? null : status;
		}
	}

	// Every role with a tag or a colour shows a badge of its own name, so a role edited in the portal reaches the game
	public static class RealtimeBadges
	{
		private static readonly ILogger s_log = AppLog.For(typeof(RealtimeBadges));

		public static IReadOnlyDictionary<string, BadgeDefinition> Definitions(RoleCatalog catalog) =>
			catalog.Assignable
				.Where(r => !String.IsNullOrEmpty(r.Tag) || !String.IsNullOrEmpty(r.Color))
				.ToDictionary(r => r.Name, r => new BadgeDefinition { Text = r.Tag ?? "", Color = r.Color ?? "" }, StringComparer.Ordinal);

		public static IReadOnlyList<string> For(Int64 userID) => ForRoles(StaffRoles.RolesOf(userID), StaffRoles.Catalog);

		// The game takes the tag and the colour from the first listed badge that has one, so the highest position goes first
		public static IReadOnlyList<string> ForRoles(IEnumerable<string> roles, RoleCatalog catalog)
		{
			IReadOnlyDictionary<string, BadgeDefinition> definitions = Definitions(catalog);
			return roles.Distinct(StringComparer.Ordinal).Where(definitions.ContainsKey).OrderByDescending(r => catalog.Roles[r].Position).ToArray();
		}

		// Each copy sends only to its own connections, and one failed send never stops the rest
		public static void PublishChanges(RoleCatalog before, RoleCatalog after)
		{
			IReadOnlyDictionary<string, BadgeDefinition> next = Definitions(after);
			IReadOnlyDictionary<string, BadgeDefinition> old = Definitions(before);
			if (old.Count == next.Count && old.All(b => next.TryGetValue(b.Key, out BadgeDefinition? d) && d == b.Value))
			{
				return;
			}

			BadgesUpdateEvent update = new BadgesUpdateEvent { Badges = next };
			foreach (UserSession session in RealtimeAudience.AllSessions().ToList())
			{
				try
				{
					RealtimeOut.Fire(RealtimeOut.Session(session).BadgesUpdate(update));
				}
				catch (Exception ex)
				{
					s_log.LogWarning(ex, "BadgesUpdate for user {UserID} failed", session.m_UserID);
				}
			}
		}
	}
}
