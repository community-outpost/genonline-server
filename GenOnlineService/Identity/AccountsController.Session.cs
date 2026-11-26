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

using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Client.AspNetCore;
using System.Globalization;
using System.Security.Cryptography;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace GenOnlineService.Identity
{
	// The portal host's own session: it has no sign-in page and asks the login host, as any OAuth client does
	public static class PortalSession
	{
		public const string ClientID = "portal";
		// the name of the sign-in flow at this service's OAuth client, which no upstream provider may carry
		public const string Provider = "go_portal";

		public const string SignInPath = "/session/sign_in";
		public const string CallbackPath = "/session/callback";
		public const string SignOutPath = "/session/sign_out";

		public static string RedirectUri(HostSettings hosts) => hosts.PortalOrigin + CallbackPath;

		public static OAuthClientSettings Client(HostSettings hosts) => new()
		{
			ClientID = ClientID,
			DisplayName = "Generals Online",
			Kind = ClientKinds.App,
			RedirectUris = [RedirectUri(hosts)]
		};
	}

	public sealed partial class AccountsController
	{
		private const string SignOutTicketField = "ticket";

		// one process serves both hosts, so the portal can vouch for a sign-out to the login host without an ID token to keep
		private ITimeLimitedDataProtector SignOutTickets => HttpContext.RequestServices.GetRequiredService<IDataProtectionProvider>().CreateProtector("go_session_sign_out").ToTimeLimitedDataProtector();

		// Also on one host, so the pages link here whatever the hosts are
		[HttpGet("~" + PortalSession.SignInPath)]
		public IActionResult SessionSignIn([FromQuery(Name = ReturnUrlField)] string? returnUrl)
		{
			string target = SafeReturnUrl(returnUrl);
			if (!m_settings.Hosts.Split)
			{
				return LocalRedirect($"/users/sign_in?{ReturnUrlField}={Uri.EscapeDataString(target)}");
			}

			var properties = new AuthenticationProperties(new Dictionary<string, string?>
			{
				[OpenIddictClientAspNetCoreConstants.Properties.ProviderName] = PortalSession.Provider
			})
			{
				RedirectUri = target
			};
			return Challenge(properties, OpenIddictClientAspNetCoreDefaults.AuthenticationScheme);
		}

		[AcceptVerbs("GET", "POST", Route = "~" + PortalSession.CallbackPath)]
		[IgnoreAntiforgeryToken]
		public async Task<IActionResult> SessionCallback()
		{
			if (!m_settings.Hosts.Split)
			{
				return NotFound();
			}

			AuthenticateResult result = await HttpContext.AuthenticateAsync(OpenIddictClientAspNetCoreDefaults.AuthenticationScheme);
			// only the flow this host started, since an upstream provider's subject is not a GenOnline user id
			if (!result.Succeeded || result.Principal == null
				|| result.Properties?.Items.TryGetValue(OpenIddictClientAspNetCoreConstants.Properties.ProviderName, out string? started) != true || started != PortalSession.Provider)
			{
				s_log.LogWarning("Portal sign-in failed: {Error}", result.Failure?.Message);
				return DeadEnd(401, "Upstream.Failed.Title", "Upstream.Failed.Body");
			}

			// the login host already refused blocked and deactivated accounts and saw to two-factor, the name and the agreements
			User? user = await m_users.FindByIdAsync(result.Principal.GetClaim(Claims.Subject) ?? String.Empty);
			if (user == null || user.State != UserStates.Active)
			{
				return DeadEnd(403, "Refused.Title", "Refused.Deactivated", "user-x");
			}

			await m_signIn.SignInAsync(user, isPersistent: false, authenticationMethod: PortalSession.Provider);
			return LocalRedirect(SafeReturnUrl(result.Properties.RedirectUri));
		}

		[HttpPost("~" + PortalSession.SignOutPath)]
		public async Task<IActionResult> SessionSignOut()
		{
			User? user = await CookieUser();
			await m_signIn.SignOutAsync();
			if (!m_settings.Hosts.Split || user == null)
			{
				return DeadEnd(200, "SignedOut.Title", "SignedOut.Body", "log-out");
			}

			string ticket = SignOutTickets.Protect(user.ID.ToString(CultureInfo.InvariantCulture), TimeSpan.FromMinutes(1));
			return Redirect($"{m_settings.Hosts.LoginOrigin}/users/sign_out?{SignOutTicketField}={Uri.EscapeDataString(ticket)}");
		}

		// The second half of a portal sign-out, which ends nothing without the portal's ticket for this very account so a link never signs anyone out
		[HttpGet("~/users/sign_out")]
		public async Task<IActionResult> SignOutFromPortal([FromQuery(Name = SignOutTicketField)] string? ticket)
		{
			string? vouched = null;
			try
			{
				vouched = SignOutTickets.Unprotect(ticket ?? String.Empty);
			}
			catch (CryptographicException)
			{
			}

			User? user = await CookieUser();
			if (vouched != null && user != null && vouched == user.ID.ToString(CultureInfo.InvariantCulture))
			{
				await m_signIn.SignOutAsync();
			}
			return DeadEnd(200, "SignedOut.Title", "SignedOut.Body", "log-out");
		}
	}
}
