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

using GenOnlineService;
using GenOnlineService.Rules;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public static class ModerationCaseStatuses
{
	public const string Open = "open";
	public const string Closed = "closed";
}

public static class ModerationCaseOutcomes
{
	public const string NoAction = "no_action";
	public const string Sanctioned = "sanctioned";
}

public static class CaseSignalKinds
{
	public const string Report = "report";
	public const string RuleMatch = "rule_match";

	public static readonly IReadOnlyList<string> AntiCheat = AntiCheatReviewKinds.All;
	public static readonly IReadOnlyList<string> All = [Report, .. AntiCheatReviewKinds.All, RuleMatch];

	public static bool IsKnown(string? kind) => kind != null && All.Contains(kind);

	// reports are for any staff member, anti-cheat rows and rule matches need anticheat.review
	public static bool NeedsAntiCheatReview(string kind) => kind != Report;
}

// A case is open only while a staff member has claimed it; a later signal waits for the player's next case.
public class ModerationCase
{
	public Int64 ID { get; set; }
	public Int64 UserID { get; set; }
	public string Status { get; set; } = ModerationCaseStatuses.Open;
	public DateTime OpenedAt { get; set; } = DateTime.UnixEpoch;

	public Int64? ClaimedByID { get; set; }
	public DateTime? ClaimedAt { get; set; }

	public DateTime? ClosedAt { get; set; }
	public Int64? ClosedByID { get; set; }
	public string? Outcome { get; set; }

	public string? Note { get; set; }

	public Int64? SanctionID { get; set; }
}

public class ModerationCaseConfiguration : IEntityTypeConfiguration<ModerationCase>
{
	public void Configure(EntityTypeBuilder<ModerationCase> builder)
	{
		builder.ToTable("moderation_cases");

		builder.HasKey(e => e.ID);
		builder.HasIndex(e => new { e.UserID, e.Status }).HasDatabaseName("ix_moderation_cases_user_status");
		builder.HasIndex(e => new { e.Status, e.ClosedAt }).HasDatabaseName("ix_moderation_cases_status_closed_at");

		builder.Property(e => e.ID).HasColumnName("id").ValueGeneratedOnAdd();
		builder.Property(e => e.UserID).HasColumnName("user_id");
		builder.Property(e => e.Status).HasColumnName("status").HasMaxLength(16);
		builder.Property(e => e.OpenedAt).HasColumnName("opened_at");
		builder.Property(e => e.ClaimedByID).HasColumnName("claimed_by_id");
		builder.Property(e => e.ClaimedAt).HasColumnName("claimed_at");
		builder.Property(e => e.ClosedAt).HasColumnName("closed_at");
		builder.Property(e => e.ClosedByID).HasColumnName("closed_by_id");
		builder.Property(e => e.Outcome).HasColumnName("outcome").HasMaxLength(16);
		builder.Property(e => e.Note).HasColumnName("note").HasMaxLength(ModerationManager.MaximumReasonLength);
		builder.Property(e => e.SanctionID).HasColumnName("sanction_id");
	}
}

namespace Database
{
	public sealed class CaseSignalRow
	{
		public string Kind { get; init; } = "";
		public Int64 ID { get; init; }
		public Int64 UserID { get; init; }
		public Int64? MatchID { get; init; }
		public Int64? LobbyID { get; init; }

		// null for an anti-cheat row stored before the kinds kept their time
		public DateTime? At { get; init; }

		public string? ModuleName { get; init; }
		public string? ModulePath { get; init; }
		public Int64? ModuleSize { get; init; }
		public string? Reason { get; init; }
		public string? Request { get; init; }
		public string? Detail { get; init; }
		public string? State { get; init; }
		public string? EacKind { get; init; }
		public bool? SelfReported { get; init; }
		public string? EpicReferenceId { get; init; }
		public string? EpicAction { get; init; }
		public DateTime? EpicPlacedAt { get; init; }
		public DateTime? EpicExpiresAt { get; init; }

		public PlayerReport? Report { get; init; }

		public GenOnlineService.Rules.AutomatedRuleMatch? RuleMatch { get; init; }
	}

