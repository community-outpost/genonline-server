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

using Database;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace GenOnlineService
{
	public sealed record EpicSanction(string ReferenceId, string Action, DateTime PlacedAt, DateTime? ExpiresAt);

	// Reads the active sanctions of a linked Epic account with the Sanctions Web API
	public sealed class EpicSanctionsClient
	{
		public sealed record Settings(string ClientId, string ClientSecret, string DeploymentId);

		public const string TokenUrl = "https://api.epicgames.dev/auth/v1/oauth/token";
		public const string SanctionsUrl = "https://api.epicgames.dev/sanctions/v1/productUser/";

		public static readonly TimeSpan ResultLifetime = TimeSpan.FromMinutes(10);
		private static readonly TimeSpan s_tokenMargin = TimeSpan.FromSeconds(60);

		private static readonly ILogger s_log = AppLog.For(typeof(EpicSanctionsClient));

		private static readonly Lazy<HttpClient> s_httpClient = new Lazy<HttpClient>(() =>
		{
			var handler = new SocketsHttpHandler
			{
				PooledConnectionLifetime = TimeSpan.FromMinutes(5),
				PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2)
			};

			return new HttpClient(handler)
			{
				Timeout = TimeSpan.FromSeconds(10)
			};
		}, true);

		private readonly IDbContextFactory<AppDbContext>? m_dbFactory;
		private readonly HttpClient m_http;
		private readonly TimeProvider m_time;

		private readonly SemaphoreSlim m_tokenLock = new SemaphoreSlim(1, 1);
		private string? m_token;
		private DateTime m_tokenRenewAt = DateTime.MinValue;

		private readonly ConcurrentDictionary<string, (DateTime At, IReadOnlyList<EpicSanction> Sanctions)> m_results = new(StringComparer.Ordinal);

		// A failed call pauses every lookup for a while, so bad credentials or an Epic outage do not queue a call per sign-in
		public static readonly TimeSpan FailurePause = TimeSpan.FromMinutes(1);
		private DateTime m_pausedUntil = DateTime.MinValue;

		internal int CachedResultCount => m_results.Count;

		public Settings? Config { get; }

		public EpicSanctionsClient(IConfiguration config, IDbContextFactory<AppDbContext> dbFactory) : this(Load(config), dbFactory, s_httpClient.Value, TimeProvider.System)
		{
		}

		internal EpicSanctionsClient(Settings? settings, IDbContextFactory<AppDbContext>? dbFactory, HttpClient http, TimeProvider time)
		{
			Config = settings;
			m_dbFactory = dbFactory;
			m_http = http;
			m_time = time;
		}

		public static Settings? Load(IConfiguration? config)
		{
			string? clientId = config?.GetValue<string>("Epic:client_id");
			string? clientSecret = config?.GetValue<string>("Epic:client_secret");
			string? deploymentId = config?.GetValue<string>("Middleware:deployment_id");
			if (String.IsNullOrEmpty(clientId) || String.IsNullOrEmpty(clientSecret) || String.IsNullOrEmpty(deploymentId))
			{
				return null;
			}
			return new Settings(clientId, clientSecret, deploymentId);
		}

		// runs after sign-in and reports, so every failure ends here as a log line
		public async Task CheckAsync(Int64 userId, string productUserId)
		{
			try
			{
				IReadOnlyList<EpicSanction>? sanctions = await GetActiveAsync(productUserId);
				if (sanctions == null || sanctions.Count == 0 || m_dbFactory == null)
				{
					return;
				}
				await using AppDbContext db = await m_dbFactory.CreateDbContextAsync();
				await Database.AntiCheat.RecordEpicSanctions(db, userId, sanctions, m_time.GetUtcNow().UtcDateTime);
			}
			catch (Exception ex)
			{
				s_log.LogWarning(ex, "Epic sanctions check failed for user {UserId}", userId);
			}
		}

		// null when the check is off or Epic did not answer
		public async Task<IReadOnlyList<EpicSanction>?> GetActiveAsync(string productUserId, CancellationToken cancellationToken = default)
		{
			if (Config == null || String.IsNullOrEmpty(productUserId))
			{
				return null;
			}
			DateTime now = m_time.GetUtcNow().UtcDateTime;
			if (m_results.TryGetValue(productUserId, out var cached) && now - cached.At < ResultLifetime)
			{
				return cached.Sanctions;
			}
			if (now < m_pausedUntil)
			{
				return null;
			}

			try
			{
				string? token = await GetTokenAsync(cancellationToken);
				if (token == null)
				{
					m_pausedUntil = now + FailurePause;
					return null;
				}

				using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, SanctionsUrl + Uri.EscapeDataString(productUserId) + "/active");
				request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
				using HttpResponseMessage response = await m_http.SendAsync(request, cancellationToken);
				if (!response.IsSuccessStatusCode)
				{
					s_log.LogWarning("Epic sanctions lookup answered {Status}", (int)response.StatusCode);
					m_pausedUntil = now + FailurePause;
					return null;
				}

				IReadOnlyList<EpicSanction> sanctions = ParseSanctions(await response.Content.ReadAsStringAsync(cancellationToken));
				Prune(now);
				m_results[productUserId] = (now, sanctions);
				return sanctions;
			}
			catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or FormatException or InvalidOperationException or KeyNotFoundException or ArgumentOutOfRangeException)
			{
				s_log.LogWarning("Epic sanctions lookup failed: {Reason}", ex.GetType().Name);
				m_pausedUntil = now + FailurePause;
				return null;
			}
		}

		private void Prune(DateTime now)
		{
			foreach (var entry in m_results)
			{
				if (now - entry.Value.At >= ResultLifetime)
				{
					m_results.TryRemove(entry.Key, out _);
				}
			}
		}

		private async Task<string?> GetTokenAsync(CancellationToken cancellationToken)
		{
			if (m_token != null && m_time.GetUtcNow().UtcDateTime < m_tokenRenewAt)
			{
				return m_token;
			}

			await m_tokenLock.WaitAsync(cancellationToken);
			try
			{
				DateTime now = m_time.GetUtcNow().UtcDateTime;
				if (m_token != null && now < m_tokenRenewAt)
				{
					return m_token;
				}

				using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, TokenUrl);
				request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(Config!.ClientId + ":" + Config.ClientSecret)));
				request.Content = new FormUrlEncodedContent([new("grant_type", "client_credentials"), new("deployment_id", Config.DeploymentId)]);
				using HttpResponseMessage response = await m_http.SendAsync(request, cancellationToken);
				if (!response.IsSuccessStatusCode)
				{
					s_log.LogWarning("Epic token request answered {Status}", (int)response.StatusCode);
					return null;
				}

				using JsonDocument json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
				JsonElement root = json.RootElement;
				string token = root.GetProperty("access_token").GetString() ?? throw new FormatException("access_token");
				DateTime expiresAt = root.TryGetProperty("expires_at", out JsonElement at) && at.ValueKind != JsonValueKind.Null
					? TimeOf(at)
					: now.AddSeconds(root.GetProperty("expires_in").GetDouble());
				m_token = token;
				m_tokenRenewAt = expiresAt - s_tokenMargin;
				return token;
			}
			finally
			{
				m_tokenLock.Release();
			}
		}

		private static IReadOnlyList<EpicSanction> ParseSanctions(string body)
		{
			using JsonDocument json = JsonDocument.Parse(body);
			List<EpicSanction> sanctions = new();
			foreach (JsonElement element in json.RootElement.GetProperty("elements").EnumerateArray())
			{
				string referenceId = element.GetProperty("referenceId").GetString() ?? "";
				if (referenceId.Length == 0 || referenceId.Length > AnticheatEacFindingConfiguration.ReferenceIdMaxLength)
				{
					s_log.LogWarning("Skipped an Epic sanction with a reference id of {Length} characters", referenceId.Length);
					continue;
				}
				string action = element.GetProperty("action").GetString() ?? "";
				action = action.Length > AnticheatEacFindingConfiguration.SanctionActionMaxLength ? action[..AnticheatEacFindingConfiguration.SanctionActionMaxLength] : action;
				DateTime? expiresAt = element.TryGetProperty("expirationTimestamp", out JsonElement expiration) && expiration.ValueKind != JsonValueKind.Null ? TimeOf(expiration) : null;
				sanctions.Add(new EpicSanction(referenceId, action, TimeOf(element.GetProperty("timestamp")), expiresAt));
			}
			return sanctions;
		}

		// the per player endpoint sends epoch seconds and the bulk endpoint sends ISO 8601 text
		private static DateTime TimeOf(JsonElement value)
		{
			return value.ValueKind == JsonValueKind.Number
				? DateTimeOffset.FromUnixTimeSeconds(value.GetInt64()).UtcDateTime
				: DateTimeOffset.Parse(value.GetString() ?? throw new FormatException("time"), CultureInfo.InvariantCulture).UtcDateTime;
		}
	}
}
