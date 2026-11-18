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
using GenOnlineService.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Text.RegularExpressions;
using Xunit;

namespace GenOnlineService.Tests;

public class PasswordResetTests
{
	private const string Password = "a long enough password";
	private const string NewPassword = "a brand new password";

	private static async Task<PageResponse> RequestReset(AccountsBrowser browser, string email)
	{
		PageResponse page = await browser.Get("/users/password/new");
		Assert.Equal(200, page.Status);
		return await browser.Post("/users/password", new() { ["__go_consent_token"] = AccountsBrowser.Token(page), ["email"] = email });
	}

	private static async Task<PageResponse> SetPassword(AccountsBrowser browser, string link, string password)
	{
		PageResponse page = await browser.Get(link);
		Assert.Equal(200, page.Status);
		string token = WebUtility.HtmlDecode(Regex.Match(page.Body, "name=\"reset_password_token\" value=\"([^\"]+)\"").Groups[1].Value);
		return await browser.Post("/users/password/edit", new()
		{
			["__go_consent_token"] = AccountsBrowser.Token(page),
			["reset_password_token"] = token,
			["password"] = password
		});
	}

	private static async Task<(AccountsBrowser Browser, RecordingEmailSender Mail, User User)> Account(ProbeDatabase probe, string email, Action<IServiceCollection>? configure = null)
	{
		var mail = new RecordingEmailSender();
		AccountsBrowser browser = await EmailConfirmationTests.Browser(probe, mail, configure);
		Assert.Equal(302, (await PasswordPagesTests.SignUp(browser, "Forgetful", email, Password)).Status);
		Assert.Equal(200, (await browser.Get(RecordingEmailSender.LinkIn(Assert.Single(mail.Sent)))).Status);
		mail.Sent.Clear();
		return (browser, mail, (await PasswordPagesTests.UserByEmail(probe, email))!);
	}

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task TheLinkSetsANewPasswordAndEndsTheOldSignIns(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		(AccountsBrowser browser, RecordingEmailSender mail, User user) = await Account(probe, "reset@example.org");
		string refresh = await RefreshToken(browser);

		await using (AppDbContext db = probe.Open())
		{
			User locked = await db.Users.AsTracking().SingleAsync(u => u.ID == user.ID);
			locked.LockoutEndAt = DateTime.UtcNow.AddMinutes(10);
			locked.FailedSignInCount = 5;
			await db.SaveChangesAsync();
		}
		string stampBefore = (await PasswordPagesTests.UserByEmail(probe, "reset@example.org"))!.SecurityStamp!;

		Assert.Equal(200, (await RequestReset(browser.Fork(), "reset@example.org")).Status);
		string link = RecordingEmailSender.LinkIn(Assert.Single(mail.Sent));

		AccountsBrowser stranger = browser.Fork();
		PageResponse tooShort = await SetPassword(stranger, link, "short");
		Assert.Equal(400, tooShort.Status);
		Assert.Contains("Passwords must be at least 8 characters.", tooShort.Body);

		PageResponse done = await SetPassword(stranger, link, NewPassword);
		Assert.Equal(200, done.Status);
		Assert.Contains("Your password is changed", done.Body);
		Assert.False(stranger.SignedIn);

		User after = (await PasswordPagesTests.UserByEmail(probe, "reset@example.org"))!;
		Assert.Null(after.LockoutEndAt);
		Assert.Equal(0, after.FailedSignInCount);
		Assert.NotEqual(stampBefore, after.SecurityStamp);
		await using (AppDbContext db = probe.Open())
		{
			AuditLogEntry audit = await db.AuditLog.SingleAsync(a => a.Action == "password_reset" && a.TargetID == user.ID);
			Assert.Equal(AuditActorKinds.User, audit.ActorKind);
			Assert.Equal(user.ID, audit.ActorID);
			Assert.Equal($"user:{user.ID}", audit.Actor);
			Assert.Equal(AuditTargetKinds.User, audit.TargetKind);
			Assert.Equal("web", audit.Source);
		}

		PageResponse refused = await browser.Post("/oauth/token", new() { ["grant_type"] = "refresh_token", ["refresh_token"] = refresh, ["client_id"] = "genonline_zerohour" });
		Assert.Equal(400, refused.Status);
		Assert.Equal("invalid_grant", System.Text.Json.JsonDocument.Parse(refused.Body).RootElement.GetProperty("error").GetString());

		Assert.Equal(401, (await PasswordPagesTests.SignIn(browser.Fork(), "reset@example.org", Password)).Status);
		Assert.Equal(302, (await PasswordPagesTests.SignIn(browser.Fork(), "reset@example.org", NewPassword)).Status);

		PageResponse reused = await stranger.Get(link);
		Assert.Equal(400, reused.Status);
		Assert.Contains("not valid or has expired", reused.Body);
	}

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task ABadOrExpiredLinkIsRefused(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		(AccountsBrowser browser, RecordingEmailSender mail, User user) = await Account(probe, "expired@example.org",
			services => services.PostConfigure<PasswordResetTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.FromSeconds(-1)));