	public sealed class CaseQueueEntry
	{
		public Int64 UserID { get; init; }
		public Dictionary<string, int> Waiting { get; } = new(StringComparer.Ordinal);
		public DateTime? FirstAt { get; set; }
		public DateTime? LastAt { get; set; }

		public void Add(string kind, int count, DateTime? first, DateTime? last)
		{
			Waiting[kind] = count;
			if (first != null && (FirstAt == null || first < FirstAt))
			{
				FirstAt = first;
			}
			if (last != null && (LastAt == null || last > LastAt))
			{
				LastAt = last;
			}
		}
	}

	public readonly record struct CaseSignalKey(string Kind, Int64 ID);

	public enum CaseCloseResult { Closed, NothingWaiting }

	public static class Cases
	{
		public static async Task<Dictionary<long, CaseQueueEntry>> Queue(AppDbContext db, IReadOnlyCollection<string> kinds, CancellationToken cancellationToken = default)
		{
			Dictionary<long, CaseQueueEntry> entries = new();
			void Add(string kind, long userID, int count, DateTime? first, DateTime? last)
			{
				if (!entries.TryGetValue(userID, out CaseQueueEntry? entry))
				{
					entry = new CaseQueueEntry { UserID = userID };
					entries[userID] = entry;
				}
				entry.Add(kind, count, first, last);
			}

			if (kinds.Contains(CaseSignalKinds.Report))
			{
				foreach (var g in await db.PlayerReports.AsNoTracking().Where(r => r.Status == PlayerReportStatuses.Open)
					.GroupBy(r => r.ReportedUserID).Select(g => new { g.Key, Count = g.Count(), First = g.Min(r => r.CreatedAt), Last = g.Max(r => r.CreatedAt) })
					.ToListAsync(cancellationToken))
				{
					Add(CaseSignalKinds.Report, g.Key, g.Count, g.First, g.Last);
				}
			}
			foreach (string kind in CaseSignalKinds.AntiCheat.Where(kinds.Contains))
			{
				foreach (var g in await AntiCheatReviews.Rows(db, kind).Where(r => r.ReviewedAt == null)
					.GroupBy(r => r.UserID).Select(g => new { g.Key, Count = g.Count(), First = g.Min(r => r.ReportedAt), Last = g.Max(r => r.ReportedAt) })
					.ToListAsync(cancellationToken))
				{
					Add(kind, g.Key, g.Count, g.First, g.Last);
				}
			}
			if (kinds.Contains(CaseSignalKinds.RuleMatch))
			{
				foreach (var g in await db.AutomatedRuleMatches.AsNoTracking().Where(m => m.ReviewState == RuleReviewStates.Pending)
					.GroupBy(m => m.UserID).Select(g => new { g.Key, Count = g.Count(), First = g.Min(m => m.MatchedAt), Last = g.Max(m => m.MatchedAt) })
					.ToListAsync(cancellationToken))
				{
					Add(CaseSignalKinds.RuleMatch, g.Key, g.Count, g.First, g.Last);
				}
			}
			return entries;
		}

