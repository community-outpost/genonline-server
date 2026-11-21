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
using GenOnlineService.Rest;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Text;

namespace GenOnlineService.Service
{
	[GenOnlineService.Api.ApiFixture("post-matches-files-me-report.request")]
	public sealed record MatchFileReportRequest
	{
		public string Key { get; init; } = "";

		public string Result { get; init; } = "";

		public string? Reason { get; init; }

		public int? HttpStatus { get; init; }

		public string? Detail { get; init; }
	}

	[GenOnlineService.Api.ApiFixture("post-matches-files-me-report.response")]
	public sealed record MatchFileReportResponse
	{
		public MatchFileStatus Status { get; init; }
	}

	public sealed record StoredObjectInfo(long Size, string ContentType, string ETag);

	public static class MatchFileUploads
	{
		public static readonly string[] FailureReasons = ["size_limit", "expired", "http_error", "network", "timeout", "read_failed", "cancelled"];
		public const int MaxDetailLength = 200;
		public const int MaxKeyLength = 512;

		// The check waits this long after the expiry so a late upload that started in time can finish
		public static readonly TimeSpan CheckGrace = TimeSpan.FromMinutes(1);
		private const int MaxCheckAttempts = 5;

		private static readonly ConcurrentDictionary<(long MatchID, int Slot, string Key), (DateTime Due, int Attempts)> s_pending = new();

		public static int PendingCount => s_pending.Count;

		public static void Track(long matchID, int slot, string key, DateTime expiresAt)
		{
			s_pending[(matchID, slot, key)] = (expiresAt + CheckGrace, 0);
		}

		public static Failure? Validate(MatchFileReportRequest request)
		{
			if (String.IsNullOrWhiteSpace(request.Key) || request.Key.Length > MaxKeyLength) return Result.Invalid("key");
			if (request.Result != "uploaded" && request.Result != "failed") return Result.Invalid("result");
			if (request.Result == "failed" && !FailureReasons.Contains(request.Reason)) return Result.Invalid("reason");
			if (request.HttpStatus is < 100 or > 599) return Result.Invalid("http_status");
			return null;
		}

		// Free text from the game is cut, and control characters are dropped so it cannot break a log line or a page
		internal static string? CleanDetail(string? detail)
		{
			if (String.IsNullOrWhiteSpace(detail))
			{
				return null;
			}

			var clean = new StringBuilder();
			foreach (char c in detail.Trim())
			{
				if (!Char.IsControl(c))
				{
					clean.Append(c);
				}
				if (clean.Length >= MaxDetailLength)
				{
					break;
				}
			}
			return clean.Length == 0 ? null : clean.ToString();
		}

		// An object of a size the signed range allows, anything else is not a valid upload
		private static bool IsValid(StoredObjectInfo? head, long maxBytes) => head != null && head.Size >= 1 && head.Size <= maxBytes;

		private static MemberMetadataModel Uploaded(MemberMetadataModel file, StoredObjectInfo head, DateTime now)
		{
			file.file_status = EMetadataFileStatus.FILE_STATUS_UPLOADED;
			file.file_size = head.Size;
			file.file_etag = head.ETag;
			file.file_verified_at = now;
			file.file_failure_reason = null;
			file.file_failure_http_status = null;
			file.file_failure_detail = null;
			return file;
		}

		// A verified object always wins, a failure is only taken while the file is still waiting, and a claim of success needs the storage to agree
		internal static MemberMetadataModel ApplyReport(MemberMetadataModel file, MatchFileReportRequest request, StoredObjectInfo? head, long maxBytes, DateTime now)
		{
			if (file.file_status == EMetadataFileStatus.FILE_STATUS_UPLOADED)
			{
				return file;
			}

			if (request.Result == "uploaded")
			{
				return IsValid(head, maxBytes) ? Uploaded(file, head!, now) : file;
			}

			if (file.file_status == EMetadataFileStatus.FILE_STATUS_ISSUED || file.file_status == null)
			{
				file.file_status = EMetadataFileStatus.FILE_STATUS_FAILED;
				file.file_failure_reason = request.Reason;
				file.file_failure_http_status = request.HttpStatus;
				file.file_failure_detail = CleanDetail(request.Detail);
			}
			return file;
		}

		// After the URL expired nothing more can arrive, so the storage decides
		internal static MemberMetadataModel ApplyExpiry(MemberMetadataModel file, StoredObjectInfo? head, long maxBytes, DateTime now)
		{
			if (file.file_status != EMetadataFileStatus.FILE_STATUS_ISSUED)
			{
				return file;
			}

			if (IsValid(head, maxBytes))
			{
				return Uploaded(file, head!, now);
			}

			file.file_status = EMetadataFileStatus.FILE_STATUS_MISSING;
			return file;
		}

