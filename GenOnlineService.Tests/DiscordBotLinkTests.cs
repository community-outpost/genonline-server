using Database;
using GenOnlineService.Names;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GenOnlineService.Tests;

public class DiscordBotLinkTests
{
	private static readonly ModerationActor Staff = new ModerationActor { Kind = AuditActorKinds.User, UserID = 1, Name = "user:1", Source = ModerationSources.Api };

	[Theory]
	[InlineData("123456789012345678", 123456789012345678L)]
	[InlineData("  123456789012345678 ", 123456789012345678L)]
	[InlineData("9223372036854775807", Int64.MaxValue)]
	public void ASnowflakeIsADiscordUserID(string text, long expected)
	{
		Assert.True(DiscordBotLinks.TryParse(text, out long discordUserID));
		Assert.Equal(expected, discordUserID);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("12345")]
	[InlineData("12345678901234567a")]
	[InlineData("-123456789012345678")]
	[InlineData("99999999999999999999")]
	[InlineData("@someone")]
	public void AnythingElseIsRefused(string? text)
	{
		Assert.False(DiscordBotLinks.TryParse(text, out _));
	}

	private static async Task<long> AddUser(AppDbContext db, string name, int tag)
	{
		var user = new User { Username = name, UsernameNormalized = NameNormalizer.Normalize(name), NameTag = tag, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
		db.Users.Add(user);
		await db.SaveChangesAsync();
		return user.ID;
	}

	[ProbeTheory]
	[MemberData(nameof(ProbeDatabase.Providers), MemberType = typeof(ProbeDatabase))]
	public async Task OneDiscordUserActsForOneAccountAndEveryChangeIsAudited(string provider)
	{
		await using ProbeDatabase? probe = await ProbeDatabase.Create(provider);
		if (probe == null) return;
		await using AppDbContext db = probe.Open();
		long moderator = await AddUser(db, "Moderator", 1001);
		long other = await AddUser(db, "Other", 1002);

		Assert.Equal((DiscordBotLinkOutcome.Linked, (long?)null), await DiscordBotLinks.Set(db, moderator, 111111111111111111, Staff));
		Assert.Equal(moderator, await DiscordBotLinks.UserOf(db, 111111111111111111));
		Assert.Equal((DiscordBotLinkOutcome.Unchanged, (long?)null), await DiscordBotLinks.Set(db, moderator, 111111111111111111, Staff));
		Assert.Equal((DiscordBotLinkOutcome.InUse, (long?)moderator), await DiscordBotLinks.Set(db, other, 111111111111111111, Staff));
		Assert.Null(await DiscordBotLinks.Get(db, other));

		Assert.Equal(DiscordBotLinkOutcome.Linked, (await DiscordBotLinks.Set(db, moderator, 222222222222222222, Staff)).Outcome);
		Assert.Equal(222222222222222222, await DiscordBotLinks.Get(db, moderator));
		Assert.Null(await DiscordBotLinks.UserOf(db, 111111111111111111));

		Assert.True(await DiscordBotLinks.Clear(db, moderator, Staff));
		Assert.False(await DiscordBotLinks.Clear(db, moderator, Staff));
		Assert.Null(await DiscordBotLinks.Get(db, moderator));

		List<AuditLogEntry> audit = await db.AuditLog.Where(e => e.TargetID == moderator).OrderBy(e => e.ID).ToListAsync();
		Assert.Equal([ModerationActions.DiscordBotLink, ModerationActions.DiscordBotLink, ModerationActions.DiscordBotUnlink], audit.Select(e => e.Action));
		Assert.Equal("111111111111111111", System.Text.Json.JsonDocument.Parse(audit[1].Details!).RootElement.GetProperty("previous").GetString());
		Assert.All(audit, e => Assert.Equal("user:1", e.Actor));
		Assert.Equal(0, await db.AuditLog.CountAsync(e => e.TargetID == other));
	}
}
