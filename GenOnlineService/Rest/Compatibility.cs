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
using GenOnlineService.Identity;
using GenOnlineService.Realtime;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using RegexGroup = System.Text.RegularExpressions.Group;
using RegexMatch = System.Text.RegularExpressions.Match;

namespace GenOnlineService.Rest
{
	[ApiFixture("get-compatibility.response")]
	public sealed record CompatibilityResponse
	{
		public bool Compatible { get; init; }
		public string? MinVersion { get; init; }
		public string ServiceVersion { get; init; } = "";
	}

	public static class Compatibility
	{
		private static readonly Regex s_version = new(@"^(\d{4})\.(\d{1,2})\.(\d{1,9})\z", RegexOptions.CultureInvariant);

		// The registry stores min_version as three numbers of up to 9 digits
		private static readonly Regex s_minVersion = new(@"^(\d{1,9})\.(\d{1,9})\.(\d{1,9})\z", RegexOptions.CultureInvariant);

		// Informational version stamped into the build from the git tag (GoResolveVersion in the csproj)
		public static string ServiceVersion { get; } = typeof(Compatibility).Assembly
			.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";

		public static Task<Result<CompatibilityResponse>> CheckAsync(string? clientID, string? version)
		{
			return EvaluateAsync(clientID, version, id => ClientRegistry.FindEnabled(id));
		}

		// Arguments first, then the client
		public static Task<Result<CompatibilityResponse>> EvaluateAsync(string? clientID, string? version, Func<string, RegisteredClient?> findEnabled)
		{
			if (String.IsNullOrEmpty(clientID))
			{
				return Task.FromResult<Result<CompatibilityResponse>>(Result.Invalid("client"));
			}

			if (!TryParseVersion(version, out (int, int, int) parsed))
			{
				return Task.FromResult<Result<CompatibilityResponse>>(Result.Invalid("version"));
			}

			RegisteredClient? client = findEnabled(clientID);
			if (client == null)
			{
				return Task.FromResult<Result<CompatibilityResponse>>(Result.NotFound("client"));
			}

			return Task.FromResult<Result<CompatibilityResponse>>(Result.Success(new CompatibilityResponse
			{
				Compatible = IsCompatible(parsed, client.MinVersion),
				MinVersion = client.MinVersion,
				ServiceVersion = ServiceVersion
			}));
		}

		public static bool TryParseVersion(string? version, out (int Year, int Month, int Patch) parsed)
		{
			parsed = default;
			RegexMatch match = version == null ? RegexMatch.Empty : s_version.Match(version);
			if (!match.Success)
			{
				return false;
			}

			int month = Number(match.Groups[2]);
			if (month < 1 || month > 12)
			{
				return false;
			}

			parsed = (Number(match.Groups[1]), month, Number(match.Groups[3]));
			return true;
		}

		// A client without a usable min_version accepts every version
		public static bool IsCompatible((int Year, int Month, int Patch) version, string? minVersion)
		{
			RegexMatch match = minVersion == null ? RegexMatch.Empty : s_minVersion.Match(minVersion);
			if (!match.Success)
			{
				return true;
			}

			(int, int, int) minimum = (Number(match.Groups[1]), Number(match.Groups[2]), Number(match.Groups[3]));
			return version.CompareTo(minimum) >= 0;
		}

		private static int Number(RegexGroup group) => int.Parse(group.Value, NumberStyles.None, CultureInfo.InvariantCulture);

	}
}
