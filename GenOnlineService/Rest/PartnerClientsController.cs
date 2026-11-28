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

using GenOnlineService.Api;
using GenOnlineService.Identity;
using GenOnlineService.OpenApi;
using GenOnlineService.Realtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace GenOnlineService.Rest
{
	// The OAuth clients, the audit trail and the delete of a partner, where every route also takes the staff pages sign-in cookie
	[Authorize(Policy = PermissionPolicies.Prefix + Permissions.TokensManage)]
	public sealed class PartnerClientsController : ApiTokenAdminControllerBase
	{
		public const int AuditLimit = 100;

		public PartnerClientsController(IDbContextFactory<AppDbContext> dbFactory, IConfiguration configuration) : base(dbFactory, configuration)
		{
		}

		private async Task<bool> Exists(long accountID)
		{
			await using AppDbContext db = await DbFactory.CreateDbContextAsync();
			return await db.ServiceAccounts.AsNoTracking().AnyAsync(a => a.ID == accountID);
		}

		private async Task ReloadClients()
		{
			await using AppDbContext db = await DbFactory.CreateDbContextAsync();
			await ClientRegistry.Reload(db);
		}

		[AllowSignInCookie]
		[EndpointSummary("Lists the OAuth clients of a service account.")]
		[ProducesResponseType<PartnerClientsResponse>(StatusCodes.Status200OK)]
		[HttpGet("~/api/v1/service_accounts/{accountID:long}/clients")]
		public async Task<IActionResult> ListClients(long accountID)
		{
			if (RequireWebSession() is IActionResult denied)
			{
				return denied;
			}

			return await Exists(accountID)
				? RestResults.Ok(new PartnerClientsResponse { Clients = await PartnerAdmin.ListClients(HttpContext.RequestServices, accountID) })
				: RestResults.NotFound("service_account");
		}

		[AllowSignInCookie]
		[EndpointSummary("Registers an app client for a service account; the answer is the only time a confidential client's secret is shown.")]
		[ProducesResponseType<SavedPartnerClient>(StatusCodes.Status201Created)]
		[ApiRequestBody(typeof(CreatePartnerClientRequest))]
		[HttpPost("~/api/v1/service_accounts/{accountID:long}/clients")]
		public async Task<IActionResult> CreateClient(long accountID)
		{
			if (RequireWebSession() is IActionResult denied)
			{
				return denied;
			}

			(CreatePartnerClientRequest? body, Failure? malformed) = await ReadBodyAsync<CreatePartnerClientRequest>();
			if (malformed != null)
			{
				return RestResults.Fail(malformed.Value);
			}

			if (!await Exists(accountID))
			{
				return RestResults.NotFound("service_account");
			}

			return await Answer(async () =>
			{
				(PartnerClientInfo client, string? secret) = await PartnerAdmin.CreateClient(HttpContext.RequestServices, Actor, accountID, body ?? new CreatePartnerClientRequest());
				await ReloadClients();
				return RestResults.Created(new SavedPartnerClient { Client = client, ClientSecret = secret });
			});
		}

		[AllowSignInCookie]
		[EndpointSummary("Changes or disables a client of a service account, or rotates its secret; the answer shows a rotated secret once.")]
		[ProducesResponseType<SavedPartnerClient>(StatusCodes.Status200OK)]
		[ApiRequestBody(typeof(UpdatePartnerClientRequest))]
		[HttpPatch("~/api/v1/service_accounts/{accountID:long}/clients/{clientID}")]
		public async Task<IActionResult> UpdateClient(long accountID, string clientID)
		{
			if (RequireWebSession() is IActionResult denied)
			{
				return denied;
			}

			(UpdatePartnerClientRequest? body, Failure? malformed) = await ReadBodyAsync<UpdatePartnerClientRequest>();
			if (malformed != null)
			{
				return RestResults.Fail(malformed.Value);
			}

			return await Answer(async () =>
			{
				(PartnerClientInfo client, string? secret) = await PartnerAdmin.UpdateClient(HttpContext.RequestServices, Actor, accountID, clientID, body ?? new UpdatePartnerClientRequest());
				await ReloadClients();
				return RestResults.Ok(new SavedPartnerClient { Client = client, ClientSecret = secret });
			});
		}

		[AllowSignInCookie]
		[EndpointSummary("Lists the audit entries about a service account, its clients and its tokens, newest first.")]
		[ProducesResponseType<AuditEventsResponse>(StatusCodes.Status200OK)]
		[HttpGet("~/api/v1/service_accounts/{accountID:long}/audit_events")]
		public async Task<IActionResult> ListAudit(long accountID)
		{
			if (RequireWebSession() is IActionResult denied)
			{
				return denied;
			}

			await using AppDbContext db = await DbFactory.CreateDbContextAsync();
			if (!await db.ServiceAccounts.AsNoTracking().AnyAsync(a => a.ID == accountID))
			{
				return RestResults.NotFound("service_account");
			}

			List<AuditLogEntry> rows = await PartnerAdmin.AuditOf(db, accountID, AuditLimit);
			List<long> userIDs = rows.Where(r => r.ActorID != null && r.ActorKind == AuditActorKinds.User).Select(r => r.ActorID!.Value).Distinct().ToList();
			Dictionary<long, string> names = await db.Users.AsNoTracking().Where(u => userIDs.Contains(u.ID)).Select(u => new { u.ID, u.Username }).ToDictionaryAsync(u => u.ID, u => u.Username);

			return RestResults.Ok(new AuditEventsResponse
			{
				AuditEvents = rows.Select(r => new AuditEvent
				{
					Id = r.ID,
					Action = r.Action,
					ActorKind = r.ActorKind,
					ActorName = r.Actor,
					Actor = r.ActorID != null && r.ActorKind == AuditActorKinds.User ? new AuditActor { Id = r.ActorID.Value, DisplayName = names.GetValueOrDefault(r.ActorID.Value) } : null,
					TargetKind = r.TargetKind,
					TargetId = r.TargetID,
					Reason = r.Reason,
					Source = r.Source,
					Details = DetailsOf(r),
					CreatedAt = DateTime.SpecifyKind(r.CreatedAt, DateTimeKind.Utc)
				}).ToList()
			});
		}

		private static JsonElement? DetailsOf(AuditLogEntry row)
		{
			if (row.Details == null)
			{
				return null;
			}

			try
			{
				using JsonDocument document = JsonDocument.Parse(row.Details);
				return document.RootElement.Clone();
			}
			catch (JsonException)
			{
				return null;
			}
		}
	}
}
