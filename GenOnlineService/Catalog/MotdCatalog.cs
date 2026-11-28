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

using GenOnlineService.Realtime;
using GenOnlineService.Rest;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace GenOnlineService.Catalog
{
	public static partial class CatalogStartup
	{
		static partial void RegisterMotd() => CatalogSignals.Register(CatalogResources.Motd, MotdUpdates.ReloadAsync);

		static partial void AddMotdSeed(List<Func<AppDbContext, Task>> seeds) => seeds.Add(MotdSeed.EnsureAsync);
	}

	public static class MotdSeed
	{
		public const string Actor = "system";

		public static string StoredValue(string fileText) => JsonSerializer.Serialize(fileText);

		// data/motd.txt is only the import source of the first start, nothing reads it afterwards
		public static async Task EnsureAsync(AppDbContext db)
		{
			if (await Database.Upstream.GetSetting(db, SettingKeys.Motd) != null)
			{
				return;
			}

			string path = Path.Combine(CatalogStartup.DataDirectory, "motd.txt");
			if (!File.Exists(path))
			{
				return;
			}

			db.ApplicationSettings.Add(new ApplicationSetting { Key = SettingKeys.Motd, Value = StoredValue(await File.ReadAllTextAsync(path)), UpdatedBy = Actor, UpdatedAt = DateTime.UtcNow });
			try
			{
				await db.SaveChangesAsync();
			}
			catch (DbUpdateException ex) when (DbHelpers.IsUniqueViolation(ex))
			{
				// another copy imported at the same moment
				db.ChangeTracker.Clear();
			}
		}
	}

	public static class MotdUpdates
	{
		private static readonly ILogger s_log = AppLog.For(typeof(MotdUpdates));
		private static readonly object s_lock = new();

		private static bool s_loaded;
		private static string? s_template;

		public static MotdUpdateEvent Build(string? template, int playersOnline) =>
			new() { Motd = template == null ? null : ConfigSources.FormatMotd(template, playersOnline) };

		// Every copy reloads on the catalog signal and sends only to its own connections
		public static async Task ReloadAsync(AppDbContext db)
		{
			ConfigSources.TryReadMotdSetting(await Database.Upstream.GetSetting(db, SettingKeys.Motd), out string? template);

			bool changed;
			lock (s_lock)
			{
				changed = s_loaded && s_template != template;
				s_loaded = true;
				s_template = template;
			}

			if (changed)
			{
				Publish(template);
			}
		}

		private static void Publish(string? template)
		{
			try
			{
				MotdUpdateEvent update = Build(template, WebSocketManager.GetNumberOfUsersOnline());
				foreach (UserSession session in RealtimeAudience.AllSessions())
				{
					if (RealtimeAudience.ConnectionOf(session) != null)
					{
						RealtimeOut.Fire(RealtimeOut.Session(session).MotdUpdate(update));
					}
				}
			}
			catch (Exception ex)
			{
				s_log.LogWarning(ex, "MotdUpdate failed");
			}
		}
	}
}