		public static async Task<List<CaseSignalRow>> Signals(AppDbContext db, long userID, long? caseID, IReadOnlyCollection<string> kinds, CancellationToken cancellationToken = default)
		{
			List<CaseSignalRow> signals = new();
			if (kinds.Contains(CaseSignalKinds.Report))
			{
				IQueryable<PlayerReport> reports = db.PlayerReports.AsNoTracking().Where(r => r.ReportedUserID == userID);
				reports = caseID is long id ? reports.Where(r => r.CaseID == id) : reports.Where(r => r.Status == PlayerReportStatuses.Open);
				signals.AddRange((await reports.ToListAsync(cancellationToken)).Select(r => new CaseSignalRow
				{
					Kind = CaseSignalKinds.Report, ID = r.ID, UserID = r.ReportedUserID, MatchID = r.MatchID, LobbyID = r.LobbyID, At = r.CreatedAt, Report = r
				}));
			}
			foreach (string kind in CaseSignalKinds.AntiCheat.Where(kinds.Contains))
			{
				IQueryable<AntiCheatReviewRow> rows = AntiCheatReviews.Rows(db, kind).Where(r => r.UserID == userID);
				rows = caseID is long id ? rows.Where(r => r.CaseID == id) : rows.Where(r => r.ReviewedAt == null);
				signals.AddRange((await rows.ToListAsync(cancellationToken)).Select(r => new CaseSignalRow
				{
					Kind = kind, ID = r.ReportID, UserID = r.UserID, MatchID = r.MatchID > 0 ? r.MatchID : null, LobbyID = r.LobbyID, At = r.ReportedAt,
					ModuleName = r.ModuleName, ModulePath = r.ModulePath, ModuleSize = r.ModuleSize, Reason = r.Reason, Request = r.Request, Detail = r.Detail, State = r.State,
					EacKind = r.EacKind, SelfReported = r.SelfReported, EpicReferenceId = r.EpicReferenceId, EpicAction = r.EpicAction, EpicPlacedAt = r.EpicPlacedAt, EpicExpiresAt = r.EpicExpiresAt
				}));
			}
			if (kinds.Contains(CaseSignalKinds.RuleMatch))
			{
				IQueryable<GenOnlineService.Rules.AutomatedRuleMatch> matches = db.AutomatedRuleMatches.AsNoTracking().Where(m => m.UserID == userID);
				matches = caseID is long id ? matches.Where(m => m.CaseID == id) : matches.Where(m => m.ReviewState == RuleReviewStates.Pending);
				signals.AddRange((await matches.ToListAsync(cancellationToken)).Select(m => new CaseSignalRow
				{
					Kind = CaseSignalKinds.RuleMatch, ID = m.ID, UserID = m.UserID, At = m.MatchedAt, RuleMatch = m
				}));
			}
			// an anti-cheat row without its own time sorts by id within its kind
			return signals.OrderByDescending(s => s.At ?? DateTime.MinValue).ThenByDescending(s => s.ID).ToList();
		}

		public static Task<ModerationCase?> GetOpen(AppDbContext db, long userID, CancellationToken cancellationToken = default) =>
			db.ModerationCases.AsNoTracking().Where(c => c.UserID == userID && c.Status == ModerationCaseStatuses.Open).OrderBy(c => c.ID).FirstOrDefaultAsync(cancellationToken);

		public static Task<ModerationCase?> Get(AppDbContext db, long caseID, CancellationToken cancellationToken = default) =>
			db.ModerationCases.AsNoTracking().FirstOrDefaultAsync(c => c.ID == caseID, cancellationToken);

		public static async Task<Dictionary<long, ModerationCase>> OpenOf(AppDbContext db, IReadOnlyCollection<long> userIDs, CancellationToken cancellationToken = default)
		{
			List<ModerationCase> open = await db.ModerationCases.AsNoTracking().Where(c => userIDs.Contains(c.UserID) && c.Status == ModerationCaseStatuses.Open).ToListAsync(cancellationToken);
			return open.GroupBy(c => c.UserID).ToDictionary(g => g.Key, g => g.OrderBy(c => c.ID).First());
		}

		public static Task<List<ModerationCase>> History(AppDbContext db, long userID, int limit, CancellationToken cancellationToken = default) =>
			db.ModerationCases.AsNoTracking().Where(c => c.UserID == userID && c.Status == ModerationCaseStatuses.Closed)
				.OrderByDescending(c => c.ClosedAt).ThenByDescending(c => c.ID).Take(limit).ToListAsync(cancellationToken);

		// the row lock on the player keeps one open case per player
		public static async Task<ModerationCase> Claim(AppDbContext db, long userID, long staffID, DateTime now, CancellationToken cancellationToken = default)
		{
			await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
			await LockPlayer(db, userID, cancellationToken);
			ModerationCase? open = await db.ModerationCases.AsTracking().Where(c => c.UserID == userID && c.Status == ModerationCaseStatuses.Open).OrderBy(c => c.ID).FirstOrDefaultAsync(cancellationToken);
			if (open == null)
			{
				open = new ModerationCase { UserID = userID, Status = ModerationCaseStatuses.Open, OpenedAt = now };
				db.ModerationCases.Add(open);
			}
			open.ClaimedByID = staffID;
			open.ClaimedAt = now;
			await db.SaveChangesAsync(cancellationToken);
			await transaction.CommitAsync(cancellationToken);
			db.ChangeTracker.Clear();
			return open;
		}

