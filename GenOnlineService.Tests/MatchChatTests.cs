using System.Text.Json;
using GenOnlineService.Api;
using GenOnlineService.Realtime;
using Xunit;

namespace GenOnlineService.Tests;

public class MatchChatTests
{
	private static readonly DateTime s_start = new(2026, 11, 28, 12, 0, 0, DateTimeKind.Utc);

	private static SendMatchChatRequest Request(string? text = "gg", string? scope = "everyone", params long[] recipients) =>
		new() { Text = text, Scope = scope, RecipientUserIds = recipients };

	[Fact]
	public void TextIsTrimmedAndKeptWithinTheChatLimit()
	{
		Assert.Null(MatchChat.Validate(Request("  gg  "), out string text, out _, out _));
		Assert.Equal("gg", text);
		Assert.Null(MatchChat.Validate(Request(new string('a', RealtimeHub.ChatMaxLength)), out _, out _, out _));
	}

	[Fact]
	public void TextOverTheLimitIsRefusedWithTheMaximum()
	{
		Failure failure = MatchChat.Validate(Request(new string('a', RealtimeHub.ChatMaxLength + 1)), out _, out _, out _)!.Value;
		Assert.Equal(ErrorCodes.ChatTooLong, failure.Code);
		Assert.Equal("{\"max\":140}", JsonSerializer.Serialize(failure.Params, RealtimeJson.Create()));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void EmptyTextIsInvalid(string? text)
	{
		Failure failure = MatchChat.Validate(Request(text), out _, out _, out _)!.Value;
		Assert.Equal(ErrorCodes.InvalidArgument, failure.Code);
		Assert.Equal("{\"field\":\"text\"}", JsonSerializer.Serialize(failure.Params, RealtimeJson.Create()));
	}

	[Theory]
	[InlineData("everyone")]
	[InlineData("allies")]
	[InlineData("self")]
	public void EachScopeIsAccepted(string scope)
	{
		Assert.Null(MatchChat.Validate(Request(scope: scope), out _, out string parsed, out _));
		Assert.Equal(scope, parsed);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("Everyone")]
	[InlineData("team")]
	public void UnknownScopeIsInvalid(string? scope)
	{
		Failure failure = MatchChat.Validate(Request(scope: scope), out _, out _, out _)!.Value;
		Assert.Equal(ErrorCodes.InvalidArgument, failure.Code);
		Assert.Equal("{\"field\":\"scope\"}", JsonSerializer.Serialize(failure.Params, RealtimeJson.Create()));
	}

	[Fact]
	public void RecipientsAreDeduplicatedInOrder()
	{
		Assert.Null(MatchChat.Validate(Request(scope: "allies", recipients: [5, 3, 5, 3, 9]), out _, out _, out long[] recipients));
		Assert.Equal([5L, 3L, 9L], recipients);
	}

	[Fact]
	public void NoRecipientsListMeansNone()
	{
		Assert.Null(MatchChat.Validate(new SendMatchChatRequest { Text = "gg", Scope = "self" }, out _, out _, out long[] recipients));
		Assert.Empty(recipients);
	}

	[Fact]
	public void PlayersOfTheMatchMayChatWithinTheWindow()
	{
		Assert.Null(MatchChat.CheckRoster(s_start, s_start.AddHours(23), [1, 2, 3], 1, [2, 3]));
	}

	[Fact]
	public void ANonPlayerGetsMatchNotFound()
	{
		Failure failure = MatchChat.CheckRoster(s_start, s_start, [1, 2], 9, [])!.Value;
		Assert.Equal(ErrorCodes.NotFound, failure.Code);
		Assert.Equal("{\"what\":\"match\"}", JsonSerializer.Serialize(failure.Params, RealtimeJson.Create()));
	}

	[Fact]
	public void AnUnknownMatchAndAnExpiredWindowGetMatchNotFound()
	{
		Assert.Equal(ErrorCodes.NotFound, MatchChat.CheckRoster(null, s_start, [], 1, [])!.Value.Code);
		Assert.Equal(ErrorCodes.NotFound, MatchChat.CheckRoster(s_start, s_start.AddHours(24).AddSeconds(1), [1, 2], 1, [])!.Value.Code);
	}

	[Theory]
	[InlineData(1L)]
	[InlineData(7L)]
	public void RecipientsOutsideTheMatchOrTheSenderAreInvalid(long recipient)
	{
		Failure failure = MatchChat.CheckRoster(s_start, s_start, [1, 2], 1, [2, recipient])!.Value;
		Assert.Equal(ErrorCodes.InvalidArgument, failure.Code);
		Assert.Equal("{\"field\":\"recipient_user_ids\"}", JsonSerializer.Serialize(failure.Params, RealtimeJson.Create()));
	}

	[Fact]
	public void TwentyLinesPassAndTheNextOneWaits()
	{
		SharedUserData user = new(7, new UserSocialContainer(), "n", 1, new PlayerStats(7, 1000, 0, 1000));
		for (int i = 0; i < 20; i++)
		{
			Assert.True(ChatRateLimiter.Match.TryConsume(user, out _));
		}
		Assert.False(ChatRateLimiter.Match.TryConsume(user, out long retryAfterMs));
		Assert.InRange(retryAfterMs, 1, 10_000);
	}

	[Fact]
	public void TheMatchLimitDoesNotSpendTheChannelBudget()
	{
		SharedUserData user = new(8, new UserSocialContainer(), "n", 1, new PlayerStats(8, 1000, 0, 1000));
		for (int i = 0; i < 20; i++)
		{
			ChatRateLimiter.Match.TryConsume(user, out _);
		}
		Assert.True(ChatRateLimiter.Channel.TryConsume(user, out _));
	}

	[Fact]
	public void MutedSenderFailsLikeTheRealtimeChat()
	{
		DateTime expires = new(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc);
		Failure timed = RealtimeHub.MutedFailure(new Sanction { ExpiresAt = expires });
		Assert.Equal(ErrorCodes.ChatMuted, timed.Code);
		Assert.Equal("{\"expires_at\":\"2026-12-01T00:00:00Z\"}", JsonSerializer.Serialize(timed.Params, RealtimeJson.Create()));
		Assert.Equal("{}", JsonSerializer.Serialize(RealtimeHub.MutedFailure(new Sanction()).Params, RealtimeJson.Create()));
	}

	[Theory]
	[InlineData("match:42", "match", 42L)]
	[InlineData("channel:3", "channel", 3L)]
	[InlineData("lobby:9", "lobby", 9L)]
	public void ConversationIdsParseWithTheirKind(string id, string kind, long target)
	{
		Assert.True(RealtimeHub.TryParseChannel(id, out string parsedKind, out long parsedID));
		Assert.Equal(kind, parsedKind);
		Assert.Equal(target, parsedID);
	}

	[Theory]
	[InlineData("match:")]
	[InlineData("match:-1")]
	[InlineData("match:x")]
	[InlineData("matches:1")]
	public void BadMatchConversationIdsAreRefused(string id) => Assert.False(RealtimeHub.TryParseChannel(id, out _, out _));

	[Fact]
	public void OnlyTheMatchConversationIsRestOnly()
	{
		Assert.True(RealtimeHub.IsMirrorOnly("match"));
		Assert.False(RealtimeHub.IsMirrorOnly("channel"));
		Assert.False(RealtimeHub.IsMirrorOnly("lobby"));
		Assert.False(RealtimeHub.IsMirrorOnly("dm"));
		Assert.False(RealtimeHub.IsMirrorOnly("group"));
	}

	[Fact]
	public void AStoredMatchLineIsOnTheMatchConversation()
	{
		ChatMessage row = new() { ID = 5, Kind = ChatMessageKinds.Match, MatchID = 42, LobbyID = 3, SenderID = 1, Type = ChatMessageTypes.Chat, Content = "gg", CreatedAt = s_start };
		Message wire = RealtimeHub.ToWire(row, 2, new Dictionary<long, (string Name, int Tag)> { [1] = ("A", 1) });
		Assert.Equal("match:42", wire.ChannelId);
		Assert.Equal("gg", wire.Content);
	}

	[Fact]
	public void RecipientsRoundTripThroughTheirStoredForm()
	{
		Assert.Null(Database.Chat.EncodeMentions([]));
		Assert.Equal([5L, 3L], Database.Chat.DecodeMentions(Database.Chat.EncodeMentions([5, 3])));
	}

	[Fact]
	public void ALineOfAPlayerWhoOwesTheAgreementsIsNotStored()
	{
		Failure? owed = MatchChat.CheckAgreements([new GenOnlineService.Identity.AgreementDocument { Name = "terms", Version = "2026-11-28" }]);
		Assert.NotNull(owed);
		Assert.Equal(ErrorCodes.AgreementsOwed, owed!.Value.Code);
		Assert.Null(MatchChat.CheckAgreements([]));
	}
}
