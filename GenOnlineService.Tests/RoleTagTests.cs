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

public class RoleTagTests
{
	private static RoleDefinition Role(string name, int position, string? tag, string? color) =>
		new(name, name, color, null, position, false, new HashSet<string>(StringComparer.Ordinal), tag);

	private static RoleCatalog With(params RoleDefinition[] roles) => RoleCatalog.From(RoleCatalog.BuiltInDefinitions.Concat(roles));

	private static RoleChange Wanted(string? tag) => new("helper", "Helper", null, null, 1500, new HashSet<string>(StringComparer.Ordinal), tag);

	[Theory]
	[InlineData("MOD")]
	[InlineData("★★GO STAFF★★")]
	[InlineData("1234567890123456")]
	[InlineData(null)]
	[InlineData("")]
	public void AValidOrEmptyTagIsAccepted(string? tag)
	{
		Assert.Null(RoleDefinitionRules.ValidateFields(Wanted(tag)));
	}

	[Theory]
	[InlineData("12345678901234567")]
	[InlineData("A\tB")]
	[InlineData("[MOD]")]
	[InlineData("MOD]")]
	public void AnInvalidTagIsRefused(string tag)
	{
		Failure? failure = RoleDefinitionRules.ValidateFields(Wanted(tag));

		Assert.NotNull(failure);
		Assert.Contains("tag", System.Text.Json.JsonSerializer.Serialize(failure!.Value.Params));
	}

	[Fact]
	public void BadgesListOnlyRolesWithATagOrAColour()
	{
		RoleCatalog catalog = With(Role("tagonly", 1500, "TAG", null), Role("coloronly", 1600, null, "#112233"), Role("plain", 1700, null, null));

		IReadOnlyDictionary<string, BadgeDefinition> badges = RealtimeBadges.Definitions(catalog);

		Assert.Equal(new BadgeDefinition { Text = "TAG", Color = "" }, badges["tagonly"]);
		Assert.Equal(new BadgeDefinition { Text = "", Color = "#112233" }, badges["coloronly"]);
		Assert.DoesNotContain("plain", badges.Keys);
		Assert.DoesNotContain("caster", badges.Keys);
		Assert.DoesNotContain("everyone", badges.Keys);
	}

	[Fact]
	public void TheBuiltInStaffRolesKeepTheirOldLook()
	{
		IReadOnlyDictionary<string, BadgeDefinition> badges = RealtimeBadges.Definitions(RoleCatalog.Defaults);

		Assert.Equal(new BadgeDefinition { Text = "★★GO STAFF★★", Color = "#f5c542" }, badges["admin"]);
		Assert.Equal(badges["admin"], badges["owner"]);
		Assert.Equal(new BadgeDefinition { Text = "MOD", Color = "#4aa3ff" }, badges["moderator"]);
	}

	[Fact]
	public void AUserSeesTheirBadgesHighestPositionFirstWithCustomRolesIncluded()
	{
		RoleCatalog catalog = With(Role("vip", 1500, "VIP", "#ff00ff"), Role("plain", 1700, null, null));

		Assert.Equal(new[] { "admin", "vip", "moderator" }, RealtimeBadges.ForRoles(["moderator", "plain", "vip", "admin", "nonsense", "everyone"], catalog));
		Assert.Empty(RealtimeBadges.ForRoles(["plain", "caster"], catalog));
	}
}
