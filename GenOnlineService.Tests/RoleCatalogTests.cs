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

using Xunit;

namespace GenOnlineService.Tests;

public class RoleCatalogTests
{
	private static RoleDefinition Role(string name, int position, params string[] permissions) =>
		new(name, name, null, null, position, false, new HashSet<string>(permissions, StringComparer.Ordinal));

	private static RoleCatalog WithRoles(params RoleDefinition[] extra) => RoleCatalog.From(RoleCatalog.BuiltInDefinitions.Concat(extra));

	[Fact]
	public void TheDefaultsGiveTheOldRanksAndStaffRules()
	{
		Assert.Equal(StaffRoles.OwnerRank, StaffRoles.RankOf(["owner"]));
		Assert.Equal(StaffRoles.AdminRank, StaffRoles.RankOf(["admin"]));
		Assert.Equal(RoleCatalog.ModeratorPosition, StaffRoles.RankOf(["moderator"]));
		Assert.Equal(0, StaffRoles.RankOf(["caster"]));
		Assert.Equal(0, StaffRoles.RankOf(["nonsense"]));
		Assert.True(StaffRoles.IsStaff("moderator"));
		Assert.False(StaffRoles.IsStaff("caster"));
		Assert.True(StaffRoles.IsKnown("caster"));
		Assert.False(StaffRoles.IsKnown("everyone"));
		Assert.Equal(new[] { "owner", "admin", "moderator", "caster" }, StaffRoles.All.ToArray());
	}

	[Fact]
	public void AMachinePermissionOnARoleIsDropped()
	{
		RoleCatalog catalog = WithRoles(Role("helper", 1500, Permissions.ModerationKick, Permissions.OpsDatabase, Permissions.UsersRevoke, "made.up"));

		Assert.Equal(new[] { Permissions.ModerationKick }, catalog.Roles["helper"].Permissions.ToArray());
	}

	[Fact]
	public void ARoleWithoutPermissionsHasAPositionButNoRank()
	{
		RoleCatalog catalog = WithRoles(Role("winner", 1500));

		Assert.Equal(0, catalog.RankOf(["winner"]));
		Assert.Equal(1500, catalog.PositionOf(["winner"]));
		Assert.False(catalog.IsStaff("winner"));
		Assert.True(catalog.IsKnown("winner"));
	}

	[Fact]
	public void EveryPlayerHoldsTheEveryoneRolePermissionsEvenWithoutAnyRole()
	{
		RoleDefinition everyone = new(RoleCatalog.Everyone, "Everyone", null, null, 0, true, new HashSet<string>([Permissions.PlayersInspect], StringComparer.Ordinal));
		RoleCatalog catalog = RoleCatalog.From([everyone]);

		Assert.Contains(Permissions.PlayersInspect, catalog.PermissionsFor([]));
		Assert.DoesNotContain(Permissions.ModerationBan, catalog.PermissionsFor([]));
		Assert.Contains(Permissions.ModerationKick, catalog.PermissionsFor([RoleCatalog.Moderator]));
		Assert.Contains(Permissions.PlayersInspect, catalog.PermissionsFor([RoleCatalog.Moderator]));
		Assert.Equal(0, catalog.RankOf([]));
		Assert.False(catalog.IsKnown(RoleCatalog.Everyone));
		Assert.Equal(0, catalog.RankOf([RoleCatalog.Everyone]));
	}

}
