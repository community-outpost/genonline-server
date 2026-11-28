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
using Microsoft.EntityFrameworkCore;		
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.Primitives;
using MySqlConnector;
using Npgsql;
using System.Data.Common;
using System.Text;
using System.Text.Json;

public class AnticheatModuleReport
{
	public Int64 ReportId { get; set; }
	public Int64 UserId { get; set; }
	public Int64 MatchId { get; set; }
	public string ModuleName { get; set; } = null!;
	public string ModulePath { get; set; } = null!;
	public Int64 ModuleSize { get; set; }
	// null for rows stored before the column existed; the lists then show the match start
	public DateTime? CreatedAt { get; set; }

	public DateTime? ReviewedAt { get; set; }
	public Int64? ReviewedBy { get; set; }

	// null while waiting and for rows reviewed before cases existed
	public Int64? CaseID { get; set; }
}

public readonly record struct AcModuleReport(string ModuleName, string ModulePath, int ModuleSize);

// Directories are compared without the drive letter, lower case with forward slashes, as the hub cleans module paths
internal static class AntiCheatModuleAllowList
{
	// Windows, its side by side store and the driver store, where every GPU, audio and input driver loads from
	public static readonly IReadOnlyList<string> DefaultDirectories =
	[
		"/windows/system32",
		"/windows/syswow64",
		"/windows/winsxs",
		"/windows/microsoft.net",
	];

	// Overlays that hook the game, each only from the folder its vendor installs it to
	public static readonly IReadOnlyList<(string Module, string DirectoryPart)> DefaultOverlays =
	[
		("gameoverlayrenderer.dll", "/steam"),
		("discordhook.dll", "/discord/"),
		("rtsshooks.dll", "/rivatuner statistics server"),
		("graphics-hook32.dll", "/obs-studio/"),
	];

	private sealed class Snapshot
	{
		public string[] Modules = Array.Empty<string>();
		public string[] Paths = Array.Empty<string>();
	}

	private static readonly object s_lock = new();
	private static volatile Snapshot? s_snapshot = null;
	private static IDisposable? s_reloadRegistration = null;

	private static Snapshot? GetSnapshot()
	{
		Snapshot? snapshot = s_snapshot;
		if (snapshot != null)
		{
			return snapshot;
		}

		IConfiguration? config = GenOnlineService.Program.g_Config;
		if (config == null)
		{
			return null;
		}

		lock (s_lock)
		{
			if (s_reloadRegistration == null)
			{
				s_reloadRegistration = ChangeToken.OnChange(() => config.GetReloadToken(), () => Reload());
			}

			return Reload();
		}
	}

	private static Snapshot? Reload()
	{
		IConfiguration? config = GenOnlineService.Program.g_Config;
		if (config == null)
		{
			return null;
		}

		IConfiguration acSettings = config.GetSection("AC");
		Snapshot snapshot = new Snapshot
		{
			Modules = acSettings.GetSection("allowed_modules").Get<List<string>>()?.Select(m => m.ToLowerInvariant()).ToArray() ?? Array.Empty<string>(),
			Paths = acSettings.GetSection("allowed_module_paths").Get<List<string>>()?.Select(DirectoryOf).ToArray() ?? Array.Empty<string>()
		};

		s_snapshot = snapshot;
		return snapshot;
	}

	// c:/windows/system32 and d:/windows/system32 both become /windows/system32
	private static string DirectoryOf(string path)
	{
		string clean = path.Replace('\\', '/').ToLowerInvariant().TrimEnd('/');
		return clean.Length >= 2 && clean[1] == ':' ? clean.Substring(2) : clean;
	}

	private static bool IsIn(string directory, string allowed) =>
		directory == allowed || directory.StartsWith(allowed + "/", StringComparison.Ordinal);

	public static bool IsAllowed(string moduleFilename, string modulePath) => IsAllowed(moduleFilename, modulePath, GetSnapshot());

