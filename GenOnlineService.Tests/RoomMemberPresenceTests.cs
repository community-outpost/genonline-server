using System.Text.Json;
using GenOnlineService.Realtime;
using Xunit;

namespace GenOnlineService.Tests;

public class RoomMemberPresenceTests
{
	private static UserSession SessionOf(long userID) => WebSocketManager.GetAllDataFromUser(userID).Single();

	private static string Json(object value) => JsonSerializer.Serialize(value, RealtimeJson.Create());

	[Theory]
	[InlineData(PresenceStatus.Online, "online")]
	[InlineData(PresenceStatus.Idle, "idle")]
	[InlineData(PresenceStatus.DoNotDisturb, "do_not_disturb")]
	public void ARoomMemberCarriesItsVisibleStatus(PresenceStatus status, string wire)
	{
		using IDisposable connected = AgreementGateTests.Connected(9101, "Idler");
		WebSocketManager.GetSharedDataForUser(9101)!.PresenceStatus = status;

		Member member = RealtimeMembers.Of(SessionOf(9101));

		Assert.Equal(status, member.Status);
		Assert.Contains("\"status\":\"" + wire + "\"", Json(new ChannelMembersEvent { ChannelId = 1, Members = [member] }));
		Assert.Contains("\"status\":\"" + wire + "\"", Json(new MemberAddEvent { ChannelId = "channel:1", Member = member }));
		Assert.Contains("\"status\":\"" + wire + "\"", Json(new MemberUpdateEvent { Member = member }));
	}

	[Fact]
	public void AnInvisibleMemberSendsNoStatus()
	{
		using IDisposable connected = AgreementGateTests.Connected(9102, "Ghost");
		WebSocketManager.GetSharedDataForUser(9102)!.PresenceStatus = PresenceStatus.Invisible;

		Member member = RealtimeMembers.Of(SessionOf(9102));

		Assert.Null(member.Status);
		Assert.DoesNotContain("status", Json(member));
	}

	[Fact]
	public void AMemberBuiltWithoutASessionHasNoStatus()
	{
		Assert.DoesNotContain("status", Json(RealtimeMembers.Of(9103, "Offline", null, 1)));
	}

	[Theory]
	[InlineData(PresenceStatus.Online, PresenceStatus.Idle, RealtimeHub.ChannelChange.Status)]
	[InlineData(PresenceStatus.Idle, PresenceStatus.DoNotDisturb, RealtimeHub.ChannelChange.Status)]
	[InlineData(PresenceStatus.Online, PresenceStatus.Online, RealtimeHub.ChannelChange.None)]
	[InlineData(PresenceStatus.Online, PresenceStatus.Invisible, RealtimeHub.ChannelChange.Visibility)]
	[InlineData(PresenceStatus.Invisible, PresenceStatus.Idle, RealtimeHub.ChannelChange.Visibility)]
	[InlineData(PresenceStatus.Invisible, PresenceStatus.Invisible, RealtimeHub.ChannelChange.None)]
	public void OnlyAChangeBetweenVisibleStatusesUpdatesTheMember(PresenceStatus previous, PresenceStatus status, RealtimeHub.ChannelChange change)
	{
		Assert.Equal(change, RealtimeHub.ChannelChangeOf(previous, status));
	}
}
