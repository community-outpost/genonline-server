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
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using Xunit;

namespace GenOnlineService.Tests;

[Collection(AdminAreaCollection.Name)]
public class AccountSupportTests
{
	private const string OldPassword = "a long enough password";
	private const string SignedInLocation = "/oauth/authorize?client_id=x";

	private static async Task<(AdminBrowser Browser, User User)> StaffAsync(ProbeDatabase probe, string name, string role)
	{
		var browser = new AdminBrowser(probe);
		string email = name.ToLowerInvariant() + "@example.org";
		await browser.SignUp(name, email);
		return (browser, await AdminArea.Grant(probe, email, role));
	}

	private static async Task<User> PlayerAsync(ProbeDatabase probe, string name)
	{
		var browser = new AdminBrowser(probe);
		string email = name.ToLowerInvariant() + "@example.org";
		await browser.SignUp(name, email);
		return (await PasswordPagesTests.UserByEmail(probe, email))!;
	}

	// the rank rule reads the process-wide role cache
	private static async Task ReloadRoles(ProbeDatabase probe)
	{
		await using AppDbContext db = probe.Open();
		await StaffRoles.Reload(db);
	}

	private static async Task<User> Stored(ProbeDatabase probe, long id)
	{
		await using AppDbContext db = probe.Open();
		return await db.Users.AsNoTracking().SingleAsync(u => u.ID == id);
	}

	private static async Task<List<AuditLogEntry>> AuditOf(ProbeDatabase probe, string action)
	{
		await using AppDbContext db = probe.Open();
		return await db.AuditLog.AsNoTracking().Where(e => e.Action == action).OrderBy(e => e.ID).ToListAsync();
	}

	private static async Task<string?> SignInLocation(ProbeDatabase probe, string email, string password, AccountsBrowser? browser = null)
	{
		browser ??= new AccountsBrowser(probe, PasswordPagesTests.Enabled());
		browser.SignOutLocally();
		PageResponse page = await PasswordPagesTests.SignIn(browser, email, password);
		return page.Status == 302 ? page.Location : null;
	}

	private static async Task SetUpAuthenticator(AccountsBrowser browser, long id)
	{
		using IServiceScope scope = browser.Services.CreateScope();
		UserManager<User> users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
		User user = (await users.FindByIdAsync(id.ToString(System.Globalization.CultureInfo.InvariantCulture)))!;
		await users.ResetAuthenticatorKeyAsync(user);
		await users.SetTwoFactorEnabledAsync(user, true);
		await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
	}

	private static async Task TurnOnTwoFactor(ProbeDatabase probe, long id)
	{
		await using AppDbContext db = probe.Open();
		await db.Users.Where(u => u.ID == id).ExecuteUpdateAsync(set => set.SetProperty(u => u.TwoFactorEnabled, true).SetProperty(u => u.AuthenticatorKey, "protected-key"));
		db.UserRecoveryCodes.Add(new UserRecoveryCode { UserID = id, CodeHash = new string('a', 64), CreatedAt = DateTime.UtcNow });
		await db.SaveChangesAsync();
	}

	private static async Task<Dictionary<string, string>> ServiceToken(ProbeDatabase probe, string permission)
	{
		string token = ApiTokenFormat.Generate();
		await using AppDbContext db = probe.Open();
		var account = new ServiceAccount { Name = "support-" + Guid.NewGuid().ToString("N").Substring(0, 8), CreatedBy = "test", CreatedAt = DateTime.UtcNow };
		db.ServiceAccounts.Add(account);
		await db.SaveChangesAsync();
		db.ServiceAccountPermissions.Add(new ServiceAccountPermission { ServiceAccountID = account.ID, Permission = permission, GrantedBy = "test", GrantedAt = DateTime.UtcNow });
		db.ApiTokens.Add(new ApiToken
		{
			OwnerServiceID = account.ID,
			Name = "support",
			TokenHash = ApiTokenFormat.Hash(token),
			TokenLast4 = ApiTokenFormat.Last4(token),
			Scopes = permission,
			CreatedBy = "test",
			CreatedAt = DateTime.UtcNow,
			ExpiresAt = DateTime.UtcNow.AddDays(1)
		});
		await db.SaveChangesAsync();
		await ApiTokenCache.Reload(db);
		return new Dictionary<string, string> { ["Authorization"] = "Bearer " + token };
	}

	private static readonly (string Key, string Value)[] s_gameClient =
	{
		("Accounts:clients:0:client_id", "genonline_zerohour"),
		("Accounts:clients:0:display_name", "Generals Online (Zero Hour)"),
		("Accounts:clients:0:kind", "game"),
		("Accounts:clients:0:game", "zerohour"),
		("Accounts:clients:0:redirect_uris:0", "http://127.0.0.1/callback")
	};

	private const string Verifier = "a-code-verifier-long-enough-for-pkce-0123456789-abcdefghij";