	private static bool IsAllowed(string moduleFilename, string modulePath, Snapshot? snapshot)
	{
		string module = moduleFilename.ToLowerInvariant();
		string directory = DirectoryOf(modulePath);
		if ((directory + "/").Contains("/../", StringComparison.Ordinal) || (directory + "/").Contains("/./", StringComparison.Ordinal))
		{
			return false;
		}
		if (DefaultDirectories.Any(d => IsIn(directory, d)) || DefaultOverlays.Any(o => o.Module == module && (directory + "/").Contains(o.DirectoryPart, StringComparison.Ordinal)))
		{
			return true;
		}
		if (snapshot == null)
		{
			return false;
		}
		return Array.IndexOf(snapshot.Modules, module) >= 0 || snapshot.Paths.Any(d => IsIn(directory, d));
	}

	internal static bool IsAllowedByDefault(string moduleFilename, string modulePath) => IsAllowed(moduleFilename, modulePath, null);
}

public class AnticheatProbeReport
{
	public Int64 ReportId { get; set; }
	public Int64 UserId { get; set; }
	public Int64 MatchId { get; set; }
	public string Reason { get; set; } = null!;
	// null for rows stored before the column existed; the lists then show the match start
	public DateTime? CreatedAt { get; set; }
	public DateTime? ReviewedAt { get; set; }
	public Int64? ReviewedBy { get; set; }

	// null while waiting and for rows reviewed before cases existed
	public Int64? CaseID { get; set; }
}

public class AnticheatIllegalRequest
{
	public Int64 ReportId { get; set; }
	public Int64 UserId { get; set; }

	// 0 when the lobby had no match yet
	public Int64 MatchId { get; set; }
	public Int64 LobbyId { get; set; }

	// the hub method that was refused
	public string Request { get; set; } = null!;

	// what was attempted, built from fixed words and game numbers, never from free player text
	public string Detail { get; set; } = null!;

	// the lobby state and whether the lobby is a quick match
	public string State { get; set; } = null!;
	public DateTime CreatedAt { get; set; }
	public DateTime? ReviewedAt { get; set; }
	public Int64? ReviewedBy { get; set; }

	// null while waiting and for rows reviewed before cases existed
	public Int64? CaseID { get; set; }
}

// One Easy Anti-Cheat decision about a player in one match or lobby as game clients reported it
public class AnticheatEacFinding
{
	public Int64 ReportId { get; set; }

	// the player the decision names
	public Int64 UserId { get; set; }

	// 0 when the report named no match or no lobby
	public Int64 MatchId { get; set; }
	public Int64 LobbyId { get; set; }

	// action_required or integrity_violation
	public string Kind { get; set; } = null!;

	// true when the player reported the decision about themselves
	public bool SelfReported { get; set; }
	public DateTime CreatedAt { get; set; }
	public DateTime? ReviewedAt { get; set; }
	public Int64? ReviewedBy { get; set; }
	public Int64? CaseID { get; set; }

	// client for a game client report, epic for an active sanction that Epic returned
	public string Source { get; set; } = AnticheatEacSources.Client;

	// the Epic sanction, empty or null for a client report
	public string ReferenceId { get; set; } = "";
	public string? SanctionAction { get; set; }
	public DateTime? PlacedAt { get; set; }

	// null for a permanent sanction
	public DateTime? ExpiresAt { get; set; }
}

public static class AnticheatEacSources
{
	public const string Client = "client";
	public const string Epic = "epic";

	public const string EpicKind = "sanction";
}

// One reporter of a finding with the EAC codes and text that reporter saw
public class AnticheatEacWitness
{
	public Int64 WitnessId { get; set; }
	public Int64 FindingId { get; set; }
	public Int64 ReporterId { get; set; }

	// EOS_EAntiCheatCommonClientAction, 0 for an integrity violation
	public int Action { get; set; }

	// EOS_EAntiCheatCommonClientActionReason, 0 for an integrity violation
	public int ReasonCode { get; set; }

	// EOS_EAntiCheatClientViolationType, 0 for an action
	public int ViolationType { get; set; }

	// the EAC text, never shown to other players
	public string Details { get; set; } = null!;
	public DateTime CreatedAt { get; set; }
}

public static class AntiCheatReviewKinds
{
	public const string Modules = "modules";

	public const string Probes = "probes";

	public const string Devices = "devices";

	public const string IllegalRequests = "illegal_requests";

	public const string Eac = "eac";

	public const string Epic = "epic";

	public static readonly IReadOnlyList<string> All = [Modules, Probes, Devices, IllegalRequests, Eac, Epic];

