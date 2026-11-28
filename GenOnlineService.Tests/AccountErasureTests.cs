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

using System.Text.Json.Nodes;
using GenOnlineService.Identity;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GenOnlineService.Tests;

// Erasure on request (Privacy Policy, "Your rights"): personal data goes, the account id stays so matches, ratings and bans
// keep it, the name leaves the matches, and an active ban keeps the device ids and provider links until it ends
public class AccountErasureTests
{
	private const long MatchID = 700001;
	private static readonly Database.ModerationActor Operator = new() { Kind = AuditActorKinds.Cli, Name = "cli:operator", Source = ModerationSources.Cli };

	private sealed record Seeded(long Erased, long Other);

	private static async Task<Seeded> Seed(ProbeDatabase probe, bool banned)
	{
		await using AppDbContext db = probe.Open();
		DateTime now = DateTime.UtcNow;
		var user = new User
		{
			Username = "Hunter", UsernameNormalized = NameNormalizer.Normalize("Hunter"), NameTag = 1234,
			Email = "hunter@example.org", EmailNormalized = NameNormalizer.Normalize("hunter@example.org"), EmailConfirmedAt = now,
			PasswordHash = "hash", SecurityStamp = "old-stamp", TwoFactorEnabled = true, AuthenticatorKey = "protected-key",
			LockoutEndAt = now.AddMinutes(5), FailedSignInCount = 2, LastSignInAt = now, RenamedAt = now, CreatedAt = now.AddYears(-1), UpdatedAt = now
		};
		var other = new User { Username = "Friend", UsernameNormalized = NameNormalizer.Normalize("Friend"), NameTag = 4321, Email = "friend@example.org", EmailNormalized = NameNormalizer.Normalize("friend@example.org"), CreatedAt = now, UpdatedAt = now };
		db.Users.AddRange(user, other);
		await db.SaveChangesAsync();
		long id = user.ID, otherID = other.ID;

		db.UserUsernameHistory.Add(new UserUsernameHistory { UserID = id, Username = "OldName", NameTag = 1, ChangedAt = now, ChangedBy = "user:1" });
		db.UserRecoveryCodes.Add(new UserRecoveryCode { UserID = id, CodeHash = new string('a', 64), CreatedAt = now });
		db.UserAgreements.Add(new UserAgreement { UserID = id, Document = "terms", Version = "1", AgreedAt = now });
		db.Identities.Add(new UserIdentity { UserID = id, Provider = IdentityProviders.Steam, ExternUid = "76561190000000001", CreatedAt = now });
		db.Identities.Add(new UserIdentity { UserID = otherID, Provider = IdentityProviders.Steam, ExternUid = "76561190000000002", CreatedAt = now });
		db.Friendships.Add(new Friendship { UserIdLow = Math.Min(id, otherID), UserIdHigh = Math.Max(id, otherID), CreatedAt = now });
		db.FriendRequests.Add(new FriendRequest { FromUserId = otherID, ToUserId = id, CreatedAt = now });
		db.Blocks.Add(new UserBlock { UserId = otherID, BlockedUserId = id, CreatedAt = now });
		db.PlayerPreferences.Add(new PlayerPreference { UserID = id, Color = 3 });
		db.UserRoles.Add(new UserRole { UserID = id, Role = "moderator", Source = AuthoritySources.Local, GrantedAt = now });
		db.SessionEvents.Add(new SessionEvent { UserID = id, Kind = "start", Result = SessionEventResults.Ok, Ip = "192.0.2.7", CreatedAt = now });
		db.Devices.Add(new Device { UserID = id, Kind = "mac", ValueHmac = Enumerable.Repeat((byte)9, Device.ValueHmacLength).ToArray(), ValueRaw = "AA-BB", FirstSeenAt = now, LastSeenAt = now, LastIp = "192.0.2.7" });
		db.ChatMessages.Add(new ChatMessage { Kind = ChatMessageKinds.Channel, ChannelID = 1, SenderID = id, Type = ChatMessageTypes.Chat, Content = "hello", CreatedAt = now });
		db.ChatMessages.Add(new ChatMessage { Kind = ChatMessageKinds.Channel, ChannelID = 1, SenderID = otherID, Type = ChatMessageTypes.Chat, Content = "hi", CreatedAt = now });
		db.PlayerRatings.Add(new PlayerRating { UserID = id, Rating = 1600, MatchesPlayed = 10, UpdatedAt = now });
		db.Matches.Add(new Match { Id = MatchID, Name = "lobby", MapName = "Tournament Desert", RosterType = "1v1", StartedAt = now });
		db.MatchPlayers.AddRange(
			new MatchPlayer { MatchId = MatchID, Slot = 0, UserId = id, Details = "{\"display_name\":\"Hunter\",\"units_built\":12,\"metadata\":[{\"file_name\":\"a.jpg\"}]}" },
			new MatchPlayer { MatchId = MatchID, Slot = 1, UserId = otherID, Details = "{\"display_name\":\"Friend\",\"units_built\":3}" });
		if (banned)
		{
			db.Sanctions.Add(new Sanction { UserID = id, Type = SanctionTypes.Ban, Reason = "cheating", CreatedAt = now });
		}
		await db.SaveChangesAsync();
		return new Seeded(id, otherID);
	}

	// two erasures of one account at the same time: one erases and audits, the other finds it already erased
	// a live player cannot take a name that reads as an erased account's, look-alikes included
	[Fact]
	public void ATombstoneShapedNameIsReserved()
	{
		Assert.Equal(GenOnlineService.Realtime.ErrorCodes.NameReserved, GenOnlineService.Realtime.ProfileActions.ReservedNameRefusal(AccountErasure.TombstoneName())!.Value.Code);
	}
}
