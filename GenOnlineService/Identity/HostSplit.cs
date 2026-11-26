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

namespace GenOnlineService.Identity
{
	[Flags]
	public enum SiteHosts
	{
		None = 0,
		// the game, the launcher and partners: bearer tokens, no cookies, no pages
		Api = 1,
		// sign-in, sign-up and the OAuth endpoints, and the token issuer
		Login = 2,
		// the pages of players and staff, and the /api/v1 routes those pages call with the sign-in cookie
		Portal = 4,
		All = Api | Login | Portal
	}

	// Hosts:api, Hosts:login and Hosts:portal, where unset or one name three times is one host serving everything
	public sealed class HostSettings
	{
		public static readonly HostSettings Single = new();

		public string Api { get; private init; } = String.Empty;
		public string Login { get; private init; } = String.Empty;
		public string Portal { get; private init; } = String.Empty;
		public bool Split { get; private init; }

		private string Scheme { get; init; } = Uri.UriSchemeHttps;

		public string LoginOrigin => Scheme + "://" + Login;
		public string PortalOrigin => Scheme + "://" + Portal;

		public static HostSettings Read(IConfiguration config, string? issuer)
		{
			IConfigurationSection section = config.GetSection("Hosts");
			string[] names = new[] { "api", "login", "portal" }.Select(key => (section.GetValue<string>(key) ?? String.Empty).Trim()).ToArray();
			if (names.All(n => n.Length == 0) || names.Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1 && names[0].Length > 0)
			{
				return Single;
			}

			if (names.Any(n => n.Length == 0 || n.Contains('/')))
			{
				throw new InvalidOperationException("Hosts:api, Hosts:login and Hosts:portal are set together, each a host name without a scheme (api.example.org), or all left out.");
			}

			// the login host issues the tokens, and its scheme is every host's
			if (!Uri.TryCreate(issuer, UriKind.Absolute, out Uri? issuerUrl) || !String.Equals(issuerUrl.Authority, names[1], StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidOperationException($"Accounts:issuer must be the login host's URL (https://{names[1]}/) when Hosts names separate hosts.");
			}

			return new HostSettings { Api = names[0], Login = names[1], Portal = names[2], Split = true, Scheme = issuerUrl.Scheme };
		}

		// A name that is none of the three (the pod's address, a cluster name) gets what the API host gets
		public SiteHosts Of(HttpRequest request)
		{
			if (!Split)
			{
				return SiteHosts.All;
			}
			string host = request.Host.Value ?? String.Empty;
			SiteHosts hosts = (Same(host, Login) ? SiteHosts.Login : SiteHosts.None) | (Same(host, Portal) ? SiteHosts.Portal : SiteHosts.None);
			return hosts == SiteHosts.None || Same(host, Api) ? hosts | SiteHosts.Api : hosts;
		}

		public bool IsPortal(HttpRequest request) => Of(request).HasFlag(SiteHosts.Portal);

		private static bool Same(string a, string b) => String.Equals(a, b, StringComparison.OrdinalIgnoreCase);
	}

	public static class HostSplit
	{
		// The one table of which host serves what, where anything not named belongs to the API host
		public static SiteHosts HostsOf(PathString path, Endpoint? endpoint)
		{
			if (path.StartsWithSegments("/-"))
			{
				return SiteHosts.All;
			}
			if (path.StartsWithSegments("/api/v1"))
			{
				return endpoint?.Metadata.GetMetadata<AllowSignInCookieAttribute>() != null ? SiteHosts.Api | SiteHosts.Portal : SiteHosts.Api;
			}
			// both sign-in and the pages show these: the look, the legal pages, the language choice and two-factor setup
			if (path.StartsWithSegments("/portal") || path.StartsWithSegments("/_content") || path.StartsWithSegments("/legal")
				|| path.StartsWithSegments(PortalLook.LanguagePath) || path.StartsWithSegments("/profile/two_factor_auth"))
			{
				return SiteHosts.Login | SiteHosts.Portal;
			}
			if (path.StartsWithSegments("/users") || path.StartsWithSegments("/oauth") || path.StartsWithSegments("/.well-known/openid-configuration"))
			{
				return SiteHosts.Login;
			}
			if (path.StartsWithSegments("/profile") || path.StartsWithSegments("/admin") || path.StartsWithSegments("/session") || path.StartsWithSegments("/_framework"))
			{
				return SiteHosts.Portal;
			}
			return SiteHosts.Api;
		}

		// After routing (the cookie mark is on the endpoint) and before authentication, which answers the OAuth endpoints itself
		public static IApplicationBuilder UseHostSplit(this IApplicationBuilder app, HostSettings hosts)
		{
			return !hosts.Split ? app : app.Use((context, next) =>
			{
				SiteHosts asked = hosts.Of(context.Request);
				SiteHosts serving = HostsOf(context.Request.Path, context.GetEndpoint());
				if ((asked & serving) != SiteHosts.None)
				{
					return next(context);
				}

				// a page of login asked on the portal, or the other way round, is one redirect away, and the API host sends nobody anywhere
				string? origin = (asked, serving) switch
				{
					(SiteHosts.Login, SiteHosts.Portal) => hosts.PortalOrigin,
					(SiteHosts.Portal, SiteHosts.Login) => hosts.LoginOrigin,
					_ => null
				};
				if (origin != null && HttpMethods.IsGet(context.Request.Method))
				{
					context.Response.Redirect(origin + context.Request.PathBase + context.Request.Path + context.Request.QueryString);
					return Task.CompletedTask;
				}

				context.Response.StatusCode = StatusCodes.Status404NotFound;
				return Task.CompletedTask;
			});
		}
	}
}
