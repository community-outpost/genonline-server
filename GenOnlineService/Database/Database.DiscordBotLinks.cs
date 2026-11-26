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

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

// The Discord account the bot treats as this GenOnline account, set by staff and read only by the bot's staff commands, never a
// sign-in identity
public class DiscordBotLink
{
	public Int64 UserID { get; set; }
	public Int64 DiscordUserID { get; set; }

	// An actor label such as "cli" or "user:42"
	public string? LinkedBy { get; set; }

	public DateTime LinkedAt { get; set; } = DateTime.UnixEpoch;
}

public class DiscordBotLinkConfiguration : IEntityTypeConfiguration<DiscordBotLink>
{
	public void Configure(EntityTypeBuilder<DiscordBotLink> builder)
	{
		builder.ToTable("discord_bot_links");

		builder.HasKey(e => e.UserID);
		builder.HasIndex(e => e.DiscordUserID).IsUnique().HasDatabaseName("ux_discord_bot_links_discord_user_id");

		builder.Property(e => e.UserID).HasColumnName("user_id").ValueGeneratedNever();
		builder.Property(e => e.DiscordUserID).HasColumnName("discord_user_id");
		builder.Property(e => e.LinkedBy).HasColumnName("linked_by").HasMaxLength(64);
		builder.Property(e => e.LinkedAt).HasColumnName("linked_at");
	}
}

namespace Database
{
	public enum DiscordBotLinkOutcome
	{
		Linked,
		Unchanged,
		InUse
	}

	public static class DiscordBotLinks
	{
		// Discord ids are snowflakes, so anything else is a typo, and one above Int64.MaxValue cannot be stored
		public static bool TryParse(string? text, out Int64 discordUserID)
		{
			discordUserID = 0;
			string trimmed = text?.Trim() ?? String.Empty;
			return trimmed.Length is >= 15 and <= 20 && trimmed.All(Char.IsAsciiDigit) && Int64.TryParse(trimmed, out discordUserID) && discordUserID > 0;
		}

		public static async Task<Int64?> Get(AppDbContext db, Int64 userID)
		{
			return await db.DiscordBotLinks.AsNoTracking().Where(l => l.UserID == userID).Select(l => (Int64?)l.DiscordUserID).FirstOrDefaultAsync();
		}

		public static async Task<Int64?> UserOf(AppDbContext db, Int64 discordUserID)
		{
			return await db.DiscordBotLinks.AsNoTracking().Where(l => l.DiscordUserID == discordUserID).Select(l => (Int64?)l.UserID).FirstOrDefaultAsync();
		}

		// Replaces the account's link and refuses a Discord account another GenOnline account holds, so one Discord user never acts as two
		public static async Task<(DiscordBotLinkOutcome Outcome, Int64? HeldBy)> Set(AppDbContext db, Int64 userID, Int64 discordUserID, ModerationActor actor)
		{
			Int64? holder = await UserOf(db, discordUserID);
			if (holder == userID)
			{
				return (DiscordBotLinkOutcome.Unchanged, null);
			}
			if (holder != null)
			{
				return (DiscordBotLinkOutcome.InUse, holder);
			}

			DateTime now = DateTime.UtcNow;
			DiscordBotLink? existing = await db.DiscordBotLinks.AsTracking().FirstOrDefaultAsync(l => l.UserID == userID);
			Int64? previous = existing?.DiscordUserID;
			if (existing == null)
			{
				db.DiscordBotLinks.Add(new DiscordBotLink { UserID = userID, DiscordUserID = discordUserID, LinkedBy = actor.Name, LinkedAt = now });
			}
			else
			{
				existing.DiscordUserID = discordUserID;
				existing.LinkedBy = actor.Name;
				existing.LinkedAt = now;
			}
			db.AuditLog.Add(Moderation.Entry(actor, ModerationActions.DiscordBotLink, AuditTargetKinds.User, userID, null, new { discord_user_id = discordUserID.ToString(), previous = previous?.ToString() }, now));

			try
			{
				await db.SaveChangesAsync();
			}
			catch (DbUpdateException ex) when (DbHelpers.IsUniqueViolation(ex))
			{
				// another account took that Discord user meanwhile
				db.ChangeTracker.Clear();
				return (DiscordBotLinkOutcome.InUse, await UserOf(db, discordUserID));
			}

			return (DiscordBotLinkOutcome.Linked, null);
		}

		public static async Task<bool> Clear(AppDbContext db, Int64 userID, ModerationActor actor)
		{
			DiscordBotLink? existing = await db.DiscordBotLinks.AsTracking().FirstOrDefaultAsync(l => l.UserID == userID);
			if (existing == null)
			{
				return false;
			}

			db.DiscordBotLinks.Remove(existing);
			db.AuditLog.Add(Moderation.Entry(actor, ModerationActions.DiscordBotUnlink, AuditTargetKinds.User, userID, null, new { discord_user_id = existing.DiscordUserID.ToString() }, DateTime.UtcNow));
			await db.SaveChangesAsync();
			return true;
		}
	}
}
