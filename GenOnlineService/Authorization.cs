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
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace GenOnlineService
{
	// Code checks these permissions, never role names, so a new role only changes the mapping below
	public static class Permissions
	{
		public const string ModerationKick = "moderation.kick";
		public const string ModerationBan = "moderation.ban";
		public const string ModerationMute = "moderation.mute";
		public const string ModerationRename = "moderation.rename";
		public const string ChatDeleteAny = "chat.delete_any";
		public const string ChatLogs = "chat.logs";
		public const string ChatMessageAnyone = "chat.message_anyone";
		public const string ChatStaffBadge = "chat.staff_badge";
		public const string NamesProtected = "names.protected";
		public const string NamesUnrestricted = "names.unrestricted";
		public const string SocialUnblockable = "social.unblockable";
		public const string RolesManage = "roles.manage";
		public const string ServerStats = "server.stats";
		public const string AnnouncementsSend = "announcements.send";
		public const string TokensManage = "tokens.manage";
		public const string AuditRead = "audit.read";
		public const string SettingsManage = "settings.manage";
		public const string FlagsManage = "flags.manage";
		public const string PlaylistsManage = "playlists.manage";
		public const string LaddersManage = "ladders.manage";
		public const string ChannelsManage = "channels.manage";
		public const string BroadcastsManage = "broadcasts.manage";

		public const string AnticheatReview = "anticheat.review";

		public const string PlayersInspect = "players.inspect";

		public const string AccountsSupport = "accounts.support";

		public const string TextRulesManage = "textrules.manage";

		public const string RulesManage = "rules.manage";

		public const string AppealsReview = "appeals.review";

		// What the game's local API allows, held by players and never a mark of staff
		public const string LocalApiPrefix = "local_api.";
		public const string LocalApiMatch = "local_api.match";
		public const string LocalApiStats = "local_api.stats";
		public const string LocalApiEvents = "local_api.events";
		public const string LocalApiInterest = "local_api.interest";
		public const string LocalApiCameraRead = "local_api.camera.read";
		public const string LocalApiChatRead = "local_api.chat.read";
		public const string LocalApiChatSend = "local_api.chat.send";
		public const string LocalApiQuery = "local_api.query";
		public const string LocalApiCameraControl = "local_api.camera.control";
		public const string LocalApiReplayControl = "local_api.replay.control";
		public const string LocalApiObserve = "local_api.observe";
		public const string LocalApiLobbyRead = "local_api.lobby.read";
		public const string LocalApiLobbyControl = "local_api.lobby.control";

		// Machine permissions are held only by service accounts, never by a staff role
		public const string UsersRevoke = "users.revoke";
		public const string OpsDatabase = "ops.database";
		public const string MatchHistoryRead = "match_history.read";
		public const string LobbiesRead = "lobbies.read";

		public static readonly IReadOnlySet<string> Human = new HashSet<string>(StringComparer.Ordinal)
		{
			ModerationKick, ModerationBan, ModerationMute, ModerationRename, ChatDeleteAny, ChatLogs, ChatMessageAnyone, ChatStaffBadge, NamesProtected, NamesUnrestricted, SocialUnblockable, RolesManage, ServerStats, AnnouncementsSend, TokensManage, AuditRead, SettingsManage, AnticheatReview, PlayersInspect, AccountsSupport, TextRulesManage, RulesManage, AppealsReview, FlagsManage, PlaylistsManage, LaddersManage, ChannelsManage, BroadcastsManage,
			LocalApiMatch, LocalApiStats, LocalApiEvents, LocalApiInterest, LocalApiCameraRead, LocalApiChatRead, LocalApiChatSend, LocalApiQuery, LocalApiCameraControl, LocalApiReplayControl, LocalApiObserve, LocalApiLobbyRead, LocalApiLobbyControl
		};

		public static readonly IReadOnlySet<string> Machine = new HashSet<string>(StringComparer.Ordinal)
		{
			UsersRevoke, OpsDatabase, MatchHistoryRead, LobbiesRead
		};

		// Privileged permissions act on any user's account and are granted and minted only through the CLI on the host
		public static readonly IReadOnlySet<string> Privileged = new HashSet<string>(StringComparer.Ordinal)
		{
			UsersRevoke, OpsDatabase
		};

		// Human permissions that act only through the staff pages, so no service account is ever granted them
		public static readonly IReadOnlySet<string> StaffOnly = new HashSet<string>(StringComparer.Ordinal)
		{
			AccountsSupport
		};

		public static IEnumerable<string> All => Human.Concat(Machine);

		public static bool IsLocalApi(string permission) => permission.StartsWith(LocalApiPrefix, StringComparison.Ordinal);

		// Local API permissions only switch on a client feature, so holding them alone never makes a user staff
		public static bool AnyStaff(IEnumerable<string> permissions) => permissions.Any(p => !IsLocalApi(p));

		public static bool IsKnown(string permission) => Human.Contains(permission) || Machine.Contains(permission);

		public static bool IsMachine(string permission) => Machine.Contains(permission);

		public static bool IsPrivileged(string permission) => Privileged.Contains(permission);

		public static bool IsStaffOnly(string permission) => StaffOnly.Contains(permission);
	}

	// The in-memory copy is reloaded on the reconcile tick, so a change made in the database applies without a restart
	public static class StaffRoles
	{
		private static readonly ILogger s_log = AppLog.For(typeof(StaffRoles));

		public const string Moderator = RoleCatalog.Moderator;
		public const string Admin = RoleCatalog.Admin;

		// The top rank: admin's permissions, and the only rank above admin, so an owner manages admins. Granted with the
		// roles CLI only (nobody outranks it), and never by a sign-in provider (Database.Upstream drops it)
		public const string Owner = RoleCatalog.Owner;

		// Caster grants no permission and no rank, it only switches on client features through Features:roles
		public const string Caster = RoleCatalog.Caster;

		public const string Everyone = RoleCatalog.Everyone;

		// Staff rank decides who may moderate whom, it is the position of the highest role that carries a permission,
		// and a player without such a role ranks 0
		public const int AdminRank = RoleCatalog.AdminPosition;
		public const int OwnerRank = RoleCatalog.OwnerPosition;

		private static volatile RoleCatalog s_catalog = RoleCatalog.Defaults;
		private static volatile Dictionary<Int64, HashSet<string>> s_permissionsByUser = new();
		private static volatile Dictionary<Int64, HashSet<string>> s_rolesByUser = new();
		private static string s_lastUnknownRoles = String.Empty;

		public static RoleCatalog Catalog => s_catalog;

		public static IReadOnlyCollection<string> All => s_catalog.Assignable.Select(r => r.Name).ToList();

		public static bool IsKnown(string role) => s_catalog.IsKnown(role);

		public static bool IsStaff(string role) => s_catalog.IsStaff(role);

		public static bool HasPermission(Int64 userID, string permission)
		{
			return s_permissionsByUser.TryGetValue(userID, out HashSet<string>? permissions)
				? permissions.Contains(permission)
				: s_catalog.EveryoneRole.Permissions.Contains(permission);
		}

		public static IReadOnlyCollection<string> PermissionsOf(Int64 userID)
		{
			return s_permissionsByUser.TryGetValue(userID, out HashSet<string>? permissions) ? permissions : s_catalog.EveryoneRole.Permissions;
		}

		public static int Rank(Int64 userID) => RankOf(RolesOf(userID));

		public static int RankOf(IEnumerable<string> roles) => s_catalog.RankOf(roles);

		public static int PositionOf(IEnumerable<string> roles) => s_catalog.PositionOf(roles);

		// Only a strictly higher rank may act on a user, so staff never moderate their peers or seniors
		public static bool Outranks(int actorRank, Int64 targetID) => actorRank > Rank(targetID);

		public static bool CanModerate(Int64 actorID, Int64 targetID) => actorID != targetID && Outranks(Rank(actorID), targetID);

		public static IReadOnlyCollection<string> RolesOf(Int64 userID)
		{
			return s_rolesByUser.TryGetValue(userID, out HashSet<string>? roles) ? roles : Array.Empty<string>();
		}

		// A role the service does not know grants nothing, so a typo in the database never widens access. Every player also
		// holds the everyone role.
		public static HashSet<string> PermissionsFor(IEnumerable<string> roles) => s_catalog.PermissionsFor(roles);

		public static async Task Reload(AppDbContext db)
		{
			// the definitions go first, so the user maps below are built with the roles as they are now
			RoleCatalog previousCatalog = s_catalog;
			s_catalog = await Database.StoredRoles.LoadCatalogAsync(db);
			List<UserRole> rows = await Database.Moderation.GetAllRoles(db);

			var next = rows
				.GroupBy(r => r.UserID)
				.ToDictionary(g => g.Key, g => PermissionsFor(g.Select(r => r.Role)));

			// the warning repeats only when the set of unknown roles changes, not on every reload
			string unknown = string.Join(", ", rows.Select(r => r.Role).Where(r => !IsKnown(r)).Distinct().OrderBy(r => r, StringComparer.Ordinal));
			if (unknown.Length > 0 && unknown != s_lastUnknownRoles)
			{
				s_log.LogWarning("Ignoring unknown staff role(s) {Roles} in user_roles", unknown);
			}

			s_lastUnknownRoles = unknown;
			Dictionary<Int64, HashSet<string>> previousRoles = s_rolesByUser;
			// ranks go first, so a new permission holder is never moderatable as a player while the swap runs
			s_rolesByUser = rows
				.Where(r => IsKnown(r.Role))
				.GroupBy(r => r.UserID)
				.ToDictionary(g => g.Key, g => new HashSet<string>(g.Select(r => r.Role), StringComparer.Ordinal));
			s_permissionsByUser = next;

			// every role source writes user_roles, so this swap is the one place a feature list changes
			ClientFeatures.PublishChanges(previousCatalog, previousRoles, s_catalog, s_rolesByUser);
			RealtimeBadges.PublishChanges(previousCatalog, s_catalog);
		}
	}

	// The audit actor of an HTTP caller: a session or a user's token acts as that user, a service account token as that account
	public static class CallerActors
	{
		public static Database.ModerationActor Of(ClaimsPrincipal user)
		{
			ClaimsIdentity? tokenIdentity = user.Identities.FirstOrDefault(i => i.IsAuthenticated && i.AuthenticationType == TokenPrincipal.AuthenticationType);
			if (tokenIdentity != null)
			{
				Int64? ownerID = Int64.TryParse(tokenIdentity.FindFirst(ApiTokenHandler.OwnerIDClaim)?.Value, out Int64 id) ? id : null;
				bool bService = tokenIdentity.FindFirst(ApiTokenHandler.OwnerKindClaim)?.Value == ApiTokenOwnerKinds.ServiceAccount;
				return new Database.ModerationActor
				{
					Kind = bService ? AuditActorKinds.ServiceAccount : AuditActorKinds.User,
					UserID = ownerID,
					Name = (bService ? "service_account:" : "user:") + ownerID,
					Source = ModerationSources.Api
				};
			}

			Int64? userID = Int64.TryParse(user.FindFirst(ClaimTypes.NameIdentifier)?.Value, out Int64 subject) && subject > 0 ? subject : null;
			return new Database.ModerationActor { Kind = AuditActorKinds.User, UserID = userID, Name = $"user:{userID}", Source = ModerationSources.Api };
		}
	}

	public static class PermissionPolicies
	{
		public const string Prefix = "perm:";

		public static string For(string permission) => Prefix + permission;
	}

	// Identities created by the API token scheme use this authentication type and carry their permissions as claims
	public static class TokenPrincipal
	{
		public const string AuthenticationType = "gopat";
		public const string PermissionClaim = "go_permission";
	}

	public sealed class PermissionRequirement : IAuthorizationRequirement
	{
		public PermissionRequirement(string permission)
		{
			Permission = permission;
		}

		public string Permission { get; }
	}

	public sealed class PermissionPolicyProvider : IAuthorizationPolicyProvider
	{
		private readonly DefaultAuthorizationPolicyProvider m_fallback;

		public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
		{
			m_fallback = new DefaultAuthorizationPolicyProvider(options);
		}

		public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => m_fallback.GetDefaultPolicyAsync();

		public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => m_fallback.GetFallbackPolicyAsync();

		public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
		{
			if (policyName.StartsWith(PermissionPolicies.Prefix, StringComparison.Ordinal) && policyName.Length > PermissionPolicies.Prefix.Length)
			{
				AuthorizationPolicy policy = new AuthorizationPolicyBuilder()
					.RequireAuthenticatedUser()
					.AddRequirements(new PermissionRequirement(policyName.Substring(PermissionPolicies.Prefix.Length)))
					.Build();
				return Task.FromResult<AuthorizationPolicy?>(policy);
			}

			return m_fallback.GetPolicyAsync(policyName);
		}
	}

	public sealed class PermissionHandler : AuthorizationHandler<PermissionRequirement>
	{
		protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
		{
			// the identity type is set by the scheme that authenticated it, so a claim inside a token can never fake it
			ClaimsIdentity? tokenIdentity = context.User.Identities.FirstOrDefault(i => i.IsAuthenticated && i.AuthenticationType == TokenPrincipal.AuthenticationType);
			if (tokenIdentity != null)
			{
				if (tokenIdentity.HasClaim(TokenPrincipal.PermissionClaim, requirement.Permission))
				{
					context.Succeed(requirement);
				}

				return Task.CompletedTask;
			}

			string? subject = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
			if (Int64.TryParse(subject, out Int64 userID) && userID > 0 && StaffRoles.HasPermission(userID, requirement.Permission))
			{
				context.Succeed(requirement);
			}

			return Task.CompletedTask;
		}
	}
}
