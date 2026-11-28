/*
**    GeneralsOnline Game Services - Backend Services for Command & Conquer Generals Online: Zero Hour
**    Copyright (C) 2025  GeneralsOnline Development Team
**
**    This program is free software: you can redistribute it and/or modify
**    it under the terms of the GNU Affero General Public License as
**    published by the Free Software Foundation, either version 3 of the
**    License, or (at your option) any later version.
**
**    This program is distributed in the hope that it will be useful,
**    but WITHOUT ANY WARRANTY; without even the implied warranty of
**    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
**    GNU Affero General Public License for more details.
**
**    You should have received a copy of the GNU Affero General Public License
**    along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

using System.Globalization;
using System.Text.RegularExpressions;

namespace GenOnlineService
{
	public sealed record PingBeacon(string Region, string Host, int Port);

	// The UDP echo beacons clients ping for lobby latency, set only by the deployment's PingBeacons section
	public static partial class PingBeacons
	{
		// A report carries one entry per beacon, so the list never holds more than a report accepts
		public const int MaxEntries = Realtime.RegionLatency.MaxEntries;
		public const int RegionMaxLength = 32;
		public const int HostMaxLength = 253;

		public static IReadOnlyList<PingBeacon> Current { get; private set; } = [];

		[GeneratedRegex("^[a-z]{2}(-[a-z]+)+-[0-9]$")]
		private static partial Regex RegionPattern();

		[GeneratedRegex("^[A-Za-z0-9.-]+$")]
		private static partial Regex HostPattern();

		public static void Load(IConfiguration config, ILogger log) => Current = Read(config, log);

		public static IReadOnlyList<PingBeacon> Read(IConfiguration config, ILogger log)
		{
			List<PingBeacon> beacons = new();
			foreach (IConfigurationSection entry in config.GetSection("PingBeacons").GetChildren())
			{
				string region = entry["region"] ?? "";
				string host = entry["host"] ?? "";
				bool portValid = int.TryParse(entry["port"], NumberStyles.None, CultureInfo.InvariantCulture, out int port) && port >= 1 && port <= 65535;

				string? problem = beacons.Count >= MaxEntries ? "more than " + MaxEntries + " beacons"
					: region.Length > RegionMaxLength || !RegionPattern().IsMatch(region) ? "invalid region"
					: host.Length > HostMaxLength || !HostPattern().IsMatch(host) ? "invalid host"
					: !portValid ? "invalid port"
					: beacons.Any(beacon => beacon.Region == region) ? "duplicate region"
					: null;
				if (problem != null)
				{
					log.LogWarning("PingBeacons entry {Index} dropped: {Problem}", entry.Key, problem);
					continue;
				}
				beacons.Add(new PingBeacon(region, host, port));
			}
			return beacons;
		}
	}
}
