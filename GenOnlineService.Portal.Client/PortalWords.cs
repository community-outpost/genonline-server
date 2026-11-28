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

using System.Globalization;
using System.Text.Json;

namespace GenOnlineService.Portal.Client
{
	// Plain labels for the codes the service sends, where an unknown code answers itself and never an empty text
	public static class PortalWords
	{
		// Wire names differ in case and separators between the REST and realtime layers
		private static string Key(string? code) => (code ?? "").Replace("_", "").Replace("-", "").Replace(" ", "").ToLowerInvariant();

		public static string Sentence(string? code)
		{
			string text = (code ?? "").Replace('_', ' ');
			return text.Length == 0 ? "" : char.ToUpperInvariant(text[0]) + text[1..];
		}

		public static string PlayerState(string? state) => Key(state) switch
		{
			"active" => "Active",
			"blocked" => "Blocked",
			"deactivated" => "Deactivated",
			_ => state ?? ""
		};

		public static string SessionKind(string? kind) => Key(kind) switch
		{
			"game" => "Game",
			"chat" => "Chat",
			"launcher" => "Launcher",
			_ => kind ?? ""
		};

		public static string SanctionType(string? type) => Key(type) switch
		{
			"ban" => "Ban",
			"mute" => "Mute",
			"kick" => "Kick",
			_ => type ?? ""
		};

		public static string CaseMethod(string? method) => Key(method) switch
		{
			"updatelobby" => "Change the lobby settings",
			"setlobbypassword" => "Set the lobby password",
			"updatemyslot" => "Change their own slot",
			"sethasmap" => "Report map ownership",
			"setready" => "Set ready",
			"setslotstate" => "Change a slot",
			"updateslots" => "Change several slots",
			"forceready" => "Force ready",
			"startgame" => "Start the game",
			"beginmeshcheck" => "Start the connection check",
			_ => method ?? ""
		};

		// A request state reads "<lobby state>, quick match" or "<lobby state>, custom game"
		public static string RequestState(string? state)
		{
			if (String.IsNullOrEmpty(state))
			{
				return "";
			}
			int comma = state.IndexOf(", ", StringComparison.Ordinal);
			string stage = comma < 0 ? state : state[..comma];
			string label = Key(stage) switch
			{
				"gamesetup" => "Setting up",
				"countdown" => "Counting down",
				"ingame" => "In game",
				"complete" => "Complete",
				_ => stage
			};
			return comma < 0 ? label : label + state[comma..];
		}

		public static string RuleTrigger(string? trigger) => PortalRuleWords.TriggerLabel(trigger);

		public static string RuleOutcome(string? outcome) => PortalRuleWords.OutcomeLabel(outcome);

