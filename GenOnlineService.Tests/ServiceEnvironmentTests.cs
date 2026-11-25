using GenOnlineService.Investigation;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace GenOnlineService.Tests
{
	public class ServiceEnvironmentTests
	{
		private static IConfiguration Config(params (string Key, string? Value)[] values) =>
			new ConfigurationBuilder().AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();

		[Theory]
		[InlineData("live")]
		[InlineData("test")]
		[InlineData("staging")]
		[InlineData("eu-prod")]
		public void AnyWellFormedNameIsTheEnvironment(string name) =>
			Assert.Equal(name, ServiceEnvironment.Read(Config(("Service:environment", name)), isDevelopment: false).Name);

		[Theory]
		[InlineData("Live")]
		[InlineData("1live")]
		[InlineData("live test")]
		[InlineData("-live")]
		[InlineData("abcdefghijklmnopqrstuvwxyz0123456")]
		public void MalformedNameStopsTheStart(string name) =>
			Assert.Throws<InvalidOperationException>(() => ServiceEnvironment.Read(Config(("Service:environment", name)), isDevelopment: false));

		[Fact]
		public void UnsetNameIsDevOnlyOnADevelopmentHost()
		{
			Assert.Equal(ServiceEnvironment.Dev, ServiceEnvironment.Read(Config(), isDevelopment: true).Name);
			Assert.Throws<InvalidOperationException>(() => ServiceEnvironment.Read(Config(), isDevelopment: false));
		}

		[Fact]
		public void GeoIpPathsComeFromSettingsWithDefaults()
		{
			try
			{
				GeoIpFiles.Configure(Config(("GeoIP:city_path", "/geo/city.mmdb"), ("GeoIP:asn_path", " ")));
				Assert.Equal("/geo/city.mmdb", GeoIpFiles.CityPath);
				Assert.Equal(GeoIpFiles.DefaultAsnPath, GeoIpFiles.AsnPath);
			}
			finally
			{
				GeoIpFiles.Configure(Config());
			}
		}
	}
}
