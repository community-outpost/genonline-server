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

using GenOnlineService.Realtime;

namespace GenOnlineService
{
	// Sends a signed-in player the new permission list when a role reload changes it, Ready only carries the list at sign-in
	public static class PermissionUpdates
	{
		private static readonly ILogger s_log = AppLog.For(typeof(PermissionUpdates));

		private static IReadOnlyCollection<string> Of(RoleCatalog catalog, IReadOnlyDictionary<Int64, HashSet<string>> permissionsByUser, Int64 userID)
		{
			return permissionsByUser.TryGetValue(userID, out HashSet<string>? permissions) ? permissions : catalog.EveryoneRole.Permissions;
		}

		// A user without roles holds the everyone role, so an edit of that role changes the list of every such user
		public static IReadOnlyList<(Int64 UserID, IReadOnlyList<string> Permissions)> Changes(RoleCatalog beforeCatalog, IReadOnlyDictionary<Int64, HashSet<string>> before,
			RoleCatalog afterCatalog, IReadOnlyDictionary<Int64, HashSet<string>> after, IEnumerable<Int64> online)
		{
			List<(Int64, IReadOnlyList<string>)> result = new();
			foreach (Int64 userID in online.Distinct().Order())
			{
				string[] old = Of(beforeCatalog, before, userID).Order(StringComparer.Ordinal).ToArray();
				string[] next = Of(afterCatalog, after, userID).Order(StringComparer.Ordinal).ToArray();
				if (!old.SequenceEqual(next, StringComparer.Ordinal))
				{
					result.Add((userID, next));
				}
			}
			return result;
		}

		// Every copy sees the same role change on its own reload, so each sends only to its own connections
		public static void PublishChanges(RoleCatalog beforeCatalog, IReadOnlyDictionary<Int64, HashSet<string>> before, RoleCatalog afterCatalog, IReadOnlyDictionary<Int64, HashSet<string>> after)
		{
			IEnumerable<Int64> online = WebSocketManager.GetUserDataCache().Values.SelectMany(sessions => sessions.Keys);
			foreach ((Int64 userID, IReadOnlyList<string> permissions) in Changes(beforeCatalog, before, afterCatalog, after, online))
			{
				// one user's failure never costs the others their update, the role maps are already swapped
				try
				{
					PermissionsUpdateEvent update = new PermissionsUpdateEvent { Permissions = permissions };
					foreach (UserSession session in WebSocketManager.GetAllDataFromUser(userID))
					{
						RealtimeOut.Fire(RealtimeOut.Session(session).PermissionsUpdate(update));
					}
				}
				catch (Exception ex)
				{
					s_log.LogWarning(ex, "PermissionsUpdate for user {UserID} failed", userID);
				}
			}
		}
	}
}
