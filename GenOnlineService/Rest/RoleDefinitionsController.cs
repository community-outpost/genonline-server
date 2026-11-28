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
using GenOnlineService.Realtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GenOnlineService.OpenApi;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace GenOnlineService.Rest
{
	// Giving a role to a player stays with the player routes (RolesController). Every change is audited and applies at once
	[ApiController]
	[Authorize(Policy = PermissionPolicies.Prefix + Permissions.RolesManage)]
	public sealed class RoleDefinitionsController : ControllerBase
	{
		private const int MaxMembers = 200;

		private readonly IDbContextFactory<AppDbContext> m_dbFactory;

		public RoleDefinitionsController(IDbContextFactory<AppDbContext> dbFactory)
		{
			m_dbFactory = dbFactory;
		}

		// Read from the database rows so a change made a moment ago counts. A service account token acts with admin standing and the permissions its token carries
		private async Task<(int Position, IReadOnlySet<string> Permissions)> CallerAsync(AppDbContext db, Database.ModerationActor actor)
		{
			if (RoleRules.AccountOf(actor) is long id)
			{
				List<string> roles = (await Database.Moderation.GetRoles(db, id)).Select(r => r.Role).ToList();
				return (StaffRoles.RankOf(roles), StaffRoles.PermissionsFor(roles));
			}

			var held = new HashSet<string>(User.FindAll(TokenPrincipal.PermissionClaim).Select(c => c.Value).Where(Permissions.Human.Contains), StringComparer.Ordinal);
			return (StaffRoles.AdminRank, held);
		}

		private static RoleResponse Describe(RoleDefinition role, int callerPosition, IReadOnlyDictionary<string, int> members) => new()
		{
			Name = role.Name,
			DisplayName = role.DisplayName,
			Color = role.Color,
			Tag = role.Tag,
			Description = role.Description,
			Position = role.Position,
			BuiltIn = role.BuiltIn,
			Locked = role.Name == RoleCatalog.Owner,
			Manageable = RoleDefinitionRules.CanManage(callerPosition, role),
			Permissions = role.Permissions.Order(StringComparer.Ordinal).ToList(),
			MemberCount = role.Name == RoleCatalog.Everyone ? 0 : members.GetValueOrDefault(role.Name)
		};

		private async Task<(RoleWriteRequest? Body, Failure? Failure)> ReadBodyAsync()
		{
			if (Request.ContentLength == 0)
			{
				return (null, null);
			}

			// only JSON, as [FromBody] enforced: a cross-site form cannot send application/json without a preflight
			if (!Request.HasJsonContentType())
			{
				return (null, Result.Invalid("body"));
			}

			try
			{
				using StreamReader reader = new StreamReader(Request.Body);
				string text = await reader.ReadToEndAsync();
				return String.IsNullOrWhiteSpace(text) ? (null, null) : (JsonSerializer.Deserialize<RoleWriteRequest>(text, ApiResults.Json), null);
			}
			catch (JsonException ex)
			{
				string field = ex.Path is string path && path.StartsWith("$.", StringComparison.Ordinal) ? path.Substring(2) : "body";
				return (null, Result.Invalid(field));
			}
		}

		private static string? Clean(string? text) => String.IsNullOrWhiteSpace(text) ? null : text.Trim();

		private static HashSet<string>? CleanPermissions(IReadOnlyList<string?>? permissions)
		{
			var result = new HashSet<string>(StringComparer.Ordinal);
			foreach (string? permission in permissions ?? [])
			{
				if (String.IsNullOrWhiteSpace(permission))
				{
					return null;
				}
				result.Add(permission.Trim().ToLowerInvariant());
			}
			return result;
		}

		[EndpointSummary("Lists the roles with their permissions, from the highest position down.")]
		[ProducesResponseType<RoleListResponse>(StatusCodes.Status200OK)]
		[HttpGet("~/api/v1/roles")]
		[AllowSignInCookie]
		public async Task<IActionResult> List()
		{
			await using AppDbContext db = await m_dbFactory.CreateDbContextAsync();
			RoleCatalog catalog = await Database.StoredRoles.LoadCatalogAsync(db);
			(int position, _) = await CallerAsync(db, CallerActors.Of(User));
			Dictionary<string, int> members = await Database.StoredRoles.MemberCountsAsync(db);

			return RestResults.Ok(new RoleListResponse { Roles = catalog.Roles.Values.OrderByDescending(r => r.Position).Select(r => Describe(r, position, members)).ToList() });
		}

		[EndpointSummary("Lists every permission a role can hold, with its area and meaning and whether the caller holds it.")]
		[ProducesResponseType<PermissionsResponse>(StatusCodes.Status200OK)]
		[HttpGet("~/api/v1/permissions")]
		[AllowSignInCookie]
		public async Task<IActionResult> Catalog()
		{
			await using AppDbContext db = await m_dbFactory.CreateDbContextAsync();
			(_, IReadOnlySet<string> held) = await CallerAsync(db, CallerActors.Of(User));
			return RestResults.Ok(new PermissionsResponse { Permissions = PermissionCatalog.For(held) });
		}

		[EndpointSummary("Creates a role below the caller's own highest role.")]
		[ProducesResponseType<RoleResponse>(StatusCodes.Status201Created)]
		[HttpPost("~/api/v1/roles")]
		[AllowSignInCookie]
		[ApiRequestBody(typeof(RoleWriteRequest))]
		public async Task<IActionResult> Create()
		{
			(RoleWriteRequest? body, Failure? malformed) = await ReadBodyAsync();
			if (malformed != null)
			{
				return RestResults.Fail(malformed.Value);
			}

			string name = Clean(body?.Name)?.ToLowerInvariant() ?? String.Empty;
			HashSet<string>? permissions = CleanPermissions(body?.Permissions);
			if (body == null || permissions == null)
			{
				return RestResults.Invalid(body == null ? "name" : "permissions");
			}

			await using AppDbContext db = await m_dbFactory.CreateDbContextAsync();
			RoleCatalog catalog = await Database.StoredRoles.LoadCatalogAsync(db);
			Database.ModerationActor actor = CallerActors.Of(User);
			(int callerPosition, IReadOnlySet<string> held) = await CallerAsync(db, actor);

			int? position = body.Position ?? (body.Above != null ? RoleDefinitionRules.PositionAbove(catalog, body.Above, callerPosition) : null);
			if (position == null)
			{
				return body.Above != null ? RestResults.Fail(RestCodes.RolePositionTaken, new { Above = body.Above }) : RestResults.Invalid("position");
			}

			var wanted = new RoleChange(name, Clean(body.DisplayName) ?? name, Clean(body.Color), Clean(body.Description), position.Value, permissions, Clean(body.Tag));
			Failure? failure = RoleDefinitionRules.CheckCreate(catalog, callerPosition, held, wanted);
			if (failure != null)
			{
				return RestResults.Fail(failure.Value);
			}

			if (!await Database.StoredRoles.CreateAsync(db, wanted, actor, DateTime.UtcNow))
			{
				return RestResults.Fail(RestCodes.Conflict);
			}

			await StaffRoles.Reload(db);
			RoleCatalog after = await Database.StoredRoles.LoadCatalogAsync(db);
			return RestResults.Created(Describe(after.Roles[name], callerPosition, new Dictionary<string, int>()));
		}

		[EndpointSummary("Changes a role. Name never changes, an empty color, tag or description clears it, and position or above moves a custom role.")]
		[ProducesResponseType<RoleResponse>(StatusCodes.Status200OK)]
		[HttpPatch("~/api/v1/roles/{name}")]
		[AllowSignInCookie]
		[ApiRequestBody(typeof(RoleWriteRequest))]
		public async Task<IActionResult> Update(string name)
		{
			(RoleWriteRequest? body, Failure? malformed) = await ReadBodyAsync();
			if (malformed != null)
			{
				return RestResults.Fail(malformed.Value);
			}

			if (body == null)
			{
				return RestResults.Invalid("name");
			}

			await using AppDbContext db = await m_dbFactory.CreateDbContextAsync();
			RoleCatalog catalog = await Database.StoredRoles.LoadCatalogAsync(db);
			if (!catalog.Roles.TryGetValue(name.ToLowerInvariant(), out RoleDefinition? existing))
			{
				return RestResults.NotFound("role");
			}

			Database.ModerationActor actor = CallerActors.Of(User);
			(int callerPosition, IReadOnlySet<string> held) = await CallerAsync(db, actor);

			HashSet<string>? permissions = body.Permissions == null ? new HashSet<string>(existing.Permissions, StringComparer.Ordinal) : CleanPermissions(body.Permissions);
			if (permissions == null)
			{
				return RestResults.Invalid("permissions");
			}

			int? position = body.Position ?? (body.Above != null ? RoleDefinitionRules.PositionAbove(catalog, body.Above, callerPosition) : existing.Position);
			if (position == null)
			{
				return RestResults.Fail(RestCodes.RolePositionTaken, new { Above = body.Above });
			}

			var wanted = new RoleChange(
				existing.Name,
				Clean(body.DisplayName) ?? existing.DisplayName,
				body.Color == null ? existing.Color : Clean(body.Color),
				body.Description == null ? existing.Description : Clean(body.Description),
				position.Value,
				permissions,
				body.Tag == null ? existing.Tag : Clean(body.Tag));
			if (body.Name != null && !String.Equals(body.Name.Trim(), existing.Name, StringComparison.OrdinalIgnoreCase))
			{
				return RestResults.Invalid("name");
			}

			Failure? failure = RoleDefinitionRules.CheckUpdate(catalog, callerPosition, held, existing, wanted);
			if (failure != null)
			{
				return RestResults.Fail(failure.Value);
			}

			if (wanted.DisplayName == existing.DisplayName && wanted.Color == existing.Color && wanted.Tag == existing.Tag && wanted.Description == existing.Description
				&& wanted.Position == existing.Position && wanted.Permissions.SetEquals(existing.Permissions))
			{
				return RestResults.Ok(Describe(existing, callerPosition, await Database.StoredRoles.MemberCountsAsync(db)));
			}

			if (!await Database.StoredRoles.UpdateAsync(db, existing, wanted, actor, DateTime.UtcNow))
			{
				return RestResults.Fail(RestCodes.Conflict);
			}

			await StaffRoles.Reload(db);
			RoleCatalog after = await Database.StoredRoles.LoadCatalogAsync(db);
			return RestResults.Ok(Describe(after.Roles[existing.Name], callerPosition, await Database.StoredRoles.MemberCountsAsync(db)));
		}

		[EndpointSummary("Deletes a custom role and takes it from everyone who held it.")]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
		[HttpDelete("~/api/v1/roles/{name}")]
		[AllowSignInCookie]
		public async Task<IActionResult> Delete(string name)
		{
			await using AppDbContext db = await m_dbFactory.CreateDbContextAsync();
			RoleCatalog catalog = await Database.StoredRoles.LoadCatalogAsync(db);
			if (!catalog.Roles.TryGetValue(name.ToLowerInvariant(), out RoleDefinition? existing))
			{
				return RestResults.NotFound("role");
			}

			Database.ModerationActor actor = CallerActors.Of(User);
			(int callerPosition, _) = await CallerAsync(db, actor);
			Failure? failure = RoleDefinitionRules.CheckDelete(callerPosition, existing);
			if (failure != null)
			{
				return RestResults.Fail(failure.Value);
			}

			await Database.StoredRoles.DeleteAsync(db, existing, actor, DateTime.UtcNow);
			await StaffRoles.Reload(db);
			return NoContent();
		}

		[EndpointSummary("Lists who holds a role, newest first, up to 200.")]
		[ProducesResponseType<RoleMembersResponse>(StatusCodes.Status200OK)]
		[HttpGet("~/api/v1/roles/{name}/members")]
		[AllowSignInCookie]
		public async Task<IActionResult> Members(string name)
		{
			await using AppDbContext db = await m_dbFactory.CreateDbContextAsync();
			RoleCatalog catalog = await Database.StoredRoles.LoadCatalogAsync(db);
			string role = name.ToLowerInvariant();
			if (role == RoleCatalog.Everyone)
			{
				return RestResults.Ok(new RoleMembersResponse());
			}
			if (!catalog.IsKnown(role))
			{
				return RestResults.NotFound("role");
			}

			int total = await db.UserRoles.AsNoTracking().Where(r => r.Role == role).CountAsync();
			List<UserRole> rows = await db.UserRoles.AsNoTracking().Where(r => r.Role == role).OrderByDescending(r => r.GrantedAt).ThenBy(r => r.UserID).Take(MaxMembers).ToListAsync();
			List<long> ids = rows.Select(r => r.UserID).Distinct().ToList();
			Dictionary<long, string> names = (await db.Users.AsNoTracking().Where(u => ids.Contains(u.ID)).Select(u => new { u.ID, u.Username }).ToListAsync()).ToDictionary(u => u.ID, u => u.Username);

			return RestResults.Ok(new RoleMembersResponse
			{
				Total = total,
				Members = rows.Select(r => new RoleMemberResponse { UserId = r.UserID, Name = names.GetValueOrDefault(r.UserID) ?? "", Source = r.Source, GrantedAt = r.GrantedAt, GrantedBy = r.GrantedBy }).ToList()
			});
		}
	}
}