		public static async Task<bool> Release(AppDbContext db, long userID, CancellationToken cancellationToken = default) =>
			await db.ModerationCases.Where(c => c.UserID == userID && c.Status == ModerationCaseStatuses.Open).ExecuteDeleteAsync(cancellationToken) > 0;

		// signals decided meanwhile are skipped; nothing is stored when none waits
		public static async Task<(CaseCloseResult Result, ModerationCase? Case, int Decided)> Close(AppDbContext db, long userID, IReadOnlyCollection<CaseSignalKey> signals, long staffID,
			string resolvedByKind, string outcome, string? note, long? sanctionID, DateTime now, CancellationToken cancellationToken = default)
		{
			await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
			await LockPlayer(db, userID, cancellationToken);

			ModerationCase? decision = await db.ModerationCases.AsTracking().Where(c => c.UserID == userID && c.Status == ModerationCaseStatuses.Open).OrderBy(c => c.ID).FirstOrDefaultAsync(cancellationToken);
			if (decision == null)
			{
				decision = new ModerationCase { UserID = userID, OpenedAt = now };
				db.ModerationCases.Add(decision);
			}
			decision.Status = ModerationCaseStatuses.Closed;
			decision.ClosedAt = now;
			decision.ClosedByID = staffID;
			decision.Outcome = outcome;
			decision.Note = note;
			decision.SanctionID = sanctionID;
			await db.SaveChangesAsync(cancellationToken);
			long caseID = decision.ID;

			bool sanctioned = outcome == ModerationCaseOutcomes.Sanctioned;
			int decided = 0;
			foreach (IGrouping<string, CaseSignalKey> kind in signals.GroupBy(s => s.Kind))
			{
				long[] ids = kind.Select(s => s.ID).Distinct().ToArray();
				decided += kind.Key switch
				{
					CaseSignalKinds.Report => await db.PlayerReports
						.Where(r => ids.Contains(r.ID) && r.ReportedUserID == userID && r.Status == PlayerReportStatuses.Open)
						.ExecuteUpdateAsync(s => s
							.SetProperty(r => r.Status, sanctioned ? PlayerReportStatuses.Actioned : PlayerReportStatuses.Dismissed)
							.SetProperty(r => r.ResolvedAt, now)
							.SetProperty(r => r.ResolvedByKind, resolvedByKind)
							.SetProperty(r => r.ResolvedByID, staffID)
							.SetProperty(r => r.ResolutionNote, note)
							.SetProperty(r => r.CaseID, caseID), cancellationToken),
					CaseSignalKinds.RuleMatch => await db.AutomatedRuleMatches
						.Where(m => ids.Contains(m.ID) && m.UserID == userID && m.ReviewState == RuleReviewStates.Pending)
						.ExecuteUpdateAsync(s => s
							.SetProperty(m => m.ReviewState, sanctioned ? RuleReviewStates.Reviewed : RuleReviewStates.Dismissed)
							.SetProperty(m => m.ReviewedBy, staffID)
							.SetProperty(m => m.ReviewedAt, now)
							.SetProperty(m => m.CaseID, caseID), cancellationToken),
					_ => await AntiCheatReviews.Decide(db, kind.Key, ids, userID, staffID, caseID, now, cancellationToken)
				};
			}

			// a sanction keeps its case even when every signal was decided meanwhile
			if (decided == 0 && sanctionID == null)
			{
				await transaction.RollbackAsync(cancellationToken);
				db.ChangeTracker.Clear();
				return (CaseCloseResult.NothingWaiting, null, 0);
			}

			await transaction.CommitAsync(cancellationToken);
			db.ChangeTracker.Clear();
			return (CaseCloseResult.Closed, decision, decided);
		}

		// The same lock the sanctions take (SanctionActions), so two decisions about one player run one after the other
		private static Task LockPlayer(AppDbContext db, long userID, CancellationToken cancellationToken) =>
			db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM users WHERE id = {userID} FOR UPDATE", cancellationToken);
	}
}
