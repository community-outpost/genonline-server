using System.Text;
using GenOnlineService.TextRules;
using Xunit;

namespace GenOnlineService.Tests;

// The words here are mild test data, not seed lists
public class TextLanguageTests
{
	private static TextRuleSet Set(string term, string language, string match = TextMatchModes.Word) =>
		new([TextChecks.Info(new TextRuleTerm { Term = term, Language = language, Match = match, Category = TextCategories.Insult, Severity = TextSeverities.Flag })],
			[], new TextRuleWorkflows(TextRuleWorkflows.Defaults), "test");

	private static bool Hits(TextRuleSet set, string text) => set.Check(TextKinds.Chat, text).Hits.Count > 0;

	[Theory]
	[InlineData("Blödmann")]
	[InlineData("BLÖDMANN")]
	[InlineData("blodmann")]
	[InlineData("bl0dmann")]
	[InlineData("b l ö d m a n n")]
	[InlineData("du Blödmann")]
	public void GermanUmlautsFoldToTheirBaseLetter(string text)
	{
		Assert.True(Hits(Set("blödmann", TextLanguages.German), text));
	}

	[Theory]
	[InlineData("Fußpilz")]
	[InlineData("FUẞPILZ")]
	[InlineData("fusspilz")]
	[InlineData("fu55pilz")]
	public void GermanSharpSFoldsToTwoSs(string text)
	{
		Assert.True(Hits(Set("fußpilz", TextLanguages.German, TextMatchModes.Anywhere), text));
	}

	[Fact]
	public void TheSharpSIsExpandedByTheFoldAndTheExportedMap()
	{
		Assert.Equal("strasse", new string(TextFolding.Fold("Straße").Select(c => c.C).ToArray()));
		Assert.Equal("ss", TextFolding.FoldTable()["ß"]);
	}

	[Fact]
	public void TheUmlautDigraphsAreNotRead()
	{
		Assert.False(Hits(Set("blödmann", TextLanguages.German), "bloedmann"));
	}

	[Theory]
	[InlineData("дурак")]
	[InlineData("ДУРАК")]
	[InlineData("дypak")]
	[InlineData("дур@к")]
	[InlineData("дуpaк")]
	[InlineData("д у р а к")]
	public void RussianLookAlikesFoldInBothDirections(string text)
	{
		Assert.True(Hits(Set("дурак", TextLanguages.Russian), text));
	}

	[Theory]
	[InlineData("хам")]
	[InlineData("ХАМ")]
	[InlineData("xam")]
	[InlineData("хaм")]
	public void ARussianTermOfLookAlikesOnlyHitsItsLatinSpelling(string text)
	{
		Assert.True(Hits(Set("хам", TextLanguages.Russian), text));
		Assert.True(Hits(Set("xam", TextLanguages.Russian), text));
	}

	[Theory]
	[InlineData("3лой")]
	[InlineData("з л о й")]
	[InlineData("зло́й")]
	[InlineData("ЗЛОЙ")]
	public void RussianLeetThreeReadsAsZe(string text)
	{
		Assert.True(Hits(Set("злой", TextLanguages.Russian), text));
	}

	[Theory]
	[InlineData("6лин")]
	[InlineData("Блин")]
	public void RussianLeetSixReadsAsBe(string text)
	{
		Assert.True(Hits(Set("блин", TextLanguages.Russian), text));
	}

	[Theory]
	[InlineData("4ёрт")]
	[InlineData("чёрт")]
	[InlineData("4ерт")]
	public void RussianLeetFourReadsAsCheInTheSecondReading(string text)
	{
		Assert.True(Hits(Set("черт", TextLanguages.Russian), text));
	}

	[Theory]
	[InlineData("дураки")]
	[InlineData("курак")]
	[InlineData("3 players 4 teams 6 slots")]
	public void RussianTermsStayQuietOnOtherText(string text)
	{
		Assert.False(Hits(Set("дурак", TextLanguages.Russian), text));
	}

	[Fact]
	public void TheSecondReadingRunsForAThreeAndAFourAndAOne()
	{
		Assert.True(TextFolding.NeedsAlternative(TextFolding.Words(TextFolding.Fold("3"), false)));
		Assert.True(TextFolding.NeedsAlternative(TextFolding.Words(TextFolding.Fold("4"), false)));
		Assert.True(TextFolding.NeedsAlternative(TextFolding.Words(TextFolding.Fold("1"), false)));
		Assert.False(TextFolding.NeedsAlternative(TextFolding.Words(TextFolding.Fold("5"), false)));
	}

	[Fact]
	public void TheExportedTablesCarryTheNewEntries()
	{
		Assert.Equal("ss", TextFolding.FoldTable()["ß"]);
		Assert.Equal("б", TextFolding.LeetTable()["6"]);
		Assert.Equal("з", TextFolding.LeetAlternativeTable()["3"]);
		Assert.Equal("ч", TextFolding.LeetAlternativeTable()["4"]);
		Assert.Equal("l", TextFolding.LeetAlternativeTable()["1"]);
		ChatFilterFold fold = new TextRuleSet([], [], new TextRuleWorkflows(TextRuleWorkflows.Defaults), "test").ChatFilter.Fold;
		Assert.Equal("ss", fold.Map["ß"]);
		Assert.Equal("з", fold.LeetAlt["3"]);
	}

