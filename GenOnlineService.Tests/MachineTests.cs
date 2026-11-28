using Database;
using GenOnlineService.Identity;
using GenOnlineService.Names;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GenOnlineService.Tests;

public class MachineTests
{
	private const string Guid = "3f2504e0-4f89-11d3-9a0c-0305e82c3301YY";
	private const string Mac = "AA:BB:CC:DD:EE:01ZZ";
	private const string OtherMac = "AA:BB:CC:DD:EE:02ZZ";
	private const string Volume = "32FE2EEEZZ";

	private static readonly Database.ModerationActor Operator = new() { Kind = AuditActorKinds.Cli, Name = "cli:operator", Source = ModerationSources.Cli };

	private static async Task<long> AddUser(AppDbContext db, string name)
	{
		var user = new User { Username = name, UsernameNormalized = NameNormalizer.Normalize(name), NameTag = 1, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
		db.Users.Add(user);
		await db.SaveChangesAsync();
		db.ChangeTracker.Clear();
		return user.ID;
	}

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task IdentifiersSentTogetherFormOneMachineWithEveryAddress(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		await using AppDbContext db = probe.Open();
		long id = await AddUser(db, "Hunter");

		await UserDevices.RegisterUserDevice(db, id, Guid, Mac, Volume, "192.0.2.1");
		await UserDevices.RegisterUserDevice(db, id, Guid, Mac, Volume, "192.0.2.1");
		await UserDevices.RegisterUserDevice(db, id, Guid, Mac, Volume, "2001:db8::1");
		await UserDevices.RegisterUserDevice(db, id, Guid, OtherMac, Volume, "192.0.2.1");
		await UserDevices.RegisterUserDevice(db, id, Guid, "NONE", Volume, "");

		List<InspectedDevice> devices = await StaffInspection.GetDevices(db, id);
		Assert.Equal(4, devices.Count);
		string KeyOf(string kind, string raw) => devices.Single(d => d.Kind == kind && d.ValueRaw == DeviceHasher.NormalizeRaw(raw)).Key;

		List<InspectedMachine> machines = await StaffInspection.GetMachines(db, id);
		Assert.Equal(3, machines.Count);
		InspectedMachine first = machines.Single(m => m.Keys.Contains(KeyOf(DeviceKinds.Mac, Mac)));
		Assert.Equal(new[] { KeyOf(DeviceKinds.MachineGuid, Guid), KeyOf(DeviceKinds.Mac, Mac), KeyOf(DeviceKinds.VolumeSerial, Volume) }, first.Keys);
		Assert.Equal(new[] { ("2001:db8::1", 1), ("192.0.2.1", 2) }, first.Addresses.Select(a => (a.Ip, a.SignIns)));
		InspectedMachine second = machines.Single(m => m.Keys.Contains(KeyOf(DeviceKinds.Mac, OtherMac)));
		Assert.Equal(("192.0.2.1", 1), (second.Addresses.Single().Ip, second.Addresses.Single().SignIns));
		InspectedMachine withoutMac = machines.Single(m => m.Keys.Count == 2);
		Assert.Empty(withoutMac.Addresses);
		Assert.Equal(withoutMac, machines[0]);
	}

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task RetentionDropsOldAddressesAndMachinesOfRemovedDevices(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		await using AppDbContext db = probe.Open();
		long id = await AddUser(db, "Hunter");
		await UserDevices.RegisterUserDevice(db, id, Guid, Mac, Volume, "192.0.2.1");
		await UserDevices.RegisterUserDevice(db, id, Guid, Mac, Volume, "192.0.2.2");

		DateTime now = DateTime.UtcNow;
		await db.MachineAddresses.Where(a => a.Ip == "192.0.2.1").ExecuteUpdateAsync(s => s.SetProperty(a => a.LastSeenAt, now.AddDays(-91)));
		await Retention.Purge(db, now);
		Assert.Equal("192.0.2.2", Assert.Single(await db.MachineAddresses.ToListAsync()).Ip);
		Assert.Single(await db.Machines.ToListAsync());

		await db.Devices.Where(d => d.Kind == DeviceKinds.Mac).ExecuteUpdateAsync(s => s.SetProperty(d => d.LastSeenAt, now.AddDays(-91)));
		await Retention.Purge(db, now);
		Assert.Empty(await db.Machines.ToListAsync());
		Assert.Empty(await db.MachineAddresses.ToListAsync());
		Assert.Equal(2, await db.Devices.CountAsync());
	}

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task ErasureRemovesTheAddressesOfTheMachines(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		await using AppDbContext db = probe.Open();
		long id = await AddUser(db, "Hunter");
		long other = await AddUser(db, "Friend");
		await UserDevices.RegisterUserDevice(db, id, Guid, Mac, Volume, "192.0.2.1");
		await UserDevices.RegisterUserDevice(db, other, Guid, Mac, Volume, "192.0.2.9");

		await AccountErasure.Erase(db, id, Operator, "request", live: false);

		Assert.Equal("192.0.2.9", Assert.Single(await db.MachineAddresses.ToListAsync()).Ip);
		Assert.Equal(other, Assert.Single(await db.Machines.ToListAsync()).UserID);
	}
}
