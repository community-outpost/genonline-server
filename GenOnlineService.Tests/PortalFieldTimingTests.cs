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
	// A BlazorBlueprint field updates its bound value on change (blur) unless UpdateTiming says otherwise, and @bind-Value:event does not change that
	public class PortalFieldTimingTests
	{
		private static string PortalPages()
		{
			DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory);
			while (dir != null && !File.Exists(Path.Combine(dir.FullName, "GenServices.sln")))
			{
				dir = dir.Parent;
			}
			Assert.NotNull(dir);
			return Path.Combine(dir!.FullName, "GenOnlineService.Portal.Client");
		}

		[Fact]
		public void NoPortalFieldUsesTheElementBindEvent()
		{
			string[] offenders = Directory.EnumerateFiles(PortalPages(), "*.razor", SearchOption.AllDirectories)
				.Where(path => !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
				.SelectMany(path => File.ReadLines(path).Select((line, index) => (path, line, index)))
				.Where(x => Regex.IsMatch(x.line, @"<Bb\w+[^>]*@bind-Value:event"))
				.Select(x => $"{Path.GetFileName(x.path)}:{x.index + 1}")
				.ToArray();
			Assert.Empty(offenders);
		}

		[Theory]
		[InlineData("Pages/Player.razor", "Id=\"reason\"")]
		[InlineData("Pages/Case.razor", "Id=\"case-reason\"")]
		[InlineData("Pages/Case.razor", "Id=\"case-note\"")]
		public void AReasonThatGatesConfirmUpdatesWhileTyping(string page, string field)
		{
			string line = File.ReadLines(Path.Combine(PortalPages(), page)).Single(l => l.Contains(field));
			Assert.Contains("UpdateTiming=\"UpdateTiming.Immediate\"", line);
		}
	}
}
