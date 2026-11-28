using System.Text.Json;
using GenOnlineService.Portal.Client;
using GenOnlineService.Realtime;
using GenOnlineService.Rest;
using Xunit;

namespace GenOnlineService.Tests;

public class ChatLogTests
{
	private static (ChatLogFilter? Filter, Failure? Failure) Read(string? conversation = null, string? userID = null, string? from = null, string? to = null, string? beforeID = null, string? limit = null) =>
		StaffChatLogs.ReadFilter(conversation, userID, from, to, beforeID, limit);

	private static string Field(Failure? failure) => ((JsonElement)JsonSerializer.SerializeToElement(failure!.Value.Params, RealtimeJson.Create())).GetProperty("field").GetString()!;

	[Theory]
	[InlineData("channel:5", "channel", 5L)]
	[InlineData("lobby:12", "lobby", 12L)]
	[InlineData("match:9", "match", 9L)]
	public void PublicConversationsAreAccepted(string conversation, string kind, long id)
	{
		(ChatLogFilter? filter, Failure? failure) = Read(conversation);
		Assert.Null(failure);
		Assert.Equal(kind, filter!.Kind);
		Assert.Equal(id, filter.ConversationID);
	}

	[Theory]
	[InlineData("dm:4")]
	[InlineData("group:4")]
	[InlineData("room:4")]
	[InlineData("channel:")]
	[InlineData("channel:-1")]
	[InlineData("channel:x")]
	[InlineData("match")]
	public void DirectGroupAndUnknownConversationsAreRefused(string conversation)
	{
		(_, Failure? failure) = Read(conversation, userID: "1");
		Assert.Equal(ErrorCodes.InvalidArgument, failure!.Value.Code);
		Assert.Equal("conversation", Field(failure));
	}

	[Fact]
	public void APlayerAloneSpansOnlyPublicKinds()
	{
		(ChatLogFilter? filter, Failure? failure) = Read(userID: "77");
		Assert.Null(failure);
		Assert.Null(filter!.Kind);
		Assert.Equal(77, filter.UserID);
	}

	[Fact]
	public void SomeFilterIsRequired()
	{
		Assert.Equal("conversation", Field(Read().Failure));
	}

	[Theory]
	[InlineData("0")]
	[InlineData("-3")]
	[InlineData("abc")]
	public void ABadPlayerIdIsInvalid(string userID)
	{
		Assert.Equal("user_id", Field(Read("channel:1", userID).Failure));
	}

	[Fact]
	public void TheLimitDefaultsToFiftyAndStopsAtTwoHundred()
	{
		Assert.Equal(50, Read("channel:1").Filter!.Limit);
		Assert.Equal(200, Read("channel:1", limit: "200").Filter!.Limit);
		Assert.Equal(1, Read("channel:1", limit: "1").Filter!.Limit);
	}

	[Theory]
	[InlineData("0")]
	[InlineData("201")]
	[InlineData("-1")]
	[InlineData("many")]
	public void ALimitOutsideTheBoundsIsInvalid(string limit)
	{
		Assert.Equal("limit", Field(Read("channel:1", limit: limit).Failure));
	}

	[Fact]
	public void TimesAndTheKeysetCursorAreRead()
	{
		ChatLogFilter filter = Read("lobby:1", from: "2026-11-01", to: "2026-11-02", beforeID: "900").Filter!;
		Assert.Equal(new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc), filter.From);
		Assert.Equal(new DateTime(2026, 11, 3, 0, 0, 0, DateTimeKind.Utc), filter.To);
		Assert.Equal(900, filter.BeforeID);
	}

	[Theory]
	[InlineData("from", "yesterday")]
	[InlineData("to", "2026-13-40")]
	[InlineData("before_id", "0")]
	[InlineData("before_id", "x")]
	public void BadTimesAndCursorsAreInvalid(string field, string value)
	{
		(_, Failure? failure) = field switch
		{
			"from" => Read("channel:1", from: value),
			"to" => Read("channel:1", to: value),
			_ => Read("channel:1", beforeID: value)
		};
		Assert.Equal(field, Field(failure));
	}

	[Fact]
	public void TheAuditDetailsCarryTheFilterAndNoText()
	{
		ChatLogFilter filter = Read("match:9", "4", beforeID: "12", limit: "20").Filter!;
		string details = JsonSerializer.Serialize(StaffChatLogs.AuditDetails(filter));
		Assert.Contains("\"conversation\":\"match:9\"", details);
		Assert.Contains("\"user_id\":4", details);
		Assert.Contains("\"before_id\":12", details);
		Assert.Contains("\"limit\":20", details);
		Assert.DoesNotContain("text", details);
	}

	[Fact]
	public void ALineShowsItsConversationScopeAndStaysReadableWhenDeleted()
	{
		ChatMessage row = new() { ID = 8, Kind = ChatMessageKinds.Match, MatchID = 9, LobbyID = 2, SenderID = 4, Type = ChatMessageTypes.Chat, Content = "gg", Scope = ChatMessageScopes.Allies, CreatedAt = new DateTime(2026, 11, 28, 1, 0, 0, DateTimeKind.Utc), DeletedAt = new DateTime(2026, 11, 28, 2, 0, 0, DateTimeKind.Utc) };
		PortalChatLogLine line = StaffChatLogs.LineOf(row, new Dictionary<long, (string Name, int Tag)> { [4] = ("Ann", 7) });
		Assert.Equal("match:9", line.Conversation);
		Assert.Equal("allies", line.Scope);
		Assert.Equal("gg", line.Text);
		Assert.True(line.Deleted);
		Assert.Equal(4, line.Sender.Id);
		Assert.Equal("Ann", line.Sender.Name);
		Assert.Equal("0007", line.Sender.Tag);
	}

	[Fact]
	public void ChannelLinesCarryNoScope()
	{
		ChatMessage row = new() { ID = 8, Kind = ChatMessageKinds.Channel, ChannelID = 3, SenderID = 4, Type = ChatMessageTypes.Chat, Content = "hi", CreatedAt = DateTime.UtcNow };
		Assert.Null(StaffChatLogs.LineOf(row, new Dictionary<long, (string Name, int Tag)>()).Scope);
		Assert.DoesNotContain("scope", JsonSerializer.Serialize(StaffChatLogs.LineOf(row, new Dictionary<long, (string Name, int Tag)>()), RealtimeJson.Create()));
	}

	[Fact]
	public void TheChatLogsPermissionIsAHumanOneWithACatalogEntry()
	{
		Assert.Equal("chat.logs", Permissions.ChatLogs);
		Assert.Contains(Permissions.ChatLogs, Permissions.Human);
		Assert.DoesNotContain(Permissions.ChatLogs, Permissions.Machine);
		(string Name, string Area, string Label, string Description) entry = PermissionCatalog.Entries.Single(e => e.Name == Permissions.ChatLogs);
		Assert.Equal("Chat", entry.Area);
		Assert.Equal("Read chat logs", entry.Label);
		Assert.Contains("never direct or group", entry.Description);
		Assert.Equal(Permissions.ChatLogs, PortalPermissions.ChatLogs);
	}

	[Fact]
	public void TheAuditActionHasAWordLabelInThePortal()
	{
		Assert.Equal("chat_log_view", ModerationActions.ChatLogView);
		Assert.Contains("chat_log_view", PortalAuditActions.All);
		Assert.NotEqual("chat_log_view", PortalAuditActions.LabelOf("chat_log_view"));
	}
}
