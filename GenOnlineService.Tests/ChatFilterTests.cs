using System.Globalization;
using System.Text;
using System.Text.Json;
using GenOnlineService.Api;
using GenOnlineService.Realtime;
using GenOnlineService.TextRules;
using Xunit;

namespace GenOnlineService.Tests;

public class ChatFilterTests
{
	private static TextRuleSet Set(IEnumerable<TextRuleTerm> terms, IEnumerable<TextRuleException>? exceptions = null) =>
		new(terms.Select(TextChecks.Info), (exceptions ?? []).Select(TextChecks.Info), new TextRuleWorkflows(TextRuleWorkflows.Defaults), "test");

	private static TextRuleTerm Term(string term, string severity, string match = TextMatchModes.Word, string category = TextCategories.Profanity, string language = TextLanguages.English) =>
		new() { Term = term, Severity = severity, Match = match, Category = category, Language = language };

	[Fact]
	public void ListHoldsTheTermsThatHaveALevelWhateverTheirSeverity()
	{
		ChatFilterList list = Set([Term("heck", TextSeverities.Flag), Term("darn", TextSeverities.Block), Term("admin", TextSeverities.Flag, category: TextCategories.Impersonation)]).ChatFilter;
		Assert.Equal(["darn", "heck"], list.Terms.Select(t => t.Text).Order());
	}

	[Fact]
	public void TermsCarryTheirLevel()
	{
		ChatFilterList list = Set([Term("heck", TextSeverities.Flag), Term("kkk", TextSeverities.Block, category: TextCategories.Slur), Term("porn", TextSeverities.Flag, category: TextCategories.Sexual)]).ChatFilter;
		Assert.Equal([("heck", "strict"), ("kk", "extreme"), ("porn", "standard")], list.Terms.Select(t => (t.Text, t.Level)).Order());
	}

	[Fact]
	public void ATermKeepsAStaffLevelOverItsCategory()
	{
		var term = Term("heck", TextSeverities.Flag);
		term.Level = TextLevels.Extreme;
		Assert.Equal("extreme", Set([term]).ChatFilter.Terms[0].Level);
	}

	[Fact]
	public void AnExceptionTakesTheMostSevereLevelOfTheTermsItProtects()
	{
		ChatFilterList list = Set(
			[Term("cock", TextSeverities.Flag, TextMatchModes.Anywhere, TextCategories.Sexual), Term("ass", TextSeverities.Flag, TextMatchModes.Anywhere), Term("kkk", TextSeverities.Block, TextMatchModes.Anywhere, TextCategories.Slur)],
			[new TextRuleException { Term = "cockpit" }, new TextRuleException { Term = "assassin" }, new TextRuleException { Term = "cockkkpit" }, new TextRuleException { Term = "unrelated" }]).ChatFilter;
		Assert.Equal([("assassin", "strict"), ("cockkpit", "extreme"), ("cockpit", "standard"), ("unrelated", "extreme")], list.Exceptions.Select(e => (e.Text, e.Level)).Order());
	}

	[Fact]
	public void TermsCarryTheFoldedTextTheMatchAndTheLanguage()
	{
		ChatFilterList list = Set([Term("Sh1T", TextSeverities.Flag, TextMatchModes.Anywhere), Term("b4d  wörd", TextSeverities.Flag)]).ChatFilter;
		Assert.Equal([("bad word", "word", "en"), ("shit", "anywhere", "en")], list.Terms.Select(t => (t.Text, t.Match, t.Language)).Order());
	}

	[Fact]
	public void ExceptionsAreListedFolded()
	{
		ChatFilterList list = Set([Term("sex", TextSeverities.Flag, TextMatchModes.Anywhere)], [new TextRuleException { Term = "Sussex" }]).ChatFilter;
		Assert.Equal(["sussex"], list.Exceptions.Select(e => e.Text));
	}

	[Fact]
	public void ListIsEmptyWhenTheChatWorkflowHasNoFilterRule()
	{
		var chat = new TextWorkflowDefinition { Rules = [new TextRuleDefinition { Name = "blocked words", Expression = "input.Block > 0", Outcome = TextOutcomes.Block }] };
		var workflows = new Dictionary<string, TextWorkflowDefinition>(TextRuleWorkflows.Defaults) { [TextKinds.Chat] = chat };
		var set = new TextRuleSet([TextChecks.Info(Term("heck", TextSeverities.Flag))], [], new TextRuleWorkflows(workflows), "test");
		Assert.Empty(set.ChatFilter.Terms);
	}

