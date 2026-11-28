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

using GenOnlineService.Portal.Client;
using GenOnlineService.Portal.Client.Layout;
using Xunit;

namespace GenOnlineService.Tests
{
	public class PortalCaseFlowTests
	{
		[Fact]
		public void TheQueueFiltersTravelToTheCasePage()
		{
			Assert.Equal("/admin/players/7/case?kind=report&claimed=unclaimed", PortalRoutes.PlayerCasePage(7, "report", "unclaimed"));
			Assert.Equal("/admin/players/7/case", PortalRoutes.PlayerCasePage(7, "", "all"));
			Assert.Equal("/admin/cases?claimed=mine", PortalRoutes.CasesQueue(null, "mine"));
		}

		[Theory]
		[InlineData("mine", "mine")]
		[InlineData("all", "unclaimed")]
		[InlineData(null, "unclaimed")]
		public void TheNextCaseIsOneNobodyHoldsUnlessTheQueueShowsMine(string? claimed, string expected)
		{
			Assert.Equal(expected, PortalRoutes.NextClaimed(claimed));
		}

		[Fact]
		public void OnlyABanNeedsAConfirmationAndAReason()
		{
			Assert.True(SanctionDecision.NeedsConfirm("ban"));
			Assert.False(SanctionDecision.NeedsConfirm("mute"));
			Assert.True(SanctionDecision.Blocked("ban", " "));
			Assert.False(SanctionDecision.Blocked("mute", null));
			Assert.Equal("", SanctionDecision.DefaultDuration("kick"));
		}
	}
}
