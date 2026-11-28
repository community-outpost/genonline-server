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

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GenOnlineService.TextRules
{
	public static class TextKinds
	{
		public const string DisplayName = "display_name";
		public const string LobbyName = "lobby_name";
		public const string Chat = "chat";

		public static readonly IReadOnlyList<string> All = [DisplayName, LobbyName, Chat];

		public static bool IsKnown(string? kind) => kind != null && All.Contains(kind, StringComparer.Ordinal);
	}

	public static class TextLanguages
	{
		public const string English = "en";
		public const string Arabic = "ar";
		public const string German = "de";
		public const string Russian = "ru";

		public static readonly IReadOnlyList<string> All = [English, Arabic, German, Russian];
	}

	public static class TextCategories
	{
		public const string Slur = "slur";
		public const string Sexual = "sexual";
		public const string Profanity = "profanity";
		public const string Insult = "insult";

		public const string Impersonation = "impersonation";

		public const string Reserved = "reserved";

		public static readonly IReadOnlyList<string> All = [Slur, Sexual, Profanity, Insult, Impersonation, Reserved];

		// Name only categories: they count apart from the block and flag counts the rules read
		public static bool IsNameOnly(string category) => category is Impersonation or Reserved;
	}

	// How much of the chat filter list a client turns on, its level and every level below it, with no level for the name only categories
	public static class TextLevels
	{
		public const string Extreme = "extreme";
		public const string Standard = "standard";
		public const string Strict = "strict";

		public static readonly IReadOnlyList<string> All = [Extreme, Standard, Strict];

		public static int Rank(string level) => level == Extreme ? 0 : level == Standard ? 1 : 2;

		public static string? OfCategory(string category) => category switch
		{
			TextCategories.Slur => Extreme,
			TextCategories.Sexual or TextCategories.Insult => Standard,
			TextCategories.Profanity => Strict,
			_ => null
		};
	}

	public static class TextSeverities
	{
		public const string Block = "block";
		public const string Flag = "flag";

		public static readonly IReadOnlyList<string> All = [Block, Flag];
	}

	public static class TextMatchModes
	{
		public const string Word = "word";

		public const string Anywhere = "anywhere";

		// Lower-cased text contains the lower-cased word, no other folding
		public const string Literal = "literal";

		public static readonly IReadOnlyList<string> All = [Word, Anywhere, Literal];
	}

	public static class TextOutcomes
	{
		public const string Allow = "allow";
		public const string Refuse = "refuse";
		public const string Block = "block";

		// Chat only: the words of the rule go to the filter list of players who turn the chat filter on, and the stored value stays "mask" from when the rule rewrote the text
		public const string Filter = "mask";

		public static IReadOnlyList<string> For(string kind) => kind == TextKinds.Chat ? [Allow, Block, Filter] : [Allow, Refuse];
	}

	public class TextRuleTerm
	{
		public const int TermMaxLength = 64;

		public Int64 ID { get; set; }
		public string Term { get; set; } = String.Empty;
		public string Language { get; set; } = TextLanguages.English;
		public string Category { get; set; } = TextCategories.Profanity;
		public string Severity { get; set; } = TextSeverities.Block;
		public string Match { get; set; } = TextMatchModes.Word;

		// Null for the name only categories
		public string? Level { get; set; }

		public string Source { get; set; } = TextRuleSources.Staff;
		public string UpdatedBy { get; set; } = String.Empty;
		public DateTime UpdatedAt { get; set; } = DateTime.UnixEpoch;
	}

	public class TextRuleException
	{
		public Int64 ID { get; set; }
		public string Term { get; set; } = String.Empty;
		public string Language { get; set; } = TextLanguages.English;
		public string Source { get; set; } = TextRuleSources.Staff;
		public string UpdatedBy { get; set; } = String.Empty;
		public DateTime UpdatedAt { get; set; } = DateTime.UnixEpoch;
	}

	public class TextRuleWorkflow
	{
		public string Kind { get; set; } = String.Empty;
		public string Definition { get; set; } = "{}";
		public string UpdatedBy { get; set; } = String.Empty;
		public DateTime UpdatedAt { get; set; } = DateTime.UnixEpoch;
	}

	public static class TextRuleSources
	{
		public const string Seed = "seed";
		public const string Reserved = "reserved";
		public const string Staff = "staff";
		public const string Ldnoobw = "ldnoobw";
	}

	public class TextRuleTermConfiguration : IEntityTypeConfiguration<TextRuleTerm>
	{
		public void Configure(EntityTypeBuilder<TextRuleTerm> builder)
		{
			builder.ToTable("text_rule_terms");
			builder.HasKey(e => e.ID);
			builder.Property(e => e.ID).HasColumnName("id");
			builder.Property(e => e.Term).HasColumnName("term").HasMaxLength(TextRuleTerm.TermMaxLength);
			builder.Property(e => e.Language).HasColumnName("language").HasMaxLength(8);
			builder.Property(e => e.Category).HasColumnName("category").HasMaxLength(32);
			builder.Property(e => e.Severity).HasColumnName("severity").HasMaxLength(16);
			builder.Property(e => e.Match).HasColumnName("match_mode").HasMaxLength(16);
			builder.Property(e => e.Level).HasColumnName("level").HasMaxLength(16);
			builder.Property(e => e.Source).HasColumnName("source").HasMaxLength(16);
			builder.Property(e => e.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);
			builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
			builder.HasIndex(e => new { e.Language, e.Term }).IsUnique().HasDatabaseName("ux_text_rule_terms_language_term");
		}
	}

	public class TextRuleExceptionConfiguration : IEntityTypeConfiguration<TextRuleException>
	{
		public void Configure(EntityTypeBuilder<TextRuleException> builder)
		{
			builder.ToTable("text_rule_exceptions");
			builder.HasKey(e => e.ID);
			builder.Property(e => e.ID).HasColumnName("id");
			builder.Property(e => e.Term).HasColumnName("term").HasMaxLength(TextRuleTerm.TermMaxLength);
			builder.Property(e => e.Language).HasColumnName("language").HasMaxLength(8);
			builder.Property(e => e.Source).HasColumnName("source").HasMaxLength(16);
			builder.Property(e => e.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);
			builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
			builder.HasIndex(e => new { e.Language, e.Term }).IsUnique().HasDatabaseName("ux_text_rule_exceptions_language_term");
		}
	}

	public class TextRuleWorkflowConfiguration : IEntityTypeConfiguration<TextRuleWorkflow>
	{
		public void Configure(EntityTypeBuilder<TextRuleWorkflow> builder)
		{
			builder.ToTable("text_rule_workflows");
			builder.HasKey(e => e.Kind);
			builder.Property(e => e.Kind).HasColumnName("kind").HasMaxLength(32);
			builder.Property(e => e.Definition).HasColumnName("definition");
			builder.Property(e => e.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);
			builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
		}
	}
}
