using Database;
using GenOnlineService.Failover;
using GenOnlineService.Identity;
using GenOnlineService.Investigation;
using GenOnlineService.Names;
using GenOnlineService.Rest;
using Microsoft.EntityFrameworkCore;
using OpenIddict.EntityFrameworkCore.Models;
using System.Text.Json;
using Xunit;

namespace GenOnlineService.Tests;

public class MatchSessionTests
{
	private const string Guid = "3f2504e0-4f89-11d3-9a0c-0305e82c3301YY";
	private const string Mac = "AA:BB:CC:DD:EE:01ZZ";
	private const string Volume = "32FE2EEEZZ";
	private const string Address = "192.0.2.7";

	private sealed class StubWorkerStore : IMatchIdWorkerStore
	{
		public Task<MatchIdSlot?> AcquireAsync(string holder, long nowSecond, int windowSeconds, CancellationToken cancellationToken) =>
			Task.FromResult<MatchIdSlot?>(new MatchIdSlot(1, 1, nowSecond, nowSecond + windowSeconds));

		public Task<bool> ExtendAsync(MatchIdSlot slot, long reservedUntil, CancellationToken cancellationToken) => Task.FromResult(true);
	}

	private static async Task<long> AddUser(AppDbContext db, string name)
	{
		var user = new User { Username = name, UsernameNormalized = NameNormalizer.Normalize(name), NameTag = 1, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
		db.Users.Add(user);
		await db.SaveChangesAsync();
		db.ChangeTracker.Clear();
		return user.ID;
	}

	// A game client sign-in as the service writes it: machine, registration, then the session event of the connection
	private static async Task<long> StartSession(AppDbContext db, long userID, string authorization, string address, string guid = Guid)
	{
		long? machine = await UserDevices.RegisterUserDevice(db, userID, guid, Mac, Volume, address);
		db.Set<OpenIddictEntityFrameworkCoreAuthorization>().Add(new OpenIddictEntityFrameworkCoreAuthorization { Id = authorization, Subject = userID.ToString(), Status = "valid", Type = "permanent", CreationDate = DateTime.UtcNow });
		await db.SaveChangesAsync();
		db.ChangeTracker.Clear();
		await GameSessionStore.Save(db, userID, EUserSessionType.GameClient, new GameSessionRegistration(authorization, "genonline_zerohour", "zh", DateTime.UtcNow, 1, 2, 3));
		await GameSessionStore.SetMachine(db, userID, EUserSessionType.GameClient, authorization, machine);
		db.ChangeTracker.Clear();
		return (await Users.UpdateLastLoginData(db, userID, address, "genonline_zerohour", "NL", false, EUserSessionType.GameClient, authorization))!.Value;
	}

	private static async Task<long> PlayMatch(AppDbContext db, long first, long second)
	{
		LobbyLive live = new()
		{
			Id = 10,
			Owner = first,
			Name = "lobby",
			Slots = Enumerable.Range(0, Lobby.MaxLobbySize).Select(index => new LobbyMemberLive
			{
				SlotIndex = index,
				SlotState = (int)(index < 2 ? EPlayerType.SLOT_PLAYER : EPlayerType.SLOT_OPEN),
				UserId = index == 0 ? first : index == 1 ? second : -1
			}).ToList()
		};
		return (long)await MatchHistory.CreatePlaceholderMatchHistory(db, Lobby.FromLive(live), new MatchIdGenerator(new StubWorkerStore(), TimeProvider.System, "test"));
	}

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task AGameClientStartEventCarriesTheMachineOfItsRegistration(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		await using AppDbContext db = probe.Open();
		long id = await AddUser(db, "Hunter");

		long eventID = await StartSession(db, id, "auth-1", Address);
		long machine = (await db.Machines.SingleAsync()).ID;
		Assert.Equal(machine, (await db.SessionEvents.SingleAsync(e => e.ID == eventID)).MachineID);

		// an event of an authorization that is not the registered one never takes its machine
		long other = (await Users.UpdateLastLoginData(db, id, Address, "genonline_zerohour", "NL", false, EUserSessionType.GameClient, "auth-other"))!.Value;
		Assert.Null((await db.SessionEvents.SingleAsync(e => e.ID == other)).MachineID);
		long chat = (await Users.UpdateLastLoginData(db, id, Address, "genonline_zerohour", "NL", false, EUserSessionType.ChatClient, "auth-1"))!.Value;
		Assert.Null((await db.SessionEvents.SingleAsync(e => e.ID == chat)).MachineID);
	}

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task AMatchPlayerCarriesTheStartEventOfItsSessionAndRetentionClearsIt(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		await using AppDbContext db = probe.Open();
		long first = await AddUser(db, "Hunter");
		long second = await AddUser(db, "Friend");
		long firstEvent = await StartSession(db, first, "auth-1", Address);
		await StartSession(db, second, "auth-2", "198.51.100.4", guid: Guid.Replace("3f25", "3f26"));

		using IDisposable one = AgreementGateTests.Connected(first, "Hunter");
		using IDisposable two = AgreementGateTests.Connected(second, "Friend");
		WebSocketManager.GetSessionFromUser(first, EUserSessionType.GameClient)!.SessionEventID = firstEvent;

		long matchID = await PlayMatch(db, first, second);
		Assert.NotEqual(0, matchID);
		List<MatchPlayer> players = await db.MatchPlayers.Where(p => p.MatchId == matchID).OrderBy(p => p.Slot).ToListAsync();
		Assert.Equal(firstEvent, players[0].SessionEventId);
		// a session that kept no start event leaves the link empty
		Assert.Null(players[1].SessionEventId);

		await db.SessionEvents.ExecuteUpdateAsync(s => s.SetProperty(e => e.CreatedAt, DateTime.UtcNow.AddDays(-91)));
		await Retention.Purge(db, DateTime.UtcNow);
		Assert.Empty(await db.SessionEvents.ToListAsync());
		Assert.Equal(2, await db.MatchPlayers.CountAsync(p => p.MatchId == matchID));
		Assert.All(await db.MatchPlayers.ToListAsync(), p => Assert.Null(p.SessionEventId));
		Assert.Empty(await StaffInspection.GetMatchSessions(db, matchID));
	}

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task StaffSeeTheDeviceAddressAndMatchesOfASession(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		await using AppDbContext db = probe.Open();
		long first = await AddUser(db, "Hunter");
		long second = await AddUser(db, "Friend");
		long firstEvent = await StartSession(db, first, "auth-1", Address);
		await StartSession(db, second, "auth-2", "198.51.100.4", guid: Guid.Replace("3f25", "3f26"));

		using IDisposable one = AgreementGateTests.Connected(first, "Hunter");
		using IDisposable two = AgreementGateTests.Connected(second, "Friend");
		WebSocketManager.GetSessionFromUser(first, EUserSessionType.GameClient)!.SessionEventID = firstEvent;
		WebSocketManager.GetSessionFromUser(second, EUserSessionType.GameClient)!.SessionEventID = await db.SessionEvents.Where(e => e.UserID == second).Select(e => e.ID).SingleAsync();
		long matchID = await PlayMatch(db, first, second);

		Dictionary<long, MatchSession> sessions = await StaffInspection.GetMatchSessions(db, matchID);
		Assert.Equal(new[] { DeviceKinds.MachineGuid, DeviceKinds.Mac, DeviceKinds.VolumeSerial }, sessions[first].Devices.Select(d => d.Kind));
		Assert.Equal((Address, "NL"), (sessions[first].Ip, sessions[first].Country));
		Assert.Equal("198.51.100.4", sessions[second].Ip);

		// the device key of the first player finds the match with the first player only, the shared Mac with both
		List<InspectedDevice> devices = await StaffInspection.GetDevices(db, first);
		string guidKey = devices.Single(d => d.Kind == DeviceKinds.MachineGuid).Key;
		List<IdentifierMatch> byDevice = await IdentifierAccounts.MatchesOf(db, IdentifierSearch.Read("machine_guid", null, guidKey).Query!);
		Assert.Equal((matchID, first), (byDevice.Single().MatchID, byDevice.Single().UserID));
		string macKey = devices.Single(d => d.Kind == DeviceKinds.Mac).Key;
		Assert.Equal(2, (await IdentifierAccounts.MatchesOf(db, IdentifierSearch.Read("mac", null, macKey).Query!)).Count);

		List<IdentifierMatch> byAddress = await IdentifierAccounts.MatchesOf(db, IdentifierSearch.Read("ip", Address, null).Query!);
		Assert.Equal("Hunter", byAddress.Single().Name);
		List<IdentifierMatch> byRange = await IdentifierAccounts.MatchesOf(db, IdentifierSearch.Read("ip_range", "192.0.2.0/24", null).Query!);
		Assert.Equal(first, byRange.Single().UserID);
		Assert.Empty(await IdentifierAccounts.MatchesOf(db, IdentifierSearch.Read("country", "NL", null).Query!));

		// the player page lists the match on the machine it was played on
		InspectedMachine machine = Assert.Single(await StaffInspection.GetMachines(db, first));
		Assert.Equal(matchID, Assert.Single(machine.Matches).MatchID);
	}

	[Fact]
	public void ASlotWithoutASessionLeavesTheSessionOutOfTheJson()
	{
		string json = JsonSerializer.Serialize(new MatchSlot());
		Assert.DoesNotContain("session", json, StringComparison.OrdinalIgnoreCase);
		Assert.Contains("\"session\":", JsonSerializer.Serialize(new MatchSlot { Session = new MatchSlotSession { Ip = Address } }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
	}
}
