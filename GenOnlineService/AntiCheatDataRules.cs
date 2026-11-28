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

namespace GenOnlineService
{
	// The accepted values live in the deployment config, so the public repository does not show which data staff accept
	public static class AntiCheatDataRules
	{
		private static readonly ILogger s_log = AppLog.For(typeof(AntiCheatDataRules));

		public const string ConfigSection = "AntiCheat:KnownAudioCrcs";
		public const int MaxReasonLength = 256;
		private const int MaxArchiveNameLength = 64;

		private static volatile IReadOnlyDictionary<string, IReadOnlySet<uint>> s_known = new Dictionary<string, IReadOnlySet<uint>>(StringComparer.Ordinal);

		// AntiCheat:KnownAudioCrcs:<game> is a list of decimal or 0x hex values
		public static IReadOnlyDictionary<string, IReadOnlySet<uint>> Parse(IConfiguration config, List<string> errors)
		{
			Dictionary<string, IReadOnlySet<uint>> result = new(StringComparer.Ordinal);
			foreach (IConfigurationSection game in config.GetSection(ConfigSection).GetChildren())
			{
				if (game.Key == ClientGames.None || !ClientGames.All.Contains(game.Key))
				{
					errors.Add($"{ConfigSection}:{game.Key} names no known game.");
					continue;
				}

				HashSet<uint> values = new();
				foreach (IConfigurationSection entry in game.GetChildren())
				{
					string text = (entry.Value ?? String.Empty).Trim();
					bool hex = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
					if (!UInt32.TryParse(hex ? text[2..] : text, hex ? NumberStyles.AllowHexSpecifier : NumberStyles.None, CultureInfo.InvariantCulture, out uint value))
					{
						errors.Add($"{ConfigSection}:{game.Key}:{entry.Key} is not a decimal or 0x hex value.");
						continue;
					}
					values.Add(value);
				}
				result[game.Key] = values;
			}
			return result;
		}

		public static bool Configure(IConfiguration config)
		{
			List<string> errors = new();
			IReadOnlyDictionary<string, IReadOnlySet<uint>> known = Parse(config, errors);
			if (errors.Count > 0)
			{
				foreach (string error in errors)
				{
					s_log.LogCritical("{Problem}", error);
				}
				return false;
			}

			s_known = known;
			return true;
		}

		public static string? Finding(string game, uint? audioCrc, IReadOnlyList<string>? archives) => Finding(s_known, game, audioCrc, archives);

		// A game without accepted values is not checked yet, so an empty config raises no findings
		public static string? Finding(IReadOnlyDictionary<string, IReadOnlySet<uint>> known, string game, uint? audioCrc, IReadOnlyList<string>? archives)
		{
			if (audioCrc == null || !known.TryGetValue(game, out IReadOnlySet<uint>? values) || values.Count == 0 || values.Contains(audioCrc.Value))
			{
				return null;
			}

			string reason = $"Unknown audio rules 0x{audioCrc.Value:X8}";
			List<string> names = (archives ?? Array.Empty<string>())
				.Select(a => TextSanitizer.Clean(a?.Trim(), MaxArchiveNameLength))
				.Where(a => a.Length > 0)
				.ToList();
			if (names.Count == 0)
			{
				return reason;
			}

			reason += ", game folder archives: " + String.Join(", ", names);
			return reason.Length <= MaxReasonLength ? reason : reason[..(MaxReasonLength - 3)] + "...";
		}
	}
}
