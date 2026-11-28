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

using System.Buffers.Text;
using System.Text;
using GenOnlineService.Realtime;
using GenOnlineService.Rest;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Xunit;

namespace GenOnlineService.Tests;

public class MatchFeedArgsTests
{
	private static IQueryCollection Query(string text) => new QueryCollection(QueryHelpers.ParseQuery(text));

	private static string Cursor(string json) => Uri.EscapeDataString(Base64Url.EncodeToString(Encoding.UTF8.GetBytes(json)));

	[Theory]
	[InlineData("{\"v\":1,\"s\":\"f\",\"o\":\"d\",\"dir\":\"n\",\"k\":[null,1],\"q\":\"0000000000000000\",\"f\":{}}")]
	[InlineData("{\"v\":1,\"s\":\"i\",\"o\":\"d\",\"dir\":\"n\",\"k\":[1],\"q\":\"0000000000000000\",\"f\":{\"map\":\"a\",\"map\":\"b\"}}")]
	public void AMalformedCursorIsAnInvalidCursor(string json)
	{
		(MatchFeedRequest? request, Failure? failure) = MatchFeedArgs.Read(Query("?cursor=" + Cursor(json)), null);
		Assert.Null(request);
		Assert.Equal(MatchFeedCodes.InvalidCursor, failure?.Code);
	}

	[Theory]
	[InlineData("?after_id=1&page=2", true)]
	[InlineData("?since=2026-11-01T00:00:00Z&per_page=5&before_id=3&x=", true)]
	[InlineData("?after_id=1&map=x", false)]
	public void ALegacyRequestIgnoresUnknownParameters(string query, bool legacy)
	{
		(bool isLegacy, Failure? failure) = MatchFeedArgs.Classify(Query(query), ["after_id", "since"]);
		Assert.Equal(legacy, isLegacy);
		Assert.Equal(legacy ? null : MatchFeedCodes.MixedPaging, failure?.Code);
	}

	[Theory]
	[InlineData("?page=2")]
	[InlineData("?foo=1")]
	[InlineData("?per_page=20&page=2")]
	public void ARequestWithoutCursorSortOrFilterIgnoresUnknownParameters(string query)
	{
		(bool isLegacy, Failure? failure) = MatchFeedArgs.Classify(Query(query), ["after_id", "since"]);
		Assert.False(isLegacy);
		Assert.Null(failure);
	}

	[Theory]
	[InlineData("?map=x&before_id=1")]
	[InlineData("?cursor=abc&page=2")]
	public void ACursorStyleRequestRefusesUnknownParameters(string query)
	{
		(bool isLegacy, Failure? failure) = MatchFeedArgs.Classify(Query(query), ["after_id", "since"]);
		Assert.False(isLegacy);
		Assert.Equal(MatchFeedCodes.UnknownParameter, failure?.Code);
	}
}
