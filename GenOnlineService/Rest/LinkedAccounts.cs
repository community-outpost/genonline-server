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

using GenOnlineService.Realtime;

namespace GenOnlineService.Rest
{
	public sealed record SignInToken(string Token, int ExpiresIn);

	public interface ILinkedAccountProvider
	{
		string Name { get; }

		Result<SignInToken> IssueSignInToken(Int64 userID, string displayName);

		Task<Failure?> LinkAsync(Int64 userID, EUserSessionType sessionType, string proof);
	}

	public static class LinkedAccounts
	{
		public static Failure? ValidateRequest(LinkAccountRequest? body)
		{
			return String.IsNullOrEmpty(body?.Proof) ? Result.Invalid("proof") : null;
		}

		public static Failure InvalidProof(string provider) => Result.Fail(RestCodes.LinkedAccountInvalidProof, new { Provider = provider });

		public static Failure Mismatch(string provider) => Result.Fail(RestCodes.LinkedAccountMismatch, new { Provider = provider });

		public static Failure InUse(string provider) => Result.Fail(RestCodes.LinkedAccountInUse, new { Provider = provider });

		public static Failure Locked(string provider) => Result.Fail(RestCodes.LinkedAccountLocked, new { Provider = provider });

		public static Failure Unavailable(string provider) => Result.Fail(RestCodes.ServiceUnavailable, new { What = "linked_account", Provider = provider });
	}

	// EOS: the sign-in token is the service's own EOS ID token, the proof is the Epic Connect ID token
	public sealed class EosLinkedAccountProvider : ILinkedAccountProvider
	{
		private readonly LobbyManager m_lobbyManager;
		private readonly Func<EosTokenIssuer.Settings?> m_tokenSettings;

		public EosLinkedAccountProvider(LobbyManager lobbyManager) : this(lobbyManager, EosTokenIssuer.GetSettings)
		{
		}

		public EosLinkedAccountProvider(LobbyManager lobbyManager, Func<EosTokenIssuer.Settings?> tokenSettings)
		{
			m_lobbyManager = lobbyManager;
			m_tokenSettings = tokenSettings;
		}

		public string Name => EosLink.Provider;

		public Result<SignInToken> IssueSignInToken(Int64 userID, string displayName)
		{
			EosTokenIssuer.Settings? settings = m_tokenSettings();
			if (settings == null)
			{
				return LinkedAccounts.Unavailable(Name);
			}

			string token = EosTokenIssuer.Issue(settings, userID, displayName, out _);
			return Result.Success(new SignInToken(token, (int)settings.Lifetime.TotalSeconds));
		}

		public Task<Failure?> LinkAsync(Int64 userID, EUserSessionType sessionType, string proof)
		{
			return EosLink.LinkAsync(m_lobbyManager, userID, sessionType, proof);
		}
	}
}
