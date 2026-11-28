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

namespace GenOnlineService.Catalog
{
	public static partial class CatalogStartup
	{
		public static string DataDirectory { get; set; } = "data";

		public static async Task RunAsync(AppDbContext db)
		{
			RegisterMotd();
			RegisterPlaylists();
			RegisterChannels();
			RegisterBroadcasts();

			var seeds = new List<Func<AppDbContext, Task>>();
			AddMotdSeed(seeds);
			AddPlaylistSeed(seeds);
			AddChannelSeed(seeds);
			AddBroadcastSeed(seeds);
			foreach (Func<AppDbContext, Task> seed in seeds)
			{
				await seed(db);
			}

			await CatalogSignals.ReloadAllAsync(db);
		}

		static partial void RegisterMotd();
		static partial void AddMotdSeed(List<Func<AppDbContext, Task>> seeds);
		static partial void RegisterPlaylists();
		static partial void RegisterChannels();
		static partial void RegisterBroadcasts();
		static partial void AddPlaylistSeed(List<Func<AppDbContext, Task>> seeds);
		static partial void AddChannelSeed(List<Func<AppDbContext, Task>> seeds);
		static partial void AddBroadcastSeed(List<Func<AppDbContext, Task>> seeds);
	}
}
