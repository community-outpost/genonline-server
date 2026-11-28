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

using GenOnlineService.Api;
using GenOnlineService.Catalog;
using GenOnlineService.Realtime;
using GenOnlineService.Rest;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace GenOnlineService.Tests;

public class MotdCatalogTests
{
	[Fact]
	public void TheImportStoresTheFileTextAsWritten()
	{
		string file = "Hello {0}\r\n\\ffff7f2f Rules\r\n";

		string stored = MotdSeed.StoredValue(file);

		Assert.True(ConfigSources.TryReadMotdSetting(stored, out string? read));
		Assert.Equal(file, read);
	}

	[Fact]
	public void TheMotdIsReadFromTheSettingOnly()
	{
		Assert.Null(ConfigSources.MotdFrom(null, 5));
		Assert.Null(ConfigSources.MotdFrom("42", 5));
		Assert.Null(ConfigSources.MotdFrom("not json", 5));
		Assert.Equal("5 online", ConfigSources.MotdFrom("\"{0} online\"", 5));
	}

	[Fact]
	public void AnEmptyMessageIsNoMotd()
	{
		Assert.Null(ConfigSources.MotdFrom("\"\"", 5));
	}

	[Fact]
	public void AClearInThePortalStoresAnEmptyMessageAndDeletesNothing()
	{
		(IReadOnlyDictionary<string, string?>? changes, Failure? failure) = ApplicationSettingsRules.Parse(JsonNode.Parse("{\"motd\":null}"));

		Assert.Null(failure);
		Assert.Equal("", changes!["motd"]);
		Assert.Equal("\"\"", JsonSerializer.Serialize(changes["motd"]));
	}

	[Fact]
	public void AnEmptyStoredMessageStillCountsAsStored()
	{
		Assert.True(ConfigSources.TryReadMotdSetting("\"\"", out string? motd));
		Assert.Equal("", motd);
	}

	[Fact]
	public void TheSettingsAnswerPrefillsTheStoredMessage()
	{
		string stored = MotdSeed.StoredValue("Thank you {0}");

		string body = JsonSerializer.Serialize(new ApplicationSettingsResponse { Motd = ApplicationSettingsController.ReadString("motd", stored) }, ApiResults.Json);

		Assert.Equal("{\"motd\":\"Thank you {0}\"}", body);
		Assert.Equal("{\"motd\":\"\"}", JsonSerializer.Serialize(new ApplicationSettingsResponse { Motd = ApplicationSettingsController.ReadString("motd", "\"\"") }, ApiResults.Json));
	}

	[Fact]
	public void TheMotdUpdateCarriesTheFormattedText()
	{
		MotdUpdateEvent update = MotdUpdates.Build("Hi, {0} online", 7);

		Assert.Equal("Hi, 7 online", update.Motd);
		Assert.Equal("{\"motd\":\"Hi, 7 online\"}", JsonSerializer.Serialize(update, RealtimeJson.Create()));
	}

	[Fact]
	public void TheMotdUpdateOmitsTheTextWhenTheMessageIsEmpty()
	{
		Assert.Equal("{}", JsonSerializer.Serialize(MotdUpdates.Build("", 7), RealtimeJson.Create()));
		Assert.Equal("{}", JsonSerializer.Serialize(MotdUpdates.Build(null, 7), RealtimeJson.Create()));
	}

	[Fact]
	public void TheConfigAnswerKeepsItsShape()
	{
		string body = JsonSerializer.Serialize(new ConfigResponse { ServiceConfig = JsonDocument.Parse("{}").RootElement, Motd = "m" }, ApiResults.Json);

		Assert.Equal("{\"service_config\":{},\"motd\":\"m\"}", body);
	}

	[Fact]
	public void TheMotdEventAndResourceExist()
	{
		Assert.NotNull(typeof(IRealtimeClient).GetMethod("MotdUpdate"));
		Assert.Equal("motd", CatalogResources.Motd);
	}
}
