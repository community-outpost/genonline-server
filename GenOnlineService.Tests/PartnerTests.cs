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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using System.Text.Json;
using Xunit;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace GenOnlineService.Tests;

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
}
