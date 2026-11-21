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

namespace GenOnlineService.Rest
{
	public sealed record RoleChange(string Name, string DisplayName, string? Color, string? Description, int Position, IReadOnlySet<string> Permissions);

	// The hierarchy rules of editing roles, as in Discord: a caller manages only roles below their own highest role, puts a role only
	// below it, and adds or removes only permissions they hold themselves. Owner is never editable, and built-in roles stay.
	public static class RoleDefinitionRules
	{
		public static Failure? ValidateFields(RoleChange wanted)
		{
			if (String.IsNullOrWhiteSpace(wanted.DisplayName) || wanted.DisplayName.Length > RoleCatalog.DisplayNameMaxLength) return Result.Invalid("display_name");
			if (wanted.Color != null && !RoleCatalog.IsValidColor(wanted.Color)) return Result.Invalid("color");
			if (wanted.Description != null && wanted.Description.Length > RoleCatalog.DescriptionMaxLength) return Result.Invalid("description");
			return null;
		}

		// A new role sits between the bottom and the owner, strictly below the caller, on a position no other role uses
		private static Failure? CheckPosition(RoleCatalog catalog, int callerPosition, string name, int position, string? keepsPositionOf)
		{
			if (position < 1 || position >= RoleCatalog.OwnerPosition) return Result.Invalid("position");
			if (position >= callerPosition) return Result.Fail(RestCodes.RoleRank, new { Role = name });
			if (catalog.Roles.Values.Any(r => r.Position == position && r.Name != keepsPositionOf)) return Result.Fail(RestCodes.RolePositionTaken, new { Position = position });
			return null;
		}

		// Machine and unknown permissions never go on a human role, and the caller needs every permission they hand out or take away
		private static Failure? CheckPermissions(IReadOnlySet<string> before, IReadOnlySet<string> wanted, IReadOnlySet<string> callerPermissions)
		{
			if (wanted.Any(p => !Permissions.Human.Contains(p))) return Result.Invalid("permissions");

			foreach (string permission in wanted.Except(before).Concat(before.Except(wanted)).Order(StringComparer.Ordinal))
			{
				if (!callerPermissions.Contains(permission))
				{
					return Result.Fail(RestCodes.RolePermissionNotHeld, new { Permission = permission });
				}
			}
			return null;
		}

		public static Failure? CheckCreate(RoleCatalog catalog, int callerPosition, IReadOnlySet<string> callerPermissions, RoleChange wanted)
		{
			if (!RoleCatalog.IsValidName(wanted.Name)) return Result.Invalid("name");
			if (catalog.Roles.ContainsKey(wanted.Name)) return Result.Fail(RestCodes.RoleExists, new { Role = wanted.Name });

			Failure? failure = ValidateFields(wanted) ?? CheckPosition(catalog, callerPosition, wanted.Name, wanted.Position, null);
			return failure ?? CheckPermissions(new HashSet<string>(StringComparer.Ordinal), wanted.Permissions, callerPermissions);
		}

		public static Failure? CheckUpdate(RoleCatalog catalog, int callerPosition, IReadOnlySet<string> callerPermissions, RoleDefinition existing, RoleChange wanted)
		{
			if (existing.Name == RoleCatalog.Owner) return Result.Fail(RestCodes.RoleLocked, new { Role = existing.Name });

			// everyone is the baseline of every player, so only an owner changes it
			int needed = existing.Name == RoleCatalog.Everyone ? RoleCatalog.OwnerPosition : existing.Position + 1;
			if (callerPosition < needed) return Result.Fail(RestCodes.RoleRank, new { Role = existing.Name });

			if (wanted.Name != existing.Name) return Result.Invalid("name");

			Failure? failure = ValidateFields(wanted);
			if (failure != null) return failure;

			if (existing.BuiltIn)
			{
				if (wanted.Position != existing.Position) return Result.Invalid("position");
			}
			else if (wanted.Position != existing.Position)
			{
				failure = CheckPosition(catalog, callerPosition, existing.Name, wanted.Position, existing.Name);
				if (failure != null) return failure;
			}

			return CheckPermissions(existing.Permissions, wanted.Permissions, callerPermissions);
		}

		public static Failure? CheckDelete(int callerPosition, RoleDefinition existing)
		{
			if (existing.Name == RoleCatalog.Owner) return Result.Fail(RestCodes.RoleLocked, new { Role = existing.Name });
			if (existing.BuiltIn) return Result.Fail(RestCodes.RoleBuiltIn, new { Role = existing.Name });
			if (existing.Position >= callerPosition) return Result.Fail(RestCodes.RoleRank, new { Role = existing.Name });
			return null;
		}

		public static bool CanManage(int callerPosition, RoleDefinition role)
		{
			if (role.Name == RoleCatalog.Owner) return false;
			return role.Name == RoleCatalog.Everyone ? callerPosition >= RoleCatalog.OwnerPosition : role.Position < callerPosition;
		}

		// The position that places a role directly above another one: halfway to the next role above it, never reaching the caller.
		// Everyone is a valid anchor: the role then becomes the lowest one. Null when the two have no room between them.
		public static int? PositionAbove(RoleCatalog catalog, string aboveRole, int callerPosition)
		{
			if (!catalog.Roles.TryGetValue(aboveRole, out RoleDefinition? below))
			{
				return null;
			}

			int lower = below.Position;
			int upper = Math.Min(callerPosition, catalog.Roles.Values.Where(r => r.Position > lower).Select(r => r.Position).DefaultIfEmpty(RoleCatalog.OwnerPosition).Min());
			int middle = lower + (upper - lower) / 2;
			return middle > lower && middle < upper ? middle : null;
		}
	}
}