	public static bool IsKnown(string? kind) => kind != null && All.Contains(kind);
}


public class AnticheatModuleReportConfiguration : IEntityTypeConfiguration<AnticheatModuleReport>
{
	public void Configure(EntityTypeBuilder<AnticheatModuleReport> entity)
	{
		entity.ToTable("anticheat_module_reports");

		entity.HasKey(e => e.ReportId);

		entity.Property(e => e.ReportId)
			.HasColumnName("report_id")
			.ValueGeneratedOnAdd();

		entity.Property(e => e.UserId)
			.HasColumnName("user_id");

		entity.Property(e => e.MatchId)
			.HasColumnName("match_id");

		entity.Property(e => e.ModuleName)
			.HasColumnName("module_name")
			.HasMaxLength(128)
			.IsRequired();

		entity.Property(e => e.ModulePath)
			.HasColumnName("module_path")
			.HasMaxLength(512)
			.IsRequired();

		entity.Property(e => e.ModuleSize)
			.HasColumnName("module_size");

		entity.Property(e => e.CreatedAt).HasColumnName("created_at");
		entity.Property(e => e.ReviewedAt).HasColumnName("reviewed_at");
		entity.Property(e => e.ReviewedBy).HasColumnName("reviewed_by");
		entity.HasIndex(e => e.ReviewedAt).HasDatabaseName("ix_anticheat_module_reports_reviewed_at");
		entity.Property(e => e.CaseID).HasColumnName("case_id");
		entity.HasIndex(e => e.CaseID).HasDatabaseName("ix_anticheat_module_reports_case");

		entity.HasIndex(e => new { e.UserId, e.MatchId, e.ModuleName })
			.IsUnique()
			.HasDatabaseName("ux_anticheat_module_reports_user_match_module");
	}
}

public class AnticheatProbeReportConfiguration : IEntityTypeConfiguration<AnticheatProbeReport>
{
	public void Configure(EntityTypeBuilder<AnticheatProbeReport> entity)
	{
		entity.ToTable("anticheat_probe_reports");

		entity.HasKey(e => e.ReportId);

		entity.Property(e => e.ReportId)
			.HasColumnName("report_id")
			.ValueGeneratedOnAdd();

		entity.Property(e => e.UserId)
			.HasColumnName("user_id");

		entity.Property(e => e.MatchId)
			.HasColumnName("match_id");

		entity.Property(e => e.Reason)
			.HasColumnName("reason")
			.HasMaxLength(256)
			.IsRequired();

		entity.Property(e => e.CreatedAt).HasColumnName("created_at");
		entity.Property(e => e.ReviewedAt).HasColumnName("reviewed_at");
		entity.Property(e => e.ReviewedBy).HasColumnName("reviewed_by");
		entity.HasIndex(e => e.ReviewedAt).HasDatabaseName("ix_anticheat_probe_reports_reviewed_at");
		entity.Property(e => e.CaseID).HasColumnName("case_id");
		entity.HasIndex(e => e.CaseID).HasDatabaseName("ix_anticheat_probe_reports_case");
	}
}

public class AnticheatIllegalRequestConfiguration : IEntityTypeConfiguration<AnticheatIllegalRequest>
{
	public const int RequestMaxLength = 32;
	public const int DetailMaxLength = 200;
	public const int StateMaxLength = 64;