		public static string RuleActions(string? actions) => String.Join(", ", (actions ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(PortalRuleWords.ActionLabel));

		public static string RosterType(string? roster) => Sentence(roster);

		public static string EacKind(string? kind) => Key(kind) switch
		{
			"actionrequired" => "Action required",
			"integrityviolation" => "Integrity violation",
			_ => kind ?? ""
		};

		// The values of EOS_EAntiCheatCommonClientAction in the EOS SDK
		public static string EacAction(int action) => action switch
		{
			0 => "Invalid",
			1 => "Remove player",
			_ => "Code " + action.ToString(CultureInfo.InvariantCulture)
		};

		// The values of EOS_EAntiCheatCommonClientActionReason in the EOS SDK
		public static string EacReason(int reason) => reason switch
		{
			0 => "Invalid",
			1 => "Internal error",
			2 => "Invalid message",
			3 => "Authentication failed",
			4 => "Null client",
			5 => "Heartbeat timeout",
			6 => "Client violation",
			7 => "Backend violation",
			8 => "Temporary cooldown",
			9 => "Temporary ban",
			10 => "Permanent ban",
			_ => "Code " + reason.ToString(CultureInfo.InvariantCulture)
		};

		// The values of EOS_EAntiCheatClientViolationType in the EOS SDK
		public static string EacViolation(int violation) => violation switch
		{
			0 => "Invalid",
			1 => "Integrity catalog not found",
			2 => "Integrity catalog error",
			3 => "Integrity catalog certificate revoked",
			4 => "Integrity catalog misses the main executable",
			5 => "Game file mismatch",
			6 => "Required game file not found",
			7 => "Unknown game file forbidden",
			8 => "System file untrusted",
			9 => "Forbidden module loaded",
			10 => "Corrupted memory",
			11 => "Forbidden tool detected",
			12 => "Internal anti-cheat violation",
			13 => "Corrupted network message flow",
			14 => "Virtual machine not allowed",
			15 => "Forbidden system configuration",
			_ => "Code " + violation.ToString(CultureInfo.InvariantCulture)
		};

		public static string SlotState(string? state) => Key(state) switch
		{
			"open" => "Open",
			"closed" => "Closed",
			"player" => "Player",
			"aieasy" => "Easy AI",
			"aimedium" => "Medium AI",
			"aibrutal" => "Brutal AI",
			_ => state ?? ""
		};

		public static string FileType(string? type) => Key(type) switch
		{
			"screenshot" => "Screenshot",
			"replay" => "Replay",
			"stats" => "Statistics",
			"unknown" => "Other file",
			_ => type ?? ""
		};

		public static string Transport(string? transport) => Key(transport) switch
		{
			"direct" => "Direct",
			"relay" => "Relay",
			_ => transport ?? ""
		};

		public static string ConnectionState(string? state) => Key(state) switch
		{
			"connecting" => "Connecting",
			"findingroute" => "Finding a route",
			"connecteddirect" => "Connected directly",
			"connectedrelay" => "Connected through the relay",
			"failed" => "Failed",
			"disconnected" => "Disconnected",
			_ => state ?? ""
		};

		public static string ConnectionOutcome(string? outcome) => Key(outcome) switch
		{
			"connecteddirect" => "Direct",
			"connectedrelay" => "Relay",
			_ => ConnectionState(outcome)
		};

		public static string RuleMatchReview(string? state) => Key(state) switch
		{
			"pending" => "Waiting for review",
			"reviewed" => "Reviewed",
			"dismissed" => "Dismissed",
			"none" => "Not queued",
			"all" => "All",
			_ => state ?? ""
		};

		public static string TextOutcome(string? outcome) => Key(outcome) switch
		{
			"allow" => "Allowed",
			"refuse" => "Refused",
			"block" => "Blocked",
			"mask" => "Filtered for players who turn on the chat filter",
			_ => outcome ?? ""
		};

		public static string TextReading(string? reading) => Key(reading) switch
		{
			"folded" => "Folded letters",
			"foldedalt" => "Folded letters, other spelling",
			"arabizi" => "Arabic in Latin letters",
			"literal" => "Exact text",
			_ => reading ?? ""
		};

		public static string TextSeverity(string? severity) => Key(severity) switch
		{
			"block" => "Block",
			"flag" => "Flag",
			_ => severity ?? ""
		};

		public static string TextLanguage(string? language) => PortalTextChoices.Languages.FirstOrDefault(l => l.Value == language).Title ?? language ?? "";

		public static string TextCategory(string? category) => Key(category) switch
		{
			"slur" => "Slur",
			"sexual" => "Sexual",
			"profanity" => "Profanity",
			"insult" => "Insult",
			"impersonation" => "Impersonation",
			"reserved" => "Reserved name",
			_ => category ?? ""
		};

		public static string AuditSource(string? source) => Key(source) switch
		{
			"web" => "Web",
			"api" => "API",
			"cli" => "Command line",
			"portal" => "Staff portal",
			"configseed" => "Configuration seed",
			"startupsweep" => "Start-up sweep",
			_ => source ?? ""
		};

		public static string AuditDetailKey(string? key) => Key(key) switch
		{
			"clientid" => "Client",
			"firstparty" => "First party",
			"signinsrevoked" => "Sign-ins ended",
			"tokensrevoked" => "Tokens revoked",
			"authorizations" => "Authorizations ended",
			"fromversion" => "From version",
			"toversion" => "To version",
			"expiresat" => "Ends",
			"seconds" => "Seconds",
			"sanctionid" => "Sanction",
			_ => Sentence(key)
		};

		// One entry per top level field, with the value as plain text
		public static IReadOnlyList<(string Label, string Value)> AuditDetails(JsonElement? details, Func<string, JsonElement, bool>? keep = null)
		{
			if (details is not JsonElement element || element.ValueKind != JsonValueKind.Object)
			{
				return [];
			}
			return element.EnumerateObject().Where(p => keep == null || keep(p.Name, p.Value)).Select(p => (AuditDetailKey(p.Name), DetailValue(p.Value))).ToList();
		}

		private static string DetailValue(JsonElement value) => value.ValueKind switch
		{
			JsonValueKind.String => value.GetString() ?? "",
			JsonValueKind.True => "yes",
			JsonValueKind.False => "no",
			JsonValueKind.Null => "none",
			JsonValueKind.Array => String.Join(", ", value.EnumerateArray().Select(DetailValue)),
			_ => value.GetRawText()
		};

		// A stored colour is alpha, red, green and blue as eight hexadecimal digits
		public static string ColourHex(string? code)
		{
			if (code is not { Length: 8 })
			{
				return code ?? "";
			}
			string rgb = "#" + code[2..].ToUpperInvariant();
			return code.StartsWith("ff", StringComparison.OrdinalIgnoreCase) || !Int32.TryParse(code[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int alpha) ? rgb : rgb + " at " + (alpha * 100 / 255).ToString(CultureInfo.InvariantCulture) + "% opacity";
		}
	}
}
