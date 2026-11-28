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
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;
using static OpenIddict.Abstractions.OpenIddictConstants;
using OidcPermissions = OpenIddict.Abstractions.OpenIddictConstants.Permissions;

namespace GenOnlineService.Rest
{
	public sealed record PartnerDeleteSummary(string Name, int Permissions, int ApiTokens, int Clients, int Authorizations, int OAuthTokens);

	// The staff pages side of the OAuth clients of a partner and of deleting a partner, with the checks of the clients CLI
	public static class PartnerAdmin
	{
		private static ApiTokenAdminException Refused(string field, string message) => new ApiTokenAdminException(message, Result.Fail(ErrorCodes.InvalidArgument, new { Field = field, Message = message }));

		private static ApiTokenAdminException NotFound(string what) => new ApiTokenAdminException(what, Result.NotFound(what));

		private static string FieldOf(string error) =>
			error.StartsWith("client_id", StringComparison.Ordinal) ? "client_id"
			: error.Contains("edirect URI", StringComparison.Ordinal) ? "redirect_uris"
			: error.Contains("scope", StringComparison.Ordinal) ? "scopes"
			: error.Contains("display name", StringComparison.Ordinal) ? "name"
			: "client";

		private static List<string> Clean(IEnumerable<string>? values) => (values ?? []).Select(v => v.Trim()).Where(v => v.Length > 0).Distinct(StringComparer.Ordinal).ToList();

		private static string? CleanReason(string? reason)
		{
			string? text = String.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
			if (text != null && text.Length > ModerationManager.MaximumReasonLength)
			{
				throw Refused("reason", $"The reason must be {ModerationManager.MaximumReasonLength} characters or fewer.");
			}

			return text;
		}

		private static async Task<PartnerClientInfo> InfoOf(IOpenIddictApplicationManager applications, Client row)
		{
			object? application = await applications.FindByClientIdAsync(row.ClientID);
			bool bConfidential = application != null && await applications.HasClientTypeAsync(application, ClientTypes.Confidential);
			var scopes = new List<string>();
			if (application != null)
			{
				scopes.AddRange((await applications.GetPermissionsAsync(application)).Where(p => p.StartsWith(OidcPermissions.Prefixes.Scope, StringComparison.Ordinal))
					.Select(p => p.Substring(OidcPermissions.Prefixes.Scope.Length)).Prepend(Scopes.OpenId));
			}

			return new PartnerClientInfo
			{
				ClientId = row.ClientID,
				Name = application == null ? row.ClientID : await applications.GetDisplayNameAsync(application) ?? row.ClientID,
				Kind = row.Kind,
				Type = bConfidential ? ClientTypes.Confidential : ClientTypes.Public,
				FirstParty = row.FirstParty,
				Enabled = row.Enabled,
				Scopes = scopes,
				RedirectUris = application == null ? [] : (await applications.GetRedirectUrisAsync(application)).ToList(),
				CreatedAt = DateTime.SpecifyKind(row.CreatedAt, DateTimeKind.Utc),
				UpdatedAt = DateTime.SpecifyKind(row.UpdatedAt, DateTimeKind.Utc)
			};
		}

		public static async Task<List<PartnerClientInfo>> ListClients(IServiceProvider services, long partnerID)
		{
			await using AsyncServiceScope scope = services.CreateAsyncScope();
			AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
			var applications = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

			var infos = new List<PartnerClientInfo>();
			foreach (Client row in await db.Clients.AsNoTracking().Where(c => c.PartnerID == partnerID).OrderBy(c => c.ClientID).ToListAsync())
			{
				infos.Add(await InfoOf(applications, row));
			}

			return infos;
		}

