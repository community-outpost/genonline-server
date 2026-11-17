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

using BlazorBlueprint.Components;
using GenOnlineService.Api;
using GenOnlineService.Identity;
using GenOnlineService.Portal.Client;
using GenOnlineService.Realtime;
using GenOnlineService.Rest;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Security.Claims;
using System.Text.Json;

namespace GenOnlineService.Portal
{
	public sealed record PortalHostData(string Session, string CsrfHeader, string CsrfToken);

	// nothing here serves players; the sign-in pages and /profile belong to AccountsController
	public static class PortalSetup
	{
		private const string HostDataItem = "go_portal_host";

		public static void AddPortal(this IServiceCollection services)
		{
			services.AddRazorComponents().AddInteractiveWebAssemblyComponents();
			services.AddBlazorBlueprintComponents();
		}

		public static void UsePortal(this IApplicationBuilder app)
		{
			app.Use(GatePages);
			app.UseAntiforgery();
		}

		// After MapStaticAssets, which serves the WebAssembly files (_framework) and the stylesheets
		public static void MapPortal(this IEndpointRouteBuilder app)
		{
			app.MapRazorComponents<PortalApp>()
				.AddInteractiveWebAssemblyRenderMode()
				.AddAdditionalAssemblies(typeof(Routes).Assembly);
		}

		public static Task<bool> IsStaffAsync(IAuthorizationService authorization, ClaimsPrincipal user) => StaffAccess.IsStaffAsync(authorization, user);

		public static PortalHostData? HostDataOf(HttpContext? context) => context?.Items[HostDataItem] as PortalHostData;

		private static async Task GatePages(HttpContext context, RequestDelegate next)
		{
			PathString path = context.Request.Path;
			if (!path.StartsWithSegments(PortalRoutes.AdminPages))
			{
				await next(context);
				return;
			}

			if (!await SignInAsync(context))
			{
				await context.ChallengeAsync(SignInScheme, new AuthenticationProperties { RedirectUri = context.Request.PathBase + path + context.Request.QueryString });
				return;
			}

			IAuthorizationService authorization = context.RequestServices.GetRequiredService<IAuthorizationService>();
			if (!await IsStaffAsync(authorization, context.User))
			{
				await WriteRefusal(context, StatusCodes.Status403Forbidden, "Staff only", "Your account holds no staff permission.");
				return;
			}

			if (await ApiAuthentication.MustSetUpTwoFactorAsync(context))
			{
				string back = context.Request.PathBase + path + context.Request.QueryString;
				context.Response.Redirect("/profile/two_factor_auth?ReturnUrl=" + Uri.EscapeDataString(back));
				return;
			}

			// the token is made before the response starts, so its cookie can still be set
			context.Items[HostDataItem] = await HostDataAsync(context);

			// never cache or frame a staff page, its head carries the antiforgery token
			context.Response.Headers.CacheControl = "no-store";
			context.Response.Headers["X-Frame-Options"] = "DENY";
			context.Response.Headers["Content-Security-Policy"] = "frame-ancestors 'none'";
			await next(context);
		}

		// The one place the staff area authenticates, for its pages and the /api/v1 routes they call; a later portal cookie replaces the scheme here only.
		public static readonly string SignInScheme = IdentityConstants.ApplicationScheme;

		private static async Task<bool> SignInAsync(HttpContext context)
		{
			AuthenticateResult result = await context.AuthenticateAsync(SignInScheme);
			if (!result.Succeeded || result.Principal == null || RealtimeClaims.UserID(result.Principal) <= 0)
			{
				return false;
			}
			context.User = result.Principal;
			return true;
		}

		private static async Task<PortalHostData> HostDataAsync(HttpContext context)
		{
			Int64 me = RealtimeClaims.UserID(context.User);
			(string Name, int Tag)? name;
			await using (AppDbContext db = await context.RequestServices.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync(context.RequestAborted))
			{
				name = await Database.DisplayNames.Get(db, me);
			}

			var session = new PortalSessionInfo
			{
				UserId = me,
				DisplayName = name?.Name ?? String.Empty,
				Tag = PlayerTags.Of(name?.Tag ?? 0),
				Roles = StaffRoles.RolesOf(me).Order(StringComparer.Ordinal).ToList(),
				Permissions = StaffRoles.PermissionsOf(me).Order(StringComparer.Ordinal).ToList(),
				Staff = true
			};
			AntiforgeryTokenSet tokens = context.RequestServices.GetRequiredService<IAntiforgery>().GetAndStoreTokens(context);
			return new PortalHostData(JsonSerializer.Serialize(session, PortalJson.Options), tokens.HeaderName ?? String.Empty, tokens.RequestToken ?? String.Empty);
		}

		private static async Task WriteRefusal(HttpContext context, int status, string title, string message)
		{
			context.Response.StatusCode = status;
			context.Response.ContentType = "text/html; charset=utf-8";
			context.Response.Headers.CacheControl = "no-store";
			string body = PortalLook.Text(WebUtility.HtmlEncode(message)) + PortalLook.Link(PortalRoutes.AccountPage, "Back to your account");
			await context.Response.WriteAsync(PortalLook.Document(title, body));
		}
	}
}
