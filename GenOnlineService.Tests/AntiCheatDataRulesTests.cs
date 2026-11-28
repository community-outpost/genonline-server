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

using Microsoft.Extensions.Configuration;
using Xunit;

namespace GenOnlineService.Tests;

public class AntiCheatDataRulesTests
{
	private static IReadOnlyDictionary<string, IReadOnlySet<uint>> Parse(List<string> errors, params (string Key, string Value)[] values)
		=> AntiCheatDataRules.Parse(new ConfigurationBuilder().AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build(), errors);

	private static IReadOnlyDictionary<string, IReadOnlySet<uint>> Known()
	{
		List<string> errors = new();
		IReadOnlyDictionary<string, IReadOnlySet<uint>> known = Parse(errors,
			("AntiCheat:KnownAudioCrcs:zerohour:0", "0x1A2B3C4D"),
			("AntiCheat:KnownAudioCrcs:zerohour:1", "305419896"));
		Assert.Empty(errors);
		return known;
	}

	[Fact]
	public void HexAndDecimalValuesAreKnown()
	{
		IReadOnlyDictionary<string, IReadOnlySet<uint>> known = Known();
		Assert.Null(AntiCheatDataRules.Finding(known, ClientGames.ZeroHour, 0x1A2B3C4Du, null));
		Assert.Null(AntiCheatDataRules.Finding(known, ClientGames.ZeroHour, 305419896u, null));
	}

	[Fact]
	public void AnUnknownValueIsAFinding()
	{
		Assert.Equal("Unknown audio rules 0x00000BAD", AntiCheatDataRules.Finding(Known(), ClientGames.ZeroHour, 0xBADu, null));
	}

	[Fact]
	public void ZeroIsAFindingLikeAnyOtherValue()
	{
		Assert.Equal("Unknown audio rules 0x00000000", AntiCheatDataRules.Finding(Known(), ClientGames.ZeroHour, 0u, null));
	}

	[Fact]
	public void AGameWithoutKnownValuesIsNotChecked()
	{
		Assert.Null(AntiCheatDataRules.Finding(Known(), ClientGames.Generals, 0xBADu, null));
	}

	[Fact]
	public void AClientWithoutTheValueIsNotChecked()
	{
		Assert.Null(AntiCheatDataRules.Finding(Known(), ClientGames.ZeroHour, null, new[] { "hack.big" }));
	}

	[Fact]
	public void TheFindingNamesTheGameFolderArchives()
	{
		Assert.Equal("Unknown audio rules 0x00000BAD, game folder archives: !audio.big, zz_mod.big",
			AntiCheatDataRules.Finding(Known(), ClientGames.ZeroHour, 0xBADu, new[] { "!audio.big", " ", "zz_mod.big" }));
	}

	[Fact]
	public void TheFindingFitsTheReasonColumn()
	{
		string[] archives = Enumerable.Range(0, 100).Select(i => new string('a', 40) + i + ".big").ToArray();
		string? finding = AntiCheatDataRules.Finding(Known(), ClientGames.ZeroHour, 0xBADu, archives);
		Assert.NotNull(finding);
		Assert.True(finding!.Length <= AntiCheatDataRules.MaxReasonLength);
		Assert.StartsWith("Unknown audio rules 0x00000BAD, game folder archives: ", finding);
	}

	[Fact]
	public void AnInvalidValueIsAnError()
	{
		List<string> errors = new();
		Parse(errors, ("AntiCheat:KnownAudioCrcs:zerohour:0", "nope"));
		Assert.Single(errors);
	}

	[Fact]
	public void AnUnknownGameIsAnError()
	{
		List<string> errors = new();
		Parse(errors, ("AntiCheat:KnownAudioCrcs:ra2:0", "0x1"));
		Assert.Single(errors);
	}
}
