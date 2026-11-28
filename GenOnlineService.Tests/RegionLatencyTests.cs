using System.Text.Json;
using GenOnlineService.Realtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GenOnlineService.Tests;

public class RegionLatencyTests
{
	private static readonly IReadOnlyList<PingBeacon> s_beacons =
	[
		new PingBeacon("tt-east-1", "beacon.test", 7770),
		new PingBeacon("tt-west-2", "beacon.test", 7770),
		new PingBeacon("tt-north-3", "beacon.test", 7770)
	];

	private static UserSession Session(Int64 userID, double latitude = 0, double longitude = 0, params (string Region, int Rtt)[] rtts)
	{
		UserSession session = new UserSession(userID, EUserSessionType.GameClient, "genonline_zerohour", "", "", latitude, longitude);
		if (rtts.Length > 0)
		{
			session.RegionRtts = rtts.ToDictionary(r => r.Region, r => r.Rtt, StringComparer.Ordinal);
		}
		return session;
	}

	private static ReportRegionLatencyRequest Report(params (string Region, int Rtt)[] rtts) =>
		new ReportRegionLatencyRequest { Regions = rtts.Select(r => new RegionRtt { Region = r.Region, RttMs = r.Rtt }).ToArray() };

	[Fact]
	public void TheBoundEstimateLiesBetweenTheLowerAndUpperBound()
	{
		// L = max(40, 50, 50) = 50, U = min(80, 110, 250) = 80, E = 50 + 0.6 * 30 = 68
		UserSession a = Session(1, rtts: [("tt-east-1", 20), ("tt-west-2", 80), ("tt-north-3", 150)]);
		UserSession b = Session(2, rtts: [("tt-east-1", 60), ("tt-west-2", 30), ("tt-north-3", 100)]);

		Assert.Equal(68, RegionLatency.BoundMs(a.RegionRtts, b.RegionRtts));
		Assert.Equal((68, LatencySource.Beacon), RealtimeLobbies.EstimateLatency(a, [b]));
	}

	[Fact]
	public void FewerThanTwoSharedRegionsFallBackToGeo()
	{
		UserSession a = Session(1, 0, 0, ("tt-east-1", 20), ("tt-west-2", 80));
		UserSession b = Session(2, 0, 10, ("tt-east-1", 60), ("tt-north-3", 100));
		UserSession c = Session(3, 0, 10);

		Assert.Null(RegionLatency.BoundMs(a.RegionRtts, b.RegionRtts));
		Assert.Null(RegionLatency.BoundMs(a.RegionRtts, c.RegionRtts));
		(int ms, LatencySource source) = RealtimeLobbies.EstimateLatency(a, [b]);
		Assert.Equal(LatencySource.Geo, source);
		Assert.Equal(RealtimeLobbies.EstimateLatency(Session(4, 0, 0), [c]).Ms, ms);
		Assert.True(ms > 0);
	}

	[Fact]
	public void TheLobbyTakesTheLargestPairAndIsBeaconOnlyWhenEveryPairIs()
	{
		UserSession viewer = Session(1, 0, 0, ("tt-east-1", 20), ("tt-west-2", 80));
		UserSession near = Session(2, 0, 0, ("tt-east-1", 30), ("tt-west-2", 70));
		UserSession far = Session(3, 0, 0, ("tt-east-1", 120), ("tt-west-2", 40));
		UserSession unmeasured = Session(4, 0, 170);

		// near: L = 10, U = 50, E = 34. far: L = 100, U = 120, E = 112
		Assert.Equal((112, LatencySource.Beacon), RealtimeLobbies.EstimateLatency(viewer, [near, far]));

		(int ms, LatencySource source) = RealtimeLobbies.EstimateLatency(viewer, [near, unmeasured]);
		Assert.Equal(LatencySource.Geo, source);
		Assert.True(ms > 34);

		Assert.Equal((0, LatencySource.Geo), RealtimeLobbies.EstimateLatency(viewer, []));
	}

	[Fact]
	public void TheSummarySendsTheLatencySource()
	{
		string json = JsonSerializer.Serialize(new LobbySummary { LatencySource = LatencySource.Beacon }, RealtimeJson.Create());
		Assert.Contains("\"latency_source\":\"beacon\"", json);
		Assert.Contains("\"latency_source\":\"geo\"", JsonSerializer.Serialize(new LobbySummary(), RealtimeJson.Create()));
	}

