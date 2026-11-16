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

using GenOnlineService.Identity;
using OpenIddict.Abstractions;
using System.Security.Claims;
using Xunit;

namespace GenOnlineService.Tests
{
	public class UpstreamProfileTests
	{
		private static readonly UpstreamProviderSettings s_discord = new() { Name = "discord", Kind = ProviderKind.Discord };

		private static ClaimsPrincipal Principal(params (string Type, string Value)[] claims)
		{
			return new ClaimsPrincipal(new ClaimsIdentity(claims.Select(c => new Claim(c.Type, c.Value)), "test"));
		}

		[Fact]
		public void DiscordSuggestsTheDisplayName()
		{
			UpstreamProfile profile = UpstreamAccounts.ReadProfile(s_discord, Principal(
				(OpenIddictConstants.Claims.Subject, "1234"),
				(OpenIddictConstants.Claims.PreferredUsername, "arcticdolphin"),
				("global_name", "ArcticDolphin")));

			Assert.Equal("1234", profile.Subject);
			Assert.Equal("ArcticDolphin", profile.Name);
		}

		[Fact]
		public void DiscordWithoutADisplayNameSuggestsTheUsername()
		{
			UpstreamProfile profile = UpstreamAccounts.ReadProfile(s_discord, Principal(
				(OpenIddictConstants.Claims.Subject, "1234"),
				(OpenIddictConstants.Claims.PreferredUsername, "arcticdolphin")));

			Assert.Equal("1234", profile.Subject);
			Assert.Equal("arcticdolphin", profile.Name);
		}

		[Fact]
		public void OtherProvidersSuggestTheDisplayNameBeforeTheUsername()
		{
			var oidc = new UpstreamProviderSettings { Name = "oidc", Kind = ProviderKind.Oidc };
			UpstreamProfile profile = UpstreamAccounts.ReadProfile(oidc, Principal(
				(OpenIddictConstants.Claims.Subject, "abc"),
				(OpenIddictConstants.Claims.PreferredUsername, "player"),
				(OpenIddictConstants.Claims.Name, "Player One"),
				("global_name", "Ignored")));

			Assert.Equal("Player One", profile.Name);
		}

		[Fact]
		public void OtherProvidersWithoutADisplayNameSuggestTheUsername()
		{
			var oidc = new UpstreamProviderSettings { Name = "oidc", Kind = ProviderKind.Oidc };
			UpstreamProfile profile = UpstreamAccounts.ReadProfile(oidc, Principal(
				(OpenIddictConstants.Claims.Subject, "abc"),
				(OpenIddictConstants.Claims.PreferredUsername, "player")));

			Assert.Equal("player", profile.Name);
		}
	}
}
