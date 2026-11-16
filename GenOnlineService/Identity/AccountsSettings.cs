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

namespace GenOnlineService.Identity
{
	public sealed class OAuthClientSettings
	{
		public string ClientID { get; init; } = String.Empty;
		public string DisplayName { get; init; } = String.Empty;
		public string Kind { get; init; } = String.Empty;
		public string Game { get; init; } = ClientGames.None;
		public string? MinVersion { get; init; }
		public List<string> RedirectUris { get; init; } = new();
	}

	public enum ProviderKind
	{
		Oidc,
		Discord,
		Google,
		Steam,
		GameReplays
	}

	// The environment owns names, roles and bans; the provider only signs people in
	public sealed class UpstreamProviderSettings
	{
		public string Name { get; init; } = String.Empty;
		public ProviderKind Kind { get; init; } = ProviderKind.Oidc;
		public string BaseUrl { get; init; } = String.Empty;
		public string? ApplicationKey { get; init; }
		public string DisplayName { get; init; } = String.Empty;
		public bool Enabled { get; init; }
		public string Issuer { get; init; } = String.Empty;
		public string ClientID { get; init; } = String.Empty;
		public string ClientSecret { get; init; } = String.Empty;
		public bool AllowSignUp { get; init; } = true;
		public bool OnlyProvider { get; init; }
		public bool AutoLinkVerifiedEmail { get; init; }
		// Return URL registered at the provider when it is not this service's callback; it must redirect the browser, query intact, to CallbackPath here
		public string? RedirectUri { get; init; }

		public string Source => AuthoritySources.UpstreamPrefix + Name;
		public string CallbackPath => $"users/auth/{Name}/callback";
		public string ReturnUri => RedirectUri ?? CallbackPath;
		public string SteamHandlerPath => $"/users/auth/{Name}/openid";

		private static readonly Dictionary<string, ProviderKind> s_kinds = new(StringComparer.Ordinal)
		{
			["oidc"] = ProviderKind.Oidc,
			["discord"] = ProviderKind.Discord,
			["google"] = ProviderKind.Google,
			["steam"] = ProviderKind.Steam,
			["gamereplays"] = ProviderKind.GameReplays
		};

		internal static ProviderKind KindOf(string name, string? kind)
		{
			if (!String.IsNullOrEmpty(kind))
			{
				return s_kinds.TryGetValue(kind, out ProviderKind configured) ? configured
					: throw new InvalidOperationException($"Accounts:providers:{name}:kind \"{kind}\" is unknown. Use one of {String.Join(", ", s_kinds.Keys)}.");
			}
			return s_kinds.TryGetValue(name, out ProviderKind byName) ? byName : ProviderKind.Oidc;
		}

		internal void Validate()
		{
			string prefix = $"Accounts:providers:{Name}";
			switch (Kind)
			{
				case ProviderKind.Oidc:
					if (!Uri.TryCreate(Issuer, UriKind.Absolute, out _))
					{
						throw new InvalidOperationException($"{prefix}:issuer must be the provider's absolute issuer URL.");
					}
					break;
				case ProviderKind.Discord:
				case ProviderKind.Google:
				case ProviderKind.GameReplays:
					if (String.IsNullOrEmpty(ClientID) || String.IsNullOrEmpty(ClientSecret))
					{
						throw new InvalidOperationException($"{prefix} needs client_id and client_secret of the application registered at {Kind}.");
					}
					if (Kind == ProviderKind.GameReplays
						&& (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out Uri? url) || (url.Scheme != Uri.UriSchemeHttps && url.Scheme != Uri.UriSchemeHttp)))
					{
						throw new InvalidOperationException($"{prefix}:base_url must be the absolute http or https URL of the folder holding authorize.php, token.php and resource.php.");
					}
					break;
			}

