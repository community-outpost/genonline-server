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

using System.Text.Json;
using GenOnlineService.Portal.Client;
using Xunit;

namespace GenOnlineService.Tests
{
	public class PortalWordsTests
	{
		[Theory]
		[InlineData("PlayerState", "blocked", "Blocked")]
		[InlineData("SessionKind", "launcher", "Launcher")]
		[InlineData("SanctionType", "mute", "Mute")]
		[InlineData("CaseMethod", "UpdateSlots", "Change several slots")]
		[InlineData("SlotState", "AiEasy", "Easy AI")]
		[InlineData("SlotState", "ai_brutal", "Brutal AI")]
		[InlineData("FileType", "Replay", "Replay")]
		[InlineData("Transport", "relay", "Relay")]
		[InlineData("ConnectionOutcome", "connected_direct", "Direct")]
		[InlineData("ConnectionOutcome", "failed", "Failed")]
		[InlineData("ConnectionState", "finding_route", "Finding a route")]
		[InlineData("RuleMatchReview", "pending", "Waiting for review")]
		[InlineData("TextOutcome", "mask", "Filtered for players who turn on the chat filter")]
		[InlineData("TextReading", "folded_alt", "Folded letters, other spelling")]
		[InlineData("TextSeverity", "block", "Block")]
		[InlineData("TextLanguage", "de", "German")]
		[InlineData("TextCategory", "slur", "Slur")]
		[InlineData("AuditSource", "startup_sweep", "Start-up sweep")]
		[InlineData("RuleTrigger", "sign_in", "Sign-in")]
		public void KnownCodesGetPlainLabels(string method, string code, string expected)
		{
			Assert.Equal(expected, Call(method, code));
		}

		[Theory]
		[InlineData("PlayerState")]
		[InlineData("SessionKind")]
		[InlineData("SanctionType")]
		[InlineData("CaseMethod")]
		[InlineData("SlotState")]
		[InlineData("FileType")]
		[InlineData("Transport")]
		[InlineData("ConnectionOutcome")]
		[InlineData("ConnectionState")]
		[InlineData("RuleMatchReview")]
		[InlineData("TextOutcome")]
		[InlineData("TextReading")]
		[InlineData("TextSeverity")]
		[InlineData("TextLanguage")]
		[InlineData("TextCategory")]
		[InlineData("AuditSource")]
		public void AnUnknownCodeFallsBackToItself(string method)
		{
			Assert.Equal("brand_new_code", Call(method, "brand_new_code"));
		}

		[Fact]
		public void ARequestStateKeepsItsLobbyKind()
		{
			Assert.Equal("Setting up, quick match", PortalWords.RequestState("GAME_SETUP, quick match"));
			Assert.Equal("Odd, custom game", PortalWords.RequestState("Odd, custom game"));
		}

		[Fact]
		public void ACaseRuleLineUsesTheRuleLabels()
		{
			Assert.Equal("Flag for review, Ban", PortalWords.RuleActions("flag, ban"));
		}

		[Fact]
		public void AColourShowsSixDigits()
		{
			Assert.Equal("#FF7F2F", PortalWords.ColourHex("ffff7f2f"));
			Assert.Equal("#FF0000 at 50% opacity", PortalWords.ColourHex("80ff0000"));
			Assert.Equal("zz", PortalWords.ColourHex("zz"));
		}

		[Fact]
		public void AuditDetailsUseLabelsAndFallBackToTheKey()
		{
			using JsonDocument doc = JsonDocument.Parse("{\"client_id\":\"launcher\",\"first_party\":true,\"odd_key\":[1,2]}");
			var rows = PortalWords.AuditDetails(doc.RootElement.Clone());
			Assert.Equal([("Client", "launcher"), ("First party", "yes"), ("Odd key", "1, 2")], rows);
		}

		[Fact]
		public void TheUnknownErrorFallbackIsAPlainSentence()
		{
			string text = PortalText.Error("brand_new_code");
			Assert.StartsWith("Something went wrong.", text);
			Assert.Contains("brand_new_code", text);
		}

		private static string Call(string method, string code) => (string)typeof(PortalWords).GetMethod(method)!.Invoke(null, [code])!;
	}
}
