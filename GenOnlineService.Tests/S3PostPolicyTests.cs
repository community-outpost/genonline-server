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

using GenOnlineService.Service;
using System.Text;
using System.Text.Json.Nodes;
using Xunit;

namespace GenOnlineService.Tests
{
	public class S3PostPolicyTests
	{
		private static readonly DateTime s_now = new(2026, 11, 21, 9, 30, 0, DateTimeKind.Utc);
		private const string Key = "stats/20261121/551234/42/match_551234_player_42.stats.json.gz";

		private static S3PostForm Form() =>
			S3PostPolicy.Create("https://s3.gra.test/", "gen-data", "gra", "AKIDEXAMPLE", "secretEXAMPLE", Key, "application/gzip", 1, 8 * 1024 * 1024, s_now, TimeSpan.FromMinutes(5));

		private static string Field(S3PostForm form, string name) => form.Fields.Single(f => f.Key == name).Value;

		[Fact]
		public void ThePolicyAndSignatureMatchAnIndependentComputation()
		{
			S3PostForm form = Form();

			// values computed with a separate implementation of the published algorithm
			Assert.Equal("eyJleHBpcmF0aW9uIjoiMjAyNi0xMS0yMVQwOTozNTowMC4wMDBaIiwiY29uZGl0aW9ucyI6W3siYnVja2V0IjoiZ2VuLWRhdGEifSx7ImtleSI6InN0YXRzLzIwMjYxMTIxLzU1MTIzNC80Mi9tYXRjaF81NTEyMzRfcGxheWVyXzQyLnN0YXRzLmpzb24uZ3oifSx7IkNvbnRlbnQtVHlwZSI6ImFwcGxpY2F0aW9uL2d6aXAifSxbImNvbnRlbnQtbGVuZ3RoLXJhbmdlIiwxLDgzODg2MDhdLHsieC1hbXotYWxnb3JpdGhtIjoiQVdTNC1ITUFDLVNIQTI1NiJ9LHsieC1hbXotY3JlZGVudGlhbCI6IkFLSURFWEFNUExFLzIwMjYxMTIxL2dyYS9zMy9hd3M0X3JlcXVlc3QifSx7IngtYW16LWRhdGUiOiIyMDI2MTEyMVQwOTMwMDBaIn1dfQ==",
				Field(form, "policy"));
			Assert.Equal("2c4daf007ed8f28e8472e40df96da8a897c74ca530037dc7e81f5588ff6a3d39", Field(form, "x-amz-signature"));
		}

		[Fact]
		public void ThePolicyPinsTheKeyTypeSizeAndExpiry()
		{
			S3PostForm form = Form();
			JsonObject policy = JsonNode.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(Field(form, "policy"))))!.AsObject();
			JsonArray conditions = policy["conditions"]!.AsArray();

			Assert.Equal("2026-11-21T09:35:00.000Z", (string?)policy["expiration"]);
			Assert.Contains(conditions, c => c is JsonObject o && (string?)o["bucket"] == "gen-data");
			Assert.Contains(conditions, c => c is JsonObject o && (string?)o["key"] == Key);
			Assert.Contains(conditions, c => c is JsonObject o && (string?)o["Content-Type"] == "application/gzip");
			Assert.Contains(conditions, c => c is JsonArray a && (string?)a[0] == "content-length-range" && (long)a[1]! == 1 && (long)a[2]! == 8388608);
			Assert.Equal(s_now.AddMinutes(5), form.ExpiresAt);
		}

		[Theory]
		[InlineData(-1, 10)]
		[InlineData(10, 9)]
		public void ANonsenseSizeRangeIsRefused(long min, long max)
		{
			Assert.Throws<ArgumentException>(() =>
				S3PostPolicy.Create("https://s3.gra.test", "b", "gra", "a", "s", Key, "application/gzip", min, max, s_now, TimeSpan.FromMinutes(5)));
		}
	}
}
