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
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Xunit;

namespace GenOnlineService.Tests;

// SeedClients reloads the process-wide client registry, so these run with the other staff area classes
[Collection(AdminAreaCollection.Name)]
public class HostSplitTests
{
	private const string Api = "api.test";
	private const string Login = "login.test";
	private const string Portal = "portal.test";

	private static readonly (string Key, string Value)[] s_split =
	[
		("Hosts:api", Api), ("Hosts:login", Login), ("Hosts:portal", Portal), ("Accounts:issuer", "https://" + Login + "/")
	];

	// One browser on three host names, where the service's own OAuth client reaches the login host through the same pipeline
	private static AdminBrowser Browser(ProbeDatabase probe)
	{
		AdminBrowser? browser = null;
		browser = new AdminBrowser(probe, services => services.ConfigureAll<HttpClientFactoryOptions>(options =>
			options.HttpMessageHandlerBuilderActions.Add(builder => builder.AdditionalHandlers.Add(browser!.BackChannel()))), s_split)
		{
			Scheme = "https",
			HostName = Login
		};
		return browser;
	}

	private sealed record Walk(PageResponse Last, List<string> Trail, List<string> SetCookies);

	// Follows redirects as a browser does, across hosts, and notes every page asked for (host and path) and every cookie set
	private static async Task<Walk> Open(AdminBrowser browser, string host, string pathAndQuery, Dictionary<string, string>? post = null)
	{
		browser.HostName = host;
		var trail = new List<string>();
		var cookies = new List<string>();
		PageResponse page = post == null ? await browser.Get(pathAndQuery) : await browser.Post(pathAndQuery, post);
		cookies.AddRange(page.SetCookies);
		for (int hops = 0; page.Status == 302 && page.Location != null; hops++)
		{
			Assert.True(hops < 10, "redirect loop: " + String.Join(" -> ", trail));
			string next = page.Location;
			if (Uri.TryCreate(next, UriKind.Absolute, out Uri? absolute))
			{
				Assert.Equal("https", absolute.Scheme);
				browser.HostName = absolute.Authority;
				next = absolute.PathAndQuery;
			}
			trail.Add(browser.HostName + next.Split('?')[0]);
			page = await browser.Get(next);
			cookies.AddRange(page.SetCookies);
		}
		return new Walk(page, trail, cookies);
	}

	private static async Task<AdminBrowser> SignedUpStaff(ProbeDatabase probe)
	{
		AdminBrowser browser = Browser(probe);
		await browser.SeedClients();
		await browser.SignUp("Warden", "warden@example.org");
		await AdminArea.Grant(probe, "warden@example.org", StaffRoles.Moderator);
		return browser;
	}

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task PortalSignsInThroughLoginAndKeepsItsOwnCookie(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		AdminBrowser browser = await SignedUpStaff(probe);
		Assert.False(browser.Jar(Portal).ContainsKey("go_session"));

		Walk walk = await Open(browser, Portal, "/admin/cases");

		Assert.Equal(200, walk.Last.Status);
		Assert.Equal(new[] { Portal + "/session/sign_in", Login + "/oauth/authorize", Portal + "/session/callback", Portal + "/admin/cases" }, walk.Trail);

		// host-only cookies: the browser holds one per host and sends neither to the API host
		Assert.DoesNotContain(walk.SetCookies, c => c.Contains("domain=", StringComparison.OrdinalIgnoreCase));
		Assert.Contains(walk.SetCookies, c => c.StartsWith("go_session=", StringComparison.Ordinal));
		Assert.NotEqual(browser.Jar(Login)["go_session"], browser.Jar(Portal)["go_session"]);
		Assert.Empty(browser.Jar(Api));

		browser.HostName = Portal;
		Assert.Equal(200, (await browser.Get("/api/v1/players/search?query=ab")).Status);
		Assert.Equal(200, (await browser.Get("/profile")).Status);
	}

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task PortalSendsAStrangerToLoginsSignInPage(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		AdminBrowser browser = Browser(probe);
		await browser.SeedClients();

		Walk walk = await Open(browser, Portal, "/admin");

		Assert.Equal(200, walk.Last.Status);
		Assert.Equal(new[] { Portal + "/session/sign_in", Login + "/oauth/authorize", Login + "/users/sign_in" }, walk.Trail);
		Assert.Contains("name=\"password\"", walk.Last.Body);
	}

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task EachRouteAnswersOnItsOwnHostOnly(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		AdminBrowser browser = await SignedUpStaff(probe);
		await Open(browser, Portal, "/admin/cases");

		// the API host serves no page and reads no sign-in cookie, even one a browser was made to send
		browser.Jar(Api)["go_session"] = browser.Jar(Portal)["go_session"];
		browser.HostName = Api;
		Assert.Equal(404, (await browser.Get("/admin")).Status);
		Assert.Equal(404, (await browser.Get("/profile")).Status);
		Assert.Equal(404, (await browser.Get("/users/sign_in")).Status);
		Assert.Equal(404, (await browser.Get("/session/sign_in")).Status);
		Assert.Equal(404, (await browser.Post("/oauth/token", new())).Status);
		Assert.Equal(401, (await browser.Get("/api/v1/players/search?query=ab")).Status);
		Assert.Equal(401, (await browser.Post("/api/v1/auth/session", new())).Status);

		// a bearer-only route is not served to the pages' host
		browser.HostName = Portal;
		Assert.Equal(404, (await browser.Post("/api/v1/auth/session", new())).Status);
		Assert.Equal(404, (await browser.Post("/oauth/token", new())).Status);
		Assert.Equal(404, (await browser.Post("/users/sign_in", new())).Status);

		// login and portal send each other's pages across, and nothing else moves
		PageResponse toLogin = await browser.Get("/users/sign_in?ReturnUrl=%2Fadmin");
		Assert.Equal(302, toLogin.Status);
		Assert.Equal("https://" + Login + "/users/sign_in?ReturnUrl=%2Fadmin", toLogin.Location);
		browser.HostName = Login;
		PageResponse toPortal = await browser.Get("/admin/cases");
		Assert.Equal(302, toPortal.Status);
		Assert.Equal("https://" + Portal + "/admin/cases", toPortal.Location);
		Assert.Equal(404, (await browser.Get("/api/v1/admin/cases")).Status);
	}

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task PortalSignOutEndsBothSessions(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		AdminBrowser browser = await SignedUpStaff(probe);
		await Open(browser, Portal, "/admin/cases");

		Walk walk = await Open(browser, Portal, "/session/sign_out", post: new());

		Assert.Equal(200, walk.Last.Status);
		Assert.Equal(new[] { Login + "/users/sign_out" }, walk.Trail);
		Assert.False(browser.Jar(Portal).ContainsKey("go_session"));
		Assert.False(browser.Jar(Login).ContainsKey("go_session"));

		// a sign-out link without the portal's ticket ends nothing
		await browser.SignUp("Second", "second@example.org");
		browser.HostName = Login;
		Assert.Equal(200, (await browser.Get("/users/sign_out?ticket=forged")).Status);
		Assert.True(browser.Jar(Login).ContainsKey("go_session"));
	}

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task OneHostNameKeepsTheSignInPageWhereItIs(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		var browser = new AdminBrowser(probe);
		Assert.False(browser.Settings.Hosts.Split);

		PageResponse admin = await browser.Get("/admin/cases");
		Assert.Equal(302, admin.Status);
		Assert.Contains("/users/sign_in?ReturnUrl=%2Fadmin%2Fcases", admin.Location);

		PageResponse signIn = await browser.Get("/session/sign_in?ReturnUrl=%2Fadmin");
		Assert.Equal(302, signIn.Status);
		Assert.Equal("/users/sign_in?ReturnUrl=%2Fadmin", signIn.Location);

		await browser.SignUp("Warden", "warden@example.org");
		Assert.Equal(200, (await browser.Post("/session/sign_out", new())).Status);
		Assert.False(browser.Jar(AdminBrowser.Host).ContainsKey("go_session"));
	}

