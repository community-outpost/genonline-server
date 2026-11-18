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
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace GenOnlineService.Tests;

// Email + password accounts: the Accounts:password settings, the Identity options they drive, and
// the user store's password, email, lockout, authenticator key and recovery code parts against a real database
public class PasswordAccountsTests
{
	private static AccountsSettings Read(Dictionary<string, string?> values) => AccountsSettings.Read(new ConfigurationBuilder().AddInMemoryCollection(values).Build());

	[Fact]
	public void PasswordSignInIsOffByDefault()
	{
		PasswordSignInSettings password = Read(new()).Password;
		Assert.False(password.Enabled);
		Assert.True(password.AllowSignUp);
		Assert.Equal(8, password.MinLength);
		Assert.True(password.Honeypot);
		Assert.Empty(password.EmailDomainDenyList);
	}

	private static IServiceProvider Services(Dictionary<string, string?> values, IDbContextFactory<AppDbContext>? factory = null)
	{
		ServiceCollection services = new();
		services.AddLogging();
		if (factory != null)
		{
			services.AddSingleton(factory);
		}
		values = new Dictionary<string, string?>(values) { ["Accounts:ephemeral_keys"] = "true" };
		services.AddGenOnlineIdentity(Read(values), development: true);
		return services.BuildServiceProvider();
	}

	[Fact]
	public async Task OverlongPasswordsAreRefused()
	{
		IServiceProvider provider = Services(new());
		var validator = new MaximumLengthPasswordValidator();

		Assert.True((await validator.ValidateAsync(null!, new User(), new string('p', 128))).Succeeded);
		IdentityResult tooLong = await validator.ValidateAsync(null!, new User(), new string('p', 129));
		Assert.False(tooLong.Succeeded);
		Assert.Equal(MaximumLengthPasswordValidator.ErrorCode, Assert.Single(tooLong.Errors).Code);
		Assert.Contains(provider.GetServices<IPasswordValidator<User>>(), v => v is MaximumLengthPasswordValidator);
	}

	private sealed class Factory : IDbContextFactory<AppDbContext>
	{
		private readonly ProbeDatabase m_probe;

		public Factory(ProbeDatabase probe)
		{
			m_probe = probe;
		}

		public AppDbContext CreateDbContext() => m_probe.Open();
	}

	private static async Task<(IServiceProvider Provider, User User)> Account(ProbeDatabase probe, string email)
	{
		IServiceProvider provider = Services(new() { ["Accounts:password:enabled"] = "true" }, new Factory(probe));
		UserManager<User> users = provider.GetRequiredService<UserManager<User>>();
		var user = new User { Username = "Player" };
		IdentityResult created = await users.CreateAsync(user, "correct horse battery");
		Assert.True(created.Succeeded, String.Join(", ", created.Errors.Select(e => e.Description)));
		Assert.True((await users.SetEmailAsync(user, email)).Succeeded);
		return (provider, user);
	}

	// Identity writes the user it holds; a block, a rename or a role decision made meanwhile elsewhere stays
}
