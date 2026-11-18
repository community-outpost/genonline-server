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
using System.Text.RegularExpressions;
using Xunit;

namespace GenOnlineService.Tests;

internal sealed class RecordingEmailSender : IEmailSender
{
	public bool Enabled { get; init; } = true;

	public List<EmailMessage> Sent { get; } = new();

	public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
	{
		lock (Sent)
		{
			Sent.Add(message);
		}
		return Task.CompletedTask;
	}

	public static string LinkIn(EmailMessage message, string host = "http://accounts.test")
	{
		System.Text.RegularExpressions.Match match = Regex.Match(message.Text, @"https?://\S+");
		Assert.True(match.Success, "the mail has no link: " + message.Text);
		Assert.StartsWith(host + "/", match.Value);
		Assert.Contains(System.Net.WebUtility.HtmlEncode(match.Value), message.Html);
		return match.Value.Substring(host.Length);
	}
}

public class EmailConfirmationTests
{
	internal const string AuthorizeQuery = "?client_id=genonline_zerohour&response_type=code&redirect_uri=http%3A%2F%2F127.0.0.1%2Fcallback&scope=openid%20game&state=s&code_challenge=E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM&code_challenge_method=S256";

	internal static Dictionary<string, string?> WithGameClient(params (string Key, string Value)[] extra) => PasswordPagesTests.Enabled(new[]
	{
		// the public origin of mail links, required with mail (AddMail refuses to start without it)
		("Accounts:issuer", "http://accounts.test/"),
		("Accounts:clients:0:client_id", "genonline_zerohour"),
		("Accounts:clients:0:display_name", "Generals Online (Zero Hour)"),
		("Accounts:clients:0:kind", "game"),
		("Accounts:clients:0:game", "zerohour"),
		("Accounts:clients:0:redirect_uris:0", "http://127.0.0.1/callback")
	}.Concat(extra).ToArray());

	internal static async Task<AccountsBrowser> Browser(ProbeDatabase probe, RecordingEmailSender? mail, Action<IServiceCollection>? configure = null, params (string Key, string Value)[] extra)
	{
		var browser = new AccountsBrowser(probe, WithGameClient(extra), services =>
		{
			if (mail != null)
			{
				services.AddSingleton<IEmailSender>(mail);
			}
			configure?.Invoke(services);
		});
		await AccountsSetup.SeedClients(browser.Services, browser.Settings);
		await using AppDbContext db = probe.Open();
		await ClientRegistry.Reload(db);
		return browser;
	}

	private static async Task<PageResponse> Resend(AccountsBrowser browser)
	{
		PageResponse page = await browser.Get("/users/confirmation/new?ReturnUrl=%2Fprofile");
		Assert.Equal(200, page.Status);
		return await browser.Post("/users/confirmation", new() { ["__go_consent_token"] = AccountsBrowser.Token(page), ["ReturnUrl"] = "/profile" });
	}

}
