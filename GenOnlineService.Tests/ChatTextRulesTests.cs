using System.Text.Json;
using GenOnlineService.Realtime;
using GenOnlineService.TextRules;
using Xunit;

namespace GenOnlineService.Tests;

public class ChatTextRulesTests
{
	// "shit" is a flag word, "fuck" a block word in the seed lists the defaults carry
	[Theory]
	[InlineData("what the shit")]
	[InlineData("SHIT happens")]
	public void FlagWordsDoNotRefuseChat(string text)
	{
		Assert.Null(TextChecks.ChatRefusal(text));
	}

	[Fact]
	public void BlockWordsStillRefuseChat()
	{
		Assert.Equal(TextChecks.ChatBlocked, TextChecks.ChatRefusal("you fuck")!.Value.Code);
	}

	[Fact]
	public void CleanChatPasses()
	{
		Assert.Null(TextChecks.ChatRefusal("good game"));
	}

	[Theory]
	[InlineData("fuck", TextOutcomes.Refuse)]
	[InlineData("shitlord", TextOutcomes.Refuse)]
	[InlineData("admin", TextOutcomes.Refuse)]
	[InlineData("Gandalf", TextOutcomes.Allow)]
	public void DisplayNamesAreCheckedAsBefore(string name, string outcome)
	{
		Assert.Equal(outcome, TextChecks.Check(TextKinds.DisplayName, name).Outcome);
		Assert.Equal(outcome == TextOutcomes.Refuse, TextChecks.NameRefusal(name) != null);
	}

	[Theory]
	[InlineData("fuck lobby", true)]
	[InlineData("shit lobby", false)]
	[InlineData("friendly lobby", false)]
	public void LobbyNamesAreCheckedAsBefore(string name, bool refused)
	{
		Failure? failure = TextChecks.LobbyNameRefusal(name);
		Assert.Equal(refused, failure != null);
		if (refused)
		{
			Assert.Equal(ErrorCodes.NameReserved, failure!.Value.Code);
		}
	}

	private static string Json(object parameters) => JsonSerializer.Serialize(parameters, RealtimeJson.Create());

	private static TextRuleSet SetWithChat(TextRuleDefinition rule, params TextRuleTerm[] terms)
	{
		var workflows = new Dictionary<string, TextWorkflowDefinition>(TextRuleWorkflows.Defaults) { [TextKinds.Chat] = new TextWorkflowDefinition { Rules = [rule] } };
		return new TextRuleSet(terms.Select(TextChecks.Info), [], new TextRuleWorkflows(workflows), "test");
	}

	[Fact]
	public void ChatRefusalCarriesTheCategoryAndNeverTheWord()
	{
		Failure refusal = TextChecks.ChatRefusal("you fuck")!.Value;
		Assert.Equal("{\"category\":\"profanity\"}", Json(refusal.Params));
	}

	[Fact]
	public void RefusalCarriesTheStaffMessageOfTheDecidingRule()
	{
		TextRuleSet set = SetWithChat(
			new TextRuleDefinition { Name = "blocked words", Expression = "input.Block > 0", Outcome = TextOutcomes.Block, Message = "Please keep the chat friendly." },
			new TextRuleTerm { Term = "heck", Severity = TextSeverities.Block, Category = TextCategories.Insult, Match = TextMatchModes.Word });
		Failure refusal = TextChecks.Refusal(TextChecks.ChatBlocked, set.Check(TextKinds.Chat, "oh heck no"));
		Assert.Equal("{\"category\":\"insult\",\"message\":\"Please keep the chat friendly.\"}", Json(refusal.Params));
	}

	[Fact]
	public void RefusalCategoryIsOtherWhenOnlyNameWordsMatched()
	{
		TextRuleSet set = SetWithChat(
			new TextRuleDefinition { Name = "any", Expression = "input.Matches > 0", Outcome = TextOutcomes.Block },
			new TextRuleTerm { Term = "heck", Severity = TextSeverities.Block, Category = TextCategories.Reserved, Match = TextMatchModes.Word });
		TextCheck check = set.Check(TextKinds.Chat, "oh heck no");
		Assert.Equal(TextOutcomes.Block, check.Outcome);
		Assert.Equal("{\"category\":\"other\"}", Json(TextChecks.Refusal(TextChecks.ChatBlocked, check).Params));
	}

	[Theory]
	[InlineData("fuck", "profanity")]
	[InlineData("admin", "impersonation")]
	public void DisplayNameRefusalCarriesTheCategory(string name, string category)
	{
		Failure refusal = TextChecks.NameRefusal(name)!.Value;
		Assert.Equal(ErrorCodes.NameReserved, refusal.Code);
		Assert.Equal("{\"category\":\"" + category + "\"}", Json(refusal.Params));
	}

	[Fact]
	public void LobbyNameRefusalCarriesTheCategoryAndNeverTheWord()
	{
		Failure refusal = TextChecks.LobbyNameRefusal("fuck lobby")!.Value;
		Assert.Equal("{\"category\":\"profanity\"}", Json(refusal.Params));
	}

	[Fact]
	public void RuleMessageRoundTripsAndStaysOutOfTheDefaults()
	{
		Assert.DoesNotContain("message", TextRuleWorkflows.Serialize(TextRuleWorkflows.Defaults[TextKinds.Chat]));
		var definition = new TextWorkflowDefinition { Rules = [new TextRuleDefinition { Name = "a", Expression = "input.Block > 0", Outcome = TextOutcomes.Block, Message = "Be kind." }] };
		(TextWorkflowDefinition? parsed, string? error) = TextRuleWorkflows.Parse(TextKinds.Chat, TextRuleWorkflows.Serialize(definition));
		Assert.Null(error);
		Assert.Equal("Be kind.", parsed!.Rules[0].Message);
	}

	[Theory]
	[InlineData(201)]
	[InlineData(0)]
	public void RuleMessageLengthIsBounded(int length)
	{
		var definition = new TextWorkflowDefinition { Rules = [new TextRuleDefinition { Name = "a", Expression = "input.Block > 0", Outcome = TextOutcomes.Block, Message = new string('x', length) }] };
		Assert.NotNull(TextRuleWorkflows.Validate(TextKinds.Chat, definition));
		Assert.Null(TextRuleWorkflows.Validate(TextKinds.Chat, definition with { Rules = [definition.Rules[0] with { Message = new string('x', 200) }] }));
	}

	[Fact]
	public void RuleMessageIsPlainText()
	{
		var definition = new TextWorkflowDefinition { Rules = [new TextRuleDefinition { Name = "a", Expression = "input.Block > 0", Outcome = TextOutcomes.Block, Message = "line\nbreak" }] };
		Assert.NotNull(TextRuleWorkflows.Validate(TextKinds.Chat, definition));
	}
}
