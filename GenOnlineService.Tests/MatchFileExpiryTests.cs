using GenOnlineService.Realtime;
using GenOnlineService.Service;
using Xunit;

namespace GenOnlineService.Tests
{
	public class MatchFileExpiryTests
	{
		private static readonly DateTime Now = new DateTime(2026, 11, 28, 12, 0, 0, DateTimeKind.Utc);

		private static MemberMetadataModel Failed() => new MemberMetadataModel
		{
			file_key = "k",
			file_status = EMetadataFileStatus.FILE_STATUS_FAILED,
			file_failure_reason = "timeout",
			file_failure_http_status = 408
		};

		[Fact]
		public void FailedFileFoundInStorageAfterExpiryIsUploaded()
		{
			MemberMetadataModel file = MatchFileUploads.ApplyExpiry(Failed(), new StoredObjectInfo(10, "image/jpeg", "e"), 100, Now);
			Assert.Equal(EMetadataFileStatus.FILE_STATUS_UPLOADED, file.file_status);
			Assert.Equal(10, file.file_size);
			Assert.Null(file.file_failure_reason);
			Assert.Null(file.file_failure_http_status);
		}

		[Fact]
		public void FailedFileMissingFromStorageKeepsItsFailure()
		{
			MemberMetadataModel before = Failed();
			MemberMetadataModel file = MatchFileUploads.ApplyExpiry(before, null, 100, Now);
			Assert.Equal(before, file);
		}

		[Fact]
		public void IssuedFileMissingFromStorageIsMissing()
		{
			MemberMetadataModel file = MatchFileUploads.ApplyExpiry(new MemberMetadataModel { file_key = "k", file_status = EMetadataFileStatus.FILE_STATUS_ISSUED }, null, 100, Now);
			Assert.Equal(EMetadataFileStatus.FILE_STATUS_MISSING, file.file_status);
		}
	}
}
