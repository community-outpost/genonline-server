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
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.Json;

namespace GenOnlineService.Rest
{
	[ApiFixture("get-config.response")]
	public sealed record ConfigResponse
	{
		public JsonElement ServiceConfig { get; init; }
		public string? Motd { get; init; }
	}

	public static class ConfigSources
	{
		private static readonly ILogger s_log = AppLog.For(typeof(ConfigSources));

		// {0} is the number of players online
		public static async Task<string?> ReadMotdAsync(IDbContextFactory<AppDbContext> dbFactory)
		{
			string? stored;
			await using (AppDbContext db = await dbFactory.CreateDbContextAsync())
			{
				stored = await Database.Upstream.GetSetting(db, SettingKeys.Motd);
			}

			if (stored != null && !TryReadMotdSetting(stored, out _))
			{
				s_log.LogWarning("The motd setting is not a JSON string, no message of the day is sent");
			}

			return MotdFrom(stored, WebSocketManager.GetNumberOfUsersOnline());
		}

		public static string? MotdFrom(string? stored, int playersOnline) =>
			TryReadMotdSetting(stored, out string? template) && template != null ? FormatMotd(template, playersOnline) : null;

		// A setting is used when it holds a JSON string, and no row or another JSON value means no message
		public static bool TryReadMotdSetting(string? stored, out string? motd)
		{
			motd = null;
			if (stored == null)
			{
				return false;
			}

			try
			{
				using JsonDocument document = JsonDocument.Parse(stored);
				if (document.RootElement.ValueKind != JsonValueKind.String)
				{
					return false;
				}
				motd = document.RootElement.GetString();
				return true;
			}
			catch (JsonException)
			{
				return false;
			}
		}

		// An invalid format string is sent as written; an empty message is null
		public static string? FormatMotd(string template, int playersOnline)
		{
			if (template.Length == 0)
			{
				return null;
			}

			try
			{
				return String.Format(CultureInfo.InvariantCulture, template, playersOnline);
			}
			catch (FormatException)
			{
				return template;
			}
		}
	}
}
