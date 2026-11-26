using Xunit;

namespace GenOnlineService.Tests;

public class DiscordCommandTests
{
	[Theory]
	[InlineData("1m", 60L)]
	[InlineData("30m", 1800L)]
	[InlineData("12h", 43200L)]
	[InlineData(" 7D ", 604800L)]
	[InlineData("2w", 1209600L)]
	[InlineData("520w", 314496000L)]
	public void ADurationIsWholeMinutesHoursDaysOrWeeks(string text, long seconds)
	{
		Assert.Equal(seconds, DiscordCommands.ParseDuration(text));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("30")]
	[InlineData("m")]
	[InlineData("0m")]
	[InlineData("-5m")]
	[InlineData("1.5h")]
	[InlineData("3y")]
	[InlineData("600w")]
	[InlineData("99999999999999999w")]
	public void AnythingElseIsNoDuration(string? text)
	{
		Assert.Null(DiscordCommands.ParseDuration(text));
	}

	[Fact]
	public void EveryCommandBuildsAndNeedsARealStaffPermission()
	{
		Assert.Equal(DiscordCommands.All.Count, DiscordCommands.All.Select(c => c.Name).Distinct().Count());
		// Discord allows 25 subcommands under one command and refuses the whole set past that
		Assert.InRange(DiscordCommands.All.Count, 1, 25);
		DiscordCommands.BuildRoot(DiscordCommands.DefaultRoot);
		foreach (DiscordCommand command in DiscordCommands.All)
		{
			// Discord refuses a name or description outside its limits when the commands are registered
			Assert.Matches("^[a-z]{1,32}$", command.Name);
			Assert.InRange(command.Description.Length, 1, 100);
			if (command.Permission != null)
			{
				Assert.Contains(command.Permission, Permissions.Human);
			}
		}
		Assert.Null(DiscordCommands.Find("players")!.Permission);
		Assert.Equal(Permissions.ModerationBan, DiscordCommands.Find("unban")!.Permission);
	}

	[Fact]
	public void HelpListsOnlyTheCommandsTheAccountMayUse()
	{
		string moderator = DiscordCommands.HelpText("go", p => p == Permissions.ModerationKick || p == Permissions.ModerationMute);
		Assert.Contains("/go kick", moderator);
		Assert.Contains("/go mute", moderator);
		Assert.Contains("/go player ", moderator);
		Assert.DoesNotContain("ban", moderator);
		Assert.DoesNotContain("announce", moderator);

		string player = DiscordCommands.HelpText("genonline", _ => false);
		Assert.Contains("/genonline players", player);
		Assert.DoesNotContain("kick", player);
	}

	[Theory]
	[InlineData("go", true)]
	[InlineData("genonline", true)]
	[InlineData("go-test", true)]
	[InlineData("go_2", true)]
	[InlineData("", false)]
	[InlineData(null, false)]
	[InlineData("Go", false)]
	[InlineData("go online", false)]
	[InlineData("/go", false)]
	[InlineData("abcdefghijklmnopqrstuvwxyzabcdefg", false)]
	public void TheRootNameFollowsDiscordsRule(string? name, bool valid)
	{
		Assert.Equal(valid, DiscordCommands.IsValidRoot(name));
	}
}
