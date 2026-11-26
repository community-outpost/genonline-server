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
using Microsoft.EntityFrameworkCore;

namespace GenOnlineService.Rest
{
	public sealed record DiscordBotLinkRequest
	{
		// A string, so a snowflake keeps every digit in JavaScript
		public string? DiscordUserId { get; init; }
	}

	public sealed record DiscordBotLinkResponse
	{
		public string? DiscordUserId { get; init; }
	}

	// The Discord account the bot treats as a player for its staff commands only, never a sign-in, and setting it hands that
	// Discord user the player's staff permissions in the bot, so it takes roles.manage and the rank rule of roles
	[ApiController]
	[Authorize(Policy = PermissionPolicies.Prefix + Permissions.RolesManage)]
	public sealed class DiscordBotLinksController : ControllerBase
	{
		private readonly IDbContextFactory<AppDbContext> m_dbFactory;

		public DiscordBotLinksController(IDbContextFactory<AppDbContext> dbFactory)
		{
			m_dbFactory = dbFactory;
		}

		[AllowSignInCookie]
		[EndpointSummary("Reads the Discord account the bot treats as a player.")]
		[ProducesResponseType<DiscordBotLinkResponse>(StatusCodes.Status200OK)]
		[HttpGet("~/api/v1/players/{id:long}/discord_bot_link")]
		public async Task<IActionResult> Get(long id)
		{
			await using AppDbContext db = await m_dbFactory.CreateDbContextAsync();
			if (!await Database.Users.Exists(db, id))
			{
				return RestResults.NotFound("user");
			}
			return RestResults.Ok(new DiscordBotLinkResponse { DiscordUserId = (await Database.DiscordBotLinks.Get(db, id))?.ToString() });
		}

		[AllowSignInCookie]
		[EndpointSummary("Sets the Discord account the bot treats as a player.")]
		[ProducesResponseType<DiscordBotLinkResponse>(StatusCodes.Status200OK)]
		[HttpPut("~/api/v1/players/{id:long}/discord_bot_link")]
		public async Task<IActionResult> Put(long id, [FromBody] DiscordBotLinkRequest? body)
		{
			if (!Database.DiscordBotLinks.TryParse(body?.DiscordUserId, out long discordUserID))
			{
				return RestResults.Invalid("discord_user_id");
			}

			await using AppDbContext db = await m_dbFactory.CreateDbContextAsync();
			if (await CheckAsync(db, id) is IActionResult refused)
			{
				return refused;
			}

			(Database.DiscordBotLinkOutcome outcome, long? heldBy) = await Database.DiscordBotLinks.Set(db, id, discordUserID, CallerActors.Of(User));
			if (outcome == Database.DiscordBotLinkOutcome.InUse)
			{
				return RestResults.Fail(RestCodes.DiscordBotInUse, new { UserId = heldBy });
			}
			return RestResults.Ok(new DiscordBotLinkResponse { DiscordUserId = discordUserID.ToString() });
		}

		[AllowSignInCookie]
		[EndpointSummary("Removes the Discord account the bot treats as a player.")]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
		[HttpDelete("~/api/v1/players/{id:long}/discord_bot_link")]
		public async Task<IActionResult> Delete(long id)
		{
			await using AppDbContext db = await m_dbFactory.CreateDbContextAsync();
			if (await CheckAsync(db, id) is IActionResult refused)
			{
				return refused;
			}

			return await Database.DiscordBotLinks.Clear(db, id, CallerActors.Of(User)) ? new NoContentResult() : RestResults.NotFound("discord_bot_link");
		}

		// The rank rule of roles with no role changed: the caller outranks the player, so never links themself or a peer
		private async Task<IActionResult?> CheckAsync(AppDbContext db, long id)
		{
			if (!await Database.Users.Exists(db, id))
			{
				return RestResults.NotFound("user");
			}

			Database.ModerationActor actor = CallerActors.Of(User);
			Failure? failure = RoleRules.Check(actor, await RoleRules.CallerRankAsync(db, actor), id, await RoleRules.RankAsync(db, id), []);
			return failure != null ? RestResults.Fail(failure.Value) : null;
		}
	}
}
