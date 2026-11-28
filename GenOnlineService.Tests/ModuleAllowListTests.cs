using Xunit;

namespace GenOnlineService.Tests;

public class ModuleAllowListTests
{
	[Theory]
	[InlineData("kernel32.dll", "c:/windows/system32")]
	[InlineData("nvd3dum.dll", "c:/windows/system32/driverstore/filerepository/nv_dispi.inf_amd64_1234")]
	[InlineData("d3d9.dll", "d:/windows/syswow64")]
	[InlineData("comctl32.dll", "c:/windows/winsxs/x86_microsoft.windows.common-controls_6595b64144ccf1df_6.0.22621.1_none_1")]
	[InlineData("gameoverlayrenderer.dll", "c:/program files (x86)/steam")]
	[InlineData("discordhook.dll", "c:/users/player/appdata/local/discord/app-1.0.9200/modules/discord_hook-1/discord_hook")]
	[InlineData("rtsshooks.dll", "c:/program files (x86)/rivatuner statistics server")]
	[InlineData("graphics-hook32.dll", "c:/program files/obs-studio/data/obs-plugins/win-capture")]
	public void ADefaultModuleIsAllowed(string module, string directory)
	{
		Assert.True(AntiCheatModuleAllowList.IsAllowedByDefault(module, directory));
	}

	[Theory]
	[InlineData("cheat.dll", "c:/users/player/desktop")]
	[InlineData("kernel32.dll", "c:/windows/system32x")]
	[InlineData("kernel32.dll", "c:/games/windows/system32/../../desktop/x")]
	[InlineData("gameoverlayrenderer.dll", "c:/users/player/desktop")]
	[InlineData("rtsshooks.dll", "c:/temp")]
	[InlineData("kernel32.dll", "c:/windows/system32/../../users/player/desktop")]
	[InlineData("injector.dll", "c:/program files (x86)/steam")]
	public void AnythingElseIsNotAllowedByDefault(string module, string directory)
	{
		Assert.False(AntiCheatModuleAllowList.IsAllowedByDefault(module, directory));
	}
}
