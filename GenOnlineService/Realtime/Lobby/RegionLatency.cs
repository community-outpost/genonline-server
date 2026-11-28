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

namespace GenOnlineService.Realtime
{
	// The round trip times a client measured to the ping beacons, kept on its session in memory only
	internal static class RegionLatency
	{
		public const int MaxEntries = 20;
		public const int MinRttMs = 1;
		public const int MaxRttMs = 2000;
		public const long ReportIntervalMs = 60_000;
		public const int MinSharedRegions = 2;

		// The protocol places the estimate 0.6 of the way from the lower to the upper bound
		private const double UpperBoundWeight = 0.6;

		public static Failure? Report(UserSession session, ReportRegionLatencyRequest request, IReadOnlyList<PingBeacon> beacons, long nowMs)
		{
			if (request.Regions == null || request.Regions.Count > MaxEntries)
			{
				return Result.Invalid("regions");
			}
			if (!session.TryTakeRegionReport(nowMs, ReportIntervalMs, out long retryAfterMs))
			{
				return Result.Fail(ErrorCodes.RateLimited, new { RetryAfterMs = retryAfterMs });
			}

			Dictionary<string, int> rtts = new(StringComparer.Ordinal);
			foreach (RegionRtt entry in request.Regions)
			{
				if (entry?.Region != null && beacons.Any(beacon => beacon.Region == entry.Region))
				{
					rtts.TryAdd(entry.Region, Math.Clamp(entry.RttMs, MinRttMs, MaxRttMs));
				}
			}
			session.RegionRtts = rtts;
			return null;
		}

		// The triangle inequality bounds the pair latency from both sides when both ends measured the same beacons
		public static int? BoundMs(IReadOnlyDictionary<string, int>? a, IReadOnlyDictionary<string, int>? b)
		{
			if (a == null || b == null)
			{
				return null;
			}

			int shared = 0;
			int lower = 0;
			int upper = int.MaxValue;
			foreach ((string region, int rttA) in a)
			{
				if (b.TryGetValue(region, out int rttB))
				{
					shared++;
					lower = Math.Max(lower, Math.Abs(rttA - rttB));
					upper = Math.Min(upper, rttA + rttB);
				}
			}
			return shared < MinSharedRegions ? null : (int)Math.Round(lower + UpperBoundWeight * (upper - lower), MidpointRounding.AwayFromZero);
		}
	}
}
