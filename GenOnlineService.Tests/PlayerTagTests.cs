using GenOnlineService.Realtime;
using Microsoft.EntityFrameworkCore;
using GenOnlineService.Rest;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System.Text.Json;
using Xunit;

namespace GenOnlineService.Tests;

public class PlayerTagTests
{
	private static readonly JsonSerializerOptions s_json = Json();

	private static JsonSerializerOptions Json()
	{
		JsonSerializerOptions options = new();
		RealtimeJson.Configure(options);
		return options;
	}

	private static IQueryCollection Query(string search) => new QueryCollection(new Dictionary<string, StringValues> { ["search"] = search });

	[Theory]
	[InlineData("Alice#42", "Alice", 42)]
	[InlineData("alice#0042", "alice", 42)]
	[InlineData(" Alice #7 ", "Alice", 7)]
	[InlineData("Alice#9999", "Alice", 9999)]
	public void ASearchWithATagReadsTheNameAndTheNumber(string search, string name, int tag)
	{
		(PlayerListArgs args, Failure? failure) = PlayerArgs.ReadList(Query(search));
		Assert.Null(failure);
		Assert.Equal(name, args.Search);
		Assert.Equal(tag, args.Tag);
	}

	[Theory]
	[InlineData("Alice#")]
	[InlineData("Alice#0")]
	[InlineData("Alice#12345")]
	[InlineData("Alice#4x")]
	[InlineData("Alice#-1")]
	[InlineData("#42")]
	public void ASearchWithABadTagIsInvalid(string search)
	{
		(_, Failure? failure) = PlayerArgs.ReadList(Query(search));
		Assert.NotNull(failure);
		Assert.Equal(ErrorCodes.InvalidArgument, failure.Value.Code);
	}

	[Fact]
	public void ASearchWithoutAHashStaysAPrefixSearch()
	{
		(PlayerListArgs args, Failure? failure) = PlayerArgs.ReadList(Query("Ali"));
		Assert.Null(failure);
		Assert.Equal("Ali", args.Search);
		Assert.Null(args.Tag);
	}

	[Fact]
	public void AMemberOmitsTheTagWhenItIsZero()
	{
		Assert.Contains("\"tag\":\"0042\"", JsonSerializer.Serialize(RealtimeMembers.Of(1, "A", null, 42), s_json));
		Assert.DoesNotContain("tag", JsonSerializer.Serialize(RealtimeMembers.Of(1, "A", null, 0), s_json));
	}

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task TheExactSearchMatchesTheNameWithoutCaseAndTheTagAsANumber(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		await using AppDbContext db = probe.Open();
		long alice42 = await AddUser(db, "Alice", 42);
		await AddUser(db, "Alice", 43);
		await AddUser(db, "Alicia", 42);

		foreach (string name in new[] { "Alice", "ALICE", "alice" })
		{
			(int total, List<Database.PublicPlayer> page) = await PlayerSearch.ByNameAndTag(db, name, 42, 0, 10);
			Assert.Equal(1, total);
			Assert.Equal(alice42, Assert.Single(page).ID);
		}
		Assert.Equal(0, (await PlayerSearch.ByNameAndTag(db, "Alice", 44, 0, 10)).Total);

		// without a tag the prefix search is unchanged
		Assert.Equal(3, (await Database.PublicPlayers.Search(db, "ali", 0, 10)).Total);
	}

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task EveryEntryOfTheSocialListsCarriesTheTag(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		await using AppDbContext db = probe.Open();
		long me = await AddUser(db, "Me", 1);
		long friend = await AddUser(db, "Friend", 42);
		long untagged = await AddUser(db, "Untagged", 0);
		long incoming = await AddUser(db, "Incoming", 7);
		long outgoing = await AddUser(db, "Outgoing", 8);
		long blocked = await AddUser(db, "Blocked", 9);
		DateTime now = DateTime.UtcNow;
		foreach (long other in new[] { friend, untagged })
		{
			db.Friendships.Add(new Friendship { UserIdLow = Math.Min(me, other), UserIdHigh = Math.Max(me, other), CreatedAt = now });
		}
		db.FriendRequests.Add(new FriendRequest { FromUserId = incoming, ToUserId = me, CreatedAt = now });
		db.FriendRequests.Add(new FriendRequest { FromUserId = me, ToUserId = outgoing, CreatedAt = now });
		db.Blocks.Add(new UserBlock { UserId = me, BlockedUserId = blocked, CreatedAt = now });
		await db.SaveChangesAsync();
		db.ChangeTracker.Clear();

		SocialLists lists = await SocialActions.ListsAsync(db, me);
		Assert.Equal("0042", lists.Friends.Single(f => f.Id == friend).Tag);
		Assert.Null(lists.Friends.Single(f => f.Id == untagged).Tag);
		Assert.Equal("0007", Assert.Single(lists.Incoming).Tag);
		Assert.Equal("0008", Assert.Single(lists.Outgoing).Tag);
		Assert.Equal("0009", Assert.Single(lists.Blocked).Tag);
	}

	private static async Task<long> AddUser(AppDbContext db, string name, int tag)
	{
		User user = new User { Username = name, UsernameNormalized = NameNormalizer.Normalize(name), NameTag = tag, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
		db.Users.Add(user);
		await db.SaveChangesAsync();
		return user.ID;
	}
}