			if (RedirectUri != null)
			{
				if (Kind == ProviderKind.Steam)
				{
					throw new InvalidOperationException($"{prefix}:redirect_uri is not supported for Steam, whose return URL is always this service's.");
				}
				if (!Uri.TryCreate(RedirectUri, UriKind.Absolute, out Uri? relay) || (relay.Scheme != Uri.UriSchemeHttps && relay.Scheme != Uri.UriSchemeHttp))
				{
					throw new InvalidOperationException($"{prefix}:redirect_uri must be the absolute http or https return URL registered at the provider.");
				}
			}
		}
	}

	public sealed class AccountsSettings
	{
		public const string GameScope = "game";
		public const string DefaultAudience = "genonline-api";

		public bool Enabled { get; init; } = true;

		// The issuer the application profile trusts when accounts runs elsewhere
		public string? Issuer { get; init; }
		public string Audience { get; init; } = DefaultAudience;
		public int AccessTokenMinutes { get; init; } = 15;
		public int RefreshTokenDays { get; init; } = 30;
		public int RefreshReuseLeewaySeconds { get; init; } = 30;
		public List<string> SigningCertificates { get; init; } = new();
		public List<string> EncryptionCertificates { get; init; } = new();
		public string? CertificatePassword { get; init; }

		// Development only: keys live in memory and every restart invalidates every token
		public bool EphemeralKeys { get; init; }
		public bool RequireHttps { get; init; } = true;
		public List<OAuthClientSettings> Clients { get; init; } = new();
		public List<UpstreamProviderSettings> Providers { get; init; } = new();

		public IEnumerable<UpstreamProviderSettings> EnabledProviders => Providers.Where(p => p.Enabled);

		// true when missing: https-only cookies and OpenIddict transport security; a local plain http run sets false.
		public static bool EnforceHttps(IConfiguration config) => config.GetSection("Core").GetValue<bool?>("enforce_https") ?? true;

		public static AccountsSettings Read(IConfiguration config)
		{
			IConfigurationSection section = config.GetSection("Accounts");

			var clients = section.GetSection("clients").GetChildren().Select(c => new OAuthClientSettings
			{
				ClientID = c.GetValue<string>("client_id") ?? String.Empty,
				DisplayName = c.GetValue<string>("display_name") ?? c.GetValue<string>("client_id") ?? String.Empty,
				Kind = c.GetValue<string>("kind") ?? String.Empty,
				Game = c.GetValue<string>("game") ?? ClientGames.None,
				MinVersion = c.GetValue<string>("min_version"),
				RedirectUris = c.GetSection("redirect_uris").Get<List<string>>() ?? new()
			}).Where(c => c.ClientID.Length > 0).ToList();

			var providers = section.GetSection("providers").GetChildren().Select(p => new UpstreamProviderSettings
			{
				Name = p.Key,
				Kind = UpstreamProviderSettings.KindOf(p.Key, p.GetValue<string>("kind")),
				BaseUrl = (p.GetValue<string>("base_url") ?? String.Empty).TrimEnd('/'),
				ApplicationKey = String.IsNullOrEmpty(p.GetValue<string>("application_key")) ? null : p.GetValue<string>("application_key"),
				DisplayName = p.GetValue<string>("display_name") ?? p.Key,
				Enabled = p.GetValue<bool>("enabled"),
				Issuer = p.GetValue<string>("issuer") ?? String.Empty,
				ClientID = p.GetValue<string>("client_id") ?? String.Empty,
				ClientSecret = p.GetValue<string>("client_secret") ?? String.Empty,
				AllowSignUp = p.GetValue<bool?>("allow_sign_up") ?? true,
				OnlyProvider = p.GetValue<bool>("only_provider"),
				AutoLinkVerifiedEmail = p.GetValue<bool>("auto_link_verified_email"),
				RedirectUri = String.IsNullOrEmpty(p.GetValue<string>("redirect_uri")) ? null : p.GetValue<string>("redirect_uri")
			}).ToList();

			foreach (UpstreamProviderSettings provider in providers.Where(p => p.Enabled))
			{
				provider.Validate();
			}

			return new AccountsSettings
			{
				Enabled = section.GetValue<bool?>("enabled") ?? true,
				Issuer = section.GetValue<string>("issuer"),
				Audience = section.GetValue<string>("audience") ?? DefaultAudience,
				AccessTokenMinutes = section.GetValue<int?>("access_token_minutes") ?? 15,
				RefreshTokenDays = section.GetValue<int?>("refresh_token_days") ?? 30,
				RefreshReuseLeewaySeconds = section.GetValue<int?>("refresh_reuse_leeway_seconds") ?? 30,
				SigningCertificates = section.GetSection("signing_certificates").Get<List<string>>() ?? new(),
				EncryptionCertificates = section.GetSection("encryption_certificates").Get<List<string>>() ?? new(),
				CertificatePassword = section.GetValue<string>("certificate_password"),
				EphemeralKeys = section.GetValue<bool>("ephemeral_keys"),
				RequireHttps = EnforceHttps(config),
				Clients = clients,
				Providers = providers
			};
		}
	}
}