		internal static MatchFileStatus StatusOf(EMetadataFileStatus? status) => status switch
		{
			EMetadataFileStatus.FILE_STATUS_UPLOADED => MatchFileStatus.Uploaded,
			EMetadataFileStatus.FILE_STATUS_FAILED => MatchFileStatus.Failed,
			EMetadataFileStatus.FILE_STATUS_MISSING => MatchFileStatus.Missing,
			_ => MatchFileStatus.Issued
		};

		// A path style URL carries the bucket in front of the key, and the game reports the path of the URL it used
		internal static string NormalizeKey(string key, string? bucket)
		{
			string trimmed = key.TrimStart('/');
			if (!String.IsNullOrEmpty(bucket) && trimmed.StartsWith(bucket + "/", StringComparison.Ordinal))
			{
				return trimmed.Substring(bucket.Length + 1);
			}
			return trimmed;
		}

		private static long MaxBytes()
		{
			return Program.g_Config?.GetSection("MatchData").GetValue<long?>("s3_max_upload_bytes") ?? 8L * 1024 * 1024;
		}

		// Only a player of the match reports, only for a file issued to them, and a repeated report changes nothing
		public static async Task<Result<MatchFileReportResponse>> ReportAsync(IDbContextFactory<AppDbContext> dbFactory, IMatchDataStore? store, long me, long matchID, MatchFileReportRequest request, DateTime now)
		{
			Failure? invalid = Validate(request);
			if (invalid != null) return invalid.Value;

			var seat = await MatchActions.FindSeatAsync(dbFactory, matchID, me);
			if (seat == null) return Result.NotFound("match");

			await using AppDbContext db = await dbFactory.CreateDbContextAsync();
			string key = NormalizeKey(request.Key, Program.g_Config?.GetSection("MatchData").GetValue<string>("s3_bucket_name"));
			MemberMetadataModel? file = await Database.MatchHistory.GetFile(db, matchID, seat.Value.Slot, key);
			if (file == null) return Result.NotFound("file");

			// Only a success needs the storage, and a storage that cannot be reached leaves the file waiting for the expiry check
			StoredObjectInfo? head = null;
			if (request.Result == "uploaded" && file.Value.file_status != EMetadataFileStatus.FILE_STATUS_UPLOADED && store != null)
			{
				try
				{
					head = await store.HeadAsync(key);
				}
				catch (Exception)
				{
					head = null;
				}
			}

			MemberMetadataModel updated = ApplyReport(file.Value, request, head, MaxBytes(), now);
			if (!updated.Equals(file.Value))
			{
				await Database.MatchHistory.SetFile(db, matchID, seat.Value.Slot, updated);
			}
			return Result.Success(new MatchFileReportResponse { Status = StatusOf(updated.file_status) });
		}

		// The checks that are due, one head request per file; a storage error tries again a minute later and gives up after a few attempts
		public static async Task<int> CheckDueAsync(IDbContextFactory<AppDbContext> dbFactory, IMatchDataStore? store, DateTime now)
		{
			if (store == null)
			{
				return 0;
			}

			int settled = 0;
			foreach (KeyValuePair<(long MatchID, int Slot, string Key), (DateTime Due, int Attempts)> item in s_pending.Where(p => p.Value.Due <= now).ToList())
			{
				try
				{
					await using AppDbContext db = await dbFactory.CreateDbContextAsync();
					MemberMetadataModel? file = await Database.MatchHistory.GetFile(db, item.Key.MatchID, item.Key.Slot, item.Key.Key);
					if (file != null && file.Value.file_status == EMetadataFileStatus.FILE_STATUS_ISSUED)
					{
						StoredObjectInfo? head = await store.HeadAsync(item.Key.Key);
						await Database.MatchHistory.SetFile(db, item.Key.MatchID, item.Key.Slot, ApplyExpiry(file.Value, head, MaxBytes(), now));
						settled++;
					}
					s_pending.TryRemove(item.Key, out _);
				}
				catch (Exception)
				{
					if (item.Value.Attempts + 1 >= MaxCheckAttempts)
					{
						s_pending.TryRemove(item.Key, out _);
					}
					else
					{
						s_pending[item.Key] = (now + TimeSpan.FromMinutes(1), item.Value.Attempts + 1);
					}
				}
			}
			return settled;
		}

		// After a restart the files issued in the last day that are still waiting are checked again
		public static async Task RecoverAsync(IDbContextFactory<AppDbContext> dbFactory, DateTime now)
		{
			await using AppDbContext db = await dbFactory.CreateDbContextAsync();
			foreach ((long matchID, int slot, MemberMetadataModel file) in await Database.MatchHistory.ListIssuedFiles(db, now - TimeSpan.FromDays(1)))
			{
				if (file.file_key != null)
				{
					s_pending.TryAdd((matchID, slot, file.file_key), ((file.file_expires_at ?? now) + CheckGrace, 0));
				}
			}
		}
	}
}
