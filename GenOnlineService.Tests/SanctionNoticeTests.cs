using GenOnlineService.Api;
using GenOnlineService.Failover;
using GenOnlineService.Identity;
using GenOnlineService.Realtime;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Xunit;

namespace GenOnlineService.Tests
{
	public class SanctionNoticeTests
	{
		private const long Host = 1;
		private const long Outsider = 99;

		private readonly List<(string Group, Message Message)> m_sent = new();

		private Task Record(string group, Message message)
		{
			m_sent.Add((group, message));
			return Task.CompletedTask;
		}

		private static Lobby NewLobby(long id, ulong matchID)
		{
			EPlayerType[] states = { EPlayerType.SLOT_PLAYER, EPlayerType.SLOT_PLAYER, EPlayerType.SLOT_OPEN, EPlayerType.SLOT_OPEN, EPlayerType.SLOT_OPEN, EPlayerType.SLOT_OPEN, EPlayerType.SLOT_OPEN, EPlayerType.SLOT_OPEN };
			return Lobby.FromLive(new LobbyLive
			{
				Id = id,
				Owner = Host,
				State = (int)ELobbyState.GAME_SETUP,
				Type = (int)ELobbyType.CustomGame,
				StartPhase = "none",
				MatchId = matchID,
				Slots = Enumerable.Range(0, Lobby.MaxLobbySize).Select(index => new LobbyMemberLive
				{
					SlotIndex = index,
					SlotState = (int)states[index],
					UserId = index < 2 ? index + 1 : -1,
					Side = 10 + index,
					Color = index,
					Team = index,
					StartPosition = index,
					JoinSequence = index < 2 ? index + 1 : 0
				}).ToList()
			});
		}

		[Fact]
		public void EachKindOfContextParses()
		{
			Assert.Null(SanctionNotice.ParseContext(new SanctionContext { ChannelId = 5 }, out SanctionPlace? channel));
			Assert.Equal(new SanctionPlace(SanctionPlaceKind.Channel, 5), channel);
			Assert.Null(SanctionNotice.ParseContext(new SanctionContext { LobbyId = 9 }, out SanctionPlace? lobby));
			Assert.Equal(new SanctionPlace(SanctionPlaceKind.Lobby, 9), lobby);
			Assert.Null(SanctionNotice.ParseContext(new SanctionContext { MatchId = "123456789012" }, out SanctionPlace? match));
			Assert.Equal(new SanctionPlace(SanctionPlaceKind.Match, 123456789012), match);
		}

		[Fact]
		public void NoContextAndAnEmptyContextAreTheSame()
		{
			Assert.Null(SanctionNotice.ParseContext(null, out SanctionPlace? none));
			Assert.Null(none);
			Assert.Null(SanctionNotice.ParseContext(new SanctionContext(), out SanctionPlace? empty));
			Assert.Null(empty);
		}

		[Fact]
		public void MoreThanOneKindIsRefused()
		{
			AssertInvalid(new SanctionContext { ChannelId = 1, LobbyId = 2 });
			AssertInvalid(new SanctionContext { LobbyId = 2, MatchId = "3" });
			AssertInvalid(new SanctionContext { ChannelId = 1, LobbyId = 2, MatchId = "3" });
		}

		[Theory]
		[InlineData("")]
		[InlineData("abc")]
		[InlineData("0")]
		[InlineData("-1")]
		[InlineData("+5")]
		[InlineData(" 5")]
		[InlineData("5.0")]
		[InlineData("99999999999999999999")]
		public void ABadMatchIdIsRefused(string matchID) => AssertInvalid(new SanctionContext { MatchId = matchID });

		[Theory]
		[InlineData(-1)]
		[InlineData(32768)]
		public void ABadChannelIsRefused(long channelID) => AssertInvalid(new SanctionContext { ChannelId = channelID });

		[Theory]
		[InlineData(0)]
		[InlineData(-4)]
		public void ABadLobbyIsRefused(long lobbyID) => AssertInvalid(new SanctionContext { LobbyId = lobbyID });