	public void Configure(EntityTypeBuilder<AnticheatIllegalRequest> entity)
	{
		entity.ToTable("anticheat_illegal_requests");

		entity.HasKey(e => e.ReportId);

		entity.Property(e => e.ReportId)
			.HasColumnName("report_id")
			.ValueGeneratedOnAdd();

		entity.Property(e => e.UserId).HasColumnName("user_id");
		entity.Property(e => e.MatchId).HasColumnName("match_id");
		entity.Property(e => e.LobbyId).HasColumnName("lobby_id");

		entity.Property(e => e.Request)
			.HasColumnName("request")
			.HasMaxLength(RequestMaxLength)
			.IsRequired();

		entity.Property(e => e.Detail)
			.HasColumnName("detail")
			.HasMaxLength(DetailMaxLength)
			.IsRequired();

		entity.Property(e => e.State)
			.HasColumnName("state")
			.HasMaxLength(StateMaxLength)
			.IsRequired();

		entity.Property(e => e.CreatedAt).HasColumnName("created_at");
		entity.Property(e => e.ReviewedAt).HasColumnName("reviewed_at");
		entity.Property(e => e.ReviewedBy).HasColumnName("reviewed_by");
		entity.HasIndex(e => e.ReviewedAt).HasDatabaseName("ix_anticheat_illegal_requests_reviewed_at");
		entity.Property(e => e.CaseID).HasColumnName("case_id");
		entity.HasIndex(e => e.CaseID).HasDatabaseName("ix_anticheat_illegal_requests_case");
		entity.HasIndex(e => new { e.UserId, e.CreatedAt }).HasDatabaseName("ix_anticheat_illegal_requests_user_created");

		// one row per player, lobby and request, so a client that repeats the request does not flood the list
		entity.HasIndex(e => new { e.UserId, e.LobbyId, e.Request })
			.IsUnique()
			.HasDatabaseName("ux_anticheat_illegal_requests_user_lobby_request");
	}
}

public class AnticheatEacFindingConfiguration : IEntityTypeConfiguration<AnticheatEacFinding>
{
	public const int KindMaxLength = 32;
	public const int SourceMaxLength = 16;
	public const int ReferenceIdMaxLength = 64;
	public const int SanctionActionMaxLength = 64;

	public void Configure(EntityTypeBuilder<AnticheatEacFinding> entity)
	{
		entity.ToTable("anticheat_eac_findings");
		entity.HasKey(e => e.ReportId);
		entity.Property(e => e.ReportId).HasColumnName("report_id").ValueGeneratedOnAdd();
		entity.Property(e => e.UserId).HasColumnName("user_id");
		entity.Property(e => e.MatchId).HasColumnName("match_id");
		entity.Property(e => e.LobbyId).HasColumnName("lobby_id");
		entity.Property(e => e.Kind).HasColumnName("kind").HasMaxLength(KindMaxLength).IsRequired();
		entity.Property(e => e.SelfReported).HasColumnName("self_reported");
		entity.Property(e => e.CreatedAt).HasColumnName("created_at");
		entity.Property(e => e.ReviewedAt).HasColumnName("reviewed_at");
		entity.Property(e => e.ReviewedBy).HasColumnName("reviewed_by");
		entity.Property(e => e.CaseID).HasColumnName("case_id");
		entity.Property(e => e.Source).HasColumnName("source").HasMaxLength(SourceMaxLength).IsRequired().HasDefaultValue(AnticheatEacSources.Client);
		entity.Property(e => e.ReferenceId).HasColumnName("reference_id").HasMaxLength(ReferenceIdMaxLength).IsRequired().HasDefaultValue("");
		entity.Property(e => e.SanctionAction).HasColumnName("sanction_action").HasMaxLength(SanctionActionMaxLength);
		entity.Property(e => e.PlacedAt).HasColumnName("placed_at");
		entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
		entity.HasIndex(e => e.ReviewedAt).HasDatabaseName("ix_anticheat_eac_findings_reviewed_at");
		entity.HasIndex(e => e.CaseID).HasDatabaseName("ix_anticheat_eac_findings_case");

		// reports about one player in one match from every witness meet in one finding, and an Epic sanction is one finding by its reference id
		entity.HasIndex(e => new { e.UserId, e.MatchId, e.LobbyId, e.Kind, e.SelfReported, e.ReferenceId })
			.IsUnique()
			.HasDatabaseName("ux_anticheat_eac_findings_target");
	}
}

public class AnticheatEacWitnessConfiguration : IEntityTypeConfiguration<AnticheatEacWitness>
{
	public const int DetailsMaxBytes = 512;

