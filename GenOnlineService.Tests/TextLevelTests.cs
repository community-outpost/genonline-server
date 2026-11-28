using GenOnlineService.Realtime;
using GenOnlineService.TextRules;
using Xunit;

namespace GenOnlineService.Tests;

public class TextLevelTests
{
	[Theory]
	[InlineData(TextCategories.Slur, TextLevels.Extreme)]
	[InlineData(TextCategories.Sexual, TextLevels.Standard)]
	[InlineData(TextCategories.Insult, TextLevels.Standard)]
	[InlineData(TextCategories.Profanity, TextLevels.Strict)]
	[InlineData(TextCategories.Impersonation, null)]
	[InlineData(TextCategories.Reserved, null)]
	public void CategoryDecidesTheDefaultLevel(string category, string? level)
	{
		Assert.Equal(level, TextLevels.OfCategory(category));
	}

	[Fact]
	public void EverySeededTermHoldsTheLevelOfItsCategory()
	{
		Assert.All(TextRuleSeeds.Terms(), t => Assert.Equal(TextLevels.OfCategory(t.Category), t.Level));
		Assert.Contains(TextRuleSeeds.Terms(), t => t.Level == TextLevels.Extreme);
		Assert.Contains(TextRuleSeeds.Terms(), t => t.Level == TextLevels.Standard);
		Assert.Contains(TextRuleSeeds.Terms(), t => t.Level == TextLevels.Strict);
		Assert.Contains(TextRuleSeeds.Terms(), t => t.Level == null);
	}

	[Fact]
	public void InfoReadsTheLevelOfARowThatHoldsNone()
	{
		Assert.Equal(TextLevels.Strict, TextChecks.Info(new TextRuleTerm { Term = "heck", Category = TextCategories.Profanity }).Level);
		Assert.Equal(TextLevels.Extreme, TextChecks.Info(new TextRuleTerm { Term = "heck", Category = TextCategories.Profanity, Level = TextLevels.Extreme }).Level);
		Assert.Null(TextChecks.Info(new TextRuleTerm { Term = "heck", Category = TextCategories.Reserved }).Level);
	}

	[Fact]
	public void ANewTermTakesTheLevelOfItsCategoryWhenNoneIsGiven()
	{
		var term = new TextRuleTerm();
		Assert.Null(TextRulesAdmin.Apply(term, new Rest.TextTermRequest { Term = "heck", Language = "en", Category = "sexual", Severity = "block", Match = "word" }, true));
		Assert.Equal(TextLevels.Standard, term.Level);
	}

	[Fact]
	public void AGivenLevelWins()
	{
		var term = new TextRuleTerm();
		Assert.Null(TextRulesAdmin.Apply(term, new Rest.TextTermRequest { Term = "heck", Language = "en", Category = "sexual", Severity = "block", Match = "word", Level = "strict" }, true));
		Assert.Equal(TextLevels.Strict, term.Level);
	}

	[Theory]
	[InlineData("loud", "profanity")]
	[InlineData("", "profanity")]
	[InlineData("extreme", "reserved")]
	[InlineData("extreme", "impersonation")]
	public void ALevelOutsideTheThreeOrOnANameOnlyCategoryIsInvalid(string level, string category)
	{
		var term = new TextRuleTerm();
		Failure? failure = TextRulesAdmin.Apply(term, new Rest.TextTermRequest { Term = "heck", Language = "en", Category = category, Severity = "block", Match = "word", Level = level }, true);
		Assert.NotNull(failure);
		Assert.Equal("level", Json(failure!.Value.Params));
	}

	[Fact]
	public void ANameOnlyTermHoldsNoLevelAndAnEmptyLevelIsAccepted()
	{
		var term = new TextRuleTerm();
		Assert.Null(TextRulesAdmin.Apply(term, new Rest.TextTermRequest { Term = "heck", Language = "en", Category = "reserved", Severity = "block", Match = "word", Level = "" }, true));
		Assert.Null(term.Level);
	}

	[Fact]
	public void ChangingTheCategoryWithoutALevelTakesTheLevelOfTheNewCategory()
	{
		var term = new TextRuleTerm { Term = "heck", Category = TextCategories.Profanity, Level = TextLevels.Strict };
		Assert.Null(TextRulesAdmin.Apply(term, new Rest.TextTermRequest { Category = "slur" }, false));
		Assert.Equal(TextLevels.Extreme, term.Level);
		Assert.Null(TextRulesAdmin.Apply(term, new Rest.TextTermRequest { Category = "reserved" }, false));
		Assert.Null(term.Level);
	}

	[Fact]
	public void ChangingOtherFieldsKeepsAStaffLevel()
	{
		var term = new TextRuleTerm { Term = "heck", Category = TextCategories.Profanity, Level = TextLevels.Extreme };
		Assert.Null(TextRulesAdmin.Apply(term, new Rest.TextTermRequest { Severity = "flag" }, false));
		Assert.Equal(TextLevels.Extreme, term.Level);
	}

	private static string Json(object? parameters) =>
		System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(parameters, GenOnlineService.Realtime.RealtimeJson.Create())).RootElement.GetProperty("field").GetString()!;
}
