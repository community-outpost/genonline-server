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
using GenOnlineService.Rest;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;
using System.Reflection;
using System.Text.Json;
using Xunit;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace GenOnlineService.Tests;

public class PartnerPermissionTests
{
	[Fact]
	public void OwnerAndAdminHoldPartnersDeleteAndModeratorDoesNot()
	{
		RoleCatalog catalog = RoleCatalog.Defaults;

		Assert.Contains(Permissions.PartnersDelete, catalog.Roles[RoleCatalog.Owner].Permissions);
		Assert.Contains(Permissions.PartnersDelete, catalog.Roles[RoleCatalog.Admin].Permissions);
		Assert.DoesNotContain(Permissions.PartnersDelete, catalog.Roles[RoleCatalog.Moderator].Permissions);
		Assert.DoesNotContain(Permissions.PartnersDelete, catalog.EveryoneRole.Permissions);
		Assert.Contains(Permissions.PartnersDelete, Permissions.Human);
		Assert.False(Permissions.IsMachine(Permissions.PartnersDelete));
	}

	[Fact]
	public void TheRoleEditorListsItWithALabelAndADescription()
	{
		var entry = PermissionCatalog.Entries.Single(e => e.Name == "partners.delete");
		Assert.False(String.IsNullOrWhiteSpace(entry.Label));
		Assert.False(String.IsNullOrWhiteSpace(entry.Description));
	}

	[Fact]
	public void TheDeleteRouteNeedsPartnersDeleteOnTopOfTokensManage()
	{
		MethodInfo delete = typeof(PartnerClientsController).GetMethod(nameof(PartnerClientsController.Delete))!;
		Assert.Contains(delete.GetCustomAttributes<AuthorizeAttribute>(), a => a.Policy == PermissionPolicies.Prefix + Permissions.PartnersDelete);
		Assert.Equal("~/api/v1/service_accounts/{accountID:long}", delete.GetCustomAttribute<Microsoft.AspNetCore.Mvc.HttpDeleteAttribute>()!.Template);
		Assert.Contains(typeof(PartnerClientsController).GetCustomAttributes<AuthorizeAttribute>(), a => a.Policy == PermissionPolicies.Prefix + Permissions.TokensManage);
	}
}

public class PartnerProbeTests
{
	private sealed class OneContextFactory(ProbeDatabase probe) : IDbContextFactory<AppDbContext>
	{
		public AppDbContext CreateDbContext() => probe.Open();
	}

	private static ServiceProvider Services(ProbeDatabase probe)
	{
		var services = new ServiceCollection();
		services.AddLogging();
		services.AddSingleton<IDbContextFactory<AppDbContext>>(new OneContextFactory(probe));
		services.AddOpenIddictCore();
		return services.BuildServiceProvider();
	}

	private static readonly ApiTokenActor Staff = ApiTokenActor.User(7);

	private static async Task<ServiceAccount> AddPartner(AppDbContext db, string name)
	{
		var account = new ServiceAccount { Name = name, Description = "a partner", CreatedBy = "test", CreatedAt = DateTime.UtcNow };
		db.ServiceAccounts.Add(account);
		await db.SaveChangesAsync();
		db.ServiceAccountPermissions.Add(new ServiceAccountPermission { ServiceAccountID = account.ID, Permission = Permissions.LobbiesRead, GrantedBy = "test", GrantedAt = DateTime.UtcNow });
		await db.SaveChangesAsync();
		return account;
	}

	private static CreatePartnerClientRequest Strata(IReadOnlyList<string>? redirects = null) => new CreatePartnerClientRequest
	{
		ClientId = "strata",
		Name = "Strata",
		Type = ClientTypes.Confidential,
		ThirdParty = true,
		Scopes = [Scopes.OpenId, Scopes.Profile],
		RedirectUris = redirects ?? ["https://strata.example.com/callback"]
	};

	private static async Task<ApiTokenAdminException> Refusal(Func<Task> action)
	{
		return await Assert.ThrowsAsync<ApiTokenAdminException>(action);
	}

