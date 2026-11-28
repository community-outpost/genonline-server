/*
**    GeneralsOnline Game Services - Backend Services for Command & Conquer Generals Online: Zero Hour
**    Copyright (C) 2025  GeneralsOnline Development Team
**
**    This program is free software: you can redistribute it and/or modify
**    it under the terms of the GNU Affero General Public License as
**    published by the Free Software Foundation, either version 3 of the
**    License, or (at your option) any later version.
**
**    This program is distributed in the hope that it will be useful,
**    but WITHOUT ANY WARRANTY; without even the implied warranty of
**    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
**    GNU Affero General Public License for more details.
**
**    You should have received a copy of the GNU Affero General Public License
**    along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

using GenOnlineService.Realtime;
using System.Security.Cryptography;
using System.Text.Json;

namespace GenOnlineService.TextRules
{
	// A listed word as the client matcher reads it: text is folded and carries the repeats of the word, match is word, anywhere or literal
	public sealed record ChatFilterTerm(string Text, string Match, string Language);

	// The tables a client applies to inbound text: fold maps a lower-cased character to its replacement, leet reads digits and symbols
	// inside words, leet_alt is the second reading of a 1, and arabic_prefixes may sit before a whole Arabic word
	public sealed record ChatFilterFold(IReadOnlyDictionary<string, string> Map, IReadOnlyDictionary<string, string> Leet, IReadOnlyDictionary<string, string> LeetAlt, IReadOnlyList<string> ArabicPrefixes);

	// The words of the filter rules of chat for the players who turn the chat filter on, served without sign-in because the list holds no
	// player data and the retail clients carry word lists of their own
	public sealed record ChatFilterList(string Revision, ChatFilterFold Fold, IReadOnlyList<ChatFilterTerm> Terms, IReadOnlyList<string> Exceptions)
	{
		private static readonly ILogger s_log = AppLog.For(typeof(ChatFilterList));
		private static readonly JsonSerializerOptions s_json = RealtimeJson.Create();

		public string Etag => "\"" + Revision + "\"";

		public static ChatFilterList Of(TextRuleSet set)
		{
			bool filtering = set.Workflows.Definitions.TryGetValue(TextKinds.Chat, out TextWorkflowDefinition? chat)
				&& chat.Rules.Any(r => r.Enabled && r.Outcome == TextOutcomes.Filter);
			IEnumerable<ChatFilterTerm> terms = filtering
				? set.Terms.Where(t => t.Severity == TextSeverities.Flag && !TextCategories.IsNameOnly(t.Category)).Select(t => new ChatFilterTerm(TextOf(t), t.Match, t.Language))
				: [];
			var list = new ChatFilterList(
				String.Empty,
				new ChatFilterFold(Clean(TextFolding.FoldTable()), Clean(TextFolding.LeetTable()), Clean(TextFolding.LeetAlternativeTable()), TextFolding.ArabicPrefixes),
				terms.Where(t => IsClean(t.Text)).Distinct().OrderBy(t => t.Match, StringComparer.Ordinal).ThenBy(t => t.Language, StringComparer.Ordinal).ThenBy(t => t.Text, StringComparer.Ordinal).ToList(),
				set.Exceptions.Select(TextOf).Where(IsClean).Distinct().Order(StringComparer.Ordinal).ToList());
			return list with { Revision = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(list, s_json)), 0, 16).ToLowerInvariant() };
		}

		private static string TextOf(TextTermInfo term) => ExportText(term.Term, term.Match);

		public static string ExportText(string term, string match) => match == TextMatchModes.Literal
			? Names.NameKey.LowerRoot(term)
			: TextFolding.Latin(TextFolding.Words(TextFolding.Fold(term), false), false).Text;

		// Clients refuse the whole list over one entry that is empty or carries stray whitespace, so such an entry never leaves the service
		public static bool IsClean(string text) =>
			text.Length > 0 && text[0] != ' ' && text[^1] != ' ' && !text.Contains("  ", StringComparison.Ordinal) && !text.Any(c => c != ' ' && Char.IsWhiteSpace(c));

		private static IReadOnlyDictionary<string, string> Clean(IReadOnlyDictionary<string, string> table) =>
			table.Where(entry => entry.Value.Length > 0 && !entry.Value.Any(Char.IsWhiteSpace)).ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);

		public byte[] ToJson() => JsonSerializer.SerializeToUtf8Bytes(this, s_json);

		public static bool EtagMatches(IEnumerable<string?> ifNoneMatch, string etag)
		{
			return ifNoneMatch.Any(value => value != null && value.Split(',').Any(tag => tag.Trim() == "*" || (tag.Trim().StartsWith("W/", StringComparison.Ordinal) ? tag.Trim()[2..] : tag.Trim()) == etag));
		}

		// Each copy reloads the rules itself, so each tells only the connections it holds
		public static void Publish(string revision)
		{
			try
			{
				foreach (UserSession session in RealtimeAudience.AllSessions())
				{
					RealtimeOut.Fire(RealtimeOut.Session(session).ChatFilterUpdate(new ChatFilterUpdateEvent { Revision = revision }));
				}
			}
			catch (Exception ex)
			{
				s_log.LogWarning(ex, "ChatFilterUpdate failed");
			}
		}
	}
}
