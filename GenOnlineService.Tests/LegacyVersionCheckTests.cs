using GenOnlineService.Rest;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using Xunit;

namespace GenOnlineService.Tests
{
	public class LegacyVersionCheckTests
	{
		private static LegacyClientSettings Settings(params (string Key, string Value)[] values)
		{
			IConfiguration config = new ConfigurationBuilder()
				.AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>("LegacyClients:" + v.Key, v.Value)))
				.Build();
			return LegacyClientSettings.Read(config);
		}

		[Fact]
		public void AnOldClientIsSentToTheLauncherInstaller()
		{
			LegacyClientSettings settings = Settings(("installer_name", "GenOnlineLauncherSetup.exe"), ("installer_url", "https://example.org/GenOnlineLauncherSetup.exe"), ("installer_size", "1234"));

			string json = JsonSerializer.Serialize(LegacyVersionCheckAnswer.For(settings));

			Assert.Equal("{\"result\":2,\"patcher_name\":\"GenOnlineLauncherSetup.exe\",\"patcher_path\":\"https://example.org/GenOnlineLauncherSetup.exe\",\"patcher_size\":1234}", json);
		}

		[Theory]
		[InlineData("..\\setup.exe")]
		[InlineData("dir/setup.exe")]
		[InlineData("C:setup.exe")]
		public void ANameTheOldClientWouldRefuseIsNeverSent(string name)
		{
			LegacyVersionCheckAnswer answer = LegacyVersionCheckAnswer.For(Settings(("installer_name", name), ("installer_url", "https://example.org/setup.exe"), ("installer_size", "10")));

			Assert.Equal(LegacyVersionCheckAnswer.Failed, answer.result);
		}

		[Fact]
		public void WithoutAnInstallerTheCheckFails()
		{
			LegacyVersionCheckAnswer answer = LegacyVersionCheckAnswer.For(Settings(("installer_url", "https://example.org/setup.exe")));

			Assert.Equal(LegacyVersionCheckAnswer.Failed, answer.result);
			Assert.Equal(String.Empty, answer.patcher_path);
		}
	}
}
