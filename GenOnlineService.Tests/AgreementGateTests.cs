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
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using Xunit;

namespace GenOnlineService.Tests;

[Collection(AdminAreaCollection.Name)]
public class AgreementGateTests
{
	internal const string AuthorizeQuery = EmailConfirmationTests.AuthorizeQuery;
	internal const string GatedPrefix = "/users/agreements?ReturnUrl=%2Foauth%2Fauthorize%3Fclient_id%3Dgenonline_zerohour";

	internal static Task<AccountsBrowser> Browser(ProbeDatabase probe, params (string Key, string Value)[] agreements)
		=> EmailConfirmationTests.Browser(probe, null, null, agreements);

	internal static async Task<(AccountsBrowser Browser, User User)> SignedUp(ProbeDatabase probe, string name, params (string Key, string Value)[] agreements)
	{
		AccountsBrowser browser = await Browser(probe, agreements);
		string email = name.ToLowerInvariant() + "@example.org";
		Assert.Equal(302, (await PasswordPagesTests.SignUp(browser, name, email, "a long enough password")).Status);
		User user = (await PasswordPagesTests.UserByEmail(probe, email))!;
		await Owing(probe, user.ID);
		return (browser, user);
	}

	// The sign-up's tick agrees at creation; these tests want an account that owes the agreements, so what it recorded is
	// taken away again
	internal static async Task Owing(ProbeDatabase probe, long userID)
	{
		await using AppDbContext db = probe.Open();
		await db.UserAgreements.Where(a => a.UserID == userID).ExecuteDeleteAsync();
		await db.AuditLog.Where(a => a.Action == Agreements.AcceptedAction && a.TargetID == userID).ExecuteDeleteAsync();
	}

	internal static async Task Agree(ProbeDatabase probe, long userID, string document, string version)
	{
		await using AppDbContext db = probe.Open();
		db.UserAgreements.Add(new UserAgreement { UserID = userID, Document = document, Version = version, AgreedAt = DateTime.UtcNow });
		await db.SaveChangesAsync();
	}

	internal static IDisposable Connected(long userID, string name)
	{
		var shared = (ConcurrentDictionary<long, SharedUserData>)typeof(WebSocketManager).GetField("m_dictSharedUserData", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
		var social = new UserSocialContainer { Friends = new HashSet<long>(), PendingRequests = new HashSet<long>(), Blocked = new HashSet<long>() };
		shared[userID] = new SharedUserData(userID, social, name, 1, new PlayerStats(userID, 1500, 0, 1500));
		var sessions = WebSocketManager.GetUserDataCache().GetOrAdd(EUserSessionType.GameClient, _ => new ConcurrentDictionary<long, UserSession>());
		sessions[userID] = new UserSession(userID, EUserSessionType.GameClient, "genonline_zerohour", "", "", 0, 0);
		return new Undo(() =>
		{
			sessions.TryRemove(userID, out _);
			shared.TryRemove(userID, out _);
		});
	}

	private sealed class Undo(Action action) : IDisposable
	{
		public void Dispose() => action();
	}

	private static string Base64UrlEncode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

	internal static async Task<string> RefreshToken(AccountsBrowser browser)
	{
		string verifier = "a-code-verifier-long-enough-for-pkce-0123456789-abcdefghij";
		string challenge = Base64UrlEncode(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(verifier)));
		string query = $"?client_id=genonline_zerohour&response_type=code&redirect_uri=http%3A%2F%2F127.0.0.1%2Fcallback&scope=openid%20offline_access%20game&state=s&code_challenge={challenge}&code_challenge_method=S256";
		PageResponse authorized = await browser.Get("/oauth/authorize" + query);
		Assert.StartsWith("http://127.0.0.1/callback?code=", authorized.Location);
		string code = System.Web.HttpUtility.ParseQueryString(new Uri(authorized.Location!).Query)["code"]!;

		PageResponse issued = await browser.Post("/oauth/token", new()
		{
			["grant_type"] = "authorization_code", ["code"] = code, ["redirect_uri"] = "http://127.0.0.1/callback",
			["client_id"] = "genonline_zerohour", ["code_verifier"] = verifier
		});
		Assert.Equal(200, issued.Status);
		return JsonDocument.Parse(issued.Body).RootElement.GetProperty("refresh_token").GetString()!;
	}

	internal static Task<PageResponse> Refresh(AccountsBrowser browser, string refreshToken)
		=> browser.Post("/oauth/token", new() { ["grant_type"] = "refresh_token", ["refresh_token"] = refreshToken, ["client_id"] = "genonline_zerohour" });

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task ARefreshIsRefusedWhileOwedAndNotConnected(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		(AccountsBrowser browser, User user) = await SignedUp(probe, "Refresher", AgreementsTests.Configured());
		await Agree(probe, user.ID, "rules", "2026-11");
		await Agree(probe, user.ID, "terms", "1");
		string refresh = await RefreshToken(browser);

		PageResponse refreshed = await Refresh(browser, refresh);
		Assert.Equal(200, refreshed.Status);
		refresh = JsonDocument.Parse(refreshed.Body).RootElement.GetProperty("refresh_token").GetString()!;

		await using (AppDbContext db = probe.Open())
		{
			await db.UserAgreements.Where(a => a.UserID == user.ID && a.Document == "rules").ExecuteDeleteAsync();
		}

		using (Connected(user.ID, "Refresher"))
		{
			PageResponse connected = await Refresh(browser, refresh);
			Assert.Equal(200, connected.Status);
			refresh = JsonDocument.Parse(connected.Body).RootElement.GetProperty("refresh_token").GetString()!;
		}

		PageResponse refused = await Refresh(browser, refresh);
		Assert.Equal(400, refused.Status);
		JsonElement error = JsonDocument.Parse(refused.Body).RootElement;
		Assert.Equal("invalid_grant", error.GetProperty("error").GetString());
		Assert.Contains("agreements", error.GetProperty("error_description").GetString());
	}
}
