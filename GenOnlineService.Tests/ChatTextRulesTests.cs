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
		Assert.Equal(outcome == TextOutcomes.Refuse, TextChecks.RefusedNameWord(name) != null);
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
}