	private static JsonElement DetailsOf(AuditLogEntry entry) => JsonDocument.Parse(entry.Details!).RootElement;

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task AThirdPartyConfidentialAppIsRegisteredWithoutTheCli(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		await using AppDbContext db = probe.Open();
		await using ServiceProvider services = Services(probe);
		ServiceAccount partner = await AddPartner(db, "strata-team");

		(PartnerClientInfo client, string? secret) = await PartnerAdmin.CreateClient(services, Staff, partner.ID, Strata());

		Assert.False(String.IsNullOrEmpty(secret));
		Assert.Equal("strata", client.ClientId);
		Assert.Equal(ClientKinds.App, client.Kind);
		Assert.Equal(ClientTypes.Confidential, client.Type);
		Assert.False(client.FirstParty);
		Assert.True(client.Enabled);
		Assert.Equal([Scopes.OpenId, Scopes.Profile], client.Scopes.OrderBy(s => s));
		Assert.Equal(["https://strata.example.com/callback"], client.RedirectUris);

		Client row = await db.Clients.AsNoTracking().SingleAsync(c => c.ClientID == "strata");
		Assert.Equal(partner.ID, row.PartnerID);
		Assert.Equal(ClientGames.None, row.Game);

		AuditLogEntry audit = await db.AuditLog.AsNoTracking().SingleAsync(e => e.Action == "client_add");
		Assert.Equal(AuditTargetKinds.Client, audit.TargetKind);
		Assert.Equal(row.ID, audit.TargetID);
		Assert.Equal(7, audit.ActorID);
		Assert.DoesNotContain(secret!, audit.Details!);
		Assert.Equal(partner.ID, DetailsOf(audit).GetProperty("partner_id").GetInt64());

		List<PartnerClientInfo> listed = await PartnerAdmin.ListClients(services, partner.ID);
		Assert.Equal("strata", Assert.Single(listed).ClientId);
		Assert.Empty(await PartnerAdmin.ListClients(services, partner.ID + 1));
	}

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task TheCliChecksRefuseABadClientAndNothingIsWritten(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		await using AppDbContext db = probe.Open();
		await using ServiceProvider services = Services(probe);
		ServiceAccount partner = await AddPartner(db, "strata-team");

		ApiTokenAdminException http = await Refusal(() => PartnerAdmin.CreateClient(services, Staff, partner.ID, Strata(["http://strata.example.com/callback"])));
		Assert.Equal(ErrorCodes.InvalidArgument, http.Failure.Code);
		Assert.Contains("must be https", http.Message);

		await Refusal(() => PartnerAdmin.CreateClient(services, Staff, partner.ID, Strata([])));
		await Refusal(() => PartnerAdmin.CreateClient(services, Staff, partner.ID, Strata() with { ClientId = "Bad Id" }));
		await Refusal(() => PartnerAdmin.CreateClient(services, Staff, partner.ID, Strata() with { Scopes = [Scopes.Profile] }));
		await Refusal(() => PartnerAdmin.CreateClient(services, Staff, partner.ID, Strata() with { Scopes = [Scopes.OpenId, AccountsSettings.GameScope] }));
		await Refusal(() => PartnerAdmin.CreateClient(services, Staff, partner.ID, Strata() with { Type = ClientTypes.Public, Scopes = [Scopes.OpenId, "made_up"] }));
		await Refusal(() => PartnerAdmin.CreateClient(services, Staff, partner.ID, Strata() with { ThirdParty = null }));
		await Refusal(() => PartnerAdmin.CreateClient(services, Staff, partner.ID, Strata() with { Type = "other" }));
		await Refusal(() => PartnerAdmin.CreateClient(services, Staff, partner.ID, Strata() with { Name = "  " }));

		Assert.Empty(await db.Clients.AsNoTracking().ToListAsync());
		Assert.Empty(await db.AuditLog.AsNoTracking().ToListAsync());

		await PartnerAdmin.CreateClient(services, Staff, partner.ID, Strata());
		ApiTokenAdminException twice = await Refusal(() => PartnerAdmin.CreateClient(services, Staff, partner.ID, Strata()));
		Assert.Equal(RestCodes.Conflict, twice.Failure.Code);
		Assert.Equal(1, await db.Clients.CountAsync());
	}

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task EditingRotatingAndDisablingAClientEachWriteAnAuditRowAndNeverTheSecret(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		await using AppDbContext db = probe.Open();
		await using ServiceProvider services = Services(probe);
		ServiceAccount partner = await AddPartner(db, "strata-team");
		ServiceAccount other = await AddPartner(db, "other-team");
		(_, string? first) = await PartnerAdmin.CreateClient(services, Staff, partner.ID, Strata());

		(PartnerClientInfo edited, string? none) = await PartnerAdmin.UpdateClient(services, Staff, partner.ID, "strata",
			new UpdatePartnerClientRequest { Name = "Strata Login", RedirectUris = ["https://strata.example.com/callback", "https://strata.example.com/second"], Reason = "second host" });
		Assert.Null(none);
		Assert.Equal("Strata Login", edited.Name);
		Assert.Equal(2, edited.RedirectUris.Count);

		ApiTokenAdminException bad = await Refusal(() => PartnerAdmin.UpdateClient(services, Staff, partner.ID, "strata", new UpdatePartnerClientRequest { RedirectUris = ["ftp://strata.example.com"] }));
		Assert.Equal(ErrorCodes.InvalidArgument, bad.Failure.Code);
		Assert.Equal(2, (await PartnerAdmin.ListClients(services, partner.ID)).Single().RedirectUris.Count);

		(_, string? rotated) = await PartnerAdmin.UpdateClient(services, Staff, partner.ID, "strata", new UpdatePartnerClientRequest { RotateSecret = true });
		Assert.False(String.IsNullOrEmpty(rotated));
		Assert.NotEqual(first, rotated);

		(PartnerClientInfo disabled, string? afterDisable) = await PartnerAdmin.UpdateClient(services, Staff, partner.ID, "strata", new UpdatePartnerClientRequest { Enabled = false, Reason = "paused" });
		Assert.False(disabled.Enabled);
		Assert.Null(afterDisable);
		Assert.False((await db.Clients.AsNoTracking().SingleAsync()).Enabled);

		List<AuditLogEntry> audit = await db.AuditLog.AsNoTracking().OrderBy(e => e.ID).ToListAsync();
		Assert.Equal(["client_add", "client_update", "client_update", "client_disable"], audit.Select(e => e.Action));
		Assert.Equal("second host", audit[1].Reason);
		Assert.Equal("rotated", DetailsOf(audit[2]).GetProperty("changes").GetProperty("secret").GetString());
		Assert.Equal("paused", audit[3].Reason);
		Assert.All(audit, e => Assert.DoesNotContain(first!, e.Details!));
		Assert.All(audit, e => Assert.DoesNotContain(rotated!, e.Details!));

		// a client of another partner is not found, and a repeated no-op writes nothing
		Assert.Equal(ErrorCodes.NotFound, (await Refusal(() => PartnerAdmin.UpdateClient(services, Staff, other.ID, "strata", new UpdatePartnerClientRequest { Enabled = true }))).Failure.Code);
		await PartnerAdmin.UpdateClient(services, Staff, partner.ID, "strata", new UpdatePartnerClientRequest { Enabled = false });
		Assert.Equal(4, await db.AuditLog.CountAsync());

		List<AuditLogEntry> trail = await PartnerAdmin.AuditOf(db, partner.ID, 50);
		Assert.Equal(4, trail.Count);
		Assert.Empty(await PartnerAdmin.AuditOf(db, other.ID, 50));
	}

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task APublicClientHasNoSecretToRotate(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		await using AppDbContext db = probe.Open();
		await using ServiceProvider services = Services(probe);
		ServiceAccount partner = await AddPartner(db, "strata-team");

		(PartnerClientInfo client, string? secret) = await PartnerAdmin.CreateClient(services, Staff, partner.ID, Strata() with { Type = ClientTypes.Public, ThirdParty = false });
		Assert.Null(secret);
		Assert.True(client.FirstParty);

		await Refusal(() => PartnerAdmin.UpdateClient(services, Staff, partner.ID, "strata", new UpdatePartnerClientRequest { RotateSecret = true }));
		Assert.Equal(1, await db.AuditLog.CountAsync());
	}

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task DeletingAPartnerRemovesItsTokensClientsAndSignInsAndLeavesOthersAlone(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		await using AppDbContext db = probe.Open();
		await using ServiceProvider services = Services(probe);
		ServiceAccount partner = await AddPartner(db, "strata-team");
		ServiceAccount other = await AddPartner(db, "other-team");

		await PartnerAdmin.CreateClient(services, Staff, partner.ID, Strata());
		await PartnerAdmin.CreateClient(services, Staff, other.ID, Strata() with { ClientId = "other_app", Name = "Other" });

		await using (AsyncServiceScope scope = services.CreateAsyncScope())
		{
			var applications = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
			var authorizations = scope.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationManager>();
			var tokens = scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>();
			foreach (string clientID in new[] { "strata", "other_app" })
			{
				string applicationID = (await applications.GetIdAsync((await applications.FindByClientIdAsync(clientID))!))!;
				object authorization = await authorizations.CreateAsync(new OpenIddictAuthorizationDescriptor { ApplicationId = applicationID, Subject = "42", Status = Statuses.Valid, Type = AuthorizationTypes.Permanent });
				await tokens.CreateAsync(new OpenIddictTokenDescriptor
				{
					ApplicationId = applicationID,
					AuthorizationId = await authorizations.GetIdAsync(authorization),
					Subject = "42",
					Status = Statuses.Valid,
					Type = TokenTypeHints.RefreshToken,
					ReferenceId = "ref-" + clientID
				});
			}
		}

		db.ApiTokens.AddRange(
			NewToken(partner.ID, "live", DateTime.UtcNow.AddDays(30), null, 1),
			NewToken(partner.ID, "old", DateTime.UtcNow.AddDays(30), DateTime.UtcNow.AddDays(-1), 2),
			NewToken(other.ID, "keep", DateTime.UtcNow.AddDays(30), null, 3));
		await db.SaveChangesAsync();
		long partnerClientID = (await db.Clients.AsNoTracking().SingleAsync(c => c.ClientID == "strata")).ID;

		PartnerDeleteSummary summary = await PartnerAdmin.Delete(services, ApiTokenActor.User(9), partner.ID, "ended the deal");

		Assert.Equal(new PartnerDeleteSummary("strata-team", 1, 2, 1, 1, 1), summary);
		Assert.Null(await db.ServiceAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.ID == partner.ID));
		Assert.Empty(await db.ServiceAccountPermissions.AsNoTracking().Where(p => p.ServiceAccountID == partner.ID).ToListAsync());
		Assert.Empty(await db.ApiTokens.AsNoTracking().Where(t => t.OwnerServiceID == partner.ID).ToListAsync());
		Assert.Null(await db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.ClientID == "strata"));
		Assert.Empty(await db.Set<OpenIddictEntityFrameworkCoreApplication>().AsNoTracking().Where(a => a.ClientId == "strata").ToListAsync());
		Assert.Equal(1, await db.Set<OpenIddictEntityFrameworkCoreAuthorization>().CountAsync());
		Assert.Equal(1, await db.Set<OpenIddictEntityFrameworkCoreToken>().CountAsync());

		Assert.NotNull(await db.ServiceAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.ID == other.ID));
		Assert.Equal(1, await db.ApiTokens.CountAsync(t => t.OwnerServiceID == other.ID));
		Assert.Equal(1, await db.ServiceAccountPermissions.CountAsync(p => p.ServiceAccountID == other.ID));
		Assert.NotNull(await db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.ClientID == "other_app"));
		Assert.Equal(1, await db.Set<OpenIddictEntityFrameworkCoreApplication>().CountAsync(a => a.ClientId == "other_app"));

		AuditLogEntry entry = await db.AuditLog.AsNoTracking().SingleAsync(e => e.Action == "sa_delete");
		Assert.Equal(AuditTargetKinds.ServiceAccount, entry.TargetKind);
		Assert.Equal(partner.ID, entry.TargetID);
		Assert.Equal(9, entry.ActorID);
		Assert.Equal("ended the deal", entry.Reason);
		JsonElement details = DetailsOf(entry);
		Assert.Equal("strata-team", details.GetProperty("name").GetString());
		Assert.Equal(2, details.GetProperty("api_tokens").GetInt32());
		Assert.Equal(1, details.GetProperty("api_tokens_active").GetInt32());
		Assert.Equal(1, details.GetProperty("oauth_authorizations").GetInt32());
		Assert.Equal(1, details.GetProperty("oauth_tokens").GetInt32());
		Assert.Equal("strata", details.GetProperty("clients")[0].GetProperty("client_id").GetString());
		Assert.Equal(1, details.GetProperty("permissions").GetArrayLength());

		// the earlier audit rows of the partner stay readable by their target
		Assert.Equal(partnerClientID, (await db.AuditLog.AsNoTracking().SingleAsync(e => e.Action == "client_add" && e.TargetID == partnerClientID)).TargetID);
	}

	private static ApiToken NewToken(long ownerID, string name, DateTime expires, DateTime? revoked, byte seed) => new ApiToken
	{
		OwnerServiceID = ownerID,
		Name = name,
		TokenHash = Enumerable.Repeat(seed, 32).ToArray(),
		TokenLast4 = "abcd",
		Scopes = Permissions.LobbiesRead,
		CreatedBy = "test",
		CreatedAt = DateTime.UtcNow.AddDays(-2),
		ExpiresAt = expires,
		RevokedAt = revoked
	};

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task ADeleteWithoutAReasonOrForAMissingPartnerRemovesNothing(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		await using AppDbContext db = probe.Open();
		await using ServiceProvider services = Services(probe);
		ServiceAccount partner = await AddPartner(db, "strata-team");
		await PartnerAdmin.CreateClient(services, Staff, partner.ID, Strata());

		ApiTokenAdminException noReason = await Refusal(() => PartnerAdmin.Delete(services, Staff, partner.ID, "   "));
		Assert.Equal(ErrorCodes.InvalidArgument, noReason.Failure.Code);
		Assert.Equal(ErrorCodes.NotFound, (await Refusal(() => PartnerAdmin.Delete(services, Staff, partner.ID + 100, "reason"))).Failure.Code);

		Assert.NotNull(await db.ServiceAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.ID == partner.ID));
		Assert.Equal(1, await db.Clients.CountAsync());
		Assert.Equal(1, await db.AuditLog.CountAsync());
	}
}
