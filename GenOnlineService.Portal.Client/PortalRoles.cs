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

namespace GenOnlineService.Portal.Client
{
	public static class PortalRoleText
	{
		public const string NeutralColor = "#8a8f98";

		// The base role every player holds, which cannot be a place to put a role above
		private const string Everyone = "everyone";

		// A custom role name, as the host checks it: 2 to 32 lowercase letters, digits or underscores, starting with a letter
		public static bool IsValidName(string? name) => name != null && System.Text.RegularExpressions.Regex.IsMatch(name, "^[a-z][a-z0-9_]{1,31}$");

		public static bool IsValidColor(string? color) => color != null && System.Text.RegularExpressions.Regex.IsMatch(color, "^#[0-9a-fA-F]{6}$");

		public static string ColorOf(string? color) => IsValidColor(color?.Trim()) ? color!.Trim() : NeutralColor;

		public static IReadOnlyList<(string Area, IReadOnlyList<PortalPermissionEntry> Permissions)> ByArea(IEnumerable<PortalPermissionEntry> permissions) =>
			permissions.GroupBy(p => p.Area).Select(g => (g.Key, (IReadOnlyList<PortalPermissionEntry>)g.ToList())).ToList();

		public static IReadOnlyList<(string Value, string Label)> Placements(IEnumerable<PortalRoleDefinition> roles, string? except = null)
		{
			var places = new List<(string, string)>();
			foreach (PortalRoleDefinition role in roles.Where(r => r.Manageable && r.Name != Everyone && r.Name != except))
			{
				places.Add((role.Name, places.Count == 0 ? "Top of my roles" : "Directly above " + role.DisplayName));
			}
			// any caller may put a role at the bottom, directly above everyone, since that outranks nothing
			PortalRoleDefinition? baseline = roles.FirstOrDefault(r => r.Name == Everyone);
			if (baseline != null && except != Everyone)
			{
				places.Add((Everyone, "Directly above " + baseline.DisplayName));
			}
			return places;
		}

		public static bool SameSet(IEnumerable<string> a, IEnumerable<string> b) => a.ToHashSet(StringComparer.Ordinal).SetEquals(b);

		// A typed member is a numeric id or a name part; the host refuses a name part of one character
		public static bool IsMemberTerm(string? term) => term != null && (term.Trim().Length >= 2 || (Int64.TryParse(term.Trim(), out long id) && id > 0));

		public static (PortalPlayerRow? Player, string? Error) PickMember(IReadOnlyList<PortalPlayerRow> matches, string term)
		{
			if (matches.Count == 0)
			{
				return (null, "No player with that name.");
			}
			if (matches.Count == 1)
			{
				return (matches[0], null);
			}
			var exact = matches.Where(m => String.Equals(m.FullName, term.Trim(), StringComparison.OrdinalIgnoreCase) || String.Equals(m.Name, term.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
			return exact.Count == 1 ? (exact[0], null) : (null, "Several players match, use the id.");
		}
	}
}
