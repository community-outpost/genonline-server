using Database;
using GenOnlineService.Investigation;
using GenOnlineService.Names;
using GenOnlineService.Realtime;
using Xunit;

namespace GenOnlineService.Tests;

public class InvestigateFingerprintTests
{
	[Theory]
	[InlineData("32FE2EEE", "32fe2eee")]
	[InlineData(" 32fe2eee ", "32fe2eee")]
	[InlineData("32fe2eee01", "32fe2eee01")]
	public void AFingerprintOfEightOrMoreHexCharactersIsAPrefix(string typed, string value)
	{
		(IdentifierQuery? query, Failure? failure) = IdentifierSearch.Read(IdentifierKinds.Fingerprint, typed, null);
		Assert.Null(failure);
		Assert.Equal(value, query!.Value);
		Assert.Equal(Convert.FromHexString(value), query.HashPrefix);
	}

	[Theory]
	[InlineData("32fe2ee")]
	[InlineData("32fe2eee0")]
	[InlineData("32fe2eeg")]
	public void AShortOddOrNonHexFingerprintIsRefused(string typed)
	{
		Assert.NotNull(IdentifierSearch.Read(IdentifierKinds.Fingerprint, typed, null).Failure);
	}

	[Fact]
	public void AFingerprintIsNotADeviceKeyKind()
	{
		Assert.NotNull(IdentifierSearch.Read(IdentifierKinds.Fingerprint, null, new string('a', IdentifierSearch.KeyLength)).Failure);
	}

	private static byte[] Hash(string start)
	{
		byte[] hash = new byte[Device.ValueHmacLength];
		Convert.FromHexString(start).CopyTo(hash, 0);
		hash[^1] = 0x5A;
		return hash;
	}

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task AFingerprintFindsTheDeviceOfEveryKindByItsLeadingBytes(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		await using AppDbContext db = probe.Open();
		var user = new User { Username = "Probe", UsernameNormalized = NameNormalizer.Normalize("Probe"), NameTag = 1, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
		db.Users.Add(user);
		await db.SaveChangesAsync();
		DateTime now = DateTime.UtcNow;
		db.Devices.Add(new Device { UserID = user.ID, Kind = DeviceKinds.VolumeSerial, ValueHmac = Hash("32fe2eee"), FirstSeenAt = now, LastSeenAt = now });
		db.Devices.Add(new Device { UserID = user.ID, Kind = DeviceKinds.Mac, ValueHmac = Hash("c8753459aa"), FirstSeenAt = now, LastSeenAt = now });
		db.Devices.Add(new Device { UserID = user.ID, Kind = DeviceKinds.Mac, ValueHmac = Hash("c8753459bb"), FirstSeenAt = now, LastSeenAt = now });
		db.Devices.Add(new Device { UserID = user.ID, Kind = DeviceKinds.MachineGuid, ValueHmac = Hash("ffffffff"), FirstSeenAt = now, LastSeenAt = now });
		await db.SaveChangesAsync();

		var found = Assert.Single(await IdentifierAccounts.ByHashPrefix(db, Convert.FromHexString("32fe2eee")));
		Assert.Equal(DeviceKinds.VolumeSerial, found.Kind);
		Assert.Equal(Hash("32fe2eee"), found.Hash);
		Assert.Equal(DeviceKinds.MachineGuid, Assert.Single(await IdentifierAccounts.ByHashPrefix(db, Convert.FromHexString("ffffffff"))).Kind);
		Assert.Equal(2, (await IdentifierAccounts.ByHashPrefix(db, Convert.FromHexString("c8753459"))).Count);
		Assert.Single(await IdentifierAccounts.ByHashPrefix(db, Convert.FromHexString("c8753459aa")));
		Assert.Empty(await IdentifierAccounts.ByHashPrefix(db, Convert.FromHexString("32fe2eef")));
	}
}