	public void Configure(EntityTypeBuilder<AnticheatEacWitness> entity)
	{
		entity.ToTable("anticheat_eac_witnesses");
		entity.HasKey(e => e.WitnessId);
		entity.Property(e => e.WitnessId).HasColumnName("witness_id").ValueGeneratedOnAdd();
		entity.Property(e => e.FindingId).HasColumnName("finding_id");
		entity.Property(e => e.ReporterId).HasColumnName("reporter_id");
		entity.Property(e => e.Action).HasColumnName("action");
		entity.Property(e => e.ReasonCode).HasColumnName("reason_code");
		entity.Property(e => e.ViolationType).HasColumnName("violation_type");
		entity.Property(e => e.Details).HasColumnName("details").HasMaxLength(DetailsMaxBytes).IsRequired();
		entity.Property(e => e.CreatedAt).HasColumnName("created_at");

		// shortcut: a repeat with the same codes and another text keeps the first text, key on a text hash if staff need every text
		entity.HasIndex(e => new { e.FindingId, e.ReporterId, e.Action, e.ReasonCode, e.ViolationType })
			.IsUnique()
			.HasDatabaseName("ux_anticheat_eac_witnesses_reporter_codes");
	}
}

	

namespace Database
{
	public sealed class AntiCheatReviewRow
	{
		public Int64 ReportID { get; init; }
		public Int64 UserID { get; init; }
		public Int64 MatchID { get; init; }
		public string? ModuleName { get; init; }
		public string? ModulePath { get; init; }
		public Int64? ModuleSize { get; init; }
		public string? Reason { get; init; }

		public Int64? LobbyID { get; init; }
		public string? Request { get; init; }
		public string? Detail { get; init; }
		public string? State { get; init; }

		public string? EacKind { get; init; }
		public bool? SelfReported { get; init; }

		public string? EpicReferenceId { get; init; }
		public string? EpicAction { get; init; }
		public DateTime? EpicPlacedAt { get; init; }
		public DateTime? EpicExpiresAt { get; init; }

		// null for a row stored before the other kinds kept the time
		public DateTime? ReportedAt { get; init; }

		public DateTime? ReviewedAt { get; init; }
		public Int64? ReviewedBy { get; init; }
		public Int64? CaseID { get; init; }
	}

	public sealed record EacEventReport(Int64 ReporterId, Int64 TargetUserId, Int64 MatchId, Int64 LobbyId, string Kind, int Action, int ReasonCode, int ViolationType, string Details, DateTime At);

	public static class AntiCheatReviews
	{
		internal static IQueryable<AntiCheatReviewRow> Rows(AppDbContext db, string kind)
		{
			return kind switch
			{
				AntiCheatReviewKinds.Modules => db.AnticheatModuleReports.AsNoTracking().Select(r => new AntiCheatReviewRow
				{
					ReportID = r.ReportId, UserID = r.UserId, MatchID = r.MatchId, ModuleName = r.ModuleName, ModulePath = r.ModulePath, ModuleSize = r.ModuleSize, ReportedAt = r.CreatedAt, ReviewedAt = r.ReviewedAt, ReviewedBy = r.ReviewedBy, CaseID = r.CaseID
				}),
				AntiCheatReviewKinds.IllegalRequests => db.AnticheatIllegalRequests.AsNoTracking().Select(r => new AntiCheatReviewRow
				{
					ReportID = r.ReportId, UserID = r.UserId, MatchID = r.MatchId, LobbyID = r.LobbyId, Request = r.Request, Detail = r.Detail, State = r.State,
					ReportedAt = r.CreatedAt, ReviewedAt = r.ReviewedAt, ReviewedBy = r.ReviewedBy, CaseID = r.CaseID
				}),
				AntiCheatReviewKinds.Eac => db.AnticheatEacFindings.AsNoTracking().Where(r => r.Source == AnticheatEacSources.Client).Select(r => new AntiCheatReviewRow
				{
					ReportID = r.ReportId, UserID = r.UserId, MatchID = r.MatchId, LobbyID = r.LobbyId > 0 ? r.LobbyId : null, EacKind = r.Kind, SelfReported = r.SelfReported,
					ReportedAt = r.CreatedAt, ReviewedAt = r.ReviewedAt, ReviewedBy = r.ReviewedBy, CaseID = r.CaseID
				}),
				AntiCheatReviewKinds.Epic => db.AnticheatEacFindings.AsNoTracking().Where(r => r.Source == AnticheatEacSources.Epic).Select(r => new AntiCheatReviewRow
				{
					ReportID = r.ReportId, UserID = r.UserId, MatchID = 0, EpicReferenceId = r.ReferenceId, EpicAction = r.SanctionAction, EpicPlacedAt = r.PlacedAt, EpicExpiresAt = r.ExpiresAt,
					ReportedAt = r.CreatedAt, ReviewedAt = r.ReviewedAt, ReviewedBy = r.ReviewedBy, CaseID = r.CaseID
				}),
				_ => Probes(db, kind).AsNoTracking().Select(r => new AntiCheatReviewRow
				{
					ReportID = r.ReportId, UserID = r.UserId, MatchID = r.MatchId, Reason = r.Reason, ReportedAt = r.CreatedAt, ReviewedAt = r.ReviewedAt, ReviewedBy = r.ReviewedBy, CaseID = r.CaseID
				})
			};
		}

