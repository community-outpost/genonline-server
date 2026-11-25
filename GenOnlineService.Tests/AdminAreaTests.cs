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

using GenOnlineService.Api;
using GenOnlineService.Identity;
using GenOnlineService.Portal;
using GenOnlineService.Rest;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace GenOnlineService.Tests;

// The rank probe reloads the process-wide StaffRoles cache and the bearer probe the process-wide API token cache,
// so the classes of this collection run alone, after the parallel tests
[CollectionDefinition(AdminAreaCollection.Name, DisableParallelization = true)]
public sealed class AdminAreaCollection
{
	public const string Name = "Admin area (process-wide roles)";
}

internal sealed class AdminBrowser
{
	public const string Host = "accounts.test";

	private readonly RequestDelegate m_pipeline;
	private readonly IServiceProvider m_services;
	private readonly Dictionary<string, string> m_cookies = new();

	public AdminBrowser(ProbeDatabase probe, params (string Key, string Value)[] extra) : this(probe, (Action<IServiceCollection>?)null, extra)
	{
	}

	public AdminBrowser(ProbeDatabase probe, Action<IServiceCollection>? configure, params (string Key, string Value)[] extra)
	{
		Dictionary<string, string?> values = new(PasswordPagesTests.Enabled(extra)) { ["Accounts:ephemeral_keys"] = "true", ["Core:enforce_https"] = "false" };
		IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
		AccountsSettings accounts = AccountsSettings.Read(configuration);
		Settings = accounts;

		ServiceCollection services = new();
		services.AddLogging();
		services.AddSingleton(configuration);
		var diagnostics = new System.Diagnostics.DiagnosticListener(nameof(AdminAreaTests));
		services.AddSingleton(diagnostics);
		services.AddSingleton<System.Diagnostics.DiagnosticSource>(diagnostics);
		services.AddSingleton<IWebHostEnvironment>(new TestEnvironment());
		services.AddSingleton<IHostEnvironment>(sp => sp.GetRequiredService<IWebHostEnvironment>());
		services.AddSingleton<IDbContextFactory<AppDbContext>>(new ProbeFactory(probe));
		services.AddSingleton(accounts);
		services.AddApiAuthentication(signInCookie: true);
		services.AddGenOnlineIdentity(accounts, development: true);
		services.AddRouting();
		services.AddControllers().AddApplicationPart(typeof(AccountsController).Assembly);
		// the perm:<name> policies of the host; the handler maps the probe database's roles with the real role table,
		// as the process-wide role cache (StaffRoles) would mix up the user ids of parallel probe databases
		services.AddAuthorization();
		services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
		services.AddSingleton<IAuthorizationHandler, ProbeRolesHandler>();
		services.AddSingleton<IAuthorizationHandler, TokenPermissionHandler>();
		PortalSetup.AddPortal(services);
		configure?.Invoke(services);
		m_services = services.BuildServiceProvider();

		var app = new ApplicationBuilder(m_services);
		app.UseRouting();
		app.Use(RestSetup.StatusBodies);
		app.UseAuthentication();
		app.UseSignInCookieRules();
		app.UseAuthorization();
		app.UsePortal();
		app.UseEndpoints(endpoints =>
		{
			endpoints.MapControllers();
			endpoints.MapPortal();
		});
		m_pipeline = app.Build();
	}

	public AccountsSettings Settings { get; }

	public IPAddress? RemoteAddress { get; set; }

	public Task SeedClients() => AccountsSetup.SeedClients(m_services, Settings);

	public Task<PageResponse> Get(string path, Dictionary<string, string>? headers = null) => Send("GET", path, null, null, headers);

	public Task<PageResponse> Post(string path, Dictionary<string, string> form) => Send("POST", path, form, null, null);

	public Task<PageResponse> Patch(string path, object json, Dictionary<string, string>? headers = null) => Send("PATCH", path, null, json, headers);

	public Task<PageResponse> SendJson(string method, string path, object json, Dictionary<string, string>? headers = null) => Send(method, path, null, json, headers);

