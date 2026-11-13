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

using GenOnlineService;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Database
{
	public static class Upstream
	{
		public static async Task<string?> GetSetting(AppDbContext db, string key)
		{
			return await db.ApplicationSettings.AsNoTracking().Where(s => s.Key == key).Select(s => s.Value).FirstOrDefaultAsync();
		}

		public static async Task SetSetting(AppDbContext db, string key, string jsonValue, string updatedBy)
		{
			ApplicationSetting? row = await db.ApplicationSettings.AsTracking().FirstOrDefaultAsync(s => s.Key == key);
			if (row == null)
			{
				db.ApplicationSettings.Add(new ApplicationSetting { Key = key, Value = jsonValue, UpdatedBy = updatedBy, UpdatedAt = DateTime.UtcNow });
			}
			else
			{
				row.Value = jsonValue;
				row.UpdatedBy = updatedBy;
				row.UpdatedAt = DateTime.UtcNow;
			}

			await db.SaveChangesAsync();
		}
	}
}