		// a sign-in device report has match 0
		private static IQueryable<AnticheatProbeReport> Probes(AppDbContext db, string kind)
		{
			return kind == AntiCheatReviewKinds.Devices
				? db.AnticheatProbeReports.Where(r => r.MatchId == 0)
				: db.AnticheatProbeReports.Where(r => r.MatchId != 0);
		}

		public static async Task<int> Decide(AppDbContext db, string kind, long[] ids, long userID, long reviewerID, long caseID, DateTime now, CancellationToken cancellationToken = default)
		{
			return kind switch
			{
				AntiCheatReviewKinds.Modules => await db.AnticheatModuleReports
					.Where(r => ids.Contains(r.ReportId) && r.UserId == userID && r.ReviewedAt == null)
					.ExecuteUpdateAsync(s => s.SetProperty(r => r.ReviewedAt, now).SetProperty(r => r.ReviewedBy, reviewerID).SetProperty(r => r.CaseID, caseID), cancellationToken),
				AntiCheatReviewKinds.IllegalRequests => await db.AnticheatIllegalRequests
					.Where(r => ids.Contains(r.ReportId) && r.UserId == userID && r.ReviewedAt == null)
					.ExecuteUpdateAsync(s => s.SetProperty(r => r.ReviewedAt, now).SetProperty(r => r.ReviewedBy, reviewerID).SetProperty(r => r.CaseID, caseID), cancellationToken),
				AntiCheatReviewKinds.Eac or AntiCheatReviewKinds.Epic => await db.AnticheatEacFindings
					.Where(r => ids.Contains(r.ReportId) && r.UserId == userID && r.ReviewedAt == null && r.Source == (kind == AntiCheatReviewKinds.Epic ? AnticheatEacSources.Epic : AnticheatEacSources.Client))
					.ExecuteUpdateAsync(s => s.SetProperty(r => r.ReviewedAt, now).SetProperty(r => r.ReviewedBy, reviewerID).SetProperty(r => r.CaseID, caseID), cancellationToken),
				AntiCheatReviewKinds.Probes or AntiCheatReviewKinds.Devices => await Probes(db, kind)
					.Where(r => ids.Contains(r.ReportId) && r.UserId == userID && r.ReviewedAt == null)
					.ExecuteUpdateAsync(s => s.SetProperty(r => r.ReviewedAt, now).SetProperty(r => r.ReviewedBy, reviewerID).SetProperty(r => r.CaseID, caseID), cancellationToken),
				_ => 0
			};
		}
	}

	public static class AntiCheat
	{
		private static readonly ILogger s_log = AppLog.For(typeof(AntiCheat));

		public const int ModuleNameMaxLength = 128;
		public const int ModulePathMaxLength = 512;

		private static void RaiseReport(Int64 userId, Int64 matchId, string kind)
		{
			GenOnlineService.Rules.RuleRunner.Raise(GenOnlineService.Rules.RuleEvent.Of(GenOnlineService.Rules.RuleTriggers.AnticheatReport, userId, null,
				(GenOnlineService.Rules.RuleEventFields.MatchId, matchId), (GenOnlineService.Rules.RuleEventFields.AnticheatKind, kind)));
		}

		public static string CleanModuleText(string? value, int maxLength)
		{
			return TextSanitizer.Clean(value, maxLength);
		}

		private static DbParameter CreateParameter(bool postgres, string name, object value)
		{
			return postgres ? new NpgsqlParameter(name, value) : new MySqlParameter(name, value);
		}

		// Both providers store microseconds, so a stored time reads back as it was written
		private static DateTime TruncateToMicroseconds(DateTime value)
		{
			return new DateTime(value.Ticks - (value.Ticks % 10), DateTimeKind.Utc);
		}