	private async Task<PageResponse> Send(string method, string pathAndQuery, Dictionary<string, string>? form, object? json, Dictionary<string, string>? headers)
	{
		using IServiceScope scope = m_services.CreateScope();
		var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
		scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = context;
		context.Request.Method = method;
		context.Request.Scheme = "http";
		context.Request.Host = new HostString(Host);
		context.Connection.RemoteIpAddress = RemoteAddress;
		int query = pathAndQuery.IndexOf('?');
		context.Request.Path = query < 0 ? pathAndQuery : pathAndQuery.Substring(0, query);
		context.Request.QueryString = query < 0 ? QueryString.Empty : new QueryString(pathAndQuery.Substring(query));
		if (m_cookies.Count > 0)
		{
			context.Request.Headers.Cookie = String.Join("; ", m_cookies.Select(c => $"{c.Key}={c.Value}"));
		}
		foreach ((string name, string value) in headers ?? new Dictionary<string, string>())
		{
			context.Request.Headers[name] = value;
		}
		byte[]? body = null;
		if (form != null)
		{
			body = Encoding.UTF8.GetBytes(String.Join("&", form.Select(f => $"{Uri.EscapeDataString(f.Key)}={Uri.EscapeDataString(f.Value)}")));
			context.Request.ContentType = "application/x-www-form-urlencoded";
		}
		else if (json != null)
		{
			body = JsonSerializer.SerializeToUtf8Bytes(json);
			context.Request.ContentType = "application/json";
		}
		if (body != null)
		{
			context.Request.ContentLength = body.Length;
			context.Request.Body = new MemoryStream(body);
		}
		var responseBody = new MemoryStream();
		context.Response.Body = responseBody;

		await m_pipeline(context);

		List<string> setCookies = context.Response.Headers.SetCookie.Select(c => c!).ToList();
		foreach (string cookie in setCookies)
		{
			string pair = cookie.Split(';')[0];
			int equals = pair.IndexOf('=');
			string name = pair.Substring(0, equals);
			string value = pair.Substring(equals + 1);
			if (value.Length == 0 || cookie.Contains("expires=Thu, 01 Jan 1970", StringComparison.OrdinalIgnoreCase))
			{
				m_cookies.Remove(name);
			}
			else
			{
				m_cookies[name] = value;
			}
		}

		return new PageResponse(context.Response.StatusCode, Encoding.UTF8.GetString(responseBody.ToArray()), context.Response.Headers.Location.ToString() is { Length: > 0 } l ? l : null, setCookies, context.Response.Headers.XFrameOptions.ToString()) { Headers = context.Response.Headers };
	}

	public async Task SignUp(string name, string email)
	{
		PageResponse page = await Get("/users/sign_up");
		Assert.Equal(200, page.Status);
		Dictionary<string, string> form = new()
		{
			["__go_consent_token"] = AccountsBrowser.Token(page),
			["display_name"] = name,
			["email"] = email,
			["password"] = "a long enough password"
		};
		if (page.Body.Contains("name=\"agree_terms\"", StringComparison.Ordinal))
		{
			form["agree_terms"] = "1";
		}
		PageResponse created = await Post("/users/sign_up", form);
		Assert.Equal(302, created.Status);
	}

	public async Task<Dictionary<string, string>> Csrf()
	{
		PageResponse page = await Get("/admin/cases");
		Assert.Equal(200, page.Status);
		return new Dictionary<string, string> { [AdminArea.Meta(page, "csrf-header")] = AdminArea.Meta(page, "csrf-token") };
	}

	private sealed class TestEnvironment : IWebHostEnvironment
	{
		public string ApplicationName { get; set; } = typeof(PortalSetup).Assembly.GetName().Name!;
		public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
		public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
		public string EnvironmentName { get; set; } = Environments.Development;
		public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
		public string WebRootPath { get; set; } = AppContext.BaseDirectory;
	}

	private sealed class ProbeRolesHandler : AuthorizationHandler<PermissionRequirement>
	{
		private readonly IDbContextFactory<AppDbContext> m_dbFactory;

		public ProbeRolesHandler(IDbContextFactory<AppDbContext> dbFactory)
		{
			m_dbFactory = dbFactory;
		}

		protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
		{
			if (context.User.Identities.Any(i => i.IsAuthenticated && i.AuthenticationType == TokenPrincipal.AuthenticationType)
				|| !Int64.TryParse(context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out Int64 userID))
			{
				return;
			}
			await using AppDbContext db = await m_dbFactory.CreateDbContextAsync();
			List<string> roles = await db.UserRoles.Where(r => r.UserID == userID).Select(r => r.Role).ToListAsync();
			if (StaffRoles.PermissionsFor(roles).Contains(requirement.Permission))
			{
				context.Succeed(requirement);
			}
		}
	}

	private sealed class TokenPermissionHandler : AuthorizationHandler<PermissionRequirement>
	{
		protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
		{
			if (ApiTokenPrincipals.HasPermission(context.User, requirement.Permission))
			{
				context.Succeed(requirement);
			}
			return Task.CompletedTask;
		}
	}
}