	[Fact]
	public void AReportClampsTheRttAndIgnoresUnknownRegions()
	{
		UserSession session = Session(1);
		Failure? failure = RegionLatency.Report(session, Report(("tt-east-1", 0), ("tt-west-2", 5000), ("tt-nowhere-9", 50), ("Not A Region", 40), ("tt-north-3", 77)), s_beacons, 1_000_000);

		Assert.Null(failure);
		Assert.Equal(new Dictionary<string, int> { ["tt-east-1"] = 1, ["tt-west-2"] = 2000, ["tt-north-3"] = 77 }, session.RegionRtts);
	}

	[Fact]
	public void AReportOfMoreThanTwentyEntriesIsRefused()
	{
		UserSession session = Session(1);
		Failure? failure = RegionLatency.Report(session, Report(Enumerable.Range(0, 21).Select(i => ("tt-east-1", i + 1)).ToArray()), s_beacons, 1_000_000);

		Assert.Equal(ErrorCodes.InvalidArgument, failure?.Code);
		Assert.Null(session.RegionRtts);
		Assert.Equal(ErrorCodes.InvalidArgument, RegionLatency.Report(session, new ReportRegionLatencyRequest { Regions = null! }, s_beacons, 1_000_000)?.Code);
	}

	[Fact]
	public void ASecondReportWithinSixtySecondsIsRateLimitedAndChangesNothing()
	{
		UserSession session = Session(1);
		Assert.Null(RegionLatency.Report(session, Report(("tt-east-1", 40)), s_beacons, 1_000_000));

		Failure? failure = RegionLatency.Report(session, Report(("tt-east-1", 90)), s_beacons, 1_059_999);
		Assert.Equal(ErrorCodes.RateLimited, failure?.Code);
		Assert.Equal(40, session.RegionRtts!["tt-east-1"]);

		Assert.Null(RegionLatency.Report(session, Report(("tt-east-1", 90)), s_beacons, 1_060_000));
		Assert.Equal(90, session.RegionRtts!["tt-east-1"]);
	}

	[Fact]
	public void ConfiguredBeaconsAreValidatedAndInvalidEntriesDropped()
	{
		IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
		{
			["PingBeacons:0:region"] = "tt-east-1",
			["PingBeacons:0:host"] = "beacon.test",
			["PingBeacons:0:port"] = "7770",
			["PingBeacons:1:region"] = "tt-east-1",
			["PingBeacons:1:host"] = "other.test",
			["PingBeacons:1:port"] = "7770",
			["PingBeacons:2:region"] = "TT-east-1",
			["PingBeacons:2:host"] = "beacon.test",
			["PingBeacons:2:port"] = "7770",
			["PingBeacons:3:region"] = "tt-west-2",
			["PingBeacons:3:host"] = "beacon test",
			["PingBeacons:3:port"] = "7770",
			["PingBeacons:4:region"] = "tt-west-2",
			["PingBeacons:4:host"] = "beacon.test",
			["PingBeacons:4:port"] = "70000",
			["PingBeacons:5:region"] = "tt-west-2",
			["PingBeacons:5:host"] = "beacon.test",
			["PingBeacons:5:port"] = "7771"
		}).Build();

		Assert.Equal([new PingBeacon("tt-east-1", "beacon.test", 7770), new PingBeacon("tt-west-2", "beacon.test", 7771)], PingBeacons.Read(config, NullLogger.Instance));
		Assert.Empty(PingBeacons.Read(new ConfigurationBuilder().Build(), NullLogger.Instance));
	}

	[Fact]
	public void TheBeaconListHoldsNoMoreThanAReportAccepts()
	{
		Dictionary<string, string?> values = new();
		for (int i = 0; i < 25; ++i)
		{
			values[$"PingBeacons:{i}:region"] = $"tt-east{(char)('a' + i)}-1";
			values[$"PingBeacons:{i}:host"] = "beacon.test";
			values[$"PingBeacons:{i}:port"] = "7770";
		}

		IReadOnlyList<PingBeacon> read = PingBeacons.Read(new ConfigurationBuilder().AddInMemoryCollection(values).Build(), NullLogger.Instance);

		Assert.Equal(RegionLatency.MaxEntries, read.Count);
	}

	[Fact]
	public void ReadySendsTheBeaconList()
	{
		string json = JsonSerializer.Serialize(new ReadyEvent { PingBeacons = [new PingBeacon("tt-east-1", "beacon.test", 7770)] }, RealtimeJson.Create());
		Assert.Contains("\"ping_beacons\":[{\"region\":\"tt-east-1\",\"host\":\"beacon.test\",\"port\":7770}]", json);
		Assert.Contains("\"ping_beacons\":[]", JsonSerializer.Serialize(new ReadyEvent(), RealtimeJson.Create()));
	}
}
