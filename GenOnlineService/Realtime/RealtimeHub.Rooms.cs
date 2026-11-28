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

namespace GenOnlineService.Realtime
{
	public sealed partial class RealtimeHub
	{
		// Each copy sends only to its own connections, so no connection hears the change twice
		internal static async Task PublishChannelsAsync(RoomCatalogData catalog)
		{
			ChannelsUpdateEvent update = new() { Channels = catalog.Channels() };
			foreach (UserSession session in RealtimeAudience.AllSessions().ToList())
			{
				try
				{
					if (RealtimeAudience.ConnectionOf(session) != null)
					{
						RealtimeOut.Fire(RealtimeOut.Session(session).ChannelsUpdate(update));
					}
					await MoveFromHiddenRoomAsync(session, catalog);
				}
				catch (Exception ex)
				{
					AppLog.For(typeof(RealtimeHub)).LogWarning(ex, "ChannelsUpdate for user {UserID} failed", session.m_UserID);
				}
			}
		}

		// A session in a room that is no longer listed moves to the default of its parent, the way a channel join does
		private static async Task MoveFromHiddenRoomAsync(UserSession session, RoomCatalogData catalog)
		{
			Int16 oldChannel = session.networkRoomID;
			if (session.selectedNetworkRoomID >= 0)
			{
				session.selectedNetworkRoomID = (Int16)catalog.FallbackRoomID(session.selectedNetworkRoomID);
			}
			if (oldChannel < 0)
			{
				return;
			}

			Int16 newChannel = (Int16)catalog.FallbackRoomID(oldChannel);
			if (newChannel == oldChannel)
			{
				return;
			}

			session.networkRoomID = newChannel;
			string? connectionID = RealtimeAudience.ConnectionOf(session);
			if (connectionID != null)
			{
				await RealtimeOut.RemoveFromGroupAsync(connectionID, RealtimeGroups.Channel(oldChannel));
			}
			await AnnounceChannelLeaveAsync(session, oldChannel);

			if (newChannel >= 0 && connectionID != null)
			{
				await AnnounceChannelJoinAsync(session, newChannel);
				await RealtimeOut.AddToGroupAsync(connectionID, RealtimeGroups.Channel(newChannel));
				RealtimeOut.Fire(RealtimeOut.Session(session).ChannelMembers(BuildChannelMembers(newChannel, session.m_UserID)));
			}
			PublishPresence(session.m_UserID);
		}
	}
}
