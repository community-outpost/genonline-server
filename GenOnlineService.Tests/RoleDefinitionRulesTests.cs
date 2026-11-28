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
using GenOnlineService.Rest;
using Xunit;

namespace GenOnlineService.Tests;

public class RoleDefinitionRulesTests
{
	private static IReadOnlySet<string> Set(params string[] items) => new HashSet<string>(items, StringComparer.Ordinal);

	private static readonly IReadOnlySet<string> s_all = Permissions.Human;
	private static readonly IReadOnlySet<string> s_kickAndRoles = Set(Permissions.ModerationKick, Permissions.RolesManage, Permissions.AuditRead);

	private static RoleDefinition Custom(string name, int position, params string[] permissions) =>
		new(name, name, null, null, position, false, Set(permissions));

	private static RoleCatalog With(params RoleDefinition[] roles) => RoleCatalog.From(RoleCatalog.BuiltInDefinitions.Concat(roles));

	private static RoleChange Wanted(string name, int position, params string[] permissions) => new(name, name, null, null, position, Set(permissions));

	private static string? CodeOf(Failure? failure) => failure?.Code;

	private static string? FieldOf(Failure? failure) => failure == null ? null : System.Text.Json.JsonSerializer.Serialize(failure.Value.Params);

	[Fact]
	public void AnAdminCreatesARoleBelowItself()
	{
		Assert.Null(RoleDefinitionRules.CheckCreate(RoleCatalog.Defaults, RoleCatalog.AdminPosition, s_all, Wanted("helper", 1500, Permissions.ModerationKick)));
	}

	[Theory]
	[InlineData(RoleCatalog.AdminPosition)]
	[InlineData(RoleCatalog.OwnerPosition - 1)]
	public void ARoleAtOrAboveTheCallerIsRefused(int position)
	{
		Assert.Equal(RestCodes.RoleRank, CodeOf(RoleDefinitionRules.CheckCreate(RoleCatalog.Defaults, RoleCatalog.AdminPosition, s_all, Wanted("helper", position))));
	}

	[Fact]
	public void APositionAnotherRoleUsesIsRefused()
	{
		RoleCatalog catalog = With(Custom("helper", 1500));

		Assert.Equal(RestCodes.RolePositionTaken, CodeOf(RoleDefinitionRules.CheckCreate(catalog, RoleCatalog.AdminPosition, s_all, Wanted("other", 1500))));
		Assert.Equal(RestCodes.RolePositionTaken, CodeOf(RoleDefinitionRules.CheckCreate(catalog, RoleCatalog.OwnerPosition, s_all, Wanted("other", RoleCatalog.AdminPosition))));
	}

	[Fact]
	public void ACallerCannotHandOutAPermissionTheyDoNotHold()
	{
		Failure? failure = RoleDefinitionRules.CheckCreate(RoleCatalog.Defaults, RoleCatalog.AdminPosition, s_kickAndRoles, Wanted("helper", 1500, Permissions.ModerationKick, Permissions.ModerationBan));

		Assert.Equal(RestCodes.RolePermissionNotHeld, CodeOf(failure));
		Assert.Contains(Permissions.ModerationBan, FieldOf(failure));
	}

	[Fact]
	public void OwnerIsNeverDeletedAndNeverEditedByAnAdmin()
	{
		RoleDefinition owner = RoleCatalog.Defaults.Roles[RoleCatalog.Owner];

		Assert.Equal(RestCodes.RoleRank, CodeOf(RoleDefinitionRules.CheckUpdate(RoleCatalog.Defaults, RoleCatalog.AdminPosition, s_all, owner, Wanted("owner", owner.Position, [.. owner.Permissions]))));
		Assert.Equal(RestCodes.RolePermissionProtected, CodeOf(RoleDefinitionRules.CheckUpdate(RoleCatalog.Defaults, RoleCatalog.OwnerPosition, s_all, owner, Wanted("owner", owner.Position, Permissions.ModerationKick))));
		Assert.Equal(RestCodes.RoleLocked, CodeOf(RoleDefinitionRules.CheckDelete(RoleCatalog.OwnerPosition, owner)));
		Assert.False(RoleDefinitionRules.CanManage(RoleCatalog.AdminPosition, owner));
	}

	[Fact]
	public void ACallerEditsOnlyRolesStrictlyBelowTheirOwn()
	{
		RoleCatalog catalog = With(Custom("helper", 1500, Permissions.ModerationKick), Custom("lead", 1800, Permissions.ModerationKick));
		RoleDefinition lead = catalog.Roles["lead"];
		RoleDefinition helper = catalog.Roles["helper"];

		Assert.Equal(RestCodes.RoleRank, CodeOf(RoleDefinitionRules.CheckUpdate(catalog, 1500, s_all, lead, Wanted("lead", 1800, Permissions.ModerationKick))));
		Assert.Equal(RestCodes.RoleRank, CodeOf(RoleDefinitionRules.CheckUpdate(catalog, 1500, s_all, helper, Wanted("helper", 1500, Permissions.ModerationKick))));
		Assert.Null(RoleDefinitionRules.CheckUpdate(catalog, 1800, s_all, helper, Wanted("helper", 1500, Permissions.ModerationKick, Permissions.ModerationMute)));
	}