		// false on a real database error; true when stored or already there
		public static async Task<bool> FlagAccountForReview_Modules(
	AppDbContext db,
	Int64 userId,
	ulong matchId,
	IReadOnlyList<AcModuleReport> modules)
		{
			if (modules.Count == 0)
			{
				return true;
			}

			try
			{
				const int ChunkSize = 200;
				DateTime now = TruncateToMicroseconds(DateTime.UtcNow);

				for (int offset = 0; offset < modules.Count; offset += ChunkSize)
				{
					int count = Math.Min(ChunkSize, modules.Count - offset);
					bool postgres = db.Database.IsNpgsql();

					// Only parameter placeholders are concatenated here, all values are bound as parameters
					StringBuilder sql = new StringBuilder("INSERT INTO anticheat_module_reports (user_id, match_id, module_name, module_path, module_size, created_at) VALUES ");
					List<object> parameters = new List<object>(3 + (count * 3));
					parameters.Add(CreateParameter(postgres, "@uid", userId));
					parameters.Add(CreateParameter(postgres, "@mid", (long)matchId));
					parameters.Add(CreateParameter(postgres, "@at", now));

					for (int i = 0; i < count; ++i)
					{
						AcModuleReport module = modules[offset + i];

						if (i > 0)
						{
							sql.Append(',');
						}

						sql.Append("(@uid, @mid, @n").Append(i).Append(", @p").Append(i).Append(", @s").Append(i).Append(", @at)");
						parameters.Add(CreateParameter(postgres, "@n" + i, module.ModuleName));
						parameters.Add(CreateParameter(postgres, "@p" + i, module.ModulePath));
						parameters.Add(CreateParameter(postgres, "@s" + i, (long)module.ModuleSize));
					}

					// repeats on the unique key (user_id, match_id, module_name) are ignored (a no-op update on MySQL); other errors surface
					sql.Append(postgres
						? " ON CONFLICT (user_id, match_id, module_name) DO NOTHING"
						: " ON DUPLICATE KEY UPDATE report_id = report_id");

					await db.Database.ExecuteSqlRawAsync(sql.ToString(), parameters);
				}

				AppMetrics.RecordSecurityAction("anticheat_module_review", "success");
				RaiseReport(userId, (long)matchId, AntiCheatReviewKinds.Modules);
				return true;
			}
			catch (Exception ex)
			{
				if (DbHelpers.IsUniqueViolation(ex))
				{
					return true;
				}

				AppMetrics.RecordSecurityAction("anticheat_module_review", "error");
				s_log.LogError(ex, "FlagAccountForReview_Modules failed");
				return false;
			}
		}



		public static async Task FlagAccountForReview_SuspectProbes(
	AppDbContext db,
	Int64 userId,
	ulong matchId,
	string reason)
		{
			try
			{
				db.AnticheatProbeReports.Add(new AnticheatProbeReport
				{
					UserId = userId,
					MatchId = (long)matchId,
					Reason = reason,
					CreatedAt = TruncateToMicroseconds(DateTime.UtcNow)
				});

				await db.SaveChangesAsync();
				AppMetrics.RecordSecurityAction("anticheat_probe_review", "success");
				RaiseReport(userId, (long)matchId, matchId == 0 ? AntiCheatReviewKinds.Devices : AntiCheatReviewKinds.Probes);
			}
			catch (Exception ex)
			{
				AppMetrics.RecordSecurityAction("anticheat_probe_review", "error");
				s_log.LogError(ex, "FlagAccountForReview_SuspectProbes failed");
			}
		}


		// false on a real database error; true when stored or already there
		public static async Task<bool> FlagAccountForReview_IllegalRequest(AppDbContext db, AnticheatIllegalRequest report)
		{
			try
			{
				db.AnticheatIllegalRequests.Add(report);
				await db.SaveChangesAsync();
				AppMetrics.RecordSecurityAction("anticheat_illegal_request_review", "success");
				RaiseReport(report.UserId, report.MatchId, AntiCheatReviewKinds.IllegalRequests);
				return true;
			}
			catch (Exception ex)
			{
				if (DbHelpers.IsUniqueViolation(ex))
				{
					return true;
				}

				AppMetrics.RecordSecurityAction("anticheat_illegal_request_review", "error");
				s_log.LogError(ex, "FlagAccountForReview_IllegalRequest failed");
				return false;
			}
		}