	private static async Task<(AdminBrowser Game, User User)> GamePlayerAsync(ProbeDatabase probe, string name)
	{
		var game = new AdminBrowser(probe, s_gameClient);
		await game.SeedClients();
		string email = name.ToLowerInvariant() + "@example.org";
		await game.SignUp(name, email);
		await using (AppDbContext db = probe.Open())
		{
			await ClientRegistry.Reload(db);
		}
		return (game, (await PasswordPagesTests.UserByEmail(probe, email))!);
	}

	private static async Task<(string Access, string Refresh)> GameSignIn(AdminBrowser game)
	{
		string challenge = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(Verifier))).TrimEnd('=').Replace('+', '-').Replace('/', '_');
		PageResponse authorized = await game.Get($"/oauth/authorize?client_id=genonline_zerohour&response_type=code&redirect_uri=http%3A%2F%2F127.0.0.1%2Fcallback&scope=openid%20offline_access%20game&state=s&code_challenge={challenge}&code_challenge_method=S256");
		Assert.Equal(302, authorized.Status);
		string code = System.Web.HttpUtility.ParseQueryString(new Uri(authorized.Location!).Query)["code"]!;
		PageResponse issued = await game.Post("/oauth/token", new()
		{
			["grant_type"] = "authorization_code", ["code"] = code, ["redirect_uri"] = "http://127.0.0.1/callback",
			["client_id"] = "genonline_zerohour", ["code_verifier"] = Verifier
		});
		Assert.Equal(200, issued.Status);
		JsonElement tokens = JsonDocument.Parse(issued.Body).RootElement;
		return (tokens.GetProperty("access_token").GetString()!, tokens.GetProperty("refresh_token").GetString()!);
	}

	private static async Task<PageResponse> Refresh(AdminBrowser game, string refresh)
		=> await game.Post("/oauth/token", new() { ["grant_type"] = "refresh_token", ["refresh_token"] = refresh, ["client_id"] = "genonline_zerohour" });

	private static async Task<int> RegisterSession(AdminBrowser game, string access)
		=> (await game.SendJson("POST", "/api/v1/auth/session", new { exe_crc = 1u, ini_crc = 2u, anticheat_id = 3 }, new Dictionary<string, string> { ["Authorization"] = "Bearer " + access })).Status;

	private static async Task FreshGameSignIn(ProbeDatabase probe, string email, string password)
	{
		var game = new AdminBrowser(probe, s_gameClient);
		PageResponse page = await game.Get("/users/sign_in");
		Assert.Equal(200, page.Status);
		PageResponse signedIn = await game.Post("/users/sign_in", new() { ["__go_consent_token"] = AccountsBrowser.Token(page), ["email"] = email, ["password"] = password });
		Assert.Equal(302, signedIn.Status);
		(_, string refresh) = await GameSignIn(game);
		Assert.Equal(200, (await Refresh(game, refresh)).Status);
	}

	private static async Task AssertGameSignInEnded(AdminBrowser game, string access, string refresh)
	{
		PageResponse refused = await Refresh(game, refresh);
		Assert.Equal(400, refused.Status);
		Assert.Equal("invalid_grant", JsonDocument.Parse(refused.Body).RootElement.GetProperty("error").GetString());
		Assert.Equal(401, await RegisterSession(game, access));
		// signed out is signed out: the still unexpired access token stops everywhere at once, not only at the registration
		// without the browser's sign-in cookie, so only the access token speaks
		Assert.Equal(401, (await game.Get("/api/v1/me", new Dictionary<string, string> { ["Authorization"] = "Bearer " + access, ["Cookie"] = "" })).Status);
	}

	private static async Task<(string Access, string Refresh)> SignedInGame(AdminBrowser game)
	{
		(string access, string refresh) = await GameSignIn(game);
		PageResponse refreshed = await Refresh(game, refresh);
		Assert.Equal(200, refreshed.Status);
		refresh = JsonDocument.Parse(refreshed.Body).RootElement.GetProperty("refresh_token").GetString()!;
		// the access token authenticates here, so a later 401 at the session registration is the revoked sign-in
		Assert.Equal(200, (await game.Get("/api/v1/me", new Dictionary<string, string> { ["Authorization"] = "Bearer " + access })).Status);
		return (access, refresh);
	}

	private static int RevokedIn(AuditLogEntry entry) => JsonDocument.Parse(entry.Details!).RootElement.GetProperty("sign_ins_revoked").GetInt32();

	// accounts.support is for staff on the staff pages: a partner service account never holds it, whoever grants it
	private static async Task<(string Secret, long ID)> PersonalToken(ProbeDatabase probe, long ownerID, string scope)
	{
		string secret = ApiTokenFormat.Generate();
		await using AppDbContext db = probe.Open();
		var token = new ApiToken
		{
			OwnerUserID = ownerID,
			Name = "personal-" + Guid.NewGuid().ToString("N").Substring(0, 8),
			TokenHash = ApiTokenFormat.Hash(secret),
			TokenLast4 = ApiTokenFormat.Last4(secret),
			Scopes = scope,
			CreatedBy = "user:" + ownerID,
			CreatedAt = DateTime.UtcNow,
			ExpiresAt = DateTime.UtcNow.AddDays(1)
		};
		db.ApiTokens.Add(token);
		await db.SaveChangesAsync();
		await ApiTokenCache.Reload(db);
		return (secret, token.ID);
	}

	private static async Task<int> UseToken(ProbeDatabase probe, string secret)
		=> (await new AdminBrowser(probe).Get("/api/v1/personal_access_tokens/self", new Dictionary<string, string> { ["Authorization"] = "Bearer " + secret })).Status;

	private static int TokensRevokedIn(AuditLogEntry entry) => JsonDocument.Parse(entry.Details!).RootElement.GetProperty("tokens_revoked").GetInt32();

	// a taken-over staff account loses its personal tokens with a reset, from the staff pages and from the CLI alike
	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task AResetRevokesThePersonalTokensOfTheAccount(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		(AdminBrowser admin, _) = await StaffAsync(probe, "Chief", StaffRoles.Admin);
		(_, User warden) = await StaffAsync(probe, "Warden", StaffRoles.Moderator);
		(_, User keeper) = await StaffAsync(probe, "Keeper", StaffRoles.Moderator);
		await ReloadRoles(probe);
		(string wardenToken, long wardenTokenID) = await PersonalToken(probe, warden.ID, Permissions.ModerationKick);
		(string keeperToken, long keeperTokenID) = await PersonalToken(probe, keeper.ID, Permissions.ModerationKick);
		Assert.Equal(200, await UseToken(probe, wardenToken));
		Assert.Equal(200, await UseToken(probe, keeperToken));

		Assert.Equal(200, (await admin.SendJson("POST", $"/api/v1/players/{warden.ID}/password_reset", new { reason = "taken over" }, await admin.Csrf())).Status);
		Assert.Equal(401, await UseToken(probe, wardenToken));
		Assert.Equal(200, await UseToken(probe, keeperToken));

		await using (AppDbContext db = probe.Open())
		{
			await UserResets.ResetTwoFactor(db, keeper.ID, "cli:operator", "taken over");
		}
		Assert.Equal(401, await UseToken(probe, keeperToken));

		await using AppDbContext check = probe.Open();
		Assert.All(await check.ApiTokens.Where(t => t.ID == wardenTokenID || t.ID == keeperTokenID).ToListAsync(), t => Assert.NotNull(t.RevokedAt));
		Assert.Equal(1, TokensRevokedIn(Assert.Single(await AuditOf(probe, "password_reset"))));
		Assert.Equal(1, TokensRevokedIn(Assert.Single(await AuditOf(probe, "two_factor_reset"))));
		Assert.Equal(2, (await AuditOf(probe, "token_revoke")).Count(e => e.TargetID == wardenTokenID || e.TargetID == keeperTokenID));
	}

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task OnlyAnAdminActsOnALowerRank(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		(AdminBrowser admin, User chief) = await StaffAsync(probe, "Chief", StaffRoles.Admin);
		(AdminBrowser moderator, User warden) = await StaffAsync(probe, "Warden", StaffRoles.Moderator);
		(_, User peer) = await StaffAsync(probe, "Peer", StaffRoles.Admin);
		(_, User boss) = await StaffAsync(probe, "Boss", StaffRoles.Owner);
		User plain = await PlayerAsync(probe, "Plain");
		await TurnOnTwoFactor(probe, plain.ID);
		await ReloadRoles(probe);

		foreach (string action in new[] { "password_reset", "two_factor_reset" })
		{
			PageResponse notPermitted = await moderator.SendJson("POST", $"/api/v1/players/{plain.ID}/{action}", new { reason = "x" }, await moderator.Csrf());
			Assert.Equal(403, notPermitted.Status);
			Assert.Equal("forbidden", AdminArea.ErrorOf(notPermitted));

			PageResponse self = await admin.SendJson("POST", $"/api/v1/players/{chief.ID}/{action}", new { reason = "x" }, await admin.Csrf());
			Assert.Equal(400, self.Status);
			Assert.Equal("invalid_argument", AdminArea.ErrorOf(self));

			foreach (long senior in new[] { peer.ID, boss.ID })
			{
				PageResponse outranked = await admin.SendJson("POST", $"/api/v1/players/{senior}/{action}", new { reason = "x" }, await admin.Csrf());
				Assert.Equal(403, outranked.Status);
				Assert.Equal("moderation.target_outranks", AdminArea.ErrorOf(outranked));
			}

			PageResponse token = await new AdminBrowser(probe).SendJson("POST", $"/api/v1/players/{plain.ID}/{action}", new { reason = "x" }, await ServiceToken(probe, "accounts.support"));
			Assert.Equal(403, token.Status);
		}

		Assert.Equal(200, (await admin.SendJson("POST", $"/api/v1/players/{warden.ID}/password_reset", new { reason = "locked out" }, await admin.Csrf())).Status);

		User stored = await Stored(probe, plain.ID);
		Assert.True(stored.TwoFactorEnabled);
		Assert.Equal(plain.PasswordHash, stored.PasswordHash);
		Assert.Single(await AuditOf(probe, "password_reset"));
		Assert.Empty(await AuditOf(probe, "two_factor_reset"));
	}
}
