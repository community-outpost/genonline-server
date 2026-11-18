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

using GenOnlineService.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using Xunit;

namespace GenOnlineService.Tests;

[Collection(AdminAreaCollection.Name)]
public class AgreementsTests
{
	internal const string RulesUrl = "https://example.org/rules";
	internal const string TermsUrl = "https://example.org/terms";
	internal const string PrivacyUrl = "https://example.org/privacy";

	internal static (string Key, string Value)[] Configured(string rules = "2026-11", string terms = "1") =>
	[
		("Agreements:rules:version", rules),
		("Agreements:rules:date", "2026-11-18"),
		("Agreements:rules:url", RulesUrl),
		("Agreements:terms:version", terms),
		("Agreements:terms:date", "2026-11-01"),
		("Agreements:terms:url", TermsUrl),
		("Agreements:privacy:url", PrivacyUrl)
	];

	private static AgreementsSettings Read(params (string Key, string Value)[] values)
		=> AccountsSettings.Read(new ConfigurationBuilder().AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build()).Agreements;

	[Fact]
	public void ADocumentWithoutAVersionIsNotRequired()
	{
		AgreementsSettings settings = Read(("Agreements:terms:version", "1"), ("Agreements:terms:url", TermsUrl));
		Assert.Equal(new[] { AgreementsSettings.Terms }, settings.Required.Select(d => d.Name));
	}

	private static async Task<(AdminBrowser Browser, User User)> PlayerAsync(ProbeDatabase probe, string name, params (string Key, string Value)[] settings)
	{
		var browser = new AdminBrowser(probe, settings);
		string email = name.ToLowerInvariant() + "@example.org";
		await browser.SignUp(name, email);
		User user = (await PasswordPagesTests.UserByEmail(probe, email))!;
		await AgreementGateTests.Owing(probe, user.ID);
		return (browser, user);
	}

	private static async Task<(AdminBrowser Browser, User User)> StaffAsync(ProbeDatabase probe, string name, params (string Key, string Value)[] settings)
	{
		(AdminBrowser browser, User user) = await PlayerAsync(probe, name, settings);
		await AdminArea.Grant(probe, user.Email!, StaffRoles.Moderator);
		return (browser, user);
	}

}
