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
using GenOnlineService.Names;
using GenOnlineService.OpenApi;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GenOnlineService.Api
{
	using GenOnlineService.Realtime;

	// Takes over accounts, so only the staff sign-in cookie reaches it: no game session, no token
	public sealed class AccountSupportController : ApiControllerBase
	{
		public const string NoPassword = "account.no_password";
		public const string NoTwoFactor = "account.no_two_factor";

		private readonly IDbContextFactory<AppDbContext> m_dbFactory;
		private readonly AccountsSettings m_accounts;

		public AccountSupportController(IDbContextFactory<AppDbContext> dbFactory, IAuthorizationService authorization, AccountsSettings accounts) : base(authorization)
		{
			m_dbFactory = dbFactory;
			m_accounts = accounts;
		}

		// Answered once, never stored or audited in the clear
		[AllowSignInCookie]
		[EndpointSummary("Resets the password of a player to a new random one as staff.")]
		[ProducesResponseType<PasswordResetResponse>(StatusCodes.Status200OK)]
		[ApiRequestBody(typeof(AccountSupportRequest))]
		[ApiPermission(Permissions.AccountsSupport)]
		[HttpPost("~/api/v1/players/{userID:long}/password_reset")]
		public async Task<IActionResult> ResetPassword(long userID)
		{
			(string? reason, Failure? refused) = await CheckAsync(userID);
			if (refused != null)
			{
				return ApiResults.Fail(refused.Value);
			}

			await using AppDbContext db = await m_dbFactory.CreateDbContextAsync();
			User? user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.ID == userID);
			if (user == null)
			{
				return ApiResults.Fail(Result.NotFound("user"));
			}
			// the CLI's own rule: without an email address the account signs in with a provider only
			if (user.Email == null)
			{
				return ApiResults.Fail(Result.Fail(NoPassword));
			}

			string password = await UserResets.ResetPassword(db, userID, null, m_accounts.Password.MinLength, SanctionActions.ApiActorOf(Me), reason, live: true);
			return ApiResults.Ok(new PasswordResetResponse { Password = password });
		}

		[AllowSignInCookie]
		[EndpointSummary("Turns off the two-factor sign-in of a player as staff.")]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
		[ApiRequestBody(typeof(AccountSupportRequest))]
		[ApiPermission(Permissions.AccountsSupport)]
		[HttpPost("~/api/v1/players/{userID:long}/two_factor_reset")]
		public async Task<IActionResult> ResetTwoFactor(long userID)
		{
			(string? reason, Failure? refused) = await CheckAsync(userID);
			if (refused != null)
			{
				return ApiResults.Fail(refused.Value);
			}

			await using AppDbContext db = await m_dbFactory.CreateDbContextAsync();
			User? user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.ID == userID);
			if (user == null)
			{
				return ApiResults.Fail(Result.NotFound("user"));
			}
			if (!user.TwoFactorEnabled)
			{
				return ApiResults.Fail(Result.Fail(NoTwoFactor));
			}

			await UserResets.ResetTwoFactor(db, userID, SanctionActions.ApiActorOf(Me), reason, live: true);
			return NoContent();
		}

		private async Task<(string? Reason, Failure? Refused)> CheckAsync(long userID)
		{
			if (await RequirePermissionAsync(Permissions.AccountsSupport) is Failure denied)
			{
				return (null, denied);
			}
			if (!ApiAuthentication.IsSignInCookie(User))
			{
				return (null, Result.Forbidden(Permissions.AccountsSupport));
			}

			(AccountSupportRequest? body, Failure? malformed) = await ReadBodyAsync<AccountSupportRequest>();
			if (malformed != null)
			{
				return (null, malformed);
			}
			string? reason = String.IsNullOrWhiteSpace(body?.Reason) ? null : body.Reason.Trim();
			if (reason == null)
			{
				return (null, Result.Invalid("reason"));
			}

			return (reason, PlayerNames.CheckStaff(Me, StaffRoles.Rank(Me), userID, StaffRoles.Rank(userID), reason));
		}
	}
}