	[Fact]
	public void AListedRussianTermIsExportedFoldedWithItsLanguage()
	{
		var set = new TextRuleSet([TextChecks.Info(new TextRuleTerm { Term = "дурак", Language = "ru", Category = TextCategories.Insult, Severity = TextSeverities.Flag })], [], new TextRuleWorkflows(TextRuleWorkflows.Defaults), "test");
		ChatFilterTerm term = Assert.Single(set.ChatFilter.Terms);
		Assert.Equal(("дypak", "ru", "standard"), (term.Text, term.Language, term.Level));
	}

	private static Stream? Open(string file, IReadOnlyDictionary<string, string> extra) =>
		extra.TryGetValue(file, out string? text)
			? new MemoryStream(Encoding.UTF8.GetBytes(text))
			: typeof(TextRuleSeeds).Assembly.GetManifestResourceStream("GenOnlineService.data.textrules." + file);

	[Fact]
	public void TheSeedLoaderReadsTheGermanAndRussianFilesWhenTheyExist()
	{
		var files = new Dictionary<string, string>
		{
			["de.txt"] = "# comment\nblödmann\tinsult\tflag\tword\n",
			["ldnoobw-de.txt"] = "fußpilz\tprofanity\tflag\tanywhere\n",
			["ru.txt"] = "дурак\tinsult\tflag\tword\n",
			["ldnoobw-ru.txt"] = "злой\tslur\tblock\tword\n"
		};
		IReadOnlyList<TextRuleTerm> terms = TextRuleSeeds.Terms(0, file => Open(file, files));
		TextRuleTerm own = terms.Single(t => t.Term == "blödmann");
		Assert.Equal(("de", "insult", "standard", TextRuleSources.Seed), (own.Language, own.Category, own.Level, own.Source));
		TextRuleTerm listed = terms.Single(t => t.Term == "fußpilz");
		Assert.Equal(("de", "strict", TextRuleSources.Ldnoobw), (listed.Language, listed.Level, listed.Source));
		Assert.Equal(("ru", TextRuleSources.Seed), (terms.Single(t => t.Term == "дурак").Language, terms.Single(t => t.Term == "дурак").Source));
		Assert.Equal(("ru", "extreme", TextRuleSources.Ldnoobw), (terms.Single(t => t.Term == "злой").Language, terms.Single(t => t.Term == "злой").Level, terms.Single(t => t.Term == "злой").Source));
	}

	[Fact]
	public void TheGermanAndRussianWordsComeWithSeedVersionThree()
	{
		var files = new Dictionary<string, string> { ["de.txt"] = "blödmann\tinsult\tflag\tword\n", ["ldnoobw-ru.txt"] = "дурак\tinsult\tflag\tword\n", ["ldnoobw-de.txt"] = "" };
		Assert.Equal(["blödmann", "дурак"], TextRuleSeeds.Terms(2, file => Open(file, files)).Select(t => t.Term).Order());
		Assert.Empty(TextRuleSeeds.Terms(3, file => Open(file, files)));
		Assert.Contains(TextRuleSeeds.Terms(0, file => Open(file, files)), t => t.Language == "en");
	}

	[Fact]
	public void TheSeedLoaderSkipsAMissingGermanOrRussianFile()
	{
		string[] missing = ["de.txt", "ru.txt", "ldnoobw-de.txt", "ldnoobw-ru.txt"];
		IReadOnlyList<TextRuleTerm> terms = TextRuleSeeds.Terms(0, file => missing.Contains(file) ? null : Open(file, new Dictionary<string, string>()));
		Assert.DoesNotContain(terms, t => t.Language is "de" or "ru");
		Assert.Contains(terms, t => t.Language == "en");
	}

	[Fact]
	public void TheShippedGermanAndRussianListsSeedEveryLevel()
	{
		IReadOnlyList<TextRuleTerm> terms = TextRuleSeeds.Terms();
		foreach (string language in new[] { "de", "ru" })
		{
			TextRuleTerm[] seeded = terms.Where(t => t.Language == language).ToArray();
			Assert.Equal(language == "de" ? 54 : 135, seeded.Length);
			Assert.All(seeded, t => Assert.Equal(t.Category == TextCategories.Slur ? TextSeverities.Block : TextSeverities.Flag, t.Severity));
			Assert.Equal(new HashSet<string?> { TextLevels.Extreme, TextLevels.Standard, TextLevels.Strict }, seeded.Select(t => TextLevels.OfCategory(t.Category)).ToHashSet());
			Assert.All(seeded, t => Assert.NotNull(TextRulesAdmin.CleanTerm(t.Term)));
		}
	}

	[Fact]
	public void TheLanguagesAreEnglishArabicGermanAndRussian()
	{
		Assert.Equal(["en", "ar", "de", "ru"], TextLanguages.All);
	}
}