		public static async Task<(PartnerClientInfo Client, string? Secret)> CreateClient(IServiceProvider services, ApiTokenActor actor, long partnerID, CreatePartnerClientRequest body)
		{
			string clientID = (body.ClientId ?? String.Empty).Trim();
			string name = (body.Name ?? String.Empty).Trim();
			string type = body.Type ?? String.Empty;
			if (type != ClientTypes.Public && type != ClientTypes.Confidential)
			{
				throw Refused("type", "type takes public or confidential.");
			}

			// stated on every create, so a third-party app never skips the consent page by omission
			bool bFirstParty = !(body.ThirdParty ?? throw Refused("third_party", "third_party true or false is required; a third-party client shows the consent page."));
			bool bConfidential = type == ClientTypes.Confidential;
			List<string> redirectUris = Clean(body.RedirectUris);
			List<string> scopes = body.Scopes == null ? AccountsSetup.DefaultScopes(ClientKinds.App) : Clean(body.Scopes);
			string? reason = CleanReason(body.Reason);

			string? error = ClientRegistry.ValidateClient(clientID, ClientKinds.App, ClientGames.None, bConfidential, scopes, redirectUris, name, false);
			if (error != null)
			{
				throw Refused(FieldOf(error), error);
			}

			await using AsyncServiceScope scope = services.CreateAsyncScope();
			AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
			var applications = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

			if (await db.Clients.AnyAsync(c => c.ClientID == clientID) || await applications.FindByClientIdAsync(clientID) != null)
			{
				throw new ApiTokenAdminException($"Client {clientID} already exists.", Result.Fail(RestCodes.Conflict));
			}

			OpenIddictApplicationDescriptor descriptor = ClientRegistry.Descriptor(clientID, name, bConfidential, bFirstParty, true, redirectUris, scopes);
			string? secret = bConfidential ? ClientRegistry.NewSecret() : null;
			descriptor.ClientSecret = secret;

			DateTime now = DateTime.UtcNow;
			var row = new Client { ClientID = clientID, Kind = ClientKinds.App, Game = ClientGames.None, FirstParty = bFirstParty, Enabled = true, PartnerID = partnerID, CreatedAt = now, UpdatedAt = now };

			// the OpenIddict row, the client row and the audit entry commit together
			await using (var tx = await db.Database.BeginTransactionAsync())
			{
				await applications.CreateAsync(descriptor);
				db.Clients.Add(row);
				await db.SaveChangesAsync();
				db.AuditLog.Add(ApiTokenAdmin.Audit(actor, "client_add", AuditTargetKinds.Client, row.ID, new
				{
					client_id = clientID, name, kind = ClientKinds.App, game = ClientGames.None, type, first_party = bFirstParty, scopes, redirect_uris = redirectUris, partner_id = partnerID
				}, reason));
				await db.SaveChangesAsync();
				await tx.CommitAsync();
			}

			return (await InfoOf(applications, row), secret);
		}

		public static async Task<(PartnerClientInfo Client, string? Secret)> UpdateClient(IServiceProvider services, ApiTokenActor actor, long partnerID, string clientID, UpdatePartnerClientRequest body)
		{
			string? reason = CleanReason(body.Reason);
			await using AsyncServiceScope scope = services.CreateAsyncScope();
			AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
			var applications = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

			Client row = await db.Clients.AsTracking().FirstOrDefaultAsync(c => c.ClientID == clientID && c.PartnerID == partnerID) ?? throw NotFound("client");
			object application = await applications.FindByClientIdAsync(clientID) ?? throw NotFound("client");

			bool bConfidential = await applications.HasClientTypeAsync(application, ClientTypes.Confidential);
			PartnerClientInfo current = await InfoOf(applications, row);
			string name = body.Name?.Trim() ?? current.Name;
			List<string> redirectUris = body.RedirectUris != null ? Clean(body.RedirectUris) : current.RedirectUris.ToList();
			List<string> scopes = body.Scopes != null ? Clean(body.Scopes) : current.Scopes.ToList();
			bool bFirstParty = body.ThirdParty is bool bThirdParty ? !bThirdParty : row.FirstParty;
			bool bEnabled = body.Enabled ?? row.Enabled;
			bool bRotate = body.RotateSecret == true;

			if (bRotate && !bConfidential)
			{
				throw Refused("rotate_secret", "Only a confidential client has a secret to rotate.");
			}

			string? error = ClientRegistry.ValidateClient(clientID, row.Kind, row.Game, bConfidential, scopes, redirectUris, name, false);
			if (error != null)
			{
				throw Refused(FieldOf(error), error);
			}

			OpenIddictApplicationDescriptor descriptor = new();
			await applications.PopulateAsync(descriptor, application);
			OpenIddictApplicationDescriptor next = ClientRegistry.Descriptor(clientID, name, bConfidential, bFirstParty, bEnabled, redirectUris, scopes);

			// the stored secret hash is kept unless it is rotated
			string? secret = bRotate ? ClientRegistry.NewSecret() : null;
			next.ClientSecret = secret ?? descriptor.ClientSecret;

			var changes = new Dictionary<string, object?>();
			if (name != current.Name) changes["name"] = name;
			if (!redirectUris.ToHashSet().SetEquals(current.RedirectUris)) changes["redirect_uris"] = redirectUris;
			if (!scopes.ToHashSet().SetEquals(current.Scopes)) changes["scopes"] = scopes;
			if (bFirstParty != row.FirstParty) changes["first_party"] = bFirstParty;
			if (bEnabled != row.Enabled) changes["enabled"] = bEnabled;
			if (bRotate) changes["secret"] = "rotated";

			if (changes.Count == 0)
			{
				return (current, null);
			}

			row.FirstParty = bFirstParty;
			row.Enabled = bEnabled;
			row.UpdatedAt = DateTime.UtcNow;

			string action = changes.Count == 1 && changes.ContainsKey("enabled") && !bEnabled ? "client_disable" : "client_update";
			await using (var tx = await db.Database.BeginTransactionAsync())
			{
				await applications.UpdateAsync(application, next);
				db.AuditLog.Add(ApiTokenAdmin.Audit(actor, action, AuditTargetKinds.Client, row.ID, new { client_id = clientID, changes }, reason));
				await db.SaveChangesAsync();
				await tx.CommitAsync();
			}

			return (await InfoOf(applications, row), secret);
		}