		// Epic does not sign these client reports, so a finding about another player is corroborated by its distinct witnesses and a self reported one is a signal only
		public static async Task<bool> RecordEacEvent(AppDbContext db, EacEventReport report)
		{
			bool self = report.TargetUserId == report.ReporterId;
			Task<AnticheatEacFinding?> Find() => db.AnticheatEacFindings.AsNoTracking().FirstOrDefaultAsync(f => f.UserId == report.TargetUserId && f.MatchId == report.MatchId
				&& f.LobbyId == report.LobbyId && f.Kind == report.Kind && f.SelfReported == self);
			try
			{
				DateTime now = TruncateToMicroseconds(report.At);
				AnticheatEacFinding? finding = await Find();
				bool created = false;
				if (finding == null)
				{
					finding = new AnticheatEacFinding { UserId = report.TargetUserId, MatchId = report.MatchId, LobbyId = report.LobbyId, Kind = report.Kind, SelfReported = self, CreatedAt = now };
					db.AnticheatEacFindings.Add(finding);
					try
					{
						await db.SaveChangesAsync();
						created = true;
					}
					catch (Exception ex) when (DbHelpers.IsUniqueViolation(ex))
					{
						// another witness stored the finding first
						db.ChangeTracker.Clear();
						finding = await Find() ?? throw new InvalidOperationException("EAC finding vanished", ex);
					}
				}

				db.AnticheatEacWitnesses.Add(new AnticheatEacWitness
				{
					FindingId = finding.ReportId, ReporterId = report.ReporterId, Action = report.Action, ReasonCode = report.ReasonCode, ViolationType = report.ViolationType,
					Details = report.Details, CreatedAt = now
				});
				try
				{
					await db.SaveChangesAsync();
				}
				catch (Exception ex) when (DbHelpers.IsUniqueViolation(ex))
				{
				}

				AppMetrics.RecordSecurityAction("anticheat_eac_review", "success");
				if (created)
				{
					RaiseReport(report.TargetUserId, report.MatchId, AntiCheatReviewKinds.Eac);
				}
				return true;
			}
			catch (Exception ex)
			{
				AppMetrics.RecordSecurityAction("anticheat_eac_review", "error");
				s_log.LogError(ex, "RecordEacEvent failed");
				return false;
			}
		}

		// a sanction Epic no longer returns stays as history, and nothing acts on the player without staff
		public static async Task<bool> RecordEpicSanctions(AppDbContext db, Int64 userId, IReadOnlyList<EpicSanction> sanctions, DateTime at)
		{
			try
			{
				DateTime now = TruncateToMicroseconds(at);
				bool created = false;
				foreach (EpicSanction sanction in sanctions)
				{
					if (await db.AnticheatEacFindings.AsNoTracking().AnyAsync(f => f.UserId == userId && f.Source == AnticheatEacSources.Epic && f.ReferenceId == sanction.ReferenceId))
					{
						continue;
					}
					db.AnticheatEacFindings.Add(new AnticheatEacFinding
					{
						UserId = userId, Kind = AnticheatEacSources.EpicKind, Source = AnticheatEacSources.Epic, ReferenceId = sanction.ReferenceId, SanctionAction = sanction.Action,
						PlacedAt = TruncateToMicroseconds(sanction.PlacedAt), ExpiresAt = sanction.ExpiresAt is DateTime expires ? TruncateToMicroseconds(expires) : null, CreatedAt = now
					});
					try
					{
						await db.SaveChangesAsync();
						created = true;
					}
					catch (Exception ex) when (DbHelpers.IsUniqueViolation(ex))
					{
						// another check stored the sanction first
						db.ChangeTracker.Clear();
					}
				}

				AppMetrics.RecordSecurityAction("anticheat_epic_review", "success");
				if (created)
				{
					RaiseReport(userId, 0, AntiCheatReviewKinds.Epic);
				}
				return true;
			}
			catch (Exception ex)
			{
				AppMetrics.RecordSecurityAction("anticheat_epic_review", "error");
				s_log.LogError(ex, "RecordEpicSanctions failed");
				return false;
			}
		}




	}
}