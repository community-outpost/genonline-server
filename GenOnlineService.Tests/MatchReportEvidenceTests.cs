using GenOnlineService.Realtime;
using Xunit;

namespace GenOnlineService.Tests;

public class MatchReportEvidenceTests
{
	private static readonly DateTime s_at = new(2026, 11, 28, 12, 0, 0, DateTimeKind.Utc);

	private static ChatMessage Line(long id, string scope, string? recipients = null, long matchID = 7, string kind = ChatMessageKinds.Match) => new()
	{
		ID = id, Kind = kind, MatchID = matchID, SenderID = 2, Type = ChatMessageTypes.Chat, Content = "line " + id, Scope = scope, Recipients = recipients, CreatedAt = s_at.AddSeconds(id)
	};

	[Fact]
	public void EveryoneLinesAreVisibleToAnyReporter()
	{
		Assert.Equal([1L], MessageActions.VisibleMatchLines([Line(1, ChatMessageScopes.Everyone)], 1, 7).Select(m => m.ID));
	}

	[Fact]
	public void AllyLinesNeedTheReporterAmongTheRecipients()
	{
		ChatMessage[] lines = [Line(1, ChatMessageScopes.Allies, "1,3"), Line(2, ChatMessageScopes.Allies, "3,4"), Line(3, ChatMessageScopes.Allies)];
		Assert.Equal([1L], MessageActions.VisibleMatchLines(lines, 1, 7).Select(m => m.ID));
	}

	[Fact]
	public void SelfLinesStayHiddenFromTheReporter()
	{
		Assert.Empty(MessageActions.VisibleMatchLines([Line(1, ChatMessageScopes.Self), Line(2, ChatMessageScopes.Self, "3")], 1, 7));
	}

	[Fact]
	public void OtherMatchesAndOtherKindsAreLeftOut()
	{
		Assert.Empty(MessageActions.VisibleMatchLines([Line(1, ChatMessageScopes.Everyone, matchID: 8), Line(2, ChatMessageScopes.Everyone, kind: ChatMessageKinds.Lobby)], 1, 7));
	}

	[Fact]
	public void TheLatestTenVisibleLinesStayOldestFirst()
	{
		ChatMessage[] lines = Enumerable.Range(1, 15).Select(i => Line(i, ChatMessageScopes.Everyone)).ToArray();
		Assert.Equal(Enumerable.Range(6, 10).Select(i => (long)i), MessageActions.VisibleMatchLines(lines, 1, 7).Select(m => m.ID));
	}

	[Fact]
	public void MatchLinesTravelWithTheReportAndReadBack()
	{
		string json = Database.Chat.PlayerEvidenceJson([], [Line(4, ChatMessageScopes.Allies, "1")]);
		Database.Chat.ReportEvidence read = Database.Chat.ReadEvidence(new PlayerReport { Evidence = json });
		Database.Chat.ReportEvidenceMessage line = Assert.Single(read.MatchMessages);
		Assert.Equal(4, line.ID);
		Assert.Equal("line 4", line.Content);
		Assert.Equal(ChatMessageScopes.Allies, line.Scope);
		Assert.Empty(read.Messages);
	}

	[Fact]
	public void ReportsWithoutMatchLinesKeepTheirStoredShape()
	{
		ChatMessage channel = Line(1, ChatMessageScopes.Everyone, kind: ChatMessageKinds.Channel);
		channel.Scope = null;
		string json = Database.Chat.PlayerEvidenceJson([channel], []);
		Assert.DoesNotContain("match_messages", json);
		Assert.DoesNotContain("scope", json);
		Database.Chat.ReportEvidence read = Database.Chat.ReadEvidence(new PlayerReport { Evidence = json });
		Assert.Single(read.Messages);
		Assert.Empty(read.MatchMessages);
	}

	[Fact]
	public void EvidenceStoredBeforeMatchLinesStillReads()
	{
		const string old = "{\"messages\":[{\"id\":1,\"kind\":\"channel\",\"sender_id\":2,\"type\":\"chat\",\"content\":\"hi\",\"created_at\":\"2026-11-01T00:00:00Z\"}]}";
		Database.Chat.ReportEvidence read = Database.Chat.ReadEvidence(new PlayerReport { Evidence = old });
		Assert.Equal("hi", Assert.Single(read.Messages).Content);
		Assert.Empty(read.MatchMessages);
	}
}
