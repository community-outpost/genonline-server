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

using System.Text.RegularExpressions;

namespace GenOnlineService
{
	public sealed record RoleDefinition(string Name, string DisplayName, string? Color, string? Description, int Position, bool BuiltIn, IReadOnlySet<string> Permissions, string? Tag = null);

	public sealed class RoleCatalog
	{
		public const string Moderator = "moderator";
		public const string Admin = "admin";
		public const string Owner = "owner";
		public const string Caster = "caster";

		public const string Everyone = "everyone";

		// The built-in positions leave room between them for custom roles. A role's position is its rank in the moderation rules.
		public const int ModeratorPosition = 1000;
		public const int AdminPosition = 2000;
		public const int OwnerPosition = 3000;
		public const int CasterPosition = 500;
		public const int EveryonePosition = 0;

		public const int NameMaxLength = 32;
		public const int DisplayNameMaxLength = 64;
		public const int DescriptionMaxLength = 200;
		public const int TagMaxLength = 16;

		private static readonly Regex s_name = new Regex("^[a-z][a-z0-9_]{1,31}$", RegexOptions.CultureInvariant);
		private static readonly Regex s_color = new Regex("^#[0-9a-fA-F]{6}$", RegexOptions.CultureInvariant);

		private static readonly string[] s_moderatorPermissions =
		{
			Permissions.ModerationKick,
			Permissions.ModerationMute,
			Permissions.ModerationRename,
			Permissions.ChatDeleteAny,
			Permissions.ChatMessageAnyone
		};

		public IReadOnlyDictionary<string, RoleDefinition> Roles { get; }

		public RoleDefinition EveryoneRole => Roles[Everyone];

		private RoleCatalog(Dictionary<string, RoleDefinition> roles)
		{
			Roles = roles;
		}

		public static bool IsValidName(string name) => s_name.IsMatch(name);

		public static bool IsValidColor(string color) => s_color.IsMatch(color);

		// The game adds the brackets around the tag itself
		public static bool IsValidTag(string tag) => tag.Length <= TagMaxLength && !tag.Any(c => Char.IsControl(c) || c == '[' || c == ']');

		public static bool IsBuiltIn(string name) => name == Moderator || name == Admin || name == Owner || name == Caster || name == Everyone;

		private const string StaffTag = "★★GO STAFF★★";
		private const string StaffColor = "#f5c542";

		public static IReadOnlyList<RoleDefinition> BuiltInDefinitions { get; } = new[]
		{
			new RoleDefinition(Owner, "Owner", StaffColor, "Everything an admin may do, and the only role above admin. Granted with the roles command only.", OwnerPosition, true, new HashSet<string>(Permissions.Human, StringComparer.Ordinal), StaffTag),
			new RoleDefinition(Admin, "Admin", StaffColor, "Every staff permission. Manages moderators, casters and custom roles below it.", AdminPosition, true, new HashSet<string>(Permissions.Human, StringComparer.Ordinal), StaffTag),
			new RoleDefinition(Moderator, "Moderator", "#4aa3ff", "Kicks, mutes and renames players and deletes chat messages.", ModeratorPosition, true, new HashSet<string>(s_moderatorPermissions, StringComparer.Ordinal), "MOD"),
			new RoleDefinition(Caster, "Caster", null, "No permission and no rank. Switches on client features through Features:roles.", CasterPosition, true, new HashSet<string>(StringComparer.Ordinal)),
			new RoleDefinition(Everyone, "Everyone", null, "The baseline of every player. Holds no permission unless an owner adds one.", EveryonePosition, true, new HashSet<string>(StringComparer.Ordinal))
		};

		public static RoleCatalog Defaults { get; } = From(BuiltInDefinitions);

		// Safe by construction: machine and unknown permissions are dropped, a built-in role keeps its fixed position, owner keeps
		// every human permission, and a built-in role missing from the input comes back with its defaults
		public static RoleCatalog From(IEnumerable<RoleDefinition> definitions)
		{
			var roles = new Dictionary<string, RoleDefinition>(StringComparer.Ordinal);
			foreach (RoleDefinition definition in definitions)
			{
				HashSet<string> permissions = new(definition.Permissions.Where(Permissions.Human.Contains), StringComparer.Ordinal);
				int position = definition.Position;

				if (IsBuiltIn(definition.Name))
				{
					position = BuiltInDefinitions.First(b => b.Name == definition.Name).Position;
				}
				if (definition.Name == Owner)
				{
					permissions = new HashSet<string>(Permissions.Human, StringComparer.Ordinal);
				}

				roles[definition.Name] = definition with { Position = position, Permissions = permissions };
			}

			foreach (RoleDefinition builtIn in BuiltInDefinitions.Where(b => !roles.ContainsKey(b.Name)))
			{
				roles[builtIn.Name] = builtIn;
			}
			return new RoleCatalog(roles);
		}

		public IReadOnlyList<RoleDefinition> Assignable => Roles.Values.Where(r => r.Name != Everyone).OrderByDescending(r => r.Position).ToList();

		public bool IsKnown(string role) => role != Everyone && Roles.ContainsKey(role);

		public bool IsStaff(string role) => IsKnown(role) && Permissions.AnyStaff(Roles[role].Permissions);

		public int RankOf(IEnumerable<string> roles)
		{
			int rank = 0;
			foreach (string role in roles.Where(IsStaff))
			{
				rank = Math.Max(rank, Roles[role].Position);
			}
			return rank;
		}

		public int PositionOf(IEnumerable<string> roles)
		{
			int position = 0;
			foreach (string role in roles.Where(IsKnown))
			{
				position = Math.Max(position, Roles[role].Position);
			}
			return position;
		}

		public HashSet<string> PermissionsFor(IEnumerable<string> roles)
		{
			var result = new HashSet<string>(EveryoneRole.Permissions, StringComparer.Ordinal);
			foreach (string role in roles.Where(IsKnown))
			{
				result.UnionWith(Roles[role].Permissions);
			}
			return result;
		}
	}
}