	[Fact]
	public void EveryoneCanOnlyBeChangedByAnOwner()
	{
		RoleDefinition everyone = RoleCatalog.Defaults.Roles[RoleCatalog.Everyone];
		RoleChange wanted = Wanted("everyone", 0, Permissions.PlayersInspect);

		Assert.Equal(RestCodes.RoleRank, CodeOf(RoleDefinitionRules.CheckUpdate(RoleCatalog.Defaults, RoleCatalog.AdminPosition, s_all, everyone, wanted)));
		Assert.Null(RoleDefinitionRules.CheckUpdate(RoleCatalog.Defaults, RoleCatalog.OwnerPosition, s_all, everyone, wanted));
	}

	[Fact]
	public void ARoleCannotBeRenamed()
	{
		RoleCatalog catalog = With(Custom("helper", 1500));

		Assert.Contains("name", FieldOf(RoleDefinitionRules.CheckUpdate(catalog, RoleCatalog.AdminPosition, s_all, catalog.Roles["helper"], Wanted("other", 1500))));
	}

	[Fact]
	public void ABuiltInRoleKeepsItsPositionButItsPermissionsCanChange()
	{
		RoleDefinition moderator = RoleCatalog.Defaults.Roles[RoleCatalog.Moderator];

		Assert.Contains("position", FieldOf(RoleDefinitionRules.CheckUpdate(RoleCatalog.Defaults, RoleCatalog.OwnerPosition, s_all, moderator, Wanted("moderator", 1500, [.. moderator.Permissions]))));
		Assert.Null(RoleDefinitionRules.CheckUpdate(RoleCatalog.Defaults, RoleCatalog.AdminPosition, s_all, moderator, Wanted("moderator", moderator.Position, [.. moderator.Permissions, Permissions.AuditRead])));
	}

	[Fact]
	public void ACustomRoleMovesOnlyToAFreePositionBelowTheCaller()
	{
		RoleCatalog catalog = With(Custom("helper", 1500), Custom("lead", 1800));
		RoleDefinition helper = catalog.Roles["helper"];

		Assert.Null(RoleDefinitionRules.CheckUpdate(catalog, RoleCatalog.AdminPosition, s_all, helper, Wanted("helper", 1600)));
		Assert.Equal(RestCodes.RolePositionTaken, CodeOf(RoleDefinitionRules.CheckUpdate(catalog, RoleCatalog.AdminPosition, s_all, helper, Wanted("helper", 1800))));
		Assert.Equal(RestCodes.RoleRank, CodeOf(RoleDefinitionRules.CheckUpdate(catalog, 1700, s_all, helper, Wanted("helper", 1700))));
		Assert.Equal(RestCodes.RoleRank, CodeOf(RoleDefinitionRules.CheckUpdate(catalog, RoleCatalog.AdminPosition, s_all, helper, Wanted("helper", RoleCatalog.AdminPosition))));
	}

	[Fact]
	public void AddingOrRemovingAPermissionNeedsHoldingIt()
	{
		RoleCatalog catalog = With(Custom("helper", 1500, Permissions.ModerationKick, Permissions.ModerationBan));
		RoleDefinition helper = catalog.Roles["helper"];

		Assert.Equal(RestCodes.RolePermissionNotHeld, CodeOf(RoleDefinitionRules.CheckUpdate(catalog, RoleCatalog.AdminPosition, s_kickAndRoles, helper, Wanted("helper", 1500, Permissions.ModerationKick, Permissions.ModerationBan, Permissions.ModerationMute))));
		Assert.Equal(RestCodes.RolePermissionNotHeld, CodeOf(RoleDefinitionRules.CheckUpdate(catalog, RoleCatalog.AdminPosition, s_kickAndRoles, helper, Wanted("helper", 1500, Permissions.ModerationKick))));
		Assert.Null(RoleDefinitionRules.CheckUpdate(catalog, RoleCatalog.AdminPosition, s_kickAndRoles, helper, Wanted("helper", 1500, Permissions.ModerationKick, Permissions.ModerationBan)));
	}

	[Fact]
	public void BuiltInRolesCannotBeDeleted()
	{
		foreach (string name in new[] { RoleCatalog.Admin, RoleCatalog.Moderator, RoleCatalog.Caster, RoleCatalog.Everyone })
		{
			Assert.Equal(RestCodes.RoleBuiltIn, CodeOf(RoleDefinitionRules.CheckDelete(RoleCatalog.OwnerPosition, RoleCatalog.Defaults.Roles[name])));
		}
	}

	[Fact]
	public void ACustomRoleIsDeletedByWhoeverIsAboveIt()
	{
		RoleCatalog catalog = With(Custom("helper", 1500));

		Assert.Equal(RestCodes.RoleRank, CodeOf(RoleDefinitionRules.CheckDelete(1500, catalog.Roles["helper"])));
		Assert.Null(RoleDefinitionRules.CheckDelete(RoleCatalog.AdminPosition, catalog.Roles["helper"]));
	}

}