		// The entries about the partner, its clients and its tokens, newest first
		public static async Task<List<AuditLogEntry>> AuditOf(AppDbContext db, long partnerID, int limit)
		{
			List<long> tokens = await db.ApiTokens.AsNoTracking().Where(t => t.OwnerServiceID == partnerID).Select(t => t.ID).ToListAsync();
			List<long> clients = await db.Clients.AsNoTracking().Where(c => c.PartnerID == partnerID).Select(c => c.ID).ToListAsync();

			return await db.AuditLog.AsNoTracking()
				.Where(e => e.TargetID != null && ((e.TargetKind == AuditTargetKinds.ServiceAccount && e.TargetID == partnerID)
					|| (e.TargetKind == AuditTargetKinds.Token && tokens.Contains(e.TargetID.Value))
					|| (e.TargetKind == AuditTargetKinds.Client && clients.Contains(e.TargetID.Value))))
				.OrderByDescending(e => e.ID)
				.Take(limit)
				.ToListAsync();
		}

		// A real delete: the sign-ins of the clients are revoked and removed with the clients, the API tokens and grants go with the account
		public static async Task<PartnerDeleteSummary> Delete(IServiceProvider services, ApiTokenActor actor, long partnerID, string? reason)
		{
			string? text = CleanReason(reason);
			if (text == null)
			{
				throw Refused("reason", "A reason is required.");
			}

			await using AsyncServiceScope scope = services.CreateAsyncScope();
			AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
			var applications = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

			ServiceAccount account = await db.ServiceAccounts.AsTracking().FirstOrDefaultAsync(a => a.ID == partnerID) ?? throw NotFound("service_account");
			List<Client> clients = await db.Clients.AsTracking().Where(c => c.PartnerID == partnerID).ToListAsync();

			// OpenIddict goes first and a repeat finds nothing left of it, so a delete that stopped half way can be run again
			var removedClients = new List<object>();
			int authorizationCount = 0;
			int oauthTokenCount = 0;
			foreach (Client client in clients)
			{
				string clientID = client.ClientID;
				int clientAuthorizations = await db.Set<OpenIddictEntityFrameworkCoreAuthorization>().CountAsync(a => a.Application != null && a.Application.ClientId == clientID);
				int clientTokens = await db.Set<OpenIddictEntityFrameworkCoreToken>().CountAsync(t => t.Application != null && t.Application.ClientId == clientID);

				// the sign-ins end first, then the application goes with its authorizations and tokens
				await ClientRegistry.RevokeClient(clientID, services);
				object? application = await applications.FindByClientIdAsync(clientID);
				if (application != null)
				{
					await applications.DeleteAsync(application);
				}

				authorizationCount += clientAuthorizations;
				oauthTokenCount += clientTokens;
				removedClients.Add(new { client_id = clientID, authorizations = clientAuthorizations, oauth_tokens = clientTokens });
			}

			List<string> permissions = await db.ServiceAccountPermissions.AsNoTracking().Where(p => p.ServiceAccountID == partnerID).Select(p => p.Permission).OrderBy(p => p).ToListAsync();
			List<ApiToken> apiTokens = await db.ApiTokens.AsTracking().Where(t => t.OwnerServiceID == partnerID).ToListAsync();
			DateTime now = DateTime.UtcNow;
			int activeTokens = apiTokens.Count(t => t.ExpiresAt > now && (t.RevokedAt == null || t.RevokedAt > now));

			await using (var tx = await db.Database.BeginTransactionAsync())
			{
				db.ApiTokens.RemoveRange(apiTokens);
				db.Clients.RemoveRange(clients);
				db.ServiceAccounts.Remove(account);
				db.AuditLog.Add(ApiTokenAdmin.Audit(actor, "sa_delete", AuditTargetKinds.ServiceAccount, partnerID, new
				{
					name = account.Name,
					description = account.Description,
					disabled = account.DisabledAt != null,
					permissions,
					api_tokens = apiTokens.Count,
					api_tokens_active = activeTokens,
					clients = removedClients,
					oauth_authorizations = authorizationCount,
					oauth_tokens = oauthTokenCount
				}, text));
				await db.SaveChangesAsync();
				await tx.CommitAsync();
			}

			return new PartnerDeleteSummary(account.Name, permissions.Count, apiTokens.Count, clients.Count, authorizationCount, oauthTokenCount);
		}
	}
}
