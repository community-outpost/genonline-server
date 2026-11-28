using System.Text.Json;
using GenOnlineService.Realtime;
using GenOnlineService.TextRules;
using Xunit;

namespace GenOnlineService.Tests;

public class ChatBlockScopeTests
{
	private static string Json(object parameters) => JsonSerializer.Serialize(parameters, RealtimeJson.Create());

	private static TextRuleTerm Term(string term, string category, string severity = TextSeverities.Block, string source = TextRuleSources.Seed, string? level = null) =>
		new() { Term = term, Category = category, Severity = severity, Source = source, Level = level ?? TextLevels.OfCategory(category), Match = TextMatchModes.Word };

	private static TextRuleSet Set(TextRuleTerm term, TextWorkflowDefinition? chat = null)
	{
		var workflows = new Dictionary<string, TextWorkflowDefinition>(TextRuleWorkflows.Defaults);
		if (chat != null)
		{
			workflows[TextKinds.Chat] = chat;
		}
		return new TextRuleSet([TextChecks.Info(term)], [], new TextRuleWorkflows(workflows), "test");
	}

	private static Failure? Refusal(TextRuleSet set, string text)
	{
		TextCheck check = set.Check(TextKinds.Chat, text);
		return check.Outcome == TextOutcomes.Block ? TextChecks.Refusal(TextChecks.ChatBlocked, check) : null;
	}

	// The name checks read the severity and the category as before, whatever the level

	[Theory]
	[InlineData("porn", "sexual")]
	[InlineData("fuck", "profanity")]
	[InlineData("kkk", "slur")]
	public void DisplayNameRefusalsKeepTheirCategory(string name, string category)
	{
		Failure refusal = TextChecks.NameRefusal(name)!.Value;
		Assert.Equal(ErrorCodes.NameReserved, refusal.Code);
		Assert.Equal("{\"category\":\"" + category + "\"}", Json(refusal.Params));
	}

	[Theory]
	[InlineData("porn lobby", "sexual")]
	[InlineData("fuck lobby", "profanity")]
	[InlineData("kkk lobby", "slur")]
	public void LobbyNameRefusalsKeepTheirCategory(string name, string category)
	{
		Failure refusal = TextChecks.LobbyNameRefusal(name)!.Value;
		Assert.Equal(ErrorCodes.NameReserved, refusal.Code);
		Assert.Equal("{\"category\":\"" + category + "\"}", Json(refusal.Params));
	}

	[Fact]
	public void FlagWordsStillRefuseDisplayNamesAndNotLobbyNames()
	{
		Assert.NotNull(TextChecks.NameRefusal("shitlord"));
		Assert.Null(TextChecks.LobbyNameRefusal("shit lobby"));
	}

	// Chat

	[Theory]
	[InlineData("porn")]
	[InlineData("fuck you")]
	[InlineData("cunt")]
	public void SexualAndProfanityBlockWordsNoLongerRefuseChat(string text)
	{
		Assert.Null(TextChecks.ChatRefusal(text));
	}

	[Fact]
	public void AnExtremeBlockWordRefusesChatWithTheExtremeCategory()
	{
		Failure refusal = TextChecks.ChatRefusal("join the kkk")!.Value;
		Assert.Equal(TextChecks.ChatBlocked, refusal.Code);
		Assert.Equal("{\"category\":\"extreme\"}", Json(refusal.Params));
	}

	[Fact]
	public void AnExtremeFlagWordRefusesChatToo()
	{
		Failure refusal = TextChecks.ChatRefusal("nazi") ?? throw new Xunit.Sdk.XunitException("nazi was not refused");
		Assert.Equal(TextChecks.ChatBlocked, refusal.Code);
		Assert.Equal("{\"category\":\"extreme\"}", Json(refusal.Params));
		Assert.Null(TextChecks.ChatRefusal("the nazis lost"));
	}

	[Fact]
	public void AnExtremeFlagWordStaysInTheFilterList()
	{
		Assert.Contains(ChatFilterList.Of(TextChecks.Current).Terms, t => t.Text == "nazi" && t.Level == TextLevels.Extreme);
	}

	[Fact]
	public void TheRussianSlurMatchesOnlyTheWholeWord()
	{
		Assert.NotNull(TextChecks.ChatRefusal("хохол"));
		Assert.Null(TextChecks.ChatRefusal("хохолок"));
	}

	[Fact]
	public void AStaffBlockWordRefusesChatWithTheCustomCategory()
	{
		TextRuleSet set = Set(Term("heck", TextCategories.Insult, source: TextRuleSources.Staff));
		Assert.Equal("{\"category\":\"custom\"}", Json(Refusal(set, "oh heck no")!.Value.Params));
	}

	[Fact]
	public void AStaffFlagWordDoesNotRefuseChat()
	{
		Assert.Null(Refusal(Set(Term("heck", TextCategories.Insult, TextSeverities.Flag, TextRuleSources.Staff)), "oh heck no"));
	}

	[Fact]
	public void AStaffBlockWordAtTheExtremeLevelCountsAsExtreme()
	{
		TextRuleSet set = Set(Term("heck", TextCategories.Insult, source: TextRuleSources.Staff, level: TextLevels.Extreme));
		Assert.Equal("{\"category\":\"extreme\"}", Json(Refusal(set, "oh heck no")!.Value.Params));
	}

	[Fact]
	public void ASeedBlockWordBelowExtremeDoesNotRefuseChat()
	{
		Assert.Null(Refusal(Set(Term("heck", TextCategories.Insult)), "oh heck no"));
	}

	[Fact]
	public void AStaffRuleOfTheChatWorkflowBlocksWithTheCustomCategory()
	{
		var chat = new TextWorkflowDefinition { Rules = [new TextRuleDefinition { Name = "profanity", Expression = "input.Profanity > 0", Outcome = TextOutcomes.Block, Message = "Mind your language." }] };
		TextRuleSet set = Set(Term("heck", TextCategories.Profanity, TextSeverities.Flag), chat);
		Assert.Equal("{\"category\":\"custom\",\"message\":\"Mind your language.\"}", Json(Refusal(set, "oh heck no")!.Value.Params));
	}

	[Fact]
	public void TheDefaultChatRulesBlockExtremeAndCustomOnly()
	{
		Assert.Equal("input.Extreme > 0 || input.Custom > 0", TextRuleWorkflows.Defaults[TextKinds.Chat].Rules[0].Expression);
		Assert.Null(TextRuleWorkflows.Validate(TextKinds.Chat, TextRuleWorkflows.Defaults[TextKinds.Chat]));
	}

	[Fact]
	public void AStoredOldDefaultChatWorkflowIsUpgraded()
	{
		string old = TextRuleWorkflows.Serialize(new TextWorkflowDefinition
		{
			Rules =
			[
				new TextRuleDefinition { Name = "blocked words", Expression = "input.Block > 0", Outcome = TextOutcomes.Block },
				new TextRuleDefinition { Name = "flagged words", Expression = "input.Flag > 0", Outcome = TextOutcomes.Filter }
			]
		});
		Assert.Contains(TextRuleWorkflows.PreviousDefaults[TextKinds.Chat], p => TextRuleWorkflows.Serialize(p) == old);
		Assert.True(TextRuleSeeds.Version >= 3);
	}
}
