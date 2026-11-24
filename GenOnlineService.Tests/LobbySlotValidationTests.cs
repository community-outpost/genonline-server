using GenOnlineService.Realtime;
using System.Text.Json;
using Xunit;

namespace GenOnlineService.Tests
{
	public class LobbySlotValidationTests
	{
		private static string? Refusal(int? side = null, int? color = null, Optional<int?> team = default, int? startPosition = null, string prefix = "") =>
			JsonSerializer.Serialize(RealtimeHub.SlotFieldFailure(side, color, team, startPosition, prefix)?.Params);

		[Theory]
		[InlineData(-1)]
		[InlineData(0)]
		[InlineData(7)]
		public void ColorInTheGameRangeIsAccepted(int color) => Assert.Null(RealtimeHub.SlotFieldFailure(null, color, default, null, ""));

		[Theory]
		[InlineData(-2)]
		[InlineData(8)]
		[InlineData(int.MaxValue)]
		public void ColorOutsideTheGameRangeIsRefused(int color)
		{
			Failure? failure = RealtimeHub.SlotFieldFailure(null, color, default, null, "");
			Assert.Equal(ErrorCodes.InvalidArgument, failure?.Code);
			Assert.Equal("{\"Field\":\"color\"}", Refusal(color: color));
		}

		[Theory]
		[InlineData(-1)]
		[InlineData(0)]
		[InlineData(7)]
		public void StartPositionInTheGameRangeIsAccepted(int startPosition) => Assert.Null(RealtimeHub.SlotFieldFailure(null, null, default, startPosition, ""));

		[Theory]
		[InlineData(-2)]
		[InlineData(8)]
		[InlineData(int.MinValue)]
		public void StartPositionOutsideTheGameRangeIsRefused(int startPosition)
		{
			Assert.Equal(ErrorCodes.InvalidArgument, RealtimeHub.SlotFieldFailure(null, null, default, startPosition, "")?.Code);
			Assert.Equal("{\"Field\":\"start_position\"}", Refusal(startPosition: startPosition));
		}

		[Theory]
		[InlineData(0)]
		[InlineData(3)]
		public void TeamInTheGameRangeIsAccepted(int team) => Assert.Null(RealtimeHub.SlotFieldFailure(null, null, new Optional<int?>(team), null, ""));

		[Fact]
		public void NullTeamClearsTheTeam() => Assert.Null(RealtimeHub.SlotFieldFailure(null, null, new Optional<int?>(null), null, ""));

		[Theory]
		[InlineData(-1)]
		[InlineData(4)]
		[InlineData(100)]
		public void TeamOutsideTheGameRangeIsRefused(int team)
		{
			Assert.Equal(ErrorCodes.InvalidArgument, RealtimeHub.SlotFieldFailure(null, null, new Optional<int?>(team), null, "")?.Code);
			Assert.Equal("{\"Field\":\"team\"}", Refusal(team: new Optional<int?>(team)));
		}

		[Theory]
		[InlineData(-2)] // observer seat
		[InlineData(-1)] // random
		[InlineData(2)]  // USA
		[InlineData(13)] // GLA Stealth General
		public void SideInTheGameRangeIsAccepted(int side) => Assert.Null(RealtimeHub.SlotFieldFailure(side, null, default, null, ""));

		[Theory]
		[InlineData(-3)]
		[InlineData(0)]  // civilian
		[InlineData(1)]  // observer template, the seat is -2
		[InlineData(14)] // Boss General
		[InlineData(int.MaxValue)]
		public void SideOutsideTheGameRangeIsRefused(int side)
		{
			Assert.Equal(ErrorCodes.InvalidArgument, RealtimeHub.SlotFieldFailure(side, null, default, null, "")?.Code);
			Assert.Equal("{\"Field\":\"side\"}", Refusal(side: side));
		}

		[Fact]
		public void HostSlotPatchNamesTheSlotsField()
		{
			Assert.Equal("{\"Field\":\"slots.color\"}", Refusal(color: 99, prefix: "slots."));
			Assert.Equal("{\"Field\":\"slots.start_position\"}", Refusal(startPosition: 99, prefix: "slots."));
		}
	}
}
