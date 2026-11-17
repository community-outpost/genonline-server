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
using GenOnlineService.Names;
using GenOnlineService.Realtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Xunit;

namespace GenOnlineService.Tests
{
	public class RenameTests
	{
		// The plugin's key: Bob, BOB and B0b are one name, so are look-alike letters from other scripts
		// REST staff actions are audited as api, the realtime hub keeps realtime
		private static object? Param(Failure failure, string name)
		{
			JsonElement element = JsonSerializer.SerializeToElement(failure.Params, ApiResults.Json);
			if (!element.TryGetProperty(name, out JsonElement value))
			{
				return null;
			}
			return value.ValueKind == JsonValueKind.Number ? value.GetInt32() : value.GetString();
		}

		[Fact]
		public void StaffRenameFollowsTheRankRule()
		{
			Assert.Null(PlayerNames.CheckStaff(1, 1, 2, 0, null));
			Assert.Null(PlayerNames.CheckStaff(1, 2, 2, 1, "offensive name"));
			Assert.Null(PlayerNames.CheckStaff(null, StaffRoles.AdminRank, 2, 1, null));
			Assert.Equal(ErrorCodes.ModerationTargetOutranks, PlayerNames.CheckStaff(1, 1, 2, 1, null)!.Value.Code);
			Assert.Equal(ErrorCodes.ModerationTargetOutranks, PlayerNames.CheckStaff(1, 1, 2, 2, null)!.Value.Code);
			Assert.Equal(ErrorCodes.ModerationTargetOutranks, PlayerNames.CheckStaff(null, StaffRoles.AdminRank, 2, StaffRoles.AdminRank, null)!.Value.Code);
			Assert.Equal(ErrorCodes.InvalidArgument, PlayerNames.CheckStaff(1, 2, 1, 0, null)!.Value.Code);
			Assert.Equal(ErrorCodes.InvalidArgument, PlayerNames.CheckStaff(1, 2, 0, 0, null)!.Value.Code);
			Assert.Equal(ErrorCodes.ModerationReasonTooLong, PlayerNames.CheckStaff(1, 2, 2, 0, new string('r', ModerationManager.MaximumReasonLength + 1))!.Value.Code);
		}

		private sealed class DenyAll : IAuthorizationService
		{
			public List<string> Policies { get; } = new();

			public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, IEnumerable<IAuthorizationRequirement> requirements) => Task.FromResult(AuthorizationResult.Failed());

			public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, string policyName)
			{
				Policies.Add(policyName);
				return Task.FromResult(AuthorizationResult.Failed());
			}
		}

		private static ModerationController DeniedController(DenyAll authorization) => new(null!, authorization, null!)
		{
			ControllerContext = new ControllerContext
			{
				HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "7") }, "test")) }
			}
		};

		private static void AssertNeedsModerationRename(IActionResult answer, DenyAll authorization)
		{
			JsonResult json = Assert.IsType<JsonResult>(answer);
			Assert.Equal(403, json.StatusCode);
			ApiError error = Assert.IsType<ApiError>(json.Value);
			Assert.Equal(ErrorCodes.Forbidden, error.Error);
			Assert.Equal(new[] { PermissionPolicies.For(Permissions.ModerationRename) }, authorization.Policies);
		}

		[Fact]
		public async Task ANameResetNeedsModerationRename()
		{
			DenyAll authorization = new();
			AssertNeedsModerationRename(await DeniedController(authorization).ResetPlayerName(8), authorization);
		}

	}
}
