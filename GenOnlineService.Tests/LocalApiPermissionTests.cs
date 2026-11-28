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
using GenOnlineService.Rest;
using Xunit;

namespace GenOnlineService.Tests;

public class LocalApiPermissionTests
{
	private static readonly string[] s_localApi =
	{
		"local_api.match", "local_api.stats", "local_api.events", "local_api.interest", "local_api.camera.read", "local_api.chat.read", "local_api.chat.send",
		"local_api.query", "local_api.camera.control", "local_api.replay.control", "local_api.observe", "local_api.lobby.read", "local_api.lobby.control"
	};

	private static readonly IReadOnlyDictionary<string, string[]> s_noConfig = new Dictionary<string, string[]>(StringComparer.Ordinal);

	private static RoleCatalog With(params string[] permissions) => RoleCatalog.From(RoleCatalog.BuiltInDefinitions.Append(
		new RoleDefinition("helper", "Helper", null, null, 1500, false, new HashSet<string>(permissions, StringComparer.Ordinal))));

	[Fact]
	public void TheCatalogListsAllThirteenAndTheHumanSetHoldsThem()
	{
		string[] listed = PermissionCatalog.Entries.Where(e => e.Area == "Local API").Select(e => e.Name).Order(StringComparer.Ordinal).ToArray();

		Assert.Equal(s_localApi.Order(StringComparer.Ordinal), listed);
		Assert.All(s_localApi, p => Assert.Contains(p, Permissions.Human));
		Assert.All(PermissionCatalog.Entries, e => Assert.Contains(e.Name, Permissions.Human));
	}

	[Fact]
	public void AnyLocalApiPermissionImpliesTheFeatureAndNoOtherPermissionDoes()
	{
		RoleCatalog catalog = With(Permissions.LocalApiQuery);

		Assert.Equal(new[] { ClientFeatures.LocalApi }, ClientFeatures.For(s_noConfig, catalog, ["helper"]));
		Assert.Empty(ClientFeatures.For(s_noConfig, With(Permissions.ModerationKick), ["helper"]));
		Assert.Empty(ClientFeatures.For(s_noConfig, catalog, ["caster"]));
		Assert.Equal(new[] { ClientFeatures.LocalApi }, ClientFeatures.For(ClientFeatures.Defaults, With(), ["caster"]));
	}

	[Fact]
	public void ChangesReportsAUserWhoseRoleGainedALocalApiPermission()
	{
		Dictionary<long, HashSet<string>> roles = new() { [7] = new HashSet<string> { "helper" }, [8] = new HashSet<string> { "helper" } };

		var changes = ClientFeatures.Changes(s_noConfig, With(), roles, With(Permissions.LocalApiMatch), roles);

		Assert.Equal(new long[] { 7, 8 }, changes.Select(c => c.UserID));
		Assert.Equal(new[] { ClientFeatures.LocalApi }, changes[0].Features);
		Assert.Empty(ClientFeatures.Changes(s_noConfig, With(Permissions.LocalApiMatch), roles, With(Permissions.LocalApiMatch, Permissions.LocalApiStats), roles));
	}

	[Fact]
	public void LocalApiPermissionsMakeNoRoleStaff()
	{
		RoleCatalog catalog = With(s_localApi);

		Assert.False(catalog.IsStaff("helper"));
		Assert.Equal(0, catalog.RankOf(["helper"]));
		Assert.Equal(1500, catalog.PositionOf(["helper"]));
		Assert.True(catalog.IsStaff("owner"));
		Assert.Equal(RoleCatalog.OwnerPosition, catalog.RankOf(["helper", "owner"]));
	}

	[Fact]
	public void OnlyAPermissionBeyondTheLocalApiCountsAsStaff()
	{
		Assert.False(Permissions.AnyStaff(s_localApi));
		Assert.False(Permissions.AnyStaff([]));
		Assert.True(Permissions.AnyStaff([.. s_localApi, Permissions.ModerationKick]));
	}
}
