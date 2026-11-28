using GenOnlineService.TextRules;
using Xunit;

namespace GenOnlineService.Tests;

// English and Arabic matching of the seed lists, pinned before the German and Russian folding was added
public class EnglishMatchingPinTests
{
	private static IEnumerable<string> Words(string text) => TextChecks.Check(TextKinds.Chat, text).Hits.Select(h => h.Term.Term).Distinct();

	[Theory]
	[InlineData("sh1t", "shit")]
	[InlineData("sh!t", "shit")]
	[InlineData("$h1t", "shit")]
	[InlineData("SHIT happens", "shit")]
	[InlineData("f.u.c.k", "fuck")]
	[InlineData("f u c k", "fuck")]
	[InlineData("fuuuck", "fuck")]
	[InlineData("a$$hole", "asshole")]
	[InlineData("fvck sh1t", "shit")]
	[InlineData("b1tch", "bitch")]
	[InlineData("n1gg3r", "nigger")]
	[InlineData("p0rn", "porn")]
	[InlineData("ＦＵＣＫ", "fuck")]
	[InlineData("сосk", "cock")]
	public void EnglishLookAlikeAndLeetSpellingsStillHit(string text, string term)
	{
		Assert.Contains(term, Words(text));
	}

	[Theory]
	[InlineData("Scunthorpe")]
	[InlineData("assassin")]
	[InlineData("classic")]
	[InlineData("a cockpit")]
	[InlineData("good game 3 players 4 teams 6 slots")]
	[InlineData("as")]
	public void EnglishCleanTextStaysClean(string text)
	{
		Assert.DoesNotContain(Words(text), w => w is "ass" or "cock" or "cunt" or "shit" or "fuck");
	}

	[Fact]
	public void TheArabicSeedStillHits()
	{
		string arabic = TextRuleSeeds.Terms().First(t => t.Language == TextLanguages.Arabic && t.Match == TextMatchModes.Anywhere && TextFolding.IsArabicText(t.Term)).Term;
		Assert.Contains(arabic, Words(arabic));
		Assert.Contains(arabic, Words("ال" + arabic));
	}

	[Fact]
	public void TheExportedTablesKeepTheirEntries()
	{
		Assert.Equal("a", TextFolding.FoldTable()["а"]);
		Assert.Equal("ا", TextFolding.FoldTable()["أ"]);
		Assert.Equal("i", TextFolding.LeetTable()["1"]);
		Assert.Equal("e", TextFolding.LeetTable()["3"]);
		Assert.Equal("l", TextFolding.LeetAlternativeTable()["1"]);
	}
}
