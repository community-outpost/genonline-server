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

using GenOnlineService.Api;
using GenOnlineService.OpenApi;
using GenOnlineService.Realtime;
using GenOnlineService.Rules;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GenOnlineService.Rest
{
	public sealed record CaseQueueRow
	{
		public Member Player { get; init; } = new Member();
		public DateTime AccountCreatedAt { get; init; }
		public bool Banned { get; init; }
		public bool Muted { get; init; }

		public int PriorCases { get; init; }

		public IReadOnlyDictionary<string, int> Waiting { get; init; } = new Dictionary<string, int>();
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public DateTime? FirstAt { get; init; }
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public DateTime? LastAt { get; init; }

		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public Member? ClaimedBy { get; init; }
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public DateTime? ClaimedAt { get; init; }

		// the order of the queue: anti-cheat signals before rule matches before reports, more of them first
		public int Priority { get; init; }
	}

	public sealed record CasesResponse
	{
		public IReadOnlyList<CaseQueueRow> Cases { get; init; } = [];
	}

	public sealed record CaseSummary
	{
		public long Id { get; init; }
		public string Status { get; init; } = "";
		public DateTime OpenedAt { get; init; }
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public Member? ClaimedBy { get; init; }
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public DateTime? ClaimedAt { get; init; }
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public DateTime? ClosedAt { get; init; }
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public Member? ClosedBy { get; init; }
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public string? Outcome { get; init; }
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public string? Note { get; init; }
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public long? SanctionId { get; init; }
	}

	public sealed record CaseMessage
	{
		public long Id { get; init; }
		public Member Author { get; init; } = new Member();
		public string Content { get; init; } = "";
		public DateTime At { get; init; }
		public bool Deleted { get; init; }
		public string Part { get; init; } = "";

		// Match lines only: everyone, allies or self
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public string? Scope { get; init; }
	}

	public sealed record CaseSignal
	{
		public string Kind { get; init; } = "";
		public long Id { get; init; }
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public DateTime? At { get; init; }
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public long? MatchId { get; init; }
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public long? LobbyId { get; init; }

		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public Member? Reporter { get; init; }
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public string? Category { get; init; }
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public string? MessageId { get; init; }
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public IReadOnlyList<CaseMessage>? Conversation { get; init; }
		// the messages after the reported one are filled in just before chat retention removes the message
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public bool? AfterPending { get; init; }

		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public string? Reason { get; init; }

		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public string? ModuleName { get; init; }
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public string? ModulePath { get; init; }
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public long? ModuleSize { get; init; }

		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public string? Request { get; init; }
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public string? Detail { get; init; }
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public string? State { get; init; }

		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public string? EacKind { get; init; }
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public bool? SelfReported { get; init; }
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public IReadOnlyList<CaseEacWitness>? Witnesses { get; init; }

		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public string? EpicReferenceId { get; init; }
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public string? EpicAction { get; init; }
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public DateTime? EpicPlacedAt { get; init; }
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public DateTime? EpicExpiresAt { get; init; }

		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public long? RuleId { get; init; }
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public string? RuleName { get; init; }
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public int? RuleVersion { get; init; }
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public string? Trigger { get; init; }
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public string? RuleOutcome { get; init; }
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public string? Actions { get; init; }
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public JsonElement? Facts { get; init; }
	}

	public sealed record CaseEacWitness
	{
		public Member Reporter { get; init; } = new Member();
		public DateTime At { get; init; }
		public int Action { get; init; }
		public int ReasonCode { get; init; }
		public int ViolationType { get; init; }
		public string Details { get; init; } = "";
	}

	public sealed record CaseSanction
	{
		public long Id { get; init; }
		public string Type { get; init; } = "";
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public DateTime? ExpiresAt { get; init; }
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public string? Reason { get; init; }
	}

	public sealed record CaseStanding
	{
		public DateTime AccountCreatedAt { get; init; }
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public DateTime? LastSignInAt { get; init; }
		public IReadOnlyList<CaseSanction> ActiveSanctions { get; init; } = [];
		public int Sanctions { get; init; }
		public int OpenAppeals { get; init; }
		public IReadOnlyList<CaseSummary> PriorCases { get; init; } = [];
	}

	public sealed record CaseResponse
	{
		public Member Player { get; init; } = new Member();
		public CaseStanding Standing { get; init; } = new CaseStanding();

		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public CaseSummary? Case { get; init; }
		public IReadOnlyList<CaseSignal> Signals { get; init; } = [];

		public IReadOnlyList<MatchResponse> Matches { get; init; } = [];
	}

	public sealed record CaseSignalRef
	{
		public string? Kind { get; init; }
		public long Id { get; init; }
	}

	public sealed record CloseCaseRequest
	{
		public IReadOnlyList<CaseSignalRef>? Signals { get; init; }

		public string? Outcome { get; init; }
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public CreateSanctionRequest? Sanction { get; init; }
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public string? Note { get; init; }
	}

	public sealed record CloseCaseResponse
	{
		public long CaseId { get; init; }
		public int Decided { get; init; }
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public long? SanctionId { get; init; }
	}

	public static class CaseActions
	{
		// shortcut: the queue is built and paged in memory, fine for hundreds of players with waiting signals; page in the database past that
		private const int MatchesShown = 10;
		private const int PriorCasesShown = 20;

		private static readonly Dictionary<string, int> s_weights = new(StringComparer.Ordinal)
		{
			[AntiCheatReviewKinds.Modules] = 50,
			[AntiCheatReviewKinds.Probes] = 40,
			[AntiCheatReviewKinds.Devices] = 30,
			[AntiCheatReviewKinds.IllegalRequests] = 30,
			[AntiCheatReviewKinds.Eac] = 40,
			[AntiCheatReviewKinds.Epic] = 50,
			[CaseSignalKinds.RuleMatch] = 20,
			[CaseSignalKinds.Report] = 10
		};

		// repeats count, up to five of a kind
		internal static int PriorityOf(IReadOnlyDictionary<string, int> waiting) =>
			waiting.Sum(pair => s_weights.GetValueOrDefault(pair.Key, 1) * Math.Min(pair.Value, 5));

		public static async Task<(int Total, CasesResponse Response)> QueueAsync(AppDbContext db, IReadOnlyCollection<string> kinds, long me, string claimed, int skip, int take, CancellationToken cancellationToken)
		{
			Dictionary<long, Database.CaseQueueEntry> entries = await Database.Cases.Queue(db, kinds, cancellationToken);
			long[] ids = entries.Keys.ToArray();
			Dictionary<long, ModerationCase> claims = await Database.Cases.OpenOf(db, ids, cancellationToken);

			IEnumerable<Database.CaseQueueEntry> rows = entries.Values;
			if (claimed == "mine")
			{
				rows = rows.Where(e => claims.TryGetValue(e.UserID, out ModerationCase? c) && c.ClaimedByID == me);
			}
			else if (claimed == "unclaimed")
			{
				rows = rows.Where(e => !claims.ContainsKey(e.UserID));
			}
			List<Database.CaseQueueEntry> ordered = rows.OrderByDescending(e => PriorityOf(e.Waiting)).ThenByDescending(e => e.LastAt).ThenBy(e => e.UserID).ToList();
			List<Database.CaseQueueEntry> page = ordered.Skip(skip).Take(take).ToList();
			long[] pageIDs = page.Select(e => e.UserID).ToArray();

			DateTime now = DateTime.UtcNow;
			Dictionary<long, DateTime> created = await db.Users.AsNoTracking().Where(u => pageIDs.Contains(u.ID)).ToDictionaryAsync(u => u.ID, u => u.CreatedAt, cancellationToken);
			HashSet<long> banned = (await Database.Moderation.ActiveSanctions(db, SanctionTypes.Ban, now).Where(s => pageIDs.Contains(s.UserID)).Select(s => s.UserID).ToListAsync(cancellationToken)).ToHashSet();
			HashSet<long> muted = (await Database.Moderation.ActiveSanctions(db, SanctionTypes.Mute, now).Where(s => pageIDs.Contains(s.UserID)).Select(s => s.UserID).ToListAsync(cancellationToken)).ToHashSet();
			Dictionary<long, int> prior = await db.ModerationCases.AsNoTracking().Where(c => pageIDs.Contains(c.UserID) && c.Status == ModerationCaseStatuses.Closed)
				.GroupBy(c => c.UserID).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count, cancellationToken);
			Dictionary<long, (string Name, int Tag)> names = await Database.DisplayNames.GetBulk(db, pageIDs.Concat(page.Select(e => claims.TryGetValue(e.UserID, out ModerationCase? c) ? c.ClaimedByID ?? 0 : 0)).Where(id => id > 0).ToHashSet());

			return (ordered.Count, new CasesResponse
			{
				Cases = page.Select(e =>
				{
					ModerationCase? claim = claims.GetValueOrDefault(e.UserID);
					return new CaseQueueRow
					{
						Player = RealtimeMembers.Of(e.UserID, names),
						AccountCreatedAt = created.GetValueOrDefault(e.UserID),
						Banned = banned.Contains(e.UserID),
						Muted = muted.Contains(e.UserID),
						PriorCases = prior.GetValueOrDefault(e.UserID),
						Waiting = e.Waiting,
						FirstAt = e.FirstAt,
						LastAt = e.LastAt,
						ClaimedBy = claim?.ClaimedByID is long by ? RealtimeMembers.Of(by, names) : null,
						ClaimedAt = claim?.ClaimedAt,
						Priority = PriorityOf(e.Waiting)
					};
				}).ToArray()
			});
		}

		public static async Task<CaseResponse> DetailAsync(AppDbContext db, long userID, ModerationCase? decided, IReadOnlyCollection<string> kinds, CancellationToken cancellationToken)
		{
			List<Database.CaseSignalRow> rows = await Database.Cases.Signals(db, userID, decided?.ID, kinds, cancellationToken);
			ModerationCase? shown = decided ?? await Database.Cases.GetOpen(db, userID, cancellationToken);
			List<ModerationCase> history = await Database.Cases.History(db, userID, PriorCasesShown, cancellationToken);

			Dictionary<long, Database.Chat.ReportEvidence> evidence = rows.Where(r => r.Report != null).ToDictionary(r => r.ID, r => Database.Chat.ReadEvidence(r.Report!));
			HashSet<long> people = [userID];
			people.UnionWith(rows.Where(r => r.Report != null).Select(r => r.Report!.ReporterID));
			people.UnionWith(evidence.Values.SelectMany(e => e.SenderIDs));
			long[] eacIDs = rows.Where(r => r.Kind == AntiCheatReviewKinds.Eac).Select(r => r.ID).ToArray();
			Dictionary<long, List<AnticheatEacWitness>> witnesses = eacIDs.Length == 0 ? [] : (await db.AnticheatEacWitnesses.AsNoTracking()
				.Where(w => eacIDs.Contains(w.FindingId)).OrderBy(w => w.WitnessId).ToListAsync(cancellationToken))
				.GroupBy(w => w.FindingId).ToDictionary(g => g.Key, g => g.ToList());
			people.UnionWith(witnesses.Values.SelectMany(list => list).Select(w => w.ReporterId));
			people.UnionWith(history.Append(shown).Where(c => c != null).SelectMany(c => new[] { c!.ClaimedByID ?? 0, c.ClosedByID ?? 0 }).Where(id => id > 0));
			Dictionary<long, (string Name, int Tag)> names = await Database.DisplayNames.GetBulk(db, people);

			long[] ruleIDs = rows.Where(r => r.RuleMatch != null).Select(r => r.RuleMatch!.RuleID).Distinct().ToArray();
			Dictionary<long, string> ruleNames = ruleIDs.Length == 0 ? [] : await db.AutomatedRules.AsNoTracking().Where(r => ruleIDs.Contains(r.ID)).ToDictionaryAsync(r => r.ID, r => r.Name, cancellationToken);

			List<MatchResponse> matches = new();
			foreach (long matchID in rows.Where(r => r.MatchID > 0).Select(r => r.MatchID!.Value).Distinct().Take(MatchesShown))
			{
				MatchHistory_Entry? entry = await Database.MatchHistory.LoadMatchHistoryEntryAsync(db, matchID, cancellationToken);
				if (entry != null)
				{
					Dictionary<long, (string Name, int Tag)> tags = await Database.DisplayNames.GetBulk(db, MatchResponse.UserIdsOf(entry).ToHashSet());
					matches.Add(MatchResponse.Of(entry, tags.ToDictionary(pair => pair.Key, pair => pair.Value.Tag)));
				}
			}

			return new CaseResponse
			{
				Player = RealtimeMembers.Of(userID, names),
				Standing = await StandingAsync(db, userID, history, names, cancellationToken),
				Case = shown == null ? null : SummaryOf(shown, names),
				Signals = rows.Select(r => SignalOf(r, evidence, names, ruleNames, witnesses)).ToArray(),
				Matches = matches
			};
		}

		private static async Task<CaseStanding> StandingAsync(AppDbContext db, long userID, List<ModerationCase> history, Dictionary<long, (string Name, int Tag)> names, CancellationToken cancellationToken)
		{
			DateTime now = DateTime.UtcNow;
			var user = await db.Users.AsNoTracking().Where(u => u.ID == userID).Select(u => new { u.CreatedAt, u.LastSignInAt }).FirstOrDefaultAsync(cancellationToken);
			List<Sanction> sanctions = await db.Sanctions.AsNoTracking().Where(s => s.UserID == userID).ToListAsync(cancellationToken);
			int openAppeals = await db.Appeals.AsNoTracking().CountAsync(a => a.UserID == userID && a.Status == AppealStatuses.Open, cancellationToken);
			return new CaseStanding
			{
				AccountCreatedAt = user?.CreatedAt ?? DateTime.UnixEpoch,
				LastSignInAt = user?.LastSignInAt,
				ActiveSanctions = sanctions.Where(s => s.Type != SanctionTypes.Kick && s.LiftedAt == null && (s.ExpiresAt == null || s.ExpiresAt > now))
					.Select(s => new CaseSanction { Id = s.ID, Type = s.Type, ExpiresAt = s.ExpiresAt, Reason = s.Reason }).ToArray(),
				Sanctions = sanctions.Count,
				OpenAppeals = openAppeals,
				PriorCases = history.Select(c => SummaryOf(c, names)).ToArray()
			};
		}

		private static CaseSummary SummaryOf(ModerationCase c, IReadOnlyDictionary<long, (string Name, int Tag)> names) => new CaseSummary
		{
			Id = c.ID,
			Status = c.Status,
			OpenedAt = c.OpenedAt,
			ClaimedBy = c.ClaimedByID is long claimer ? RealtimeMembers.Of(claimer, names) : null,
			ClaimedAt = c.ClaimedAt,
			ClosedAt = c.ClosedAt,
			ClosedBy = c.ClosedByID is long closer ? RealtimeMembers.Of(closer, names) : null,
			Outcome = c.Outcome,
			Note = c.Note,
			SanctionId = c.SanctionID
		};

		private static CaseSignal SignalOf(Database.CaseSignalRow row, Dictionary<long, Database.Chat.ReportEvidence> evidence, IReadOnlyDictionary<long, (string Name, int Tag)> names, IReadOnlyDictionary<long, string> ruleNames,
			IReadOnlyDictionary<long, List<AnticheatEacWitness>> witnesses)
		{
			CaseSignal signal = new CaseSignal { Kind = row.Kind, Id = row.ID, At = row.At, MatchId = row.MatchID, LobbyId = row.LobbyID };
			if (row.Report is PlayerReport report)
			{
				Database.Chat.ReportEvidence found = evidence[row.ID];
				CaseMessage Line(Database.Chat.ReportEvidenceMessage m, string part) => new CaseMessage
				{
					Id = m.ID, Author = RealtimeMembers.Of(m.SenderID, names), Content = m.Content, At = m.CreatedAt, Deleted = m.DeletedAt != null, Part = part, Scope = m.Scope
				};
				List<CaseMessage> conversation = found.Message == null
					? [.. found.Messages.Select(m => Line(m, "recent")), .. found.MatchMessages.Select(m => Line(m, "match"))]
					: [.. found.Before.Select(m => Line(m, "before")), Line(found.Message, "message"), .. found.After.Select(m => Line(m, "after"))];
				return signal with
				{
					Reporter = RealtimeMembers.Of(report.ReporterID, names),
					Category = report.Category,
					Reason = report.Reason,
					MessageId = report.MessageID?.ToString(CultureInfo.InvariantCulture),
					Conversation = conversation,
					AfterPending = found.Message != null ? found.AfterPending : null
				};
			}
			if (row.RuleMatch is AutomatedRuleMatch match)
			{
				JsonElement? facts = null;
				try
				{
					facts = JsonDocument.Parse(match.Facts).RootElement.Clone();
				}
				catch (JsonException)
				{
				}
				return signal with
				{
					RuleId = match.RuleID,
					RuleName = ruleNames.GetValueOrDefault(match.RuleID),
					RuleVersion = match.RuleVersion,
					Trigger = match.Trigger,
					RuleOutcome = match.Outcome,
					Actions = match.Actions,
					Facts = facts
				};
			}
			return signal with
			{
				Reason = row.Reason,
				ModuleName = row.ModuleName,
				ModulePath = row.ModulePath,
				ModuleSize = row.ModuleSize,
				Request = row.Request,
				Detail = row.Detail,
				State = row.State,
				EacKind = row.EacKind,
				SelfReported = row.SelfReported,
				Witnesses = row.Kind == AntiCheatReviewKinds.Eac ? witnesses.GetValueOrDefault(row.ID, []).Select(w => new CaseEacWitness
				{
					Reporter = RealtimeMembers.Of(w.ReporterId, names), At = w.CreatedAt, Action = w.Action, ReasonCode = w.ReasonCode, ViolationType = w.ViolationType, Details = w.Details
				}).ToArray() : null,
				EpicReferenceId = row.EpicReferenceId,
				EpicAction = row.EpicAction,
				EpicPlacedAt = row.EpicPlacedAt,
				EpicExpiresAt = row.EpicExpiresAt
			};
		}

		internal static Failure? Validate(CloseCaseRequest? request, IReadOnlyCollection<string> allowed)
		{
			if (request?.Signals == null || request.Signals.Count == 0 || request.Signals.Count > 500)
			{
				return Result.Invalid("signals");
			}
			foreach (CaseSignalRef signal in request.Signals)
			{
				if (!CaseSignalKinds.IsKnown(signal.Kind) || signal.Id <= 0)
				{
					return Result.Invalid("signals");
				}
				if (!allowed.Contains(signal.Kind!))
				{
					return Result.Forbidden(Permissions.AnticheatReview);
				}
			}
			if (request.Outcome != ModerationCaseOutcomes.NoAction && request.Outcome != ModerationCaseOutcomes.Sanctioned)
			{
				return Result.Invalid("outcome");
			}
			if ((request.Outcome == ModerationCaseOutcomes.Sanctioned) != (request.Sanction != null))
			{
				return Result.Invalid("sanction");
			}
			if (request.Note != null && request.Note.Length > ModerationManager.MaximumReasonLength)
			{
				return Result.Fail(ErrorCodes.ModerationReasonTooLong, new { Max = ModerationManager.MaximumReasonLength });
			}
			return null;
		}
	}

	// Reports need a staff permission (moderation.kick, mute or ban), the other kinds anticheat.review
	[ApiController]
	[AllowSignInCookie]
	public sealed class CasesController : ControllerBase
	{
		private readonly IDbContextFactory<AppDbContext> m_dbFactory;
		private readonly IAuthorizationService m_authorization;

		public CasesController(IDbContextFactory<AppDbContext> dbFactory, IAuthorizationService authorization)
		{
			m_dbFactory = dbFactory;
			m_authorization = authorization;
		}

		private long Me => RealtimeClaims.UserID(User);

		private async Task<bool> HasAsync(string permission) => (await m_authorization.AuthorizeAsync(User, PermissionPolicies.For(permission))).Succeeded;

		private async Task<IReadOnlyList<string>> KindsAsync()
		{
			List<string> kinds = new();
			if (await StaffAccess.IsStaffAsync(m_authorization, User))
			{
				kinds.Add(CaseSignalKinds.Report);
			}
			if (await HasAsync(Permissions.AnticheatReview))
			{
				kinds.AddRange(CaseSignalKinds.AntiCheat);
				kinds.Add(CaseSignalKinds.RuleMatch);
			}
			return kinds;
		}

		[EndpointSummary("Lists the players with waiting moderation signals, most urgent first.")]
		[EndpointDescription("kind narrows to some signal kinds (comma separated), claimed is all (default), mine or unclaimed.")]
		[ProducesResponseType<CasesResponse>(StatusCodes.Status200OK)]
		[ApiPermission(Permissions.ModerationKick, Permissions.ModerationMute, Permissions.ModerationBan, Permissions.AnticheatReview)]
		[HttpGet("~/api/v1/admin/cases")]
		public async Task<IActionResult> List([FromQuery] string? kind, [FromQuery] string? claimed)
		{
			IReadOnlyList<string> allowed = await KindsAsync();
			if (allowed.Count == 0)
			{
				return RestResults.Fail(Result.Forbidden(Permissions.ModerationKick));
			}
			List<string> kinds = allowed.ToList();
			if (kind != null)
			{
				string[] asked = kind.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
				if (asked.Length == 0 || asked.Any(k => !CaseSignalKinds.IsKnown(k)))
				{
					return RestResults.Invalid("kind");
				}
				kinds = kinds.Where(asked.Contains).ToList();
			}
			claimed ??= "all";
			if (claimed is not ("all" or "mine" or "unclaimed"))
			{
				return RestResults.Invalid("claimed");
			}
			(OffsetPage page, Failure? invalid) = Paging.ReadOffset(Request);
			if (invalid != null)
			{
				return RestResults.Fail(invalid.Value);
			}

			await using AppDbContext db = await m_dbFactory.CreateDbContextAsync(HttpContext.RequestAborted);
			(int total, CasesResponse response) = await CaseActions.QueueAsync(db, kinds, Me, claimed, page.Skip, page.PerPage, HttpContext.RequestAborted);
			Paging.WriteOffsetHeaders(HttpContext, page, total);
			return RestResults.Ok(response);
		}

		// Shows chat and anti-cheat evidence of a player, so every read is audited
		[EndpointSummary("Reads what waits about one player, with its evidence, matches and the player's standing.")]
		[ProducesResponseType<CaseResponse>(StatusCodes.Status200OK)]
		[ApiPermission(Permissions.ModerationKick, Permissions.ModerationMute, Permissions.ModerationBan, Permissions.AnticheatReview)]
		[HttpGet("~/api/v1/admin/players/{userID:long}/case")]
		public async Task<IActionResult> Waiting(long userID)
		{
			IReadOnlyList<string> allowed = await KindsAsync();
			if (allowed.Count == 0)
			{
				return RestResults.Fail(Result.Forbidden(Permissions.ModerationKick));
			}
			await using AppDbContext db = await m_dbFactory.CreateDbContextAsync(HttpContext.RequestAborted);
			if (userID <= 0 || !await Database.Users.Exists(db, userID))
			{
				return RestResults.NotFound("user");
			}
			CaseResponse response = await CaseActions.DetailAsync(db, userID, null, allowed, HttpContext.RequestAborted);
			await Database.Moderation.LogAction(db, SanctionActions.ApiActorOf(Me), ModerationActions.CaseView, AuditTargetKinds.User, userID, null, new { signals = response.Signals.Count });
			return RestResults.Ok(response);
		}

		[EndpointSummary("Reads one decided case with the signals it covered.")]
		[ProducesResponseType<CaseResponse>(StatusCodes.Status200OK)]
		[ApiPermission(Permissions.ModerationKick, Permissions.ModerationMute, Permissions.ModerationBan, Permissions.AnticheatReview)]
		[HttpGet("~/api/v1/admin/cases/{caseID:long}")]
		public async Task<IActionResult> Decided(long caseID)
		{
			IReadOnlyList<string> allowed = await KindsAsync();
			if (allowed.Count == 0)
			{
				return RestResults.Fail(Result.Forbidden(Permissions.ModerationKick));
			}
			await using AppDbContext db = await m_dbFactory.CreateDbContextAsync(HttpContext.RequestAborted);
			ModerationCase? found = caseID > 0 ? await Database.Cases.Get(db, caseID, HttpContext.RequestAborted) : null;
			if (found == null || found.Status != ModerationCaseStatuses.Closed)
			{
				return RestResults.NotFound("case");
			}
			CaseResponse response = await CaseActions.DetailAsync(db, found.UserID, found, allowed, HttpContext.RequestAborted);
			await Database.Moderation.LogAction(db, SanctionActions.ApiActorOf(Me), ModerationActions.CaseView, AuditTargetKinds.Case, caseID, null, new { user_id = found.UserID });
			return RestResults.Ok(response);
		}

		[EndpointSummary("Claims the waiting case of one player, or takes the claim over.")]
		[ProducesResponseType<CaseSummary>(StatusCodes.Status200OK)]
		[ApiPermission(Permissions.ModerationKick, Permissions.ModerationMute, Permissions.ModerationBan, Permissions.AnticheatReview)]
		[HttpPut("~/api/v1/admin/players/{userID:long}/case/claim")]
		public async Task<IActionResult> Claim(long userID)
		{
			if ((await KindsAsync()).Count == 0)
			{
				return RestResults.Fail(Result.Forbidden(Permissions.ModerationKick));
			}
			await using AppDbContext db = await m_dbFactory.CreateDbContextAsync(HttpContext.RequestAborted);
			if (userID <= 0 || !await Database.Users.Exists(db, userID))
			{
				return RestResults.NotFound("user");
			}
			ModerationCase claimed = await Database.Cases.Claim(db, userID, Me, DateTime.UtcNow, HttpContext.RequestAborted);
			Dictionary<long, (string Name, int Tag)> names = await Database.DisplayNames.GetBulk(db, [Me]);
			return RestResults.Ok(new CaseSummary
			{
				Id = claimed.ID, Status = claimed.Status, OpenedAt = claimed.OpenedAt, ClaimedBy = RealtimeMembers.Of(Me, names), ClaimedAt = claimed.ClaimedAt
			});
		}

		[EndpointSummary("Releases the claim on the waiting case of one player.")]
		[ProducesResponseType(StatusCodes.Status204NoContent)]
		[ApiPermission(Permissions.ModerationKick, Permissions.ModerationMute, Permissions.ModerationBan, Permissions.AnticheatReview)]
		[HttpDelete("~/api/v1/admin/players/{userID:long}/case/claim")]
		public async Task<IActionResult> Release(long userID)
		{
			if ((await KindsAsync()).Count == 0)
			{
				return RestResults.Fail(Result.Forbidden(Permissions.ModerationKick));
			}
			await using AppDbContext db = await m_dbFactory.CreateDbContextAsync(HttpContext.RequestAborted);
			await Database.Cases.Release(db, userID, HttpContext.RequestAborted);
			return NoContent();
		}

		// Decides the listed signals at once: no action, or the sanction applied first (with its own permission and rank
		// checks). Signals another staff member decided meanwhile are skipped; when none still waits the answer is conflict.
		[EndpointSummary("Closes the waiting case of one player with no action or a sanction.")]
		[ProducesResponseType<CloseCaseResponse>(StatusCodes.Status200OK)]
		[ApiRequestBody(typeof(CloseCaseRequest))]
		[ApiPermission(Permissions.ModerationKick, Permissions.ModerationMute, Permissions.ModerationBan, Permissions.AnticheatReview)]
		[HttpPost("~/api/v1/admin/players/{userID:long}/case/close")]
		public async Task<IActionResult> Close(long userID)
		{
			IReadOnlyList<string> allowed = await KindsAsync();
			if (allowed.Count == 0)
			{
				return RestResults.Fail(Result.Forbidden(Permissions.ModerationKick));
			}
			CloseCaseRequest? request;
			try
			{
				request = await JsonSerializer.DeserializeAsync<CloseCaseRequest>(Request.Body, ApiResults.Json, HttpContext.RequestAborted);
			}
			catch (JsonException)
			{
				return RestResults.Invalid("body");
			}
			if (CaseActions.Validate(request, allowed) is Failure invalid)
			{
				return RestResults.Fail(invalid);
			}

			await using (AppDbContext check = await m_dbFactory.CreateDbContextAsync(HttpContext.RequestAborted))
			{
				if (userID <= 0 || !await Database.Users.Exists(check, userID))
				{
					return RestResults.NotFound("user");
				}
			}

			long? sanctionID = null;
			if (request!.Sanction != null)
			{
				CreateSanctionRequest sanction = request.Sanction with { Reason = request.Sanction.Reason ?? request.Note };
				Result<CreateSanctionResponse> applied = await SanctionActions.CreateAsync(m_dbFactory, Me, userID, sanction, async permission => await HasAsync(permission) ? null : Result.Forbidden(permission));
				if (!applied.Ok)
				{
					return RestResults.Fail(new Failure(applied.Code!, applied.Params!));
				}
				sanctionID = applied.Data!.SanctionId;
			}

			await using AppDbContext db = await m_dbFactory.CreateDbContextAsync(HttpContext.RequestAborted);
			Database.ModerationActor actor = SanctionActions.ApiActorOf(Me);
			string? note = String.IsNullOrWhiteSpace(request.Note) ? null : request.Note;
			(Database.CaseCloseResult result, ModerationCase? closed, int decided) = await Database.Cases.Close(db, userID,
				request.Signals!.Select(s => new Database.CaseSignalKey(s.Kind!, s.Id)).ToArray(), Me, actor.Kind, request.Outcome!, note, sanctionID, DateTime.UtcNow, HttpContext.RequestAborted);
			if (result == Database.CaseCloseResult.NothingWaiting)
			{
				return RestResults.Fail(RestCodes.Conflict, new { What = "case" });
			}

			// a sanction that came from a report keeps pointing at it, as the appeal and evidence pages read it
			long? report = request.Signals!.Where(s => s.Kind == CaseSignalKinds.Report).Select(s => (long?)s.Id).FirstOrDefault();
			if (sanctionID != null && report != null)
			{
				await db.Sanctions.Where(s => s.ID == sanctionID && s.ReportID == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.ReportID, report), HttpContext.RequestAborted);
			}
			await Database.Moderation.LogAction(db, actor, ModerationActions.CaseClose, AuditTargetKinds.Case, closed!.ID, note,
				new { user_id = userID, outcome = request.Outcome, decided, sanction_id = sanctionID, signals = request.Signals!.Select(s => $"{s.Kind}:{s.Id}").ToArray() });
			return RestResults.Ok(new CloseCaseResponse { CaseId = closed.ID, Decided = decided, SanctionId = sanctionID });
		}
	}
}