	private static HostSettings Read(string? issuer, params (string Key, string Value)[] hosts)
	{
		Dictionary<string, string?> values = hosts.ToDictionary(h => h.Key, h => (string?)h.Value);
		return HostSettings.Read(new ConfigurationBuilder().AddInMemoryCollection(values).Build(), issuer);
	}

	[Fact]
	public void SettingsAreAllOrNothing()
	{
		Assert.False(Read(null).Split);
		Assert.False(Read("https://one.test/", ("Hosts:api", "one.test"), ("Hosts:login", "one.test"), ("Hosts:portal", "one.test")).Split);
		Assert.True(Read("https://login.test/", s_split.Take(3).ToArray()).Split);

		Assert.Throws<InvalidOperationException>(() => Read("https://login.test/", ("Hosts:api", Api), ("Hosts:login", Login)));
		// the login host issues the tokens
		Assert.Throws<InvalidOperationException>(() => Read("https://api.test/", s_split.Take(3).ToArray()));
		Assert.Throws<InvalidOperationException>(() => Read(null, s_split.Take(3).ToArray()));
	}

	[Theory]
	[InlineData("/-/readiness", SiteHosts.Api | SiteHosts.Login | SiteHosts.Portal)]
	[InlineData("/realtime", SiteHosts.Api)]
	[InlineData("/api/v1/lobbies", SiteHosts.Api)]
	[InlineData("/env/prod/contract/1/VersionCheck", SiteHosts.Api)]
	[InlineData("/.well-known/jwks.json", SiteHosts.Api)]
	[InlineData("/.well-known/openid-configuration", SiteHosts.Login)]
	[InlineData("/oauth/token", SiteHosts.Login)]
	[InlineData("/users/sign_up", SiteHosts.Login)]
	[InlineData("/users/language", SiteHosts.Login | SiteHosts.Portal)]
	[InlineData("/profile/two_factor_auth", SiteHosts.Login | SiteHosts.Portal)]
	[InlineData("/legal/terms", SiteHosts.Login | SiteHosts.Portal)]
	[InlineData("/portal/theme.css", SiteHosts.Login | SiteHosts.Portal)]
	[InlineData("/_content/Bit.Blow/x.js", SiteHosts.Login | SiteHosts.Portal)]
	[InlineData("/profile/friends", SiteHosts.Portal)]
	[InlineData("/admin", SiteHosts.Portal)]
	[InlineData("/_framework/blazor.web.js", SiteHosts.Portal)]
	[InlineData("/session/callback", SiteHosts.Portal)]
	public void EveryPathHasItsHosts(string path, SiteHosts expected)
	{
		Assert.Equal(expected, HostSplit.HostsOf(new PathString(path), null));
	}
}