		private static void AssertInvalid(SanctionContext context)
		{
			Failure failure = SanctionNotice.ParseContext(context, out SanctionPlace? place)!.Value;
			Assert.Null(place);
			Assert.Equal(ErrorCodes.InvalidArgument, failure.Code);
			Assert.Equal("context", ((dynamic)failure.Params).Field);
		}

		[Theory]
		[InlineData(SanctionType.Kick, null, "ArcticDolphin kicked Beaver^ 001.")]
		[InlineData(SanctionType.Kick, 3600L, "ArcticDolphin kicked Beaver^ 001.")]
		[InlineData(SanctionType.Mute, 3600L, "ArcticDolphin muted Beaver^ 001 for 1 hour.")]
		[InlineData(SanctionType.Mute, 60L, "ArcticDolphin muted Beaver^ 001 for 1 minute.")]
		[InlineData(SanctionType.Mute, 90L, "ArcticDolphin muted Beaver^ 001 for 2 minutes.")]
		[InlineData(SanctionType.Mute, 7200L, "ArcticDolphin muted Beaver^ 001 for 2 hours.")]
		[InlineData(SanctionType.Ban, 259200L, "ArcticDolphin banned Beaver^ 001 for 3 days.")]
		[InlineData(SanctionType.Ban, 86400L, "ArcticDolphin banned Beaver^ 001 for 1 day.")]
		[InlineData(SanctionType.Ban, 5400L, "ArcticDolphin banned Beaver^ 001 for 90 minutes.")]
		[InlineData(SanctionType.Ban, null, "ArcticDolphin banned Beaver^ 001 permanently.")]
		public void TheLineNamesTheActionAndTheLength(SanctionType type, long? durationS, string expected)
		{
			Assert.Equal(expected, SanctionNotice.Line("ArcticDolphin", "Beaver^ 001", type, durationS));
		}

		[Fact]
		public async Task ALineGoesToTheChannelTheStaffMemberIsIn()
		{
			using IDisposable staff = AgreementGateTests.Connected(9001, "ArcticDolphin");
			WebSocketManager.GetSessionFromUser(9001, EUserSessionType.GameClient)!.networkRoomID = 3;

			Assert.True(await SanctionNotice.AnnounceAsync(new SanctionPlace(SanctionPlaceKind.Channel, 3), 9001, "line", [], Record));

			(string group, Message message) = Assert.Single(m_sent);
			Assert.Equal(RealtimeGroups.Channel(3), group);
			Assert.Equal("channel:3", message.ChannelId);
			Assert.Equal("line", message.Content);
			Assert.Null(message.Author);
		}

		[Fact]
		public async Task NothingGoesToAChannelTheStaffMemberIsNotIn()
		{
			using IDisposable staff = AgreementGateTests.Connected(9002, "ArcticDolphin");
			WebSocketManager.GetSessionFromUser(9002, EUserSessionType.GameClient)!.networkRoomID = 4;

			Assert.False(await SanctionNotice.AnnounceAsync(new SanctionPlace(SanctionPlaceKind.Channel, 3), 9002, "line", [], Record));
			Assert.False(await SanctionNotice.AnnounceAsync(new SanctionPlace(SanctionPlaceKind.Channel, 3), Outsider, "line", [], Record));
			Assert.Empty(m_sent);
		}

		[Fact]
		public async Task ALineGoesToTheLobbyTheStaffMemberIsIn()
		{
			Lobby[] lobbies = [NewLobby(10, 0), NewLobby(11, 0)];

			Assert.True(await SanctionNotice.AnnounceAsync(new SanctionPlace(SanctionPlaceKind.Lobby, 10), Host, "line", lobbies, Record));

			(string group, Message message) = Assert.Single(m_sent);
			Assert.Equal("lobby:10", group);
			Assert.Equal("lobby:10", message.ChannelId);
			Assert.Equal("line", message.Content);
			Assert.Null(message.Author);
		}

		[Fact]
		public async Task NothingGoesToALobbyTheStaffMemberIsNotIn()
		{
			Lobby[] lobbies = [NewLobby(10, 0)];

			Assert.False(await SanctionNotice.AnnounceAsync(new SanctionPlace(SanctionPlaceKind.Lobby, 10), Outsider, "line", lobbies, Record));
			Assert.False(await SanctionNotice.AnnounceAsync(new SanctionPlace(SanctionPlaceKind.Lobby, 12), Host, "line", lobbies, Record));
			Assert.Empty(m_sent);
		}

