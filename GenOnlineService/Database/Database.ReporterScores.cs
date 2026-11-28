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

namespace Database
{
	public static class ReporterBands
	{
		public const string New = "new";
		public const string Reliable = "reliable";
		public const string Mixed = "mixed";
		public const string OftenDismissed = "often_dismissed";
	}

	// Context for reviewers only: it never reaches a player and never changes what is shown or done
	public readonly record struct ReporterScore(int Upheld, int Decided)
	{
		public const int MinimumDecided = 5;

		public double Score => (Upheld + 2.0) / (Decided + 4);

		// 75 percent and 30 percent compared in whole numbers so the boundaries are exact
		public string Band => Decided < MinimumDecided ? ReporterBands.New
			: 4 * (Upheld + 2) >= 3 * (Decided + 4) ? ReporterBands.Reliable
			: 10 * (Upheld + 2) < 3 * (Decided + 4) ? ReporterBands.OftenDismissed
			: ReporterBands.Mixed;
	}

	public static class ReporterScores
	{
		// A decision is upheld while its case still ends in a sanction, and a report decided without a case counts by its own status.
		public static async Task<Dictionary<long, ReporterScore>> For(AppDbContext db, IReadOnlyCollection<long> reporterIDs, CancellationToken cancellationToken = default)
		{
			long[] ids = reporterIDs.Distinct().ToArray();
			if (ids.Length == 0)
			{
				return [];
			}

			var reports = await db.PlayerReports.AsNoTracking().Where(r => ids.Contains(r.ReporterID) && r.Status != PlayerReportStatuses.Open)
				.Select(r => new { Reporter = r.ReporterID, Reported = r.ReportedUserID, Match = r.MatchID ?? 0, r.CaseID, Actioned = r.Status == PlayerReportStatuses.Actioned })
				.ToListAsync(cancellationToken);
			var witnesses = await (from w in db.AnticheatEacWitnesses.AsNoTracking()
								   join f in db.AnticheatEacFindings.AsNoTracking() on w.FindingId equals f.ReportId
								   where ids.Contains(w.ReporterId) && f.CaseID != null && f.UserId != w.ReporterId
								   select new { Reporter = w.ReporterId, Reported = f.UserId, Match = f.MatchId, f.CaseID }).ToListAsync(cancellationToken);

			long[] caseIDs = reports.Select(r => r.CaseID).Concat(witnesses.Select(w => w.CaseID)).OfType<long>().Distinct().ToArray();
			Dictionary<long, ModerationCase> cases = (await db.ModerationCases.AsNoTracking().Where(c => caseIDs.Contains(c.ID)).ToListAsync(cancellationToken)).ToDictionary(c => c.ID);
			long[] sanctionIDs = cases.Values.Select(c => c.SanctionID).OfType<long>().ToArray();
			HashSet<long> won = (await db.Appeals.AsNoTracking().Where(a => sanctionIDs.Contains(a.SanctionID) && a.Status == AppealStatuses.Lifted)
				.Select(a => a.SanctionID).ToListAsync(cancellationToken)).ToHashSet();

			bool Upheld(long caseID) => cases.TryGetValue(caseID, out ModerationCase? c)
				&& c.Outcome == ModerationCaseOutcomes.Sanctioned && c.SanctionID is long sanction && !won.Contains(sanction);

			// one decision per reporter, player, match and case, and a report without a match never merges
			Dictionary<(long, long, long, long), (long Reporter, bool Upheld)> decisions = [];
			long unmatched = 0;
			void Add(long reporter, long reported, long match, long? caseID, bool upheld)
			{
				var key = (reporter, reported, match > 0 ? match : --unmatched, caseID ?? 0);
				decisions[key] = (reporter, upheld || (decisions.TryGetValue(key, out var seen) && seen.Upheld));
			}
			foreach (var r in reports)
			{
				Add(r.Reporter, r.Reported, r.Match, r.CaseID, r.CaseID is long caseID && cases.ContainsKey(caseID) ? Upheld(caseID) : r.Actioned);
			}
			foreach (var w in witnesses)
			{
				Add(w.Reporter, w.Reported, w.Match, w.CaseID, Upheld(w.CaseID!.Value));
			}

			return decisions.Values.GroupBy(d => d.Reporter).ToDictionary(g => g.Key, g => new ReporterScore(g.Count(d => d.Upheld), g.Count()));
		}
	}
}
