using Xunit;

namespace GenOnlineService.Tests
{
	public class MatchFileRecoveryTests
	{
		[Fact]
		public void RecoveryChecksIssuedAndFailedFiles()
		{
			MatchPlayerDetails details = new MatchPlayerDetails
			{
				metadata = new List<MemberMetadataModel>
				{
					new MemberMetadataModel { file_key = "issued", file_status = EMetadataFileStatus.FILE_STATUS_ISSUED },
					new MemberMetadataModel { file_key = "failed", file_status = EMetadataFileStatus.FILE_STATUS_FAILED },
					new MemberMetadataModel { file_key = "uploaded", file_status = EMetadataFileStatus.FILE_STATUS_UPLOADED },
					new MemberMetadataModel { file_key = "missing", file_status = EMetadataFileStatus.FILE_STATUS_MISSING },
					new MemberMetadataModel { file_key = null, file_status = EMetadataFileStatus.FILE_STATUS_ISSUED }
				}
			};
			Assert.Equal(new[] { "issued", "failed" }, Database.MatchHistory.FilesToCheck(details).Select(file => file.file_key));
		}
	}
}
