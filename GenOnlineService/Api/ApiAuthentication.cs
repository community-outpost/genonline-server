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
using GenOnlineService.Realtime;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace GenOnlineService.Api
{
	// Admits the website sign-in cookie on an /api/v1 route; every other route stays bearer-only
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false)]
	public sealed class AllowSignInCookieAttribute : Attribute
	{
	}

	public static class ApiAuthentication
	{
		public const string SelectorScheme = "go_bearer";
		public const string SignInCookieScheme = "go_cookie";

		public const string GameSessionOrSignInCookie = "game_session_or_sign_in_cookie";

		public const string CsrfError = "csrf";

		public const string TwoFactorRequired = PasswordAccounts.ErrorTwoFactorRequired;

		public static void AddApiAuthentication(this IServiceCollection services, bool signInCookie)
		{
			AuthenticationBuilder authentication = services.AddAuthentication(options =>
			{
				options.DefaultScheme = SelectorScheme;
			})
			.AddPolicyScheme(SelectorScheme, "Bearer", options =>
			{
				options.ForwardDefaultSelector = context => ApiTokenHandler.HasApiToken(context.Request)
					? ApiTokenHandler.SchemeName
					: signInCookie && AdmitsSignInCookie(context) ? SignInCookieScheme : SessionTokenHandler.SchemeName;
			})
			.AddScheme<AuthenticationSchemeOptions, ApiTokenHandler>(ApiTokenHandler.SchemeName, null)
			.AddScheme<AuthenticationSchemeOptions, SessionTokenHandler>(SessionTokenHandler.SchemeName, null);
			if (signInCookie)
			{
				authentication.AddScheme<AuthenticationSchemeOptions, SignInCookieHandler>(SignInCookieScheme, null);
			}

			services.Configure<AuthorizationOptions>(options =>
			{
				options.AddPolicy(GameSessionOrSignInCookie, policy => policy.RequireAssertion(context =>
					context.User.IsInRole("GameClient")
					|| context.User.IsInRole("ChatClient")
					|| context.User.IsInRole("GameLauncher")
					|| IsSignInCookie(context.User)));
			});

			services.AddSingleton<IAuthorizationMiddlewareResultHandler, ApiAuthorizationResultHandler>();
		}

		// Must run right after UseAuthentication
		public static IApplicationBuilder UseSignInCookieRules(this IApplicationBuilder app)
		{
			return app.Use(CheckCookieCall);
		}

		// Only without an Authorization header; any bearer value keeps the bearer path
		// With separate hosts this applies on the portal host only
		public static bool AdmitsSignInCookie(HttpContext context)
		{
			return String.IsNullOrEmpty(context.Request.Headers.Authorization.ToString()) && context.GetEndpoint()?.Metadata.GetMetadata<AllowSignInCookieAttribute>() != null
				&& (context.RequestServices.GetService<AccountsSettings>()?.Hosts ?? HostSettings.Single).IsPortal(context.Request);
		}

		// Marker added by SignInCookieHandler; no token scheme produces it
		public static bool IsSignInCookie(ClaimsPrincipal user)
		{
			return user.Identities.Any(identity => identity.IsAuthenticated && identity.AuthenticationType == SignInCookieScheme);
		}

		// Identity rebuilds the cookie every minute, so its claims are no evidence: read the account
		public static async Task<bool> MustSetUpTwoFactorAsync(HttpContext context)
		{
			User? user = await context.RequestServices.GetRequiredService<UserManager<User>>().GetUserAsync(context.User);
			return user == null || await context.RequestServices.GetRequiredService<PasswordAccounts>().MustSetUpTwoFactor(user);
		}

		private static async Task CheckCookieCall(HttpContext context, RequestDelegate next)
		{
			if (context.GetEndpoint()?.Metadata.GetMetadata<AllowSignInCookieAttribute>() == null || !IsSignInCookie(context.User))
			{
				await next(context);
				return;
			}

			// staff data is never cached by the browser
			context.Response.Headers.CacheControl = "no-store";

			// another subdomain shares the site, so SameSite lets its cookie through: refuse other origins whatever CORS allows
			if (!IsOwnOrigin(context.Request))
			{
				await WriteAsync(context, StatusCodes.Status403Forbidden, Result.Fail(ErrorCodes.Forbidden));
				return;
			}

			if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method) && !HttpMethods.IsOptions(context.Request.Method))
			{
				try
				{
					await context.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context);
				}
				catch (AntiforgeryValidationException)
				{
					await WriteAsync(context, StatusCodes.Status400BadRequest, Result.Fail(CsrfError));
					return;
				}
			}

			// staff with a password set up two-factor first, as before any token is issued
			if (await MustSetUpTwoFactorAsync(context))
			{
				await WriteAsync(context, StatusCodes.Status403Forbidden, Result.Fail(TwoFactorRequired));
				return;
			}

			await next(context);
		}

		// No Origin header is a same-origin GET or a non-browser caller; otherwise it must be this host's own origin
		private static bool IsOwnOrigin(HttpRequest request)
		{
			string origin = request.Headers.Origin.ToString();
			return origin.Length == 0 || String.Equals(origin, request.Scheme + "://" + request.Host.Value, StringComparison.OrdinalIgnoreCase);
		}

		private static Task WriteAsync(HttpContext context, int status, Failure failure)
		{
			context.Response.StatusCode = status;
			return context.Response.WriteAsJsonAsync(ApiResults.BodyOf(failure), ApiResults.Json);
		}
	}

	public sealed class SignInCookieHandler : AuthenticationHandler<AuthenticationSchemeOptions>
	{
		public SignInCookieHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
			: base(options, logger, encoder)
		{
		}

		protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
		{
			AuthenticateResult cookie = await Context.AuthenticateAsync(Portal.PortalSetup.SignInScheme);
			if (!cookie.Succeeded || cookie.Principal == null || RealtimeClaims.UserID(cookie.Principal) <= 0)
			{
				return AuthenticateResult.NoResult();
			}

			// the sign-in identity stays first, so the antiforgery token of the host page (made from it alone) matches
			var principal = new ClaimsPrincipal(cookie.Principal.Identities);
			principal.AddIdentity(new ClaimsIdentity(ApiAuthentication.SignInCookieScheme));
			return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
		}

		// A missing or stale cookie gets the 401 body of a missing token, never the sign-in page redirect
		protected override Task HandleChallengeAsync(AuthenticationProperties properties)
		{
			return UniformAuthResponses.WriteUnauthorized(Context);
		}
	}
}
