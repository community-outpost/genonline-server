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
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace GenOnlineService.Realtime
{
	public enum SanctionPlaceKind { Channel, Lobby, Match }

	public readonly record struct SanctionPlace(SanctionPlaceKind Kind, long ID)
	{
		// Kept in the audit details of the sanction
		public override string ToString() => $"{Kind.ToString().ToLowerInvariant()}:{ID.ToString(CultureInfo.InvariantCulture)}";
	}

	// The public line a kick, mute or ban leaves where the staff member took it
	public static class SanctionNotice
	{
		private static readonly ILogger s_log = AppLog.For(typeof(SanctionNotice));

		// Null place for no context or an empty one
		public static Failure? ParseContext(SanctionContext? context, out SanctionPlace? place)
		{
			place = null;
			if (context == null)
			{
				return null;
			}

			int given = (context.ChannelId != null ? 1 : 0) + (context.LobbyId != null ? 1 : 0) + (context.MatchId != null ? 1 : 0);
			if (given == 0)
			{
				return null;
			}

			if (given > 1)
			{
				return Result.Invalid("context");
			}

			if (context.ChannelId is long channelID && channelID >= 0 && channelID <= Int16.MaxValue)
			{
				place = new SanctionPlace(SanctionPlaceKind.Channel, channelID);
			}
			else if (context.LobbyId is long lobbyID && lobbyID > 0)
			{
				place = new SanctionPlace(SanctionPlaceKind.Lobby, lobbyID);
			}
			else if (context.MatchId != null && Int64.TryParse(context.MatchId, NumberStyles.None, CultureInfo.InvariantCulture, out long matchID) && matchID > 0)
			{
				place = new SanctionPlace(SanctionPlaceKind.Match, matchID);
			}

			return place == null ? Result.Invalid("context") : null;
		}

		// The reason is never part of the line
		public static string Line(string staff, string target, SanctionType type, long? durationS)
		{
			return type switch
			{
				SanctionType.Kick => $"{staff} kicked {target}.",
				SanctionType.Mute => $"{staff} muted {target} {Length(durationS)}.",
				_ => $"{staff} banned {target} {Length(durationS)}."
			};
		}

		// The largest unit that divides the length, a length that no unit divides is rounded up to whole minutes
		private static string Length(long? durationS)
		{
			if (durationS is not long seconds)
			{
				return "permanently";
			}

			(long Unit, string Name)[] units = [(86400, "day"), (3600, "hour"), (60, "minute")];
			foreach ((long unit, string name) in units)
			{
				if (seconds % unit == 0)
				{
					return $"for {Plural(seconds / unit, name)}";
				}
			}

			return $"for {Plural((seconds + 59) / 60, "minute")}";
		}

		private static string Plural(long count, string name) => count == 1 ? $"1 {name}" : $"{count} {name}s";

		// Sends the line when the staff member is in the place at this moment, true when it was sent
		internal static async Task<bool> AnnounceAsync(SanctionPlace place, long staff, string line, IEnumerable<Lobby> lobbies, Func<string, Message, Task> send)
		{
			string? group = null;
			string conversation = String.Empty;
			switch (place.Kind)
			{
				case SanctionPlaceKind.Channel:
				{
					if (RealtimeAudience.AllSessions().Any(s => s.m_UserID == staff && s.networkRoomID == place.ID))
					{
						group = RealtimeGroups.Channel(place.ID);
						conversation = Database.Chat.ChannelConversationKey(place.ID);
					}
					break;
				}
				case SanctionPlaceKind.Lobby:
				{
					Lobby? lobby = lobbies.FirstOrDefault(l => l.LobbyID == place.ID && IsMember(l, staff));
					if (lobby != null)
					{
						group = RealtimeGroups.Lobby(lobby.LobbyID);
						conversation = group;
					}
					break;
				}
				default:
				{
					Lobby? lobby = lobbies.FirstOrDefault(l => l.MatchID != 0 && l.MatchID == (UInt64)place.ID && IsMember(l, staff));
					if (lobby != null)
					{
						// the players of a match hear its chat on the lobby group while the lobby exists
						group = RealtimeGroups.Lobby(lobby.LobbyID);
						conversation = Database.Chat.MatchConversationKey(place.ID);
					}
					break;
				}
			}

			if (group == null)
			{
				return false;
			}

			// no author marks a system line, it is not stored so it never counts as a chat line of the staff member
			await send(group, new Message { Id = "0", ChannelId = conversation, Content = line, CreatedAt = DateTime.UtcNow });
			return true;
		}

		private static bool IsMember(Lobby lobby, long userID) => lobby.GetMemberFromUserID(userID) is LobbyMember member && member.IsHuman();

		internal static Task Deliver(string group, Message message) => RealtimeFanout.IsShared
			? RealtimeFanout.SendAsync(group, message, [])
			: RealtimeOut.Group(group).MessageCreate(message);

		// A failed line never fails the sanction
		public static async Task PostAsync(IDbContextFactory<AppDbContext> dbFactory, LobbyManager? lobbyManager, SanctionPlace place, long staff, long target, SanctionType type, long? durationS)
		{
			try
			{
				Dictionary<long, (string Name, int Tag)> names;
				await using (AppDbContext db = await dbFactory.CreateDbContextAsync())
				{
					names = await RealtimeHub.NamesAsync(db, [staff, target]);
				}

				string line = Line(names.TryGetValue(staff, out var staffName) ? staffName.Name : $"user {staff}", names.TryGetValue(target, out var targetName) ? targetName.Name : $"user {target}", type, durationS);
				await AnnounceAsync(place, staff, line, lobbyManager?.GetLobbies() ?? [], Deliver);
			}
			catch (Exception ex)
			{
				s_log.LogWarning(ex, "The public line of a sanction of user {Target} failed", target);
			}
		}
	}
}