		Assert.Equal(200, (await RequestReset(browser.Fork(), "expired@example.org")).Status);
		string link = RecordingEmailSender.LinkIn(Assert.Single(mail.Sent));
		string packed = Uri.UnescapeDataString(link.Substring(link.IndexOf('=') + 1));
		Assert.True(AccountMail.TryUnpack(packed, out long id, out string raw));

		Assert.Equal(400, (await browser.Get(link)).Status);
		Assert.Equal(400, (await browser.Get("/users/password/edit?reset_password_token=garbage")).Status);
		Assert.Equal(400, (await browser.Get("/users/password/edit?reset_password_token=" + Uri.EscapeDataString(AccountMail.Pack(id, raw + "x")))).Status);

		PageResponse form = await browser.Get("/users/password/new");
		PageResponse posted = await browser.Post("/users/password/edit", new()
		{
			["__go_consent_token"] = AccountsBrowser.Token(form),
			["reset_password_token"] = packed,
			["password"] = NewPassword
		});
		Assert.Equal(400, posted.Status);
		Assert.Equal(user.PasswordHash, (await PasswordPagesTests.UserByEmail(probe, "expired@example.org"))!.PasswordHash);
		await using AppDbContext db = probe.Open();
		Assert.False(await db.AuditLog.AnyAsync(a => a.Action == "password_reset" && a.TargetID == user.ID));
	}

	// a mail link never takes its host from the request: a forged Host header still gets the configured origin
	// without a configured origin no link is built at all, so nothing goes out with the request's host
	// one reset link redeemed by several requests at once sets the password once: one 200, one audit row
	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task ConcurrentRedemptionsOfOneLinkSucceedOnce(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		(AccountsBrowser browser, RecordingEmailSender mail, User user) = await Account(probe, "racer@example.org");
		Assert.Equal(200, (await RequestReset(browser.Fork(), "racer@example.org")).Status);
		string link = RecordingEmailSender.LinkIn(Assert.Single(mail.Sent));

		AccountsBrowser tab = browser.Fork();
		PageResponse page = await tab.Get(link);
		Assert.Equal(200, page.Status);
		string token = WebUtility.HtmlDecode(Regex.Match(page.Body, "name=\"reset_password_token\" value=\"([^\"]+)\"").Groups[1].Value);
		string antiforgery = AccountsBrowser.Token(page);

		PageResponse[] answers = await Task.WhenAll(Enumerable.Range(0, 4).Select(i => Task.Run(() => tab.Fork(keepCookies: true).Post("/users/password/edit", new()
		{
			["__go_consent_token"] = antiforgery,
			["reset_password_token"] = token,
			["password"] = NewPassword + i
		}))));

		Assert.Single(answers, a => a.Status == 200);
		Assert.Equal(3, answers.Count(a => a.Status == 400));
		await using AppDbContext db = probe.Open();
		Assert.Equal(1, await db.AuditLog.CountAsync(a => a.Action == "password_reset" && a.TargetID == user.ID));
	}

	private static async Task<string> RefreshToken(AccountsBrowser browser)
	{
		string verifier = "a-code-verifier-long-enough-for-pkce-0123456789-abcdefghij";
		string challenge = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(verifier))).TrimEnd('=').Replace('+', '-').Replace('/', '_');
		PageResponse authorized = await browser.Get($"/oauth/authorize?client_id=genonline_zerohour&response_type=code&redirect_uri=http%3A%2F%2F127.0.0.1%2Fcallback&scope=openid%20offline_access%20game&state=s&code_challenge={challenge}&code_challenge_method=S256");
		string code = System.Web.HttpUtility.ParseQueryString(new Uri(authorized.Location!).Query)["code"]!;
		PageResponse issued = await browser.Post("/oauth/token", new()
		{
			["grant_type"] = "authorization_code", ["code"] = code, ["redirect_uri"] = "http://127.0.0.1/callback",
			["client_id"] = "genonline_zerohour", ["code_verifier"] = verifier
		});
		Assert.Equal(200, issued.Status);
		return System.Text.Json.JsonDocument.Parse(issued.Body).RootElement.GetProperty("refresh_token").GetString()!;
	}
}
