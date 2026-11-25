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
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GenOnlineService.Rules
{
	public static class RuleTriggers
	{
		public const string SignIn = "sign_in";
		public const string GameSession = "game_session";
		public const string LobbyJoin = "lobby_join";
		public const string MatchResult = "match_result";
		public const string AnticheatReport = "anticheat_report";
		public const string Sweep = "sweep";

		public static readonly IReadOnlyList<string> All = [SignIn, GameSession, LobbyJoin, MatchResult, AnticheatReport, Sweep];

		public static bool IsKnown(string? trigger) => trigger != null && All.Contains(trigger, StringComparer.Ordinal);
	}

	// Watch records a match, Flag also queues it for staff review, Enforce takes the actions
	public static class RuleModes
	{
		public const string Watch = "watch";
		public const string Flag = "flag";
		public const string Enforce = "enforce";

		public static readonly IReadOnlyList<string> All = [Watch, Flag, Enforce];

		public static bool IsKnown(string? mode) => mode != null && All.Contains(mode, StringComparer.Ordinal);
	}

	public static class RuleNodeKinds
	{
		public const string Trigger = "trigger";
		public const string Condition = "condition";
		public const string Action = "action";

		// Reserved for later step nodes, a document with one is refused until they exist
		public const string Step = "step";
	}

	public static class RuleActionKinds
	{
		public const string Flag = "flag";
		public const string Kick = "kick";
		public const string Mute = "mute";
		public const string Ban = "ban";

		public static readonly IReadOnlyList<string> All = [Flag, Kick, Mute, Ban];
	}

	public static class RuleOutputs
	{
		public const string True = "true";
		public const string False = "false";
	}

	public static class RuleGroupMatches
	{
		public const string All = "all";
		public const string Any = "any";
	}

	public static class RuleOperators
	{
		public const string Equals = "equals";
		public const string NotEquals = "not_equals";
		public const string InList = "in_list";
		public const string Contains = "contains";
		public const string StartsWith = "starts_with";
		public const string GreaterThan = "greater_than";
		public const string LessThan = "less_than";
		public const string Between = "between";
		public const string IsEmpty = "is_empty";
		public const string Cidr = "cidr";
	}

	public static class RuleReviewStates
	{
		public const string None = "none";
		public const string Pending = "pending";
		public const string Reviewed = "reviewed";
		public const string Dismissed = "dismissed";
	}

	public static class RuleOutcomes
	{
		public const string Watched = "watched";
		public const string Flagged = "flagged";
		public const string Enforced = "enforced";

		// The player holds a staff role, rules never act on staff
		public const string StaffExempt = "staff_exempt";

		// Every action was skipped: the player is already banned or muted, or a kick found them offline
		public const string NothingToDo = "nothing_to_do";

		public const string Previewed = "previewed";
	}

	public static class RuleAudit
	{
		public const string TargetKind = "automated_rule";
		public const string MatchTargetKind = "rule_match";
		public const string Create = "rule_create";
		public const string Version = "rule_version";
		public const string Update = "rule_update";
		public const string Archive = "rule_archive";
		public const string Restore = "rule_restore";
		public const string Breaker = "rule_breaker";
		public const string Preview = "rule_preview";
		public const string SweepStart = "rule_sweep_start";
		public const string SweepCancel = "rule_sweep_cancel";
		public const string MatchReview = "rule_match_review";
	}

	public sealed record RuleDocument
	{
		public IReadOnlyList<RuleNode> Nodes { get; init; } = [];
		public IReadOnlyList<RuleEdge> Edges { get; init; } = [];
	}

	public sealed record RuleNode
	{
		public string Id { get; init; } = "";
		public string Kind { get; init; } = "";

		public RuleNodeEvent? Event { get; init; }

		// trigger nodes saved before event.action, read into Event and never written
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public string? Trigger { get; init; }

		public RuleQuery? Query { get; init; }

		public string? Action { get; init; }

		// mute and ban: the length in seconds, a ban without one is permanent
		public long? DurationS { get; init; }
		public string? Reason { get; init; }

		// The server keeps it as given
		public RuleNodePosition? Position { get; init; }
	}

	public sealed record RuleNodeEvent
	{
		public string? Action { get; init; }
	}

	public sealed record RuleNodePosition
	{
		public double X { get; init; }
		public double Y { get; init; }
	}

	public sealed record RuleQuery
	{
		public string? Match { get; init; }
		public IReadOnlyList<RuleQuery>? Rules { get; init; }

		public string? Field { get; init; }
		public string? Operator { get; init; }
		public JsonElement? Value { get; init; }
	}

	public sealed record RuleEdge
	{
		public string From { get; init; } = "";
		public string To { get; init; } = "";
		public string? Output { get; init; }
	}

	// Mode and enabled live here, the logic lives in its versions
	public class AutomatedRule
	{
		public const int NameMaxLength = 100;
		public const int DescriptionMaxLength = 1000;

		public Int64 ID { get; set; }
		public string Name { get; set; } = String.Empty;
		public string? Description { get; set; }
		public string Mode { get; set; } = RuleModes.Watch;
		public bool Enabled { get; set; }
		public int CurrentVersion { get; set; }
		public DateTime? ArchivedAt { get; set; }
		public string CreatedBy { get; set; } = String.Empty;
		public DateTime CreatedAt { get; set; } = DateTime.UnixEpoch;
		public string UpdatedBy { get; set; } = String.Empty;
		public DateTime UpdatedAt { get; set; } = DateTime.UnixEpoch;
	}

	// Immutable; each save is the next number
	public class AutomatedRuleVersion
	{
		public const int CommentMaxLength = 500;

		public Int64 RuleID { get; set; }
		public int Number { get; set; }
		public string Document { get; set; } = "{}";
		public string CreatedBy { get; set; } = String.Empty;
		public string? Comment { get; set; }
		public DateTime CreatedAt { get; set; } = DateTime.UnixEpoch;
	}

	public class AutomatedRuleMatch
	{
		public Int64 ID { get; set; }
		public Int64 RuleID { get; set; }
		public int RuleVersion { get; set; }
		public Int64 UserID { get; set; }
		public string Trigger { get; set; } = String.Empty;
		public DateTime MatchedAt { get; set; } = DateTime.UnixEpoch;
		public string Mode { get; set; } = RuleModes.Watch;

		// the actions taken, comma separated, null when none was
		public string? Actions { get; set; }
		public string Outcome { get; set; } = RuleOutcomes.Watched;

		public string Facts { get; set; } = "{}";
		public string ReviewState { get; set; } = RuleReviewStates.None;
		public Int64? ReviewedBy { get; set; }
		public DateTime? ReviewedAt { get; set; }

		// The moderation case that decided a flagged match, null while it waits
		public Int64? CaseID { get; set; }
	}

	public class AutomatedRuleConfiguration : IEntityTypeConfiguration<AutomatedRule>
	{
		public void Configure(EntityTypeBuilder<AutomatedRule> builder)
		{
			builder.ToTable("automated_rules");
			builder.HasKey(e => e.ID);
			builder.Property(e => e.ID).HasColumnName("id").ValueGeneratedOnAdd();
			builder.Property(e => e.Name).HasColumnName("name").HasMaxLength(AutomatedRule.NameMaxLength);
			builder.Property(e => e.Description).HasColumnName("description").HasMaxLength(AutomatedRule.DescriptionMaxLength);
			builder.Property(e => e.Mode).HasColumnName("mode").HasMaxLength(16);
			builder.Property(e => e.Enabled).HasColumnName("enabled");
			builder.Property(e => e.CurrentVersion).HasColumnName("current_version");
			builder.Property(e => e.ArchivedAt).HasColumnName("archived_at");
			builder.Property(e => e.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
			builder.Property(e => e.CreatedAt).HasColumnName("created_at");
			builder.Property(e => e.UpdatedBy).HasColumnName("updated_by").HasMaxLength(64);
			builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
		}
	}

	public class AutomatedRuleVersionConfiguration : IEntityTypeConfiguration<AutomatedRuleVersion>
	{
		public void Configure(EntityTypeBuilder<AutomatedRuleVersion> builder)
		{
			builder.ToTable("automated_rule_versions");
			builder.HasKey(e => new { e.RuleID, e.Number });
			builder.Property(e => e.RuleID).HasColumnName("rule_id");
			builder.Property(e => e.Number).HasColumnName("number");
			builder.Property(e => e.Document).HasColumnName("document");
			builder.Property(e => e.CreatedBy).HasColumnName("created_by").HasMaxLength(64);
			builder.Property(e => e.Comment).HasColumnName("comment").HasMaxLength(AutomatedRuleVersion.CommentMaxLength);
			builder.Property(e => e.CreatedAt).HasColumnName("created_at");
		}
	}

	public class AutomatedRuleMatchConfiguration : IEntityTypeConfiguration<AutomatedRuleMatch>
	{
		public void Configure(EntityTypeBuilder<AutomatedRuleMatch> builder)
		{
			builder.ToTable("automated_rule_matches");
			builder.HasKey(e => e.ID);
			builder.HasIndex(e => new { e.RuleID, e.UserID, e.Trigger, e.MatchedAt }).HasDatabaseName("ix_automated_rule_matches_rule_user_trigger");
			builder.HasIndex(e => new { e.RuleID, e.MatchedAt }).HasDatabaseName("ix_automated_rule_matches_rule_matched_at");
			builder.HasIndex(e => new { e.ReviewState, e.UserID }).HasDatabaseName("ix_automated_rule_matches_review_user");
			builder.HasIndex(e => e.UserID).HasDatabaseName("ix_automated_rule_matches_user");
			builder.Property(e => e.ID).HasColumnName("id").ValueGeneratedOnAdd();
			builder.Property(e => e.RuleID).HasColumnName("rule_id");
			builder.Property(e => e.RuleVersion).HasColumnName("rule_version");
			builder.Property(e => e.UserID).HasColumnName("user_id");
			builder.Property(e => e.Trigger).HasColumnName("trigger").HasMaxLength(32);
			builder.Property(e => e.MatchedAt).HasColumnName("matched_at");
			builder.Property(e => e.Mode).HasColumnName("mode").HasMaxLength(16);
			builder.Property(e => e.Actions).HasColumnName("actions").HasMaxLength(64);
			builder.Property(e => e.Outcome).HasColumnName("outcome").HasMaxLength(32);
			builder.Property(e => e.Facts).HasColumnName("facts");
			builder.Property(e => e.ReviewState).HasColumnName("review_state").HasMaxLength(16);
			builder.Property(e => e.ReviewedBy).HasColumnName("reviewed_by");
			builder.Property(e => e.ReviewedAt).HasColumnName("reviewed_at");
			builder.Property(e => e.CaseID).HasColumnName("case_id");
			builder.HasIndex(e => e.CaseID).HasDatabaseName("ix_automated_rule_matches_case");
		}
	}
}