		private sealed class OneContextFactory(ProbeDatabase probe) : IDbContextFactory<AppDbContext>
		{
			public AppDbContext CreateDbContext() => probe.Open();
		}

		private static async Task<long> AddUser(AppDbContext db, string name, int tag)
		{
			User user = new User { Username = name, UsernameNormalized = NameNormalizer.Normalize(name), NameTag = tag, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
			db.Users.Add(user);
			await db.SaveChangesAsync();
			return user.ID;
		}

		[ProbeTheory]
		[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
		public async Task TheContextIsKeptInTheAuditDetailsOfTheSanction(string provider)
		{
			await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
			if (probe == null) return;
			await using AppDbContext db = probe.Open();
			long admin = await AddUser(db, "Chief", 1001);
			long player = await AddUser(db, "Player", 1002);
			db.UserRoles.Add(new UserRole { UserID = admin, Role = StaffRoles.Admin, GrantedAt = DateTime.UtcNow });
			await db.SaveChangesAsync();
			await StaffRoles.Reload(db);

			Result<CreateSanctionResponse> withContext = await SanctionActions.CreateAsync(new OneContextFactory(probe), admin, player,
				new CreateSanctionRequest { Type = SanctionType.Mute, DurationS = 3600, Reason = "toxic", Context = new SanctionContext { MatchId = "77" } }, _ => Task.FromResult<Failure?>(null));
			Assert.True(withContext.Ok, withContext.Code);
			Result<CreateSanctionResponse> without = await SanctionActions.CreateAsync(new OneContextFactory(probe), admin, player,
				new CreateSanctionRequest { Type = SanctionType.Ban, Reason = "toxic" }, _ => Task.FromResult<Failure?>(null));
			Assert.True(without.Ok, without.Code);
			Result<CreateSanctionResponse> invalid = await SanctionActions.CreateAsync(new OneContextFactory(probe), admin, player,
				new CreateSanctionRequest { Type = SanctionType.Kick, Context = new SanctionContext { ChannelId = 1, LobbyId = 2 } }, _ => Task.FromResult<Failure?>(null));
			Assert.False(invalid.Ok);
			Assert.Equal(ErrorCodes.InvalidArgument, invalid.Code);

			AuditLogEntry mute = await db.AuditLog.AsNoTracking().SingleAsync(e => e.Action == SanctionTypes.Mute);
			JsonElement details = JsonDocument.Parse(mute.Details!).RootElement;
			Assert.Equal("match:77", details.GetProperty("context").GetString());
			Assert.Equal(SanctionTypes.Mute, details.GetProperty("type").GetString());
			AuditLogEntry ban = await db.AuditLog.AsNoTracking().SingleAsync(e => e.Action == SanctionTypes.Ban);
			Assert.False(JsonDocument.Parse(ban.Details!).RootElement.TryGetProperty("context", out _));
		}

		[Fact]
		public async Task AMatchLineGoesToTheLobbyOfThatMatchUnderTheMatchConversation()
		{
			Lobby[] lobbies = [NewLobby(10, 76), NewLobby(11, 77)];

			Assert.True(await SanctionNotice.AnnounceAsync(new SanctionPlace(SanctionPlaceKind.Match, 77), Host, "line", lobbies, Record));

			(string group, Message message) = Assert.Single(m_sent);
			Assert.Equal("lobby:11", group);
			Assert.Equal("match:77", message.ChannelId);
			Assert.Null(message.Author);
		}

		[Fact]
		public async Task NothingGoesToAMatchTheStaffMemberIsNotIn()
		{
			Lobby[] lobbies = [NewLobby(10, 77)];

			Assert.False(await SanctionNotice.AnnounceAsync(new SanctionPlace(SanctionPlaceKind.Match, 77), Outsider, "line", lobbies, Record));
			Assert.False(await SanctionNotice.AnnounceAsync(new SanctionPlace(SanctionPlaceKind.Match, 78), Host, "line", lobbies, Record));
			Assert.Empty(m_sent);
		}
	}
}
