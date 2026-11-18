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
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace GenOnlineService.Tests;

public class PasswordPagesTests
{
	internal static Dictionary<string, string?> Enabled(params (string Key, string Value)[] extra)
	{
		Dictionary<string, string?> values = new() { ["Accounts:password:enabled"] = "true" };
		foreach ((string key, string value) in extra)
		{
			values[key] = value;
		}
		return values;
	}

	internal static async Task<PageResponse> SignUp(AccountsBrowser browser, string name, string email, string password, string? honeypot = null, bool agree = false)
	{
		PageResponse page = await browser.Get("/users/sign_up?ReturnUrl=%2Foauth%2Fauthorize%3Fclient_id%3Dx");
		Assert.Equal(200, page.Status);
		Dictionary<string, string> form = new()
		{
			["__go_consent_token"] = AccountsBrowser.Token(page),
			["display_name"] = name,
			["email"] = email,
			["password"] = password,
			["ReturnUrl"] = "/oauth/authorize?client_id=x"
		};
		if (honeypot != null)
		{
			form["website"] = honeypot;
		}
		if (agree || page.Body.Contains("name=\"agree_terms\"", StringComparison.Ordinal))
		{
			form["agree_terms"] = "1";
		}
		return await browser.Post("/users/sign_up", form);
	}

	internal static async Task<PageResponse> SignIn(AccountsBrowser browser, string email, string password)
	{
		PageResponse page = await browser.Get("/users/sign_in?ReturnUrl=%2Foauth%2Fauthorize%3Fclient_id%3Dx");
		Assert.Equal(200, page.Status);
		return await browser.Post("/users/sign_in", new()
		{
			["__go_consent_token"] = AccountsBrowser.Token(page),
			["email"] = email,
			["password"] = password,
			["ReturnUrl"] = "/oauth/authorize?client_id=x"
		});
	}

	internal static async Task<User?> UserByEmail(ProbeDatabase probe, string email)
	{
		await using AppDbContext db = probe.Open();
		string normalized = NameNormalizer.Normalize(email);
		return await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.EmailNormalized == normalized);
	}

	public static IEnumerable<object[]> Refusals()
	{
		foreach (object[] provider in ProbeDatabase.Providers())
		{
			yield return new object[] { provider[0], "Bot", "bot@example.org", "a long enough password", "http://spam.test", "Sign-up failed." };
			yield return new object[] { provider[0], "Denied", "someone@Mailinator.com", "a long enough password", "", "This email address cannot be used." };
			yield return new object[] { provider[0], "Denied", "someone@sub.mailinator.com", "a long enough password", "", "This email address cannot be used." };
			yield return new object[] { provider[0], "NoMail", "not an email", "a long enough password", "", "Enter a valid email address." };
			yield return new object[] { provider[0], "Short", "short@example.org", "short", "", "Passwords must be at least 8 characters." };
			yield return new object[] { provider[0], "Long", "long@example.org", new string('p', 129), "", "Passwords must be at most 128 characters." };
			yield return new object[] { provider[0], "TheAdmin", "admin@example.org", "a long enough password", "", "This name contains a reserved word." };
			yield return new object[] { provider[0], "Bad#Name", "hash@example.org", "a long enough password", "", "This name has characters that are not allowed." };
		}
	}

	// a person may hold more than one account, each with its own address: the exact address is the login identifier, so a
	// second account cannot take it, but another spelling of the same mailbox is free
	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task SignInWithPasswordAndLockout(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		Assert.Equal(302, (await SignUp(new AccountsBrowser(probe, Enabled()), "Signer", "signer@example.org", "the right password")).Status);

		var browser = new AccountsBrowser(probe, Enabled());
		PageResponse right = await SignIn(browser, "SIGNER@example.org", "the right password");
		Assert.Equal(302, right.Status);
		Assert.Equal("/oauth/authorize?client_id=x", right.Location);
		Assert.True(browser.SignedIn);

		PageResponse unknown = await SignIn(new AccountsBrowser(probe, Enabled()), "nobody@example.org", "the right password");
		Assert.Equal(401, unknown.Status);
		Assert.Contains("Wrong email or password.", unknown.Body);

		for (int attempt = 1; attempt <= 5; attempt++)
		{
			PageResponse wrong = await SignIn(new AccountsBrowser(probe, Enabled()), "signer@example.org", "a wrong password");
			Assert.Equal(401, wrong.Status);
			Assert.Contains("Wrong email or password.", wrong.Body);
		}

		var locked = new AccountsBrowser(probe, Enabled());
		PageResponse lockedOut = await SignIn(locked, "signer@example.org", "the right password");
		Assert.Equal(401, lockedOut.Status);
		Assert.Contains("Wrong email or password.", lockedOut.Body);
		Assert.False(locked.SignedIn);
		Assert.NotNull((await UserByEmail(probe, "signer@example.org"))!.LockoutEndAt);
	}

	internal static async Task SetLastSignIn(ProbeDatabase probe, string email, DateTime? value)
	{
		await using AppDbContext db = probe.Open();
		string normalized = NameNormalizer.Normalize(email);
		User user = await db.Users.AsTracking().SingleAsync(u => u.EmailNormalized == normalized);
		user.LastSignInAt = value;
		await db.SaveChangesAsync();
	}

	// until two-factor sign-in exists, an account with it on is never signed in by its password alone
	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task APasswordAloneNeverPassesTwoFactor(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		Assert.Equal(302, (await SignUp(new AccountsBrowser(probe, Enabled()), "Guarded", "guarded@example.org", "the right password")).Status);
		await using (AppDbContext db = probe.Open())
		{
			await db.Users.Where(u => u.Email == "guarded@example.org").ExecuteUpdateAsync(s => s.SetProperty(u => u.TwoFactorEnabled, true));
		}

		var browser = new AccountsBrowser(probe, Enabled());
		PageResponse response = await SignIn(browser, "guarded@example.org", "the right password");

		Assert.False(browser.SignedIn);
		Assert.NotEqual("/oauth/authorize?client_id=x", response.Location);
	}

}
