using GenOnlineService.Api;
using GenOnlineService.Realtime;
using System.Text.Json;
using Xunit;

namespace GenOnlineService.Tests
{
	public class MatchResultAnswerTests
	{
		private static MatchResultRequest Read(string json) => JsonSerializer.Deserialize<MatchResultRequest>(json, ApiResults.Json)!;

		[Fact]
		public void MissingStatsFileStillGetsAStatsURL()
		{
			Assert.True(MatchActions.WantsStatsURL(true, Read("{}")));
		}

		[Fact]
		public void StatsFileTrueGetsAStatsURL()
		{
			Assert.True(MatchActions.WantsStatsURL(true, Read("{\"stats_file\":true}")));
		}

		[Fact]
		public void StatsFileFalseGetsNoStatsURL()
		{
			Assert.False(MatchActions.WantsStatsURL(true, Read("{\"stats_file\":false}")));
		}

		[Fact]
		public void DisabledStatsUploadsGetNoStatsURL()
		{
			Assert.False(MatchActions.WantsStatsURL(false, Read("{\"stats_file\":true}")));
		}

		private static readonly DateTime Now = new DateTime(2026, 11, 28, 12, 0, 0, DateTimeKind.Utc);

		[Fact]
		public void KeptAnswerIsReusedWhileItsURLsLastLonger()
		{
			Assert.True(MatchActions.KeptAnswerUsable(new MatchResultResponse { ExpiresAt = Now.AddMinutes(10) }, Now - TimeSpan.FromMinutes(1), Now));
		}

		[Fact]
		public void KeptAnswerCloseToExpiryGivesFreshURLs()
		{
			Assert.False(MatchActions.KeptAnswerUsable(new MatchResultResponse { ExpiresAt = Now.AddMinutes(2) }, Now - TimeSpan.FromMinutes(1), Now));
			Assert.False(MatchActions.KeptAnswerUsable(new MatchResultResponse { ExpiresAt = Now.AddSeconds(90) }, Now - TimeSpan.FromMinutes(1), Now));
		}

		private static async Task<(MatchResultResponse Answer, List<EScreenshotType> Asked)> Answer(bool wantsLoadScreen)
		{
			var asked = new List<EScreenshotType>();
			MatchResultResponse answer = await MatchActions.BuildAnswerAsync((fileType, screenshotType) =>
			{
				asked.Add(screenshotType);
				return Task.FromResult<GenOnlineService.Service.UploadTarget?>(new GenOnlineService.Service.UploadTarget(fileType + "/" + screenshotType, new Dictionary<string, string> { ["key"] = screenshotType.ToString() }));
			}, false, false, Now, wantsLoadScreen);
			return (answer, asked);
		}

		[Fact]
		public async Task MissingLoadScreenGetsALoadScreenURL()
		{
			var (answer, asked) = await Answer(true);
			Assert.Equal("FILE_TYPE_SCREENSHOT/SCREENSHOT_TYPE_LOADSCREEN", answer.LoadscreenUrl);
			Assert.Equal("SCREENSHOT_TYPE_LOADSCREEN", answer.LoadscreenFields!["key"]);
			Assert.Equal("FILE_TYPE_SCREENSHOT/SCREENSHOT_TYPE_SCORESCREEN", answer.ScreenshotUrl);
			Assert.Equal(Now, answer.ExpiresAt);
			Assert.Contains(EScreenshotType.SCREENSHOT_TYPE_LOADSCREEN, asked);
		}

		[Fact]
		public async Task UploadedLoadScreenGetsNoLoadScreenURL()
		{
			var (answer, asked) = await Answer(false);
			Assert.Null(answer.LoadscreenUrl);
			Assert.Null(answer.LoadscreenFields);
			Assert.DoesNotContain(EScreenshotType.SCREENSHOT_TYPE_LOADSCREEN, asked);
			Assert.DoesNotContain("loadscreen", JsonSerializer.Serialize(answer, ApiResults.Json));
		}

		[Fact]
		public void AnswerNamesTheLoadScreenFieldsInSnakeCase()
		{
			string json = JsonSerializer.Serialize(new MatchResultResponse { LoadscreenUrl = "u", LoadscreenFields = new Dictionary<string, string> { ["key"] = "k" } }, ApiResults.Json);
			Assert.Contains("\"loadscreen_url\":\"u\"", json);
			Assert.Contains("\"loadscreen_fields\":{\"key\":\"k\"}", json);
		}

		[Fact]
		public void OnlyAStoredLoadScreenCountsAsUploaded()
		{
			MemberMetadataModel File(string name, EMetadataFileStatus status) => new MemberMetadataModel { file_name = name, file_type = EMetadataFileType.FILE_TYPE_SCREENSHOT, file_status = status };
			MatchPlayerDetails Of(params MemberMetadataModel[] files) => new MatchPlayerDetails { metadata = files.ToList() };

			Assert.False(Database.MatchHistory.LoadScreenUploaded(new MatchPlayerDetails()));
			Assert.False(Database.MatchHistory.LoadScreenUploaded(Of(File("match_1_player_2_loadscreen_9_30.jpg", EMetadataFileStatus.FILE_STATUS_ISSUED))));
			Assert.False(Database.MatchHistory.LoadScreenUploaded(Of(File("match_1_player_2_loadscreen_9_30.jpg", EMetadataFileStatus.FILE_STATUS_MISSING))));
			Assert.False(Database.MatchHistory.LoadScreenUploaded(Of(File("match_1_player_2_scorescreen_9_30.jpg", EMetadataFileStatus.FILE_STATUS_UPLOADED))));
			Assert.False(Database.MatchHistory.LoadScreenUploaded(Of(new MemberMetadataModel { file_status = EMetadataFileStatus.FILE_STATUS_UPLOADED })));
			Assert.True(Database.MatchHistory.LoadScreenUploaded(Of(
				File("match_1_player_2_loadscreen_9_30.jpg", EMetadataFileStatus.FILE_STATUS_MISSING),
				File("match_1_player_2_loadscreen_9_41.jpg", EMetadataFileStatus.FILE_STATUS_UPLOADED))));
		}

		[Fact]
		public void KeptAnswerPastItsLifetimeGivesFreshURLs()
		{
			Assert.False(MatchActions.KeptAnswerUsable(new MatchResultResponse { ExpiresAt = Now.AddHours(5) }, Now - TimeSpan.FromHours(1), Now));
		}
	}
}