internal static class AdminArea
{
	public static async Task<User> Grant(ProbeDatabase probe, string email, string role, bool twoFactor = true)
	{
		User user = (await PasswordPagesTests.UserByEmail(probe, email))!;
		await using AppDbContext db = probe.Open();
		db.UserRoles.Add(new UserRole { UserID = user.ID, Role = role, Source = AuthoritySources.Local, GrantedAt = DateTime.UtcNow, GrantedBy = "test" });
		// staff password accounts keep two-factor sign-in; the page gate checks it on the account itself
		if (twoFactor)
		{
			await db.Users.Where(u => u.ID == user.ID).ExecuteUpdateAsync(set => set.SetProperty(u => u.TwoFactorEnabled, true));
		}
		await db.SaveChangesAsync();
		return user;
	}

	public static string ErrorOf(PageResponse response) => JsonDocument.Parse(response.Body).RootElement.GetProperty("error").GetString()!;

	public static string Meta(PageResponse page, string name)
	{
		System.Text.RegularExpressions.Match match = Regex.Match(page.Body, "<meta name=\"" + Regex.Escape(name) + "\" content=\"([^\"]*)\"");
		Assert.True(match.Success, "the page has no meta " + name + ": " + page.Body);
		return WebUtility.HtmlDecode(match.Groups[1].Value);
	}
}

[Collection(AdminAreaCollection.Name)]
public class AdminAreaTests
{
	private static readonly string[] s_staffReads = ["/api/v1/admin/overview", "/api/v1/admin/cases", "/api/v1/players/search?query=ab", "/api/v1/admin/cases?kind=modules"];

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task StaffWithoutTwoFactorSetItUpFirst(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		var moderator = new AdminBrowser(probe);
		await moderator.SignUp("Warden", "warden@example.org");
		await AdminArea.Grant(probe, "warden@example.org", StaffRoles.Moderator, twoFactor: false);

		PageResponse page = await moderator.Get("/admin/cases");
		Assert.Equal(302, page.Status);
		Assert.Equal("/profile/two_factor_auth?ReturnUrl=%2Fadmin%2Fcases", page.Location);

		PageResponse reports = await moderator.Get("/api/v1/admin/cases");
		Assert.Equal(403, reports.Status);
		Assert.Equal(ApiAuthentication.TwoFactorRequired, AdminArea.ErrorOf(reports));
	}

	// Uses the process-wide rank cache (StaffRoles.Reload), so it must not run in parallel with other probe tests
	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task ProbeRankRuleHoldsThroughTheCookie(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		var moderator = new AdminBrowser(probe);
		await moderator.SignUp("Warden", "warden@example.org");
		await AdminArea.Grant(probe, "warden@example.org", StaffRoles.Moderator);
		var admin = new AdminBrowser(probe);
		await admin.SignUp("Chief", "chief@example.org");
		User chief = await AdminArea.Grant(probe, "chief@example.org", StaffRoles.Admin);
		var player = new AdminBrowser(probe);
		await player.SignUp("Plain", "plain@example.org");
		User plain = (await PasswordPagesTests.UserByEmail(probe, "plain@example.org"))!;
		await using (AppDbContext db = probe.Open())
		{
			await StaffRoles.Reload(db);
		}

		Dictionary<string, string> csrf = await moderator.Csrf();
		PageResponse rename = await moderator.Patch($"/api/v1/players/{chief.ID}", new { name = "Renamed", reason = "probe" }, csrf);
		Assert.Equal(403, rename.Status);
		Assert.Equal("moderation.target_outranks", AdminArea.ErrorOf(rename));
		PageResponse mute = await moderator.SendJson("POST", $"/api/v1/players/{chief.ID}/sanctions", new { type = "mute", duration_s = 60, reason = "probe" }, csrf);
		Assert.Equal(403, mute.Status);
		Assert.Equal("moderation.target_outranks", AdminArea.ErrorOf(mute));
		Assert.Equal("Chief", (await PasswordPagesTests.UserByEmail(probe, "chief@example.org"))!.Username);

		Assert.Equal(204, (await moderator.Patch($"/api/v1/players/{plain.ID}", new { name = "Cleaned", reason = "probe" }, csrf)).Status);
		Assert.Equal("Cleaned", (await PasswordPagesTests.UserByEmail(probe, "plain@example.org"))!.Username);
	}

	// evidence for a sanction the caller may not give answers as if it did not exist, so its id leaks nothing
}
