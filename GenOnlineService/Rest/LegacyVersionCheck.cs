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

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace GenOnlineService.Rest
{
	// LegacyClients in appsettings.json: the launcher installer an old client downloads and runs as its patcher
	public sealed class LegacyClientSettings
	{
		public string InstallerName { get; init; } = String.Empty;
		public string InstallerUrl { get; init; } = String.Empty;
		public long InstallerSize { get; init; }

		// the old client refuses a name with a path in it, and runs the file only when its size matches exactly
		public bool Configured => InstallerName.Length > 0 && InstallerName.IndexOfAny(['/', '\\', ':']) < 0 && !InstallerName.Contains("..", StringComparison.Ordinal)
			&& Uri.TryCreate(InstallerUrl, UriKind.Absolute, out Uri? url) && url.Scheme == Uri.UriSchemeHttps && InstallerSize > 0;

		public static LegacyClientSettings Read(IConfiguration config)
		{
			IConfigurationSection section = config.GetSection("LegacyClients");
			return new LegacyClientSettings
			{
				InstallerName = section.GetValue<string>("installer_name")?.Trim() ?? String.Empty,
				InstallerUrl = section.GetValue<string>("installer_url")?.Trim() ?? String.Empty,
				InstallerSize = section.GetValue<long?>("installer_size") ?? 0
			};
		}
	}

	// The answer of the old version check, in the old client's own shape: result 2 asks it to download and run the patcher
	public sealed class LegacyVersionCheckAnswer
	{
		public const int Failed = 1;
		public const int NeedsUpdate = 2;

		public int result { get; init; } = Failed;
		public string patcher_name { get; init; } = String.Empty;
		public string patcher_path { get; init; } = String.Empty;
		public long patcher_size { get; init; }

		// every old client is outdated: without a configured installer it only learns that the check failed
		public static LegacyVersionCheckAnswer For(LegacyClientSettings settings) => settings.Configured
			? new LegacyVersionCheckAnswer { result = NeedsUpdate, patcher_name = settings.InstallerName, patcher_path = settings.InstallerUrl, patcher_size = settings.InstallerSize }
			: new LegacyVersionCheckAnswer();
	}

	// Clients from before /api/v1 start with this check; the only old route this service answers, for any environment
	[ApiController]
	[AllowAnonymous]
	[ApiExplorerSettings(IgnoreApi = true)]
	public sealed class LegacyVersionCheckController : ControllerBase
	{
		private readonly LegacyClientSettings m_settings;

		public LegacyVersionCheckController(IConfiguration config)
		{
			m_settings = LegacyClientSettings.Read(config);
		}

		[HttpPost("~/env/{environment}/contract/1/VersionCheck")]
		public IActionResult Post(string environment)
		{
			// the old client reads plain System.Text.Json names and numbers, not the /api/v1 conventions
			return Content(JsonSerializer.Serialize(LegacyVersionCheckAnswer.For(m_settings)), "application/json");
		}
	}
}
