using Database;
using GenOnlineService.Failover;
using GenOnlineService.Portal.Client;
using GenOnlineService.Realtime;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GenOnlineService.Tests
{
	public class IllegalRequestTests
	{
		private static Lobby NewLobby(ELobbyType type = ELobbyType.CustomGame, ELobbyState state = ELobbyState.GAME_SETUP) => Lobby.FromLive(new LobbyLive
		{
			Id = 10,
			Owner = 1,
			Name = "lobby",
			State = (int)state,
			Type = (int)type,
			Slots = Enumerable.Range(0, Lobby.MaxLobbySize).Select(index => new LobbyMemberLive
			{
				SlotIndex = index,
				SlotState = (int)(index == 0 ? EPlayerType.SLOT_PLAYER : EPlayerType.SLOT_OPEN),
				UserId = index == 0 ? 1 : -1
			}).ToList()
		});

		[Fact]
		public void TheRowNamesTheRequestWhatWasAttemptedAndTheState()
		{
			DateTime now = new(2026, 11, 28, 12, 0, 0, DateTimeKind.Utc);
			AnticheatIllegalRequest row = RealtimeLobbies.IllegalRequestOf(NewLobby(ELobbyType.QuickMatch, ELobbyState.INGAME), 7, nameof(RealtimeHub.UpdateMySlot),
				RealtimeLobbies.DescribeEdit(new UpdateMySlotRequest { Color = 3 }), now);

			Assert.Equal((7L, 10L, 0L), (row.UserId, row.LobbyId, row.MatchId));
			Assert.Equal("UpdateMySlot", row.Request);
			Assert.Equal("own slot: color 3", row.Detail);
			Assert.Equal("INGAME, quick match", row.State);
			Assert.Equal(now, row.CreatedAt);
		}

		[Fact]
		public void TheTextsStayWithinTheColumnLengths()
		{
			AnticheatIllegalRequest row = RealtimeLobbies.IllegalRequestOf(NewLobby(), 7, new string('r', 100), new string('d', 500), DateTime.UtcNow);

			Assert.Equal(AnticheatIllegalRequestConfiguration.RequestMaxLength, row.Request.Length);
			Assert.Equal(AnticheatIllegalRequestConfiguration.DetailMaxLength, row.Detail.Length);
			Assert.True(row.State.Length <= AnticheatIllegalRequestConfiguration.StateMaxLength);
		}

		[Fact]
		public void TheDescriptionsUseFixedWordsAndGameNumbers()
		{
			Assert.Equal("lobby edit: map, visibility, starting cash", RealtimeLobbies.DescribeEdit(new UpdateLobbyRequest
			{
				Map = new UpdateLobbyMap { Name = "free text", Path = "p", MaxPlayers = 2 },
				Visibility = LobbyVisibility.Friends,
				Settings = new UpdateLobbySettings { StartingCash = 5000 }
			}));
			Assert.Equal("own slot: side 2, team 1", RealtimeLobbies.DescribeEdit(new UpdateMySlotRequest { Side = 2, Team = 1 }));
			Assert.Equal("slot edit of 2 slots: color, start position", RealtimeLobbies.DescribeEdit(new UpdateSlotsRequest
			{
				Slots = [new SlotPatch { Index = 1, Color = 2 }, new SlotPatch { Index = 2, StartPosition = 3 }]
			}));
		}

		[Fact]
		public void ALobbyMarksEachPlayerAndRequestOnce()
		{
			Lobby lobby = NewLobby();

			Assert.True(lobby.TryMarkIllegalRequestReported(1, "UpdateMySlot"));
			Assert.False(lobby.TryMarkIllegalRequestReported(1, "UpdateMySlot"));
			Assert.True(lobby.TryMarkIllegalRequestReported(1, "UpdateSlots"));
			Assert.True(lobby.TryMarkIllegalRequestReported(2, "UpdateMySlot"));
		}

		[Fact]
		public void TheRefusalsKeepTheirErrorCodes()
		{
			Assert.Equal(ErrorCodes.LobbyNotHost, RealtimeLobbies.RefuseQuickMatchPassword(NewLobby(ELobbyType.QuickMatch))?.Code);
			Assert.Equal(ErrorCodes.LobbyNotHost, RealtimeLobbies.RefuseQuickMatchSettings(NewLobby(ELobbyType.QuickMatch), new UpdateLobbyRequest { Visibility = LobbyVisibility.Friends })?.Code);
		}

		[Fact]
		public void NoListOrCaseKindNamesTheDroppedKinds()
		{
			string[] dropped = ["new_accounts", "quickmatch_rules"];

			Assert.DoesNotContain(PortalReviewList.All, list => dropped.Contains(list.Kind));
			Assert.DoesNotContain(PortalCaseKinds.All, kind => dropped.Contains(kind));
			Assert.DoesNotContain(AntiCheatReviewKinds.All, kind => dropped.Contains(kind));
			Assert.Equal("Illegal requests", PortalReviewList.Find("illegal_requests")?.Title);
			Assert.Equal(PortalReviewList.All.Select(list => list.Kind).Order(), AntiCheatReviewKinds.All.Order());
		}

		[ProbeTheory]
		[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
		public async Task ADatabaseKeepsOneRowPerPlayerLobbyAndRequest(string provider)
		{
			await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
			if (probe == null) return;

			Lobby lobby = NewLobby();
			await using (AppDbContext db = probe.Open())
			{
				Assert.True(await Database.AntiCheat.FlagAccountForReview_IllegalRequest(db, RealtimeLobbies.IllegalRequestOf(lobby, 7, "UpdateSlots", "slot edit of 1 slots: color", DateTime.UtcNow)));
			}
			await using (AppDbContext db = probe.Open())
			{
				Assert.True(await Database.AntiCheat.FlagAccountForReview_IllegalRequest(db, RealtimeLobbies.IllegalRequestOf(lobby, 7, "UpdateSlots", "slot edit of 1 slots: side", DateTime.UtcNow)));
			}
			await using (AppDbContext db = probe.Open())
			{
				Assert.True(await Database.AntiCheat.FlagAccountForReview_IllegalRequest(db, RealtimeLobbies.IllegalRequestOf(lobby, 7, "StartGame", "start", DateTime.UtcNow)));
			}

			await using AppDbContext read = probe.Open();
			List<AnticheatIllegalRequest> rows = await read.AnticheatIllegalRequests.OrderBy(r => r.ReportId).ToListAsync();
			Assert.Equal(["UpdateSlots", "StartGame"], rows.Select(r => r.Request));
			Assert.Equal("slot edit of 1 slots: color", rows[0].Detail);

			Assert.Equal(2, await AntiCheatReviews.Rows(read, AntiCheatReviewKinds.IllegalRequests).CountAsync(r => r.UserID == 7 && r.ReviewedAt == null));
			Assert.Equal(1, await AntiCheatReviews.Decide(read, AntiCheatReviewKinds.IllegalRequests, [rows[0].ReportId], 7, 99, 5, DateTime.UtcNow));
		}
	}
}