	[Fact]
	public void RevisionIsStableForTheSameListAndChangesWithIt()
	{
		string first = Set([Term("heck", TextSeverities.Flag)]).ChatFilter.Revision;
		Assert.Equal(first, Set([Term("heck", TextSeverities.Flag)]).ChatFilter.Revision);
		Assert.NotEqual(first, Set([Term("heck", TextSeverities.Flag), Term("darn", TextSeverities.Flag)]).ChatFilter.Revision);
		Assert.NotEqual(first, Set([Term("heck", TextSeverities.Flag, TextMatchModes.Anywhere)]).ChatFilter.Revision);
		Assert.NotEqual(first, Set([Term("heck", TextSeverities.Flag, category: TextCategories.Slur)]).ChatFilter.Revision);
		// a word without a level is not in the list
		Assert.Equal(first, Set([Term("heck", TextSeverities.Flag), Term("admin", TextSeverities.Block, category: TextCategories.Reserved)]).ChatFilter.Revision);
	}

	[Fact]
	public void BodyCarriesTheRevisionTheTermsAndTheFold()
	{
		TextRuleSet set = Set([Term("heck", TextSeverities.Flag)]);
		using JsonDocument body = JsonDocument.Parse(set.ChatFilterJson);
		JsonElement root = body.RootElement;
		Assert.Equal(set.ChatFilter.Revision, root.GetProperty("revision").GetString());
		Assert.Equal("heck", root.GetProperty("terms")[0].GetProperty("text").GetString());
		Assert.Equal("strict", root.GetProperty("terms")[0].GetProperty("level").GetString());
		Assert.Equal("a", root.GetProperty("fold").GetProperty("leet").GetProperty("4").GetString());
		Assert.Equal("l", root.GetProperty("fold").GetProperty("leet_alt").GetProperty("1").GetString());
		Assert.Equal("ال", root.GetProperty("fold").GetProperty("arabic_prefixes")[0].GetString());
	}

	[Theory]
	[InlineData("\"abc\"", "\"abc\"", true)]
	[InlineData("\"x\", \"abc\"", "\"abc\"", true)]
	[InlineData("W/\"abc\"", "\"abc\"", true)]
	[InlineData("*", "\"abc\"", true)]
	[InlineData("\"abd\"", "\"abc\"", false)]
	[InlineData("", "\"abc\"", false)]
	public void IfNoneMatchComparesTheEtag(string header, string etag, bool matches)
	{
		Assert.Equal(matches, ChatFilterList.EtagMatches([header], etag));
	}

	[Fact]
	public void EtagIsTheQuotedRevision()
	{
		ChatFilterList list = Set([Term("heck", TextSeverities.Flag)]).ChatFilter;
		Assert.Equal("\"" + list.Revision + "\"", list.Etag);
	}

	// What a client does with the exported fold, written from the documented steps
	private static string ClientFold(string text, ChatFilterFold fold)
	{
		var result = new StringBuilder();
		foreach (Rune rune in text.EnumerateRunes())
		{
			foreach (char raw in rune.ToString().Normalize(NormalizationForm.FormKD))
			{
				UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(raw);
				if (category is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.Format || raw == 'ـ')
				{
					continue;
				}
				string lower = Char.ToLowerInvariant(raw).ToString();
				result.Append(fold.Map.TryGetValue(lower, out string? mapped) ? mapped : lower);
			}
		}
		return result.ToString();
	}

	[Theory]
	[InlineData("Привет ΜΟΝΟ")]
	[InlineData("Ｆｕｃｋ café ﬁne")]
	[InlineData("١٢٣ ۴۵۶ abc")]
	[InlineData("أإآ ى ة ؤ ک مـرحبا")]
	[InlineData("plain text 123")]
	[InlineData("Straße FUẞBALL Größe")]
	public void ExportedFoldMatchesTheServiceFolding(string text)
	{
		ChatFilterFold fold = Set([]).ChatFilter.Fold;
		Assert.Equal(new string(TextFolding.Fold(text).Select(c => c.C).ToArray()), ClientFold(text, fold));
	}

