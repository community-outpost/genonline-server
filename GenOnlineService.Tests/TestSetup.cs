using System.Runtime.CompilerServices;
using GenOnlineService.Names;

namespace GenOnlineService.Tests;

internal static class TestSetup
{
	[ModuleInitializer]
	internal static void Initialize()
	{
		DeviceHasher.Initialize(Path.Combine(Path.GetTempPath(), "gn_probe_device_hmac.key"));
		NameTags.UseKey(Enumerable.Range(0, 32).Select(i => (byte)(i * 7 + 1)).ToArray());
	}
}
