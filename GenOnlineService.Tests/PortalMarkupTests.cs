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

using System.Text.RegularExpressions;
using Xunit;

namespace GenOnlineService.Tests
{
	public class PortalMarkupTests
	{
		private static IEnumerable<(string File, int Line, string Text)> PortalLines()
		{
			DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory);
			while (dir != null && !File.Exists(Path.Combine(dir.FullName, "GenServices.sln")))
			{
				dir = dir.Parent;
			}
			Assert.NotNull(dir);
			foreach (string file in Directory.EnumerateFiles(Path.Combine(dir!.FullName, "GenOnlineService.Portal.Client"), "*.razor", SearchOption.AllDirectories))
			{
				string[] lines = File.ReadAllLines(file);
				for (int i = 0; i < lines.Length; i++)
				{
					yield return (Path.GetFileName(file), i + 1, lines[i]);
				}
			}
		}

		// Razor drops the whitespace in front of a code block, so text and the block run together
		[Fact]
		public void NoTextIsFollowedByASpaceAndACodeBlockOnOneLine()
		{
			var bad = PortalLines().Where(l => Regex.IsMatch(l.Text, @"[^\s>] @(if|foreach) \(")).Select(l => l.File + ":" + l.Line).ToList();
			Assert.Empty(bad);
		}

		// The hint class is drawn in the error colour, so only an alert may use it
		[Fact]
		public void TheErrorHintClassIsOnlyUsedForAlerts()
		{
			var bad = PortalLines().Where(l => l.Text.Contains("go-filterbar-hint") && !l.Text.Contains("role=\"alert\"")).Select(l => l.File + ":" + l.Line).ToList();
			Assert.Empty(bad);
		}
	}
}