	[Fact]
	public void ExportedLeetTablesMatchTheLatinReading()
	{
		const string text = "5h!7 1d10t";
		ChatFilterFold fold = Set([]).ChatFilter.Fold;
		string folded = ClientFold(text, fold);
		string Read(bool alternative) => String.Concat(folded.Select(c =>
			alternative && fold.LeetAlt.TryGetValue(c.ToString(), out string? alt) ? alt : fold.Leet.TryGetValue(c.ToString(), out string? leet) ? leet : c.ToString()));
		List<List<FoldedChar>> words = TextFolding.Words(TextFolding.Fold(text), false);
		Assert.Equal(TextFolding.Latin(words, false).Text, Read(false));
		Assert.Equal(TextFolding.Latin(words, true).Text, Read(true));
	}

	[Fact]
	public void ReadyAndUpdateEventsSerialize()
	{
		JsonSerializerOptions json = RealtimeJson.Create();
		Assert.Equal("{\"revision\":\"abc\"}", JsonSerializer.Serialize(new ChatFilterUpdateEvent { Revision = "abc" }, json));
		using JsonDocument ready = JsonDocument.Parse(JsonSerializer.Serialize(new ReadyEvent { ChatFilterRevision = "abc" }, json));
		Assert.Equal("abc", ready.RootElement.GetProperty("chat_filter_revision").GetString());
		Assert.True(typeof(IRealtimeClient).GetMethod(nameof(IRealtimeClient.ChatFilterUpdate)) != null);
	}

	[Theory]
	[InlineData("heck", true)]
	[InlineData("white power", true)]
	[InlineData("", false)]
	[InlineData(" heck", false)]
	[InlineData("heck ", false)]
	[InlineData("white  power", false)]
	[InlineData("white	power", false)]
	public void AnExportedTextIsCleanOnlyWithSingleSpacesBetweenWords(string text, bool clean)
	{
		Assert.Equal(clean, ChatFilterList.IsClean(text));
	}

	[Theory]
	[InlineData("́́")]
	[InlineData("​")]
	[InlineData("   ")]
	public void ATermThatFoldsToNothingIsRefusedOnSave(string term)
	{
		Assert.Null(TextRulesAdmin.CleanTerm(term));
	}

	[Fact]
	public void TheListLeavesOutAnEntryThatFoldsToNothing()
	{
		ChatFilterList list = ChatFilterList.Of(Set(
			[Term("heck", TextSeverities.Flag), Term("́́", TextSeverities.Flag)],
			[new TextRuleException { Term = "́", Language = TextLanguages.English }]));
		Assert.Equal(["heck"], list.Terms.Select(t => t.Text));
		Assert.Empty(list.Exceptions);
		Assert.All(list.Terms.Select(t => t.Text).Concat(list.Exceptions.Select(e => e.Text)), t => Assert.True(ChatFilterList.IsClean(t)));
	}

	[Fact]
	public void TheFoldTablesHoldNoEmptyOrSpacedValue()
	{
		ChatFilterList list = ChatFilterList.Of(Set([Term("heck", TextSeverities.Flag)]));
		foreach (IReadOnlyDictionary<string, string> table in new[] { list.Fold.Map, list.Fold.Leet, list.Fold.LeetAlt })
		{
			Assert.All(table.Values, v => Assert.True(v.Length > 0 && !v.Any(Char.IsWhiteSpace), $"fold value [{v}]"));
		}
	}

	[Fact]
	public void EverySeededTermAndExceptionExportsCleanly()
	{
		ChatFilterList list = ChatFilterList.Of(Set(
			TextRuleSeeds.Terms().Select(t => Term(t.Term, TextSeverities.Flag, t.Match, t.Category, t.Language)),
			TextRuleSeeds.Exceptions()));
		string[] bad = list.Terms.Select(t => t.Text).Concat(list.Exceptions.Select(e => e.Text)).Where(t => !ChatFilterList.IsClean(t)).ToArray();
		Assert.Empty(bad);
		Assert.All(TextRuleSeeds.Terms(), t => Assert.NotNull(TextRulesAdmin.CleanTerm(t.Term)));
		Assert.All(TextRuleSeeds.Exceptions(), e => Assert.NotNull(TextRulesAdmin.CleanTerm(e.Term)));
	}
}
